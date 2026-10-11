using UnityEngine;

/// <summary>
/// 한 타워에서 갈라지는 업그레이드 분기 하나입니다.
/// </summary>
[System.Serializable]
public class TowerUpgradeOption : BuildingUpgradeOption
{
    [Label("상위 타워")]
    [Tooltip("업그레이드 시 교체될 상위 타워입니다.")]
    public BuildableTowerData tier;

    [Label("Watt 비용")]
    [Tooltip("이 분기에 소비되는 Watt입니다. 0 이하면 tier의 건설 비용을 그대로 씁니다.")]
    public int wattCost;

    public override IBuildablePlacementData Target => tier;

    protected override int WattCostOverride => wattCost;
}
