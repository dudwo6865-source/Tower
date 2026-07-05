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

    [Tooltip("footprint 모든 칸이 NavMesh 위에 있어야 합니다.")]
    public bool requireNavMeshForCells = true;

    [Tooltip("NavMesh.SamplePosition 검색 반경(칸 크기 대비)입니다.")]
    [Range(0.1f, 1.5f)]
    public float navMeshSampleRadiusFactor = 0.45f;

    [Tooltip("칸 NavMesh 검증 샘플 반경(칸 크기 대비)입니다. 건설 구역에서 사용합니다.")]
    [Range(0.05f, 0.75f)]
    public float navMeshCellValidationRadiusFactor = 0.25f;

    [Tooltip("칸 NavMesh 검증 시 모서리에서 안쪽으로 띄울 거리(칸 크기 대비)입니다.")]
    [Range(0f, 0.45f)]
    public float navMeshCellSampleInsetFactor = 0.2f;

    [Tooltip("높이/시각화용 NavMesh 샘플 반경(칸 크기 대비)입니다.")]
    [Range(0.5f, 2f)]
    public float navMeshHeightSampleRadiusFactor = 1.2f;

    [Tooltip("NavMesh 샘플 시 위에서 내려다볼 여유 높이(미터)입니다.")]
    public float navMeshSampleHeightOffset = 4f;

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

    private Vector3 mapOrigin;
    private Vector2 mapSize;
    private float navMeshMinY;
    private float navMeshMaxY;
    private bool navMeshBoundsActive;
    private bool loggedNavMeshFailure;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
        if (Instance == this)
            Instance = null;
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

        mapOrigin = new Vector3(min.x, min.y, min.z);
        mapSize = new Vector2(max.x - min.x, max.z - min.z);
        navMeshMinY = min.y;
        navMeshMaxY = max.y;
        navMeshBoundsActive = true;
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

        mapOrigin = new Vector3(min.x, min.y, min.z);
        mapSize = new Vector2(max.x - min.x, max.z - min.z);
        navMeshMinY = min.y;
        navMeshMaxY = max.y;
        navMeshBoundsActive = true;
        return true;
    }

    public bool TryRefreshFromManualBounds()
    {
        if (manualMapSize.x <= 0f || manualMapSize.y <= 0f)
            return false;

        mapOrigin = manualMapOrigin;
        mapSize = manualMapSize;
        navMeshBoundsActive = false;
        return true;
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
        if (!IsFootprintInRect(originCell, footprintCells))
            return false;

        if (UsesNavMesh && requireNavMeshForCells)
            return IsFootprintOnNavMesh(originCell, footprintCells);

        return true;
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

        return IsNavMeshSampleInCell(cell, GetCellCenterWorld(cell)) &&
               IsNavMeshSampleInCell(cell, GetCellSamplePoint(cell, 0f, 0f)) &&
               IsNavMeshSampleInCell(cell, GetCellSamplePoint(cell, 1f, 0f)) &&
               IsNavMeshSampleInCell(cell, GetCellSamplePoint(cell, 1f, 1f)) &&
               IsNavMeshSampleInCell(cell, GetCellSamplePoint(cell, 0f, 1f));
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

    bool IsNavMeshSampleInCell(Vector2Int cell, Vector3 worldPoint)
    {
        if (!TrySampleNavMeshForCell(cell, worldPoint, out NavMeshHit hit))
            return false;

        return WorldToCell(hit.position) == cell;
    }

    bool TrySampleNavMeshForCell(
        Vector2Int cell,
        Vector3 worldPoint,
        out NavMeshHit hit)
    {
        hit = default;

        Vector3 probe = worldPoint;

        if (UsesNavMesh && navMeshBoundsActive)
            probe.y = navMeshMaxY + navMeshSampleHeightOffset;

        float radius = cellSize * navMeshCellValidationRadiusFactor;

        if (NavMesh.SamplePosition(
                probe,
                out hit,
                radius,
                navMeshAreaMask) &&
            WorldToCell(hit.position) == cell)
        {
            return true;
        }

        if (!TrySampleNavMesh(probe, out hit))
            return false;

        return WorldToCell(hit.position) == cell;
    }

    Vector3 GetCellNavMeshProbePosition(Vector2Int cell)
    {
        Vector3 center = GetCellCenterWorld(cell);

        if (UsesNavMesh && navMeshBoundsActive)
            center.y = navMeshMaxY + navMeshSampleHeightOffset;

        return center;
    }

    public bool TrySampleNavMeshAtXZ(Vector3 worldPosition, out NavMeshHit hit)
    {
        Vector3 probe = worldPosition;

        if (UsesNavMesh && navMeshBoundsActive)
            probe.y = navMeshMaxY + navMeshSampleHeightOffset;

        return TrySampleNavMesh(probe, out hit);
    }

    public bool IsFootprintOnNavMesh(
        Vector2Int originCell,
        Vector2Int footprintCells)
    {
        for (int x = 0; x < footprintCells.x; x++)
        {
            for (int z = 0; z < footprintCells.y; z++)
            {
                Vector2Int cell = new Vector2Int(
                    originCell.x + x,
                    originCell.y + z);

                if (!IsCellOnNavMesh(cell))
                    return false;
            }
        }

        return true;
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

        if (!IsFootprintInBounds(originCell, footprintCells) &&
            UsesNavMesh &&
            requireNavMeshForCells &&
            TryFindNearestValidFootprint(
                originCell,
                footprintCells,
                out Vector2Int fallbackOrigin))
        {
            originCell = fallbackOrigin;
        }

        if (!IsFootprintInBounds(originCell, footprintCells))
            return false;

        centerWorld = GetFootprintCenterWorld(originCell, footprintCells);
        centerWorld.y = SampleGroundHeight(centerWorld);
        return true;
    }

    public float SampleGroundHeight(Vector3 worldPosition)
    {
        if (TrySampleNavMeshHeight(worldPosition, out NavMeshHit hit))
            return hit.position.y;

        Vector2Int cell = WorldToCell(worldPosition);
        Vector3 cellCenter = GetCellCenterWorld(cell);

        if (TrySampleNavMeshHeight(cellCenter, out hit))
            return hit.position.y;

        return worldPosition.y;
    }

    public bool TrySampleNavMesh(Vector3 worldPosition, out NavMeshHit hit)
    {
        float radius = cellSize * navMeshSampleRadiusFactor;

        if (NavMesh.SamplePosition(
                worldPosition,
                out hit,
                radius,
                navMeshAreaMask))
        {
            return true;
        }

        if (!UsesNavMesh)
            return false;

        float verticalRange = GetNavMeshVerticalSearchRange();

        Vector3 fromAbove = worldPosition;
        fromAbove.y = navMeshBoundsActive
            ? navMeshMaxY + navMeshSampleHeightOffset
            : worldPosition.y + navMeshSampleHeightOffset;

        if (NavMesh.SamplePosition(
                fromAbove,
                out hit,
                verticalRange,
                navMeshAreaMask))
        {
            return true;
        }

        Vector3 fromBelow = worldPosition;
        fromBelow.y = navMeshBoundsActive
            ? navMeshMinY - navMeshSampleHeightOffset
            : worldPosition.y - navMeshSampleHeightOffset;

        return NavMesh.SamplePosition(
            fromBelow,
            out hit,
            verticalRange,
            navMeshAreaMask);
    }

    float GetNavMeshVerticalSearchRange()
    {
        if (navMeshBoundsActive)
        {
            return navMeshMaxY - navMeshMinY +
                   navMeshSampleHeightOffset * 2f +
                   cellSize;
        }

        return navMeshSampleHeightOffset * 4f + cellSize * 2f;
    }

    bool TrySampleNavMeshHeight(Vector3 worldPosition, out NavMeshHit hit)
    {
        if (TrySampleNavMesh(worldPosition, out hit))
            return true;

        return NavMesh.SamplePosition(
            worldPosition,
            out hit,
            cellSize * navMeshHeightSampleRadiusFactor,
            navMeshAreaMask);
    }

    bool TryFindNearestValidFootprint(
        Vector2Int originCell,
        Vector2Int footprintCells,
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

                    if (!IsFootprintInBounds(candidate, footprintCells))
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
        for (int x = 0; x <= CellCountX; x++)
        {
            float worldX = mapOrigin.x + x * cellSize;
            Vector3 start = new Vector3(worldX, mapOrigin.y, mapOrigin.z);
            Vector3 end = new Vector3(
                worldX,
                mapOrigin.y,
                mapOrigin.z + mapSize.y);

            Gizmos.DrawLine(start, end);
        }

        for (int z = 0; z <= CellCountZ; z++)
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
        for (int x = 0; x < CellCountX; x++)
        {
            for (int z = 0; z < CellCountZ; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);

                if (!IsCellOnNavMesh(cell))
                    continue;

                Vector3 corner = CellCornerToWorld(cell);
                float y = SampleGroundHeight(GetCellCenterWorld(cell));

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
