using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 씬에서 연결한 UI만 사용한다. 맵 로딩보다 먼저 암전과 입력 잠금을 시작한다.
[DefaultExecutionOrder(-2000)]
[DisallowMultipleComponent]
public class StageIntroController : MonoBehaviour
{
    public static StageIntroController Instance { get; private set; }
    public static bool IsPlaying => Instance != null && Instance.running;
    public bool HasRevealed { get; private set; }

    [Header("연출 UI")]
    [Label("암전 패널")]
    [Tooltip("화면 전체를 덮는 검은 Image에 붙인 CanvasGroup입니다. 씬에서 직접 만들고 활성 상태로 연결합니다.")]
    public CanvasGroup blackout;
    // 기존 씬에 연결했던 안내 텍스트는 숨기고, 인스펙터에서는 더 이상 노출하지 않는다.
    [SerializeField, HideInInspector] TMP_Text stageNameText;
    [SerializeField, HideInInspector] TMP_Text objectiveText;
    [SerializeField, HideInInspector] TMP_Text announcementText;

    [Header("연출 시간")]
    [Label("화면 공개 시간(초)")]
    [Min(0f)] public float revealDuration = 1f;
    [Label("로딩 대기 제한(초)")]
    [Tooltip("초기 배치에 실패해도 암전에 갇히지 않도록 제한 시간이 지나면 연출을 취소합니다. 실제 시간 기준입니다.")]
    [Min(1f)] public float loadingTimeout = 30f;

    StageIntroTiming timing;
    bool running;
    float restoreTimeScale;
    float loadingStarted;
    int startFrame;
    StageBuildingSpawnPoint[] initialMarkers;
    readonly List<BaseInputModule> lockedInputModules = new List<BaseInputModule>();

    void Awake()
    {
        HideLegacyText();
        if (!enabled) { HideVisuals(); return; }
        if (Instance != null && Instance != this) { Destroy(this); return; }
        if (blackout == null || !blackout.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("StageIntroController: 활성 상태의 암전 패널을 연결하세요. 시작 연출을 생략합니다.", this);
            HideVisuals();
            return;
        }
        Instance = this;
        running = true;
        restoreTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        blackout.alpha = 1f;
        blackout.blocksRaycasts = true;
        timing = new StageIntroTiming(revealDuration);
        loadingStarted = Time.realtimeSinceStartup;
        LockUIInput();
    }

    void Start()
    {
        startFrame = Time.frameCount;
        loadingStarted = Time.realtimeSinceStartup;
    }

    void Update()
    {
        if (!running) return;
        // 카메라 초기화 과정에서 나중에 만들어진 입력 모듈도 잠근다.
        LockUIInput();
        if (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver)
        {
            Finish();
            return;
        }
        Time.timeScale = 0f;
        if (Time.frameCount <= startFrame) return;
        if (timing.State == StageIntroTiming.Phase.Loading &&
            Time.realtimeSinceStartup - loadingStarted >= Mathf.Max(1f, loadingTimeout))
        {
            Debug.LogWarning("StageIntroController: 초기 배치 대기 시간이 초과되어 연출을 취소합니다. 맵·본부·보스 설정을 확인하세요.", this);
            Finish();
            return;
        }

        StageIntroTiming.Phase previous = timing.State;
        timing.Tick(Time.unscaledDeltaTime, IsReady());
        if (previous == StageIntroTiming.Phase.Loading && timing.State == StageIntroTiming.Phase.Revealing)
        {
            FogOfWarManager.Instance?.RefreshVisionNow();
            RTSCameraPivotController cameraController = FindFirstObjectByType<RTSCameraPivotController>();
            if (cameraController != null) cameraController.FocusStageStart();
        }
        if (previous == StageIntroTiming.Phase.Revealing && timing.State == StageIntroTiming.Phase.Finished)
        {
            // 문구 없이 화면 공개 완료와 동시에 로어 대기를 풀고 조작을 시작한다.
            HasRevealed = true;
            Finish();
        }
        else if (timing.State == StageIntroTiming.Phase.Revealing)
            blackout.alpha = 1f - timing.Progress;
    }

    bool IsReady()
    {
        if (timing.State != StageIntroTiming.Phase.Loading) return true;
        MapLoader loader = MapLoader.Instance;
        if (loader != null && !loader.IsMapReady) return false;
        if (initialMarkers == null)
            initialMarkers = FindObjectsByType<StageBuildingSpawnPoint>(FindObjectsSortMode.None);
        if (initialMarkers != null)
            foreach (StageBuildingSpawnPoint marker in initialMarkers)
                if (marker != null && marker.isActiveAndEnabled && !marker.HasFinishedSpawn) return false;
        InitialEnemyPlacer placer = InitialEnemyPlacer.Instance;
        if (placer != null && placer.isActiveAndEnabled && !placer.HasCompletedPlacement) return false;
        EnemySpawnScheduler scheduler = EnemySpawnScheduler.Instance;
        if (scheduler != null && scheduler.PendingCount > 0) return false;
        GameResultManager result = GameResultManager.Instance;
        if (result != null && result.winByDefeatingBoss)
        {
            BossNest nest = result.bossNest;
            if (nest == null || !nest.isActiveAndEnabled || !nest.WasSpawned) return false;
        }
        return true;
    }

    void LockUIInput()
    {
        foreach (BaseInputModule module in FindObjectsByType<BaseInputModule>(FindObjectsSortMode.None))
        {
            if (!module.enabled) continue;
            lockedInputModules.Add(module);
            module.enabled = false;
        }
    }

    void Finish()
    {
        if (!running) return;
        running = false;
        HideVisuals();
        foreach (BaseInputModule module in lockedInputModules)
            if (module != null) module.enabled = true;
        lockedInputModules.Clear();
        if (Time.timeScale == 0f && (GameResultManager.Instance == null || !GameResultManager.Instance.IsGameOver))
            Time.timeScale = restoreTimeScale;
    }

    void HideVisuals()
    {
        if (blackout != null) { blackout.alpha = 0f; blackout.blocksRaycasts = false; }
    }

    void HideLegacyText()
    {
        if (stageNameText != null) stageNameText.enabled = false;
        if (objectiveText != null) objectiveText.enabled = false;
        if (announcementText != null) announcementText.enabled = false;
    }

    void OnDisable() { Finish(); }
    void OnDestroy()
    {
        Finish();
        if (Instance == this) Instance = null;
    }
}
