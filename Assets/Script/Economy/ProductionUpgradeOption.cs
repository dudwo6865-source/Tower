using UnityEngine;

/// <summary>
/// 한 생산 건물에서 갈라지는 업그레이드 분기 하나입니다.
/// </summary>
[System.Serializable]
public class ProductionUpgradeOption : BuildingUpgradeOption
{
    [Label("상위 생산 건물")]
    [Tooltip("업그레이드 시 교체될 상위 생산 건물 데이터입니다. 레시피 목록·최대 인구수는 이 데이터 값으로 바뀝니다.")]
    public BuildableProductionData tier;

    [Label("Watt 비용")]
    [Tooltip("이 분기에 소비되는 Watt입니다. 0 이하면 tier의 건설 비용을 그대로 씁니다.")]
    public int wattCost;

    public override IBuildablePlacementData Target => tier;

    protected override int WattCostOverride => wattCost;
}
