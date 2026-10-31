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
    }
}
