using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// 원본 스테이지를 보존하고 독립적인 절벽 맵과 경로 데이터를 생성한다.
public static class StageExpansionGenerator
{
    const string SourcePath = "Assets/Data/Stage/1Stage.asset";
    const string OutputFolder = "Assets/Data/Stage/Expansion";
    static readonly string[] Names = { "갈림길 전초기지", "협곡 우회로", "삼중 침공로", "분산 전선", "포위망" };
    static readonly int[] Widths = { 33, 34, 35, 36, 36 };
    static readonly int[] Heights = { 20, 21, 22, 23, 24 };

    [MenuItem("Tools/맵/1Stage 기반 추가 스테이지 5개 생성")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("플레이 모드를 종료한 후 생성하세요.");
        var source = AssetDatabase.LoadAssetAtPath<MapConfig>(SourcePath);
        if (source == null || source.mapRootPrefab == null)
            throw new InvalidOperationException("1Stage의 맵 루트 프리팹이 필요합니다.");
        // 기존 결과를 덮어쓰지 않아 수동 편집과 저장된 스테이지 기록을 보호한다.
        if (AssetDatabase.IsValidFolder(OutputFolder))
            throw new InvalidOperationException("추가 스테이지 폴더가 이미 있습니다. 기존 결과를 보존하기 위해 생성을 중단합니다.");
        var template = source.mapRootPrefab.GetComponent<CliffPainter>();
        var markers = source.mapRootPrefab.GetComponentsInChildren<StageBuildingSpawnPoint>(true);
        var hq = markers.First(m => m.buildingPrefab != null && m.buildingPrefab.GetComponentInChildren<Headquarters>(true) != null);
        var spawner = markers.First(m => m.buildingPrefab != null && m.buildingPrefab.GetComponentInChildren<EnemySpawner>(true) != null);
        if (template == null || template.tileSet == null)
            throw new InvalidOperationException("원본 맵의 절벽 타일 세트가 필요합니다.");
        AssetDatabase.CreateFolder("Assets/Data/Stage", "Expansion");
        var report = new List<string> { "1Stage 기반 추가 맵 생성 결과", "면적은 외곽 절벽 한 칸을 포함한 격자 경계 기준입니다.", "경제, 웨이브, 초기 적, 승리 조건, 보상은 원본 데이터에서 복사합니다." };
        var cells = template.tops.Select(t => t.cell).Concat(template.groundCells).ToArray();
        int originalArea = (cells.Max(c => c.x) - cells.Min(c => c.x) + 1) * (cells.Max(c => c.z) - cells.Min(c => c.z) + 1);
        try
        {
            for (int i = 0; i < Names.Length; i++)
                GenerateOne(source, template, hq, spawner, i, originalArea, report);
            File.WriteAllLines(OutputFolder + "/생성결과.txt", report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("추가 스테이지 5개 생성 및 스포너→본부 경로 검증 완료: " + OutputFolder);
        }
        catch
        {
            // 이번 실행에서 새로 만든 결과만 정리하여 실패 후 다시 생성할 수 있게 한다.
            AssetDatabase.DeleteAsset(OutputFolder);
            throw;
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static void GenerateOne(MapConfig source, CliffPainter template, StageBuildingSpawnPoint hqTemplate,
        StageBuildingSpawnPoint spawnerTemplate, int index, int originalArea, List<string> report)
    {
        string id = (index + 2) + "Stage";
        EditorUtility.DisplayProgressBar("추가 스테이지 생성", Names[index], index / 5f);
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject(id);
        SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            int width = Widths[index] - 2, height = Heights[index] - 2;
            var painter = root.AddComponent<CliffPainter>();
            painter.tileSet = template.tileSet;
            painter.hillTileSet = template.hillTileSet;
            painter.baseHeight = template.baseHeight;
            // 본부를 원점 근처에 두어 기존 전투 카메라의 시작 위치와 맞춘다.
            painter.gridOrigin = new Vector3(-2.5f * painter.TileSize, 0, 61f - (height / 2 + .5f) * painter.TileSize);
            var walkable = new HashSet<Vector2Int>();
            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                {
                    if (IsGap(x, z, width, height, index)) continue;
                    painter.tops.Add(new CliffPainter.TopEntry { cell = new CliffPainter.CellCoord(x, z), maxLayer = 0 });
                    walkable.Add(new Vector2Int(x, z));
                }
            ValidateConnected(walkable);
            painter.RebuildGeometry();
            var markerRoot = new GameObject("건물 배치");
            markerRoot.transform.SetParent(root.transform, false);
            var hqCell = new Vector2Int(2, height / 2);
            var hq = CreateMarker(hqTemplate, markerRoot.transform, painter, hqCell, "본부");
            var enemies = new List<StageBuildingSpawnPoint>();
            // 동쪽 주력과 남북 우회 부대를 단계적으로 늘린다.
            var positions = new[] {
                new Vector2Int(width - 3, 2), new Vector2Int(width - 3, height / 2),
                new Vector2Int(width - 3, height - 3), new Vector2Int(width - 8, 2),
                new Vector2Int(width - 8, height - 3), new Vector2Int(width / 2, 2),
                new Vector2Int(width / 2, height - 3), new Vector2Int(7, 2), new Vector2Int(7, height - 3)
            };
            for (int n = 0; n < index + 5; n++)
            {
                if (!walkable.Contains(positions[n])) throw new InvalidOperationException("스포너 위치에 지형이 없습니다.");
                enemies.Add(CreateMarker(spawnerTemplate, markerRoot.transform, painter, positions[n], "적 스포너 " + (n + 1)));
            }
            var surface = root.AddComponent<NavMeshSurface>();
            var sourceSurface = source.mapRootPrefab.GetComponentInChildren<NavMeshSurface>(true);
            if (sourceSurface != null) EditorUtility.CopySerialized(sourceSurface, surface);
            surface.RemoveData();
            surface.navMeshData = null;
            surface.collectObjects = CollectObjects.Children;
            var map = root.AddComponent<MapRoot>();
            map.navMeshSurface = surface;
            map.bakeNavMeshOnStart = false;
            // 현재 열려 있는 씬의 경로 데이터와 겹치지 않는 곳에서 검증한다.
            root.transform.position = new Vector3(10000, 0, 10000);
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("경로 데이터 생성에 실패했습니다.");
            // 미리보기 씬의 Surface는 자동 등록되지 않으므로 검증 동안만 직접 등록한다.
            var navigation = NavMesh.AddNavMeshData(surface.navMeshData, root.transform.position, root.transform.rotation);
            try
            {
                foreach (var enemy in enemies) ValidatePath(enemy.transform.position, hq.transform.position, surface.agentTypeID);
            }
            finally { navigation.Remove(); }
            surface.RemoveData();
            root.transform.position = Vector3.zero;
            AssetDatabase.CreateAsset(surface.navMeshData, OutputFolder + "/" + id + "-NavMesh.asset");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, OutputFolder + "/" + id + ".prefab");
            var config = UnityEngine.Object.Instantiate(source);
            config.name = id;
            config.displayName = Names[index];
            config.description = $"난이도 {index + 2} · 적 스포너 {enemies.Count}개 · 기준 맵 면적의 {(float)(Widths[index] * Heights[index]) / originalArea:0.00}배. "
                + new[] { "중앙 절벽을 사이에 둔 양방향 우회로.", "엇갈린 절벽 사이로 이어지는 협곡과 외곽 우회로.", "두 절벽 사이 중앙 통로와 남북 외곽 통로.", "연속된 협곡을 넘나드는 다중 방어선.", "중앙 분기와 남북 전진 스포너를 동시에 방어하는 포위 전선." }[index];
            config.mapRootPrefab = prefab;
            config.previewImage = null;
            AssetDatabase.CreateAsset(config, OutputFolder + "/" + id + ".asset");
            var serialized = new SerializedObject(config);
            serialized.FindProperty("stageId").stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            report.Add($"{id}: {Names[index]}, {Widths[index]}×{Heights[index]}칸, 면적 {(float)(Widths[index] * Heights[index]) / originalArea:0.00}배, 스포너 {enemies.Count}개, 모든 경로 연결 확인");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static bool IsGap(int x, int z, int w, int h, int index)
    {
        // 남북 세 칸의 우회로와 본부 앞 건설 공간을 항상 남긴다.
        if (z < 4 || z >= h - 4 || x < 8 || x >= w - 9) return false;
        if (index == 0) return x >= w / 2 - 1 && x <= w / 2 + 1 && z >= 5 && z < h - 5;
        if (index == 1) return (x >= 10 && x <= 12 && z < h / 2 + 1) || (x >= 18 && x <= 20 && z > h / 2 - 2);
        if (index == 2) return x >= 12 && x <= 16 && (z < h / 2 - 2 || z > h / 2 + 2);
        if (index == 3) return (x >= 9 && x <= 11 && z < h / 2 + 1) || (x >= 17 && x <= 19 && z > h / 2 - 2) || (x >= 23 && z < h / 2);
        return (x >= 10 && x <= 12 || x >= 19 && x <= 21) && (z < h / 2 - 2 || z > h / 2 + 2);
    }

    static StageBuildingSpawnPoint CreateMarker(StageBuildingSpawnPoint source, Transform parent, CliffPainter painter, Vector2Int cell, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = painter.CellCenterWorldAtTop(new CliffPainter.CellCoord(cell.x, cell.y), 0)
            + Vector3.up * painter.tileSet.topHeightOffset;
        var marker = go.AddComponent<StageBuildingSpawnPoint>();
        EditorUtility.CopySerialized(source, marker);
        return marker;
    }

    static void ValidateConnected(HashSet<Vector2Int> cells)
    {
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        var first = cells.First();
        queue.Enqueue(first);
        visited.Add(first);
        var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var direction in directions)
                if (cells.Contains(cell + direction) && visited.Add(cell + direction)) queue.Enqueue(cell + direction);
        }
        if (visited.Count != cells.Count) throw new InvalidOperationException("고립된 지형이 발견되었습니다.");
    }

    static void ValidatePath(Vector3 from, Vector3 to, int agentType)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
        var path = new NavMeshPath();
        if (!NavMesh.SamplePosition(from, out var start, 4f, filter) || !NavMesh.SamplePosition(to, out var end, 4f, filter)
            || !NavMesh.CalculatePath(start.position, end.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
            throw new InvalidOperationException("스포너에서 본부로 이어지는 이동 경로가 없습니다.");
    }
}
