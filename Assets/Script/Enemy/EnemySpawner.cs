using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Target")]
    [Tooltip("스폰할 적 프리팹 목록입니다. 여러 개면 매 스폰마다 무작위로 선택합니다.")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();

    [Tooltip("스폰 위치 목록입니다. 비워두면 이 오브젝트의 위치에서 스폰합니다.")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Tooltip("스폰되는 적의 소속 ID입니다. 플레이어와 달라야 적으로 인식됩니다.")]
    public int enemyOwnerId = 2;

    [Header("Timing")]
    [Tooltip("시작 시 자동으로 스폰을 시작합니다.")]
    public bool autoStart = true;

    [Tooltip("스폰을 시작하기 전 대기 시간(초)입니다.")]
    public float startDelay = 2f;

    [Tooltip("한 번의 스폰 주기 사이 간격(초)입니다.")]
    public float spawnInterval = 3f;

    [Tooltip("한 번의 스폰 주기에 생성할 적 수입니다.")]
    public int enemiesPerSpawn = 1;

    [Header("Limits")]
    [Tooltip("동시에 살아있을 수 있는 최대 적 수입니다. 0이면 무제한입니다.")]
    public int maxAliveEnemies = 0;

    [Header("Difficulty Scaling")]
    [Tooltip("스폰 주기마다 누적되는 체력 증가 비율입니다. (예: 0.08 = 주기당 +8%)")]
    public float healthGrowthPerCycle = 0.08f;

    [Tooltip("스폰 주기마다 누적되는 공격력 증가 비율입니다.")]
    public float damageGrowthPerCycle = 0.05f;

    [Tooltip("스폰 주기마다 누적되는 이동 속도 증가 비율입니다.")]
    public float speedGrowthPerCycle = 0f;

    [Tooltip("이동 속도 배율의 상한입니다. NavMesh가 깨지지 않도록 제한합니다.")]
    public float maxSpeedMultiplier = 2f;

    public int AliveCount { get; private set; }
    public int CycleCount { get; private set; }

    public event System.Action<int> OnAliveCountChanged;
    public event System.Action<int> OnCycleStarted;

    private Coroutine spawnRoutine;

    void Start()
    {
        if (autoStart)
            StartSpawning();
    }

    [ContextMenu("Start Spawning")]
    public void StartSpawning()
    {
        if (spawnRoutine != null)
            return;

        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    [ContextMenu("Stop Spawning")]
    public void StopSpawning()
    {
        if (spawnRoutine == null)
            return;

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            SpawnCycle();
            CycleCount++;
            OnCycleStarted?.Invoke(CycleCount);

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnCycle()
    {
        if (enemyPrefabs.Count == 0)
            return;

        float healthMultiplier = 1f + healthGrowthPerCycle * CycleCount;
        float damageMultiplier = 1f + damageGrowthPerCycle * CycleCount;
        float speedMultiplier =
            Mathf.Min(1f + speedGrowthPerCycle * CycleCount, maxSpeedMultiplier);

        for (int i = 0; i < enemiesPerSpawn; i++)
        {
            if (maxAliveEnemies > 0 && AliveCount >= maxAliveEnemies)
                return;

            SpawnOne(healthMultiplier, damageMultiplier, speedMultiplier);
        }
    }

    void SpawnOne(
        float healthMultiplier,
        float damageMultiplier,
        float speedMultiplier)
    {
        GameObject prefab =
            enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];

        if (prefab == null)
            return;

        Vector3 position = GetSpawnPosition();

        GameObject enemy = Instantiate(prefab, position, prefab.transform.rotation);

        ConfigureEnemy(enemy, healthMultiplier, damageMultiplier, speedMultiplier);
    }

    Vector3 GetSpawnPosition()
    {
        Vector3 basePosition = spawnPoints.Count > 0
            ? spawnPoints[Random.Range(0, spawnPoints.Count)].position
            : transform.position;

        if (NavMesh.SamplePosition(basePosition, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            return hit.position;

        return basePosition;
    }

    void ConfigureEnemy(
        GameObject enemy,
        float healthMultiplier,
        float damageMultiplier,
        float speedMultiplier)
    {
        SelectableEntity selectable = enemy.GetComponent<SelectableEntity>();
        if (selectable != null)
            selectable.ownerId = enemyOwnerId;

        EntityHealth health = enemy.GetComponent<EntityHealth>();
        if (health != null)
        {
            health.SetMaxHealth(health.MaxHealth * healthMultiplier);
            TrackAlive(health);
        }

        UnitAttacker attacker = enemy.GetComponent<UnitAttacker>();
        if (attacker != null)
            attacker.attackDamage *= damageMultiplier;

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.speed *= speedMultiplier;
    }

    void TrackAlive(EntityHealth health)
    {
        AliveCount++;
        OnAliveCountChanged?.Invoke(AliveCount);

        void HandleDied()
        {
            health.OnDied -= HandleDied;
            AliveCount = Mathf.Max(0, AliveCount - 1);
            OnAliveCountChanged?.Invoke(AliveCount);
        }

        health.OnDied += HandleDied;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        if (spawnPoints.Count == 0)
        {
            Gizmos.DrawWireSphere(transform.position, 1f);
            return;
        }

        foreach (Transform point in spawnPoints)
        {
            if (point != null)
                Gizmos.DrawWireSphere(point.position, 1f);
        }
    }
}
