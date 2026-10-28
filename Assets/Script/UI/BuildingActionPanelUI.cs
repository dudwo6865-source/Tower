using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택한 건물의 업그레이드 분기와 생산 레시피를 한 패널에서 보여주는 패널입니다.
/// - 업그레이드: 타워·생산 건물의 업그레이드 분기 버튼
/// - 생산: 생산 건물의 레시피 선택 버튼과 일시정지 버튼
/// 둘 중 하나라도 해당하면 패널이 켜지고, 해당하지 않는 쪽 버튼은 숨깁니다.
/// Building Command Panel과 독립적으로 켜지고 꺼집니다.
/// (예전 TowerUpgradePanelUI와 같은 스크립트라 씬에 설정해 둔 업그레이드 슬롯은 그대로 유지됩니다.)
/// </summary>
public class BuildingActionPanelUI : MonoBehaviour
{
    [System.Serializable]
    public class UpgradeSlot
    {
        [Label("버튼")]
        public Button button;

        [Label("라벨 텍스트")]
        [Tooltip("분기 이름과 비용을 표시할 텍스트입니다. 비워두면 건너뜁니다.")]
        public TextMeshProUGUI label;

        [Label("단축키 텍스트")]
        [Tooltip("업그레이드 슬롯에 대응하는 단축키를 표시할 텍스트입니다. 단축키를 사용하지 않으면 비웁니다. 위치와 외형은 에디터에서 설정합니다.")]
        public TextMeshProUGUI hotkeyLabel;

        [Label("아이콘")]
        [Tooltip("상위 건물 썸네일을 표시할 Image입니다. 비워두면 건너뜁니다.")]
        public Image icon;
    }

    [System.Serializable]
    public class RecipeSlot
    {
        [Label("버튼")]
        public Button button;

        [Label("애니메이터")]
        [Tooltip("현재 생산 중인 레시피일 때 '선택 bool 파라미터'를 켤 Animator입니다. 비워두면 버튼 오브젝트의 Animator를 씁니다.")]
        public Animator animator;

        // 애니메이터에 선택 파라미터가 있는지 한 번만 확인해 둔다.
        [System.NonSerialized] public Animator cachedAnimator;
        [System.NonSerialized] public string cachedParameter;
        [System.NonSerialized] public bool hasSelectedParameter;

        [Label("라벨 텍스트")]
        [Tooltip("유닛 이름과 생산 정보를 표시할 텍스트입니다. 비워두면 건너뜁니다.")]
        public TextMeshProUGUI label;

        [Label("아이콘")]
        [Tooltip("유닛 아이콘을 표시할 Image입니다. 비워두면 건너뜁니다.")]
        public Image icon;

        [Label("잠금 표시")]
        [Tooltip("해금 조건을 채우지 못한 레시피일 때만 켜지는 오브젝트(자물쇠 등)입니다. 비워두면 건너뜁니다.")]
        public GameObject lockedMark;

        [Label("단축키 텍스트")]
        [Tooltip("슬롯에 대응하는 단축키 글자를 표시할 텍스트입니다. 비워두면 건너뜁니다.")]
        public TextMeshProUGUI hotkeyLabel;
    }

    [Header("패널")]
    [Label("패널 루트")]
    [Tooltip("업그레이드할 수 있거나 생산 건물인 건물을 선택했을 때만 표시할 패널 루트입니다.")]
    public GameObject panelRoot;

    [Label("업그레이드 묶음")]
    [Tooltip("업그레이드 버튼들을 묶은 오브젝트입니다. 업그레이드가 없으면 끕니다. 비워두면 버튼만 개별로 숨깁니다.")]
    public GameObject upgradeGroup;

    [Label("생산 묶음")]
    [Tooltip("레시피·일시정지 버튼들을 묶은 오브젝트입니다. 생산 건물이 아니면 끕니다. 비워두면 버튼만 개별로 숨깁니다.")]
    public GameObject productionGroup;

    [Header("업그레이드 슬롯")]
    [Label("업그레이드 슬롯")]
    [Tooltip("건물 데이터의 업그레이드 분기 순서대로 채워지고, 남는 슬롯은 숨겨집니다.")]
    public UpgradeSlot[] slots;

    [Label("업그레이드 라벨 형식")]
    [Tooltip("{0}=상위 건물 이름, {1}=소비 Watt")]
    [TextArea(2, 4)]
    public string labelFormat = "{0}\n{1} W";

    [Label("구매 가능 색")]
    [Tooltip("Watt가 충분할 때 아이콘 색상입니다.")]
    public Color affordableTint = Color.white;

    [Label("구매 불가 색")]
    [Tooltip("Watt가 부족할 때 아이콘 색상입니다.")]
    public Color unaffordableTint = new Color(1f, 1f, 1f, 0.4f);

    [Header("생산 슬롯")]
    [Label("레시피 슬롯")]
    [Tooltip("건물의 생산 가능 레시피 순서대로 채워지고, 남는 슬롯은 숨겨집니다.")]
    public RecipeSlot[] recipeSlots;

    [Label("레시피 하나면 숨김")]
    [Tooltip("고를 레시피가 하나뿐인 건물을 선택했을 때 레시피 버튼을 숨깁니다. 일시정지 버튼은 그대로 보입니다.")]
    public bool hideWhenSingleRecipe;

    [Label("레시피 라벨 형식")]
    [Tooltip("{0}=유닛 이름, {1}=생산 간격(초), {2}=한 마리당 Watt, {3}=초당 Watt, {4}=인구수")]
    [TextArea(2, 4)]
    public string recipeLabelFormat = "{0}\n{1:0.#}초 · {3:0.#} W/s";

    [Label("잠긴 레시피 라벨 형식")]
    [Tooltip("해금 조건을 채우지 못한 레시피의 라벨 형식입니다. {0}=유닛 이름, {1}=부족한 조건")]
    [TextArea(2, 4)]
    public string recipeLockedLabelFormat = "{0}\n<size=70%>필요: {1}</size>";

    [Label("선택 bool 파라미터")]
    [Tooltip("레시피 버튼 Animator에서 현재 생산 중인 레시피일 때 true가 되는 bool 파라미터 이름입니다. " +
        "Button의 Animation 트리거(Normal·Selected 등)와 이름이 겹치지 않게 하세요. 비워두면 쓰지 않습니다.")]
    public string recipeSelectedBool = "IsSelected";

    [Label("설명 텍스트")]
    [Tooltip("현재 생산 중인 레시피의 설명을 표시할 텍스트입니다. 비워두면 건너뜁니다.")]
    public TextMeshProUGUI descriptionText;

    [Header("일시정지")]
    [Label("일시정지 버튼")]
    [Tooltip("선택한 생산 건물의 생산을 멈추거나 재개합니다. 하나라도 생산 중이면 모두 멈추고, 모두 멈춰 있으면 모두 재개합니다. 비워두면 건너뜁니다.")]
    public Button pauseButton;

    [Label("일시정지 버튼 텍스트")]
    [Tooltip("일시정지 버튼의 글자입니다. 상태에 따라 아래 두 문구로 바뀝니다. 비워두면 건너뜁니다.")]
    public TextMeshProUGUI pauseButtonLabel;

    [Label("일시정지 문구")]
    [Tooltip("생산 중일 때 버튼에 표시할 문구입니다.")]
    public string pauseText = "일시정지";

    [Label("재개 문구")]
    [Tooltip("일시정지 중일 때 버튼에 표시할 문구입니다.")]
    public string resumeText = "재개";

    [Label("일시정지 표시")]
    [Tooltip("선택한 생산 건물이 모두 일시정지일 때만 켜지는 오브젝트입니다. 비워두면 건너뜁니다.")]
    public GameObject pausedMark;

    [Header("단축키")]
    [Label("단축키 사용")]
    public bool enableHotkeys = true;

    [Label("업그레이드 단축키")]
    [Tooltip("업그레이드 슬롯 순서대로 대응하는 단축키입니다.")]
    public KeyCode[] hotkeys = { KeyCode.U, KeyCode.I, KeyCode.O };

    [Label("레시피 단축키")]
    [Tooltip("레시피 슬롯 순서대로 대응하는 단축키입니다.")]
    public KeyCode[] recipeHotkeys = { KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B, KeyCode.N };

    [Label("일시정지 단축키")]
    [Tooltip("일시정지 버튼에 대응하는 단축키입니다. None이면 쓰지 않습니다.")]
    public KeyCode pauseHotkey = KeyCode.J;

    readonly List<BuildingUpgradeOption> upgradeOptions = new List<BuildingUpgradeOption>();
    readonly List<ProductionRecipe> recipeOptions = new List<ProductionRecipe>();

    UnitSelectionManager selectionManager;
    ProductionBuilding primaryBuilding;
    CanvasGroup selfHideGroup;
    bool panelVisible;
    bool upgradeVisible;
    bool upgradeReady;
    bool productionVisible;

    void Start()
    {
        selectionManager = UnitSelectionManager.Instance;

        if (selectionManager == null)
            selectionManager = FindObjectOfType<UnitSelectionManager>();

        if (selectionManager != null)
            selectionManager.OnSelectionChanged += HandleSelectionChanged;

        BindSlots();
        RefreshVisibility();
    }

    void OnDestroy()
    {
        if (selectionManager != null)
            selectionManager.OnSelectionChanged -= HandleSelectionChanged;
    }

    void Update()
    {
        RefreshVisibility();

        if (!enableHotkeys || !panelVisible || IsTextInputFocused())
            return;

        HandleHotkeys();
    }

    void HandleSelectionChanged()
    {
        RefreshVisibility();
    }

    public void RefreshVisibility()
    {
        RefreshHotkeyLabels();
        upgradeVisible = BuildingCommandHandler.ShouldShowUpgradePanel(upgradeOptions, out upgradeReady);
        productionVisible = BuildingCommandHandler.ShouldShowProductionPanel(recipeOptions, out primaryBuilding);

        SetPanelVisible(upgradeVisible || productionVisible);

        if (!panelVisible)
            return;

        SetGroupActive(upgradeGroup, upgradeVisible);
        SetGroupActive(productionGroup, productionVisible);

        RefreshUpgradeSlots();
        RefreshRecipeSlots();
        RefreshPauseButton();
    }

    void SetPanelVisible(bool visible)
    {
        panelVisible = visible;

        if (panelRoot == null)
            return;

        // 이 스크립트가 붙은 오브젝트를 SetActive(false)로 끄면 Update가 멈춰
        // 스스로 다시 켤 수 없다. 그럴 때는 CanvasGroup으로만 감춘다.
        if (transform.IsChildOf(panelRoot.transform))
        {
            if (selfHideGroup == null)
            {
                selfHideGroup = panelRoot.GetComponent<CanvasGroup>();

                if (selfHideGroup == null)
                    selfHideGroup = panelRoot.AddComponent<CanvasGroup>();
            }

            selfHideGroup.alpha = visible ? 1f : 0f;
            selfHideGroup.interactable = visible;
            selfHideGroup.blocksRaycasts = visible;
            return;
        }

        panelRoot.SetActive(visible);
    }

    static void SetGroupActive(GameObject group, bool active)
    {
        if (group != null && group.activeSelf != active)
            group.SetActive(active);
    }

    void BindSlots()
    {
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i].button == null)
                    continue;

                int capturedIndex = i;
                slots[i].button.onClick.AddListener(() => IssueUpgrade(capturedIndex));
            }
        }

        if (recipeSlots != null)
        {
            for (int i = 0; i < recipeSlots.Length; i++)
            {
                if (recipeSlots[i] == null || recipeSlots[i].button == null)
                    continue;

                int capturedIndex = i;
                recipeSlots[i].button.onClick.AddListener(() => IssueRecipe(capturedIndex));
            }
        }

        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);
    }

    void RefreshHotkeyLabels()
    {
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                TextMeshProUGUI hotkeyLabel = slots[i]?.hotkeyLabel;

                if (hotkeyLabel == null)
                    continue;

                bool hasHotkey = enableHotkeys && hotkeys != null && i < hotkeys.Length &&
                    hotkeys[i] != KeyCode.None;
                hotkeyLabel.text = hasHotkey ? hotkeys[i].ToString() : string.Empty;
            }
        }

        if (recipeSlots == null)
            return;

        for (int i = 0; i < recipeSlots.Length; i++)
        {
            TextMeshProUGUI hotkeyLabel = recipeSlots[i]?.hotkeyLabel;

            if (hotkeyLabel == null)
                continue;

            bool hasHotkey = enableHotkeys && recipeHotkeys != null && i < recipeHotkeys.Length &&
                recipeHotkeys[i] != KeyCode.None;
            hotkeyLabel.text = hasHotkey ? recipeHotkeys[i].ToString() : string.Empty;
        }
    }

    // ───────────── 업그레이드 ─────────────

    void RefreshUpgradeSlots()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            UpgradeSlot slot = slots[i];

            if (slot == null || slot.button == null)
                continue;

            if (!upgradeVisible || i >= upgradeOptions.Count)
            {
                slot.button.gameObject.SetActive(false);
                continue;
            }

            BuildingUpgradeOption option = upgradeOptions[i];
            bool canAfford = BuildingUpgradeService.CanAfford(option);

            slot.button.gameObject.SetActive(true);

            // Watt가 모자라거나 건설 잠금 중이면 버튼은 보이되 누를 수 없게 둔다.
            slot.button.interactable = canAfford && upgradeReady;

            if (slot.label != null)
                slot.label.text = FormatLabel("업그레이드 라벨 형식", labelFormat, option.DisplayName, option.ResolveCost());

            if (slot.icon != null)
            {
                Sprite iconSprite = option.Icon;
                slot.icon.sprite = iconSprite;
                slot.icon.enabled = iconSprite != null;
                slot.icon.color = canAfford ? affordableTint : unaffordableTint;
            }
        }
    }

    void IssueUpgrade(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= upgradeOptions.Count)
            return;

        BuildingUpgradeOption option = upgradeOptions[slotIndex];

        if (option == null)
            return;

        ClearUISelection();

        if (BuildingCommandHandler.IssueUpgradeToSelection(option.Target))
            RefreshVisibility();
    }

    // ───────────── 생산 ─────────────

    void RefreshRecipeSlots()
    {
        bool showRecipes = productionVisible && !(hideWhenSingleRecipe && recipeOptions.Count <= 1);
        ProductionRecipe current = primaryBuilding != null ? primaryBuilding.recipe : null;

        if (recipeSlots != null)
        {
            for (int i = 0; i < recipeSlots.Length; i++)
            {
                ProductionRecipe recipe = showRecipes && i < recipeOptions.Count ? recipeOptions[i] : null;
                RefreshRecipeSlot(recipeSlots[i], recipe, current);
            }
        }

        if (descriptionText != null)
            descriptionText.text = productionVisible && current != null ? current.description : string.Empty;
    }

    void RefreshRecipeSlot(RecipeSlot slot, ProductionRecipe recipe, ProductionRecipe current)
    {
        if (slot == null || slot.button == null)
            return;

        if (recipe == null)
        {
            slot.button.gameObject.SetActive(false);
            return;
        }

        bool isCurrent = recipe == current;
        int ownerId = primaryBuilding != null ? primaryBuilding.OwnerId : 1;
        bool unlocked = primaryBuilding != null
            ? primaryBuilding.IsRecipeUnlocked(recipe)
            : recipe.IsUnlocked(ownerId);

        slot.button.gameObject.SetActive(true);
        slot.button.interactable = recipe.unitPrefab != null && unlocked;

        if (slot.label != null)
        {
            slot.label.text = unlocked
                ? FormatLabel(
                    "레시피 라벨 형식",
                    recipeLabelFormat,
                    recipe.GetDisplayName(),
                    recipe.spawnInterval,
                    Mathf.RoundToInt(recipe.TotalWattCost),
                    recipe.wattCostPerSecond,
                    recipe.population)
                : FormatLabel(
                    "잠긴 레시피 라벨 형식",
                    recipeLockedLabelFormat,
                    recipe.GetDisplayName(),
                    primaryBuilding != null
                        ? primaryBuilding.DescribeMissingRequirements(recipe)
                        : recipe.DescribeMissingRequirements(ownerId));
        }

        if (slot.icon != null)
        {
            Sprite iconSprite = recipe.GetIcon();
            slot.icon.sprite = iconSprite;
            slot.icon.enabled = iconSprite != null;
        }

        SetSelectedBool(slot, isCurrent);

        if (slot.lockedMark != null)
            slot.lockedMark.SetActive(!unlocked);
    }

    // 생산 중인 레시피는 버튼 Animator의 bool 파라미터로 표시한다.
    // 오브젝트를 껐다 켜면 Animator 파라미터가 초기화되므로 매번 다시 넣는다.
    void SetSelectedBool(RecipeSlot slot, bool selected)
    {
        Animator target = slot.animator != null ? slot.animator : slot.button.GetComponent<Animator>();

        if (target != slot.cachedAnimator || recipeSelectedBool != slot.cachedParameter)
        {
            slot.cachedAnimator = target;
            slot.cachedParameter = recipeSelectedBool;
            slot.hasSelectedParameter = HasBoolParameter(target, recipeSelectedBool);
        }

        if (slot.hasSelectedParameter && target.isActiveAndEnabled)
            target.SetBool(recipeSelectedBool, selected);
    }

    static bool HasBoolParameter(Animator target, string parameter)
    {
        if (target == null || target.runtimeAnimatorController == null || string.IsNullOrEmpty(parameter))
            return false;

        AnimatorControllerParameter[] parameters = target.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Bool && parameters[i].name == parameter)
                return true;
        }

        return false;
    }

    void IssueRecipe(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= recipeOptions.Count)
            return;

        ClearUISelection();

        if (BuildingCommandHandler.IssueRecipeToSelection(recipeOptions[slotIndex]))
            RefreshVisibility();
    }

    void RefreshPauseButton()
    {
        if (pauseButton != null && pauseButton.gameObject.activeSelf != productionVisible)
            pauseButton.gameObject.SetActive(productionVisible);

        bool paused = productionVisible && BuildingCommandHandler.AreSelectedProductionsPaused();

        if (pauseButtonLabel != null)
            pauseButtonLabel.text = paused ? resumeText : pauseText;

        if (pausedMark != null)
            pausedMark.SetActive(paused);
    }

    void TogglePause()
    {
        ClearUISelection();

        if (BuildingCommandHandler.ToggleProductionPauseForSelection())
            RefreshVisibility();
    }

    // ───────────── 단축키 ─────────────

    void HandleHotkeys()
    {
        if (productionVisible && pauseHotkey != KeyCode.None && Input.GetKeyDown(pauseHotkey))
        {
            TogglePause();
            return;
        }

        if (upgradeVisible && TryHandleSlotHotkeys(hotkeys, slots?.Length ?? 0, i => slots[i]?.button, IssueUpgrade))
            return;

        if (productionVisible)
            TryHandleSlotHotkeys(recipeHotkeys, recipeSlots?.Length ?? 0, i => recipeSlots[i]?.button, IssueRecipe);
    }

    static bool TryHandleSlotHotkeys(
        KeyCode[] keys,
        int slotCount,
        System.Func<int, Button> getButton,
        System.Action<int> issue)
    {
        if (keys == null)
            return false;

        int count = Mathf.Min(slotCount, keys.Length);

        for (int i = 0; i < count; i++)
        {
            if (keys[i] == KeyCode.None || !Input.GetKeyDown(keys[i]))
                continue;

            Button button = getButton(i);

            if (button != null && button.gameObject.activeSelf && button.interactable)
                issue(i);

            return true;
        }

        return false;
    }

    static readonly HashSet<string> warnedFormats = new HashSet<string>();

    // 인스펙터 형식 문자열로 라벨을 만든다. 한 줄로 적은 "\n" 글자도 줄바꿈으로 처리한다.
    // 형식이 잘못됐으면(없는 {번호} 등) 예외 대신 경고를 한 번만 남기고 형식 문자열을 그대로 보여준다.
    static string FormatLabel(string fieldName, string format, params object[] args)
    {
        if (string.IsNullOrEmpty(format))
            return string.Empty;

        format = format.Replace("\\n", "\n");

        try
        {
            return string.Format(format, args);
        }
        catch (System.FormatException)
        {
            if (warnedFormats.Add(format))
                Debug.LogWarning($"BuildingActionPanelUI: '{fieldName}' 형식이 잘못됐습니다. 쓸 수 있는 번호는 {{0}}~{{{args.Length - 1}}}입니다. 형식: {format}");

            return format;
        }
    }

    static void ClearUISelection()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    static bool IsTextInputFocused()
    {
        GameObject current =
            UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;

        if (current == null)
            return false;

        return current.GetComponent<InputField>() != null ||
               current.GetComponent<TMP_InputField>() != null;
    }
}
