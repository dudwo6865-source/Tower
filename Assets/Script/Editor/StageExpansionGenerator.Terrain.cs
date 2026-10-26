using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static partial class StageExpansionGenerator
{
    static readonly Vector2Int[] CardinalDirections = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    // 빈 칸을 저지대로 채우고 각 저지대의 양쪽에 고지대로 이어지는 언덕을 만든다.
    static void AddLowlandsAndHills(CliffPainter painter)
    {
        if (painter.tileSet == null || painter.tileSet.ground == null || painter.hillTileSet == null || painter.hillTileSet.hill == null)
            throw new InvalidOperationException("바닥과 언덕 타일 프리팹이 필요합니다.");
        painter.RebuildLookup();
        int minX = painter.tops.Min(t => t.cell.x), maxX = painter.tops.Max(t => t.cell.x);
        int minZ = painter.tops.Min(t => t.cell.z), maxZ = painter.tops.Max(t => t.cell.z);
        for (int x = minX - 1; x <= maxX + 1; x++)
            for (int z = minZ - 1; z <= maxZ + 1; z++)
                painter.TryAddGround(new CliffPainter.CellCoord(x, z));

        var floors = new HashSet<Vector2Int>(painter.groundCells
            .Where(c => !painter.IsEdgeSlot(c)).Select(c => new Vector2Int(c.x, c.z)));
        while (floors.Count > 0)
        {
            var region = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var first = floors.OrderBy(c => c.x).ThenBy(c => c.y).First();
            floors.Remove(first);
            queue.Enqueue(first);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                region.Add(cell);
                foreach (var direction in CardinalDirections)
                    if (floors.Remove(cell + direction)) queue.Enqueue(cell + direction);
            }

            var candidates = new List<(Vector2Int cell, int direction)>();
            foreach (var floor in region)
                for (int d = 0; d < CardinalDirections.Length; d++)
                {
                    var direction = CardinalDirections[d];
                    var slope = floor + direction;
                    var top = slope + direction;
                    var tangent = new Vector2Int(direction.y, -direction.x);
                    var slot = new CliffPainter.CellCoord(slope.x, slope.y);
                    // 모서리 대신 평평한 벽 가운데에 경사로를 두어 자동 연결 타일이 겹치지 않게 한다.
                    if (painter.HasTop(top.x, top.y)
                        && painter.HasTop(top.x + tangent.x, top.y + tangent.y)
                        && painter.HasTop(top.x - tangent.x, top.y - tangent.y)
                        && painter.CanPlaceHill(slot)) candidates.Add((slope, d));
                }
            if (candidates.Count < 2)
                throw new InvalidOperationException("양쪽 언덕을 배치할 수 없는 저지대가 있습니다: " + first);
            Vector2 center = new Vector2(region.Average(c => (float)c.x), region.Average(c => (float)c.y));
            var entry = candidates.OrderBy(c => ((Vector2)c.cell - center).sqrMagnitude).ThenBy(c => c.direction).First();
            var exits = candidates.Where(c => c.cell != entry.cell).ToList();
            var exit = exits.OrderBy(c => c.direction == (entry.direction + 2) % 4 ? 0 : 1)
                .ThenByDescending(c => (c.cell - entry.cell).sqrMagnitude).First();
            if (!painter.TryAddHill(new CliffPainter.CellCoord(entry.cell.x, entry.cell.y))
                || !painter.TryAddHill(new CliffPainter.CellCoord(exit.cell.x, exit.cell.y)))
                throw new InvalidOperationException("저지대 언덕 배치에 실패했습니다.");
        }
    }

    static void ValidateLowlandPaths(CliffPainter painter, Vector3 headquarters, int agentType)
    {
        foreach (var cell in painter.groundCells)
        {
            if (painter.IsEdgeSlot(cell) || painter.HasTop(cell)) continue;
            var tile = painter.generatedRoot.Find($"Ground_{cell.x}_{cell.z}");
            if (tile == null) throw new InvalidOperationException("저지대 바닥 메시가 누락되었습니다: " + cell);
            var renderers = tile.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("저지대 바닥 렌더러가 없습니다.");
            Vector3 point = tile.position;
            point.y = renderers.Max(r => r.bounds.max.y);
            try { ValidatePath(point, headquarters, agentType); }
            catch (Exception e) { throw new InvalidOperationException("저지대에서 본부로 올라갈 수 없습니다: " + cell, e); }
        }
    }

    [MenuItem("Tools/맵/추가 스테이지 5개를 저지대와 언덕으로 변경")]
    public static void UpgradeTerrain()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("플레이 모드를 종료한 후 실행하세요.");
        var roots = new List<GameObject>();
        var surfaces = new List<NavMeshSurface>();
        var originals = new List<NavMeshData>();
        var backups = new Dictionary<string, byte[]>();
        string backupFolder = "Temp/StageTerrainBackup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backupFolder);
        bool saving = false;
        try
        {
            for (int i = 0; i < Names.Length; i++)
            {
                string path = OutputFolder + "/" + (i + 2) + "Stage.prefab";
                if (!File.Exists(path)) throw new InvalidOperationException("추가 맵 프리팹이 없습니다: " + path);
                EditorUtility.DisplayProgressBar("저지대와 언덕 연결", Names[i], i / 5f);
                var root = PrefabUtility.LoadPrefabContents(path);
                roots.Add(root);
                var painter = root.GetComponent<CliffPainter>();
                var surface = root.GetComponent<NavMeshSurface>();
                if (painter == null || surface == null || surface.navMeshData == null)
                    throw new InvalidOperationException("맵 지형 또는 경로 데이터가 없습니다: " + path);
                // 지형 생성 부모 아래의 수동 배치물은 재생성 전에 보존한다.
                if (painter.generatedRoot != null)
                    foreach (var marker in painter.generatedRoot.GetComponentsInChildren<StageBuildingSpawnPoint>(true))
                        marker.transform.SetParent(root.transform, true);
                var original = surface.navMeshData;
                string navPath = AssetDatabase.GetAssetPath(original);
                foreach (var file in new[] { path, navPath })
                {
                    if (string.IsNullOrEmpty(file) || !File.Exists(file)) throw new InvalidOperationException("저장된 맵 에셋이 필요합니다.");
                    backups[file] = File.ReadAllBytes(file);
                    File.Copy(file, backupFolder + "/" + Path.GetFileName(file));
                }
                // 기존 연결을 지우고 같은 규칙으로 다시 생성해 반복 실행 결과도 동일하게 한다.
                painter.hills.Clear();
                painter.hillCells.Clear();
                painter.RebuildLookup();
                AddLowlandsAndHills(painter);
                painter.RebuildGeometry();
                surface.RemoveData();
                surface.navMeshData = null;
                surface.collectObjects = CollectObjects.Children;
                root.transform.position = new Vector3(10000, 0, 10000);
                surface.BuildNavMesh();
                if (surface.navMeshData == null) throw new InvalidOperationException("경로 굽기에 실패했습니다.");
                surfaces.Add(surface);
                originals.Add(original);
                var navigation = NavMesh.AddNavMeshData(surface.navMeshData, root.transform.position, root.transform.rotation);
                try
                {
                    var markers = root.GetComponentsInChildren<StageBuildingSpawnPoint>(true);
                    var hq = markers.First(m => m.buildingPrefab != null && m.buildingPrefab.GetComponentInChildren<Headquarters>(true) != null);
                    foreach (var marker in markers)
                        if (marker.buildingPrefab != null && marker.buildingPrefab.GetComponentInChildren<EnemySpawner>(true) != null)
                            ValidatePath(marker.transform.position, hq.transform.position, surface.agentTypeID);
                    ValidateLowlandPaths(painter, hq.transform.position, surface.agentTypeID);
                }
                finally { navigation.Remove(); }
                root.transform.position = Vector3.zero;
            }
            // 다섯 맵이 모두 경로 검사를 통과한 뒤 기존 GUID를 유지하며 저장한다.
            saving = true;
            for (int i = 0; i < roots.Count; i++)
            {
                var built = surfaces[i].navMeshData;
                EditorUtility.CopySerialized(built, originals[i]);
                EditorUtility.SetDirty(originals[i]);
                surfaces[i].navMeshData = originals[i];
                UnityEngine.Object.DestroyImmediate(built);
                if (PrefabUtility.SaveAsPrefabAsset(roots[i], OutputFolder + "/" + (i + 2) + "Stage.prefab") == null)
                    throw new InvalidOperationException("맵 프리팹 저장에 실패했습니다.");
                AssetDatabase.SaveAssetIfDirty(originals[i]);
            }
            Debug.Log("추가 맵 5개의 저지대·언덕·벽 연결 및 전체 경로 검증을 완료했습니다. 원본 백업: " + backupFolder);
        }
        catch
        {
            if (saving)
            {
                foreach (var backup in backups) File.WriteAllBytes(backup.Key, backup.Value);
                foreach (var file in backups.Keys) AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate);
            }
            throw;
        }
        finally
        {
            foreach (var surface in surfaces)
                if (surface != null && surface.navMeshData != null && !EditorUtility.IsPersistent(surface.navMeshData))
                    UnityEngine.Object.DestroyImmediate(surface.navMeshData);
            foreach (var root in roots) PrefabUtility.UnloadPrefabContents(root);
            EditorUtility.ClearProgressBar();
        }
    }
}
