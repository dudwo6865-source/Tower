using System.Collections.Generic;
using UnityEngine;

// 하이라키에 배치하는 스포너(건물형).
// - 주기적으로 일정 수의 적을 스폰한다.
// - 근처에 아군이 있거나 공격받으면 같은 간격으로 추가 스폰한다.
// - 스포너가 파괴되면 적을 한 번에 스폰한다.
// - 스폰된 적은 낮에는 어그로만 반응하고, 밤이 되면 플레이어 HQ로 진군한다.
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
    [Tooltip("이 반경 안에 아군 유닛이 있으면 주기적으로 스폰합니다.")]
    public float allyDetectRadius = 15f;

    [Tooltip("근접 감지 대상을 유닛으로만 한정할지 여부입니다. 끄면 건물도 트리거가 됩니다.")]
    public bool detectUnitsOnly = true;

    [Header("Attacked Spawn")]
    [Tooltip("공격받은 뒤 이 시간(초) 동안은 근처에 아군이 없어도 같은 간격으로 스폰합니다. 이 시간 이상 추가 공격이 없으면 피격 스폰을 멈춥니다.")]
    public float attackedSpawnDuration = 8f;

    [Header("Death Spawn")]
    [Tooltip("스포너가 파괴될 때 스폰할 적 수입니다. 0이면 파괴 시 스폰하지 않습니다.")]
    public int enemiesOnDeath = 5;

    [Header("Enemy Behavior")]
    [Tooltip("밤이 되면 스폰된 적이 어그로에 적이 없을 때 플레이어 HQ로 진군합니다.")]
    public bool advanceToEnemyBuildings = true;

    public int AliveCount { get; private set; }

    readonly List<EnemyCombatAI> spawnedAIs = new List<EnemyCombatAI>();
    EntityHealth health;
    DayNightCycle dayNightCycle;
    float spawnTimer;
    float attackedTimer;
    bool isDead;

    void Awake()
    {
        health = GetComponent<EntityHealth>();

        if (health != null)
        {
            health.OnDied += HandleDied;
            health.OnDamaged += HandleDamaged;
        }

        if (spawnPeriodically)
            spawnTimer = Mathf.Max(0.1f, spawnInterval);
    }

    void OnEnable()
    {
        BindDayNightCycle();
    }

    void OnDisable()
    {
        UnbindDayNightCycle();
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
        if (isDead)
            return;

        if (attackedTimer > 0f)
            attackedTimer -= Time.deltaTime;

        bool shouldSpawn =
            spawnPeriodically ||
            HasAllyNearby() ||
            attackedTimer > 0f;

        if (!shouldSpawn)
            return;

        if (maxAliveEnemies > 0 && AliveCount >= maxAliveEnemies)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer > 0f)
            return;

        spawnTimer = Mathf.Max(0.1f, spawnInterval);
        SpawnBurst(enemiesPerSpawn, respectAliveCap: true);
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

        // 파괴 시 방출은 생존 상한을 무시하고 정해진 수만큼 모두 스폰한다.
        SpawnBurst(enemiesOnDeath, respectAliveCap: false);
    }

    void SpawnBurst(int count, bool respectAliveCap)
    {
        if (count <= 0 || enemyPrefabs.Count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            if (respectAliveCap &&
                maxAliveEnemies > 0 &&
                AliveCount >= maxAliveEnemies)
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

            GameObject enemyObject = EnemySpawnUtility.SpawnEnemy(
                prefab,
                position,
                prefab.transform.rotation,
                enemyOwnerId,
                1f,
                1f,
                1f,
                TrackAlive);

            if (enemyObject == null)
                continue;

            RegisterSpawnedEnemy(enemyObject);
        }
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
