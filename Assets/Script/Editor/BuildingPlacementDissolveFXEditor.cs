using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BuildingPlacementDissolveFX))]
[CanEditMultipleObjects]
public class BuildingPlacementDissolveFXEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        GUI.enabled = Application.isPlaying;

        if (GUILayout.Button("연출 재생"))
        {
            foreach (Object targetObject in targets)
            {
                if (targetObject is BuildingPlacementDissolveFX fx)
                    fx.Play();
            }
        }

        GUI.enabled = true;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "코루틴으로 재생되는 연출이라 Play 모드에서만 재생됩니다. " +
                "이 오브젝트를 씬에 두고 Play를 누른 뒤 버튼을 눌러주세요.",
                MessageType.Info);
        }
    }
}
