using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildShopUI : MonoBehaviour
{
    [System.Serializable]
    public class ShopEntry
    {
        public BuildableTowerData data;
        public Button button;
        public TextMeshProUGUI label;
    }

    [Header("References")]
    public ShopEntry[] entries;

    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public WattManager wattManager;

    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public TowerPlacementController placementController;

    [Header("Labels")]
    public string affordableFormat = "[{2}] {0}\n{1} W";
    public string unaffordableFormat = "[{2}] {0}\n{1} W";

    [Header("Hotkeys")]
    [Tooltip("등록 순서대로 1~4 키로 구매·배치를 시작합니다.")]
    public bool enableHotkeys = true;

    [Tooltip("단축키로 사용할 최대 슬롯 수입니다.")]
    public int maxHotkeySlots = 4;

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
        if (!enableHotkeys || entries == null || entries.Length == 0)
            return;

        if (IsTextInputFocused())
            return;

        int hotkeyCount = Mathf.Min(maxHotkeySlots, entries.Length, 9);

        for (int i = 0; i < hotkeyCount; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i))
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
        if (entry == null || entry.data == null || entry.button == null)
            return;

        entry.button.onClick.AddListener(() => TryBeginPlacementByIndex(index));
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
        if (entry == null || entry.data == null)
            return;

        bool canAfford = currentWatt >= entry.data.wattCost;

        if (entry.label != null)
        {
            if (index < maxHotkeySlots)
            {
                entry.label.text = string.Format(
                    canAfford ? affordableFormat : unaffordableFormat,
                    entry.data.displayName,
                    entry.data.wattCost,
                    index + 1);
            }
            else
            {
                entry.label.text = string.Format(
                    "{0}\n{1} W",
                    entry.data.displayName,
                    entry.data.wattCost);
            }
        }

        if (entry.button != null)
            entry.button.interactable = canAfford;
    }

    void TryBeginPlacementByIndex(int index)
    {
        if (entries == null || index < 0 || index >= entries.Length)
            return;

        ShopEntry entry = entries[index];

        if (entry == null || entry.data == null)
            return;

        TryBeginPlacement(entry.data);
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

    void TryBeginPlacement(BuildableTowerData data)
    {
        if (data == null || placementController == null || wattManager == null)
            return;

        if (!wattManager.CanAfford(data.wattCost))
            return;

        placementController.BeginPlacement(data);
    }
}
