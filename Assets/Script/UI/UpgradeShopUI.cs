using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 마석으로 게임 내 한정 업그레이드를 구매하는 UI입니다.
// BuildShopUI와 동일한 방식으로, 각 버튼에 UpgradeDefinition을 연결합니다.
public class UpgradeShopUI : MonoBehaviour
{
    [System.Serializable]
    public class ShopEntry
    {
        [Tooltip("이 버튼이 구매할 업그레이드 정의입니다.")]
        public UpgradeDefinition upgrade;

        public Button button;

        [Tooltip("이름/레벨/비용을 표시할 텍스트입니다. 비워두면 버튼 자식에서 자동으로 찾습니다.")]
        public TextMeshProUGUI label;

        [Tooltip("업그레이드 아이콘 Image입니다. 비워두면 버튼 자식에서 자동으로 찾습니다.")]
        public Image icon;
    }

    [Header("Auto Build")]
    [Tooltip("지정하면 이 패널의 자식 버튼들을 업그레이드 목록 순서대로 자동 배정합니다.\n" +
             "이 경우 아래 Entries는 무시됩니다.")]
    public Transform buttonPanel;

    [Tooltip("자동 배정에 사용할 업그레이드 순서입니다. 비워두면 UpgradeManager의 목록을 사용합니다.")]
    public UpgradeDefinition[] upgradeList;

    [Tooltip("자동 배정 시 업그레이드보다 버튼이 많으면 남는 버튼을 숨깁니다.")]
    public bool hideExtraButtons = true;

    [Header("References")]
    [Tooltip("패널 자동 배정을 쓰지 않을 때 수동으로 지정하는 항목들입니다.")]
    public ShopEntry[] entries;

    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public UpgradeManager upgradeManager;

    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public ManaStoneManager manaStoneManager;

    [Header("Labels")]
    [Tooltip("{0}=이름, {1}=현재레벨, {2}=최대레벨, {3}=다음비용")]
    public string entryFormat = "{0}\nLv {1}/{2}  ({3} 마석)";

    [Tooltip("최대 레벨일 때 표시 형식입니다. {0}=이름, {1}=최대레벨")]
    public string maxedFormat = "{0}\nLv {1} (MAX)";

    [Tooltip("연구 중일 때(이 항목) 표시 형식입니다. {0}=이름, {1}=남은 초")]
    public string researchingFormat = "{0}\n연구 중 {1:0.0}s";

    [Tooltip("다른 연구가 진행 중일 때 표시 형식입니다. {0}=이름, {1}=연구 중인 업그레이드")]
    public string researchBusyFormat = "{0}\n연구 중: {1}";

    [Header("Icon Tint")]
    public Color iconAffordableTint = Color.white;
    public Color iconUnaffordableTint = new Color(1f, 1f, 1f, 0.4f);

    [Header("Visibility")]
    [Tooltip("보이고/숨길 업그레이드 패널 루트입니다. 이 스크립트가 붙은 오브젝트가 아닌 별도 자식이어야 합니다.\n" +
             "비워두면 항상 표시됩니다.")]
    public GameObject panelRoot;

    [Tooltip("켜면 UpgradeBuilding이 붙은 건물을 선택했을 때만 패널을 보여줍니다.")]
    public bool showOnlyWhenBuildingSelected = true;

    [Tooltip("선택을 감지하는 매니저입니다. 비워두면 자동으로 찾습니다.")]
    public UnitSelectionManager selectionManager;

    void Start()
    {
        if (upgradeManager == null)
            upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
            upgradeManager = FindObjectOfType<UpgradeManager>();

        if (manaStoneManager == null)
            manaStoneManager = ManaStoneManager.Instance;

        if (manaStoneManager == null)
            manaStoneManager = FindObjectOfType<ManaStoneManager>();

        if (buttonPanel != null)
            BuildEntriesFromPanel();

        for (int i = 0; i < entries.Length; i++)
        {
            ResolveEntryReferences(entries[i]);
            BindEntry(entries[i]);
        }

        if (manaStoneManager != null)
            manaStoneManager.OnManaStoneChanged += HandleManaStoneChanged;

        if (upgradeManager != null)
            upgradeManager.OnUpgradesChanged += HandleUpgradesChanged;

        if (selectionManager == null)
            selectionManager = UnitSelectionManager.Instance;

        if (selectionManager == null)
            selectionManager = FindObjectOfType<UnitSelectionManager>();

        if (selectionManager != null)
            selectionManager.OnSelectionChanged += HandleSelectionChanged;

        UpdatePanelVisibility();
        RefreshAllEntries();
    }

    void OnDestroy()
    {
        if (manaStoneManager != null)
            manaStoneManager.OnManaStoneChanged -= HandleManaStoneChanged;

        if (upgradeManager != null)
            upgradeManager.OnUpgradesChanged -= HandleUpgradesChanged;

        if (selectionManager != null)
            selectionManager.OnSelectionChanged -= HandleSelectionChanged;
    }

    void HandleSelectionChanged()
    {
        UpdatePanelVisibility();
    }

    void Update()
    {
        // 연구 중이면 버튼 라벨/상태를 주기적으로 갱신한다.
        if (upgradeManager != null &&
            upgradeManager.IsResearching &&
            panelRoot != null &&
            panelRoot.activeSelf)
        {
            RefreshAllEntries();
        }
    }

    // 선택 상태에 따라 업그레이드 패널을 보이거나 숨깁니다.
    void UpdatePanelVisibility()
    {
        if (panelRoot == null)
            return;

        bool visible = !showOnlyWhenBuildingSelected || IsUpgradeBuildingSelected();

        if (panelRoot.activeSelf != visible)
            panelRoot.SetActive(visible);

        // 패널을 다시 열 때 최신 상태로 갱신한다.
        if (visible)
            RefreshAllEntries();
    }

    // 선택 목록에 UpgradeBuilding이 붙은 대상이 있는지 확인합니다.
    bool IsUpgradeBuildingSelected()
    {
        if (selectionManager == null)
            return false;

        IReadOnlyList<SelectableEntity> selected = selectionManager.GetSelectedEntities();

        if (selected == null)
            return false;

        for (int i = 0; i < selected.Count; i++)
        {
            if (selected[i] != null && selected[i].GetComponent<UpgradeBuilding>() != null)
                return true;
        }

        return false;
    }

    // 패널의 자식 버튼들을 업그레이드 목록 순서대로 자동 배정해 entries를 구성합니다.
    void BuildEntriesFromPanel()
    {
        List<Button> buttons = CollectChildButtons(buttonPanel);
        List<UpgradeDefinition> definitions = GetUpgradeSource();

        int count = Mathf.Min(buttons.Count, definitions.Count);
        List<ShopEntry> built = new List<ShopEntry>(count);

        for (int i = 0; i < buttons.Count; i++)
        {
            if (i < count)
            {
                built.Add(new ShopEntry
                {
                    button = buttons[i],
                    upgrade = definitions[i],
                });
            }
            else if (hideExtraButtons)
            {
                // 업그레이드보다 버튼이 많으면 남는 버튼을 숨긴다.
                buttons[i].gameObject.SetActive(false);
            }
        }

        entries = built.ToArray();
    }

    // 패널 직속 자식들 중 Button을 계층 순서대로 모읍니다.
    static List<Button> CollectChildButtons(Transform panel)
    {
        List<Button> result = new List<Button>();

        for (int i = 0; i < panel.childCount; i++)
        {
            Button button = panel.GetChild(i).GetComponent<Button>();

            if (button != null)
                result.Add(button);
        }

        // 직속 자식에 버튼이 없으면 하위 전체에서 찾는다(래퍼 구조 대응).
        if (result.Count == 0)
            result.AddRange(panel.GetComponentsInChildren<Button>(true));

        return result;
    }

    // 자동 배정에 사용할 업그레이드 순서를 반환합니다.
    List<UpgradeDefinition> GetUpgradeSource()
    {
        if (upgradeList != null && upgradeList.Length > 0)
            return new List<UpgradeDefinition>(upgradeList);

        if (upgradeManager != null && upgradeManager.upgrades != null)
            return new List<UpgradeDefinition>(upgradeManager.upgrades);

        return new List<UpgradeDefinition>();
    }

    // 버튼만 지정된 경우, 자식에서 라벨과 아이콘 Image를 자동으로 찾아 채웁니다.
    void ResolveEntryReferences(ShopEntry entry)
    {
        if (entry == null || entry.button == null)
            return;

        if (entry.label == null)
            entry.label = entry.button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (entry.icon == null)
            entry.icon = FindIconImage(entry.button);
    }

    // 버튼 배경(자기 자신의 Image / targetGraphic)을 제외한 첫 자식 Image를 아이콘으로 사용합니다.
    static Image FindIconImage(Button button)
    {
        Image background = button.targetGraphic as Image;
        if (background == null)
            background = button.GetComponent<Image>();

        Image[] images = button.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image == background)
                continue;

            if (image.gameObject == button.gameObject)
                continue;

            return image;
        }

        return null;
    }

    void BindEntry(ShopEntry entry)
    {
        if (entry == null || entry.upgrade == null || entry.button == null)
            return;

        UpgradeDefinition definition = entry.upgrade;
        entry.button.onClick.AddListener(() => TryPurchase(definition));
    }

    void HandleManaStoneChanged(float current)
    {
        RefreshAllEntries();
    }

    void HandleUpgradesChanged()
    {
        RefreshAllEntries();
    }

    void TryPurchase(UpgradeDefinition definition)
    {
        if (upgradeManager == null || definition == null)
            return;

        if (IsSelectedUpgradeBuildingFeatureLocked())
            return;

        upgradeManager.TryPurchase(definition);
        RefreshAllEntries();
    }

    bool IsSelectedUpgradeBuildingFeatureLocked()
    {
        if (selectionManager == null)
            return false;

        IReadOnlyList<SelectableEntity> selected = selectionManager.GetSelectedEntities();
        if (selected == null)
            return false;

        for (int i = 0; i < selected.Count; i++)
        {
            SelectableEntity entity = selected[i];
            if (entity == null || entity.GetComponent<UpgradeBuilding>() == null)
                continue;

            if (BuildingConstructionGate.IsFeatureLockedOn(entity))
                return true;
        }

        return false;
    }

    void RefreshAllEntries()
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
            RefreshEntry(entries[i]);
    }

    void RefreshEntry(ShopEntry entry)
    {
        if (entry == null || entry.upgrade == null)
            return;

        UpgradeDefinition def = entry.upgrade;
        int level = upgradeManager != null ? upgradeManager.GetLevel(def) : 0;
        bool isMax = upgradeManager != null && upgradeManager.IsMaxLevel(def);
        bool canPurchase = upgradeManager != null && upgradeManager.CanPurchase(def);
        bool constructionLocked = IsSelectedUpgradeBuildingFeatureLocked();
        bool isResearchingThis =
            upgradeManager != null &&
            upgradeManager.IsResearching &&
            upgradeManager.ActiveResearch == def;
        bool anyResearching = upgradeManager != null && upgradeManager.IsResearching;

        if (entry.label != null)
        {
            if (isMax)
            {
                entry.label.text = string.Format(maxedFormat, def.displayName, def.maxLevel);
            }
            else if (isResearchingThis)
            {
                entry.label.text = string.Format(
                    researchingFormat,
                    def.displayName,
                    upgradeManager.ResearchRemainingTime);
            }
            else if (anyResearching)
            {
                UpgradeDefinition active = upgradeManager.ActiveResearch;
                entry.label.text = string.Format(
                    researchBusyFormat,
                    def.displayName,
                    active != null ? active.displayName : string.Empty);
            }
            else
            {
                int cost = upgradeManager != null ? upgradeManager.GetNextCost(def) : def.baseCost;
                entry.label.text = string.Format(
                    entryFormat,
                    def.displayName,
                    level,
                    def.maxLevel,
                    cost);
            }
        }

        if (entry.icon != null)
        {
            entry.icon.sprite = def.icon;
            entry.icon.enabled = def.icon != null;
            entry.icon.color = (isMax || canPurchase || isResearchingThis)
                ? iconAffordableTint
                : iconUnaffordableTint;
        }

        if (entry.button != null)
            entry.button.interactable = canPurchase && !constructionLocked;
    }
}
