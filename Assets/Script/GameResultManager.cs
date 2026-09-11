using System;
using UnityEngine;

// 승리/패배 판정을 모아두는 매니저입니다.
// 지금은 두 조건만 있습니다: 본부 파괴 = 패배, 지정한 밤까지 생존 = 승리.
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

    [Header("References")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public DayNightCycle dayNightCycle;

    [Tooltip("본부(HQ)를 찾을 때 사용하는 ownerId입니다.")]
    public int playerOwnerId = 1;

    [Header("Win Condition - Survive N Nights")]
    [Tooltip("본부가 파괴되지 않고 이 밤까지 버티면 승리합니다. (예: 5 = 5번째 밤이 끝나면 승리)")]
    public int survivalNightsToWin = 5;

    public Result CurrentResult { get; private set; } = Result.None;

    public bool IsGameOver => CurrentResult != Result.None;

    public event Action<Result> OnGameEnded;

    EntityHealth hqHealth;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dayNightCycle == null)
            dayNightCycle = FindObjectOfType<DayNightCycle>();
    }

    void OnEnable()
    {
        if (dayNightCycle != null)
            dayNightCycle.OnPhaseStarted += HandlePhaseStarted;
    }

    void OnDisable()
    {
        if (dayNightCycle != null)
            dayNightCycle.OnPhaseStarted -= HandlePhaseStarted;

        UnsubscribeHq();
    }

    void Start()
    {
        TrySubscribeHq();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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

        if (phase != DayNightPhase.Day || dayNightCycle == null)
            return;

        if (dayNightCycle.CycleCount >= survivalNightsToWin)
            EndGame(Result.Victory);
    }

    void EndGame(Result result)
    {
        if (IsGameOver)
            return;

        CurrentResult = result;
        UnsubscribeHq();

        Debug.Log($"GameResultManager: 게임 종료 - {result}");
        OnGameEnded?.Invoke(result);
    }
}
