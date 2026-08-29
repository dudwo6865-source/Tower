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

        if (GUILayout.Button("Cliff Map Editor 열기"))
            EditorApplication.ExecuteMenuItem("Tools/Map/Cliff Map Editor");
    }
}
