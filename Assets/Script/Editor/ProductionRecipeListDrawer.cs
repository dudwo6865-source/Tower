using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 생산 건물 데이터의 레시피 목록을 그리는 공용 도우미입니다.
/// 데이터 인스펙터와 생산 관리 창이 함께 씁니다.
/// 줄마다 기본 레시피 라디오·아이콘·레시피 칸·요약(간격·비용·인구·최대 생산 수)을 그립니다.
/// </summary>
public class ProductionRecipeListDrawer
{
    const float IconSize = 36f;
    const float DefaultToggleWidth = 34f;
    const float Spacing = 4f;

    readonly SerializedObject serializedObject;
    readonly SerializedProperty recipesProperty;
    readonly SerializedProperty defaultIndexProperty;
    readonly SerializedProperty maxPopulationProperty;
    readonly ReorderableList list;

    public ProductionRecipeListDrawer(SerializedObject target)
    {
        serializedObject = target;
        recipesProperty = target.FindProperty("recipes");
        defaultIndexProperty = target.FindProperty("defaultRecipeIndex");
        maxPopulationProperty = target.FindProperty("maxPopulation");

        list = new ReorderableList(target, recipesProperty, true, true, true, true)
        {
            elementHeight = IconSize + 6f,
            drawHeaderCallback = DrawHeader,
            drawElementCallback = DrawElement,
            onReorderCallbackWithDetails = HandleReorder,
            onRemoveCallback = l => RemoveRecipeAt(serializedObject, l.index),
        };
    }

    public SerializedProperty RecipesProperty => recipesProperty;

    /// <summary>목록에서 선택된 레시피입니다. 없으면 null입니다.</summary>
    public ProductionRecipe SelectedRecipe
    {
        get
        {
            if (list.index < 0 || list.index >= recipesProperty.arraySize)
                return null;

            return recipesProperty.GetArrayElementAtIndex(list.index).objectReferenceValue as ProductionRecipe;
        }
    }

    public void DoLayoutList()
    {
        list.DoLayoutList();
    }

    /// <summary>'기존 레시피 추가'·'새 레시피 만들기' 버튼을 그립니다.</summary>
    public void DrawAddButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(
                    new GUIContent("기존 레시피 추가 ▾", "프로젝트에 있는 생산 레시피 중 목록에 없는 것을 골라 추가합니다."),
                    GUILayout.Width(130f)))
            {
                ShowAddMenu();
            }

            if (GUILayout.Button(
                    new GUIContent("새 레시피 만들기", "이 데이터 옆 폴더에 생산 레시피 에셋을 만들고 목록에 추가합니다."),
                    GUILayout.Width(120f)))
            {
                CreateRecipeAsset();
            }
        }
    }

    void DrawHeader(Rect rect)
    {
        Rect defaultRect = new Rect(rect.x + 14f, rect.y, DefaultToggleWidth + 10f, rect.height);
        Rect recipeRect = new Rect(defaultRect.xMax, rect.y, rect.width - defaultRect.width, rect.height);

        EditorGUI.LabelField(defaultRect, new GUIContent("기본", "건물이 완공됐을 때 처음 생산할 레시피입니다."));
        EditorGUI.LabelField(recipeRect, "레시피 (순서 = 생산 패널 슬롯 순서)");
    }

    void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty element = recipesProperty.GetArrayElementAtIndex(index);
        ProductionRecipe recipe = element.objectReferenceValue as ProductionRecipe;

        rect.y += 3f;
        rect.height = IconSize;

        Rect defaultRect = new Rect(rect.x, rect.y + (IconSize - EditorGUIUtility.singleLineHeight) * 0.5f,
            DefaultToggleWidth, EditorGUIUtility.singleLineHeight);
        bool isDefault = defaultIndexProperty.intValue == index;

        if (EditorGUI.Toggle(defaultRect, isDefault, EditorStyles.radioButton) && !isDefault)
            defaultIndexProperty.intValue = index;

        Rect iconRect = new Rect(defaultRect.xMax, rect.y, IconSize, IconSize);
        DrawRecipeIcon(iconRect, recipe);

        float textX = iconRect.xMax + Spacing * 2f;
        float lineHeight = EditorGUIUtility.singleLineHeight;
        Rect fieldRect = new Rect(textX, rect.y, rect.xMax - textX, lineHeight);
        Rect summaryRect = new Rect(textX, rect.y + lineHeight + 2f, rect.xMax - textX, lineHeight);

        EditorGUI.PropertyField(fieldRect, element, GUIContent.none);
        EditorGUI.LabelField(summaryRect, BuildSummary(recipe, MaxPopulation), EditorStyles.miniLabel);
    }

    int MaxPopulation => maxPopulationProperty != null ? maxPopulationProperty.intValue : 0;

    public static void DrawRecipeIcon(Rect rect, ProductionRecipe recipe)
    {
        Sprite icon = recipe != null ? recipe.GetIcon() : null;

        if (icon == null)
        {
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.2f));
            return;
        }

        Texture2D preview = AssetPreview.GetAssetPreview(icon);
        GUI.DrawTexture(rect, preview != null ? preview : icon.texture, ScaleMode.ScaleToFit);
    }

    /// <summary>최대 인구수 안에서 이 레시피 유닛을 몇 기까지 만들 수 있는지 설명합니다.</summary>
    public static string DescribeCapacity(ProductionRecipe recipe, int maxPopulation)
    {
        if (recipe == null)
            return string.Empty;

        if (maxPopulation <= 0 || recipe.population <= 0)
            return "최대 무제한";

        int count = maxPopulation / recipe.population;
        return count > 0 ? $"최대 {count}기" : "⚠ 인구 부족(생산 불가)";
    }

    public static string BuildSummary(ProductionRecipe recipe, int maxPopulation)
    {
        if (recipe == null)
            return "레시피를 지정하세요.";

        if (recipe.unitPrefab == null)
            return "⚠ 유닛 프리팹이 비어 있습니다.";

        string lockText = recipe.HasRequirements
            ? $"  ·  해금 조건 {recipe.RequirementCount}개"
            : string.Empty;

        return $"{recipe.GetDisplayName()}  ·  {recipe.spawnInterval:0.#}초  ·  " +
            $"{recipe.TotalWattCost:0.#} W/기  ·  인구 {recipe.population} → {DescribeCapacity(recipe, maxPopulation)}{lockText}";
    }

    // 순서를 바꿔도 기본 레시피가 같은 에셋을 가리키도록 번호를 따라 옮긴다.
    void HandleReorder(ReorderableList reorderable, int oldIndex, int newIndex)
    {
        int current = defaultIndexProperty.intValue;

        if (current == oldIndex)
            defaultIndexProperty.intValue = newIndex;
        else if (oldIndex < current && newIndex >= current)
            defaultIndexProperty.intValue = current - 1;
        else if (oldIndex > current && newIndex <= current)
            defaultIndexProperty.intValue = current + 1;
    }

    /// <summary>목록 끝에 레시피를 추가합니다. 이미 있으면 무시합니다.</summary>
    public static bool AddRecipe(SerializedObject target, ProductionRecipe recipe)
    {
        if (recipe == null || IndexOf(target, recipe) >= 0)
            return false;

        SerializedProperty recipes = target.FindProperty("recipes");
        int index = recipes.arraySize;
        recipes.InsertArrayElementAtIndex(index);
        recipes.GetArrayElementAtIndex(index).objectReferenceValue = recipe;
        return true;
    }

    /// <summary>레시피를 목록에서 빼고 기본 레시피 번호를 맞춥니다.</summary>
    public static void RemoveRecipeAt(SerializedObject target, int index)
    {
        SerializedProperty recipes = target.FindProperty("recipes");
        SerializedProperty defaultIndex = target.FindProperty("defaultRecipeIndex");

        if (index < 0 || index >= recipes.arraySize)
            return;

        // 예전 유니티는 참조 요소를 한 번 지우면 null로만 바꾸므로 먼저 비운다.
        SerializedProperty element = recipes.GetArrayElementAtIndex(index);

        if (element.objectReferenceValue != null)
            element.objectReferenceValue = null;

        recipes.DeleteArrayElementAtIndex(index);

        int current = defaultIndex.intValue;

        if (index < current)
            current--;

        defaultIndex.intValue = recipes.arraySize > 0
            ? Mathf.Clamp(current, 0, recipes.arraySize - 1)
            : 0;
    }

    public static int IndexOf(SerializedObject target, ProductionRecipe recipe)
    {
        SerializedProperty recipes = target.FindProperty("recipes");

        for (int i = 0; i < recipes.arraySize; i++)
        {
            if (recipes.GetArrayElementAtIndex(i).objectReferenceValue == recipe)
                return i;
        }

        return -1;
    }

    void ShowAddMenu()
    {
        var menu = new GenericMenu();
        List<ProductionRecipe> recipes = FindAllRecipes();

        foreach (ProductionRecipe recipe in recipes)
        {
            if (IndexOf(serializedObject, recipe) >= 0)
                continue;

            ProductionRecipe captured = recipe;
            menu.AddItem(new GUIContent($"{recipe.name}  ({recipe.GetDisplayName()})"), false, () =>
            {
                serializedObject.Update();
                AddRecipe(serializedObject, captured);
                serializedObject.ApplyModifiedProperties();
            });
        }

        if (menu.GetItemCount() == 0)
            menu.AddDisabledItem(new GUIContent("추가할 레시피가 없습니다"));

        menu.ShowAsContext();
    }

    void CreateRecipeAsset()
    {
        ProductionRecipe recipe = CreateRecipeNextTo(serializedObject.targetObject);

        if (recipe == null)
            return;

        AddRecipe(serializedObject, recipe);
        list.index = recipesProperty.arraySize - 1;
        EditorGUIUtility.PingObject(recipe);
    }

    public static ProductionRecipe CreateRecipeNextTo(Object anchor)
    {
        string anchorPath = anchor != null ? AssetDatabase.GetAssetPath(anchor) : string.Empty;
        string folder = string.IsNullOrEmpty(anchorPath)
            ? "Assets"
            : Path.GetDirectoryName(anchorPath).Replace('\\', '/');
        string baseName = anchor != null ? $"{anchor.name} 레시피" : "ProductionRecipe";
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.asset");

        ProductionRecipe recipe = ScriptableObject.CreateInstance<ProductionRecipe>();
        AssetDatabase.CreateAsset(recipe, path);
        AssetDatabase.SaveAssets();
        return recipe;
    }

    public static List<ProductionRecipe> FindAllRecipes()
    {
        var results = new List<ProductionRecipe>();

        foreach (string guid in AssetDatabase.FindAssets("t:ProductionRecipe"))
        {
            ProductionRecipe recipe = AssetDatabase.LoadAssetAtPath<ProductionRecipe>(AssetDatabase.GUIDToAssetPath(guid));

            if (recipe != null && !results.Contains(recipe))
                results.Add(recipe);
        }

        results.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        return results;
    }

    public static List<BuildableProductionData> FindAllProductionData()
    {
        var results = new List<BuildableProductionData>();

        foreach (string guid in AssetDatabase.FindAssets("t:BuildableProductionData"))
        {
            BuildableProductionData data = AssetDatabase.LoadAssetAtPath<BuildableProductionData>(AssetDatabase.GUIDToAssetPath(guid));

            if (data != null && !results.Contains(data))
                results.Add(data);
        }

        results.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        return results;
    }

    /// <summary>목록·기본 레시피·인구 설정의 문제를 HelpBox로 보여줍니다.</summary>
    public void DrawWarnings()
    {
        if (recipesProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox("생산 레시피가 없습니다. 이 건물은 유닛을 생산하지 않습니다.", MessageType.Warning);
            return;
        }

        int defaultIndex = defaultIndexProperty.intValue;

        if (defaultIndex >= 0 && defaultIndex < recipesProperty.arraySize &&
            recipesProperty.GetArrayElementAtIndex(defaultIndex).objectReferenceValue is ProductionRecipe defaultRecipe &&
            defaultRecipe.HasRequirements)
        {
            EditorGUILayout.HelpBox(
                $"기본 레시피 '{defaultRecipe.name}'에 해금 조건이 있습니다. 조건을 채울 때까지 이 건물은 생산하지 않습니다.",
                MessageType.Warning);
        }

        int maxPopulation = MaxPopulation;

        for (int i = 0; i < recipesProperty.arraySize; i++)
        {
            Object current = recipesProperty.GetArrayElementAtIndex(i).objectReferenceValue;

            if (current == null)
            {
                EditorGUILayout.HelpBox($"{i + 1}번째 레시피가 비어 있습니다.", MessageType.Warning);
                continue;
            }

            if (current is ProductionRecipe recipe && maxPopulation > 0 && recipe.population > maxPopulation)
            {
                EditorGUILayout.HelpBox(
                    $"'{recipe.name}'의 인구수({recipe.population})가 최대 인구수({maxPopulation})보다 커서 생산할 수 없습니다.",
                    MessageType.Warning);
            }

            for (int j = 0; j < i; j++)
            {
                if (recipesProperty.GetArrayElementAtIndex(j).objectReferenceValue == current)
                {
                    EditorGUILayout.HelpBox(
                        $"'{current.name}' 레시피가 목록에 두 번 들어 있습니다. 생산 패널에는 한 번만 표시됩니다.",
                        MessageType.Info);
                    break;
                }
            }
        }
    }
}
