using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public DayNightCycle dayNightCycle;

    [Header("Enemy Buildings")]
    [Tooltip("밤마다 생성할 적 건물(스포너) 프리팹 목록입니다. 여러 개면 생성마다 무작위로 선택합니다.")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();

    [Tooltip("생성되는 적 건물의 소속 ID입니다. 플레이어와 달라야 적으로 인식됩니다.")]
    public int enemyOwnerId = 2;

    [Header("Player")]
    [Tooltip("플레이어 본부를 찾을 때 사용하는 ownerId입니다.")]
    public int playerOwnerId = 1;

    [Header("Initial Scatter (Day Start)")]
    [Tooltip("초기 배치에 사용할 적 건물 프리팹 목록입니다. 비워두면 위의 Enemy Buildings 프리팹을 사용합니다.")]
    public List<GameObject> initialEnemyPrefabs = new List<GameObject>();

    [Tooltip("게임 시작 시 맵에 미리 배치할 적 스포너 수입니다.")]
    public int initialEnemyCount = 0;

    [Tooltip("본부와 최소 이 거리 이상 떨어진 곳에만 배치합니다.")]
    public float initialMinDistanceFromHq = 25f;

    [Tooltip("맵 가장자리에서 안쪽으로 둘 여백입니다.")]
    public float mapEdgeMargin = 8f;

    [Tooltip("랜덤 위치 샘플 최대 시도 횟수입니다.")]
    public int randomPositionAttempts = 32;

    [Header("Map Bounds")]
    [Tooltip("NavMesh 바운드를 못 찾을 때만 사용하는 폴백입니다. 스폰은 기본적으로 baked NavMesh 범위를 맵 바운드로 씁니다.")]
    public MapPlayBoundsSource boundsSource = MapPlayBoundsSource.MapGrid;

    [Tooltip("boundsSource가 Manual일 때 사용하는 맵 원점입니다.")]
    public Vector3 manualBoundsOrigin = Vector3.zero;

    [Tooltip("boundsSource가 Manual일 때 사용하는 맵 크기(X=가로, Y=세로)입니다.")]
    public Vector2 manualBoundsSize = new Vector2(256f, 256f);

    [Header("Night Spawn")]
    [Tooltip("밤이 시작된 뒤 스포너 생성까지 대기 시간(초)입니다.")]
    public float nightWaveStartDelay = 3f;

    [Tooltip("밤마다 생성할 스포너 수입니다. 인덱스 0=1번째 밤. 이후 밤을 지정하지 않으면 마지막 값을 계속 사용합니다.")]
    public List<int> spawnersPerNight = new List<int> { 1, 2 };

    [Tooltip("밤 스포너를 본부와 최소 이 거리 이상 떨어진 곳에 배치합니다.")]
    public float nightWaveMinDistanceFromHq = 20f;

    [Tooltip("켜면 밤 스포너를 플레이어 시야 밖(안개 속)에 우선 배치합니다.")]
    public bool nightWaveAvoidPlayerVision = true;

    public int GlobalWaveIndex { get; private set; }

    public int CurrentNightWave { get; private set; }

    // 완료한 밤 주기 수. 0=첫 밤.
    public int NightCycleIndex { get; private set; }

    public int TotalAliveEnemies => aliveCount;

    int aliveCount;

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
        StartCoroutine(StartRoutine());
    }

    IEnumerator StartRoutine()
    {
        // 씬에 미리 놓인 건물이 격자를 점유한 뒤에 스포너를 배치한다.
        yield return null;

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
    }

    void TrackAlive(EntityHealth health)
    {
        if (health == null)
            return;

        aliveCount++;

        void HandleDied()
        {
            health.OnDied -= HandleDied;
            aliveCount = Mathf.Max(0, aliveCount - 1);
        }

        health.OnDied += HandleDied;
    }

    void HandlePhaseStarted(DayNightPhase phase)
    {
        if (phase == DayNightPhase.Day)
        {
            StopNightWaves();

            NightCycleIndex++;
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

        int spawned = SpawnBuildings(
            initialEnemyCount,
            initialMinDistanceFromHq,
            avoidPlayerVision: true,
            GetInitialEnemyPrefab);

        initialScatterDone = true;

        Debug.Log(
            $"WaveManager: 초기 적 스포너 {spawned}/{initialEnemyCount}개를 맵에 배치했습니다.");
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

        if (!IsNightActive())
            yield break;

        CurrentNightWave = 1;
        GlobalWaveIndex++;
        OnNightWaveStarted?.Invoke(CurrentNightWave);
        SpawnNightSpawners();
    }

    bool IsNightActive()
    {
        if (dayNightCycle == null)
            return false;

        return dayNightCycle.IsNight;
    }

    void SpawnNightSpawners()
    {
        int count = GetSpawnersForNight(NightCycleIndex);

        int spawned = SpawnBuildings(
            count,
            nightWaveMinDistanceFromHq,
            nightWaveAvoidPlayerVision,
            GetNightWaveEnemyPrefab);

        Debug.Log(
            $"WaveManager: 밤#{NightCycleIndex + 1} — 스포너 {spawned}/{count}개 생성");
    }

    int SpawnBuildings(
        int count,
        float minDistanceFromHq,
        bool avoidPlayerVision,
        Func<GameObject> prefabSelector)
    {
        if (count <= 0 || prefabSelector == null || !HasAnyEnemyPrefab())
            return 0;

        if (!TryGetMapBounds(out MapPlayBoundsData bounds))
        {
            Debug.LogWarning("WaveManager: 맵 경계를 찾지 못해 적 건물 배치를 건너뜁니다.");
            return 0;
        }

        FogOfWarManager.Instance?.RefreshVisionNow();

        Vector3 hqPosition = FindPlayerHeadquartersPosition();
        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = prefabSelector();

            if (prefab == null)
                continue;

            Vector2Int footprintCells = EnemySpawnUtility.ResolveBuildingFootprint(prefab);

            if (!TryGetRandomBuildingPlacement(
                    bounds,
                    hqPosition,
                    minDistanceFromHq,
                    mapEdgeMargin,
                    avoidPlayerVision,
                    footprintCells,
                    out Vector2Int originCell,
                    out Vector3 position))
            {
                continue;
            }

            GameObject buildingObject = EnemySpawnUtility.SpawnEnemyBuilding(
                prefab,
                position,
                prefab.transform.rotation,
                originCell,
                enemyOwnerId,
                playerOwnerId,
                1f,
                TrackAlive);

            if (buildingObject == null)
                continue;

            spawned++;
        }

        return spawned;
    }

    public int GetSpawnersForNight(int nightCycleIndex)
    {
        if (spawnersPerNight == null || spawnersPerNight.Count == 0)
            return 0;

        int index = Mathf.Clamp(nightCycleIndex, 0, spawnersPerNight.Count - 1);
        return Mathf.Max(0, spawnersPerNight[index]);
    }

    bool HasAnyEnemyPrefab()
    {
        if (enemyPrefabs != null && enemyPrefabs.Count > 0)
            return true;

        return initialEnemyPrefabs != null && initialEnemyPrefabs.Count > 0;
    }

    GameObject GetNightWaveEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
            return GetInitialEnemyPrefab();

        return enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Count)];
    }

    GameObject GetInitialEnemyPrefab()
    {
        List<GameObject> source =
            (initialEnemyPrefabs != null && initialEnemyPrefabs.Count > 0)
                ? initialEnemyPrefabs
                : enemyPrefabs;

        if (source == null || source.Count == 0)
            return null;

        return source[UnityEngine.Random.Range(0, source.Count)];
    }

    bool TryGetMapBounds(out MapPlayBoundsData bounds)
    {
        if (TryGetNavMeshMapBounds(out bounds))
            return true;

        return MapPlayBounds.TryResolve(
            boundsSource,
            manualBoundsOrigin,
            manualBoundsSize,
            out bounds);
    }

    static bool TryGetNavMeshMapBounds(out MapPlayBoundsData bounds)
    {
        bounds = default;

        MapGrid mapGrid = MapGrid.Instance;

        if (mapGrid == null)
            mapGrid = UnityEngine.Object.FindObjectOfType<MapGrid>();

        if (mapGrid == null)
            return false;

        mapGrid.Refresh();

        if (!mapGrid.IsNavMeshBoundsActive ||
            mapGrid.CellCountX <= 0 ||
            mapGrid.CellCountZ <= 0)
        {
            return false;
        }

        bounds.IsValid = true;
        bounds.Origin = mapGrid.MapOrigin;
        bounds.Width = mapGrid.MapSize.x;
        bounds.Length = mapGrid.MapSize.y;
        return true;
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

    bool TryGetRandomBuildingPlacement(
        MapPlayBoundsData bounds,
        Vector3 avoidCenter,
        float minDistanceFromAvoid,
        float edgeMargin,
        bool avoidPlayerVision,
        Vector2Int footprintCells,
        out Vector2Int originCell,
        out Vector3 position)
    {
        originCell = default;
        position = Vector3.zero;

        MapGrid mapGrid = MapGrid.Instance;
        float cellSize = mapGrid != null ? mapGrid.cellSize : 2f;
        float footprintMargin =
            Mathf.Max(footprintCells.x, footprintCells.y) * cellSize * 0.5f;
        float margin = edgeMargin + footprintMargin;

        float minX = bounds.Origin.x + margin;
        float maxX = bounds.Origin.x + bounds.Width - margin;
        float minZ = bounds.Origin.z + margin;
        float maxZ = bounds.Origin.z + bounds.Length - margin;

        if (maxX <= minX || maxZ <= minZ)
            return false;

        float minDistanceSqr = minDistanceFromAvoid * minDistanceFromAvoid;
        int attempts = Mathf.Max(1, randomPositionAttempts);

        if (avoidPlayerVision)
            attempts = Mathf.Max(attempts, randomPositionAttempts * 4);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float x = UnityEngine.Random.Range(minX, maxX);
            float z = UnityEngine.Random.Range(minZ, maxZ);

            if (!UnitSpawnUtility.TrySampleTopmostAtXZ(x, z, out Vector3 sampled))
                continue;

            if (mapGrid != null)
            {
                if (!mapGrid.TryGetSnappedFootprintPlacement(
                        sampled,
                        footprintCells,
                        out Vector2Int candidateOrigin,
                        out Vector3 center))
                {
                    continue;
                }

                if (GridOccupancy.Instance != null &&
                    !GridOccupancy.Instance.CanOccupy(
                        candidateOrigin,
                        footprintCells,
                        center.y))
                {
                    continue;
                }

                if ((center - avoidCenter).sqrMagnitude < minDistanceSqr)
                    continue;

                if (avoidPlayerVision &&
                    EnemySpawnUtility.IsFootprintVisibleToLocalPlayer(
                        center,
                        footprintCells))
                {
                    continue;
                }

                originCell = candidateOrigin;
                position = center;
                return true;
            }

            if ((sampled - avoidCenter).sqrMagnitude < minDistanceSqr)
                continue;

            if (avoidPlayerVision &&
                EnemySpawnUtility.IsVisibleToLocalPlayer(sampled))
            {
                continue;
            }

            originCell = default;
            position = sampled;
            return true;
        }

        return false;
    }
}
