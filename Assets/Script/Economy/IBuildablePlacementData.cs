using UnityEngine;

public interface IBuildablePlacementData
{
    string BuildAssetName { get; }
    string DisplayName { get; }
    GameObject Prefab { get; }
    int WattCost { get; }
    int OwnerId { get; }
    string GetEntityTypeId();
    Vector2Int GetFootprintCells();
}
