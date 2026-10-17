using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 유물 시스템 UI를 한 스크립트에서 전부 연결합니다.
// - 후보 선택 화면: 스포너 파괴 시 후보 3종 중 하나를 고릅니다.
// - 교체 화면: 슬롯이 가득 찬 상태에서 고르면, 버릴 유물을 정합니다.
// - 장착 슬롯: 현재 장착 중인 유물을 상시 보여줍니다.
// UpgradeShopUI/BuildShopUI와 같은 방식으로, 직접 만든 버튼/텍스트/아이콘을
// 각 entries 배열에 하나씩 끌어다 놓으면 됩니다. 비주얼/레이아웃은 강제하지 않습니다.
public class RelicPanelUI : MonoBehaviour
{
    [System.Serializable]
    public class CandidateEntry
    {
        [Label("버튼")]
        public Button button;

        [Label("이름 텍스트")]
        [Tooltip("유물 이름을 표시할 텍스트입니다.")]
        public TextMeshProUGUI nameLabel;

        [Label("설명 텍스트")]
        [Tooltip("유물 효과 설명을 표시할 텍스트입니다. 비워두면 표시하지 않습니다.")]
        public TextMeshProUGUI descriptionLabel;

        [Label("아이콘")]
        [Tooltip("유물 아이콘입니다. 비워두면 표시하지 않습니다.")]
        public Image icon;
    }

    [System.Serializable]
    public class DiscardEntry
    {
        [Label("버튼")]
        public Button button;

        [Label("이름 텍스트")]
        [Tooltip("장착 중인 유물의 이름을 표시할 텍스트입니다.")]
        public TextMeshProUGUI nameLabel;

        [Label("아이콘")]
        [Tooltip("장착 중인 유물의 아이콘입니다. 비워두면 표시하지 않습니다.")]
        public Image icon;
    }

    [System.Serializable]
    public class SlotEntry
    {
        [Label("아이콘")]
        [Tooltip("유물 아이콘을 표시할 Image입니다.")]
        public Image icon;

        [Label("이름 텍스트")]
        [Tooltip("유물 이름을 표시할 텍스트입니다. 비워두면 표시하지 않습니다.")]
        public TextMeshProUGUI nameLabel;

        [Label("빈 슬롯 아이콘")]
        [Tooltip("빈 슬롯일 때 대신 보여줄 아이콘입니다. 비워두면 빈 슬롯에서는 아이콘을 끕니다.")]
        public Sprite emptySprite;
    }

    [Header("공통")]
    [Label("유물 매니저")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public RelicManager relicManager;

    [Label("선택 중 게임 정지")]
    [Tooltip("켜면 후보 선택·교체 화면이 떠 있는 동안 게임 시간을 멈춥니다(Time.timeScale = 0). BGM과 UI 효과음은 계속 재생됩니다. " +
        "패널에 Animator 연출이 있으면 Animator의 Update Mode를 Unscaled Time으로 바꿔야 정지 중에도 움직입니다.")]
    public bool pauseWhileChoosing = true;

    [Header("후보 선택 화면")]
    [Label("후보 화면 루트")]
    [Tooltip("보이고/숨길 패널 루트입니다. 비워두면 이 화면을 켜고 끄지 않습니다.")]
    public GameObject candidatePanelRoot;

    [Label("후보 항목")]
    [Tooltip("후보 버튼들입니다. RelicManager의 Candidate Count와 개수를 맞추세요.")]
    public CandidateEntry[] candidateEntries;

    [Header("교체 화면")]
    [Label("교체 화면 루트")]
    [Tooltip("보이고/숨길 패널 루트입니다. 비워두면 이 화면을 켜고 끄지 않습니다.")]
    public GameObject replacePanelRoot;

    [Label("새 유물 이름 텍스트")]
    [Tooltip("새로 고른(장착 대기 중인) 유물의 이름을 표시합니다.")]
    public TextMeshProUGUI incomingNameLabel;

    [Label("새 유물 설명 텍스트")]
    [Tooltip("새로 고른 유물의 설명을 표시합니다. 비워두면 표시하지 않습니다.")]
    public TextMeshProUGUI incomingDescriptionLabel;

    [Label("새 유물 아이콘")]
    [Tooltip("새로 고른 유물의 아이콘입니다. 비워두면 표시하지 않습니다.")]
    public Image incomingIcon;

    [Label("버릴 유물 항목")]
    [Tooltip("현재 장착 중인 유물을 보여주고, 그중 버릴 대상을 고르는 버튼들입니다. " +
        "RelicManager의 Slot Count와 개수를 맞추세요.")]
    public DiscardEntry[] discardEntries;

    [Label("교체 취소 버튼")]
    [Tooltip("교체를 취소하는 버튼입니다. 비워두면 취소 기능 없이 진행됩니다.")]
    public Button cancelReplaceButton;

    [Header("장착 슬롯")]
    [Label("슬롯 항목")]
    [Tooltip("장착 슬롯을 보여줄 UI 요소들입니다. RelicManager의 Slot Count와 개수를 맞추세요.")]
    public SlotEntry[] slotEntries;

    List<RelicDefinition> currentCandidates;

    // 화면 루트를 비워둔 경우에도 정지를 판단할 수 있도록 열림 상태를 따로 기록한다.
    bool candidateOpen;
    bool replaceOpen;
    bool pausedByPanel;
    float timeScaleBeforePause = 1f;

    void Start()
    {
        if (relicManager == null)
            relicManager = RelicManager.Instance;

        if (relicManager == null)
            relicManager = FindObjectOfType<RelicManager>();

        for (int i = 0; i < candidateEntries.Length; i++)
            BindCandidateEntry(i);

        for (int i = 0; i < discardEntries.Length; i++)
            BindDiscardEntry(i);

        if (cancelReplaceButton != null)
            cancelReplaceButton.onClick.AddListener(HandleCancelReplace);

        if (relicManager != null)
        {
            relicManager.OnCandidatesOffered += HandleCandidatesOffered;
            relicManager.OnReplaceRequired += HandleReplaceRequired;
            relicManager.OnRelicsChanged += HandleRelicsChanged;
        }

        SetCandidateOpen(false);
        SetReplaceOpen(false);
        RefreshSlots();
    }

    void OnDestroy()
    {
        // 화면이 열린 채 씬을 떠나도 다음 씬이 멈춘 채로 시작하지 않게 되돌린다.
        ResumeIfPaused();

        if (relicManager == null)
            return;

        relicManager.OnCandidatesOffered -= HandleCandidatesOffered;
        relicManager.OnReplaceRequired -= HandleReplaceRequired;
        relicManager.OnRelicsChanged -= HandleRelicsChanged;
    }

    // ---- 후보 선택 화면 ----

    void BindCandidateEntry(int index)
    {
        CandidateEntry entry = candidateEntries[index];

        if (entry == null || entry.button == null)
            return;

        int capturedIndex = index;
        entry.button.onClick.AddListener(() => TryChooseCandidate(capturedIndex));
    }

    void HandleCandidatesOffered(IReadOnlyList<RelicDefinition> candidates)
    {
        currentCandidates = new List<RelicDefinition>(candidates);
        RefreshCandidateEntries();
        SetCandidateOpen(true);
    }

    void RefreshCandidateEntries()
    {
        for (int i = 0; i < candidateEntries.Length; i++)
        {
            CandidateEntry entry = candidateEntries[i];
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

    void Update()
    {
        if (GameResultManager.Instance == null || !GameResultManager.Instance.IsGameOver)
            return;
        SetCandidateOpen(false);
        SetReplaceOpen(false);
    }

    void TryChooseCandidate(int index)
    {
        if (relicManager == null || currentCandidates == null ||
            index < 0 || index >= currentCandidates.Count)
            return;

        relicManager.ChooseRelic(currentCandidates[index]);

        // 교체가 필요하면 RelicManager가 OnReplaceRequired를 곧바로 발생시키므로,
        // 후보 화면은 선택 즉시 닫아 교체 화면과 겹치지 않게 한다.
        SetCandidateOpen(false);
    }

    // ---- 교체 화면 ----

    void BindDiscardEntry(int index)
    {
        DiscardEntry entry = discardEntries[index];

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

        RefreshDiscardEntries();
        SetReplaceOpen(true);
    }

    void RefreshDiscardEntries()
    {
        IReadOnlyList<RelicDefinition> owned = relicManager != null ? relicManager.OwnedRelics : null;

        for (int i = 0; i < discardEntries.Length; i++)
        {
            DiscardEntry entry = discardEntries[i];
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
        SetReplaceOpen(false);
    }

    void HandleCancelReplace()
    {
        if (relicManager != null)
            relicManager.CancelReplace();

        SetReplaceOpen(false);
    }

    // ---- 장착 슬롯 ----

    void HandleRelicsChanged()
    {
        RefreshSlots();
    }

    void RefreshSlots()
    {
        IReadOnlyList<RelicDefinition> owned = relicManager != null ? relicManager.OwnedRelics : null;

        for (int i = 0; i < slotEntries.Length; i++)
        {
            SlotEntry entry = slotEntries[i];
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

    // ---- 공통 ----

    void SetCandidateOpen(bool open)
    {
        candidateOpen = open;
        SetPanelVisible(candidatePanelRoot, open);
        RefreshPause();
    }

    void SetReplaceOpen(bool open)
    {
        replaceOpen = open;
        SetPanelVisible(replacePanelRoot, open);
        RefreshPause();
    }

    // 후보·교체 화면 중 하나라도 열려 있으면 게임 시간을 멈추고, 모두 닫히면 원래 속도로 되돌린다.
    // 후보를 고르는 순간 교체 화면이 먼저 열린 뒤 후보 화면이 닫히므로, 그 사이에 정지가 풀리지 않는다.
    void RefreshPause()
    {
        bool shouldPause = pauseWhileChoosing && (candidateOpen || replaceOpen);

        if (shouldPause && !pausedByPanel)
        {
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            pausedByPanel = true;
        }
        else if (!shouldPause)
        {
            ResumeIfPaused();
        }
    }

    // 게임이 끝나 화면을 닫는 경우에도 되돌린다. 결과 창은 표시 지연 뒤에 직접 다시 멈춘다.
    void ResumeIfPaused()
    {
        if (!pausedByPanel)
            return;

        pausedByPanel = false;
        Time.timeScale = timeScaleBeforePause;
    }

    static void SetPanelVisible(GameObject panelRoot, bool visible)
    {
        if (panelRoot == null || panelRoot.activeSelf == visible)
            return;

        panelRoot.SetActive(visible);
    }
}
