using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 게임 승리/패배 결과 창입니다.
// - 제목(미션 성공/실패), 게임 통계, 승리 보상, 재시작/로비 버튼을 표시합니다.
// - 각 항목의 텍스트·이미지는 인스펙터에서 지정합니다.
// 결과 창 패널(Canvas 아래)의 부모 오브젝트에 붙이고, panelRoot 에 실제 창을 연결하세요.
public class GameResultUI : MonoBehaviour
{
    public enum StatType
    {
        PlayTime,           // 게임 시간
        DestroyedSpawners,  // 파괴한 적 스포너
        KilledEnemyUnits,   // 처치한 적 유닛
        SpentWatt,          // 소모한 와트
    }

    [Serializable]
    public class StatRow
    {
        [Tooltip("이 줄에 표시할 통계 종류입니다.")]
        public StatType type;

        [Tooltip("항목 이름을 표시할 텍스트입니다. (예: 게임 시간)")]
        public TextMeshProUGUI labelText;

        [Tooltip("항목 이름입니다. 비워두면 labelText 에 입력된 글자를 그대로 둡니다.")]
        public string label;

        [Tooltip("통계 값을 표시할 텍스트입니다.")]
        public TextMeshProUGUI valueText;

        [Tooltip("값 표시 형식입니다. {0} 자리에 값이 들어갑니다. (예: {0} 개)")]
        public string valueFormat = "{0}";

        [Tooltip("항목 아이콘 Image 입니다.")]
        public Image iconImage;

        [Tooltip("아이콘 스프라이트입니다. 비워두면 iconImage 의 이미지를 그대로 둡니다.")]
        public Sprite icon;
    }

    [Serializable]
    public class RewardSlot
    {
        [Tooltip("보상 칸 오브젝트입니다. 표시할 보상이 없으면 이 칸을 숨깁니다. 비워두면 숨기지 않습니다.")]
        public GameObject slotRoot;

        [Tooltip("보상 아이콘을 표시할 Image 입니다.")]
        public Image iconImage;

        [Tooltip("보상 수량/이름을 표시할 텍스트입니다.")]
        public TextMeshProUGUI amountText;
    }

    [Serializable]
    public class RewardItem
    {
        [Tooltip("보상 아이콘입니다.")]
        public Sprite icon;

        [Tooltip("보상 수량/이름 텍스트입니다. (예: x100)")]
        public string amount;
    }

    [Header("Panel")]
    [Tooltip("결과 창 전체 오브젝트입니다. 게임 중엔 숨겨지고 승패가 결정되면 켜집니다.")]
    public GameObject panelRoot;

    [Tooltip("비워두면 씬에서 GameResultManager 를 자동으로 찾습니다.")]
    public GameResultManager resultManager;

    [Header("Title")]
    [Tooltip("제목 텍스트입니다.")]
    public TextMeshProUGUI titleText;

    public string victoryTitle = "미션 성공";
    public string defeatTitle = "미션 실패";
    public Color victoryTitleColor = Color.white;
    public Color defeatTitleColor = new Color(1f, 0.45f, 0.45f, 1f);

    [Tooltip("제목 배경 Image 입니다. (선택)")]
    public Image titleBackgroundImage;

    [Tooltip("승리 시 제목 배경 스프라이트입니다. 비워두면 바꾸지 않습니다.")]
    public Sprite victoryTitleBackground;

    [Tooltip("패배 시 제목 배경 스프라이트입니다. 비워두면 바꾸지 않습니다.")]
    public Sprite defeatTitleBackground;

    [Header("Game Stats")]
    [Tooltip("'게임 통계' 소제목 텍스트입니다. (선택)")]
    public TextMeshProUGUI statsHeaderText;
    public string statsHeader = "게임 통계";

    [Tooltip("통계 줄 목록입니다. 각 줄에 표시할 통계 종류와 텍스트·아이콘을 지정하세요.")]
    public List<StatRow> statRows = new List<StatRow>
    {
        new StatRow { type = StatType.PlayTime, label = "게임 시간" },
        new StatRow { type = StatType.DestroyedSpawners, label = "파괴한 적 스포너" },
        new StatRow { type = StatType.KilledEnemyUnits, label = "처치한 적 유닛" },
        new StatRow { type = StatType.SpentWatt, label = "소모한 와트" },
    };

    [Tooltip("게임 시간 표시 형식입니다. {0}=분, {1}=초")]
    public string playTimeFormat = "{0:00}:{1:00}";

    [Header("Rewards")]
    [Tooltip("'승리 보상' 영역 전체 오브젝트입니다. 패배 시 숨길 수 있습니다.")]
    public GameObject rewardSectionRoot;

    [Tooltip("'승리 보상' 소제목 텍스트입니다. (선택)")]
    public TextMeshProUGUI rewardHeaderText;
    public string victoryRewardHeader = "승리 보상";
    public string defeatRewardHeader = "보상";

    [Tooltip("켜면 패배 시에도 보상 영역을 표시합니다. (defeatRewards 사용)")]
    public bool showRewardsOnDefeat = false;

    [Tooltip("화면에 있는 보상 칸 목록입니다.")]
    public List<RewardSlot> rewardSlots = new List<RewardSlot>();

    [Tooltip("승리 시 지급(표시)할 보상 목록입니다. 순서대로 보상 칸에 채워집니다.")]
    public List<RewardItem> victoryRewards = new List<RewardItem>();

    [Tooltip("패배 시 표시할 보상 목록입니다. (showRewardsOnDefeat 가 켜져 있을 때)")]
    public List<RewardItem> defeatRewards = new List<RewardItem>();

    [Tooltip("켜면 보상이 없는 칸은 숨깁니다. 끄면 빈 칸(아이콘 없음)으로 남겨둡니다.")]
    public bool hideEmptyRewardSlots = false;

    [Header("Buttons")]
    [Tooltip("미션 재시작(재침공) 버튼입니다.")]
    public Button restartButton;

    [Tooltip("재시작 버튼 글자 텍스트입니다. (선택)")]
    public TextMeshProUGUI restartButtonText;
    public string restartButtonLabel = "재침공";

    [Tooltip("로비 이동(함선으로) 버튼입니다.")]
    public Button lobbyButton;

    [Tooltip("로비 버튼 글자 텍스트입니다. (선택)")]
    public TextMeshProUGUI lobbyButtonText;
    public string lobbyButtonLabel = "함선으로";

    void Awake()
    {
        // panelRoot 가 이 오브젝트 자신이어도 동작하도록, 창을 끄기 전에 이벤트를 연결합니다.
        if (resultManager == null)
            resultManager = GameResultManager.Instance;

        if (resultManager == null)
            resultManager = FindObjectOfType<GameResultManager>();

        if (resultManager != null)
            resultManager.OnGameResult += Show;
        else
            Debug.LogError("GameResultUI: 씬에 GameResultManager 가 없습니다.");

        if (restartButton != null)
            restartButton.onClick.AddListener(HandleRestartClicked);

        if (lobbyButton != null)
            lobbyButton.onClick.AddListener(HandleLobbyClicked);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (resultManager != null)
            resultManager.OnGameResult -= Show;

        if (restartButton != null)
            restartButton.onClick.RemoveListener(HandleRestartClicked);

        if (lobbyButton != null)
            lobbyButton.onClick.RemoveListener(HandleLobbyClicked);
    }

    public void Show(bool victory)
    {
        ApplyTitle(victory);
        ApplyStats();
        ApplyRewards(victory);
        ApplyButtons();

        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    void ApplyTitle(bool victory)
    {
        if (titleText != null)
        {
            titleText.text = victory ? victoryTitle : defeatTitle;
            titleText.color = victory ? victoryTitleColor : defeatTitleColor;
        }

        Sprite background = victory ? victoryTitleBackground : defeatTitleBackground;

        if (titleBackgroundImage != null && background != null)
            titleBackgroundImage.sprite = background;
    }

    void ApplyStats()
    {
        if (statsHeaderText != null && !string.IsNullOrEmpty(statsHeader))
            statsHeaderText.text = statsHeader;

        foreach (StatRow row in statRows)
        {
            if (row == null)
                continue;

            if (row.labelText != null && !string.IsNullOrEmpty(row.label))
                row.labelText.text = row.label;

            if (row.iconImage != null && row.icon != null)
                row.iconImage.sprite = row.icon;

            if (row.valueText != null)
                row.valueText.text = FormatStat(row);
        }
    }

    string FormatStat(StatRow row)
    {
        string value = GetStatValue(row.type);
        string format = string.IsNullOrEmpty(row.valueFormat) ? "{0}" : row.valueFormat;
        return string.Format(format, value);
    }

    string GetStatValue(StatType type)
    {
        if (resultManager == null)
            return "-";

        switch (type)
        {
            case StatType.PlayTime:
                int totalSeconds = Mathf.FloorToInt(resultManager.PlayTime);
                return string.Format(playTimeFormat, totalSeconds / 60, totalSeconds % 60);
            case StatType.DestroyedSpawners:
                return resultManager.DestroyedSpawnerCount.ToString();
            case StatType.KilledEnemyUnits:
                return resultManager.KilledEnemyUnitCount.ToString();
            case StatType.SpentWatt:
                return resultManager.SpentWatt.ToString();
            default:
                return "-";
        }
    }

    void ApplyRewards(bool victory)
    {
        bool showRewards = victory || showRewardsOnDefeat;

        if (rewardSectionRoot != null)
            rewardSectionRoot.SetActive(showRewards);

        if (!showRewards)
            return;

        if (rewardHeaderText != null)
            rewardHeaderText.text = victory ? victoryRewardHeader : defeatRewardHeader;

        List<RewardItem> rewards = victory ? victoryRewards : defeatRewards;

        for (int i = 0; i < rewardSlots.Count; i++)
        {
            RewardSlot slot = rewardSlots[i];

            if (slot == null)
                continue;

            RewardItem reward =
                (rewards != null && i < rewards.Count) ? rewards[i] : null;
            bool hasReward = reward != null && reward.icon != null;

            if (slot.slotRoot != null)
                slot.slotRoot.SetActive(hasReward || !hideEmptyRewardSlots);

            if (slot.iconImage != null)
            {
                slot.iconImage.sprite = hasReward ? reward.icon : null;
                slot.iconImage.enabled = hasReward;
            }

            if (slot.amountText != null)
                slot.amountText.text = reward != null ? reward.amount : "";
        }
    }

    void ApplyButtons()
    {
        if (restartButtonText != null && !string.IsNullOrEmpty(restartButtonLabel))
            restartButtonText.text = restartButtonLabel;

        if (lobbyButtonText != null && !string.IsNullOrEmpty(lobbyButtonLabel))
            lobbyButtonText.text = lobbyButtonLabel;
    }

    void HandleRestartClicked()
    {
        if (resultManager != null)
            resultManager.RestartMission();
    }

    void HandleLobbyClicked()
    {
        if (resultManager != null)
            resultManager.GoToLobby();
    }
}
