using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 승리/패배를 판정하고 게임 통계를 기록합니다.
// - 승리: 맵의 적 스포너를 모두 파괴
// - 패배: 플레이어 본부(Headquarters)가 모두 파괴
// 씬에 빈 오브젝트를 하나 만들어 붙여 사용합니다.
[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public class GameResultManager : MonoBehaviour
{
    public static GameResultManager Instance { get; private set; }

    [Header("Owner")]
    [Tooltip("플레이어 소속 ID입니다.")]
    public int playerOwnerId = 1;

    [Header("Victory Condition")]
    [Tooltip("켜면 적 스포너를 모두 파괴했을 때 승리합니다.")]
    public bool winWhenAllSpawnersDestroyed = true;

    [Tooltip("게임 시작 후 이 시간(초)이 지나야 승리 판정을 시작합니다. 시작 직후 스포너가 배치되기 전에 승리하는 것을 막습니다.")]
    public float victoryCheckStartDelay = 3f;

    [Header("Defeat Condition")]
    [Tooltip("켜면 플레이어 본부가 모두 파괴됐을 때 패배합니다.")]
    public bool loseWhenHeadquartersDestroyed = true;

    [Header("Result")]
    [Tooltip("승패가 결정된 뒤 결과 창을 띄우기까지 대기 시간(초, 실제 시간)입니다. 파괴 연출을 보여주기 위함입니다.")]
    public float resultShowDelay = 1.5f;

    [Tooltip("켜면 결과 창이 뜰 때 게임을 일시정지(Time.timeScale = 0)합니다.")]
    public bool pauseOnResult = true;

    [Header("Scenes")]
    [Tooltip("'함선으로(로비)' 버튼을 눌렀을 때 이동할 씬 이름입니다. File > Build Settings 에 등록되어 있어야 합니다.")]
    public string lobbySceneName = "";

    // 게임 통계
    public float PlayTime { get; private set; }
    public int DestroyedSpawnerCount { get; private set; }
    public int KilledEnemyUnitCount { get; private set; }
    public int SpentWatt { get; private set; }

    public bool IsGameOver { get; private set; }
    public bool IsVictory { get; private set; }

    // 결과 창을 띄울 시점에 발생합니다. (true = 승리)
    public event Action<bool> OnGameResult;

    int spawnersSeen;
    WattManager wattManager;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 이전 판에서 일시정지된 상태로 씬을 다시 불러온 경우를 대비합니다.
        Time.timeScale = 1f;
    }

    void OnEnable()
    {
        EntityHealth.OnAnyDied += HandleAnyDied;
        EnemySpawner.OnSpawnerRegistered += HandleSpawnerRegistered;
        EnemySpawner.OnSpawnerDestroyed += HandleSpawnerDestroyed;
    }

    void OnDisable()
    {
        EntityHealth.OnAnyDied -= HandleAnyDied;
        EnemySpawner.OnSpawnerRegistered -= HandleSpawnerRegistered;
        EnemySpawner.OnSpawnerDestroyed -= HandleSpawnerDestroyed;

        if (wattManager != null)
            wattManager.OnWattSpent -= HandleWattSpent;
    }

    void Start()
    {
        // 씬에 미리 놓인 스포너도 카운트합니다.
        spawnersSeen += EnemySpawner.ActiveSpawners.Count;

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

        if (winWhenAllSpawnersDestroyed &&
            PlayTime >= victoryCheckStartDelay &&
            spawnersSeen > 0 &&
            EnemySpawner.ActiveSpawners.Count == 0)
        {
            EndGame(true);
        }
    }

    void HandleSpawnerRegistered(EnemySpawner spawner)
    {
        spawnersSeen++;
    }

    void HandleSpawnerDestroyed(EnemySpawner spawner)
    {
        if (IsGameOver)
            return;

        DestroyedSpawnerCount++;
    }

    void HandleWattSpent(int amount)
    {
        if (IsGameOver)
            return;

        SpentWatt += amount;
    }

    void HandleAnyDied(EntityHealth health)
    {
        if (IsGameOver || health == null)
            return;

        SelectableEntity entity = health.Entity;

        if (entity == null)
            return;

        if (entity.ownerId != playerOwnerId &&
            entity.entityType == SelectableEntityType.Unit)
        {
            KilledEnemyUnitCount++;
            return;
        }

        if (loseWhenHeadquartersDestroyed &&
            entity.ownerId == playerOwnerId &&
            entity.GetComponent<Headquarters>() != null &&
            !HasAlivePlayerHeadquarters())
        {
            EndGame(false);
        }
    }

    bool HasAlivePlayerHeadquarters()
    {
        foreach (SelectableEntity building in BuildingRegistry.Buildings)
        {
            if (building == null || building.ownerId != playerOwnerId)
                continue;

            if (building.GetComponent<Headquarters>() == null)
                continue;

            EntityHealth health = building.CachedHealth;

            if (health == null || health.IsAlive)
                return true;
        }

        return false;
    }

    // 강제로 승리/패배 처리할 때도 사용할 수 있습니다.
    public void EndGame(bool victory)
    {
        if (IsGameOver)
            return;

        IsGameOver = true;
        IsVictory = victory;

        Debug.Log(victory ? "GameResultManager: 미션 성공" : "GameResultManager: 미션 실패");

        if (resultShowDelay > 0f)
            StartCoroutine(ShowResultAfterDelay());
        else
            ShowResult();
    }

    IEnumerator ShowResultAfterDelay()
    {
        yield return new WaitForSecondsRealtime(resultShowDelay);
        ShowResult();
    }

    void ShowResult()
    {
        if (pauseOnResult)
            Time.timeScale = 0f;

        OnGameResult?.Invoke(IsVictory);
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
        // Build Settings 에 없는 씬도 에디터에서는 다시 불러올 수 있게 합니다.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            current.path,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogWarning("GameResultManager: 현재 씬이 Build Settings 에 등록되어 있지 않아 재시작할 수 없습니다.");
#endif
    }

    // 로비(함선) 씬으로 이동합니다.
    public void GoToLobby()
    {
        if (string.IsNullOrEmpty(lobbySceneName))
        {
            Debug.LogWarning("GameResultManager: Lobby Scene Name 이 비어 있습니다. 인스펙터에서 로비 씬 이름을 입력하세요.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(lobbySceneName);
    }
}
