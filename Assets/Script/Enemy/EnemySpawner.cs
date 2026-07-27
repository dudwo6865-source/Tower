using System.Collections.Generic;
using UnityEngine;

// 하이라키에 배치하는 스포너(건물형).
// - 근처에 아군(플레이어 소속) 유닛이 들어오면 일정 간격으로 적을 스폰한다.
// - 스포너 오브젝트가 파괴될 때 적을 한 번에 스폰한다.
// 낮/밤과 무관하게 조건이 맞을 때만 동작한다.
// (밤 웨이브 스폰은 WaveManager가 직접 담당하며, 이 컴포넌트와는 별개다.)
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

    [Header("Proximity Spawn")]
    [Tooltip("이 반경 안에 아군 유닛이 있으면 주기적으로 스폰합니다.")]
    public float allyDetectRadius = 15f;

    [Tooltip("근접 감지 대상을 유닛으로만 한정할지 여부입니다. 끄면 건물도 트리거가 됩니다.")]
    public bool detectUnitsOnly = true;

    [Tooltip("근접 조건이 충족된 동안의 스폰 간격(초)입니다.")]
    public float spawnInterval = 5f;

    [Tooltip("근접 조건 충족 시 한 번에 스폰할 적 수입니다.")]
    public int enemiesPerSpawn = 2;

    [Tooltip("이 스포너가 근접 스폰으로 동시에 유지할 최대 생존 적 수입니다. 0이면 무제한입니다.")]
    public int maxAliveEnemies = 0;

    [Header("Death Spawn")]
    [Tooltip("스포너가 파괴될 때 스폰할 적 수입니다. 0이면 파괴 시 스폰하지 않습니다.")]
    public int enemiesOnDeath = 5;

    [Header("Enemy Behavior")]
    [Tooltip("스폰된 적이 아군 건물/본부로 자동 진군할지 여부입니다.")]
    public bool advanceToEnemyBuildings = true;

    public int AliveCount { get; private set; }

    EntityHealth health;
    float spawnTimer;
    bool isDead;

    void Awake()
    {
        health = GetComponent<EntityHealth>();

        if (health != null)
            health.OnDied += HandleDied;
    }

    void OnDestroy()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    void Update()
    {
        if (isDead)
            return;

        if (!HasAllyNearby())
            return;

        if (maxAliveEnemies > 0 && AliveCount >= maxAliveEnemies)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer > 0f)
            return;

        spawnTimer = Mathf.Max(0.1f, spawnInterval);
        SpawnBurst(enemiesPerSpawn, respectAliveCap: true);
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

            EnemySpawnUtility.ApplyAdvanceToEnemyBuildings(
                enemyObject,
                advanceToEnemyBuildings);
        }
    }

    bool HasAllyNearby()
    {
        float radiusSqr = allyDetectRadius * allyDetectRadius;
        Vector3 origin = transform.position;

        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (entity == null)
                continue;

            // 이 스포너와 같은 소속(적)은 감지 대상이 아니다.
            if (entity.ownerId == enemyOwnerId)
                continue;

            if (detectUnitsOnly &&
                entity.entityType != SelectableEntityType.Unit)
                continue;

            EntityHealth entityHealth = entity.GetComponent<EntityHealth>();

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
