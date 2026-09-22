using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스포너 파괴 시 나오는 유물 후보 3택1 화면입니다.
// UpgradeShopUI/BuildShopUI와 같은 방식으로, 미리 만들어 둔 버튼들을 entries에 하나씩
// 연결하면 됩니다. 버튼 개수는 RelicManager의 Candidate Count와 맞추는 것을 권장합니다.
public class RelicCandidatePanelUI : MonoBehaviour
{
    [System.Serializable]
    public class CandidateEntry
    {
        public Button button;

        [Tooltip("유물 이름을 표시할 텍스트입니다.")]
        public TextMeshProUGUI nameLabel;

        [Tooltip("유물 효과 설명을 표시할 텍스트입니다. 비워두면 표시하지 않습니다.")]
        public TextMeshProUGUI descriptionLabel;

        [Tooltip("유물 아이콘입니다. 비워두면 표시하지 않습니다.")]
        public Image icon;
    }

    [Header("References")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public RelicManager relicManager;

    [Tooltip("보이고/숨길 패널 루트입니다. 이 스크립트가 붙은 오브젝트가 아닌 별도 자식이어야 합니다.\n" +
        "비워두면 이 오브젝트 자신을 켜고 끕니다.")]
    public GameObject panelRoot;

    [Header("Candidates")]
    [Tooltip("후보 버튼들입니다. RelicManager의 Candidate Count와 개수를 맞추세요.")]
    public CandidateEntry[] entries;

    List<RelicDefinition> currentCandidates;

    void Start()
    {
        if (relicManager == null)
            relicManager = RelicManager.Instance;

        if (relicManager == null)
            relicManager = FindObjectOfType<RelicManager>();

        for (int i = 0; i < entries.Length; i++)
            BindEntry(i);

        if (relicManager != null)
            relicManager.OnCandidatesOffered += HandleCandidatesOffered;

        SetPanelVisible(false);
    }

    void OnDestroy()
    {
        if (relicManager != null)
            relicManager.OnCandidatesOffered -= HandleCandidatesOffered;
    }

    void BindEntry(int index)
    {
        CandidateEntry entry = entries[index];

        if (entry == null || entry.button == null)
            return;

        int capturedIndex = index;
        entry.button.onClick.AddListener(() => TryChoose(capturedIndex));
    }

    void HandleCandidatesOffered(IReadOnlyList<RelicDefinition> candidates)
    {
        currentCandidates = new List<RelicDefinition>(candidates);
        RefreshEntries();
        SetPanelVisible(true);
    }

    void RefreshEntries()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            CandidateEntry entry = entries[i];
            if (entry == null)
                continue;

            RelicDefinition def = currentCandidates != null && i < currentCandidates.Count
                ? currentCandidates[i]
                : null;

            bool hasCandidate = def != null;

            if (entry.button != null)
                entry.button.gameObject.SetActive(hasCandidate);

            if (!hasCandidate)
                continue;

            if (entry.nameLabel != null)
                entry.nameLabel.text = def.displayName;

            if (entry.descriptionLabel != null)
                entry.descriptionLabel.text = def.description;

            if (entry.icon != null)
            {
                entry.icon.sprite = def.icon;
                entry.icon.enabled = def.icon != null;
            }
        }
    }

    void TryChoose(int index)
    {
        if (relicManager == null || currentCandidates == null ||
            index < 0 || index >= currentCandidates.Count)
            return;

        relicManager.ChooseRelic(currentCandidates[index]);

        // 교체가 필요하면 RelicManager가 OnReplaceRequired를 곧바로 발생시키므로,
        // 이 후보 패널은 선택 즉시 닫아 교체 패널과 겹치지 않게 한다.
        SetPanelVisible(false);
    }

    void SetPanelVisible(bool visible)
    {
        GameObject root = panelRoot != null ? panelRoot : gameObject;

        if (root.activeSelf != visible)
            root.SetActive(visible);
    }
}
