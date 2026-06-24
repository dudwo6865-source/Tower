using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Headquarters : MonoBehaviour
{
    [Tooltip("비워두면 SelectableEntity의 ownerId를 사용합니다.")]
    public int ownerId = 1;

    [Tooltip("HQ 중심 기준 원형 건설 가능 반경(칸)입니다.")]
    public int buildRadiusCells = 12;

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

        for (int x = 0; x < footprintCells.x; x++)
        {
            for (int z = 0; z < footprintCells.y; z++)
            {
                Vector2Int cell = new Vector2Int(
                    originCell.x + x,
                    originCell.y + z);

                if (!ContainsCell(cell))
                    return false;
            }
        }

        return true;
    }

    public bool ContainsCell(Vector2Int cell)
    {
        if (buildRadiusCells <= 0)
            return false;

        MapGrid grid = MapGrid.Instance;

        if (grid != null)
        {
            Vector3 cellCenter = grid.GetCellCenterWorld(cell);
            Vector3 hqCenter = grid.GetCellCenterWorld(CenterCell);
            float radiusWorld = buildRadiusCells * grid.cellSize;

            cellCenter.y = 0f;
            hqCenter.y = 0f;

            return (cellCenter - hqCenter).sqrMagnitude <= radiusWorld * radiusWorld;
        }

        float dx = cell.x - CenterCell.x;
        float dz = cell.y - CenterCell.y;
        return dx * dx + dz * dz <= buildRadiusCells * buildRadiusCells;
    }

    static Vector2Int GetFootprintCenterCell(
        Vector2Int originCell,
        Vector2Int footprintCells)
    {
        return originCell + new Vector2Int(
            footprintCells.x / 2,
            footprintCells.y / 2);
    }

    void OnDrawGizmosSelected()
    {
        if (buildRadiusCells <= 0 || MapGrid.Instance == null)
            return;

        RefreshCenterCell();

        MapGrid grid = MapGrid.Instance;
        Vector3 centerWorld = grid.GetCellCenterWorld(CenterCell);
        float radiusWorld = buildRadiusCells * grid.cellSize;

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.35f);
        Gizmos.DrawWireSphere(centerWorld + Vector3.up * 0.05f, radiusWorld);
    }
}
