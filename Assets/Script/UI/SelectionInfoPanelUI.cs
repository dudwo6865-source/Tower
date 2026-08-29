using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectionInfoPanelUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("유닛 선택을 감지하는 매니저입니다. 비워두면 UnitSelectionManager.Instance를 사용합니다.")]
    public UnitSelectionManager selectionManager;

    [Tooltip("업그레이드 변경 시 전투 스탯 표시를 갱신합니다. 비워두면 자동으로 찾습니다.")]
    public UpgradeManager upgradeManager;

    [Header("Panel")]
    [Tooltip("선택 정보 패널 전체 루트입니다. 선택이 없으면 숨깁니다.")]
    public GameObject panelRoot;

    [Header("Identity")]
    [Tooltip("이름/부제/초상화를 묶는 ID 패널 루트입니다. 다중 선택 시 숨깁니다.")]
    public GameObject identitySection;
    [Tooltip("선택 대상 이름(유닛/건물 표시명)을 표시할 텍스트입니다.")]
    public TextMeshProUGUI nameText;
    [Tooltip("SelectableEntity.portrait 또는 UnitData.portrait를 표시할 초상화 이미지입니다.")]
    public Image portraitImage;

    [Header("Health")]
    [Tooltip("현재/최대 체력 수치를 표시할 텍스트입니다.")]
    public TextMeshProUGUI healthText;

    [Header("Combat")]
    [Tooltip("공격·사거리·이동·시야 등 전투 스탯 영역 루트입니다.")]
    public GameObject combatSection;
    [Tooltip("공격력을 표시할 텍스트입니다.")]
    public TextMeshProUGUI attackText;
    [Tooltip("사거리를 표시할 텍스트입니다.")]
    public TextMeshProUGUI rangeText;
    [Tooltip("이동 속도를 표시할 텍스트입니다. 건물 선택 시 숨깁니다.")]
    public TextMeshProUGUI moveSpeedText;
    [Tooltip("시야 범위를 표시할 텍스트입니다.")]
    public TextMeshProUGUI visionText;

    [Header("Production")]
    [Tooltip("생산 건물 정보 영역 루트입니다. 생산 건물이 아니면 숨깁니다.")]
    public GameObject productionSection;
    [Tooltip("생산 중인 유닛의 초상화(SelectableEntity.portrait)를 표시할 이미지입니다.")]
    public Image productionIconImage;
    [Tooltip("생산 중인 유닛 이름을 표시할 텍스트입니다.")]
    public TextMeshProUGUI productionTitleText;
    [Tooltip("생산 상태(대기/생산 중), 보유 수, 한도 등을 표시할 텍스트입니다.")]
    public TextMeshProUGUI productionDetailText;
    [Tooltip("현재 생산 진행률(0~1)을 표시할 슬라이더입니다.")]
    public Slider productionSlider;

    [Header("Upgrade Research")]
    [Tooltip("업그레이드 건물 연구 정보 영역 루트입니다. UpgradeBuilding 선택 시에만 표시합니다.")]
    public GameObject upgradeResearchSection;
    [Tooltip("연구 중인 업그레이드 아이콘입니다.")]
    public Image upgradeResearchIconImage;
    [Tooltip("연구 중인 업그레이드 이름을 표시할 텍스트입니다.")]
    public TextMeshProUGUI upgradeResearchTitleText;
    [Tooltip("연구 상태/남은 시간 등을 표시할 텍스트입니다.")]
    public TextMeshProUGUI upgradeResearchDetailText;
    [Tooltip("현재 연구 진행률(0~1)을 표시할 슬라이더입니다.")]
    public Slider upgradeResearchSlider;

    [Header("Multi Selection")]
    [Tooltip("여러 개 선택 시 각 대상 초상화를 나열할 그리드 부모입니다. (Grid Layout Group 등을 붙인 오브젝트)")]
    public Transform portraitGridParent;
    [Tooltip("초상화 1칸 프리팹입니다. SelectionPortraitCell 컴포넌트가 있어야 합니다.")]
    public SelectionPortraitCell portraitCellPrefab;
    [Tooltip("(선택) 초상화 그리드 영역 전체 루트입니다. 단일/빈 선택 시 숨깁니다.")]
    public GameObject multiSelectionSection;
    [Tooltip("초상화 그리드에 표시할 최대 칸 수입니다. 0 이하이면 제한 없음.")]
    public int maxPortraitCells = 0;

    [Header("Multi Selection - Cell Size")]
    [Tooltip("켜면 선택 수에 따라 초상화 셀 크기를 자동으로 조절합니다.")]
    public bool dynamicCellSize = false;
    [Tooltip("셀 크기를 조절할 그리드입니다. 비워두면 portraitGridParent에서 GridLayoutGroup을 찾습니다.")]
    public GridLayoutGroup portraitGrid;
    [Tooltip("기본 셀 크기입니다. (선택 수가 축소 기준 미만일 때)")]
    public Vector2 defaultCellSize = new Vector2(96f, 96f);
    [Tooltip("이 개수 이상 선택하면 셀 크기를 shrunkCellSize로 줄입니다. (1차)")]
    public int cellShrinkThreshold = 8;
    [Tooltip("1차 축소 기준 이상 선택 시 사용할 셀 크기입니다.")]
    public Vector2 shrunkCellSize = new Vector2(56f, 56f);
    [Tooltip("이 개수 이상 선택하면 셀 크기를 shrunkCellSize2로 더 줄입니다. (2차)")]
    public int cellShrinkThreshold2 = 16;
    [Tooltip("2차 축소 기준 이상 선택 시 사용할 셀 크기입니다.")]
    public Vector2 shrunkCellSize2 = new Vector2(40f, 40f);

    [Tooltip("켜면 다중 선택 초상화 그리드 생성 과정을 Console에 출력합니다.")]
    public bool debugPortraitGrid = false;

    [Header("Text Formats")]
    [Tooltip("체력 텍스트 형식입니다. {0}=현재 체력, {1}=최대 체력")]
    public string healthFormat = "{0:0} / {1:0}";
    [Tooltip("공격력 텍스트 형식입니다. {0}=공격력")]
    public string attackFormat = "공격 {0:0}";
    [Tooltip("사거리 텍스트 형식입니다. {0}=사거리")]
    public string rangeFormat = "사거리 {0:0.0}";
    [Tooltip("이동 속도 텍스트 형식입니다. {0}=이동 속도")]
    public string moveSpeedFormat = "이동 {0:0.0}";
    [Tooltip("시야 범위 텍스트 형식입니다. {0}=시야")]
    public string visionFormat = "시야 {0:0}";
    [Tooltip("유닛만 여러 개 선택했을 때 제목 형식입니다. {0}=유닛 수")]
    public string multiUnitFormat = "유닛 {0}기";
    [Tooltip("건물만 여러 개 선택했을 때 제목 형식입니다. {0}=건물 수")]
    public string multiBuildingFormat = "건물 {0}개";
    [Tooltip("유닛과 건물을 함께 선택했을 때 제목 형식입니다. {0}=유닛 수, {1}=건물 수")]
    public string multiMixedFormat = "유닛 {0} / 건물 {1}";
    [Tooltip("생산 건물 보유 유닛 수 형식입니다. {0}=현재 보유, {1}=최대 보유")]
    public string productionCountFormat = "보유 {0} / {1}";
    [Tooltip("유닛을 생산 중일 때 표시할 문구입니다. {0}=남은 초")]
    public string productionProgressFormat = "생산 중  {0:0.0}s";
    [Tooltip("생산 대기 중일 때 표시할 문구입니다.")]
    public string productionIdleFormat = "대기 중";
    [Tooltip("생산 한도에 도달했을 때 표시할 문구입니다.")]
    public string productionCapacityFormat = "생산 한도";
    [Tooltip("업그레이드 연구 중일 때 표시할 문구입니다. {0}=남은 초")]
    public string upgradeResearchProgressFormat = "연구 중  {0:0.0}s";
    [Tooltip("업그레이드 연구가 없을 때 표시할 문구입니다.")]
    public string upgradeResearchIdleFormat = "대기 중";

    // 단일 선택 시 생산 진행률 갱신 대상
    SelectableEntity trackedEntity;
    // 체력 변경 이벤트 구독 대상
    EntityHealth trackedHealth;
    // 다중 선택 초상화 셀 풀
    readonly List<SelectionPortraitCell> spawnedPortraitCells =
        new List<SelectionPortraitCell>();
    // 현재 다중 선택 초상화 그리드를 표시 중인지
    bool multiSelectionActive;

    void Start()
    {
        if (selectionManager == null)
            selectionManager = UnitSelectionManager.Instance;

        if (selectionManager == null)
            selectionManager = FindObjectOfType<UnitSelectionManager>();

        if (selectionManager != null)
            selectionManager.OnSelectionChanged += HandleSelectionChanged;

        if (upgradeManager == null)
            upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
            upgradeManager = FindObjectOfType<UpgradeManager>();

        if (upgradeManager != null)
            upgradeManager.OnUpgradesChanged += HandleUpgradesChanged;

        if (portraitGrid == null && portraitGridParent != null)
            portraitGrid = portraitGridParent.GetComponent<GridLayoutGroup>();

        RefreshPanel();
    }

    void OnDestroy()
    {
        UnsubscribeHealth();

        if (selectionManager != null)
            selectionManager.OnSelectionChanged -= HandleSelectionChanged;

        if (upgradeManager != null)
            upgradeManager.OnUpgradesChanged -= HandleUpgradesChanged;
    }

    void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf)
            return;

        if (multiSelectionActive)
        {
            RefreshPortraitCellHealth();
            return;
        }

        if (trackedEntity == null)
            return;

        ProductionBuilding production = trackedEntity.GetComponent<ProductionBuilding>();
        if (production != null)
            RefreshProduction(production);

        if (trackedEntity.GetComponent<UpgradeBuilding>() != null)
            RefreshUpgradeResearch(showSection: true);
    }

    void HandleSelectionChanged()
    {
        RefreshPanel();
    }

    // 업그레이드가 변경되면 단일 선택 대상의 전투 스탯 표시를 갱신합니다.
    // (최대 체력은 EntityHealth.OnHealthChanged로 자동 갱신됩니다.)
    void HandleUpgradesChanged()
    {
        if (multiSelectionActive || trackedEntity == null)
            return;

        UnitData data = SelectionInfoUtility.GetUnitData(trackedEntity);
        RefreshCombat(trackedEntity, data);
        RefreshProduction(trackedEntity.GetComponent<ProductionBuilding>());
    }

    void HandleTrackedHealthChanged(float currentHealth, float maxHealth)
    {
        RefreshHealth(currentHealth, maxHealth);
    }

    void RefreshPanel()
    {
        UnsubscribeHealth();
        trackedEntity = null;

        if (selectionManager == null)
        {
            SetPanelVisible(false);
            return;
        }

        IReadOnlyList<SelectableEntity> selected = selectionManager.GetSelectedEntities();

        if (debugPortraitGrid)
            Debug.Log($"[SelectionInfoPanel] RefreshPanel: 선택 수={(selected == null ? 0 : selected.Count)}", this);

        if (selected == null || selected.Count == 0)
        {
            SetPanelVisible(false);
            return;
        }

        SetPanelVisible(true);

        if (selected.Count == 1)
        {
            ShowSingleSelection(selected[0]);
            return;
        }

        ShowMultiSelection(selected);
    }

    void ShowSingleSelection(SelectableEntity entity)
    {
        if (entity == null)
        {
            SetPanelVisible(false);
            return;
        }

        multiSelectionActive = false;
        ClearPortraitGrid();

        SetSectionActive(identitySection, true);

        trackedEntity = entity;
        UnitData data = SelectionInfoUtility.GetUnitData(entity);

        SetText(nameText, SelectionInfoUtility.GetDisplayName(entity, data));
        SetPortrait(SelectionInfoUtility.GetPortrait(entity, data));

        EntityHealth health = entity.GetComponent<EntityHealth>();

        if (health != null)
        {
            trackedHealth = health;
            trackedHealth.OnHealthChanged += HandleTrackedHealthChanged;
            RefreshHealth(health.CurrentHealth, health.MaxHealth);
        }

        RefreshCombat(entity, data);
        RefreshProduction(entity.GetComponent<ProductionBuilding>());
        RefreshUpgradeResearch(entity.GetComponent<UpgradeBuilding>() != null);
    }

    void ShowMultiSelection(IReadOnlyList<SelectableEntity> selected)
    {
        int unitCount = 0;
        int buildingCount = 0;

        foreach (SelectableEntity entity in selected)
        {
            if (entity == null)
                continue;

            if (entity.entityType == SelectableEntityType.Unit)
                unitCount++;
            else
                buildingCount++;
        }

        SetText(nameText, SelectionInfoUtility.GetMultiSelectionTitle(
            unitCount,
            buildingCount,
            multiUnitFormat,
            multiBuildingFormat,
            multiMixedFormat));

        SetPortrait(null);
        SetSectionActive(identitySection, false);
        SetSectionActive(combatSection, false);
        SetSectionActive(productionSection, false);
        SetSectionActive(upgradeResearchSection, false);

        multiSelectionActive = true;
        RefreshPortraitGrid(selected);
    }

    void RefreshPortraitGrid(IReadOnlyList<SelectableEntity> selected)
    {
        SetSectionActive(multiSelectionSection, true);
        ApplyCellSize(selected.Count);

        if (debugPortraitGrid)
        {
            Debug.Log(
                $"[SelectionInfoPanel] RefreshPortraitGrid 시작: gridParent={(portraitGridParent == null ? "없음(null)" : portraitGridParent.name)}, " +
                $"cellPrefab={(portraitCellPrefab == null ? "없음(null)" : portraitCellPrefab.name)}, " +
                $"section={(multiSelectionSection == null ? "없음(null)" : multiSelectionSection.name)}, " +
                $"maxCells={maxPortraitCells}, 선택 수={selected.Count}",
                this);
        }

        if (portraitGridParent == null || portraitCellPrefab == null)
        {
            if (debugPortraitGrid)
                Debug.LogWarning(
                    "[SelectionInfoPanel] 셀 생성 중단: portraitGridParent 또는 portraitCellPrefab이 인스펙터에 연결되지 않았습니다.",
                    this);

            return;
        }

        int limit = maxPortraitCells > 0
            ? Mathf.Min(selected.Count, maxPortraitCells)
            : selected.Count;

        int shown = 0;

        for (int i = 0; i < selected.Count && shown < limit; i++)
        {
            SelectableEntity entity = selected[i];

            if (entity == null)
            {
                if (debugPortraitGrid)
                    Debug.Log($"[SelectionInfoPanel] 셀 건너뜀: 선택 인덱스 {i}가 null", this);

                continue;
            }

            SelectionPortraitCell cell = GetOrCreatePortraitCell(shown);
            cell.gameObject.SetActive(true);

            UnitData data = SelectionInfoUtility.GetUnitData(entity);

            cell.SetEntity(
                entity,
                SelectionInfoUtility.GetPortrait(entity, data),
                SelectionInfoUtility.GetDisplayName(entity, data));

            if (debugPortraitGrid)
                Debug.Log(
                    $"[SelectionInfoPanel] 셀 #{shown} 설정: entity={entity.name}, " +
                    $"portrait={(SelectionInfoUtility.GetPortrait(entity, data) == null ? "없음" : "있음")}, " +
                    $"cellActive={cell.gameObject.activeInHierarchy}",
                    this);

            shown++;
        }

        for (int i = shown; i < spawnedPortraitCells.Count; i++)
        {
            if (spawnedPortraitCells[i] != null)
                spawnedPortraitCells[i].gameObject.SetActive(false);
        }

        if (debugPortraitGrid)
            Debug.Log(
                $"[SelectionInfoPanel] RefreshPortraitGrid 완료: 표시={shown}, 풀 크기={spawnedPortraitCells.Count}",
                this);
    }

    void ApplyCellSize(int selectedCount)
    {
        if (!dynamicCellSize || portraitGrid == null)
            return;

        if (selectedCount >= cellShrinkThreshold2)
            portraitGrid.cellSize = shrunkCellSize2;
        else if (selectedCount >= cellShrinkThreshold)
            portraitGrid.cellSize = shrunkCellSize;
        else
            portraitGrid.cellSize = defaultCellSize;

        if (debugPortraitGrid)
            Debug.Log(
                $"[SelectionInfoPanel] ApplyCellSize: 선택 수={selectedCount}, " +
                $"기준={cellShrinkThreshold}, 셀크기={portraitGrid.cellSize}",
                this);
    }

    SelectionPortraitCell GetOrCreatePortraitCell(int index)
    {
        if (index < spawnedPortraitCells.Count && spawnedPortraitCells[index] != null)
            return spawnedPortraitCells[index];

        SelectionPortraitCell cell =
            Instantiate(portraitCellPrefab, portraitGridParent);

        if (debugPortraitGrid)
            Debug.Log(
                $"[SelectionInfoPanel] 셀 인스턴스 생성 #{index}: {cell.name} (parent={portraitGridParent.name})",
                cell);

        if (index < spawnedPortraitCells.Count)
            spawnedPortraitCells[index] = cell;
        else
            spawnedPortraitCells.Add(cell);

        return cell;
    }

    void RefreshPortraitCellHealth()
    {
        for (int i = 0; i < spawnedPortraitCells.Count; i++)
        {
            SelectionPortraitCell cell = spawnedPortraitCells[i];

            if (cell != null && cell.gameObject.activeSelf)
                cell.RefreshHealth();
        }
    }

    void ClearPortraitGrid()
    {
        for (int i = 0; i < spawnedPortraitCells.Count; i++)
        {
            if (spawnedPortraitCells[i] != null)
                spawnedPortraitCells[i].gameObject.SetActive(false);
        }

        SetSectionActive(multiSelectionSection, false);
    }

    void RefreshHealth(float currentHealth, float maxHealth)
    {
        SetText(healthText, string.Format(healthFormat, currentHealth, maxHealth));
    }

    void RefreshCombat(SelectableEntity entity, UnitData data)
    {
        bool showCombat = combatSection != null &&
            (attackText != null || rangeText != null || moveSpeedText != null || visionText != null);

        if (!showCombat)
            return;

        UnitAttacker attacker = entity.GetComponent<UnitAttacker>();
        bool hasCombatData = attacker != null || data != null;

        SetSectionActive(combatSection, hasCombatData);

        if (!hasCombatData)
            return;

        float attackDamage = attacker != null ? attacker.EffectiveAttackDamage : data.attackDamage;
        float attackRange = attacker != null ? attacker.attackRange : data.attackRange;
        float moveSpeed = data != null ? data.moveSpeed : 0f;
        float visionRange = data != null ? data.visionRange : 0f;

        SetText(attackText, attackDamage > 0f
            ? string.Format(attackFormat, attackDamage)
            : string.Empty);

        SetText(rangeText, attackRange > 0f
            ? string.Format(rangeFormat, attackRange)
            : string.Empty);

        if (entity.entityType == SelectableEntityType.Unit)
        {
            SetText(moveSpeedText, moveSpeed > 0f
                ? string.Format(moveSpeedFormat, moveSpeed)
                : string.Empty);
        }
        else
        {
            SetText(moveSpeedText, string.Empty);
        }

        SetText(visionText, visionRange > 0f
            ? string.Format(visionFormat, visionRange)
            : string.Empty);
    }

    void RefreshProduction(ProductionBuilding production)
    {
        bool showProduction = productionSection != null && production != null &&
            production.recipe != null && production.recipe.unitPrefab != null;

        SetSectionActive(productionSection, showProduction);

        if (!showProduction)
            return;

        ProductionRecipe recipe = production.recipe;
        UnitData unitData = SelectionInfoUtility.GetUnitData(recipe.unitPrefab);

        if (productionIconImage != null)
        {
            Sprite icon = SelectionInfoUtility.GetPortrait(recipe.unitPrefab, unitData);
            productionIconImage.sprite = icon;
            productionIconImage.enabled = icon != null;
        }

        SetText(
            productionTitleText,
            SelectionInfoUtility.GetDisplayName(recipe.unitPrefab, unitData));

        int maxAlive = production.MaxAliveCount;
        string countText = maxAlive > 0
            ? string.Format(productionCountFormat, production.AliveCount, maxAlive)
            : string.Empty;

        string statusText;
        float sliderValue;

        if (production.IsAtCapacity)
        {
            statusText = productionCapacityFormat;
            sliderValue = 1f;
        }
        else if (production.IsProducing)
        {
            statusText = string.Format(
                productionProgressFormat,
                production.ProductionRemainingTime);
            sliderValue = production.ProductionProgress;
        }
        else
        {
            statusText = productionIdleFormat;
            sliderValue = 0f;
        }

        SetText(
            productionDetailText,
            string.IsNullOrEmpty(countText)
                ? statusText
                : $"{statusText}  {countText}");

        if (productionSlider != null)
        {
            productionSlider.minValue = 0f;
            productionSlider.maxValue = 1f;
            productionSlider.value = sliderValue;
        }
    }

    void RefreshUpgradeResearch(bool showSection)
    {
        if (upgradeResearchSection == null &&
            upgradeResearchIconImage == null &&
            upgradeResearchTitleText == null &&
            upgradeResearchDetailText == null &&
            upgradeResearchSlider == null)
        {
            return;
        }

        bool hasUpgradeBuilding = showSection;
        SetSectionActive(upgradeResearchSection, hasUpgradeBuilding);

        if (!hasUpgradeBuilding)
            return;

        UpgradeDefinition research = upgradeManager != null
            ? upgradeManager.ActiveResearch
            : null;
        bool researching = research != null;

        if (upgradeResearchIconImage != null)
        {
            Sprite icon = researching ? research.icon : null;
            upgradeResearchIconImage.sprite = icon;
            upgradeResearchIconImage.enabled = icon != null;
        }

        SetText(
            upgradeResearchTitleText,
            researching ? research.displayName : string.Empty);

        if (researching)
        {
            SetText(
                upgradeResearchDetailText,
                string.Format(
                    upgradeResearchProgressFormat,
                    upgradeManager.ResearchRemainingTime));
        }
        else
        {
            SetText(upgradeResearchDetailText, upgradeResearchIdleFormat);
        }

        if (upgradeResearchSlider != null)
        {
            upgradeResearchSlider.minValue = 0f;
            upgradeResearchSlider.maxValue = 1f;
            upgradeResearchSlider.value = researching
                ? upgradeManager.ResearchProgress
                : 0f;
        }
    }

    void UnsubscribeHealth()
    {
        if (trackedHealth == null)
            return;

        trackedHealth.OnHealthChanged -= HandleTrackedHealthChanged;
        trackedHealth = null;
    }

    void SetPanelVisible(bool visible)
    {
        if (!visible)
            ClearPanelContent();

        if (panelRoot != null)
            panelRoot.SetActive(visible);
    }

    void ClearPanelContent()
    {
        SetSectionActive(identitySection, false);
        SetSectionActive(combatSection, false);
        SetSectionActive(productionSection, false);
        SetSectionActive(upgradeResearchSection, false);
        SetText(nameText, string.Empty);
        SetPortrait(null);

        multiSelectionActive = false;
        ClearPortraitGrid();
    }

    static void SetSectionActive(GameObject section, bool active)
    {
        if (section != null)
            section.SetActive(active);
    }

    static void SetText(TextMeshProUGUI text, string value)
    {
        if (text == null)
            return;

        text.text = value ?? string.Empty;
    }

    void SetPortrait(Sprite sprite)
    {
        if (portraitImage == null)
            return;

        portraitImage.sprite = sprite;
        portraitImage.enabled = sprite != null;
    }
}

public static class SelectionInfoUtility
{
    public static UnitData GetUnitData(Component source)
    {
        if (source == null)
            return null;

        Unit unit = source.GetComponent<Unit>();

        if (unit != null && unit.data != null)
            return unit.data;

        Building building = source.GetComponent<Building>();

        if (building != null && building.data != null)
            return building.data;

        return null;
    }

    public static UnitData GetUnitData(GameObject source)
    {
        return source == null ? null : GetUnitData(source.transform);
    }

    public static Sprite GetPortrait(Component source, UnitData data)
    {
        if (source != null)
        {
            SelectableEntity entity = source.GetComponent<SelectableEntity>();

            if (entity != null && entity.portrait != null)
                return entity.portrait;
        }

        return data != null ? data.portrait : null;
    }

    public static Sprite GetPortrait(GameObject source, UnitData data)
    {
        return GetPortrait(source != null ? source.transform : null, data);
    }

    public static string GetDisplayName(Component source, UnitData data)
    {
        if (data != null && !string.IsNullOrWhiteSpace(data.displayName))
            return data.displayName;

        if (data != null && !string.IsNullOrWhiteSpace(data.entityTypeId))
            return data.entityTypeId;

        return source != null ? source.name : string.Empty;
    }

    public static string GetDisplayName(GameObject source, UnitData data)
    {
        return GetDisplayName(source != null ? source.transform : null, data);
    }

    public static string GetSubtitle(SelectableEntity entity, UnitData data)
    {
        if (entity == null)
            return string.Empty;

        if (entity.entityType == SelectableEntityType.Building)
        {
            if (entity.GetComponent<Headquarters>() != null)
                return "본부";

            if (entity.GetComponent<BuildZoneProvider>() != null)
                return "전초기지";

            if (entity.GetComponent<ProductionBuilding>() != null)
                return "생산 건물";

            if (entity.GetComponent<UnitAttacker>() != null)
                return "방어 건물";
        }

        if (data != null && data.canAttack && !data.canMoveManually)
            return "고정 포대";

        if (data != null && data.canAttack)
            return "전투 유닛";

        if (data != null && data.canMoveManually)
            return "이동 유닛";

        return entity.entityType == SelectableEntityType.Unit ? "유닛" : "건물";
    }

    public static string GetMultiSelectionTitle(
        int unitCount,
        int buildingCount,
        string unitFormat,
        string buildingFormat,
        string mixedFormat)
    {
        if (unitCount > 0 && buildingCount == 0)
            return string.Format(unitFormat, unitCount);

        if (buildingCount > 0 && unitCount == 0)
            return string.Format(buildingFormat, buildingCount);

        return string.Format(mixedFormat, unitCount, buildingCount);
    }
}
