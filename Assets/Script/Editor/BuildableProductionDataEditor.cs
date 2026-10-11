using UnityEditor;
using UnityEngine;

/// <summary>
/// 생산 건물 데이터의 레시피 목록을 한눈에 관리하는 인스펙터입니다.
/// 줄마다 아이콘·이름·생산 간격·비용·인구를 요약하고, 기본 레시피를 라디오 버튼으로 고릅니다.
/// </summary>
[CustomEditor(typeof(BuildableProductionData))]
public class BuildableProductionDataEditor : Editor
{
    ProductionRecipeListDrawer recipeList;

    void OnEnable()
    {
        recipeList = new ProductionRecipeListDrawer(serializedObject);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "recipes",
            "defaultRecipeIndex",
            "recipe",
            "upgradeOptions");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            new GUIContent("생산 레시피 목록", recipeList.RecipesProperty.tooltip),
            EditorStyles.boldLabel);
        recipeList.DoLayoutList();
        recipeList.DrawAddButtons();

        if (GUILayout.Button(new GUIContent("생산 관리 창에서 열기", "모든 생산 건물과 레시피를 한곳에서 관리하는 창을 엽니다.")))
            ProductionManagerWindow.Open(target as BuildableProductionData);

        recipeList.DrawWarnings();

        EditorGUILayout.Space(6f);
        SerializedProperty upgradeOptions = serializedObject.FindProperty("upgradeOptions");
        EditorGUILayout.PropertyField(upgradeOptions, new GUIContent("업그레이드 분기", upgradeOptions.tooltip), true);

        serializedObject.ApplyModifiedProperties();
    }
}
