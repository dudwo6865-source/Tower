using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-300)]
public class MapGrid : MonoBehaviour
{
    public static MapGrid Instance { get; private set; }

    [Header("Grid")]
    [Tooltip("한 칸의 월드 크기(미터)입니다.")]
    public float cellSize = 2f;

    [Header("Bounds")]
    [Tooltip("Bake된 NavMesh 이동 가능 영역 AABB를 맵 bounds로 사용합니다.")]
    public bool useNavMeshBounds = true;

    [Tooltip("footprint 모든 칸이 같은 층의 NavMesh 위에 있어야 합니다.")]
    public bool requireNavMeshForCells = true;

    [Tooltip("지면 스냅·층 탐색용 NavMesh 검색 반경(칸 크기 대비)입니다.")]
    [Range(0.1f, 1.5f)]
    public float navMeshSampleRadiusFactor = 0.45f;

    [Tooltip("칸 중심·모서리가 NavMesh에 얼마나 가까워야 건설 가능한지(칸 크기 대비)입니다. 작을수록 칸 전체가 길 위에 있어야 합니다.")]
    [Range(0.05f, 0.75f)]
    public float navMeshCellValidationRadiusFactor = 0.25f;

    [Tooltip("칸 모서리를 얼마나 안쪽에서 검사할지(칸 크기 대비)입니다. 0이면 진짜 모서리까지 NavMesh여야 합니다.")]
    [Range(0f, 0.45f)]
    public float navMeshCellSampleInsetFactor = 0.2f;

    [Tooltip("NavMesh 샘플 시 위에서 내려다볼 여유 높이(미터)입니다.")]
    public float navMeshSampleHeightOffset = 4f;

    [Tooltip("같은 층으로 볼 높이 허용 오차(미터)입니다. 건물 footprint의 모든 칸이 이 오차 안의 높이에 있어야 건설됩니다.")]
    public float navMeshFloorHeightTolerance = 2.5f;

    [Header("Building NavMesh")]
    [Tooltip("건물 footprint(격자) 대비 NavMeshObstacle Box 가로·세로 비율입니다. 1에 가까울수록 격자와 같습니다.")]
    [Range(0.1f, 1f)]
    public float navObstacleSizeScale = 0.85f;

    public int navMeshAreaMask = NavMesh.AllAreas;

    [Header("Manual Bounds Fallback")]
    [Tooltip("NavMesh를 못 찾을 때 사용할 맵 원점(왼쪽 아래)입니다.")]
    public Vector3 manualMapOrigin;

    [Tooltip("NavMesh를 못 찾을 때 사용할 맵 크기(X=가로, Y=세로)입니다.")]
    public Vector2 manualMapSize = new Vector2(256f, 256f);

    [Header("Debug")]
    public bool drawGridGizmos = true;

    [Tooltip("NavMesh 모드에서 Gizmo를 walkable 칸만 그립니다.")]
    public bool drawOnlyWalkableCellsInGizmos = true;

    public float CellSize => cellSize;

    public int CellCountX =>
        mapSize.x > 0f ? Mathf.FloorToInt(mapSize.x / cellSize) : 0;

    public int CellCountZ =>
        mapSize.y > 0f ? Mathf.FloorToInt(mapSize.y / cellSize) : 0;

    public Vector3 MapOrigin => mapOrigin;

    public Vector2 MapSize => mapSize;

    public bool UsesNavMesh => useNavMeshBounds;

    public bool IsNavMeshBoundsActive => navMeshBoundsActive;

    public float NavMeshMinY => navMeshMinY;

    public float NavMeshMaxY => navMeshMaxY;

    private Vector3 mapOrigin;
    private Vector2 mapSize;
    private float navMeshMinY;
    private float navMeshMaxY;
    private bool navMeshBoundsActive;
    private bool loggedNavMeshFailure;
    private CliffPainter cachedCliffPainter;
    private MapGridNavFloorCache navFloorCache;

    // 바운즈가 실제로 달라졌을 때만 칸 캐시를 무효화하기 위한 오차 허용치입니다.
    const float BoundsChangeEpsilon = 0.01f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        navFloorCache = new MapGridNavFloorCache(this);

        BuildingRegistry.OnBuildingRegistered += HandleBuildingRegistryChanged;
        BuildingRegistry.OnBuildingRemoved += HandleBuildingRegistryChanged;
    }

    void Start()
    {
        Refresh();
    }

    void OnEnable()
    {
        if (!Application.isPlaying || navMeshBoundsActive)
            return;

        Refresh();
    }

    void OnDestroy()
    {
        BuildingRegistry.OnBuildingRegistered -= HandleBuildingRegistryChanged;
        BuildingRegistry.OnBuildingRemoved -= HandleBuildingRegistryChanged;

        if (Instance == this)
            Instance = null;
    }

    // 건물이 놓이거나 철거되면 그 자리의 NavMesh가 carve되어 바뀌므로,
    // 다음 조회부터 다시 계산하도록 칸 높이 캐시를 통째로 비운다.
    void HandleBuildingRegistryChanged(SelectableEntity building)
    {
        navFloorCache?.Invalidate();
    }

    public void Refresh()
    {
        if (useNavMeshBounds && TryRefreshFromNavMesh())
        {
            loggedNavMeshFailure = false;
            return;
        }

        if (useNavMeshBounds && TryRefreshFromNavMeshSurfaces())
        {
            loggedNavMeshFailure = false;
            return;
        }

        if (TryRefreshFromManualBounds())
        {
            loggedNavMeshFailure = false;
            return;
        }

        if (!loggedNavMeshFailure)
        {
            Debug.LogError(
                "MapGrid: NavMesh 또는 Manual bounds를 찾을 수 없습니다. " +
                "Navigation(NavMeshSurface) Bake 또는 Manual Map Size를 설정하세요.");
            loggedNavMeshFailure = true;
        }
    }

    public bool TryRefreshFromNavMesh()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        if (triangulation.vertices == null || triangulation.vertices.Length == 0)
            return false;

        Vector3 min = triangulation.vertices[0];
        Vector3 max = triangulation.vertices[0];

        for (int i = 1; i < triangulation.vertices.Length; i++)
        {
            Vector3 vertex = triangulation.vertices[i];
            min = Vector3.Min(min, vertex);
            max = Vector3.Max(max, vertex);
        }

        ApplyNavMeshBounds(min, max);
        return true;
    }

    public bool TryRefreshFromNavMeshSurfaces()
    {
        NavMeshSurface[] surfaces = FindObjectsByType<NavMeshSurface>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (surfaces == null || surfaces.Length == 0)
            return false;

        bool hasBounds = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        foreach (NavMeshSurface surface in surfaces)
        {
            if (surface == null || surface.navMeshData == null)
                continue;

            Matrix4x4 matrix = Matrix4x4.TRS(
                surface.transform.position,
                surface.transform.rotation,
                Vector3.one);

            foreach (Vector3 corner in GetBoundsCorners(surface.navMeshData.sourceBounds))
            {
                Vector3 worldCorner = matrix.MultiplyPoint3x4(corner);

                if (!hasBounds)
                {
                    min = max = worldCorner;
                    hasBounds = true;
                }
                else
                {
                    min = Vector3.Min(min, worldCorner);
                    max = Vector3.Max(max, worldCorner);
                }
            }
        }

        if (!hasBounds)
            return false;

        ApplyNavMeshBounds(min, max);
        return true;
    }

    public bool TryRefreshFromManualBounds()
    {
        if (manualMapSize.x <= 0f || manualMapSize.y <= 0f)
            return false;

        bool boundsChanged =
            navMeshBoundsActive ||
            Vector3.Distance(mapOrigin, manualMapOrigin) > BoundsChangeEpsilon ||
            Vector2.Distance(mapSize, manualMapSize) > BoundsChangeEpsilon;

        mapOrigin = manualMapOrigin;
        mapSize = manualMapSize;
        navMeshBoundsActive = false;

        if (boundsChanged)
            navFloorCache?.Invalidate();

        return true;
    }

    // NavMesh 기반 바운즈를 적용한다. 바운즈가 실제로 바뀐 경우에만 칸 높이 캐시를
    // 비운다 - 트라이앵귤레이션은 Gizmo가 뜰 때마다 다시 돌더라도, 결과가 이전과
    // 같으면 칸별 NavMesh 샘플링(가장 비싼 부분)까지 다시 할 필요는 없다.
    void ApplyNavMeshBounds(Vector3 min, Vector3 max)
    {
        Vector3 newOrigin = new Vector3(min.x, min.y, min.z);
        Vector2 newSize = new Vector2(max.x - min.x, max.z - min.z);

        bool boundsChanged =
            !navMeshBoundsActive ||
            Vector3.Distance(mapOrigin, newOrigin) > BoundsChangeEpsilon ||
            Vector2.Distance(mapSize, newSize) > BoundsChangeEpsilon ||
            Mathf.Abs(navMeshMinY - min.y) > BoundsChangeEpsilon ||
            Mathf.Abs(navMeshMaxY - max.y) > BoundsChangeEpsilon;

        mapOrigin = newOrigin;
        mapSize = newSize;
        navMeshMinY = min.y;
        navMeshMaxY = max.y;
        navMeshBoundsActive = true;

        if (boundsChanged)
            navFloorCache?.Invalidate();
    }

    static Vector3[] GetBoundsCorners(Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        return new[]
        {
            center + new Vector3(-extents.x, -extents.y, -extents.z),
            center + new Vector3(extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y, extents.z),
            center + new Vector3(extents.x, -extents.y, extents.z),
            center + new Vector3(-extents.x, extents.y, -extents.z),
            center + new Vector3(extents.x, extents.y, -extents.z),
            center + new Vector3(-extents.x, extents.y, extents.z),
            center + new Vector3(extents.x, extents.y, extents.z),
        };
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Vector3 local = worldPosition - mapOrigin;

        return new Vector2Int(
            Mathf.FloorToInt(local.x / cellSize),
            Mathf.FloorToInt(local.z / cellSize));
    }

    public Vector3 CellCornerToWorld(Vector2Int cell)
    {
        return mapOrigin + new Vector3(
            cell.x * cellSize,
            0f,
            cell.y * cellSize);
    }

    public Vector3 GetCellCenterWorld(Vector2Int cell)
    {
        Vector3 corner = CellCornerToWorld(cell);

        return corner + new Vector3(
            cellSize * 0.5f,
            0f,
            cellSize * 0.5f);
    }

    public Vector3 GetFootprintCenterWorld(
        Vector2Int originCell,
        Vector2Int footprintCells)
    {
        Vector3 corner = CellCornerToWorld(originCell);

        return corner + new Vector3(
            footprintCells.x * cellSize * 0.5f,
            0f,
            footprintCells.y * cellSize * 0.5f);
    }

    public Vector2Int GetFootprintOriginFromWorld(
        Vector3 worldPosition,
        Vector2Int footprintCells)
    {
        Vector2Int origin = WorldToCell(worldPosition);
        origin.x = ClampOriginCoord(origin.x, footprintCells.x, CellCountX);
        origin.y = ClampOriginCoord(origin.y, footprintCells.y, CellCountZ);
        return origin;
    }

    public Vector2Int GetFootprintOriginFromCenterWorld(
        Vector3 centerWorld,
        Vector2Int footprintCells)
    {
        Vector3 local = centerWorld - mapOrigin;

        Vector2Int origin = new Vector2Int(
            Mathf.FloorToInt(
                (local.x - footprintCells.x * cellSize * 0.5f) / cellSize),
            Mathf.FloorToInt(
                (local.z - footprintCells.y * cellSize * 0.5f) / cellSize));

        origin.x = ClampOriginCoord(origin.x, footprintCells.x, CellCountX);
        origin.y = ClampOriginCoord(origin.y, footprintCells.y, CellCountZ);
        return origin;
    }

    public bool IsFootprintInBounds(
        Vector2Int originCell,
        Vector2Int footprintCells)
    {
        return IsFootprintInBounds(originCell, footprintCells, float.NaN);
    }

    public bool IsFootprintInBounds(
        Vector2Int originCell,
        Vector2Int footprintCells,
        float preferredY)
    {
        if (!IsFootprintInRect(originCell, footprintCells))
            return false;

        if (UsesNavMesh && requireNavMeshForCells)
            return IsFootprintOnNavMesh(originCell, footprintCells, preferredY);

        return true;
    }

    public bool IsCellOnHill(Vector2Int cell)
    {
        CliffPainter painter = GetCliffPainter();

        if (painter == null)
            return false;

        painter.EnsureLookup();
        return painter.HasHill(painter.WorldToCell(GetCellCenterWorld(cell)));
    }

    public bool IsFootprintOnHill(Vector2Int originCell, Vector2Int footprintCells)
    {
        for (int x = 0; x < footprintCells.x; x++)
        {
            for (int z = 0; z < footprintCells.y; z++)
            {
                if (IsCellOnHill(new Vector2Int(originCell.x + x, originCell.y + z)))
                    return true;
            }
        }

        return false;
    }

    CliffPainter GetCliffPainter()
    {
        if (cachedCliffPainter != null)
            return cachedCliffPainter;

        cachedCliffPainter = FindFirstObjectByType<CliffPainter>();
        return cachedCliffPainter;
    }

    // 보통 Awake에서 채워지지만, 에디터 Gizmo 경로처럼 Awake보다 먼저
    // 호출될 수 있는 경우를 대비한 지연 초기화이다.
    MapGridNavFloorCache GetNavFloorCache()
    {
        if (navFloorCache == null)
            navFloorCache = new MapGridNavFloorCache(this);

        return navFloorCache;
    }

    public bool IsFootprintInRect(
        Vector2Int originCell,
        Vector2Int footprintCells)
    {
        if (footprintCells.x <= 0 || footprintCells.y <= 0)
            return false;

        if (originCell.x < 0 || originCell.y < 0)
            return false;

        return originCell.x + footprintCells.x <= CellCountX &&
               originCell.y + footprintCells.y <= CellCountZ;
    }

    public bool IsCellOnNavMesh(Vector2Int cell)
    {
        if (!IsCellInGrid(cell))
            return false;

        return GetNavFloorCache().Get(cell).Covered.Length > 0;
    }

    // 칸 전체가 NavMesh로 덮인 층 중 가장 위쪽 높이입니다.
    // 건설 판정(IsCellOnNavMesh)과 같은 기준이면서 높이까지 한 번에 돌려주므로,
    // 건설 가능 칸을 그리는 쪽은 이걸 써야 표시와 판정이 어긋나지 않습니다.
    public bool TryGetBuildableNavMeshHeight(Vector2Int cell, out float height)
    {
        height = 0f;

        if (!IsCellInGrid(cell))
            return false;

        float[] covered = GetNavFloorCache().Get(cell).Covered;

        if (covered.Length == 0)
            return false;

        // Compute()가 Raw(오름차순)를 그대로 걸러낸 순서라 Covered도 오름차순이다.
        height = covered[covered.Length - 1];
        return true;
    }

    public float FloorHeightTolerance => GetFloorHeightTolerance();

    // 위에서 내려다볼 때 보이는 해당 칸의 최상단 NavMesh 높이.
    public bool TryGetTopmostNavMeshHeight(Vector2Int cell, out float height)
    {
        height = 0f;

        float[] raw = GetNavFloorCache().Get(cell).Raw;

        if (raw.Length == 0)
            return false;

        height = raw[raw.Length - 1];
        return true;
    }

    // 한 칸 XZ의 NavMesh 표면 높이를 수집합니다 (낮은 순).
    // MapGridNavFloorCache가 칸당 한 번만 호출해 캐싱하므로, 직접 부르는 외부
    // 코드가 없다면 매 프레임 반복 호출되지 않습니다.
    public int CollectNavMeshSurfaceHeights(
        Vector2Int cell,
        List<float> heights)
    {
        if (heights == null)
            return 0;

        heights.Clear();

        if (!IsCellInGrid(cell) || !UsesNavMesh)
            return 0;

        Vector3 center = GetCellCenterWorld(cell);
        float sampleRadius = Mathf.Max(0.05f, cellSize * navMeshSampleRadiusFactor);

        float minY = navMeshBoundsActive
            ? navMeshMinY - 0.5f
            : center.y - 32f;
        float maxY = navMeshBoundsActive
            ? navMeshMaxY + navMeshSampleHeightOffset
            : center.y + 32f;

        float separation = Mathf.Max(0.5f, GetFloorHeightTolerance());
        const int sliceCount = 16;

        for (int i = 0; i <= sliceCount; i++)
        {
            float t = i / (float)sliceCount;
            Vector3 probe = center;
            probe.y = Mathf.Lerp(maxY, minY, t);

            if (!NavMesh.SamplePosition(
                    probe,
                    out NavMeshHit hit,
                    sampleRadius,
                    navMeshAreaMask))
            {
                continue;
            }

            // XZ가 칸 안에 있는지만 본다 (Y는 층마다 다름).
            Vector2Int hitCell = WorldToCell(hit.position);
            if (hitCell.x != cell.x || hitCell.y != cell.y)
                continue;

            float y = hit.position.y;
            bool nearExisting = false;

            for (int h = 0; h < heights.Count; h++)
            {
                if (Mathf.Abs(heights[h] - y) < separation)
                {
                    nearExisting = true;
                    break;
                }
            }

            if (!nearExisting)
                heights.Add(y);
        }

        // 폴백: 최상단 XZ 샘플
        if (heights.Count == 0 &&
            UnitSpawnUtility.TrySampleTopmostAtXZ(center.x, center.z, out Vector3 topmost))
        {
            Vector2Int hitCell = WorldToCell(topmost);
            if (hitCell.x == cell.x && hitCell.y == cell.y)
                heights.Add(topmost.y);
        }

        heights.Sort();
        return heights.Count;
    }

    bool IsCellInGrid(Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < CellCountX &&
               cell.y < CellCountZ;
    }

    Vector3 GetCellSamplePoint(Vector2Int cell, float normalizedX, float normalizedZ)
    {
        Vector3 corner = CellCornerToWorld(cell);
        float inset = cellSize * navMeshCellSampleInsetFactor;
        float sampleX = Mathf.Lerp(inset, cellSize - inset, normalizedX);
        float sampleZ = Mathf.Lerp(inset, cellSize - inset, normalizedZ);

        return corner + new Vector3(sampleX, 0f, sampleZ);
    }

    float GetCellValidationRadius()
    {
        return Mathf.Max(0.05f, cellSize * navMeshCellValidationRadiusFactor);
    }

    float GetFloorHeightTolerance()
    {
        return Mathf.Max(0.5f, navMeshFloorHeightTolerance);
    }

    // MapGridNavFloorCache가 칸당 높이 후보를 걸러낼 때만 호출한다(내부 전용).
    internal bool IsCellCoveredAtHeight(Vector2Int cell, float height)
    {
        return TrySampleCellPointAtHeight(cell, GetCellCenterWorld(cell), height, out _) &&
               TrySampleCellPointAtHeight(cell, GetCellSamplePoint(cell, 0f, 0f), height, out _) &&
               TrySampleCellPointAtHeight(cell, GetCellSamplePoint(cell, 1f, 0f), height, out _) &&
               TrySampleCellPointAtHeight(cell, GetCellSamplePoint(cell, 1f, 1f), height, out _) &&
               TrySampleCellPointAtHeight(cell, GetCellSamplePoint(cell, 0f, 1f), height, out _);
    }

    bool TrySampleCellPointAtHeight(
        Vector2Int cell,
        Vector3 worldPoint,
        float height,
        out float sampledY)
    {
        sampledY = 0f;

        Vector3 probe = worldPoint;
        probe.y = height;

        if (!NavMesh.SamplePosition(
                probe,
                out NavMeshHit hit,
                GetCellValidationRadius(),
                navMeshAreaMask))
        {
            return false;
        }

        if (WorldToCell(hit.position) != cell)
            return false;

        if (Mathf.Abs(hit.position.y - height) > GetFloorHeightTolerance())
            return false;

        sampledY = hit.position.y;
        return true;
    }

    public bool TrySampleNavMeshAtXZ(Vector3 worldPosition, out NavMeshHit hit)
    {
        return TrySampleNavMeshNearHeight(worldPosition, worldPosition.y, out hit);
    }

    public bool IsFootprintOnNavMesh(
        Vector2Int originCell,
        Vector2Int footprintCells)
    {
        return IsFootprintOnNavMesh(originCell, footprintCells, float.NaN);
    }

    public bool IsFootprintOnNavMesh(
        Vector2Int originCell,
        Vector2Int footprintCells,
        float preferredY)
    {
        return TryGetSharedFootprintFloorHeight(
            originCell,
            footprintCells,
            out _,
            preferredY);
    }

    public bool TryGetSharedFootprintFloorHeight(
        Vector2Int originCell,
        Vector2Int footprintCells,
        out float floorHeight,
        float preferredY = float.NaN)
    {
        floorHeight = 0f;

        if (footprintCells.x <= 0 || footprintCells.y <= 0)
            return false;

        if (!UsesNavMesh)
        {
            floorHeight = GetFootprintCenterWorld(originCell, footprintCells).y;
            return true;
        }

        // 후보 높이는 원점 칸 기준으로만 뽑고(기존과 동일한 기준), 각 칸이 그
        // 높이에서 실제로 덮여 있는지는 칸별 캐시에서 찾는다 - NavMesh를 다시
        // 샘플링하지 않는다.
        float[] candidates = GetNavFloorCache().Get(originCell).Raw;

        if (candidates.Length == 0)
            return false;

        float tolerance = GetFloorHeightTolerance();
        float bestScore = float.MaxValue;
        float bestAverage = 0f;
        bool found = false;

        for (int c = 0; c < candidates.Length; c++)
        {
            float candidate = candidates[c];

            if (!float.IsNaN(preferredY) &&
                Mathf.Abs(candidate - preferredY) > tolerance)
            {
                continue;
            }

            float sum = 0f;
            int count = 0;
            bool allMatch = true;

            for (int x = 0; x < footprintCells.x && allMatch; x++)
            {
                for (int z = 0; z < footprintCells.y; z++)
                {
                    Vector2Int cell = new Vector2Int(
                        originCell.x + x,
                        originCell.y + z);

                    if (!TryFindCoveredHeightNear(cell, candidate, tolerance, out float match))
                    {
                        allMatch = false;
                        break;
                    }

                    sum += match;
                    count++;
                }
            }

            if (!allMatch || count <= 0)
                continue;

            float average = sum / count;
            float score = float.IsNaN(preferredY)
                ? 0f
                : Mathf.Abs(average - preferredY);

            if (score < bestScore)
            {
                bestScore = score;
                bestAverage = average;
                found = true;
            }
        }

        if (!found)
            return false;

        floorHeight = bestAverage;
        return true;
    }

    bool TryFindCoveredHeightNear(Vector2Int cell, float target, float tolerance, out float match)
    {
        float[] covered = GetNavFloorCache().Get(cell).Covered;

        for (int i = 0; i < covered.Length; i++)
        {
            if (Mathf.Abs(covered[i] - target) <= tolerance)
            {
                match = covered[i];
                return true;
            }
        }

        match = 0f;
        return false;
    }

    public bool TryGetSnappedFootprintPlacement(
        Vector3 worldHint,
        Vector2Int footprintCells,
        out Vector2Int originCell,
        out Vector3 centerWorld)
    {
        originCell = default;
        centerWorld = worldHint;

        if (footprintCells.x <= 0 || footprintCells.y <= 0)
            return false;

        if (CellCountX <= 0 || CellCountZ <= 0)
            return false;

        originCell = GetFootprintOriginFromCenterWorld(worldHint, footprintCells);

        if ((!IsFootprintInBounds(originCell, footprintCells, worldHint.y) ||
             IsFootprintOnHill(originCell, footprintCells)) &&
            UsesNavMesh &&
            requireNavMeshForCells &&
            TryFindNearestValidFootprint(
                originCell,
                footprintCells,
                worldHint.y,
                out Vector2Int fallbackOrigin))
        {
            originCell = fallbackOrigin;
        }

        if (!IsFootprintInRect(originCell, footprintCells))
            return false;

        centerWorld = GetFootprintCenterWorld(originCell, footprintCells);

        if (UsesNavMesh &&
            TryGetSharedFootprintFloorHeight(
                originCell,
                footprintCells,
                out float floorHeight,
                worldHint.y))
        {
            centerWorld.y = floorHeight;
        }
        else
        {
            centerWorld.y = SampleGroundHeight(centerWorld, worldHint.y);
        }

        return true;
    }

    public float SampleGroundHeight(Vector3 worldPosition)
    {
        return SampleGroundHeight(worldPosition, worldPosition.y);
    }

    // preferredY에 가장 가까운 NavMesh 표면 높이를 고릅니다 (다층 맵용).
    public float SampleGroundHeight(Vector3 worldPosition, float preferredY)
    {
        if (TrySampleNavMeshNearHeight(worldPosition, preferredY, out NavMeshHit hit))
            return hit.position.y;

        Vector2Int cell = WorldToCell(worldPosition);
        Vector3 cellCenter = GetCellCenterWorld(cell);

        if (TrySampleNavMeshNearHeight(cellCenter, preferredY, out hit))
            return hit.position.y;

        return preferredY;
    }

    public bool TrySampleNavMeshNearHeight(
        Vector3 worldPosition,
        float preferredY,
        out NavMeshHit hit)
    {
        return TrySampleNavMeshNearHeight(
            worldPosition,
            preferredY,
            out hit,
            maxVerticalDelta: GetFloorHeightTolerance());
    }

    public bool TrySampleNavMeshNearHeight(
        Vector3 worldPosition,
        float preferredY,
        out NavMeshHit hit,
        float maxVerticalDelta)
    {
        bool found = false;
        float bestScore = float.MaxValue;
        NavMeshHit best = default;

        float sampleRadius = Mathf.Max(0.05f, cellSize * navMeshSampleRadiusFactor);
        float maxYDelta = Mathf.Max(0.5f, maxVerticalDelta);

        Vector3 atPreferred = worldPosition;
        atPreferred.y = preferredY;

        if (NavMesh.SamplePosition(atPreferred, out NavMeshHit sample, sampleRadius, navMeshAreaMask) &&
            TryImproveCandidate(sample, worldPosition, preferredY, maxYDelta, ref bestScore, ref best))
        {
            found = true;
        }

        if (NavMesh.SamplePosition(worldPosition, out sample, sampleRadius, navMeshAreaMask) &&
            TryImproveCandidate(sample, worldPosition, preferredY, maxYDelta, ref bestScore, ref best))
        {
            found = true;
        }

        // 같은 층 근처만 슬라이스 (아래/위 층으로 떨어지지 않게)
        float minY = preferredY - maxYDelta;
        float maxY = preferredY + maxYDelta;

        if (navMeshBoundsActive)
        {
            minY = Mathf.Max(minY, navMeshMinY - 0.5f);
            maxY = Mathf.Min(maxY, navMeshMaxY + 0.5f);
        }

        const int sliceCount = 4;
        for (int i = 0; i <= sliceCount; i++)
        {
            float t = i / (float)sliceCount;
            Vector3 probe = worldPosition;
            probe.y = Mathf.Lerp(minY, maxY, t);

            if (NavMesh.SamplePosition(probe, out sample, sampleRadius * 2f, navMeshAreaMask) &&
                TryImproveCandidate(sample, worldPosition, preferredY, maxYDelta, ref bestScore, ref best))
            {
                found = true;
            }
        }

        hit = best;
        return found;
    }

    // 후보가 이전 최선보다 나으면 채택한다. 지역 함수 클로저 할당을 피하려고
    // static으로 뺐다 - 이 메서드는 유닛 스폰/건물 배치 등에서 자주 불린다.
    static bool TryImproveCandidate(
        NavMeshHit candidate,
        Vector3 worldPosition,
        float preferredY,
        float maxYDelta,
        ref float bestScore,
        ref NavMeshHit best)
    {
        float yDelta = Mathf.Abs(candidate.position.y - preferredY);
        if (yDelta > maxYDelta)
            return false;

        float score = yDelta;
        float xz = Vector2.Distance(
            new Vector2(candidate.position.x, candidate.position.z),
            new Vector2(worldPosition.x, worldPosition.z));
        score += xz * 0.05f;

        if (score >= bestScore)
            return false;

        bestScore = score;
        best = candidate;
        return true;
    }

    bool TryFindNearestValidFootprint(
        Vector2Int originCell,
        Vector2Int footprintCells,
        float preferredY,
        out Vector2Int validOrigin)
    {
        validOrigin = originCell;

        int maxRadius = 8;

        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(z) != radius)
                        continue;

                    Vector2Int candidate = new Vector2Int(
                        originCell.x + x,
                        originCell.y + z);

                    if (!IsFootprintInBounds(candidate, footprintCells, preferredY) ||
                        IsFootprintOnHill(candidate, footprintCells))
                        continue;

                    validOrigin = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    static int ClampOriginCoord(int origin, int footprint, int cellCount)
    {
        if (cellCount <= footprint)
            return 0;

        return Mathf.Clamp(origin, 0, cellCount - footprint);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGridGizmos || cellSize <= 0f)
            return;

        if (Application.isPlaying)
            Refresh();
        else if (useNavMeshBounds)
        {
            if (!TryRefreshFromNavMesh())
                TryRefreshFromNavMeshSurfaces();
        }
        else
            TryRefreshFromManualBounds();

        if (CellCountX <= 0 || CellCountZ <= 0)
            return;

        Gizmos.color = navMeshBoundsActive
            ? new Color(0.25f, 0.75f, 1f, 0.45f)
            : new Color(0.3f, 0.9f, 0.4f, 0.35f);

        if (UsesNavMesh && drawOnlyWalkableCellsInGizmos)
        {
            DrawWalkableCellGizmos();
            return;
        }

        DrawFullRectGridGizmos();
    }

    void DrawFullRectGridGizmos()
    {
        int cellCountX = CellCountX;
        int cellCountZ = CellCountZ;

        for (int x = 0; x <= cellCountX; x++)
        {
            float worldX = mapOrigin.x + x * cellSize;
            Vector3 start = new Vector3(worldX, mapOrigin.y, mapOrigin.z);
            Vector3 end = new Vector3(
                worldX,
                mapOrigin.y,
                mapOrigin.z + mapSize.y);

            Gizmos.DrawLine(start, end);
        }

        for (int z = 0; z <= cellCountZ; z++)
        {
            float worldZ = mapOrigin.z + z * cellSize;
            Vector3 start = new Vector3(mapOrigin.x, mapOrigin.y, worldZ);
            Vector3 end = new Vector3(
                mapOrigin.x + mapSize.x,
                mapOrigin.y,
                worldZ);

            Gizmos.DrawLine(start, end);
        }
    }

    void DrawWalkableCellGizmos()
    {
        int cellCountX = CellCountX;
        int cellCountZ = CellCountZ;

        for (int x = 0; x < cellCountX; x++)
        {
            for (int z = 0; z < cellCountZ; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);

                // 실제 건설 판정과 같은 기준(TryGetBuildableNavMeshHeight)으로
                // 그려야, 다층 지형에서 표시와 판정이 어긋나지 않는다.
                if (!TryGetBuildableNavMeshHeight(cell, out float y))
                    continue;

                Vector3 corner = CellCornerToWorld(cell);

                Vector3 a = new Vector3(corner.x, y, corner.z);
                Vector3 b = new Vector3(corner.x + cellSize, y, corner.z);
                Vector3 c = new Vector3(corner.x + cellSize, y, corner.z + cellSize);
                Vector3 d = new Vector3(corner.x, y, corner.z + cellSize);

                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(b, c);
                Gizmos.DrawLine(c, d);
                Gizmos.DrawLine(d, a);
            }
        }
    }
}
