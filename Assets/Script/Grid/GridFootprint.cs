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
        if (!blockCells)
            return true;

        Release();

        if (MapGrid.Instance == null || GridOccupancy.Instance == null)
            return false;

        if (!GridOccupancy.Instance.TryOccupy(originCell, footprintCells, this))
            return false;

        IsRegistered = true;
        RegisteredOrigin = originCell;

        ConfigureStationaryBuilding(gameObject);

        if (snapTransformOnRegister)
        {
            Vector3 center =
                MapGrid.Instance.GetFootprintCenterWorld(originCell, footprintCells);

            center.y = MapGrid.Instance.SampleGroundHeight(center);
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

        float cellSize = MapGrid.Instance != null
            ? MapGrid.Instance.cellSize
            : 2f;

        NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();

        if (obstacle == null)
            obstacle = gameObject.AddComponent<NavMeshObstacle>();

        float width = footprintCells.x * cellSize;
        float depth = footprintCells.y * cellSize;
        float height = obstacle.size.y > 0f ? obstacle.size.y : 4f;

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(
            width * 0.85f,
            height,
            depth * 0.85f);
        obstacle.center = new Vector3(0f, height * 0.5f, 0f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.enabled = true;
    }
}
