using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CliffPainter))]
public class CliffPainterEditor : Editor
{
    void OnEnable() => CliffPainterUndo.Track((CliffPainter)target);

    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector()) CliffPainterUndo.MarkDirty((CliffPainter)target);

        CliffPainter painter = (CliffPainter)target;

        EditorGUILayout.Space(8f);

        if (GUILayout.Button("가장자리 재생성", GUILayout.Height(28f)))
        {
            int group = CliffPainterUndo.Begin(painter, "절벽 재생성 및 층 정리");
            painter.RebuildGeometry();
            CliffPainterUndo.MarkDirty(painter);
            CliffPainterUndo.End(group);
        }

        if (GUILayout.Button("맵 생성기 열기"))
            EditorApplication.ExecuteMenuItem("Tools/맵/맵 생성기");
    }
}
