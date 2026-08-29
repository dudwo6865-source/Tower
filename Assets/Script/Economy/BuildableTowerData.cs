using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BuildableTowerData",
    menuName = "Tank/Buildable Tower Data")]
public class BuildableTowerData : ScriptableObject, IBuildablePlacementData
{
    [Header("Display")]
    public string displayName = "Tower";

    [Tooltip("빌드 버튼에 표시할 아이콘입니다. 비워두면 프리팹의 SelectableEntity.portrait를 사용합니다.")]
    public Sprite icon;

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

    [Header("Upgrade")]
    [Tooltip("업그레이드 분기 목록입니다. 여러 개를 넣으면 플레이어가 갈래를 고를 수 있습니다. " +
        "각 분기 프리팹의 칸 수는 이 타워와 같아야 합니다.")]
    public List<TowerUpgradeOption> upgradeOptions = new List<TowerUpgradeOption>();

    public bool HasUpgradeOptions
    {
        get
        {
            if (upgradeOptions == null)
                return false;

            for (int i = 0; i < upgradeOptions.Count; i++)
            {
                if (IsUsableOption(upgradeOptions[i]))
                    return true;
            }

            return false;
        }
    }

    /// <summary>자기 자신으로의 순환은 무한 교체가 되므로 분기에서 제외합니다.</summary>
    public bool IsUsableOption(TowerUpgradeOption option)
    {
        return option != null && option.IsValid && option.tier != this;
    }

    public string BuildAssetName => name;
    public string DisplayName => displayName;
    public GameObject Prefab => prefab;
    public int WattCost => wattCost;
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
