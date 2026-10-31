using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 별도 임시 유니티 프로젝트에서 실제 Undo와 프리팹 재생성을 검증한다.
public static class CliffPainterRegressionTests
{
    static int checks;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    public static void RunBatch()
    {
        try
        {
            Run();
            Debug.Log("CLIFF_TEST_PASS " + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static CliffPainter.CellCoord Cell(int x, int z) => new CliffPainter.CellCoord(x, z);
    static HashSet<CliffPainter.CellCoord> Ground(CliffPainter p) => new HashSet<CliffPainter.CellCoord>(p.groundCells);

    static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(primitive, "Assets/TestTile.prefab");
        UnityEngine.Object.DestroyImmediate(primitive);
        var set = ScriptableObject.CreateInstance<CliffTileSet>();
        set.ground = set.top = set.straight = set.outerCorner = set.innerCorner = prefab;
        set.cliffHeight = 2f;
        set.tileSize = 1f;
        var p = new GameObject("검증 지형").AddComponent<CliffPainter>();
        p.tileSet = set;
        p.RebuildLookup();
        Check(!p.TryAddTop(Cell(0, 0)), "빈 칸에는 Top을 만들지 않는다");
        Check(!p.TryAddTop(Cell(0, 0), -2), "빈 칸에는 지하 Top도 만들지 않는다");
        p.FillGround(Cell(-3, -3), 7, false);
        p.RebuildGeometry(false);
        var baseline = Ground(p);
        CliffPainterUndo.Track(p);

        int group = CliffPainterUndo.Begin(p, "회귀 검증 칠하기");
        Check(p.TryAddTop(Cell(0, 0)), "바닥 위 Top 배치");
        Check(p.TryAddTop(Cell(1, 0)), "같은 스트로크 두 번째 칸 배치");
        Check(p.TryAddTop(Cell(2, 0)), "같은 스트로크 세 번째 칸 배치");
        p.RebuildGeometry(false);
        CliffPainterUndo.MarkDirty(p);
        CliffPainterUndo.End(group);
        Check(p.TopCount == 3 && p.groundCells.Count == baseline.Count - 3, "Top이 바닥 데이터를 대체한다");
        Undo.PerformUndo();
        Check(p.TopCount == 0 && baseline.SetEquals(Ground(p)), "한 번의 Undo로 모든 원래 바닥을 정확히 복원한다");
        Check(p.generatedRoot.childCount == baseline.Count, "창을 열지 않아도 바닥 메시가 모두 복원된다");
        Undo.PerformRedo();
        Check(p.TopCount == 3 && p.groundCells.Count == baseline.Count - 3, "Redo로 Top과 바닥 목록을 함께 복원한다");
        Check(p.generatedRoot.Find("Top_L0_0_0") != null, "Redo가 실제 Top 메시도 복원한다");
        Undo.PerformUndo();
        Check(baseline.SetEquals(Ground(p)), "반복 Undo에서도 바닥을 잃지 않는다");

        group = CliffPainterUndo.Begin(p, "두 번째 스트로크");
        p.TryAddTop(Cell(-2, -2));
        p.RebuildGeometry(false);
        CliffPainterUndo.MarkDirty(p);
        CliffPainterUndo.End(group);
        group = CliffPainterUndo.Begin(p, "세 번째 스트로크");
        p.TryAddTop(Cell(2, 2));
        p.RebuildGeometry(false);
        CliffPainterUndo.MarkDirty(p);
        CliffPainterUndo.End(group);
        Undo.PerformUndo();
        Check(p.HasTop(Cell(-2, -2)) && !p.HasTop(Cell(2, 2)) && p.HasGround(Cell(2, 2)), "별도 스트로크는 섞이지 않는다");
        Undo.PerformUndo();
        Check(baseline.SetEquals(Ground(p)), "이전 스트로크도 정확히 복원한다");

        p.TryAddTop(Cell(0, 0));
        Check(p.TryAddHill(Cell(0, 1)), "바닥 위 언덕 배치");
        Check(!p.HasGround(Cell(0, 1)), "언덕이 바닥을 대체한다");
        p.TryRemoveTop(Cell(0, 0));
        Check(!p.HasHill(Cell(0, 1)) && p.HasGround(Cell(0, 1)), "연결할 Top이 없어진 언덕의 바닥도 복구한다");
        p.TryAddTop(Cell(0, 0));
        p.TryAddHill(Cell(0, 1));
        p.ClearTopsAndHills();
        Check(p.TopCount == 0 && p.hills.Count == 0 && baseline.SetEquals(Ground(p)), "Top/Hill만 지우기는 전체 바닥 면적을 유지한다");

        p.TryAddTop(Cell(0, 0), -2);
        Check(p.TryRemoveTop(Cell(0, 0), 0) && p.HasGround(Cell(0, 0)), "기본 Top 지우개로 지하 Top도 지우고 바닥을 복원한다");
        p.TryAddTop(Cell(0, 0), -2);
        Check(p.TryAddTop(Cell(0, 0), -1), "기존 지하 Top의 높이를 변경할 수 있다");
        Check(p.TryAddTop(Cell(0, 0), 0), "기존 지하 Top을 높일 수 있다");
        Check(!p.TryAddTop(Cell(0, 0), 1), "아랫층 이웃이 없는 윗층은 거절한다");
        p.ClearTopsAndHills();
        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++) p.TryAddTop(Cell(x, z));
        Check(p.TryAddTop(Cell(0, 0), 1), "지지되는 안쪽 칸은 윗층을 허용한다");
        Check(!p.TryAddTop(Cell(1, 0), 1), "가장자리 윗층은 허용하지 않는다");
        p.ClearTopsAndHills();
        p.TryAddTop(Cell(0, 0));
        Check(p.TrySetRamp(Cell(0, 0), 0), "바닥으로 연결되는 램프 허용");
        Check(!p.TrySetRamp(Cell(0, 0), 0), "같은 램프 재배치는 변경하지 않는다");
        p.TryEraseGroundStack(Cell(0, 1));
        Check(!p.TrySetRamp(Cell(0, 0), 0), "바닥 없는 방향의 램프 거절");
        p.FillGround(Cell(5, 5), 2, false);
        Check(!p.TryAddTop(Cell(7, 5)), "맵 경계를 넘어 칠하지 않는다");

        p.RebuildGeometry(false);
        CliffPainterUndo.MarkDirty(p);
        int generatedId = p.generatedRoot.GetChild(0).GetInstanceID();
        var unrelated = new GameObject("다른 오브젝트");
        Undo.RecordObject(unrelated.transform, "다른 작업");
        unrelated.transform.position = Vector3.one;
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Check(p.generatedRoot.GetChild(0).GetInstanceID() == generatedId, "다른 오브젝트 Undo는 맵을 재생성하지 않는다");
        Check(p.generatedRoot.name == "지형", "자동 생성 부모 이름은 지형이다");
        Transform originalRoot = p.generatedRoot;
        originalRoot.name = "Generated";
        p.generatedRoot = null;
        Check(p.EnsureGeneratedRoot() == originalRoot && originalRoot.name == "지형", "기존 Generated를 중복 없이 재사용한다");
        originalRoot.SetParent(null, true);
        p.generatedRoot = originalRoot;
        Check(p.EnsureGeneratedRoot() == originalRoot && originalRoot.parent == p.transform, "외부 전용 부모도 기존 타일째 재사용한다");
        p.generatedRoot = p.transform;
        Check(p.EnsureGeneratedRoot() == originalRoot, "자기 자신이 연결되어도 기존 전용 자식을 선택한다");
        var otherPainter = new GameObject("다른 지형").AddComponent<CliffPainter>();
        var mapRoot = new GameObject("맵 루트");
        otherPainter.transform.SetParent(mapRoot.transform);
        var building = new GameObject("보존할 본부");
        building.transform.SetParent(mapRoot.transform);
        otherPainter.generatedRoot = mapRoot.transform;
        otherPainter.tileSet = set;
        otherPainter.RebuildGeometry(false);
        Check(otherPainter.generatedRoot.name == "지형" && otherPainter.generatedRoot.parent == otherPainter.transform &&
            building != null && building.transform.parent == mapRoot.transform, "잘못 연결된 맵 루트 대신 전용 부모를 만들고 본부는 보존한다");
        VerifyEdgeCombinations(set);
    }

    static void VerifyEdgeCombinations(CliffTileSet set)
    {
        var painter = new GameObject("경계 조합 검증").AddComponent<CliffPainter>();
        painter.tileSet = set;
        var neighbors = new[] { Cell(0, 1), Cell(1, 1), Cell(1, 0), Cell(1, -1),
            Cell(0, -1), Cell(-1, -1), Cell(-1, 0), Cell(-1, 1) };
        for (int mask = 0; mask < 256; mask++)
        {
            painter.ClearTopsAndHills();
            painter.FillGround(Cell(-3, -3), 7, false);
            for (int i = 0; i < neighbors.Length; i++)
                if ((mask & (1 << i)) != 0) painter.TryAddTop(neighbors[i]);
            painter.EnsureGeneratedRoot();
            string before = EditorJsonUtility.ToJson(painter);
            painter.RebuildGeometry(false);
            var slots = new HashSet<CliffPainter.CellCoord>();
            foreach (Transform tile in painter.generatedRoot)
            {
                var cell = painter.WorldToCell(tile.position);
                if (!slots.Add(cell)) throw new Exception("경계 중복: " + mask + " / " + tile.name);
            }
            Check(slots.Count == 49, "주변 8칸 조합의 바닥·상판·경계에 빈 칸이 없다: " + mask);
            Check(EditorJsonUtility.ToJson(painter) == before, "자동 연결이 칠 데이터를 변경하지 않는다: " + mask);
            if (mask == 17 || mask == 34 || mask == 255 || mask == 7)
            {
                var names = GeometryNames(painter);
                painter.tops.Reverse();
                painter.RebuildGeometry(false);
                Check(names.SetEquals(GeometryNames(painter)), "칠 순서를 뒤집어도 같은 모듈과 방향을 생성한다: " + mask);
            }
            Check(painter.generatedRoot.Find("Top_L0_0_0") == null,
                "칠하지 않은 중앙 칸을 자동 상판으로 메우지 않는다: " + mask);
            if (mask == 17)
                Check(painter.generatedRoot.Find("Junction_L0_0_0/벽_0") != null &&
                    painter.generatedRoot.Find("Junction_L0_0_0/벽_2") != null,
                    "마주 보는 두 벽을 빠뜨리지 않고 함께 생성한다");
            if (mask == 7)
                Check(painter.generatedRoot.Find("Top_L0_0_0") == null,
                    "정상적인 단일 안쪽 코너는 상판으로 메우지 않는다");
        }
        painter.ClearTopsAndHills();
        painter.FillGround(Cell(-3, -3), 7, false);
        painter.TryAddTop(Cell(0, 1));
        painter.RebuildGeometry(false);
        Check(painter.generatedRoot.Find("Top_L0_0_0") == null, "접점이 사라지면 자동 상판도 사라진다");
        foreach (int layer in new[] { -1, 1 })
        {
            painter.ClearTopsAndHills();
            painter.groundCells.Clear();
            for (int x = -3; x <= 3; x++)
                for (int z = -3; z <= 3; z++)
                    painter.tops.Add(new CliffPainter.TopEntry { cell = Cell(x, z), maxLayer = layer - 1 });
            painter.tops.Add(new CliffPainter.TopEntry { cell = Cell(0, 1), maxLayer = layer });
            painter.tops.Add(new CliffPainter.TopEntry { cell = Cell(0, -1), maxLayer = layer });
            painter.RebuildLookup();
            painter.RebuildGeometry(false);
            Check(painter.generatedRoot.Find("Top_L" + layer + "_0_0") == null &&
                painter.generatedRoot.Find("Junction_L" + layer + "_0_0/낮은 바닥") != null,
                "지하·윗층에서도 충돌 칸을 낮게 유지한다: " + layer);
            Check(painter.GetTopMaxLayer(Cell(0, 0)) == layer - 1,
                "생성 후 원래 지하·윗층 높이 조회를 복원한다: " + layer);
        }
        painter.ClearTopsAndHills();
        painter.FillGround(Cell(-3, -3), 7, false);
        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++) painter.TryAddTop(Cell(x, z));
        painter.RebuildGeometry(false);
        int group = CliffPainterUndo.Begin(painter, "한 칸 지우기 검증");
        Check(painter.TryRemoveTop(Cell(0, 0)), "둘러싸인 중앙 한 칸도 지워진다");
        painter.RebuildGeometry(false);
        CliffPainterUndo.MarkDirty(painter);
        CliffPainterUndo.End(group);
        Check(painter.TopCount == 8 && painter.generatedRoot.Find("Top_L0_0_0") == null &&
            painter.generatedRoot.Find("Junction_L0_0_0/낮은 바닥") != null,
            "한 칸을 지운 뒤 주변 여덟 칸을 보존하고 낮은 바닥을 생성한다");
        Transform pit = painter.generatedRoot.Find("Junction_L0_0_0");
        Check(pit.childCount == 5, "한 칸 구덩이에 바닥과 네 방향 벽을 생성한다");
        Physics.SyncTransforms();
        var ray = new Ray(pit.position + Vector3.up * 100f, Vector3.down);
        foreach (Transform part in pit)
        {
            bool hit = part.GetComponentInChildren<Collider>().Raycast(ray, out RaycastHit info, 200f);
            Check(hit == (part.name == "낮은 바닥"), "중앙 광선은 벽 대신 낮은 바닥에만 닿는다: " + part.name);
        }
        Undo.PerformUndo();
        Check(painter.TopCount == 9 && painter.generatedRoot.Find("Top_L0_0_0") != null,
            "지우기 Undo는 중앙 상판을 복원한다");
        Undo.PerformRedo();
        Check(painter.TopCount == 8 && painter.generatedRoot.Find("Top_L0_0_0") == null,
            "지우기 Redo는 중앙 상판을 다시 제거한다");
    }

    static HashSet<string> GeometryNames(CliffPainter painter)
    {
        var result = new HashSet<string>();
        foreach (Transform tile in painter.generatedRoot.GetComponentsInChildren<Transform>())
            result.Add(tile.name + ":" + tile.localPosition + ":" + tile.localEulerAngles.y + ":" + tile.localScale);
        return result;
    }
}
