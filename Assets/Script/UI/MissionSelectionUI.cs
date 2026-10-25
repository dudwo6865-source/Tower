using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 등록한 스테이지마다 미션 항목을 만들고 선택한 스테이지로 출발한다.
[DisallowMultipleComponent]
public class MissionSelectionUI : MonoBehaviour
{
    [Serializable]
    public class Mission
    {
        [Label("스테이지")] public MapConfig stage;
        [Label("미션 번호")]
        [Tooltip("비워두면 등록 순서대로 01, 02처럼 표시합니다.")]
        public string number;
        [Label("표시 이름")]
        [Tooltip("비워두면 스테이지 데이터의 표시 이름을 사용합니다.")]
        public string displayName;
    }

    [Header("미션 등록")]
    [Label("미션 목록")]
    [Tooltip("첫 미션은 기본 해금됩니다. 이후 미션은 바로 이전의 유효한 스테이지를 클리어하면 해금됩니다. 비어 있는 항목은 건너뜁니다.")]
    public List<Mission> missions = new List<Mission>();

    [Header("목록 UI 연결")]
    [Label("항목 원본")]
    [Tooltip("MissionListItemUI가 붙은 프리팹 또는 씬의 견본 항목입니다. 씬 견본은 자동으로 숨깁니다.")]
    public MissionListItemUI itemTemplate;
    [Label("항목 생성 위치")]
    [Tooltip("Scroll View의 Content를 연결합니다. Vertical Layout Group으로 세로 배치합니다.")]
    public RectTransform content;
    [Label("스크롤 뷰")] public ScrollRect scrollRect;
    [Label("빈 목록 안내")] public GameObject emptyListMessage;

    [Header("버튼과 로비 연결")]
    [Label("출발 버튼")] public Button launchButton;
    [Label("닫기 버튼")] public Button closeButton;
    [Label("로비 매니저")]
    [Tooltip("비워두면 현재 로비 매니저를 사용합니다.")]
    public LobbyManager lobbyManager;
    [Label("전투 씬 이름")] public string battleSceneName = "Battle";

    readonly List<MissionListItemUI> items = new List<MissionListItemUI>();
    readonly List<Mission> visibleMissions = new List<Mission>();
    int selectedIndex = -1;
    bool loading;
    Button boundLaunch;
    Button boundClose;
    LobbyManager Lobby => lobbyManager != null ? lobbyManager : LobbyManager.Instance;

    void OnEnable()
    {
        boundLaunch = launchButton;
        boundClose = closeButton;
        if (boundLaunch != null) boundLaunch.onClick.AddListener(Launch);
        if (boundClose != null) boundClose.onClick.AddListener(Close);
        CreditWallet.Changed += RefreshProgress;
        Rebuild();
    }

    void OnDisable()
    {
        if (boundLaunch != null) boundLaunch.onClick.RemoveListener(Launch);
        if (boundClose != null) boundClose.onClick.RemoveListener(Close);
        CreditWallet.Changed -= RefreshProgress;
    }

    void RefreshProgress()
    {
        if (!loading) Rebuild();
    }

    public void Rebuild()
    {
        MapConfig previous = selectedIndex >= 0 && selectedIndex < visibleMissions.Count
            ? visibleMissions[selectedIndex].stage : (Lobby != null ? Lobby.SelectedStage : MapLoader.PendingConfig);
        foreach (MissionListItemUI item in items)
            if (item != null) { item.gameObject.SetActive(false); Destroy(item.gameObject); }
        items.Clear();
        visibleMissions.Clear();
        selectedIndex = -1;
        if (launchButton != null) launchButton.interactable = false;
        if (emptyListMessage != null) emptyListMessage.SetActive(false);

        if (itemTemplate == null || content == null)
        {
            Debug.LogWarning("미션 목록: 항목 원본과 항목 생성 위치를 연결해 주세요.", this);
            return;
        }
        if (transform.IsChildOf(itemTemplate.transform) || content.IsChildOf(itemTemplate.transform))
        {
            Debug.LogError("미션 목록: 팝업이나 Content가 아닌 개별 항목을 원본으로 지정해 주세요.", this);
            return;
        }
        if (itemTemplate.gameObject.scene.IsValid()) itemTemplate.gameObject.SetActive(false);
        if (missions != null)
            for (int i = 0; i < missions.Count; i++)
            {
                Mission mission = missions[i];
                if (mission == null || mission.stage == null) continue;
                int index = visibleMissions.Count;
                MissionListItemUI item = Instantiate(itemTemplate, content, false);
                items.Add(item);
                visibleMissions.Add(mission);
                bool unlocked = IsMissionUnlocked(index);
                item.Bind(string.IsNullOrWhiteSpace(mission.number) ? (i + 1).ToString("00") : mission.number,
                    string.IsNullOrWhiteSpace(mission.displayName) ? mission.stage.displayName : mission.displayName,
                    CreditWallet.GetBestStars(mission.stage.StageId), () => SelectMission(index), unlocked);
                item.gameObject.SetActive(true);
                if (unlocked && mission.stage == previous && selectedIndex < 0) selectedIndex = index;
            }
        if (emptyListMessage != null) emptyListMessage.SetActive(items.Count == 0);
        if (items.Count > 0) SelectMission(selectedIndex >= 0 ? selectedIndex : 0);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    public void SelectMission(int index)
    {
        if (loading || !IsMissionUnlocked(index)) return;
        selectedIndex = index;
        for (int i = 0; i < items.Count; i++) items[i].SetSelected(i == index);
        if (Lobby != null) Lobby.SetSelectedStage(visibleMissions[index].stage);
        else MapLoader.PendingConfig = visibleMissions[index].stage;
        if (launchButton != null) launchButton.interactable = true;
    }

    public void Launch()
    {
        if (loading || selectedIndex < 0 || selectedIndex >= visibleMissions.Count) return;
        if (!IsMissionUnlocked(selectedIndex)) { Rebuild(); return; }
        MapConfig stage = visibleMissions[selectedIndex].stage;
        if (stage == null) return;
        if (string.IsNullOrWhiteSpace(battleSceneName) || !Application.CanStreamedLevelBeLoaded(battleSceneName))
        {
            Debug.LogError("미션 목록: 전투 씬 이름과 Build Settings의 씬 등록을 확인해 주세요.", this);
            return;
        }
        if (Lobby != null) Lobby.SetSelectedStage(stage);
        else MapLoader.PendingConfig = stage;
        loading = true;
        if (launchButton != null) launchButton.interactable = false;
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(battleSceneName);
    }

    public void Close()
    {
        if (!loading) gameObject.SetActive(false);
    }

    bool IsMissionUnlocked(int index)
    {
        if (index < 0 || index >= visibleMissions.Count) return false;
        if (index == 0) return true;
        MapConfig previous = visibleMissions[index - 1].stage;
        return previous != null && CreditWallet.HasCleared(previous.StageId);
    }
}
