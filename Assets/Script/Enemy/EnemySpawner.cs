using System.Collections.Generic;
using UnityEngine;

// 밤 공격대는 밤의 0/3, 1/3, 2/3 시점에 출격하고 수비대는 아침에 부족한 수만 보충한다.
[DefaultExecutionOrder(110)]
[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    public const int NightBatchCount = 3;
    [Header("스폰 대상")]
    [Label("적 프리팹 목록")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();
    [Label("적 소속 ID")]
    public int enemyOwnerId = 2;
    [Header("스폰 위치")]
    [Label("스폰 반경")]
    [Min(0f)] public float spawnRadius = 6f;
    [Label("스폰 위치 시도 횟수")]
    [Min(1)] public int spawnPositionAttempts = 16;
    [Header("밤 공격대")]
    [Label("밤 공격대 사용")]
    public bool spawnPeriodically = true;
    [Label("밤 총 출격 수")]
    [Tooltip("웨이브 적 구성이 비어 있을 때 사용하는 밤 전체 기본 물량입니다. 기존 1회 스폰 수를 이어받습니다.")]
    [Min(0)] public int enemiesPerSpawn = 60;
    [Label("공격대 최대 생존 수")]
    [Tooltip("공격대의 생존 수와 생성 예약 수에 적용합니다. 수비대는 제외합니다. 0이면 무제한입니다. 막힌 물량은 그 밤 동안 대기합니다.")]
    [Min(0)] public int maxAliveEnemies;
    [Label("본부로 진군")]
    public bool advanceToEnemyBuildings = true;
    [Header("점령지 공격대")]
    [Label("점령 중 출격")]
    [Tooltip("낮밤과 관계없이 점령 또는 경합 중인 거점으로 추가 공격대를 보냅니다. 활동 시작 웨이브와 공격대 생존 상한을 적용합니다.")]
    public bool spawnDuringCapture = true;
    [Label("점령 출격 간격(초)")]
    [Tooltip("점령 시작 후 첫 출격까지의 시간과 이후 출격 간격입니다.")]
    [Min(0.1f)] public float captureSpawnInterval = 10f;
    [Label("점령 출격 수")]
    [Tooltip("매 출격마다 이 스포너에서 생성할 수입니다. 적 프리팹 목록을 사용하며 웨이브의 능력치 보정만 적용합니다.")]
    [Min(0)] public int captureSpawnCount = 3;
    [Header("수비대")]
    [Label("수비대 정원")]
    [Tooltip("첫 배치와 매일 아침에 생존 수와 보충 예약 수를 뺀 부족분만 생성합니다. 공격대 수 배율과 생존 상한은 적용하지 않습니다.")]
    [Min(0)] public int guardCapacity = 20;
    [Label("수비대 프리팹 목록")]
    [Tooltip("비우면 적 프리팹 목록을 사용합니다.")]
    public List<GameObject> guardPrefabs = new List<GameObject>();
    [Label("수비대 방어 반경")]
    [Min(1f)] public float guardRadius = 18f;
    [Header("파괴 시 스폰")]
    [Label("파괴 시 스폰 수")]
    [Tooltip("파괴 방출은 기존 설정을 유지합니다. 공격대 총량이나 수비대 정원에는 포함하지 않습니다.")]
    [Min(0)] public int enemiesOnDeath = 5;
    [Header("웨이브")]
    [Label("활동 시작 웨이브")]
    [Min(1)] public int activateFromWave = 1;
    [Header("디버그")]
    [Label("스폰 로그")]
    public bool logSpawnEvents;

    static readonly List<EnemySpawner> active = new List<EnemySpawner>();
    public static IReadOnlyList<EnemySpawner> Active => active;
    readonly List<EntityHealth> attackers = new List<EntityHealth>();
    readonly List<EntityHealth> guards = new List<EntityHealth>();
    readonly List<GameObject> nightOrder = new List<GameObject>();
    readonly Queue<GameObject> dueAttackers = new Queue<GameObject>();
    EntityHealth health;
    DayNightCycle cycle;
    WaveTuning tuning = new WaveTuning();
    WaveTuning nightTuning;
    int nightCycle = -1;
    int morningCycle = -1;
    int nextBatch;
    int orderIndex;
    bool initialGuardsPlaced;
    bool wasNight;
    bool isDead;
    double mutationRemainder;
    CapturePoint captureObjective;
    float captureSpawnElapsed;

    public int AliveCount { get { attackers.RemoveAll(IsGone); return attackers.Count; } }
    public int GuardAliveCount { get { guards.RemoveAll(IsGone); return guards.Count; } }
    public int PendingSpawnCount { get; private set; }
    public int PendingGuardCount { get; private set; }
    public int WaitingAttackCount => dueAttackers.Count;
    public int CompletedBatches => nextBatch;
    public bool IsDead => isDead;
    public int AppliedWaveNumber { get; private set; } = 1;
    public bool IsAwakeForCurrentWave => AppliedWaveNumber >= Mathf.Max(1, activateFromWave);
    public WaveTuning AppliedTuning => tuning;
    public bool UsesWaveComposition => tuning.HasEnemyComposition;
    public int EffectiveEnemiesPerSpawn => GetNightTotal(enemiesPerSpawn, tuning);
    public int EffectiveMaxAliveEnemies => GetEffectiveMaxAlive(maxAliveEnemies, tuning);
    public int EffectiveEnemiesOnDeath => Mathf.Max(0, enemiesOnDeath);
    public bool IsBlockedByAliveCap => EffectiveMaxAliveEnemies > 0 && AliveCount + PendingSpawnCount >= EffectiveMaxAliveEnemies;
    public int WaveCompositionTotal => nightOrder.Count;
    public int WaveCompositionRemaining => Mathf.Max(0, nightOrder.Count - orderIndex);
    public bool IsWaveCompositionDone => nextBatch >= NightBatchCount && dueAttackers.Count == 0;
    public float SpawnCountdown => cycle != null && cycle.IsNight && nextBatch < NightBatchCount
        ? Mathf.Max(0f, cycle.nightDuration * nextBatch / NightBatchCount - cycle.PhaseElapsed) : 0f;
    static bool IsGone(EntityHealth entity) => entity == null || !entity.IsAlive;

    void Awake()
    {
        health = GetComponent<EntityHealth>();
        if (health != null) health.OnDied += HandleDied;
    }
    void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
        WaveManager.Instance?.ApplyCurrentWaveTo(this);
    }
    void OnDisable() => active.Remove(this);
    void OnDestroy()
    {
        if (health != null) health.OnDied -= HandleDied;
    }
    public void ApplyWaveTuning(WaveTuning value, int waveNumber)
    {
        tuning = (value ?? new WaveTuning()).Sanitized();
        AppliedWaveNumber = Mathf.Max(1, waveNumber);
    }
    public static int GetEffectiveSpawnCount(int baseCount, WaveTuning value) => Mathf.Max(0,
        value == null ? baseCount : Mathf.RoundToInt(baseCount * value.spawnCountMultiplier) + value.spawnCountBonus);
    public static int GetNightTotal(int fallback, WaveTuning value) =>
        GetEffectiveSpawnCount(value != null && value.HasEnemyComposition ? value.enemyComposition.TotalCount : fallback, value);
    public static int GetEffectiveMaxAlive(int baseMax, WaveTuning value) =>
        value != null && value.maxAliveEnemies > 0 ? value.maxAliveEnemies : Mathf.Max(0, baseMax);
    public static int GetBatchSize(int total, int batch) => NightSpawnMath.BatchSize(total, batch);
    public void ResetMutationSpawnBudget() => mutationRemainder = 0d;

    void Update()
    {
        if (isDead || (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver)) return;
        UpdateCaptureSpawns();
        if (cycle == null) cycle = DayNightCycle.Instance != null ? DayNightCycle.Instance : FindObjectOfType<DayNightCycle>();
        if (cycle == null || !IsAwakeForCurrentWave) return;
        // 모든 페이즈 이벤트와 웨이브 보정이 끝난 뒤 처리하여 구독 순서에 영향을 받지 않는다.
        if (!initialGuardsPlaced || (cycle.IsDay && morningCycle != cycle.CycleCount))
        {
            initialGuardsPlaced = true;
            morningCycle = cycle.CycleCount;
            RefillGuards();
        }
        if (wasNight != cycle.IsNight)
        {
            wasNight = cycle.IsNight;
            foreach (EntityHealth unit in attackers)
                if (unit != null) unit.GetComponent<EnemyCombatAI>()?.SetAdvanceToEnemyBuildings(wasNight && advanceToEnemyBuildings);
        }
        if (!cycle.IsNight)
        {
            dueAttackers.Clear();
            return;
        }
        if (!spawnPeriodically) return;
        if (nightCycle != cycle.CycleCount) BeginNight();
        while (nextBatch < NightBatchCount && cycle.PhaseElapsed >= cycle.nightDuration * nextBatch / NightBatchCount)
            ReleaseBatch();
        while (dueAttackers.Count > 0 && !IsBlockedByAliveCap)
            QueueEnemy(dueAttackers.Dequeue(), nightTuning, false);
    }

    bool CanSendToCapturePoint(CapturePoint point)
    {
        GameResultManager result = GameResultManager.Instance;
        return spawnDuringCapture && IsAwakeForCurrentWave && isActiveAndEnabled && !isDead &&
            result != null && !result.IsGameOver && result.winByCapturingPoint &&
            enemyOwnerId != result.playerOwnerId && result.capturePoint == point &&
            point != null && point.isActiveAndEnabled && point.HasValidRadius &&
            (point.State == CapturePoint.CaptureState.Capturing || point.State == CapturePoint.CaptureState.Contested);
    }

    void UpdateCaptureSpawns()
    {
        CapturePoint point = GameResultManager.Instance != null ? GameResultManager.Instance.capturePoint : null;
        if (captureObjective != point || !CanSendToCapturePoint(point))
        {
            captureObjective = point;
            captureSpawnElapsed = 0f;
        }
        if (!CanSendToCapturePoint(point) || Time.deltaTime <= 0f) return;
        captureSpawnElapsed += Time.deltaTime;
        if (captureSpawnElapsed < Mathf.Max(0.1f, captureSpawnInterval)) return;
        captureSpawnElapsed = 0f;
        for (int i = 0; i < captureSpawnCount && !IsBlockedByAliveCap; i++)
        {
            GameObject prefab = PickPrefab(enemyPrefabs);
            if (prefab != null) QueueEnemy(prefab, new WaveTuning(tuning), false, captureTarget: point);
        }
    }

    void BeginNight()
    {
        nightCycle = cycle.CycleCount;
        nextBatch = orderIndex = 0;
        dueAttackers.Clear();
        nightTuning = new WaveTuning(tuning);
        nightOrder.Clear();
        int total = GetNightTotal(enemiesPerSpawn, nightTuning);
        var composition = new List<GameObject>();
        if (nightTuning.HasEnemyComposition) nightTuning.enemyComposition.BuildSpawnOrder(composition);
        for (int i = 0; i < total; i++)
        {
            GameObject prefab = composition.Count > 0
                ? composition[(int)((long)i * composition.Count / total)] : PickPrefab(enemyPrefabs);
            if (prefab != null) nightOrder.Add(prefab);
        }
    }

    void ReleaseBatch()
    {
        int count = GetBatchSize(nightOrder.Count, nextBatch);
        int released = 0;
        float bonus = EnemyMutationManager.Instance != null ? EnemyMutationManager.Instance.GetSpawnBonus(enemyOwnerId) : 0f;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = nightOrder[orderIndex++];
            dueAttackers.Enqueue(prefab);
            released++;
            int extra = EnemyMutationMath.AccumulateExtraSpawns(ref mutationRemainder, bonus);
            for (int n = 0; n < extra; n++) { dueAttackers.Enqueue(prefab); released++; }
        }
        nextBatch++;
        if (logSpawnEvents) Debug.Log($"EnemySpawner '{name}': 밤 {nextBatch}/3차 공격대 {released}마리 출격 예약", this);
    }

    void RefillGuards()
    {
        int missing = NightSpawnMath.GuardDeficit(guardCapacity, GuardAliveCount, PendingGuardCount);
        List<GameObject> prefabs = guardPrefabs != null && guardPrefabs.Exists(p => p != null) ? guardPrefabs : enemyPrefabs;
        for (int i = 0; i < missing; i++)
        {
            GameObject prefab = PickPrefab(prefabs);
            if (prefab != null) QueueEnemy(prefab, new WaveTuning(tuning), true);
        }
        if (logSpawnEvents) Debug.Log($"EnemySpawner '{name}': 수비대 생존 {GuardAliveCount}, 보충 예약 {PendingGuardCount}, 정원 {guardCapacity}", this);
    }

    static GameObject PickPrefab(List<GameObject> prefabs)
    {
        if (prefabs == null || prefabs.Count == 0) return null;
        int start = Random.Range(0, prefabs.Count);
        for (int i = 0; i < prefabs.Count; i++)
            if (prefabs[(start + i) % prefabs.Count] != null) return prefabs[(start + i) % prefabs.Count];
        return null;
    }

    void QueueEnemy(GameObject prefab, WaveTuning spawnTuning, bool guard, bool deathBurst = false, CapturePoint captureTarget = null)
    {
        bool captureRaid = captureTarget != null;
        Vector3 center = transform.position;
        float radius = guard ? Mathf.Min(spawnRadius, guardRadius * 0.5f) : spawnRadius;
        int attempts = spawnPositionAttempts;
        int owner = enemyOwnerId;
        int scheduledCycle = cycle != null ? cycle.CycleCount : -1;
        float defenseRadius = guardRadius;
        bool advance = advanceToEnemyBuildings;
        if (guard) PendingGuardCount++; else if (!deathBurst) PendingSpawnCount++;
        EnemySpawnScheduler.Enqueue(() =>
        {
            if (guard) PendingGuardCount = Mathf.Max(0, PendingGuardCount - 1);
            else if (!deathBurst) PendingSpawnCount = Mathf.Max(0, PendingSpawnCount - 1);
            if (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver) return;
            if (!deathBurst && (this == null || isDead)) return;
            if (captureRaid && !CanSendToCapturePoint(captureTarget)) return;
            if (!guard && !deathBurst && !captureRaid && (cycle == null || !cycle.IsNight || cycle.CycleCount != scheduledCycle)) return;
            Vector3 position = EnemySpawnUtility.GetRandomPositionInRadius(center, radius, false, attempts);
            GameObject enemy = EnemySpawnUtility.SpawnEnemy(prefab, position, prefab.transform.rotation, owner,
                spawnTuning.healthMultiplier, spawnTuning.damageMultiplier, spawnTuning.speedMultiplier,
                armorBonus: spawnTuning.armorBonus);
            if (enemy == null) return;
            EntityHealth unitHealth = enemy.GetComponent<EntityHealth>();
            if (!deathBurst && unitHealth != null) (guard ? guards : attackers).Add(unitHealth);
            EnemyCombatAI ai = enemy.GetComponent<EnemyCombatAI>();
            if (ai != null) ai.SetAdvanceToEnemyBuildings(!guard && advance && DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight);
            if (captureRaid && ai != null) ai.SetCaptureDestination(captureTarget);
            if (guard)
            {
                if (ai != null) ai.shareTargetWithNearbyAllies = false;
                EnemySpawnerGuard defender = enemy.GetComponent<EnemySpawnerGuard>();
                if (defender == null) defender = enemy.AddComponent<EnemySpawnerGuard>();
                defender.Initialize(center, defenseRadius);
            }
        });
    }

    void HandleDied()
    {
        if (isDead) return;
        isDead = true;
        dueAttackers.Clear();
        GameResultManager result = GameResultManager.Instance;
        if (result != null && (result.IsGameOver || result.CanWinBySpawnerDestruction)) return;
        EnemyMutationManager.EnsureInstance().NotifySpawnerDestroyed(this);
        RelicManager.Instance?.NotifySpawnerDestroyed(this);
        if (!IsAwakeForCurrentWave) return;
        for (int i = 0; i < EffectiveEnemiesOnDeath; i++)
        {
            GameObject prefab = PickPrefab(enemyPrefabs);
            if (prefab != null) QueueEnemy(prefab, new WaveTuning(tuning), false, true);
        }
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, guardRadius);
    }
}
