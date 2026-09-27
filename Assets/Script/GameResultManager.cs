using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 승리/패배 판정을 모아두는 매니저입니다.
// - 패배: 본부 파괴
// - 승리: 지정한 밤까지 생존 / 적 스포너 모두 파괴 (켜 둔 조건 중 먼저 달성한 것)
// 게임 통계(게임 시간, 파괴한 스포너, 처치한 적 유닛, 소모한 와트)도 여기서 기록하고,
// 결과 창(GameResultUI)의 재시작/로비 이동도 이 매니저가 처리합니다.
// 나중에 조건을 늘릴 때는 EndGame(Result...)을 호출하는 검사 함수를 추가하면 됩니다
// (예: 특정 유닛 전멸, 특정 타이머, 목표 지점 점령 등).
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class GameResultManager : MonoBehaviour
{
    public static GameResultManager Instance { get; private set; }

    public enum Result
    {
        None,
        Victory,
        Defeat,
    }

    [Header("참조")]
    [Label("낮밤 사이클")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public DayNightCycle dayNightCycle;

    [Label("플레이어 ID")]
    [Tooltip("본부(HQ)를 찾을 때 사용하는 ownerId입니다.")]
    public int playerOwnerId = 1;

    [Header("승리 조건 - N번째 밤까지 생존")]
    [Label("생존 승리 사용")]
    [Tooltip("켜면 본부가 파괴되지 않고 지정한 밤까지 버텼을 때 승리합니다.")]
    public bool winBySurvivingNights = true;

    [Label("승리까지 버틸 밤 수")]
    [Tooltip("본부가 파괴되지 않고 이 밤까지 버티면 승리합니다. (예: 5 = 5번째 밤이 끝나면 승리)")]
    public int survivalNightsToWin = 5;

    [Header("승리 조건 - 적 스포너 모두 파괴")]
    [Label("스포너 전멸 승리 사용")]
    [Tooltip("켜면 맵의 적 스포너를 모두 파괴했을 때 승리합니다.")]
    public bool winWhenAllSpawnersDestroyed = true;

    [Label("판정 시작 대기(초)")]
    [Tooltip("게임 시작 후 이 시간이 지나야 스포너 전멸 판정을 시작합니다. 맵이 불러와지기 전에 승리하는 것을 막습니다.")]
    public float spawnerCheckStartDelay = 3f;

    [Header("결과 창")]
    [Label("결과 창 표시 대기(초)")]
    [Tooltip("승패가 결정된 뒤 결과 창을 띄우기까지 기다리는 시간(실제 시간)입니다. 파괴 연출을 보여주기 위함입니다.")]
    public float resultShowDelay = 1.5f;

    [Label("결과 창에서 일시정지")]
    [Tooltip("켜면 결과 창이 뜰 때 게임을 멈춥니다. (Time.timeScale = 0)")]
    public bool pauseOnResult = true;

    [Label("로비 씬 이름")]
    [Tooltip("'함선으로' 버튼을 눌렀을 때 이동할 씬 이름입니다. File > Build Settings에 등록되어 있어야 합니다.")]
    public string lobbySceneName = "Lobby";

    public Result CurrentResult { get; private set; } = Result.None;

    public bool IsGameOver => CurrentResult != Result.None;

    // 승패가 결정된 즉시 발생합니다.
    public event Action<Result> OnGameEnded;

    // 결과 창을 띄울 시점(표시 대기 후)에 발생합니다. GameResultUI가 사용합니다.
    public event Action<Result> OnResultScreenShown;

    // 게임 통계
    public float PlayTime { get; private set; }
    public int DestroyedSpawnerCount { get; private set; }
    public int KilledEnemyUnitCount { get; private set; }
    public float SpentWatt { get; private set; }

    EntityHealth hqHealth;
    WattManager wattManager;
    int maxSpawnersSeen;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        // 결과 창에서 일시정지된 채로 씬을 다시 불러온 경우를 대비한다.
        Time.timeScale = 1f;

        if (dayNightCycle == null)
            dayNightCycle = FindObjectOfType<DayNightCycle>();
    }

    void OnEnable()
    {
        if (dayNightCycle != null)
            dayNightCycle.OnPhaseStarted += HandlePhaseStarted;

        EntityHealth.OnAnyDied += HandleAnyDied;
    }

    void OnDisable()
    {
        if (dayNightCycle != null)
            dayNightCycle.OnPhaseStarted -= HandlePhaseStarted;

        EntityHealth.OnAnyDied -= HandleAnyDied;

        if (wattManager != null)
            wattManager.OnWattSpent -= HandleWattSpent;

        UnsubscribeHq();
    }

    void Start()
    {
        TrySubscribeHq();

        wattManager = WattManager.Instance;

        if (wattManager == null)
            wattManager = FindObjectOfType<WattManager>();

        if (wattManager != null)
            wattManager.OnWattSpent += HandleWattSpent;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (IsGameOver)
            return;

        PlayTime += Time.deltaTime;
        CheckAllSpawnersDestroyed();
    }

    void TrySubscribeHq()
    {
        if (hqHealth != null)
            return;

        foreach (SelectableEntity building in BuildingRegistry.Buildings)
        {
            if (building == null || building.ownerId != playerOwnerId)
                continue;

            if (building.GetComponent<Headquarters>() == null)
                continue;

            hqHealth = building.GetComponent<EntityHealth>();

            if (hqHealth != null)
                hqHealth.OnDied += HandleHqDied;

            break;
        }
    }

    void UnsubscribeHq()
    {
        if (hqHealth == null)
            return;

        hqHealth.OnDied -= HandleHqDied;
        hqHealth = null;
    }

    void HandleHqDied()
    {
        EndGame(Result.Defeat);
    }

    void HandlePhaseStarted(DayNightPhase phase)
    {
        if (IsGameOver)
            return;

        // 본부가 씬에 늦게 배치/생성되는 경우를 대비해 매 페이즈마다 한 번씩 확인한다.
        if (hqHealth == null)
            TrySubscribeHq();

        if (!winBySurvivingNights)
            return;

        if (phase != DayNightPhase.Day || dayNightCycle == null)
            return;

        if (dayNightCycle.CycleCount >= survivalNightsToWin)
            EndGame(Result.Victory);
    }

    void CheckAllSpawnersDestroyed()
    {
        if (!winWhenAllSpawnersDestroyed)
            return;

        int alive = CountAliveSpawners();

        // 스포너가 한 번도 없던 맵에서는 이 조건으로 승리하지 않는다.
        if (alive > maxSpawnersSeen)
            maxSpawnersSeen = alive;

        if (PlayTime < spawnerCheckStartDelay || maxSpawnersSeen == 0)
            return;

        if (alive == 0)
            EndGame(Result.Victory);
    }

    public static int CountAliveSpawners()
    {
        int count = 0;

        foreach (EnemySpawner spawner in EnemySpawner.Active)
        {
            if (spawner != null && !spawner.IsDead)
                count++;
        }

        return count;
    }

    void HandleAnyDied(EntityHealth health)
    {
        if (IsGameOver || health == null)
            return;

        SelectableEntity entity = health.Entity;

        if (entity == null || entity.ownerId == playerOwnerId)
            return;

        if (health.GetComponent<EnemySpawner>() != null)
            DestroyedSpawnerCount++;
        else if (entity.entityType == SelectableEntityType.Unit)
            KilledEnemyUnitCount++;
    }

    void HandleWattSpent(float amount)
    {
        if (IsGameOver)
            return;

        SpentWatt += amount;
    }

    public void EndGame(Result result)
    {
        if (IsGameOver || result == Result.None)
            return;

        CurrentResult = result;
        UnsubscribeHq();

        Debug.Log($"GameResultManager: 게임 종료 - {result}");
        OnGameEnded?.Invoke(result);

        if (resultShowDelay > 0f)
            StartCoroutine(ShowResultAfterDelay());
        else
            ShowResultScreen();
    }

    IEnumerator ShowResultAfterDelay()
    {
        yield return new WaitForSecondsRealtime(resultShowDelay);
        ShowResultScreen();
    }

    void ShowResultScreen()
    {
        if (pauseOnResult)
            Time.timeScale = 0f;

        OnResultScreenShown?.Invoke(CurrentResult);
    }

    // 현재 미션(씬)을 처음부터 다시 시작합니다.
    public void RestartMission()
    {
        Time.timeScale = 1f;
        Scene current = SceneManager.GetActiveScene();

        if (current.buildIndex >= 0)
        {
            SceneManager.LoadScene(current.buildIndex);
            return;
        }

#if UNITY_EDITOR
        // Build Settings에 없는 씬도 에디터에서는 다시 불러올 수 있게 한다.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            current.path,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogWarning("GameResultManager: 현재 씬이 Build Settings에 등록되어 있지 않아 재시작할 수 없습니다.");
#endif
    }

    // 로비(함선) 씬으로 이동합니다.
    public void GoToLobby()
    {
        if (string.IsNullOrEmpty(lobbySceneName))
        {
            Debug.LogWarning("GameResultManager: '로비 씬 이름'이 비어 있습니다. 인스펙터에서 입력하세요.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
        {
            Debug.LogWarning($"GameResultManager: '{lobbySceneName}' 씬이 Build Settings에 등록되어 있지 않습니다. File > Build Settings에서 추가하세요.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(lobbySceneName);
    }
}
