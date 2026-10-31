using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 지형 목록만 실행 취소의 원본으로 삼고 생성된 타일은 복원된 데이터로 다시 만든다.
// 창이 닫혀 있어도 실행 취소와 다시 실행이 화면에 반영된다.
[InitializeOnLoad]
public static class CliffPainterUndo
{
    [Serializable]
    sealed class TerrainState
    {
        public CliffTileSet tileSet;
        public HillTileSet hillTileSet;
        public Vector3 origin;
        public float height;
        public bool normals;
        public float distance;
        public float angle;
        public float junctionWidth;
        public List<CliffPainter.TopEntry> tops;
        public List<CliffPainter.CellCoord> legacyTops;
        public List<CliffPainter.HillEntry> hills;
        public List<CliffPainter.CellCoord> legacyHills;
        public List<CliffPainter.CellCoord> ground;
        public List<CliffPainter.RampEntry> ramps;
    }

    static readonly Dictionary<int, string> states = new Dictionary<int, string>();

    static CliffPainterUndo()
    {
        Undo.undoRedoPerformed += RestoreGeometry;
        EditorApplication.delayCall += TrackLoadedPainters;
    }

    static string Capture(CliffPainter painter) => JsonUtility.ToJson(new TerrainState
    {
        tileSet = painter.tileSet, hillTileSet = painter.hillTileSet,
        origin = painter.gridOrigin, height = painter.baseHeight,
        normals = painter.connectTileNormals, distance = painter.normalJoinDistance, angle = painter.normalJoinAngle,
        junctionWidth = painter.junctionWallWidth,
        tops = painter.tops, legacyTops = painter.topCells, hills = painter.hills,
        legacyHills = painter.hillCells, ground = painter.groundCells, ramps = painter.ramps
    });

    public static void Track(CliffPainter painter)
    {
        if (painter != null) states[painter.GetInstanceID()] = Capture(painter);
    }

    static void TrackLoadedPainters()
    {
        foreach (CliffPainter painter in Resources.FindObjectsOfTypeAll<CliffPainter>())
            if (painter.gameObject.scene.IsValid()) Track(painter);
    }

    public static int Begin(CliffPainter painter, string name)
    {
        painter.EnsureLookup();
        Undo.FlushUndoRecordObjects();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(name);
        // 여러 목록에서 삭제·삽입이 함께 일어나므로 차이 대신 작업 전 전체 목록을 보존한다.
        Undo.RegisterCompleteObjectUndo(painter, name);
        Track(painter);
        return group;
    }

    public static void End(int group)
    {
        if (group < 0) return;
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(group);
        Undo.IncrementCurrentGroup();
    }

    public static void MarkDirty(CliffPainter painter)
    {
        EditorUtility.SetDirty(painter);
        if (PrefabUtility.IsPartOfPrefabInstance(painter))
            PrefabUtility.RecordPrefabInstancePropertyModifications(painter);
        if (painter.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(painter.gameObject.scene);
        Track(painter);
    }

    static void RestoreGeometry()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (CliffPainter painter in Resources.FindObjectsOfTypeAll<CliffPainter>())
        {
            if (!painter.gameObject.scene.IsValid() || EditorUtility.IsPersistent(painter)) continue;
            string state = Capture(painter);
            if (states.TryGetValue(painter.GetInstanceID(), out string previous) && state == previous) continue;
            painter.InvalidateLookup();
            // 실행 취소로 되돌린 원본 데이터는 정리하거나 다시 바꾸지 않는다.
            painter.RebuildGeometry(false);
            MarkDirty(painter);
        }
        SceneView.RepaintAll();
    }
}
