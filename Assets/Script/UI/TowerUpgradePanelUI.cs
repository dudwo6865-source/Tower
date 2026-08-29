using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택한 타워의 업그레이드 분기를 보여주는 전용 패널입니다.
/// Building Command Panel과 독립적으로 켜지고 꺼집니다.
/// </summary>
public class TowerUpgradePanelUI : MonoBehaviour
{
    [System.Serializable]
    public class UpgradeSlot
    {
        public Button button;

        [Tooltip("분기 이름과 비용을 표시할 텍스트입니다. 비워두면 건너뜁니다.")]
        public TextMeshProUGUI label;

        [Tooltip("상위 타워 썸네일을 표시할 Image입니다. 비워두면 건너뜁니다.")]
        public Image icon;
    }

    [Header("Panel")]
    [Tooltip("업그레이드 가능한 타워를 선택했을 때만 표시할 패널 루트입니다.")]
    public GameObject panelRoot;

    [Header("Slots")]
    [Tooltip("BuildableTowerData의 Upgrade Options 순서대로 채워지고, 남는 슬롯은 숨겨집니다.")]
    public UpgradeSlot[] slots;

    [Header("Labels")]
    [Tooltip("{0}=상위 타워 이름, {1}=소비 Watt")]
    public string labelFormat = "{0}\n{1} W";

    [Tooltip("Watt가 충분할 때 아이콘 색상입니다.")]
    public Color affordableTint = Color.white;

    [Tooltip("Watt가 부족할 때 아이콘 색상입니다.")]
    public Color unaffordableTint = new Color(1f, 1f, 1f, 0.4f);

    [Header("Hotkeys")]
    public bool enableHotkeys = true;

    [Tooltip("슬롯 순서대로 대응하는 단축키입니다.")]
    public KeyCode[] hotkeys = { KeyCode.U, KeyCode.I, KeyCode.O };

    readonly List<TowerUpgradeOption> options = new List<TowerUpgradeOption>();

    UnitSelectionManager selectionManager;
    CanvasGroup selfHideGroup;
    bool panelVisible;
    bool upgradeReady;

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

        if (!enableHotkeys || !IsPanelVisible() || IsTextInputFocused())
            return;

        HandleHotkeys();
    }

    void HandleSelectionChanged()
    {
        RefreshVisibility();
    }

    public void RefreshVisibility()
    {
        bool visible = BuildingCommandHandler.ShouldShowUpgradePanel(options, out upgradeReady);

        SetPanelVisible(visible);

        if (visible)
            RefreshSlots();
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

    void BindSlots()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            UpgradeSlot slot = slots[i];

            if (slot == null || slot.button == null)
                continue;

            int capturedIndex = i;
            slot.button.onClick.AddListener(() => IssueUpgrade(capturedIndex));
        }
    }

    void RefreshSlots()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            UpgradeSlot slot = slots[i];

            if (slot == null || slot.button == null)
                continue;

            if (i >= options.Count)
            {
                slot.button.gameObject.SetActive(false);
                continue;
            }

            TowerUpgradeOption option = options[i];
            bool canAfford = TowerUpgradeService.CanAfford(option);

            slot.button.gameObject.SetActive(true);

            // Watt가 모자라거나 건설 잠금 중이면 버튼은 보이되 누를 수 없게 둔다.
            slot.button.interactable = canAfford && upgradeReady;

            if (slot.label != null)
                slot.label.text = string.Format(labelFormat, option.DisplayName, option.ResolveCost());

            if (slot.icon != null)
            {
                Sprite iconSprite = option.Icon;
                slot.icon.sprite = iconSprite;
                slot.icon.enabled = iconSprite != null;
                slot.icon.color = canAfford ? affordableTint : unaffordableTint;
            }
        }
    }

    void HandleHotkeys()
    {
        if (slots == null || hotkeys == null)
            return;

        int count = Mathf.Min(slots.Length, hotkeys.Length);

        for (int i = 0; i < count; i++)
        {
            if (!Input.GetKeyDown(hotkeys[i]))
                continue;

            Button button = slots[i]?.button;

            if (button != null && button.gameObject.activeSelf && button.interactable)
                IssueUpgrade(i);

            return;
        }
    }

    void IssueUpgrade(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= options.Count)
            return;

        TowerUpgradeOption option = options[slotIndex];

        if (option == null)
            return;

        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

        if (BuildingCommandHandler.IssueUpgradeToSelection(option.tier))
            RefreshVisibility();
    }

    bool IsPanelVisible()
    {
        return panelVisible;
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
