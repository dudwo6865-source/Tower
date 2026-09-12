using System.Collections.Generic;
using UnityEngine;

// 하이라키에 배치하는 스포너(건물형).
// 스폰 트리거가 셋이고 각각 자기 타이머로 따로 돈다. 조건이 겹치면 그만큼 함께 쏟아진다.
// - 주기 스폰: 낮/밤과 관계없이 일정 간격으로 스폰한다.
// - 근접 스폰: 반경 안에 아군이 있는 동안 추가로 스폰한다.
// - 피격 스폰: 공격받은 뒤 일정 시간 동안 추가로 스폰한다.
// 그리고 스포너가 파괴되면 생존 상한을 무시하고 한 번에 방출한다.
// 스폰된 적은 낮에는 어그로만 반응하고, 밤이 되면 플레이어 HQ로 진군한다.
[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Target")]
    [Tooltip("스폰할 적 프리팹 목록입니다. 여러 개면 매 스폰마다 무작위로 선택합니다.")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();

    [Tooltip("스폰되는 적의 소속 ID입니다. 이 값과 다른 소속 유닛을 '아군'으로 감지합니다.")]
    public int enemyOwnerId = 2;

    [Header("Spawn Position")]
    [Tooltip("이 스포너를 중심으로 적을 스폰할 반경(미터)입니다.")]
    public float spawnRadius = 6f;

    [Tooltip("스폰 위치(NavMesh 위)를 찾기 위한 최대 시도 횟수입니다.")]
    public int spawnPositionAttempts = 16;

    [Header("Periodic Spawn")]
    [Tooltip("켜면 낮/밤과 관계없이 일정 간격으로 적을 스폰합니다.")]
    public bool spawnPeriodically = true;

    [Tooltip("주기 스폰 및 근접/피격 스폰의 간격(초)입니다.")]
    public float spawnInterval = 5f;

    [Tooltip("한 번에 스폰할 적 수입니다.")]
    public int enemiesPerSpawn = 2;

    [Tooltip("이 스포너가 동시에 유지할 최대 생존 적 수입니다. 0이면 무제한입니다.")]
    public int maxAliveEnemies = 0;

    [Header("Proximity Spawn")]
    [Tooltip("이 반경 안에 아군 유닛이 있으면 주기 스폰과 별개로 추가 스폰합니다.")]
    public float allyDetectRadius = 15f;

    [Tooltip("근접 감지 대상을 유닛으로만 한정할지 여부입니다. 끄면 건물도 트리거가 됩니다.")]
    public bool detectUnitsOnly = true;

    [Tooltip("근접 감지로 한 번에 추가 스폰할 적 수입니다. 0이면 근접 추가 스폰을 하지 않습니다.")]
    public int proximityEnemiesPerSpawn = 2;

    [Tooltip("근접 추가 스폰의 간격(초)입니다. 0 이하면 주기 스폰 간격을 그대로 씁니다.")]
    public float proximitySpawnInterval = 0f;

    [Tooltip("아군이 반경 안에 있는지 확인하는 주기(초)입니다. 매 프레임 검사하지 않게 해 부하를 줄입니다.")]
    public float allyCheckInterval = 0.25f;

    [Header("Attacked Spawn")]
    [Tooltip("공격받은 뒤 이 시간(초) 동안 추가 스폰합니다. 이 시간 이상 추가 공격이 없으면 피격 스폰을 멈춥니다.")]
    public float attackedSpawnDuration = 8f;

    [Tooltip("피격 반응으로 한 번에 추가 스폰할 적 수입니다. 0이면 피격 추가 스폰을 하지 않습니다.")]
    public int attackedEnemiesPerSpawn = 2;

    [Tooltip("피격 추가 스폰의 간격(초)입니다. 0 이하면 주기 스폰 간격을 그대로 씁니다.")]
    public float attackedSpawnInterval = 0f;

    [Header("Death Spawn")]
    [Tooltip("스포너가 파괴될 때 스폰할 적 수입니다. 0이면 파괴 시 스폰하지 않습니다.")]
    public int enemiesOnDeath = 5;

    [Header("Enemy Behavior")]
    [Tooltip("밤이 되면 스폰된 적이 어그로에 적이 없을 때 플레이어 HQ로 진군합니다.")]
    public bool advanceToEnemyBuildings = true;

    [Header("Wave")]
    [Tooltip("이 웨이브(1부터)가 되어야 활동을 시작합니다. 1이면 처음부터 활동합니다. " +
             "맵에 스포너를 미리 다 깔아두고 웨이브가 진행될수록 하나씩 깨어나게 할 때 씁니다.")]
    public int activateFromWave = 1;

    [Header("Debug")]
    [Tooltip("어떤 트리거로 몇 마리를 스폰했는지 콘솔에 남깁니다. 설정이 먹히는지 확인할 때 켜세요.")]
    public bool logSpawnEvents = false;

    public int AliveCount { get; private set; }

    /// <summary>씬에서 활성화된 모든 스포너입니다. WaveManager가 웨이브 수치를 적용할 때 씁니다.</summary>
    public static IReadOnlyList<EnemySpawner> Active => active;

    static readonly List<EnemySpawner> active = new List<EnemySpawner>();

    /// <summary>현재 웨이브 보정이 적용된 실제 값입니다.</summary>
    public int EffectiveEnemiesPerSpawn { get; private set; }

    public float EffectiveSpawnInterval { get; private set; }

    public int EffectiveMaxAliveEnemies { get; private set; }

    public int EffectiveEnemiesOnDeath { get; private set; }

    public int EffectiveProximityEnemiesPerSpawn { get; private set; }

    public float EffectiveProximitySpawnInterval { get; private set; }

    public int EffectiveAttackedEnemiesPerSpawn { get; private set; }

    public float EffectiveAttackedSpawnInterval { get; private set; }

    /// <summary>지금 반경 안에 아군이 있어 근접 스폰이 도는 중인지 여부입니다.</summary>
    public bool HasAllyInRange { get; private set; }

    /// <summary>피격 스폰이 남아 있는 시간(초)입니다.</summary>
    public float AttackedSpawnRemaining => Mathf.Max(0f, attackedTimer);

    /// <summary>activateFromWave에 도달해 이 웨이브에 활동하는지 여부입니다.</summary>
    public bool IsAwakeForCurrentWave { get; private set; } = true;

    /// <summary>Awake에서 캡처한 인스펙터 기준값입니다. 웨이브 보정은 항상 이 값에서 다시 계산합니다.</summary>
    public int BaseEnemiesPerSpawn => baseEnemiesPerSpawn;

    public float BaseSpawnInterval => baseSpawnInterval;

    public int BaseMaxAliveEnemies => baseMaxAliveEnemies;

    /// <summary>마지막으로 적용된 웨이브 번호입니다.</summary>
    public int AppliedWaveNumber { get; private set; } = 1;

    /// <summary>마지막으로 적용된 보정값입니다.</summary>
    public WaveTuning AppliedTuning => tuning;

    /// <summary>다음 스폰까지 남은 시간(초)입니다.</summary>
    public float SpawnCountdown => spawnTimer;

    /// <summary>스포너가 파괴되어 더 이상 스폰하지 않는 상태인지 여부입니다.</summary>
    public bool IsDead => isDead;

    /// <summary>생존 상한에 걸려 스폰이 멈춰 있는지 여부입니다.</summary>
    public bool IsBlockedByAliveCap =>
        EffectiveMaxAliveEnemies > 0 && AliveCount >= EffectiveMaxAliveEnemies;

    readonly List<EnemyCombatAI> spawnedAIs = new List<EnemyCombatAI>();
    EntityHealth health;
    DayNightCycle dayNightCycle;
    float spawnTimer;
    float attackedTimer;
    bool isDead;

    // 인스펙터에 적어둔 값을 기준값으로 보관한다. 웨이브 보정은 항상 이 기준값에서
    // 다시 계산하므로, 웨이브가 여러 번 바뀌어도 배율이 누적되지 않는다.
    int baseEnemiesPerSpawn;
    float baseSpawnInterval;
    int baseMaxAliveEnemies;
    int baseEnemiesOnDeath;
    int baseProximityEnemiesPerSpawn;
    float baseProximitySpawnInterval;
    int baseAttackedEnemiesPerSpawn;
    float baseAttackedSpawnInterval;

    // 트리거마다 타이머를 따로 둔다. 하나로 묶으면 조건이 겹쳐도 스폰량이 늘지 않는다.
    float proximityTimer;
    float attackedSpawnTimer;
    float allyCheckTimer;

    WaveTuning tuning = new WaveTuning();

    void Awake()
    {
        health = GetComponent<EntityHealth>();

        if (health != null)
        {
            health.OnDied += HandleDied;
            health.OnDamaged += HandleDamaged;
        }

        baseEnemiesPerSpawn = enemiesPerSpawn;
        baseSpawnInterval = spawnInterval;
        baseMaxAliveEnemies = maxAliveEnemies;
        baseEnemiesOnDeath = enemiesOnDeath;
        baseProximityEnemiesPerSpawn = proximityEnemiesPerSpawn;
        baseProximitySpawnInterval = proximitySpawnInterval;
        baseAttackedEnemiesPerSpawn = attackedEnemiesPerSpawn;
        baseAttackedSpawnInterval = attackedSpawnInterval;

        ApplyWaveTuning(new WaveTuning(), 1);

        if (spawnPeriodically)
            spawnTimer = Mathf.Max(0.1f, EffectiveSpawnInterval);
    }

    void OnEnable()
    {
        BindDayNightCycle();

        if (!active.Contains(this))
            active.Add(this);

        // 웨이브 도중에 생긴 스포너도 바로 현재 웨이브 수치를 따르게 한다.
        WaveManager.Instance?.ApplyCurrentWaveTo(this);
    }

    void OnDisable()
    {
        UnbindDayNightCycle();
        active.Remove(this);
    }

    /// <summary>WaveManager가 웨이브마다 호출합니다. 기준값에 보정을 적용해 실제 값을 다시 만듭니다.</summary>
    public void ApplyWaveTuning(WaveTuning waveTuning, int waveNumber)
    {
        tuning = (waveTuning ?? new WaveTuning()).Sanitized();
        AppliedWaveNumber = Mathf.Max(1, waveNumber);
        IsAwakeForCurrentWave = waveNumber >= Mathf.Max(1, activateFromWave);

        EffectiveEnemiesPerSpawn = GetEffectiveSpawnCount(baseEnemiesPerSpawn, tuning);
        EffectiveEnemiesOnDeath = GetEffectiveDeathSpawnCount(baseEnemiesOnDeath, tuning);
        EffectiveSpawnInterval = GetEffectiveSpawnInterval(baseSpawnInterval, tuning);
        EffectiveMaxAliveEnemies = GetEffectiveMaxAlive(baseMaxAliveEnemies, tuning);

        // 추가 스폰도 같은 웨이브 보정을 받는다. 간격이 0 이하면 주기 스폰 간격을 그대로 쓴다.
        EffectiveProximityEnemiesPerSpawn =
            GetEffectiveDeathSpawnCount(baseProximityEnemiesPerSpawn, tuning);

        EffectiveProximitySpawnInterval = baseProximitySpawnInterval > 0f
            ? GetEffectiveSpawnInterval(baseProximitySpawnInterval, tuning)
            : EffectiveSpawnInterval;

        EffectiveAttackedEnemiesPerSpawn =
            GetEffectiveDeathSpawnCount(baseAttackedEnemiesPerSpawn, tuning);

        EffectiveAttackedSpawnInterval = baseAttackedSpawnInterval > 0f
            ? GetEffectiveSpawnInterval(baseAttackedSpawnInterval, tuning)
            : EffectiveSpawnInterval;
    }

    // 아래 계산식은 에디터 미리보기(EnemySpawnerEditor)와 공유한다.
    // 런타임과 미리보기가 서로 다른 값을 보여주는 일이 없도록 한 곳에만 둔다.

    public static int GetEffectiveSpawnCount(int baseCount, WaveTuning tuning)
    {
        if (tuning == null)
            return Mathf.Max(0, baseCount);

        return Mathf.Max(
            0,
            Mathf.RoundToInt(baseCount * tuning.spawnCountMultiplier) + tuning.spawnCountBonus);
    }

    public static int GetEffectiveDeathSpawnCount(int baseCount, WaveTuning tuning)
    {
        if (tuning == null)
            return Mathf.Max(0, baseCount);

        return Mathf.Max(0, Mathf.RoundToInt(baseCount * tuning.spawnCountMultiplier));
    }

    public static float GetEffectiveSpawnInterval(float baseInterval, WaveTuning tuning)
    {
        if (tuning == null)
            return Mathf.Max(0.1f, baseInterval);

        return Mathf.Max(0.1f, baseInterval * tuning.spawnIntervalMultiplier);
    }

    /// <summary>기준값이 0(무제한)이면 배율을 곱해도 무제한으로 둡니다.</summary>
    public static int GetEffectiveMaxAlive(int baseMaxAlive, WaveTuning tuning)
    {
        if (baseMaxAlive <= 0)
            return 0;

        if (tuning == null)
            return baseMaxAlive;

        return Mathf.Max(1, Mathf.RoundToInt(baseMaxAlive * tuning.maxAliveMultiplier));
    }

    void OnDestroy()
    {
        UnbindDayNightCycle();

        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDamaged -= HandleDamaged;
        }
    }

    void BindDayNightCycle()
    {
        if (dayNightCycle == null)
            dayNightCycle = DayNightCycle.Instance;

        if (dayNightCycle == null)
            dayNightCycle = FindObjectOfType<DayNightCycle>();

        if (dayNightCycle == null)
            return;

        dayNightCycle.OnPhaseStarted -= HandlePhaseStarted;
        dayNightCycle.OnPhaseStarted += HandlePhaseStarted;
    }

    void UnbindDayNightCycle()
    {
        if (dayNightCycle == null)
            return;

        dayNightCycle.OnPhaseStarted -= HandlePhaseStarted;
    }

    void Update()
    {
        if (isDead || !IsAwakeForCurrentWave)
            return;

        if (attackedTimer > 0f)
            attackedTimer -= Time.deltaTime;

        UpdateAllyDetection();

        // 세 트리거는 서로 독립이다. 조건이 겹치면 그만큼 함께 쏟아진다.
        TickPeriodicSpawn();
        TickProximitySpawn();
        TickAttackedSpawn();
    }

    // 아군 감지는 매 프레임 할 필요가 없다. 스포너가 많아지면 그대로 부하가 된다.
    void UpdateAllyDetection()
    {
        if (EffectiveProximityEnemiesPerSpawn <= 0)
        {
            HasAllyInRange = false;
            return;
        }

        allyCheckTimer -= Time.deltaTime;

        if (allyCheckTimer > 0f)
            return;

        allyCheckTimer = Mathf.Max(0.05f, allyCheckInterval);
        HasAllyInRange = HasAllyNearby();
    }

    void TickPeriodicSpawn()
    {
        if (!spawnPeriodically)
        {
            spawnTimer = Mathf.Max(0.1f, EffectiveSpawnInterval);
            return;
        }

        TickSpawnTrigger(
            ref spawnTimer,
            EffectiveSpawnInterval,
            EffectiveEnemiesPerSpawn,
            "주기");
    }

    void TickProximitySpawn()
    {
        // 조건이 풀리면 타이머를 되돌린다. 아군이 들어오자마자 터지지 않고
        // 한 간격을 채워야 나오게 해서, 스쳐 지나가는 유닛에 반응하지 않게 한다.
        if (EffectiveProximityEnemiesPerSpawn <= 0 || !HasAllyInRange)
        {
            proximityTimer = Mathf.Max(0.1f, EffectiveProximitySpawnInterval);
            return;
        }

        TickSpawnTrigger(
            ref proximityTimer,
            EffectiveProximitySpawnInterval,
            EffectiveProximityEnemiesPerSpawn,
            "근접");
    }

    void TickAttackedSpawn()
    {
        if (EffectiveAttackedEnemiesPerSpawn <= 0 || attackedTimer <= 0f)
        {
            attackedSpawnTimer = Mathf.Max(0.1f, EffectiveAttackedSpawnInterval);
            return;
        }

        TickSpawnTrigger(
            ref attackedSpawnTimer,
            EffectiveAttackedSpawnInterval,
            EffectiveAttackedEnemiesPerSpawn,
            "피격");
    }

    void TickSpawnTrigger(ref float timer, float interval, int count, string triggerName)
    {
        if (count <= 0)
            return;

        // 생존 상한에 걸려 있으면 타이머도 멈춘다. 상한이 풀리자마자 몰아서
        // 터지는 것을 막는다.
        if (IsBlockedByAliveCap)
            return;

        timer -= Time.deltaTime;

        if (timer > 0f)
            return;

        timer = Mathf.Max(0.1f, interval);

        int spawned = SpawnBurst(count, respectAliveCap: true);

        if (logSpawnEvents && spawned > 0)
            Debug.Log($"EnemySpawner '{name}': {triggerName} 스폰 {spawned}마리", this);
    }

    void HandlePhaseStarted(DayNightPhase phase)
    {
        ApplyAdvanceToSpawned(phase == DayNightPhase.Night);
    }

    // 적에게 공격받으면 피격 스폰 유지 시간을 초기화(갱신)한다.
    void HandleDamaged(float damage, SelectableEntity attacker)
    {
        // 같은 소속(적군끼리)의 피해는 트리거하지 않는다.
        if (attacker != null && attacker.ownerId == enemyOwnerId)
            return;

        attackedTimer = Mathf.Max(0f, attackedSpawnDuration);
    }

    void HandleDied()
    {
        if (isDead)
            return;

        isDead = true;

        // 아직 깨어나지 않은 스포너를 미리 부수면 방출도 없다. 웨이브가 오기 전에
        // 선제 공격으로 정리하는 플레이가 손해 보지 않게 한다.
        if (!IsAwakeForCurrentWave)
            return;

        // 파괴 시 방출은 생존 상한을 무시하고 정해진 수만큼 모두 스폰한다.
        int spawned = SpawnBurst(EffectiveEnemiesOnDeath, respectAliveCap: false);

        if (logSpawnEvents)
        {
            Debug.Log(
                $"EnemySpawner '{name}': 파괴 방출 {spawned}/{EffectiveEnemiesOnDeath}마리",
                this);
        }
    }

    /// <summary>실제로 스폰한 수를 돌려줍니다. (위치를 못 찾아 건너뛴 만큼 줄어듭니다)</summary>
    int SpawnBurst(int count, bool respectAliveCap)
    {
        if (count <= 0 || enemyPrefabs.Count == 0)
            return 0;

        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            if (respectAliveCap &&
                EffectiveMaxAliveEnemies > 0 &&
                AliveCount >= EffectiveMaxAliveEnemies)
                break;

            GameObject prefab =
                enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];

            if (prefab == null)
                continue;

            Vector3 position = EnemySpawnUtility.GetRandomPositionInRadius(
                transform.position,
                spawnRadius,
                avoidPlayerVision: false,
                spawnPositionAttempts);

            // 웨이브 스탯 가중치를 여기서 적용한다. 스폰된 개체에만 붙으므로
            // 이미 살아있는 적은 그대로 두고, 이후 스폰부터 강해진다.
            GameObject enemyObject = EnemySpawnUtility.SpawnEnemy(
                prefab,
                position,
                prefab.transform.rotation,
                enemyOwnerId,
                tuning.healthMultiplier,
                tuning.damageMultiplier,
                tuning.speedMultiplier,
                TrackAlive);

            if (enemyObject == null)
                continue;

            RegisterSpawnedEnemy(enemyObject);
            spawned++;
        }

        return spawned;
    }

    void RegisterSpawnedEnemy(GameObject enemyObject)
    {
        EnemyCombatAI enemyAI = enemyObject.GetComponent<EnemyCombatAI>();
        if (enemyAI == null)
            return;

        spawnedAIs.Add(enemyAI);
        enemyAI.SetAdvanceToEnemyBuildings(ShouldAdvanceNow());
    }

    bool ShouldAdvanceNow()
    {
        if (!advanceToEnemyBuildings)
            return false;

        if (dayNightCycle == null)
            dayNightCycle = DayNightCycle.Instance;

        return dayNightCycle != null && dayNightCycle.IsNight;
    }

    void ApplyAdvanceToSpawned(bool night)
    {
        bool advance = night && advanceToEnemyBuildings;

        for (int i = spawnedAIs.Count - 1; i >= 0; i--)
        {
            EnemyCombatAI enemyAI = spawnedAIs[i];
            if (enemyAI == null)
            {
                spawnedAIs.RemoveAt(i);
                continue;
            }

            enemyAI.SetAdvanceToEnemyBuildings(advance);
        }
    }

    bool HasAllyNearby()
    {
        if (SpatialQueryWorld.Instance != null)
        {
            return SpatialQueryWorld.Instance.HasOtherOwnerInRange(
                transform.position,
                enemyOwnerId,
                allyDetectRadius,
                detectUnitsOnly);
        }

        float radiusSqr = allyDetectRadius * allyDetectRadius;
        Vector3 origin = transform.position;

        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (entity == null)
                continue;

            if (entity.ownerId == enemyOwnerId)
                continue;

            if (detectUnitsOnly &&
                entity.entityType != SelectableEntityType.Unit)
                continue;

            EntityHealth entityHealth = entity.CachedHealth;

            if (entityHealth != null && !entityHealth.IsAlive)
                continue;

            Vector3 delta = entity.transform.position - origin;
            delta.y = 0f;

            if (delta.sqrMagnitude <= radiusSqr)
                return true;
        }

        return false;
    }

    void TrackAlive(EntityHealth spawnedHealth)
    {
        if (spawnedHealth == null)
            return;

        AliveCount++;

        void HandleSpawnedDied()
        {
            spawnedHealth.OnDied -= HandleSpawnedDied;
            AliveCount = Mathf.Max(0, AliveCount - 1);
        }

        spawnedHealth.OnDied += HandleSpawnedDied;
    }

    void OnDrawGizmosSelected()
    {
        // 아군 감지 반경(노랑)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, allyDetectRadius);

        // 스폰 반경(빨강)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
