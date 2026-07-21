using UnityEngine;

[CreateAssetMenu(
    fileName = "BuildableTowerData",
    menuName = "Tank/Buildable Tower Data")]
public class BuildableTowerData : ScriptableObject, IBuildablePlacementData
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

    [Tooltip("더블클릭 시 같은 종류로 묶을 타입 ID입니다. 비워두면 프리팹 SelectableEntity 값을 사용합니다.")]
    public string entityTypeId;

    public string BuildAssetName => name;
    public string DisplayName => displayName;
    public GameObject Prefab => prefab;
    public int WattCost => wattCost;
    public int OwnerId => ownerId;

    public Vector2Int GetFootprintCells()
    {
        return GridFootprint.ResolveFootprintCells(prefab);
    }

    public string GetEntityTypeId()
    {
        if (!string.IsNullOrWhiteSpace(entityTypeId))
            return entityTypeId;

        if (prefab == null)
            return displayName;

        SelectableEntity selectable = prefab.GetComponent<SelectableEntity>();

        if (selectable != null && !string.IsNullOrWhiteSpace(selectable.entityTypeId))
            return selectable.entityTypeId;

        return displayName;
    }
}
