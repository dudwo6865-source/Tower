using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// UnitSound 인스펙터. 클립 배열은 원소가 "클립+볼륨" 묶음이라 유니티 기본 기능으로는
// 배열 제목에 클립을 끌어다 놓아 추가할 수 없으므로, 배열마다 끌어다 놓기 칸을 따로 그린다.
[CustomEditor(typeof(UnitSound))]
[CanEditMultipleObjects]
public class UnitSoundEditor : Editor
{
    const float DropAreaHeight = 24f;

    static readonly Dictionary<string, string> ClipArrayLabels = new Dictionary<string, string>
    {
        { "attackSounds", "공격 사운드" },
        { "attackHitSounds", "명중 사운드" },
        { "deathSounds", "사망 사운드" },
        { "selectSounds", "선택 사운드" },
        { "productionCompleteSounds", "생산 완료 사운드" },
        { "attackOrderSounds", "공격 명령 사운드" },
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(property, true);

                continue;
            }

            if (ClipArrayLabels.TryGetValue(property.propertyPath, out string labelText))
            {
                EditorGUILayout.PropertyField(
                    property,
                    new GUIContent(labelText, property.tooltip),
                    true);

                DrawClipDropArea(property.Copy(), labelText);
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    static void DrawClipDropArea(SerializedProperty array, string labelText)
    {
        Rect area = GUILayoutUtility.GetRect(0f, DropAreaHeight, GUILayout.ExpandWidth(true));
        area = EditorGUI.IndentedRect(area);

        GUI.Box(
            area,
            $"오디오 클립을 여기로 끌어다 놓으면 '{labelText}'에 추가합니다",
            EditorStyles.helpBox);

        Event current = Event.current;

        if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
            return;

        if (!area.Contains(current.mousePosition))
            return;

        List<AudioClip> clips = new List<AudioClip>();

        foreach (Object dragged in DragAndDrop.objectReferences)
        {
            if (dragged is AudioClip clip)
                clips.Add(clip);
        }

        if (clips.Count == 0)
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

        if (current.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();

            foreach (AudioClip clip in clips)
            {
                int index = array.arraySize;
                array.arraySize++;

                SerializedProperty element = array.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("clip").objectReferenceValue = clip;
                element.FindPropertyRelative("volume").floatValue = 1f;
                element.FindPropertyRelative("initialized").boolValue = true;
            }

            array.isExpanded = true;
        }

        current.Use();
    }
}
