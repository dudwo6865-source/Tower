using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Unit))]
[CanEditMultipleObjects]
public class UnitEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("data"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        if (GUILayout.Button("필요 컴포넌트 추가"))
        {
            foreach (Object targetObject in targets)
            {
                Unit unit = (Unit)targetObject;
                Undo.RegisterCompleteObjectUndo(unit.gameObject, "Setup Unit Components");
                unit.EnsureComponents(true, true, true);
            }
        }
    }

    void OnEnable()
    {
        foreach (Object targetObject in targets)
        {
            if (targetObject is Unit unit)
                RestoreComponentVisibility(unit);
        }
    }

    static void RestoreComponentVisibility(Unit unit)
    {
        RestoreFlag<SelectableEntity>(unit);
        RestoreFlag<EntityHealth>(unit);
        RestoreFlag<WorldHealthBar>(unit);
        RestoreFlag<UnitAttacker>(unit);
        RestoreFlag<UnitCombatAI>(unit);
        RestoreFlag<UnitMovement>(unit);
        RestoreFlag<UnitAnimator>(unit);
        RestoreFlag<UnitSound>(unit);
    }

    static void RestoreFlag<T>(Unit unit) where T : Component
    {
        T component = unit.GetComponent<T>();
        if (component == null || component.hideFlags == HideFlags.None)
            return;

        component.hideFlags = HideFlags.None;
        EditorUtility.SetDirty(component);
    }
}
