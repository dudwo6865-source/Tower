using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 로비 버튼과 표시 데이터를 연결한다. 팝업이 없어도 이벤트 연결부터 준비할 수 있다.
[DisallowMultipleComponent]
public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("버튼")]
    [Label("연구 버튼")]
    public Button researchButton;
    [Label("미션 버튼")]
    public Button missionButton;
    [Label("정비창 버튼")]
    public Button maintenanceButton;

    [Header("표시 텍스트")]
    [Label("크레딧 텍스트")]
    public TMP_Text creditText;
    [Label("크레딧 표시 형식")]
    [Tooltip("{0}에 보유 크레딧을 표시합니다.")]
    public string creditFormat = "{0:N0}";
    [Label("씬 정보 텍스트")]
    public TMP_Text sceneInfoText;
    [Label("로비 표시 이름")]
    public string lobbyTitle = "함교";
    [Label("씬 정보 표시 형식")]
    [Tooltip("{0}=로비 표시 이름, {1}=선택한 스테이지 이름, {2}=스테이지 설명, {3}=현재 씬 이름입니다.")]
    public string sceneInfoFormat = "{0}";

    [Header("스테이지 데이터")]
    [Label("기본 스테이지")]
    public MapConfig defaultStage;
    [Label("이전 선택 유지")]
    [Tooltip("켜면 이전에 선택한 MapLoader.PendingConfig를 기본 스테이지보다 우선합니다.")]
    public bool keepPreviousSelection = true;
    [Label("미선택 표시")]
    public string noStageText = "미션을 선택해 주세요";

    [Header("팝업 연결 (선택)")]
    [Label("연구 팝업")]
    public GameObject researchPopup;
    [Label("미션 팝업")]
    public GameObject missionPopup;
    [Label("정비창 팝업")]
    public GameObject maintenancePopup;
    [Label("시작 시 팝업 닫기")]
    public bool closePopupsOnStart = true;

    [Header("테스트용 저장 초기화")]
    [Label("초기화 단축키 사용")]
    [Tooltip("에디터와 Development Build의 로비에서만 동작합니다. 크레딧과 모든 스테이지 클리어·별·최초 보상 기록을 초기화합니다.")]
    public bool enableProgressResetHotkey = true;
    [Label("초기화 키")] public KeyCode progressResetKey = KeyCode.F9;
    [Label("Ctrl 함께 누르기")] public bool resetRequiresControl = true;
    [Label("Shift 함께 누르기")] public bool resetRequiresShift = true;

    [Header("버튼 요청 이벤트")]
    [Label("연구 열기 요청")]
    [Tooltip("팝업 연결 여부와 관계없이 호출합니다. 나중에 연구 화면을 만드는 동작을 연결할 수 있습니다.")]
    public UnityEvent onResearchRequested = new UnityEvent();
    [Label("미션 열기 요청")]
    public UnityEvent onMissionRequested = new UnityEvent();
    [Label("정비창 열기 요청")]
    public UnityEvent onMaintenanceRequested = new UnityEvent();
    [Label("스테이지 변경")]
    public UnityEvent onStageChanged = new UnityEvent();

    public MapConfig SelectedStage { get; private set; }
    Button boundResearch;
    Button boundMission;
    Button boundMaintenance;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        SelectedStage = keepPreviousSelection && MapLoader.PendingConfig != null
            ? MapLoader.PendingConfig : defaultStage;
        MapLoader.PendingConfig = SelectedStage;
        if (closePopupsOnStart) ClosePopups();
    }

    void OnEnable()
    {
        if (Instance != this) return;
        boundResearch = researchButton;
        boundMission = missionButton;
        boundMaintenance = maintenanceButton;
        if (boundResearch != null) boundResearch.onClick.AddListener(OpenResearch);
        if (boundMission != null) boundMission.onClick.AddListener(OpenMission);
        if (boundMaintenance != null) boundMaintenance.onClick.AddListener(OpenMaintenance);
        CreditWallet.Changed += RefreshCredits;
        RefreshAll();
    }

    void OnDisable()
    {
        if (boundResearch != null) boundResearch.onClick.RemoveListener(OpenResearch);
        if (boundMission != null) boundMission.onClick.RemoveListener(OpenMission);
        if (boundMaintenance != null) boundMaintenance.onClick.RemoveListener(OpenMaintenance);
        CreditWallet.Changed -= RefreshCredits;
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance != this || !enableProgressResetHotkey || progressResetKey == KeyCode.None) return;
        if (!Input.GetKeyDown(progressResetKey)) return;
        if (resetRequiresControl && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;
        if (resetRequiresShift && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;
        if (CreditWallet.TryResetProgress())
            Debug.Log("로비: 크레딧과 모든 스테이지 클리어 기록을 초기화했습니다.", this);
#endif
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RefreshAll()
    {
        RefreshCredits();
        RefreshSceneInfo();
    }

    public void RefreshCredits()
    {
        if (creditText == null) return;
        creditText.text = CreditWallet.TryGetBalance(out long credits)
            ? Format(creditFormat, "{0:N0}", credits) : "크레딧 불러오기 실패";
    }

    public void RefreshSceneInfo()
    {
        if (sceneInfoText == null) return;
        string stageName = SelectedStage != null ? SelectedStage.displayName : noStageText;
        string description = SelectedStage != null ? SelectedStage.description : "";
        sceneInfoText.text = Format(sceneInfoFormat, "{0}", lobbyTitle, stageName,
            description, SceneManager.GetActiveScene().name);
    }

    public void SetSelectedStage(MapConfig stage)
    {
        SelectedStage = stage;
        MapLoader.PendingConfig = stage;
        RefreshSceneInfo();
        onStageChanged?.Invoke();
    }

    public void OpenResearch()
    {
        OpenPopup(researchPopup);
        onResearchRequested?.Invoke();
    }

    public void OpenMission()
    {
        OpenPopup(missionPopup);
        onMissionRequested?.Invoke();
    }

    public void OpenMaintenance()
    {
        OpenPopup(maintenancePopup);
        onMaintenanceRequested?.Invoke();
    }

    void OpenPopup(GameObject target)
    {
        // 미구현 메뉴를 눌러도 열려 있던 창을 닫지 않는다.
        if (target == null) return;
        SetPopupVisible(researchPopup, target == researchPopup);
        SetPopupVisible(missionPopup, target == missionPopup);
        SetPopupVisible(maintenancePopup, target == maintenancePopup);
    }

    public void ClosePopups()
    {
        SetPopupVisible(researchPopup, false);
        SetPopupVisible(missionPopup, false);
        SetPopupVisible(maintenancePopup, false);
    }

    void SetPopupVisible(GameObject panel, bool visible)
    {
        if (panel == null || panel == gameObject || transform.IsChildOf(panel.transform)) return;
        panel.SetActive(visible);
    }

    static string Format(string format, string fallback, params object[] values)
    {
        try { return string.Format(string.IsNullOrEmpty(format) ? fallback : format, values); }
        catch (System.FormatException) { return string.Format(fallback, values); }
    }
}
