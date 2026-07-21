using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-290)]
public class GridOccupancy : MonoBehaviour
{
    public static GridOccupancy Instance { get; private set; }

    [Header("Scene Setup")]
    [Tooltip("씬에 미리 배치된 건물 중 GridFootprint가 없으면 이 크기로 자동 등록합니다.")]
    public Vector2Int defaultBuildingFootprint = new Vector2Int(2, 2);

    private readonly Dictionary<Vector2Int, GridFootprint> occupiedCells =
        new Dictionary<Vector2Int, GridFootprint>();

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
        StartCoroutine(RegisterSceneBuildingsNextFrame());
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    IEnumerator RegisterSceneBuildingsNextFrame()
    {
        yield return null;

        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (entity == null ||
                entity.entityType != SelectableEntityType.Building)
                continue;

            EntityHealth health = entity.GetComponent<EntityHealth>();

            if (health != null && !health.IsAlive)
                continue;

            GridFootprint.ConfigureStationaryBuilding(entity.gameObject);

            GridFootprint footprint = entity.GetComponent<GridFootprint>();

            if (footprint == null)
            {
                footprint = entity.gameObject.AddComponent<GridFootprint>();
                footprint.footprintCells = defaultBuildingFootprint;
            }

            if (!footprint.IsRegistered)
                footprint.RegisterAtCurrentPosition();
        }
    }

    public bool CanOccupy(Vector2Int originCell, Vector2Int footprintCells)
    {
        if (MapGrid.Instance == null)
            return true;

        if (!MapGrid.Instance.IsFootprintInBounds(originCell, footprintCells))
            return false;

        foreach (Vector2Int cell in IterateFootprint(originCell, footprintCells))
        {
            if (occupiedCells.ContainsKey(cell))
                return false;
        }

        return true;
    }

    public bool TryOccupy(
        Vector2Int originCell,
        Vector2Int footprintCells,
        GridFootprint owner)
    {
        if (owner == null || !CanOccupy(originCell, footprintCells))
            return false;

        foreach (Vector2Int cell in IterateFootprint(originCell, footprintCells))
            occupiedCells[cell] = owner;

        return true;
    }

    public void Release(GridFootprint owner)
    {
        if (owner == null)
            return;

        var cellsToRemove = new List<Vector2Int>();

        foreach (KeyValuePair<Vector2Int, GridFootprint> pair in occupiedCells)
        {
            if (pair.Value == owner)
                cellsToRemove.Add(pair.Key);
        }

        foreach (Vector2Int cell in cellsToRemove)
            occupiedCells.Remove(cell);
    }

    public bool IsOccupied(Vector2Int cell)
    {
        return occupiedCells.ContainsKey(cell);
    }

    public void CopyOccupiedCellsTo(List<Vector2Int> results)
    {
        if (results == null)
            return;

        results.Clear();

        foreach (Vector2Int cell in occupiedCells.Keys)
            results.Add(cell);
    }

    static IEnumerable<Vector2Int> IterateFootprint(
        Vector2Int originCell,
        Vector2Int footprintCells)
    {
        for (int x = 0; x < footprintCells.x; x++)
        {
            for (int z = 0; z < footprintCells.y; z++)
                yield return new Vector2Int(originCell.x + x, originCell.y + z);
        }
    }
}
