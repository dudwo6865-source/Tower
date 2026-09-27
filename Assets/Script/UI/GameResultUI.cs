using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 게임 승리/패배 결과 창입니다.
// - 제목(미션 성공/실패), 게임 통계, 승리 보상, 재침공/함선으로 버튼을 표시합니다.
// - 각 항목의 텍스트·이미지는 인스펙터에서 지정합니다.
// 결과 창 오브젝트(또는 그 부모)에 붙이고, '결과 창'에 실제 창 오브젝트를 연결합니다.
public class GameResultUI : MonoBehaviour
{
    public enum StatType
    {
        [InspectorName("게임 시간")] PlayTime,
        [InspectorName("파괴한 적 스포너")] DestroyedSpawners,
        [InspectorName("처치한 적 유닛")] KilledEnemyUnits,
        [InspectorName("소모한 와트")] SpentWatt,
    }

    [Serializable]
    public class StatRow
    {
        [Label("통계 종류")]
        [Tooltip("이 줄에 표시할 통계 종류입니다.")]
        public StatType type;

        [Label("이름 텍스트")]
        [Tooltip("항목 이름을 표시할 텍스트입니다. (예: 게임 시간)")]
        public TextMeshProUGUI labelText;

        [Label("이름")]
        [Tooltip("항목 이름입니다. 비워두면 이름 텍스트에 입력된 글자를 그대로 둡니다.")]
        public string label;

        [Label("값 텍스트")]
        [Tooltip("통계 값을 표시할 텍스트입니다.")]
        public TextMeshProUGUI valueText;

        [Label("값 형식")]
        [Tooltip("값 표시 형식입니다. {0} 자리에 값이 들어갑니다. (예: {0} 개)")]
        public string valueFormat = "{0}";

        [Label("아이콘 이미지")]
        [Tooltip("항목 아이콘 Image입니다.")]
        public Image iconImage;

        [Label("아이콘")]
        [Tooltip("아이콘 스프라이트입니다. 비워두면 아이콘 이미지에 들어 있는 그림을 그대로 둡니다.")]
        public Sprite icon;
    }

    [Serializable]
    public class RewardSlot
    {
        [Label("칸 오브젝트")]
        [Tooltip("보상 칸 오브젝트입니다. '빈 보상 칸 숨기기'가 켜져 있으면 보상이 없을 때 숨깁니다.")]
        public GameObject slotRoot;

        [Label("아이콘 이미지")]
        [Tooltip("보상 아이콘을 표시할 Image입니다.")]
        public Image iconImage;

        [Label("수량 텍스트")]
        [Tooltip("보상 수량/이름을 표시할 텍스트입니다.")]
        public TextMeshProUGUI amountText;
    }

    [Serializable]
    public class RewardItem
    {
        [Label("아이콘")]
        [Tooltip("보상 아이콘입니다.")]
        public Sprite icon;

        [Label("수량 글자")]
        [Tooltip("보상 수량/이름 텍스트입니다. (예: x100)")]
        public string amount;
    }

    [Header("창")]
    [Label("결과 창")]
    [Tooltip("결과 창 전체 오브젝트입니다. 게임 중엔 숨겨지고 승패가 결정되면 켜집니다.")]
    public GameObject panelRoot;

    [Label("결과 매니저")]
    [Tooltip("비워두면 씬에서 GameResultManager를 자동으로 찾습니다.")]
    public GameResultManager resultManager;

    [Header("제목")]
    [Label("제목 텍스트")]
    [Tooltip("'미션 성공' 제목 텍스트입니다.")]
    public TextMeshProUGUI titleText;

    [Label("승리 제목")]
    public string victoryTitle = "미션 성공";

    [Label("패배 제목")]
    public string defeatTitle = "미션 실패";

    [Label("승리 제목 색")]
    public Color victoryTitleColor = Color.white;

    [Label("패배 제목 색")]
    public Color defeatTitleColor = new Color(1f, 0.45f, 0.45f, 1f);

    [Label("제목 배경 이미지")]
    [Tooltip("제목 배경 Image입니다. (선택)")]
    public Image titleBackgroundImage;

    [Label("승리 제목 배경")]
    [Tooltip("승리 시 제목 배경 스프라이트입니다. 비워두면 바꾸지 않습니다.")]
    public Sprite victoryTitleBackground;

    [Label("패배 제목 배경")]
    [Tooltip("패배 시 제목 배경 스프라이트입니다. 비워두면 바꾸지 않습니다.")]
    public Sprite defeatTitleBackground;

    [Header("게임 통계")]
    [Label("통계 소제목 텍스트")]
    [Tooltip("'게임 통계' 소제목 텍스트입니다. (선택)")]
    public TextMeshProUGUI statsHeaderText;

    [Label("통계 소제목")]
    public string statsHeader = "게임 통계";

    [Label("통계 줄 목록")]
    [Tooltip("통계 줄 목록입니다. 각 줄에 표시할 통계 종류와 텍스트·아이콘을 지정합니다.")]
    public List<StatRow> statRows = new List<StatRow>
    {
        new StatRow { type = StatType.PlayTime, label = "게임 시간" },
        new StatRow { type = StatType.DestroyedSpawners, label = "파괴한 적 스포너" },
        new StatRow { type = StatType.KilledEnemyUnits, label = "처치한 적 유닛" },
        new StatRow { type = StatType.SpentWatt, label = "소모한 와트" },
    };

    [Label("게임 시간 형식")]
    [Tooltip("게임 시간 표시 형식입니다. {0}=분, {1}=초")]
    public string playTimeFormat = "{0:00}:{1:00}";

    [Header("보상")]
    [Label("보상 영역")]
    [Tooltip("'승리 보상' 영역 전체 오브젝트입니다. 패배 시 숨길 수 있습니다.")]
    public GameObject rewardSectionRoot;

    [Label("보상 소제목 텍스트")]
    [Tooltip("'승리 보상' 소제목 텍스트입니다. (선택)")]
    public TextMeshProUGUI rewardHeaderText;

    [Label("승리 보상 소제목")]
    public string victoryRewardHeader = "승리 보상";

    [Label("패배 보상 소제목")]
    public string defeatRewardHeader = "보상";

    [Label("패배 시 보상 표시")]
    [Tooltip("켜면 패배 시에도 보상 영역을 표시합니다. (패배 보상 목록 사용)")]
    public bool showRewardsOnDefeat = false;

    [Label("보상 칸 목록")]
    [Tooltip("화면에 있는 보상 칸 목록입니다.")]
    public List<RewardSlot> rewardSlots = new List<RewardSlot>();

    [Label("승리 보상 목록")]
    [Tooltip("승리 시 표시할 보상 목록입니다. 순서대로 보상 칸에 채워집니다.")]
    public List<RewardItem> victoryRewards = new List<RewardItem>();

    [Label("패배 보상 목록")]
    [Tooltip("패배 시 표시할 보상 목록입니다. ('패배 시 보상 표시'가 켜져 있을 때)")]
    public List<RewardItem> defeatRewards = new List<RewardItem>();

    [Label("빈 보상 칸 숨기기")]
    [Tooltip("켜면 보상이 없는 칸은 숨깁니다. 끄면 빈 칸으로 남겨둡니다.")]
    public bool hideEmptyRewardSlots = false;

    [Header("버튼")]
    [Label("재침공 버튼")]
    [Tooltip("미션을 처음부터 다시 시작하는 버튼입니다.")]
    public Button restartButton;

    [Label("재침공 버튼 텍스트")]
    [Tooltip("재침공 버튼 글자 텍스트입니다. (선택)")]
    public TextMeshProUGUI restartButtonText;

    [Label("재침공 버튼 글자")]
    public string restartButtonLabel = "재침공";

    [Label("함선으로 버튼")]
    [Tooltip("로비 씬으로 이동하는 버튼입니다.")]
    public Button lobbyButton;

    [Label("함선으로 버튼 텍스트")]
    [Tooltip("함선으로 버튼 글자 텍스트입니다. (선택)")]
    public TextMeshProUGUI lobbyButtonText;

    [Label("함선으로 버튼 글자")]
    public string lobbyButtonLabel = "함선으로";

    void Awake()
    {
        // 결과 창이 이 오브젝트 자신이어도 동작하도록, 창을 끄기 전에 이벤트를 연결한다.
        if (resultManager == null)
            resultManager = GameResultManager.Instance;

        if (resultManager == null)
            resultManager = FindObjectOfType<GameResultManager>();

        if (resultManager != null)
            resultManager.OnResultScreenShown += Show;
        else
            Debug.LogError("GameResultUI: 씬에 GameResultManager가 없습니다.");

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
            resultManager.OnResultScreenShown -= Show;

        if (restartButton != null)
            restartButton.onClick.RemoveListener(HandleRestartClicked);

        if (lobbyButton != null)
            lobbyButton.onClick.RemoveListener(HandleLobbyClicked);
    }

    public void Show(GameResultManager.Result result)
    {
        bool victory = result == GameResultManager.Result.Victory;

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
            {
                string format = string.IsNullOrEmpty(row.valueFormat) ? "{0}" : row.valueFormat;
                row.valueText.text = string.Format(format, GetStatValue(row.type));
            }
        }
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
                return Mathf.RoundToInt(resultManager.SpentWatt).ToString();
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
