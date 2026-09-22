using UnityEngine;

[CreateAssetMenu(
    fileName = "BuildableProductionData",
    menuName = "Tank/Buildable Production Data")]
public class BuildableProductionData : ScriptableObject, IBuildablePlacementData
{
    [Header("Display")]
    public string displayName = "Barracks";

    [Tooltip("빌드 버튼에 표시할 아이콘입니다. 비워두면 프리팹의 SelectableEntity.portrait를 사용합니다.")]
    public Sprite icon;

    [TextArea]
    public string description;

    [Header("Build")]
    public GameObject prefab;

    [Tooltip("배치 확정 시 소비되는 Watt입니다.")]
    public int wattCost = 75;

    [Tooltip("배치되는 건물의 소유자 ID입니다.")]
    public int ownerId = 1;

    [Tooltip("더블클릭 시 같은 종류로 묶을 타입 ID입니다.")]
    public string entityTypeId;

    [Header("Production")]
    public ProductionRecipe recipe;

    public string BuildAssetName => name;
    public string DisplayName => displayName;
    public GameObject Prefab => prefab;
    // W03 절약형 건축법: 건설 Watt 비용 할인을 반영합니다.
    public int WattCost => RelicManager.Instance != null
        ? Mathf.Max(0, Mathf.RoundToInt(RelicManager.Instance.ApplyBonus(RelicEffectType.BuildWattCost, wattCost)))
        : wattCost;
    public int OwnerId => ownerId;
    public Sprite Icon => icon != null ? icon : BuildableIconResolver.ResolvePrefabPortrait(prefab);

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
