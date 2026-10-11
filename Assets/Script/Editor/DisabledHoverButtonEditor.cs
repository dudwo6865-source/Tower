using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

/// <summary>
/// 기본 Button 인스펙터는 파생 클래스의 필드를 그리지 않으므로, '잠겨도 호버 표시'를 덧붙여 그립니다.
/// </summary>
[CustomEditor(typeof(DisabledHoverButton), true)]
[CanEditMultipleObjects]
public class DisabledHoverButtonEditor : ButtonEditor
{
    SerializedProperty hoverWhenDisabled;

    protected override void OnEnable()
    {
        base.OnEnable();
        hoverWhenDisabled = serializedObject.FindProperty(nameof(DisabledHoverButton.hoverWhenDisabled));
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(
            hoverWhenDisabled,
            new GUIContent("잠겨도 호버 표시", hoverWhenDisabled.tooltip));
        serializedObject.ApplyModifiedProperties();
    }
}
