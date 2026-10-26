using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 항상 켜져 있는 오브젝트에 붙이고, 실제 메뉴 창은 별도 자식으로 연결한다.
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class EscapeMenuUI : MonoBehaviour
{
    [Header("창")]
    [Label("메뉴 전체 루트")] public GameObject menuRoot;
    [Label("기본 메뉴 창")] public GameObject mainPanel;
    [Label("설정 창")] public GameObject settingsPanel;
    [Label("사운드 창")] public GameObject soundPanel;
    [Label("그래픽 창")] public GameObject graphicsPanel;
    [Label("게임플레이 창")] public GameObject gameplayPanel;
    [Label("스테이지 저장 창")] public GameObject savePanel;
    [Label("스테이지 불러오기 창")] public GameObject loadPanel;
    [Label("확인 창")] public GameObject confirmationPanel;
    [Label("확인 문구")] public TMP_Text confirmationText;

    [Header("버튼")]
    [Label("게임으로 돌아가기")] public Button resumeButton;
    [Label("설정")] public Button settingsButton;
    [Label("사운드")] public Button soundButton;
    [Label("그래픽")] public Button graphicsButton;
    [Label("게임플레이")] public Button gameplayButton;
    [Label("스테이지 진행사항 저장")] public Button saveButton;
    [Label("스테이지 진행사항 불러오기")] public Button loadButton;
    [Label("로비로 이동")] public Button lobbyButton;
    [Label("게임 종료")] public Button quitButton;
    [Label("뒤로 버튼 목록")] public Button[] backButtons;
    [Label("확인 버튼")] public Button confirmButton;
    [Label("취소 버튼")] public Button cancelButton;

    [Header("이동 및 입력")]
    [Label("로비 씬 이름")] public string lobbySceneName = "Lobby";
    [Label("메뉴 중 끌 입력 컴포넌트")]
    [Tooltip("추가 단축키나 카메라 입력 컴포넌트를 연결합니다. 닫을 때 원래 활성 상태로 복원합니다.")]
    public Behaviour[] inputComponents;

    [Header("저장 시스템 연결")]
    [Label("저장 요청")]
    [Tooltip("저장 창이 없을 때 호출합니다. 실제 전투 저장 기능을 가진 컴포넌트의 함수를 연결합니다.")]
    public UnityEvent onSaveRequested = new UnityEvent();
    [Label("불러오기 요청")]
    [Tooltip("불러오기 창이 없을 때 호출합니다. 실제 전투 복원 기능을 가진 컴포넌트의 함수를 연결합니다.")]
    public UnityEvent onLoadRequested = new UnityEvent();

    public bool IsOpen { get; private set; }
    readonly Stack<GameObject> history = new Stack<GameObject>();
    readonly Dictionary<Behaviour, bool> inputStates = new Dictionary<Behaviour, bool>();
    readonly List<KeyValuePair<Button, UnityAction>> bindings = new List<KeyValuePair<Button, UnityAction>>();
    GameObject currentPanel;
    UnityAction pendingAction;
    float previousTimeScale;

    void Awake()
    {
        if (menuRoot == null || menuRoot == gameObject || transform.IsChildOf(menuRoot.transform))
        {
            Debug.LogError("ESC 메뉴: 메뉴 전체 루트를 별도 자식 오브젝트로 연결해야 합니다.", this);
            enabled = false;
            return;
        }
        HidePanels();
        menuRoot.SetActive(false);
        Bind(resumeButton, Resume);
        Bind(settingsButton, () => OpenPage(settingsPanel));
        Bind(soundButton, () => OpenPage(soundPanel));
        Bind(graphicsButton, () => OpenPage(graphicsPanel));
        Bind(gameplayButton, () => OpenPage(gameplayPanel));
        Bind(saveButton, RequestSave);
        Bind(loadButton, RequestLoad);
        Bind(lobbyButton, RequestLobby);
        Bind(quitButton, RequestQuit);
        Bind(confirmButton, Confirm);
        Bind(cancelButton, Back);
        if (backButtons != null)
            foreach (Button button in backButtons) Bind(button, Back);
        if (saveButton != null) saveButton.interactable = savePanel != null || onSaveRequested.GetPersistentEventCount() > 0;
        if (loadButton != null) loadButton.interactable = loadPanel != null || onLoadRequested.GetPersistentEventCount() > 0;
    }

    void Bind(Button button, UnityAction action)
    {
        if (button == null) return;
        button.onClick.AddListener(action);
        bindings.Add(new KeyValuePair<Button, UnityAction>(button, action));
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (IsOpen) { Back(); return; }
        // 기존 입력보다 먼저 검사해 취소와 메뉴 열림이 같은 프레임에 발생하지 않게 한다.
        if (TowerPlacementController.Instance != null && TowerPlacementController.Instance.IsPlacing) return;
        if (UnitCommandController.Instance != null && UnitCommandController.Instance.HasPendingMode) return;
        Open();
    }

    public void Open()
    {
        if (!enabled || IsOpen || Time.timeScale <= 0f ||
            (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver)) return;
        if (UnitCommandController.Instance != null) UnitCommandController.Instance.CancelMode();
        if (TowerPlacementController.Instance != null) TowerPlacementController.Instance.CancelPlacement();
        previousTimeScale = Time.timeScale;
        IsOpen = true;
        Time.timeScale = 0f;
        Suspend(UnitSelectionManager.Instance);
        Suspend(UnitCommandController.Instance);
        Suspend(TowerPlacementController.Instance);
        foreach (RTSCameraPivotController cameraController in FindObjectsOfType<RTSCameraPivotController>()) Suspend(cameraController);
        if (inputComponents != null)
            foreach (Behaviour component in inputComponents) Suspend(component);
        history.Clear();
        HidePanels();
        menuRoot.SetActive(true);
        currentPanel = mainPanel;
        if (currentPanel != null) currentPanel.SetActive(true);
    }

    void Suspend(Behaviour component)
    {
        if (component == null || component == this || inputStates.ContainsKey(component)) return;
        inputStates.Add(component, component.enabled);
        component.enabled = false;
    }

    public void Resume()
    {
        if (!IsOpen) return;
        IsOpen = false;
        pendingAction = null;
        history.Clear();
        HidePanels();
        if (menuRoot != null) menuRoot.SetActive(false);
        foreach (var entry in inputStates)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        inputStates.Clear();
        if (GameResultManager.Instance == null || !GameResultManager.Instance.IsGameOver)
            Time.timeScale = previousTimeScale;
    }

    public void OpenPage(GameObject panel)
    {
        if (!IsOpen || panel == null || panel == currentPanel) return;
        history.Push(currentPanel);
        if (currentPanel != null) currentPanel.SetActive(false);
        currentPanel = panel;
        currentPanel.SetActive(true);
    }

    public void Back()
    {
        if (!IsOpen) return;
        pendingAction = null;
        if (history.Count == 0) { Resume(); return; }
        if (currentPanel != null) currentPanel.SetActive(false);
        currentPanel = history.Pop();
        if (currentPanel != null) currentPanel.SetActive(true);
    }

    public void RequestSave()
    {
        if (!IsOpen) return;
        if (savePanel != null) OpenPage(savePanel);
        else onSaveRequested.Invoke();
    }

    public void RequestLoad()
    {
        if (!IsOpen) return;
        if (loadPanel != null) OpenPage(loadPanel);
        else Ask("저장하지 않은 진행사항은 사라집니다. 불러올까요?", () => onLoadRequested.Invoke());
    }

    public void RequestLobby() => Ask("저장하지 않은 진행사항은 사라집니다. 로비로 이동할까요?", GoToLobby);
    public void RequestQuit() => Ask("저장하지 않은 진행사항은 사라집니다. 게임을 종료할까요?", Quit);

    void Ask(string message, UnityAction action)
    {
        if (!IsOpen) return;
        if (confirmationPanel == null || confirmButton == null || confirmationText == null)
        {
            Debug.LogWarning("ESC 메뉴: 확인 창, 확인 문구, 확인 버튼을 연결해야 합니다.", this);
            return;
        }
        confirmationText.text = message;
        pendingAction = action;
        OpenPage(confirmationPanel);
    }

    public void Confirm()
    {
        if (!IsOpen || pendingAction == null) return;
        UnityAction action = pendingAction;
        Back();
        action.Invoke();
    }

    void GoToLobby()
    {
        if (string.IsNullOrWhiteSpace(lobbySceneName) || !Application.CanStreamedLevelBeLoaded(lobbySceneName))
        {
            Debug.LogError("ESC 메뉴: 로비 씬 이름과 빌드 설정의 씬 목록을 확인해 주세요.", this);
            return;
        }
        Resume();
        Time.timeScale = 1f;
        SceneManager.LoadScene(lobbySceneName);
    }

    void Quit()
    {
        Resume();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void HidePanels()
    {
        foreach (GameObject panel in new[] { mainPanel, settingsPanel, soundPanel, graphicsPanel,
            gameplayPanel, savePanel, loadPanel, confirmationPanel })
            if (panel != null && panel != gameObject && !transform.IsChildOf(panel.transform)) panel.SetActive(false);
    }

    void OnDisable() => Resume();

    void OnDestroy()
    {
        foreach (var binding in bindings)
            if (binding.Key != null) binding.Key.onClick.RemoveListener(binding.Value);
    }
}
