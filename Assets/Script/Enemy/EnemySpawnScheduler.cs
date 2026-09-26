using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 생성을 여러 프레임에 나눠 처리합니다.
/// 스포너 여러 개가 같은 프레임에 한꺼번에 스폰하면 Instantiate가 몰려 프레임이 튀므로,
/// 요청을 대기열에 넣고 프레임마다 정해진 수만큼만 생성합니다.
/// 씬에 없으면 처음 요청할 때 자동으로 만들어집니다. 수치를 조절하려면 씬 오브젝트에 직접 붙이세요.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class EnemySpawnScheduler : MonoBehaviour
{
    public static EnemySpawnScheduler Instance { get; private set; }

    [Header("생성 분산")]
    [Label("프레임당 최대 생성 수")]
    [Tooltip("한 프레임에 생성할 적의 최대 수입니다. 0이면 나누지 않고 요청이 들어온 프레임에 모두 생성합니다.")]
    [Min(0)]
    public int maxSpawnsPerFrame = 4;

    [Label("프레임당 생성 시간 한도(ms)")]
    [Tooltip("한 프레임에 적 생성에 쓸 수 있는 시간입니다. 넘으면 남은 요청은 다음 프레임으로 미룹니다. " +
             "0이면 시간 한도 없이 최대 생성 수만 따릅니다. (최소 1마리는 항상 생성합니다)")]
    [Min(0f)]
    public float maxSpawnMillisecondsPerFrame = 4f;

    [Label("대기열 로그")]
    [Tooltip("대기열이 밀릴 때 남은 요청 수를 콘솔에 남깁니다.")]
    public bool logQueue;

    readonly Queue<Action> queue = new Queue<Action>(64);
    readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

    public int PendingCount => queue.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    public static EnemySpawnScheduler EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        Instance = FindObjectOfType<EnemySpawnScheduler>();

        if (Instance != null)
            return Instance;

        GameObject go = new GameObject(nameof(EnemySpawnScheduler));
        return go.AddComponent<EnemySpawnScheduler>();
    }

    /// <summary>
    /// 적 한 마리 생성 작업을 예약합니다. 분산이 꺼져 있으면 바로 실행합니다.
    /// 작업은 스포너가 먼저 파괴돼도 실행되므로, 스포너의 transform 등을 직접 참조하지 마세요.
    /// </summary>
    public static void Enqueue(Action spawnOne)
    {
        if (spawnOne == null)
            return;

        EnemySpawnScheduler scheduler = EnsureInstance();

        if (scheduler.maxSpawnsPerFrame <= 0)
        {
            spawnOne();
            return;
        }

        scheduler.queue.Enqueue(spawnOne);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (queue.Count == 0)
            return;

        int limit = maxSpawnsPerFrame > 0 ? maxSpawnsPerFrame : int.MaxValue;
        double budgetMs = maxSpawnMillisecondsPerFrame;
        int spawned = 0;

        stopwatch.Restart();

        while (queue.Count > 0 && spawned < limit)
        {
            // 최소 1마리는 생성해서 대기열이 영영 줄지 않는 일을 막는다.
            if (spawned > 0 && budgetMs > 0 && stopwatch.Elapsed.TotalMilliseconds >= budgetMs)
                break;

            Action spawnOne = queue.Dequeue();

            try
            {
                spawnOne();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            spawned++;
        }

        if (logQueue && queue.Count > 0)
        {
            Debug.Log(
                $"EnemySpawnScheduler: 이번 프레임 {spawned}마리 생성 ({stopwatch.Elapsed.TotalMilliseconds:0.0}ms), 대기 {queue.Count}마리",
                this);
        }
    }
}
