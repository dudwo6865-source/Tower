using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 현재 장착 중인 유물 슬롯을 상시 보여주는 UI입니다.
// 슬롯 UI 요소들을 미리 만들어 둔 뒤 entries에 하나씩 연결하세요.
// 슬롯 개수는 RelicManager의 Slot Count와 맞추는 것을 권장합니다.
public class RelicSlotPanelUI : MonoBehaviour
{
    [System.Serializable]
    public class SlotEntry
    {
        [Tooltip("유물 아이콘을 표시할 Image입니다.")]
        public Image icon;

        [Tooltip("유물 이름을 표시할 텍스트입니다. 비워두면 표시하지 않습니다.")]
        public TextMeshProUGUI nameLabel;

        [Tooltip("빈 슬롯일 때 대신 보여줄 아이콘입니다. 비워두면 빈 슬롯에서는 아이콘을 끕니다.")]
        public Sprite emptySprite;
    }

    [Header("References")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public RelicManager relicManager;

    [Header("Slots")]
    [Tooltip("장착 슬롯을 보여줄 UI 요소들입니다. RelicManager의 Slot Count와 개수를 맞추세요.")]
    public SlotEntry[] entries;

    void Start()
    {
        if (relicManager == null)
            relicManager = RelicManager.Instance;

        if (relicManager == null)
            relicManager = FindObjectOfType<RelicManager>();

        if (relicManager != null)
            relicManager.OnRelicsChanged += HandleRelicsChanged;

        RefreshSlots();
    }

    void OnDestroy()
    {
        if (relicManager != null)
            relicManager.OnRelicsChanged -= HandleRelicsChanged;
    }

    void HandleRelicsChanged()
    {
        RefreshSlots();
    }

    void RefreshSlots()
    {
        IReadOnlyList<RelicDefinition> owned = relicManager != null ? relicManager.OwnedRelics : null;

        for (int i = 0; i < entries.Length; i++)
        {
            SlotEntry entry = entries[i];
            if (entry == null)
                continue;

            RelicDefinition def = owned != null && i < owned.Count ? owned[i] : null;

            if (entry.nameLabel != null)
                entry.nameLabel.text = def != null ? def.displayName : string.Empty;

            if (entry.icon == null)
                continue;

            if (def != null)
            {
                entry.icon.sprite = def.icon;
                entry.icon.enabled = def.icon != null;
            }
            else if (entry.emptySprite != null)
            {
                entry.icon.sprite = entry.emptySprite;
                entry.icon.enabled = true;
            }
            else
            {
                entry.icon.enabled = false;
            }
        }
    }
}
