using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Building))]
[CanEditMultipleObjects]
public class BuildingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("data"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isHeadquarters"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useTowerAI"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isProductionBuilding"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("productionRecipe"));

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        if (GUILayout.Button("필요 컴포넌트 추가"))
        {
            foreach (Object targetObject in targets)
            {
                Building building = (Building)targetObject;
                Undo.RegisterCompleteObjectUndo(
                    building.gameObject,
                    "Setup Building Components");

                building.EnsureComponentsFromData();
            }
        }
    }

    void OnEnable()
    {
        foreach (Object targetObject in targets)
        {
            if (targetObject is Building building)
                RestoreComponentVisibility(building);
        }
    }

    static void RestoreComponentVisibility(Building building)
    {
        RestoreFlag<SelectableEntity>(building);
        RestoreFlag<EntityHealth>(building);
        RestoreFlag<WorldHealthBar>(building);
        RestoreFlag<UnitAttacker>(building);
        RestoreFlag<TowerAI>(building);
        RestoreFlag<Headquarters>(building);
        RestoreFlag<ProductionBuilding>(building);
        RestoreFlag<UnitAnimator>(building);
        RestoreFlag<UnitSound>(building);
        RestoreFlag<GridFootprint>(building);
        RestoreFlag<FogOfWarVisionSource>(building);
        RestoreFlag<FogOfWarVisibility>(building);
    }

    static void RestoreFlag<T>(Building building) where T : Component
    {
        T component = building.GetComponent<T>();
        if (component == null || component.hideFlags == HideFlags.None)
            return;

        component.hideFlags = HideFlags.None;
        EditorUtility.SetDirty(component);
    }
}
