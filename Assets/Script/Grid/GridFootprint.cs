using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-320)]
public class GridFootprint : MonoBehaviour
{
    public static Vector2Int DefaultBuildingFootprint = new Vector2Int(2, 2);

    [Tooltip("이 오브젝트가 차지하는 칸 수입니다. 타워·건물 프리팹마다 설정합니다.")]
    public Vector2Int footprintCells = new Vector2Int(2, 2);

    [Tooltip("격자 점유 및 NavMeshObstacle 크기를 자동 적용합니다.")]
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
            ConfigureStationaryBuilding();
    }

    void Start()
    {
        if (blockCells && !IsRegistered)
            RegisterAtCurrentPosition();
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

        ConfigureStationaryBuilding();

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

    void ConfigureStationaryBuilding()
    {
        ConfigureStationaryBuilding(gameObject);
    }

    void ApplyNavMeshObstacleSize()
    {
        if (MapGrid.Instance == null)
            return;

        NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();

        if (obstacle == null)
            obstacle = gameObject.AddComponent<NavMeshObstacle>();

        float width = footprintCells.x * MapGrid.Instance.cellSize;
        float depth = footprintCells.y * MapGrid.Instance.cellSize;

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(
            width * 0.95f,
            obstacle.size.y > 0f ? obstacle.size.y : 4f,
            depth * 0.95f);
        obstacle.center = new Vector3(0f, obstacle.size.y * 0.5f, 0f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.enabled = true;
    }
}
