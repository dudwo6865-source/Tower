using UnityEngine;

[CreateAssetMenu(
    fileName = "BuildableTowerData",
    menuName = "Tank/Buildable Tower Data")]
public class BuildableTowerData : ScriptableObject
{
    [Header("Display")]
    public string displayName = "Tower";

    [TextArea]
    public string description;

    [Header("Build")]
    public GameObject prefab;

    [Tooltip("배치 확정 시 소비되는 Watt입니다. 확정 후에는 환불되지 않습니다.")]
    public int wattCost = 50;

    [Tooltip("배치되는 타워의 소유자 ID입니다.")]
    public int ownerId = 1;

    public Vector2Int GetFootprintCells()
    {
        return GridFootprint.ResolveFootprintCells(prefab);
    }
}
