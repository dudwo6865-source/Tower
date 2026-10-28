using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildShopUI : MonoBehaviour
{
    [System.Serializable]
    public class ShopEntry
    {
        [Label("건물 데이터")]
        [Tooltip("BuildableTowerData 또는 BuildableProductionData를 할당합니다.")]
        public ScriptableObject data;

        [Label("버튼")]
        public Button button;
        [Label("라벨 텍스트")]
        public TextMeshProUGUI label;

        [Label("단축키 텍스트")]
        [Tooltip("해당 상점 항목의 단축키를 표시할 텍스트입니다. 단축키를 사용하지 않는 항목은 빈 문자열로 표시합니다. 위치와 외형은 에디터에서 설정합니다.")]
        public TextMeshProUGUI hotkeyLabel;

        [Label("아이콘")]
        [Tooltip("건물 썸네일을 표시할 Image입니다. 비워두면 아이콘 표시를 건너뜁니다.")]
        public Image icon;

        [Label("버튼 애니메이터")]
        [Tooltip("선택 Bool을 넣을 Animator입니다. 비워두면 버튼 오브젝트의 Animator를 자동으로 찾습니다.")]
        public Animator animator;

        [System.NonSerialized] public bool hasSelectedParameter;

        public IBuildablePlacementData GetBuildData()
        {
            return data as IBuildablePlacementData;
        }
    }

    [Header("참조")]
    [Label("상점 항목")]
    public ShopEntry[] entries;

    [Label("Watt 매니저")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public WattManager wattManager;

    [Label("배치 컨트롤러")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public TowerPlacementController placementController;

    [Header("라벨")]
    [Label("구매 가능 형식")]
    public string affordableFormat = "[{2}] {0}\n{1} W";
    [Label("구매 불가 형식")]
    public string unaffordableFormat = "[{2}] {0}\n{1} W";

    [Header("아이콘 색")]
    [Label("구매 가능 아이콘 색")]
    [Tooltip("구매 가능할 때 아이콘 색상입니다.")]
    public Color iconAffordableTint = Color.white;

    [Label("구매 불가 아이콘 색")]
    [Tooltip("구매 불가능(Watt 부족)할 때 아이콘 색상입니다.")]
    public Color iconUnaffordableTint = new Color(1f, 1f, 1f, 0.4f);

    [Header("선택 애니메이션")]
    [Label("선택 Bool 파라미터")]
    [Tooltip("배치를 시작한 버튼의 Animator에서 건설 확정·취소 전까지 켜 둘 Bool 파라미터 이름입니다.")]
    public string selectedBoolParameter = "IsSelected";

    [Header("단축키")]
    [Label("단축키 사용")]
    [Tooltip("등록 순서대로 Q/W/E/R/T 키로 구매·배치를 시작합니다.")]
    public bool enableHotkeys = true;

    [Label("최대 단축키 슬롯 수")]
    [Tooltip("단축키로 사용할 최대 슬롯 수입니다.")]
    public int maxHotkeySlots = 5;

    [Label("건물 설치 단축키")]
    [Tooltip("상점 항목 순서대로 사용할 키입니다. None이면 해당 항목의 단축키를 끕니다.")]
    public KeyCode[] buildHotkeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T };

    void Start()
    {
        if (wattManager == null)
            wattManager = WattManager.Instance;

        if (wattManager == null)
            wattManager = FindObjectOfType<WattManager>();

        if (placementController == null)
            placementController = TowerPlacementController.Instance;

        if (placementController == null)
            placementController = FindObjectOfType<TowerPlacementController>();

        for (int i = 0; i < entries.Length; i++)
            BindEntry(entries[i], i);

        if (wattManager != null)
            wattManager.OnWattChanged += HandleWattChanged;

        RefreshAllEntries();
    }

    void Update()
    {
        RefreshSelectionStates();

        if (!enableHotkeys || entries == null || entries.Length == 0 || buildHotkeys == null)
            return;

        if (IsTextInputFocused())
            return;

        int hotkeyCount = Mathf.Min(maxHotkeySlots, entries.Length, buildHotkeys.Length);

        for (int i = 0; i < hotkeyCount; i++)
        {
            if (buildHotkeys[i] == KeyCode.None || !Input.GetKeyDown(buildHotkeys[i]))
                continue;

            TryBeginPlacementByIndex(i);
            break;
        }
    }

    void OnDestroy()
    {
        if (wattManager != null)
            wattManager.OnWattChanged -= HandleWattChanged;
    }

    void BindEntry(ShopEntry entry, int index)
    {
        if (entry == null || entry.button == null)
            return;

        if (entry.animator == null)
            entry.animator = entry.button.GetComponent<Animator>();

        entry.hasSelectedParameter = HasBoolParameter(entry.animator, selectedBoolParameter);

        int capturedIndex = index;
        entry.button.onClick.AddListener(() => TryBeginPlacementByIndex(capturedIndex));
    }

    // 배치 대기 중인 건물 데이터와 같은 항목의 버튼만 선택 Bool을 켠다.
    void RefreshSelectionStates()
    {
        if (entries == null)
            return;

        IBuildablePlacementData pending =
            placementController != null ? placementController.PendingBuildData : null;

        for (int i = 0; i < entries.Length; i++)
        {
            ShopEntry entry = entries[i];

            if (entry == null || !entry.hasSelectedParameter || entry.animator == null)
                continue;

            if (!entry.animator.isActiveAndEnabled)
                continue;

            bool selected = pending != null && ReferenceEquals(pending, entry.GetBuildData());
            entry.animator.SetBool(selectedBoolParameter, selected);
        }
    }

    static bool HasBoolParameter(Animator animator, string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName) ||
            animator.runtimeAnimatorController == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.name == parameterName)
                return true;
        }

        return false;
    }

    void HandleWattChanged(float currentWatt)
    {
        RefreshAllEntries(currentWatt);
    }

    void RefreshAllEntries(float? currentWatt = null)
    {
        if (entries == null)
            return;

        float watt = currentWatt ?? (wattManager != null ? wattManager.CurrentWatt : 0f);

        for (int i = 0; i < entries.Length; i++)
            RefreshEntry(entries[i], watt, i);
    }

    void RefreshEntry(ShopEntry entry, float currentWatt, int index)
    {
        IBuildablePlacementData buildData = entry?.GetBuildData();

        if (entry == null)
            return;

        bool hasHotkey = buildData != null && enableHotkeys && buildHotkeys != null &&
            index < maxHotkeySlots && index < buildHotkeys.Length &&
            buildHotkeys[index] != KeyCode.None;

        if (entry.hotkeyLabel != null)
            entry.hotkeyLabel.text = hasHotkey ? buildHotkeys[index].ToString() : string.Empty;

        if (buildData == null)
            return;

        bool canAfford = currentWatt >= buildData.WattCost;

        if (entry.icon != null)
        {
            Sprite iconSprite = buildData.Icon;
            entry.icon.sprite = iconSprite;
            entry.icon.enabled = iconSprite != null;
            entry.icon.color = canAfford ? iconAffordableTint : iconUnaffordableTint;
        }

        if (entry.label != null)
        {
            if (hasHotkey)
            {
                entry.label.text = string.Format(
                    canAfford ? affordableFormat : unaffordableFormat,
                    buildData.DisplayName,
                    buildData.WattCost,
                    buildHotkeys[index].ToString());
            }
            else
            {
                entry.label.text = string.Format(
                    "{0}\n{1} W",
                    buildData.DisplayName,
                    buildData.WattCost);
            }
        }

        if (entry.button != null)
            entry.button.interactable = true;
    }

    void TryBeginPlacementByIndex(int index)
    {
        if (entries == null || index < 0 || index >= entries.Length)
            return;

        ShopEntry entry = entries[index];

        if (entry == null || entry.GetBuildData() == null)
            return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        TryBeginPlacement(entry.GetBuildData());
    }

    static bool IsTextInputFocused()
    {
        if (EventSystem.current == null)
            return false;

        GameObject selected = EventSystem.current.currentSelectedGameObject;

        if (selected == null)
            return false;

        return selected.GetComponent<TMP_InputField>() != null ||
               selected.GetComponent<UnityEngine.UI.InputField>() != null;
    }

    void TryBeginPlacement(IBuildablePlacementData data)
    {
        if (data == null || placementController == null || wattManager == null)
            return;

        if (!wattManager.CanAfford(data.WattCost))
        {
            placementController.PlayFailedPlacementFeedback();
            return;
        }

        placementController.BeginPlacement(data);
    }
}
