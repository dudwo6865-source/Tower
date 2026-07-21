using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public DayNightCycle dayNightCycle;

    [Tooltip("비워두면 씬의 모든 EnemySpawner를 사용합니다.")]
    public EnemySpawner[] spawners;

    [Header("Player")]
    [Tooltip("플레이어 본부를 찾을 때 사용하는 ownerId입니다.")]
    public int playerOwnerId = 1;

    [Header("Initial Scatter (Day Start)")]
    [Tooltip("초기 배치에 사용할 적 프리팹 목록입니다. 비워두면 스포너의 enemyPrefabs를 사용합니다. 여러 개면 배치마다 무작위로 선택합니다.")]
    public List<GameObject> initialEnemyPrefabs = new List<GameObject>();

    [Tooltip("게임 시작 시 맵에 미리 배치할 적 수입니다.")]
    public int initialEnemyCount = 12;

    [Tooltip("초기 배치에 사용할 웨이브 난이도 인덱스입니다.")]
    public int initialWaveIndex = 0;

    [Tooltip("초기 배치 적이 아군 건물/본부로 자동 진군할지 여부입니다.")]
    public bool initialEnemiesAdvanceToBase = false;

    [Tooltip("본부와 최소 이 거리 이상 떨어진 곳에만 배치합니다.")]
    public float initialMinDistanceFromHq = 25f;

    [Tooltip("맵 가장자리에서 안쪽으로 둘 여백입니다.")]
    public float mapEdgeMargin = 8f;

    [Tooltip("랜덤 위치 샘플 최대 시도 횟수입니다.")]
    public int randomPositionAttempts = 32;

    [Header("Map Bounds")]
    public MapPlayBoundsSource boundsSource = MapPlayBoundsSource.Auto;

    public Vector3 manualBoundsOrigin = Vector3.zero;

    public Vector2 manualBoundsSize = new Vector2(256f, 256f);

    [Header("Night Waves")]
    [Tooltip("밤이 시작된 뒤 첫 웨이브까지 대기 시간(초)입니다.")]
    public float nightWaveStartDelay = 3f;

    [Tooltip("웨이브 사이 간격(초)입니다. maxWavesPerNight가 1 이상이면 밤 길이 ÷ maxWavesPerNight 값으로 자동 조정됩니다.")]
    public float waveInterval = 12f;

    [Tooltip("각 스포너가 웨이브마다 생성할 기본 적 수입니다. 0이면 스포너의 enemiesPerSpawn을 사용합니다.")]
    public int enemiesPerSpawnerPerWave = 0;

    [Tooltip("밤 동안 진행할 최대 웨이브 수입니다. 0이면 밤이 끝날 때까지 계속합니다.")]
    public int maxWavesPerNight = 0;

    public int GlobalWaveIndex { get; private set; }

    public int CurrentNightWave { get; private set; }

    public int TotalAliveEnemies { get; private set; }

    public event Action<int> OnNightWaveStarted;

    Coroutine nightWaveRoutine;
    bool initialScatterDone;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        TakeOverSpawners();
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

        StopNightWaves();
    }

    void Start()
    {
        SubscribeSpawnerAliveEvents();

        if (!initialScatterDone)
            ScatterInitialEnemies();

        if (dayNightCycle != null &&
            dayNightCycle.IsNight &&
            nightWaveRoutine == null)
        {
            StartNightWaves();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void ResolveReferences()
    {
        if (dayNightCycle == null)
            dayNightCycle = FindObjectOfType<DayNightCycle>();

        if (spawners == null || spawners.Length == 0)
            spawners = FindObjectsOfType<EnemySpawner>();
    }

    void TakeOverSpawners()
    {
        if (spawners == null)
            return;

        foreach (EnemySpawner spawner in spawners)
        {
            if (spawner == null)
                continue;

            spawner.autoStart = false;
            spawner.StopSpawning();
        }
    }

    void SubscribeSpawnerAliveEvents()
    {
        if (spawners == null)
            return;

        foreach (EnemySpawner spawner in spawners)
        {
            if (spawner == null)
                continue;

            spawner.OnAliveCountChanged -= HandleSpawnerAliveChanged;
            spawner.OnAliveCountChanged += HandleSpawnerAliveChanged;
        }

        RefreshTotalAliveCount();
    }

    void HandleSpawnerAliveChanged(int aliveCount)
    {
        RefreshTotalAliveCount();
    }

    void RefreshTotalAliveCount()
    {
        int total = 0;

        if (spawners != null)
        {
            foreach (EnemySpawner spawner in spawners)
            {
                if (spawner != null)
                    total += spawner.AliveCount;
            }
        }

        TotalAliveEnemies = total;
    }

    void HandlePhaseStarted(DayNightPhase phase)
    {
        if (phase == DayNightPhase.Day)
        {
            StopNightWaves();
            return;
        }

        StartNightWaves();
    }

    public void ScatterInitialEnemies()
    {
        if (initialEnemyCount <= 0)
        {
            initialScatterDone = true;
            return;
        }

        EnemySpawner sourceSpawner = GetPrimarySpawner();

        if (sourceSpawner == null)
        {
            Debug.LogWarning("WaveManager: 초기 적 배치를 위한 EnemySpawner가 없습니다.");
            initialScatterDone = true;
            return;
        }

        if (!TryGetMapBounds(out MapPlayBoundsData bounds))
        {
            Debug.LogWarning("WaveManager: 맵 경계를 찾지 못해 초기 적 배치를 건너뜁니다.");
            initialScatterDone = true;
            return;
        }

        Vector3 hqPosition = FindPlayerHeadquartersPosition();
        int spawned = 0;

        for (int i = 0; i < initialEnemyCount; i++)
        {
            if (!TryGetRandomMapPosition(
                    bounds,
                    hqPosition,
                    initialMinDistanceFromHq,
                    mapEdgeMargin,
                    out Vector3 position))
            {
                continue;
            }

            GameObject enemyObject = sourceSpawner.SpawnAtPosition(
                GetInitialEnemyPrefab(),
                position,
                initialWaveIndex,
                initialEnemiesAdvanceToBase);

            if (enemyObject != null)
                spawned++;
        }

        initialScatterDone = true;
        RefreshTotalAliveCount();

        Debug.Log($"WaveManager: 초기 적 {spawned}/{initialEnemyCount}마리를 맵에 배치했습니다.");
    }

    void StartNightWaves()
    {
        StopNightWaves();
        CurrentNightWave = 0;
        nightWaveRoutine = StartCoroutine(NightWaveLoop());
    }

    void StopNightWaves()
    {
        if (nightWaveRoutine == null)
            return;

        StopCoroutine(nightWaveRoutine);
        nightWaveRoutine = null;
    }

    IEnumerator NightWaveLoop()
    {
        if (nightWaveStartDelay > 0f)
            yield return new WaitForSeconds(nightWaveStartDelay);

        while (IsNightActive())
        {
            CurrentNightWave++;
            GlobalWaveIndex++;
            OnNightWaveStarted?.Invoke(CurrentNightWave);

            SpawnNightWave(GlobalWaveIndex);

            if (maxWavesPerNight > 0 && CurrentNightWave >= maxWavesPerNight)
                yield break;

            float interval = GetEffectiveWaveInterval();

            if (interval <= 0f)
                yield return null;
            else
                yield return new WaitForSeconds(interval);
        }
    }

    bool IsNightActive()
    {
        if (dayNightCycle == null)
            return false;

        return dayNightCycle.IsNight;
    }

    // maxWavesPerNight가 1 이상이면 밤 길이를 웨이브 수로 나눠 간격을 자동 계산한다.
    // (밤 동안 웨이브가 균등하게 퍼지도록.) 그 외에는 인스펙터의 waveInterval을 사용한다.
    float GetEffectiveWaveInterval()
    {
        if (maxWavesPerNight <= 0 || dayNightCycle == null)
            return waveInterval;

        float nightDuration = dayNightCycle.nightDuration;

        if (nightDuration <= 0f)
            return waveInterval;

        return nightDuration / maxWavesPerNight;
    }

    void SpawnNightWave(int waveIndex)
    {
        if (spawners == null || spawners.Length == 0)
            return;

        int totalSpawned = 0;

        foreach (EnemySpawner spawner in spawners)
        {
            if (spawner == null)
                continue;

            int count = enemiesPerSpawnerPerWave > 0
                ? enemiesPerSpawnerPerWave
                : spawner.enemiesPerSpawn;

            totalSpawned += spawner.SpawnWaveBurst(count, waveIndex, true);
        }

        RefreshTotalAliveCount();

        Debug.Log(
            $"WaveManager: 밤 웨이브 {CurrentNightWave} — {totalSpawned}마리 스폰 (난이도 {waveIndex})");
    }

    // 초기 배치에 사용할 프리팹을 고른다. 지정 목록이 비어 있으면 null을 반환해
    // 스포너의 enemyPrefabs에서 무작위로 스폰되도록 한다.
    GameObject GetInitialEnemyPrefab()
    {
        if (initialEnemyPrefabs == null || initialEnemyPrefabs.Count == 0)
            return null;

        GameObject prefab =
            initialEnemyPrefabs[UnityEngine.Random.Range(0, initialEnemyPrefabs.Count)];

        return prefab;
    }

    EnemySpawner GetPrimarySpawner()
    {
        if (spawners == null || spawners.Length == 0)
            return null;

        foreach (EnemySpawner spawner in spawners)
        {
            if (spawner != null && spawner.enemyPrefabs.Count > 0)
                return spawner;
        }

        return spawners[0];
    }

    bool TryGetMapBounds(out MapPlayBoundsData bounds)
    {
        return MapPlayBounds.TryResolve(
            boundsSource,
            manualBoundsOrigin,
            manualBoundsSize,
            out bounds);
    }

    Vector3 FindPlayerHeadquartersPosition()
    {
        Vector3 fallback = Vector3.zero;
        bool hasFallback = false;

        foreach (SelectableEntity building in BuildingRegistry.Buildings)
        {
            if (building == null || building.ownerId != playerOwnerId)
                continue;

            if (!hasFallback)
            {
                fallback = building.transform.position;
                hasFallback = true;
            }

            if (building.GetComponent<Headquarters>() != null)
                return building.transform.position;
        }

        if (hasFallback)
            return fallback;

        if (UnitSelectionManager.Instance != null)
            return UnitSelectionManager.Instance.transform.position;

        return Vector3.zero;
    }

    bool TryGetRandomMapPosition(
        MapPlayBoundsData bounds,
        Vector3 avoidCenter,
        float minDistanceFromAvoid,
        float edgeMargin,
        out Vector3 position)
    {
        position = Vector3.zero;

        float minX = bounds.Origin.x + edgeMargin;
        float maxX = bounds.Origin.x + bounds.Width - edgeMargin;
        float minZ = bounds.Origin.z + edgeMargin;
        float maxZ = bounds.Origin.z + bounds.Length - edgeMargin;

        if (maxX <= minX || maxZ <= minZ)
            return false;

        float minDistanceSqr = minDistanceFromAvoid * minDistanceFromAvoid;

        for (int attempt = 0; attempt < randomPositionAttempts; attempt++)
        {
            float x = UnityEngine.Random.Range(minX, maxX);
            float z = UnityEngine.Random.Range(minZ, maxZ);
            Vector3 candidate = new Vector3(x, bounds.Origin.y, z);

            if ((candidate - avoidCenter).sqrMagnitude < minDistanceSqr)
                continue;

            candidate.y = MapPlayBounds.SampleGroundHeight(candidate);

            if (NavMesh.SamplePosition(
                    candidate,
                    out NavMeshHit hit,
                    12f,
                    NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }
        }

        return false;
    }
}
