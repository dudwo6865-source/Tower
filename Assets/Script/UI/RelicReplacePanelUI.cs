using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 슬롯이 가득 찬 상태에서 새 유물을 고른 뒤, 버릴 유물을 정하는 교체 화면입니다.
// 현재 장착 중인 유물을 버튼으로 보여주고, 그중 하나를 눌러 새 유물과 교체합니다.
// 버릴 유물 버튼 개수는 RelicManager의 Slot Count와 맞추는 것을 권장합니다.
public class RelicReplacePanelUI : MonoBehaviour
{
    [System.Serializable]
    public class DiscardEntry
    {
        public Button button;

        [Tooltip("장착 중인 유물의 이름을 표시할 텍스트입니다.")]
        public TextMeshProUGUI nameLabel;

        [Tooltip("장착 중인 유물의 아이콘입니다. 비워두면 표시하지 않습니다.")]
        public Image icon;
    }

    [Header("References")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public RelicManager relicManager;

    [Tooltip("보이고/숨길 패널 루트입니다. 비워두면 이 오브젝트 자신을 켜고 끕니다.")]
    public GameObject panelRoot;

    [Header("Incoming Relic")]
    [Tooltip("새로 고른(장착 대기 중인) 유물의 이름을 표시합니다.")]
    public TextMeshProUGUI incomingNameLabel;

    [Tooltip("새로 고른 유물의 설명을 표시합니다. 비워두면 표시하지 않습니다.")]
    public TextMeshProUGUI incomingDescriptionLabel;

    [Tooltip("새로 고른 유물의 아이콘입니다. 비워두면 표시하지 않습니다.")]
    public Image incomingIcon;

    [Header("Owned Relics (버릴 대상)")]
    [Tooltip("현재 장착 중인 유물을 보여주고 버릴 대상을 고르는 버튼들입니다.")]
    public DiscardEntry[] entries;

    [Header("Cancel")]
    [Tooltip("교체를 취소하는 버튼입니다. 비워두면 취소 기능 없이 진행됩니다.")]
    public Button cancelButton;

    void Start()
    {
        if (relicManager == null)
            relicManager = RelicManager.Instance;

        if (relicManager == null)
            relicManager = FindObjectOfType<RelicManager>();

        for (int i = 0; i < entries.Length; i++)
            BindEntry(i);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(HandleCancel);

        if (relicManager != null)
            relicManager.OnReplaceRequired += HandleReplaceRequired;

        SetPanelVisible(false);
    }

    void OnDestroy()
    {
        if (relicManager != null)
            relicManager.OnReplaceRequired -= HandleReplaceRequired;
    }

    void BindEntry(int index)
    {
        DiscardEntry entry = entries[index];

        if (entry == null || entry.button == null)
            return;

        int capturedIndex = index;
        entry.button.onClick.AddListener(() => TryDiscard(capturedIndex));
    }

    void HandleReplaceRequired(RelicDefinition incoming)
    {
        if (incomingNameLabel != null)
            incomingNameLabel.text = incoming != null ? incoming.displayName : string.Empty;

        if (incomingDescriptionLabel != null)
            incomingDescriptionLabel.text = incoming != null ? incoming.description : string.Empty;

        if (incomingIcon != null)
        {
            incomingIcon.sprite = incoming != null ? incoming.icon : null;
            incomingIcon.enabled = incoming != null && incoming.icon != null;
        }

        RefreshOwnedEntries();
        SetPanelVisible(true);
    }

    void RefreshOwnedEntries()
    {
        IReadOnlyList<RelicDefinition> owned = relicManager != null ? relicManager.OwnedRelics : null;

        for (int i = 0; i < entries.Length; i++)
        {
            DiscardEntry entry = entries[i];
            if (entry == null)
                continue;

            RelicDefinition def = owned != null && i < owned.Count ? owned[i] : null;
            bool hasRelic = def != null;

            if (entry.button != null)
                entry.button.gameObject.SetActive(hasRelic);

            if (!hasRelic)
                continue;

            if (entry.nameLabel != null)
                entry.nameLabel.text = def.displayName;

            if (entry.icon != null)
            {
                entry.icon.sprite = def.icon;
                entry.icon.enabled = def.icon != null;
            }
        }
    }

    void TryDiscard(int index)
    {
        if (relicManager == null)
            return;

        IReadOnlyList<RelicDefinition> owned = relicManager.OwnedRelics;

        if (owned == null || index < 0 || index >= owned.Count)
            return;

        relicManager.ConfirmReplace(owned[index]);
        SetPanelVisible(false);
    }

    void HandleCancel()
    {
        if (relicManager != null)
            relicManager.CancelReplace();

        SetPanelVisible(false);
    }

    void SetPanelVisible(bool visible)
    {
        GameObject root = panelRoot != null ? panelRoot : gameObject;

        if (root.activeSelf != visible)
            root.SetActive(visible);
    }
}
