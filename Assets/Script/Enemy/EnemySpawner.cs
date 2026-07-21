using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("WaveManager가 없을 때만 사용합니다. WaveManager가 있으면 자동으로 꺼집니다.")]
    public bool autoStart = true;

    [Tooltip("스폰을 시작하기 전 대기 시간(초)입니다.")]
    public float startDelay = 2f;

    [Tooltip("한 번의 스폰 주기 사이 간격(초)입니다.")]
    public float spawnInterval = 3f;

    [Tooltip("한 번의 스폰 주기에 생성할 적 수(기본값)입니다.")]
    public int enemiesPerSpawn = 1;

    [Header("Limits")]
    [Tooltip("동시에 살아있을 수 있는 최대 적 수입니다. 0이면 무제한입니다.")]
    public int maxAliveEnemies = 0;

    [Tooltip("게임 전체에서 생성할 수 있는 적 최대 수입니다. 0이면 무제한입니다.")]
    public int maxTotalSpawns = 0;

    [Header("Difficulty Scaling")]
    [Tooltip("웨이브/사이클마다 한 번에 추가로 생성할 적 수입니다.")]
    public int enemiesSpawnGrowthPerCycle = 0;

    [Tooltip("한 번의 스폰 주기에 생성 가능한 최대 적 수입니다. 0이면 무제한입니다.")]
    public int maxEnemiesPerSpawn = 0;

    [Tooltip("웨이브/사이클마다 누적되는 체력 증가 비율입니다.")]
    public float healthGrowthPerCycle = 0.08f;

    [Tooltip("웨이브/사이클마다 누적되는 공격력 증가 비율입니다.")]
    public float damageGrowthPerCycle = 0.05f;

    [Tooltip("웨이브/사이클마다 누적되는 이동 속도 증가 비율입니다.")]
    public float speedGrowthPerCycle = 0f;

    [Tooltip("이동 속도 배율의 상한입니다.")]
    public float maxSpeedMultiplier = 2f;

    public int AliveCount { get; private set; }
    public int CycleCount { get; private set; }
    public int TotalSpawnedCount { get; private set; }

    public bool HasReachedTotalSpawnLimit =>
        maxTotalSpawns > 0 && TotalSpawnedCount >= maxTotalSpawns;

    public event System.Action<int> OnAliveCountChanged;
    public event System.Action<int> OnCycleStarted;

    Coroutine spawnRoutine;

    void Start()
    {
        if (WaveManager.Instance != null)
            return;

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

    public int SpawnWaveBurst(int count, int waveIndex, bool? advanceToEnemyBuildings = null)
    {
        if (enemyPrefabs.Count == 0 || count <= 0)
            return 0;

        if (HasReachedTotalSpawnLimit)
            return 0;

        GetWaveMultipliers(
            waveIndex,
            out float healthMultiplier,
            out float damageMultiplier,
            out float speedMultiplier);

        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            if (maxAliveEnemies > 0 && AliveCount >= maxAliveEnemies)
                break;

            if (HasReachedTotalSpawnLimit)
                break;

            if (SpawnOne(
                    healthMultiplier,
                    damageMultiplier,
                    speedMultiplier,
                    advanceToEnemyBuildings))
            {
                TotalSpawnedCount++;
                spawned++;
            }
        }

        if (spawned > 0)
        {
            CycleCount++;
            OnCycleStarted?.Invoke(CycleCount);
        }

        return spawned;
    }

    public GameObject SpawnAtPosition(
        Vector3 position,
        int waveIndex,
        bool? advanceToEnemyBuildings = null)
    {
        return SpawnAtPosition(null, position, waveIndex, advanceToEnemyBuildings);
    }

    // prefabOverride가 지정되면 그 프리팹을, 아니면 enemyPrefabs에서 무작위로 스폰한다.
    public GameObject SpawnAtPosition(
        GameObject prefabOverride,
        Vector3 position,
        int waveIndex,
        bool? advanceToEnemyBuildings = null)
    {
        if (HasReachedTotalSpawnLimit)
            return null;

        if (maxAliveEnemies > 0 && AliveCount >= maxAliveEnemies)
            return null;

        GameObject prefab = prefabOverride != null
            ? prefabOverride
            : (enemyPrefabs.Count > 0
                ? enemyPrefabs[Random.Range(0, enemyPrefabs.Count)]
                : null);

        if (prefab == null)
            return null;

        GetWaveMultipliers(
            waveIndex,
            out float healthMultiplier,
            out float damageMultiplier,
            out float speedMultiplier);

        GameObject enemyObject = EnemySpawnUtility.SpawnEnemy(
            prefab,
            position,
            prefab.transform.rotation,
            enemyOwnerId,
            healthMultiplier,
            damageMultiplier,
            speedMultiplier,
            TrackAlive);

        if (enemyObject == null)
            return null;

        if (advanceToEnemyBuildings.HasValue)
            EnemySpawnUtility.ApplyAdvanceToEnemyBuildings(
                enemyObject,
                advanceToEnemyBuildings.Value);

        TotalSpawnedCount++;
        return enemyObject;
    }

    public void GetWaveMultipliers(
        int waveIndex,
        out float healthMultiplier,
        out float damageMultiplier,
        out float speedMultiplier)
    {
        healthMultiplier = 1f + healthGrowthPerCycle * waveIndex;
        damageMultiplier = 1f + damageGrowthPerCycle * waveIndex;

        // 속도 배율은 기본 1에서 시작해 증가만 하는 값이다.
        // maxSpeedMultiplier가 1 미만(예: 0)으로 잘못 설정돼도 속도가 0이 되어
        // 스폰된 적이 아예 움직이지 못하는 것을 막기 위해 하한을 1로 둔다.
        float speedCap = Mathf.Max(1f, maxSpeedMultiplier);
        speedMultiplier =
            Mathf.Min(1f + speedGrowthPerCycle * waveIndex, speedCap);
    }

    public Vector3 GetSpawnPosition()
    {
        Vector3 basePosition = spawnPoints.Count > 0
            ? spawnPoints[Random.Range(0, spawnPoints.Count)].position
            : transform.position;

        return EnemySpawnUtility.SampleNavMeshPosition(basePosition);
    }

    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            if (HasReachedTotalSpawnLimit)
            {
                spawnRoutine = null;
                yield break;
            }

            SpawnCycle();
            CycleCount++;
            OnCycleStarted?.Invoke(CycleCount);

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    int GetEnemiesToSpawnThisCycle()
    {
        long scaledCount = (long)enemiesPerSpawn +
            (long)enemiesSpawnGrowthPerCycle * CycleCount;

        int count = scaledCount > int.MaxValue
            ? int.MaxValue
            : Mathf.Max(0, (int)scaledCount);

        if (maxEnemiesPerSpawn > 0)
            count = Mathf.Min(count, maxEnemiesPerSpawn);

        return count;
    }

    void SpawnCycle()
    {
        GetWaveMultipliers(
            CycleCount,
            out float healthMultiplier,
            out float damageMultiplier,
            out float speedMultiplier);

        int spawnCount = GetEnemiesToSpawnThisCycle();

        if (maxTotalSpawns > 0)
        {
            int remaining = maxTotalSpawns - TotalSpawnedCount;
            spawnCount = Mathf.Min(spawnCount, remaining);
        }

        for (int i = 0; i < spawnCount; i++)
        {
            if (maxAliveEnemies > 0 && AliveCount >= maxAliveEnemies)
                return;

            if (HasReachedTotalSpawnLimit)
                return;

            if (SpawnOne(healthMultiplier, damageMultiplier, speedMultiplier))
                TotalSpawnedCount++;
        }
    }

    bool SpawnOne(
        float healthMultiplier,
        float damageMultiplier,
        float speedMultiplier,
        bool? advanceToEnemyBuildings = null)
    {
        return SpawnOneAt(
            GetSpawnPosition(),
            healthMultiplier,
            damageMultiplier,
            speedMultiplier,
            advanceToEnemyBuildings);
    }

    bool SpawnOneAt(
        Vector3 position,
        float healthMultiplier,
        float damageMultiplier,
        float speedMultiplier,
        bool? advanceToEnemyBuildings = null)
    {
        GameObject prefab =
            enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];

        if (prefab == null)
            return false;

        GameObject enemyObject = EnemySpawnUtility.SpawnEnemy(
            prefab,
            position,
            prefab.transform.rotation,
            enemyOwnerId,
            healthMultiplier,
            damageMultiplier,
            speedMultiplier,
            TrackAlive);

        if (enemyObject == null)
            return false;

        if (advanceToEnemyBuildings.HasValue)
            EnemySpawnUtility.ApplyAdvanceToEnemyBuildings(
                enemyObject,
                advanceToEnemyBuildings.Value);

        return true;
    }

    void TrackAlive(EntityHealth health)
    {
        if (health == null)
            return;

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
