using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-320)]
public class GridFootprint : MonoBehaviour
{
    public static Vector2Int DefaultBuildingFootprint = new Vector2Int(2, 2);

    [Tooltip("배치·겹침 검사에 사용할 칸 수입니다. 배치 미리보기 격자 크기도 이 값을 따릅니다.")]
    public Vector2Int footprintCells = new Vector2Int(2, 2);

    [Tooltip("켜면 footprint 칸을 격자에 점유하고 NavMeshObstacle 크기를 footprint에 맞춥니다.")]
    public bool blockCells = true;

    [Tooltip("켜면 NavMesh에 구멍을 뚫습니다(카빙). 적 스포너처럼 유닛이 위에서 스폰돼 나와야 하는 건물은 끄세요.")]
    public bool carveNavMesh = true;

    [Tooltip("등록 시 transform.position을 footprint 중심 격자에 맞춥니다.")]
    public bool snapTransformOnRegister;

    public bool IsRegistered { get; private set; }

    public Vector2Int RegisteredOrigin { get; private set; }

    public static Vector2Int ResolveFootprintCells(GameObject source)
    {
        if (source == null)
            return DefaultBuildingFootprint;

        GridFootprint footprint = source.GetComponent<GridFootprint>();

        if (footprint != null)
            return NormalizeFootprint(footprint.footprintCells);

        return DefaultBuildingFootprint;
    }

    public static GridFootprint EnsureOnInstance(GameObject instance)
    {
        GridFootprint footprint = instance.GetComponent<GridFootprint>();

        if (footprint != null)
            return footprint;

        return instance.AddComponent<GridFootprint>();
    }

    static Vector2Int NormalizeFootprint(Vector2Int cells)
    {
        if (cells.x <= 0 || cells.y <= 0)
            return Vector2Int.one;

        return cells;
    }

    void Awake()
    {
        if (blockCells)
            ConfigureStationaryBuilding(gameObject);
    }

    void Start()
    {
        if (!blockCells)
            return;

        if (!IsRegistered)
            RegisterAtCurrentPosition();
        else
            ApplyNavMeshObstacleSize();
    }

    void OnValidate()
    {
        footprintCells = NormalizeFootprint(footprintCells);

        if (blockCells)
            ApplyNavMeshObstacleSize();
    }

    void OnDisable()
    {
        Release();
    }

    public bool RegisterAtCurrentPosition()
    {
        return RegisterAtWorldPosition(transform.position);
    }

    public bool RegisterAtWorldPosition(Vector3 worldPosition)
    {
        if (!blockCells)
            return true;

        Release();

        if (MapGrid.Instance == null || GridOccupancy.Instance == null)
            return false;

        Vector2Int origin =
            MapGrid.Instance.GetFootprintOriginFromCenterWorld(
                worldPosition,
                footprintCells);

        return RegisterAtOriginCell(origin);
    }

    public bool RegisterAtOriginCell(Vector2Int originCell)
    {
        return RegisterAtOriginCell(originCell, false);
    }

    /// <summary>
    /// skipTerrainChecks는 방금 철거한 같은 크기 건물의 칸을 그대로 이어받을 때만 켭니다.
    /// NavMesh carve 구멍이 아직 복구되지 않아 지형 검사가 실패하기 때문입니다.
    /// </summary>
    public bool RegisterAtOriginCell(Vector2Int originCell, bool skipTerrainChecks)
    {
        if (!blockCells)
            return true;

        Release();

        if (MapGrid.Instance == null || GridOccupancy.Instance == null)
            return false;

        if (!GridOccupancy.Instance.TryOccupy(
                originCell,
                footprintCells,
                this,
                float.NaN,
                skipTerrainChecks))
            return false;

        IsRegistered = true;
        RegisteredOrigin = originCell;

        ConfigureStationaryBuilding(gameObject);

        if (snapTransformOnRegister)
        {
            Vector3 center =
                MapGrid.Instance.GetFootprintCenterWorld(originCell, footprintCells);

            // 다층 맵: 이미 배치된 높이를 선호해 같은 XZ의 아래층 NavMesh로 내려가지 않게 합니다.
            center.y = MapGrid.Instance.SampleGroundHeight(center, transform.position.y);
            transform.position = center;
        }

        ApplyNavMeshObstacleSize();
        return true;
    }

    public void Release()
    {
        if (!IsRegistered)
            return;

        if (GridOccupancy.Instance != null)
            GridOccupancy.Instance.Release(this);

        IsRegistered = false;
    }

    public static void ConfigureStationaryBuilding(GameObject target)
    {
        if (target == null)
            return;

        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();

        if (agent != null)
            agent.enabled = false;

        Rigidbody body = target.GetComponent<Rigidbody>();

        if (body != null)
        {
            body.isKinematic = true;
            body.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    void ApplyNavMeshObstacleSize()
    {
        if (!blockCells)
            return;

        NavMeshObstacle existing = GetComponent<NavMeshObstacle>();

        // 카빙을 끈 건물(예: 적 스포너)은 NavMesh 구멍을 뚫지 않는다.
        // 유닛이 건물 위에서 스폰돼 나올 수 있어야 하므로 장애물을 비활성화한다.
        if (!carveNavMesh)
        {
            if (existing != null)
            {
                existing.carving = false;
                existing.enabled = false;
            }

            return;
        }

        float cellSize = MapGrid.Instance != null
            ? MapGrid.Instance.cellSize
            : 2f;

        NavMeshObstacle obstacle = existing;

        if (obstacle == null)
            obstacle = gameObject.AddComponent<NavMeshObstacle>();

        float width = footprintCells.x * cellSize;
        float depth = footprintCells.y * cellSize;
        float height = obstacle.size.y > 0f ? obstacle.size.y : 4f;
        float sizeScale = MapGrid.Instance != null
            ? MapGrid.Instance.navObstacleSizeScale
            : 0.85f;

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(
            width * sizeScale,
            height,
            depth * sizeScale);
        obstacle.center = new Vector3(0f, height * 0.5f, 0f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.enabled = true;
    }
}
