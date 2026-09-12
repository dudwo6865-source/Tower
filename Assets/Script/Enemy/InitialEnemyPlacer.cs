using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>게임 시작 시 맵에 미리 깔아둘 적 한 무리입니다.</summary>
[Serializable]
public class InitialEnemyGroup
{
    [Tooltip("배치할 적 프리팹입니다.")]
    public GameObject prefab;

    [Tooltip("무리 하나에 넣을 적 수입니다.")]
    public int countPerCluster = 3;

    [Tooltip("맵에 흩뿌릴 무리 수입니다. 2 이상이면 같은 구성의 무리를 여러 곳에 만듭니다.")]
    public int clusterCount = 1;

    [Tooltip("무리를 이 반경(미터) 안에 모아서 배치합니다. 0이면 한 지점에 몰립니다.")]
    public float clusterRadius = 6f;

    public int TotalCount => Mathf.Max(0, countPerCluster) * Mathf.Max(0, clusterCount);

    public InitialEnemyGroup()
    {
    }

    public InitialEnemyGroup(InitialEnemyGroup source)
    {
        if (source == null)
            return;

        prefab = source.prefab;
        countPerCluster = source.countPerCluster;
        clusterCount = source.clusterCount;
        clusterRadius = source.clusterRadius;
    }

    /// <summary>플레이 중 값을 만져도 원본 에셋이 더러워지지 않게 사본을 만듭니다.</summary>
    public static List<InitialEnemyGroup> CloneList(List<InitialEnemyGroup> source)
    {
        List<InitialEnemyGroup> clone = new List<InitialEnemyGroup>();

        if (source == null)
            return clone;

        foreach (InitialEnemyGroup group in source)
            clone.Add(new InitialEnemyGroup(group));

        return clone;
    }
}

// 게임 시작 시 맵 곳곳에 적을 미리 깔아둡니다.
// 스포너가 계속 뿜어내는 적과 달리, 여기서 놓는 적은 한 번만 배치되고 다시 채워지지 않습니다.
// 낮에 맵을 돌아다닐 때 마주치는 야생 몬스터 같은 역할입니다.
//
// 값은 스테이지(MapConfig)에서 관리하고 MapLoader가 주입합니다.
// 씬에 이 컴포넌트가 없으면 MapLoader가 자기 오브젝트에 하나 붙입니다.
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class InitialEnemyPlacer : MonoBehaviour
{
    public static InitialEnemyPlacer Instance { get; private set; }

    [Header("Enemies")]
    [Tooltip("게임 시작 시 맵에 배치할 적 무리 목록입니다.")]
    public List<InitialEnemyGroup> groups = new List<InitialEnemyGroup>();

    [Tooltip("배치되는 적의 소속 ID입니다. 플레이어와 달라야 적으로 인식됩니다.")]
    public int enemyOwnerId = 2;

    [Header("Placement")]
    [Tooltip("플레이어 본부와 최소 이 거리 이상 떨어진 곳에만 배치합니다.")]
    public float minDistanceFromHq = 25f;

    [Tooltip("맵 가장자리에서 안쪽으로 둘 여백(미터)입니다.")]
    public float mapEdgeMargin = 8f;

    [Tooltip("켜면 플레이어 시야 밖(안개 속)에 우선 배치합니다.")]
    public bool avoidPlayerVision = true;

    [Tooltip("무리 위치를 찾기 위한 최대 시도 횟수입니다.")]
    public int placementAttempts = 32;

    [Header("Player")]
    [Tooltip("본부를 찾을 때 사용하는 ownerId입니다.")]
    public int playerOwnerId = 1;

    [Header("Map Bounds")]
    [Tooltip("맵 범위를 어디서 얻을지입니다. 보통 Auto로 두면 baked NavMesh 범위를 씁니다.")]
    public MapPlayBoundsSource boundsSource = MapPlayBoundsSource.Auto;

    [Tooltip("boundsSource가 Manual일 때 쓰는 맵 원점입니다.")]
    public Vector3 manualBoundsOrigin = Vector3.zero;

    [Tooltip("boundsSource가 Manual일 때 쓰는 맵 크기(X=가로, Y=세로)입니다.")]
    public Vector2 manualBoundsSize = new Vector2(256f, 256f);

    [Header("Debug")]
    [Tooltip("몇 마리를 어디에 배치했는지 콘솔에 남깁니다.")]
    public bool logPlacement = true;

    /// <summary>실제로 배치된 적 수입니다.</summary>
    public int PlacedCount { get; private set; }

    /// <summary>설정상 배치하려던 총 마리 수입니다.</summary>
    public int RequestedCount => GetRequestedCount();

    bool placed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 컴포넌트만 지운다. 같은 오브젝트의 다른 매니저까지 날리면 안 된다.
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

    void Start()
    {
        if (Instance != this)
            return;

        StartCoroutine(PlaceRoutine());
    }

    IEnumerator PlaceRoutine()
    {
        // 맵 인스턴스화와 NavMesh 등록, 씬 건물의 격자 점유가 끝난 다음에 배치한다.
        yield return null;

        PlaceAll();
    }

    [ContextMenu("지금 배치 (테스트)")]
    public void PlaceAll()
    {
        if (placed)
            return;

        placed = true;
        PlacedCount = 0;

        int requested = GetRequestedCount();

        if (requested <= 0)
            return;

        if (!MapPlayBounds.TryResolve(
                boundsSource,
                manualBoundsOrigin,
                manualBoundsSize,
                out MapPlayBoundsData bounds))
        {
            Debug.LogWarning(
                "InitialEnemyPlacer: 맵 범위를 찾지 못해 초기 적 배치를 건너뜁니다.",
                this);
            return;
        }

        // 시야 밖에 놓으려면 지금 시야가 최신이어야 한다.
        if (avoidPlayerVision)
            FogOfWarManager.Instance?.RefreshVisionNow();

        Vector3 hqPosition = FindPlayerHeadquartersPosition();

        foreach (InitialEnemyGroup group in groups)
        {
            if (group == null || group.prefab == null)
                continue;

            int clusters = Mathf.Max(0, group.clusterCount);

            for (int i = 0; i < clusters; i++)
                PlaceCluster(group, bounds, hqPosition);
        }

        if (!logPlacement)
            return;

        Debug.Log(
            $"InitialEnemyPlacer: 초기 적 {PlacedCount}/{requested}마리를 맵에 배치했습니다.",
            this);
    }

    void PlaceCluster(
        InitialEnemyGroup group,
        MapPlayBoundsData bounds,
        Vector3 hqPosition)
    {
        if (!TryFindClusterCenter(bounds, hqPosition, out Vector3 center))
        {
            if (logPlacement)
            {
                Debug.LogWarning(
                    $"InitialEnemyPlacer: '{group.prefab.name}' 무리를 놓을 자리를 찾지 못했습니다. " +
                    "본부와의 최소 거리나 가장자리 여백을 줄여보세요.",
                    this);
            }

            return;
        }

        // 초기 배치 적도 현재 웨이브의 스탯 가중치를 따른다.
        WaveTuning tuning = WaveManager.Instance != null
            ? WaveManager.Instance.CurrentTuning
            : null;

        float health = tuning != null ? tuning.healthMultiplier : 1f;
        float damage = tuning != null ? tuning.damageMultiplier : 1f;
        float speed = tuning != null ? tuning.speedMultiplier : 1f;

        int count = Mathf.Max(0, group.countPerCluster);
        float radius = Mathf.Max(0f, group.clusterRadius);

        for (int i = 0; i < count; i++)
        {
            Vector3 position = radius > 0f
                ? EnemySpawnUtility.GetRandomPositionInRadius(
                    center,
                    radius,
                    avoidPlayerVision: false,
                    attempts: 8)
                : center;

            GameObject enemy = EnemySpawnUtility.SpawnEnemy(
                group.prefab,
                position,
                group.prefab.transform.rotation,
                enemyOwnerId,
                health,
                damage,
                speed);

            if (enemy == null)
                continue;

            PlacedCount++;
        }
    }

    bool TryFindClusterCenter(
        MapPlayBoundsData bounds,
        Vector3 hqPosition,
        out Vector3 center)
    {
        center = Vector3.zero;

        float minX = bounds.Origin.x + mapEdgeMargin;
        float maxX = bounds.Origin.x + bounds.Width - mapEdgeMargin;
        float minZ = bounds.Origin.z + mapEdgeMargin;
        float maxZ = bounds.Origin.z + bounds.Length - mapEdgeMargin;

        if (maxX <= minX || maxZ <= minZ)
            return false;

        float minDistanceSqr = minDistanceFromHq * minDistanceFromHq;
        int attempts = Mathf.Max(1, placementAttempts);

        // 시야 밖 조건까지 붙으면 자리 찾기가 훨씬 까다로워지므로 시도를 늘린다.
        if (avoidPlayerVision)
            attempts *= 4;

        Vector3 fallback = Vector3.zero;
        bool hasFallback = false;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float x = UnityEngine.Random.Range(minX, maxX);
            float z = UnityEngine.Random.Range(minZ, maxZ);

            if (!UnitSpawnUtility.TrySampleTopmostAtXZ(x, z, out Vector3 sampled))
                continue;

            if ((sampled - hqPosition).sqrMagnitude < minDistanceSqr)
                continue;

            // 시야 조건만 못 맞춘 자리는 예비로 들고 있는다.
            // 맵이 전부 밝혀져 있어도 아예 배치를 포기하지는 않게 한다.
            if (!hasFallback)
            {
                fallback = sampled;
                hasFallback = true;
            }

            if (avoidPlayerVision &&
                EnemySpawnUtility.IsVisibleToLocalPlayer(sampled))
            {
                continue;
            }

            center = sampled;
            return true;
        }

        if (!hasFallback)
            return false;

        center = fallback;
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

        return hasFallback ? fallback : Vector3.zero;
    }

    int GetRequestedCount()
    {
        if (groups == null)
            return 0;

        int total = 0;

        foreach (InitialEnemyGroup group in groups)
        {
            if (group == null || group.prefab == null)
                continue;

            total += group.TotalCount;
        }

        return total;
    }
}
