using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 생산 건물 데이터와 생산 레시피를 한곳에서 관리하는 창입니다.
/// - 건물별 생산: 건물 하나를 골라 정보·최대 인구수·레시피 목록·레시피 세부값을 편집합니다.
/// - 건물 × 레시피: 어떤 건물이 무엇을 생산하는지 표에서 체크로 바꿉니다.
/// - 레시피 균형표: 모든 레시피 수치와 유닛 스탯, Watt 대비 효율을 비교합니다.
/// </summary>
public class ProductionManagerWindow : EditorWindow
{
    enum Tab
    {
        Buildings,
        Matrix,
        Balance,
    }

    static readonly GUIContent[] TabLabels =
    {
        new GUIContent("건물별 생산", "생산 건물 하나를 골라 정보와 레시피를 편집합니다."),
        new GUIContent("건물 × 레시피", "어떤 건물이 무엇을 생산할지 표에서 체크로 정합니다."),
        new GUIContent("레시피 균형표", "모든 레시피의 수치와 유닛 스탯, Watt 대비 효율을 비교합니다."),
    };

    static readonly Color WarningRowColor = new Color(1f, 0.55f, 0.2f, 0.12f);
    static readonly Color StripeColor = new Color(0f, 0f, 0f, 0.06f);
    static readonly Color SelectedColor = new Color(0.24f, 0.48f, 0.9f, 0.35f);

    Tab tab;

    // 프로젝트의 생산 데이터와 레시피. 창에 포커스가 올 때마다 다시 읽는다.
    readonly List<BuildableProductionData> buildings = new List<BuildableProductionData>();
    readonly List<ProductionRecipe> recipes = new List<ProductionRecipe>();
    readonly Dictionary<ProductionRecipe, List<BuildableProductionData>> usage =
        new Dictionary<ProductionRecipe, List<BuildableProductionData>>();
    readonly Dictionary<BuildableProductionData, SerializedObject> buildingObjects =
        new Dictionary<BuildableProductionData, SerializedObject>();

    [MenuItem("Tools/생산/생산 관리")]
    public static void Open()
    {
        Open(null);
    }

    public static void Open(BuildableProductionData focus)
    {
        ProductionManagerWindow window = GetWindow<ProductionManagerWindow>();
        window.titleContent = new GUIContent("생산 관리");
        window.minSize = new Vector2(760f, 360f);

        if (focus != null)
        {
            window.tab = Tab.Buildings;
            window.SelectBuilding(focus);
        }

        window.Show();
    }

    void OnEnable()
    {
        Reload();
    }

    void OnFocus()
    {
        // 다른 곳에서 에셋을 만들거나 지웠을 수 있으므로 창으로 돌아올 때 다시 읽는다.
        Reload();
    }

    void OnDisable()
    {
        if (recipeEditor != null)
            DestroyImmediate(recipeEditor);
    }

    void Reload()
    {
        buildings.Clear();
        buildings.AddRange(ProductionRecipeListDrawer.FindAllProductionData());

        recipes.Clear();
        recipes.AddRange(ProductionRecipeListDrawer.FindAllRecipes());

        usage.Clear();
        buildingObjects.Clear();

        foreach (BuildableProductionData data in buildings)
        {
            foreach (ProductionRecipe recipe in data.Recipes)
            {
                if (recipe == null)
                    continue;

                if (!usage.TryGetValue(recipe, out List<BuildableProductionData> users))
                {
                    users = new List<BuildableProductionData>();
                    usage.Add(recipe, users);
                }

                if (!users.Contains(data))
                    users.Add(data);
            }
        }

        if (selectedBuilding == null && buildings.Count > 0)
            SelectBuilding(buildings[0]);
        else if (selectedBuilding != null && selectedObject == null)
            SelectBuilding(selectedBuilding);

        ReloadBalanceRows();
        Repaint();
    }

    List<BuildableProductionData> GetUsers(ProductionRecipe recipe)
    {
        return recipe != null && usage.TryGetValue(recipe, out List<BuildableProductionData> users)
            ? users
            : null;
    }

    SerializedObject GetBuildingObject(BuildableProductionData data)
    {
        if (!buildingObjects.TryGetValue(data, out SerializedObject serialized) || serialized.targetObject == null)
        {
            serialized = new SerializedObject(data);
            buildingObjects[data] = serialized;
        }

        return serialized;
    }

    void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            Tab nextTab = (Tab)GUILayout.Toolbar((int)tab, TabLabels, EditorStyles.toolbarButton, GUILayout.Width(360f));

            if (nextTab != tab)
            {
                tab = nextTab;
                Reload();
            }
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                Reload();
        }

        switch (tab)
        {
            case Tab.Buildings:
                DrawBuildingsTab();
                break;
            case Tab.Matrix:
                DrawMatrixTab();
                break;
            case Tab.Balance:
                DrawBalanceTab();
                break;
        }
    }

    static void Ping(Object target)
    {
        Selection.activeObject = target;
        EditorGUIUtility.PingObject(target);
    }

    static bool Contains(string source, string keyword)
    {
        return !string.IsNullOrEmpty(source) &&
            source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ───────────────────────────── 건물별 생산 ─────────────────────────────

    const float BuildingListWidth = 230f;

    BuildableProductionData selectedBuilding;
    SerializedObject selectedObject;
    ProductionRecipeListDrawer selectedRecipeList;
    Editor recipeEditor;
    string buildingSearch = string.Empty;
    Vector2 buildingListScroll;
    Vector2 buildingDetailScroll;

    void SelectBuilding(BuildableProductionData data)
    {
        selectedBuilding = data;
        selectedObject = data != null ? new SerializedObject(data) : null;
        selectedRecipeList = selectedObject != null ? new ProductionRecipeListDrawer(selectedObject) : null;
        GUI.FocusControl(null);
    }

    void DrawBuildingsTab()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawBuildingList();
            DrawBuildingDetail();
        }
    }

    void DrawBuildingList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(BuildingListWidth)))
        {
            buildingSearch = EditorGUILayout.TextField(buildingSearch, EditorStyles.toolbarSearchField);

            buildingListScroll = EditorGUILayout.BeginScrollView(buildingListScroll);

            foreach (BuildableProductionData data in buildings)
            {
                if (data == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(buildingSearch) &&
                    !Contains(data.name, buildingSearch.Trim()) &&
                    !Contains(data.displayName, buildingSearch.Trim()))
                    continue;

                DrawBuildingListItem(data);
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button(new GUIContent("새 생산 건물 데이터", "생산 건물 데이터 에셋을 새로 만듭니다.")))
                CreateBuildingData();
        }
    }

    void DrawBuildingListItem(BuildableProductionData data)
    {
        Rect rect = GUILayoutUtility.GetRect(BuildingListWidth - 20f, 40f, GUILayout.ExpandWidth(true));
        bool selected = data == selectedBuilding;

        if (Event.current.type == EventType.Repaint && selected)
            EditorGUI.DrawRect(rect, SelectedColor);

        Rect iconRect = new Rect(rect.x + 2f, rect.y + 2f, 36f, 36f);
        Sprite icon = data.Icon;

        if (icon != null)
        {
            Texture2D preview = AssetPreview.GetAssetPreview(icon);
            GUI.DrawTexture(iconRect, preview != null ? preview : icon.texture, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUI.DrawRect(iconRect, new Color(0f, 0f, 0f, 0.2f));
        }

        float textX = iconRect.xMax + 6f;
        Rect nameRect = new Rect(textX, rect.y + 3f, rect.xMax - textX, 18f);
        Rect infoRect = new Rect(textX, rect.y + 20f, rect.xMax - textX, 16f);

        int recipeCount = 0;

        foreach (ProductionRecipe recipe in data.Recipes)
        {
            if (recipe != null)
                recipeCount++;
        }

        string populationText = data.maxPopulation > 0 ? data.maxPopulation.ToString() : "무제한";
        bool hasProblem = recipeCount == 0;

        EditorGUI.LabelField(nameRect, data.name, EditorStyles.boldLabel);
        EditorGUI.LabelField(
            infoRect,
            hasProblem ? "⚠ 레시피 없음" : $"레시피 {recipeCount}개 · 인구 {populationText}",
            EditorStyles.miniLabel);

        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            SelectBuilding(data);

            if (Event.current.clickCount == 2)
                Ping(data);

            Event.current.Use();
        }
    }

    void DrawBuildingDetail()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (selectedBuilding == null || selectedObject == null || selectedObject.targetObject == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 생산 건물 데이터를 고르세요.", MessageType.Info);
                return;
            }

            buildingDetailScroll = EditorGUILayout.BeginScrollView(buildingDetailScroll);
            selectedObject.Update();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(selectedBuilding.name, EditorStyles.largeLabel);

                if (GUILayout.Button("에셋 선택", GUILayout.Width(70f)))
                    Ping(selectedBuilding);
            }

            DrawBuildingProperties();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                new GUIContent("생산 레시피 목록", selectedRecipeList.RecipesProperty.tooltip),
                EditorStyles.boldLabel);
            selectedRecipeList.DoLayoutList();
            selectedRecipeList.DrawAddButtons();
            selectedRecipeList.DrawWarnings();

            EditorGUILayout.Space(8f);
            SerializedProperty upgradeOptions = selectedObject.FindProperty("upgradeOptions");
            EditorGUILayout.PropertyField(upgradeOptions, new GUIContent("업그레이드 분기", upgradeOptions.tooltip), true);
            DrawUpgradeChain(selectedBuilding);

            selectedObject.ApplyModifiedProperties();

            DrawSelectedRecipeDetail();

            EditorGUILayout.EndScrollView();
        }
    }

    // 레시피 목록을 뺀 건물 데이터 필드를 [Label]이 붙은 그대로 그린다.
    void DrawBuildingProperties()
    {
        SerializedProperty iterator = selectedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == "m_Script" ||
                iterator.name == "recipes" ||
                iterator.name == "defaultRecipeIndex" ||
                iterator.name == "recipe" ||
                iterator.name == "upgradeOptions")
                continue;

            EditorGUILayout.PropertyField(iterator, true);
        }
    }

    // 이 건물로 올라오는 하위 건물과 여기서 갈 수 있는 상위 건물을 한 줄로 보여준다.
    void DrawUpgradeChain(BuildableProductionData data)
    {
        var from = new List<string>();

        foreach (BuildableProductionData other in buildings)
        {
            if (other == null || other == data || other.upgradeOptions == null)
                continue;

            foreach (ProductionUpgradeOption option in other.upgradeOptions)
            {
                if (option != null && option.tier == data)
                {
                    from.Add(other.name);
                    break;
                }
            }
        }

        var to = new List<string>();

        if (data.upgradeOptions != null)
        {
            foreach (ProductionUpgradeOption option in data.upgradeOptions)
            {
                if (data.IsUsableOption(option))
                    to.Add($"{option.tier.name} ({option.ResolveCost()} W)");
            }
        }

        if (from.Count == 0 && to.Count == 0)
            return;

        string fromText = from.Count > 0 ? string.Join(", ", from) + "  →  " : string.Empty;
        string toText = to.Count > 0 ? "  →  " + string.Join(" / ", to) : string.Empty;

        EditorGUILayout.HelpBox($"업그레이드 단계: {fromText}[{data.name}]{toText}", MessageType.None);

        if (data.upgradeOptions == null)
            return;

        Vector2Int cells = data.GetFootprintCells();

        foreach (ProductionUpgradeOption option in data.upgradeOptions)
        {
            if (data.IsUsableOption(option) && option.tier.GetFootprintCells() != cells)
            {
                EditorGUILayout.HelpBox(
                    $"'{option.tier.name}'의 칸 수 {option.tier.GetFootprintCells()}가 이 건물 {cells}과 달라 업그레이드할 수 없습니다.",
                    MessageType.Warning);
            }
            else if (data.IsUsableOption(option) &&
                     !FootprintShape.SameShape(
                         cells,
                         GridFootprint.ResolveFootprintMask(data.Prefab),
                         option.tier.GetFootprintCells(),
                         GridFootprint.ResolveFootprintMask(option.tier.Prefab)))
            {
                EditorGUILayout.HelpBox(
                    $"'{option.tier.name}'의 칸 모양이 이 건물과 달라 업그레이드할 수 없습니다.",
                    MessageType.Warning);
            }
        }
    }

    void DrawSelectedRecipeDetail()
    {
        ProductionRecipe recipe = selectedRecipeList.SelectedRecipe;

        EditorGUILayout.Space(8f);

        if (recipe == null)
        {
            EditorGUILayout.HelpBox("목록에서 레시피를 고르면 여기서 생산 정보를 바로 고칠 수 있습니다.", MessageType.None);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"레시피: {recipe.name}", EditorStyles.boldLabel);

                if (GUILayout.Button("에셋 선택", GUILayout.Width(70f)))
                    Ping(recipe);
            }

            List<BuildableProductionData> users = GetUsers(recipe);

            if (users != null && users.Count > 1)
            {
                var names = new List<string>();

                foreach (BuildableProductionData user in users)
                    names.Add(user.name);

                EditorGUILayout.HelpBox(
                    $"이 레시피는 여러 건물이 함께 씁니다: {string.Join(", ", names)}\n여기서 고친 값은 모두에 반영됩니다.",
                    MessageType.Info);
            }

            Editor.CreateCachedEditor(recipe, null, ref recipeEditor);
            recipeEditor.OnInspectorGUI();
        }
    }

    void CreateBuildingData()
    {
        string folder = "Assets/Data/Building";

        if (selectedBuilding != null)
            folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(selectedBuilding)).Replace('\\', '/');

        string path = EditorUtility.SaveFilePanelInProject(
            "새 생산 건물 데이터",
            "Production Building",
            "asset",
            "생산 건물 데이터 에셋을 저장할 위치를 고르세요.",
            folder);

        if (string.IsNullOrEmpty(path))
            return;

        BuildableProductionData data = CreateInstance<BuildableProductionData>();
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();

        Reload();
        SelectBuilding(data);
        Ping(data);
    }

    // ───────────────────────────── 건물 × 레시피 ─────────────────────────────

    const float MatrixNameWidth = 160f;
    const float MatrixPopulationWidth = 64f;
    const float MatrixCellWidth = 86f;
    const float MatrixRowHeight = 40f;

    Vector2 matrixScroll;

    void DrawMatrixTab()
    {
        if (buildings.Count == 0 || recipes.Count == 0)
        {
            EditorGUILayout.HelpBox("생산 건물 데이터와 생산 레시피가 모두 있어야 표를 그릴 수 있습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            "체크하면 그 건물의 생산 목록에 레시피를 넣고, 끄면 뺍니다. " +
            "칸 아래 숫자는 최대 인구수 안에서 만들 수 있는 수입니다. '기본'은 완공 직후 생산하는 레시피입니다(건물별 생산 탭에서 바꿉니다).",
            MessageType.None);

        matrixScroll = EditorGUILayout.BeginScrollView(matrixScroll);

        DrawMatrixHeader();

        for (int i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] != null)
                DrawMatrixRow(buildings[i], i);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawMatrixHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("건물", EditorStyles.boldLabel, GUILayout.Width(MatrixNameWidth));
            GUILayout.Label(new GUIContent("최대 인구", "건물 하나가 생산한 유닛들의 인구수 합계 상한입니다. 0이면 무제한입니다."),
                EditorStyles.boldLabel, GUILayout.Width(MatrixPopulationWidth));

            foreach (ProductionRecipe recipe in recipes)
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(MatrixCellWidth)))
                {
                    Rect iconRect = GUILayoutUtility.GetRect(28f, 28f, GUILayout.Width(28f), GUILayout.Height(28f));
                    iconRect.x += (MatrixCellWidth - 28f) * 0.5f - 4f;
                    ProductionRecipeListDrawer.DrawRecipeIcon(iconRect, recipe);

                    string tooltip = $"{recipe.GetDisplayName()}\n인구 {recipe.population} · {recipe.spawnInterval:0.#}초 · {recipe.TotalWattCost:0.#} W/기";

                    if (GUILayout.Button(new GUIContent(recipe.name, tooltip), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(MatrixCellWidth)))
                        Ping(recipe);

                    GUILayout.Label($"인구 {recipe.population}", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(MatrixCellWidth));
                }
            }
        }
    }

    void DrawMatrixRow(BuildableProductionData data, int rowIndex)
    {
        SerializedObject serialized = GetBuildingObject(data);
        serialized.Update();

        Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(MatrixRowHeight));

        if (Event.current.type == EventType.Repaint && rowIndex % 2 == 1)
            EditorGUI.DrawRect(rowRect, StripeColor);

        if (GUILayout.Button(new GUIContent(data.name, "클릭하면 건물별 생산 탭에서 엽니다."),
                EditorStyles.linkLabel, GUILayout.Width(MatrixNameWidth)))
        {
            SelectBuilding(data);
            tab = Tab.Buildings;
        }

        SerializedProperty maxPopulation = serialized.FindProperty("maxPopulation");
        DrawNumberInput(maxPopulation, MatrixPopulationWidth);

        SerializedProperty defaultIndex = serialized.FindProperty("defaultRecipeIndex");
        bool listChanged = false;

        foreach (ProductionRecipe recipe in recipes)
        {
            int index = ProductionRecipeListDrawer.IndexOf(serialized, recipe);
            bool included = index >= 0;

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(MatrixCellWidth)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(MatrixCellWidth * 0.5f - 10f);
                    bool toggled = EditorGUILayout.Toggle(included, GUILayout.Width(18f));

                    if (toggled != included)
                    {
                        if (toggled)
                            ProductionRecipeListDrawer.AddRecipe(serialized, recipe);
                        else
                            ProductionRecipeListDrawer.RemoveRecipeAt(serialized, index);

                        listChanged = true;
                    }
                }

                if (included)
                {
                    string capacity = ProductionRecipeListDrawer.DescribeCapacity(recipe, maxPopulation.intValue);
                    string label = index == defaultIndex.intValue ? $"기본 · {capacity}" : capacity;
                    GUILayout.Label(label, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(MatrixCellWidth));
                }
            }
        }

        serialized.ApplyModifiedProperties();
        EditorGUILayout.EndHorizontal();

        // 목록이 바뀌었으면 사용 현황(균형표의 사용 건물 등)을 다시 계산한다.
        if (listChanged)
        {
            Reload();
            GUIUtility.ExitGUI();
        }
    }

    // ───────────────────────────── 레시피 균형표 ─────────────────────────────

    class Row
    {
        public ProductionRecipe recipe;
        public SerializedObject recipeObject;
        // 체력·공격 수치를 읽고 고칠 대상. 유닛 데이터가 있으면 그것을(런타임에 컴포넌트를 덮어쓰므로),
        // 없으면 프리팹의 EntityHealth / UnitAttacker를 직접 쓴다.
        public SerializedObject healthObject;
        public SerializedObject attackObject;

        public float TotalWatt => recipe.TotalWattCost;
        public float Health => ReadFloat(healthObject, "maxHealth");
        public float Armor => ReadFloat(healthObject, "armor");
        public float Damage => ReadFloat(attackObject, "attackDamage");
        public float Cooldown => ReadFloat(attackObject, "attackCooldown");
        public float Dps => Cooldown > 0f ? Damage / Cooldown : 0f;
        public float HealthPerWatt => TotalWatt > 0f ? Health / TotalWatt : 0f;
        public float DpsPer100Watt => TotalWatt > 0f ? Dps / TotalWatt * 100f : 0f;
        public float DpsPerPopulation => recipe.population > 0 ? Dps / recipe.population : 0f;

        public void ResolveStatSources()
        {
            healthObject = null;
            attackObject = null;

            GameObject prefab = recipe.unitPrefab;

            if (prefab == null)
                return;

            UnitData data = SelectionInfoUtility.GetUnitData(prefab);

            if (data != null)
            {
                healthObject = attackObject = new SerializedObject(data);
                return;
            }

            EntityHealth health = prefab.GetComponent<EntityHealth>();
            UnitAttacker attacker = prefab.GetComponent<UnitAttacker>();

            if (health != null)
                healthObject = new SerializedObject(health);

            if (attacker != null)
                attackObject = new SerializedObject(attacker);
        }

        public void UpdateStats()
        {
            healthObject?.Update();

            if (attackObject != healthObject)
                attackObject?.Update();
        }

        public void ApplyStats()
        {
            healthObject?.ApplyModifiedProperties();

            if (attackObject != healthObject)
                attackObject?.ApplyModifiedProperties();
        }

        static float ReadFloat(SerializedObject target, string propertyName)
        {
            SerializedProperty property = target?.FindProperty(propertyName);
            return property != null ? property.floatValue : 0f;
        }
    }

    enum SortKey
    {
        Name,
        InitialDelay,
        Interval,
        WattPerSecond,
        WattPerUnit,
        Population,
        Health,
        Armor,
        Damage,
        Cooldown,
        Dps,
        HealthPerWatt,
        DpsPerWatt,
        DpsPerPopulation,
        Requirements,
    }

    const float IconWidth = 32f;
    const float NameWidth = 180f;
    const float NumWidth = 76f;
    const float ColumnGap = 6f;
    const float RequirementWidth = 48f;
    const float UsedByWidth = 170f;
    const float RowHeight = 42f;

    readonly List<Row> rows = new List<Row>();
    Vector2 balanceScroll;
    string balanceSearch = string.Empty;
    SortKey sortKey = SortKey.Name;
    bool sortDescending;
    bool showUnitStats = true;

    void ReloadBalanceRows()
    {
        rows.Clear();

        foreach (ProductionRecipe recipe in recipes)
        {
            Row row = new Row
            {
                recipe = recipe,
                recipeObject = new SerializedObject(recipe),
            };

            row.ResolveStatSources();
            rows.Add(row);
        }

        SortRows();
    }

    void DrawBalanceTab()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("검색", GUILayout.Width(30f));
            balanceSearch = EditorGUILayout.TextField(balanceSearch, EditorStyles.toolbarSearchField, GUILayout.Width(200f));
            showUnitStats = GUILayout.Toggle(showUnitStats, "유닛 스탯 표시", EditorStyles.toolbarButton, GUILayout.Width(90f));
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("새 레시피", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                CreateRecipe();
        }

        balanceScroll = EditorGUILayout.BeginScrollView(balanceScroll);
        DrawBalanceHeader();

        int visibleIndex = 0;

        foreach (Row row in rows)
        {
            if (row.recipe == null || !MatchesSearch(row))
                continue;

            DrawBalanceRow(row, visibleIndex++);
        }

        if (visibleIndex == 0)
            EditorGUILayout.HelpBox("표시할 생산 레시피가 없습니다.", MessageType.Info);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.HelpBox(
            "W/기 = 초당 Watt × 생산 간격. 체력/W·DPS/100W는 Watt 대비, DPS/인구는 인구 대비 효율입니다(높을수록 좋음). " +
            "체력·공격력·공격 간격은 유닛 데이터가 있으면 그 에셋을, 없으면 유닛 프리팹의 EntityHealth·UnitAttacker를 직접 고칩니다. " +
            "주황색 줄은 유닛 프리팹이 없거나 어떤 생산 건물 데이터에도 들어 있지 않은 레시피입니다.",
            MessageType.None);
    }

    void DrawBalanceHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(IconWidth + 4f);
            HeaderButton("레시피", "레시피 에셋 이름과 유닛 프리팹입니다.", SortKey.Name, NameWidth);
            HeaderButton("첫 대기", "건물 완공 후 첫 생산까지 대기 시간(초)입니다.", SortKey.InitialDelay, NumWidth);
            HeaderButton("간격", "유닛 한 마리를 생산하는 간격(초)입니다.", SortKey.Interval, NumWidth);
            HeaderButton("W/초", "생산 중 초당 소모하는 Watt입니다.", SortKey.WattPerSecond, NumWidth);
            HeaderButton("W/기", "유닛 한 마리를 만드는 데 드는 총 Watt입니다.", SortKey.WattPerUnit, NumWidth);
            HeaderButton("인구", "유닛 한 마리가 차지하는 인구수입니다.", SortKey.Population, NumWidth);

            if (showUnitStats)
            {
                HeaderButton("체력", "유닛 데이터의 최대 체력입니다.", SortKey.Health, NumWidth);
                HeaderButton("방어력", "유닛 데이터의 방어력입니다. 한 번 맞을 때마다 피해를 이만큼 줄입니다.", SortKey.Armor, NumWidth);
                HeaderButton("공격력", "유닛 데이터의 공격력입니다.", SortKey.Damage, NumWidth);
                HeaderButton("공격 간격", "유닛 데이터의 공격 간격(초)입니다.", SortKey.Cooldown, NumWidth);
                HeaderButton("DPS", "공격력 ÷ 공격 간격입니다.", SortKey.Dps, NumWidth);
                HeaderButton("체력/W", "Watt 1당 체력입니다.", SortKey.HealthPerWatt, NumWidth);
                HeaderButton("DPS/100W", "Watt 100당 DPS입니다.", SortKey.DpsPerWatt, NumWidth);
                HeaderButton("DPS/인구", "인구 1당 DPS입니다.", SortKey.DpsPerPopulation, NumWidth);
            }

            HeaderButton("조건", "해금 조건 개수입니다.", SortKey.Requirements, RequirementWidth);
            GUILayout.Label(new GUIContent("사용 건물", "이 레시피를 목록에 넣은 생산 건물 데이터입니다."),
                EditorStyles.boldLabel, GUILayout.Width(UsedByWidth));
        }
    }

    void HeaderButton(string title, string tooltip, SortKey key, float width)
    {
        string arrow = sortKey == key ? (sortDescending ? " ▼" : " ▲") : string.Empty;

        bool clicked = GUILayout.Button(new GUIContent(title + arrow, tooltip), EditorStyles.miniButton, GUILayout.Width(width));
        GUILayout.Space(ColumnGap);

        if (!clicked)
            return;

        if (sortKey == key)
        {
            sortDescending = !sortDescending;
        }
        else
        {
            sortKey = key;
            sortDescending = key != SortKey.Name;
        }

        SortRows();
    }

    void DrawBalanceRow(Row row, int visibleIndex)
    {
        Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));

        List<BuildableProductionData> users = GetUsers(row.recipe);
        bool hasWarning = row.recipe.unitPrefab == null || users == null || users.Count == 0;

        if (Event.current.type == EventType.Repaint)
        {
            if (hasWarning)
                EditorGUI.DrawRect(rowRect, WarningRowColor);
            else if (visibleIndex % 2 == 1)
                EditorGUI.DrawRect(rowRect, StripeColor);
        }

        row.recipeObject.Update();
        row.UpdateStats();

        Rect iconRect = GUILayoutUtility.GetRect(IconWidth, IconWidth, GUILayout.Width(IconWidth), GUILayout.Height(IconWidth));
        ProductionRecipeListDrawer.DrawRecipeIcon(iconRect, row.recipe);
        GUILayout.Space(4f);

        using (new EditorGUILayout.VerticalScope(GUILayout.Width(NameWidth)))
        {
            if (GUILayout.Button(new GUIContent(row.recipe.name, "클릭하면 에셋을 선택합니다."),
                    EditorStyles.linkLabel, GUILayout.Width(NameWidth)))
                Ping(row.recipe);

            SerializedProperty prefabProperty = row.recipeObject.FindProperty("unitPrefab");
            EditorGUI.BeginChangeCheck();
            Object prefab = EditorGUILayout.ObjectField(
                prefabProperty.objectReferenceValue, typeof(GameObject), false, GUILayout.Width(NameWidth));

            if (EditorGUI.EndChangeCheck())
            {
                prefabProperty.objectReferenceValue = prefab;

                // 프리팹이 바뀌면 유닛 데이터 참조도 다시 찾는다.
                row.recipeObject.ApplyModifiedProperties();
                row.ResolveStatSources();
            }
        }

        GUILayout.Space(ColumnGap);

        NumberField(row.recipeObject, "initialSpawnDelay");
        NumberField(row.recipeObject, "spawnInterval");
        NumberField(row.recipeObject, "wattCostPerSecond");
        ReadOnlyNumber(row.TotalWatt);
        NumberField(row.recipeObject, "population");

        if (showUnitStats)
        {
            StatField(row.healthObject, "maxHealth", "체력 정보 없음", "유닛 프리팹에 EntityHealth도 유닛 데이터도 없습니다.");
            StatField(row.healthObject, "armor", "체력 정보 없음", "유닛 프리팹에 EntityHealth도 유닛 데이터도 없습니다.");
            StatField(row.attackObject, "attackDamage", "공격 없음", "유닛 프리팹에 UnitAttacker도 유닛 데이터도 없습니다.");
            StatField(row.attackObject, "attackCooldown", "공격 없음", "유닛 프리팹에 UnitAttacker도 유닛 데이터도 없습니다.");

            ReadOnlyNumber(row.Dps);
            ReadOnlyNumber(row.HealthPerWatt);
            ReadOnlyNumber(row.DpsPer100Watt);
            ReadOnlyNumber(row.DpsPerPopulation);
        }

        int requirementCount = row.recipe.RequirementCount;
        GUILayout.Label(
            new GUIContent(requirementCount > 0 ? requirementCount.ToString() : "-", BuildRequirementTooltip(row.recipe)),
            EditorStyles.centeredGreyMiniLabel, GUILayout.Width(RequirementWidth));
        GUILayout.Space(ColumnGap);

        DrawUsedBy(row, users);

        row.recipeObject.ApplyModifiedProperties();
        row.ApplyStats();

        EditorGUILayout.EndHorizontal();
    }

    // 표 칸에는 숫자 입력 칸만 그린다. PropertyField는 [Header]·[Label]까지 칸 안에 그려 겹쳐 보인다.
    // 표에 나오는 수치는 모두 음수가 의미 없으므로 0 아래로 내려가지 않게 막는다.
    static void NumberField(SerializedObject target, string propertyName)
    {
        SerializedProperty property = target.FindProperty(propertyName);

        if (property == null)
        {
            GUILayout.Space(NumWidth + ColumnGap);
            return;
        }

        DrawNumberInput(property, NumWidth);
        GUILayout.Space(ColumnGap);
    }

    static void DrawNumberInput(SerializedProperty property, float width)
    {
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

        if (property.propertyType == SerializedPropertyType.Integer)
        {
            int value = EditorGUILayout.IntField(property.intValue, GUILayout.Width(width));

            if (value != property.intValue)
                property.intValue = Mathf.Max(0, value);
        }
        else if (property.propertyType == SerializedPropertyType.Float)
        {
            float value = EditorGUILayout.FloatField(property.floatValue, GUILayout.Width(width));

            if (!Mathf.Approximately(value, property.floatValue))
                property.floatValue = Mathf.Max(0f, value);
        }
        else
        {
            GUILayout.Space(width);
        }

        EditorGUI.showMixedValue = false;
    }

    static void StatField(SerializedObject target, string propertyName, string missingText, string missingTooltip)
    {
        if (target != null)
        {
            NumberField(target, propertyName);
            return;
        }

        GUILayout.Label(new GUIContent(missingText, missingTooltip), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(NumWidth));
        GUILayout.Space(ColumnGap);
    }

    static GUIStyle readOnlyStyle;

    static void ReadOnlyNumber(float value)
    {
        if (readOnlyStyle == null)
            readOnlyStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };

        GUILayout.Label(value > 0f ? value.ToString("0.##") : "-", readOnlyStyle, GUILayout.Width(NumWidth));
        GUILayout.Space(ColumnGap);
    }

    void DrawUsedBy(Row row, List<BuildableProductionData> users)
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(UsedByWidth)))
        {
            if (row.recipe.unitPrefab == null)
                GUILayout.Label("유닛 프리팹 없음", EditorStyles.miniBoldLabel);

            if (users == null || users.Count == 0)
            {
                GUILayout.Label("사용하는 건물 없음", EditorStyles.miniLabel);
                return;
            }

            foreach (BuildableProductionData data in users)
            {
                if (data == null)
                    continue;

                string capacity = ProductionRecipeListDrawer.DescribeCapacity(row.recipe, data.maxPopulation);

                if (GUILayout.Button(new GUIContent($"{data.name} ({capacity})", "클릭하면 건물별 생산 탭에서 엽니다."),
                        EditorStyles.linkLabel, GUILayout.Width(UsedByWidth)))
                {
                    SelectBuilding(data);
                    tab = Tab.Buildings;
                }
            }
        }
    }

    static string BuildRequirementTooltip(ProductionRecipe recipe)
    {
        if (!recipe.HasRequirements)
            return "해금 조건이 없습니다.";

        var parts = new List<string>();

        foreach (BuildingRequirement requirement in recipe.requiredBuildings)
        {
            if (requirement != null && !requirement.IsEmpty)
                parts.Add("건물: " + requirement.Describe());
        }

        foreach (UpgradeRequirement requirement in recipe.requiredUpgrades)
        {
            if (requirement != null && requirement.upgrade != null)
                parts.Add("업그레이드: " + requirement.Describe());
        }

        return string.Join("\n", parts);
    }

    bool MatchesSearch(Row row)
    {
        if (string.IsNullOrWhiteSpace(balanceSearch))
            return true;

        string keyword = balanceSearch.Trim();

        return Contains(row.recipe.name, keyword) ||
            Contains(row.recipe.GetDisplayName(), keyword) ||
            (row.recipe.unitPrefab != null && Contains(row.recipe.unitPrefab.name, keyword));
    }

    void SortRows()
    {
        rows.Sort((a, b) =>
        {
            int result = sortKey == SortKey.Name
                ? string.Compare(a.recipe.name, b.recipe.name, StringComparison.OrdinalIgnoreCase)
                : GetSortValue(a).CompareTo(GetSortValue(b));

            return sortDescending ? -result : result;
        });
    }

    float GetSortValue(Row row)
    {
        ProductionRecipe recipe = row.recipe;

        switch (sortKey)
        {
            case SortKey.InitialDelay: return recipe.initialSpawnDelay;
            case SortKey.Interval: return recipe.spawnInterval;
            case SortKey.WattPerSecond: return recipe.wattCostPerSecond;
            case SortKey.WattPerUnit: return row.TotalWatt;
            case SortKey.Population: return recipe.population;
            case SortKey.Health: return row.Health;
            case SortKey.Armor: return row.Armor;
            case SortKey.Damage: return row.Damage;
            case SortKey.Cooldown: return row.Cooldown;
            case SortKey.Dps: return row.Dps;
            case SortKey.HealthPerWatt: return row.HealthPerWatt;
            case SortKey.DpsPerWatt: return row.DpsPer100Watt;
            case SortKey.DpsPerPopulation: return row.DpsPerPopulation;
            case SortKey.Requirements: return recipe.RequirementCount;
            default: return 0f;
        }
    }

    void CreateRecipe()
    {
        string folder = "Assets/Data/Building";

        if (recipes.Count > 0)
            folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(recipes[0])).Replace('\\', '/');

        string path = EditorUtility.SaveFilePanelInProject(
            "새 생산 레시피",
            "ProductionRecipe",
            "asset",
            "생산 레시피 에셋을 저장할 위치를 고르세요.",
            folder);

        if (string.IsNullOrEmpty(path))
            return;

        ProductionRecipe recipe = CreateInstance<ProductionRecipe>();
        AssetDatabase.CreateAsset(recipe, path);
        AssetDatabase.SaveAssets();

        Ping(recipe);
        Reload();
    }
}
