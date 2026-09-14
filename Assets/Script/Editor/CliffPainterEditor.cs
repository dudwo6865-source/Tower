using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CliffPainter))]
public class CliffPainterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CliffPainter painter = (CliffPainter)target;

        EditorGUILayout.Space(8f);

        if (GUILayout.Button("가장자리 재생성", GUILayout.Height(28f)))
        {
            Undo.RecordObject(painter, "Rebuild Cliffs");
            painter.RebuildGeometry();
            EditorUtility.SetDirty(painter);
        }

        if (GUILayout.Button("절벽 맵 에디터 열기"))
            EditorApplication.ExecuteMenuItem("Tools/맵/절벽 맵 에디터");
    }
}
