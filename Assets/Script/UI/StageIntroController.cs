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
    [Label("안내 문구 그룹")]
    [Tooltip("스테이지 이름과 안내 텍스트의 CanvasGroup입니다. 암전 패널과 서로 부모·자식이 아닌 별도 그룹으로 연결합니다.")]
    public CanvasGroup captions;
    [Label("스테이지 이름 텍스트")] public TMP_Text stageNameText;
    [Label("목표 텍스트")] public TMP_Text objectiveText;
    [Label("로어 안내 텍스트")] public TMP_Text announcementText;

    [Header("연출 시간")]
    [Label("화면 공개 시간(초)")]
    [Min(0f)] public float revealDuration = 1f;
    [Label("공개 후 안내 유지 시간(초)")]
    [Tooltip("화면 공개 후 이 시간이 지나면 문구를 지우고 조작을 시작합니다. 로어 재생이 끝날 때까지 기다리지는 않습니다.")]
    [Min(0f)] public float announcementDuration = 1f;
    [Label("문구 사라짐 시간(초)")]
    [Min(0f)] public float dismissDuration = 0.3f;
    [Label("로딩 대기 제한(초)")]
    [Tooltip("초기 배치에 실패해도 암전에 갇히지 않도록 제한 시간이 지나면 연출을 취소합니다. 실제 시간 기준입니다.")]
    [Min(1f)] public float loadingTimeout = 30f;

    [Header("문구")]
    [Label("기본 스테이지 이름")] public string fallbackStageName = "전투 시작";
    [Label("일반 목표 안내")] public string standardObjective = "승리 조건을 달성하세요";
    [Label("보스 목표 안내")] public string bossObjective = "보스를 처치하세요";
    [Label("로어 안내")] public string roarAnnouncement = "멀리서 포효가 들려옵니다";

    StageIntroTiming timing;
    bool running;
    float restoreTimeScale;
    float loadingStarted;
    int startFrame;
    StageBuildingSpawnPoint[] initialMarkers;
    readonly List<BaseInputModule> lockedInputModules = new List<BaseInputModule>();

    void Awake()
    {
        if (!enabled) { HideVisuals(); return; }
        if (Instance != null && Instance != this) { Destroy(this); return; }
        if (blackout == null || captions == null || !blackout.gameObject.activeInHierarchy ||
            !captions.gameObject.activeInHierarchy || blackout.transform.IsChildOf(captions.transform) ||
            captions.transform.IsChildOf(blackout.transform))
        {
            Debug.LogWarning("StageIntroController: 활성 상태의 암전 패널과 안내 문구 그룹을 별도로 연결하세요. 시작 연출을 생략합니다.", this);
            HideVisuals();
            return;
        }
        Instance = this;
        running = true;
        restoreTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        blackout.alpha = 1f;
        blackout.blocksRaycasts = true;
        captions.alpha = 0f;
        captions.blocksRaycasts = false;
        if (announcementText != null) announcementText.text = string.Empty;
        timing = new StageIntroTiming(revealDuration, announcementDuration, dismissDuration);
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
            PrepareCaptions();
            FogOfWarManager.Instance?.RefreshVisionNow();
            RTSCameraPivotController cameraController = FindFirstObjectByType<RTSCameraPivotController>();
            if (cameraController != null) cameraController.FocusStageStart();
        }
        if (previous == StageIntroTiming.Phase.Revealing && timing.State == StageIntroTiming.Phase.Announcement)
        {
            HasRevealed = true;
            BossNest nest = GetBossNest();
            if (announcementText != null)
                announcementText.text = nest != null && nest.introSound != null ? roarAnnouncement : string.Empty;
        }
        switch (timing.State)
        {
            case StageIntroTiming.Phase.Revealing:
                blackout.alpha = 1f - timing.Progress;
                captions.alpha = timing.Progress;
                break;
            case StageIntroTiming.Phase.Announcement:
                blackout.alpha = 0f;
                captions.alpha = 1f;
                break;
            case StageIntroTiming.Phase.Dismissing:
                captions.alpha = 1f - timing.Progress;
                break;
            case StageIntroTiming.Phase.Finished:
                Finish();
                break;
        }
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

    BossNest GetBossNest()
    {
        GameResultManager result = GameResultManager.Instance;
        return result != null && result.winByDefeatingBoss ? result.bossNest : null;
    }

    void PrepareCaptions()
    {
        MapConfig config = MapLoader.Instance != null ? MapLoader.Instance.LoadedConfig : null;
        if (stageNameText != null) stageNameText.text = config != null ? config.displayName : fallbackStageName;
        if (objectiveText != null) objectiveText.text = GetBossNest() != null ? bossObjective : standardObjective;
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
        if (captions != null) { captions.alpha = 0f; captions.blocksRaycasts = false; }
    }

    void OnDisable() { Finish(); }
    void OnDestroy()
    {
        Finish();
        if (Instance == this) Instance = null;
    }
}
