using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildZoneProvider : MonoBehaviour
{
    [Tooltip("비워두면 SelectableEntity의 ownerId를 사용합니다.")]
    public int ownerId = 1;

    [Tooltip("건물 중심 기준 원형 건설 가능 반경(칸)입니다.")]
    public int buildRadiusCells = 12;

    [Tooltip("건설 구역(푸른 타일)을 지면 위로 띄울 높이(미터)입니다.")]
    public float buildZoneHeightOffset = 0.06f;

    [Tooltip("이 건물 자체는 기존 건설 구역 밖에도 배치할 수 있습니다.")]
    public bool canPlaceOutsideBuildZones;

    public int OwnerId =>
        selectableEntity != null ? selectableEntity.ownerId : ownerId;

    public Vector2Int CenterCell { get; private set; }

    private SelectableEntity selectableEntity;
    private GridFootprint gridFootprint;

    void Awake()
    {
        selectableEntity = GetComponent<SelectableEntity>();
        gridFootprint = GetComponent<GridFootprint>();
    }

    void OnEnable()
    {
        StartCoroutine(RegisterWhenReady());
    }

    void OnDisable()
    {
        if (BuildZoneManager.Instance != null)
            BuildZoneManager.Instance.Unregister(this);
    }

    IEnumerator RegisterWhenReady()
    {
        yield return null;

        RefreshCenterCell();

        if (BuildZoneManager.Instance != null)
            BuildZoneManager.Instance.Register(this);
    }

    public void RefreshCenterCell()
    {
        Vector2Int footprint = gridFootprint != null
            ? gridFootprint.footprintCells
            : GridFootprint.DefaultBuildingFootprint;

        if (gridFootprint != null && gridFootprint.IsRegistered)
        {
            CenterCell = GetFootprintCenterCell(
                gridFootprint.RegisteredOrigin,
                footprint);

            return;
        }

        if (MapGrid.Instance == null)
        {
            CenterCell = Vector2Int.zero;
            return;
        }

        Vector2Int origin = MapGrid.Instance.GetFootprintOriginFromCenterWorld(
            transform.position,
            footprint);

        CenterCell = GetFootprintCenterCell(origin, footprint);
    }

    public bool ContainsFootprint(Vector2Int originCell, Vector2Int footprintCells)
    {
        RefreshCenterCell();
        return ContainsFootprint(originCell, footprintCells, CenterCell, buildRadiusCells);
    }

    public bool ContainsCell(Vector2Int cell)
    {
        RefreshCenterCell();
        return ContainsCell(cell, CenterCell, buildRadiusCells);
    }

    public static bool ContainsFootprint(
        Vector2Int originCell,
        Vector2Int footprintCells,
        Vector2Int centerCell,
        int radiusCells)
    {
        for (int x = 0; x < footprintCells.x; x++)
        {
            for (int z = 0; z < footprintCells.y; z++)
            {
                Vector2Int cell = new Vector2Int(
                    originCell.x + x,
                    originCell.y + z);

                if (!ContainsCell(cell, centerCell, radiusCells))
                    return false;
            }
        }

        return true;
    }

    public static bool ContainsCell(
        Vector2Int cell,
        Vector2Int centerCell,
        int radiusCells)
    {
        if (radiusCells <= 0)
            return false;

        MapGrid grid = MapGrid.Instance;

        if (grid != null)
        {
            Vector3 cellCenter = grid.GetCellCenterWorld(cell);
            Vector3 zoneCenter = grid.GetCellCenterWorld(centerCell);
            float radiusWorld = radiusCells * grid.cellSize;

            cellCenter.y = 0f;
            zoneCenter.y = 0f;

            if ((cellCenter - zoneCenter).sqrMagnitude > radiusWorld * radiusWorld)
                return false;

            return !grid.IsCellOnHill(cell);
        }

        float dx = cell.x - centerCell.x;
        float dz = cell.y - centerCell.y;
        return dx * dx + dz * dz <= radiusCells * radiusCells;
    }

    public static Vector2Int GetFootprintCenterCell(
        Vector2Int originCell,
        Vector2Int footprintCells)
    {
        return originCell + new Vector2Int(
            footprintCells.x / 2,
            footprintCells.y / 2);
    }

    public static bool PrefabCanPlaceOutsideBuildZones(GameObject prefab)
    {
        BuildZoneProvider provider = ResolveProvider(prefab);
        return provider != null && provider.canPlaceOutsideBuildZones;
    }

    public static bool PrefabRequiresVisibleVision(GameObject prefab)
    {
        return ResolveProvider(prefab) != null;
    }

    public static bool IsFootprintCurrentlyVisible(
        Vector2Int originCell,
        Vector2Int footprintCells)
    {
        FogOfWarManager fog = FogOfWarManager.Instance;
        MapGrid grid = MapGrid.Instance;

        if (fog == null || grid == null)
            return true;

        int width = Mathf.Max(1, footprintCells.x);
        int depth = Mathf.Max(1, footprintCells.y);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                Vector2Int cell = new Vector2Int(originCell.x + x, originCell.y + z);

                if (!IsCellCurrentlyVisible(fog, grid, cell))
                    return false;
            }
        }

        return true;
    }

    static bool IsCellCurrentlyVisible(
        FogOfWarManager fog,
        MapGrid grid,
        Vector2Int cell)
    {
        Vector3 center = grid.GetCellCenterWorld(cell);
        float inset = grid.cellSize * 0.25f;

        // 안개 텍스처는 선형 보간이라 화면상 시야와 칸 중심 점 샘플이 어긋날 수 있다.
        // 엔티티가 보이는 것과 같은 임계값으로, 칸 안 여러 지점 중 현재 시야가 있으면 통과한다.
        Vector3[] samples =
        {
            center,
            center + new Vector3(-inset, 0f, -inset),
            center + new Vector3(inset, 0f, -inset),
            center + new Vector3(-inset, 0f, inset),
            center + new Vector3(inset, 0f, inset)
        };

        for (int i = 0; i < samples.Length; i++)
        {
            if (fog.IsVisibleForSpawnAvoidance(samples[i]))
                return true;
        }

        return false;
    }

    static BuildZoneProvider ResolveProvider(GameObject prefab)
    {
        if (prefab == null)
            return null;

        BuildZoneProvider provider = prefab.GetComponent<BuildZoneProvider>();

        if (provider != null)
            return provider;

        return prefab.GetComponentInChildren<BuildZoneProvider>(true);
    }

    void OnDrawGizmosSelected()
    {
        if (buildRadiusCells <= 0 || MapGrid.Instance == null)
            return;

        RefreshCenterCell();

        MapGrid grid = MapGrid.Instance;
        Vector3 centerWorld = grid.GetCellCenterWorld(CenterCell);
        float radiusWorld = buildRadiusCells * grid.cellSize;

        Gizmos.color = canPlaceOutsideBuildZones
            ? new Color(0.35f, 1f, 0.55f, 0.35f)
            : new Color(0.2f, 0.85f, 1f, 0.35f);
        Gizmos.DrawWireSphere(centerWorld + Vector3.up * 0.05f, radiusWorld);
    }
}
