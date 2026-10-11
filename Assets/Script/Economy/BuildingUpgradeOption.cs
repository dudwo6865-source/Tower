using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건물 하나에서 갈라지는 업그레이드 분기 하나의 공통 부분입니다.
/// 타워는 TowerUpgradeOption, 생산 건물은 ProductionUpgradeOption을 씁니다.
/// </summary>
[System.Serializable]
public abstract class BuildingUpgradeOption
{
    /// <summary>업그레이드 시 교체될 상위 건물 데이터입니다.</summary>
    public abstract IBuildablePlacementData Target { get; }

    /// <summary>분기 전용 Watt 비용입니다. 0 이하면 상위 건물의 건설 비용을 씁니다.</summary>
    protected abstract int WattCostOverride { get; }

    public bool IsValid => Target != null && Target.Prefab != null;

    public string DisplayName => Target != null ? Target.DisplayName : string.Empty;

    public Sprite Icon => Target != null ? Target.Icon : null;

    public int ResolveCost()
    {
        if (Target == null)
            return 0;

        return WattCostOverride > 0 ? WattCostOverride : Target.WattCost;
    }
}

/// <summary>
/// 업그레이드 분기를 가진 건물 데이터입니다. 타워 데이터와 생산 건물 데이터가 구현합니다.
/// </summary>
public interface IUpgradableBuildableData : IBuildablePlacementData
{
    bool HasUpgradeOptions { get; }

    /// <summary>쓸 수 있는 분기(자기 자신으로의 순환 제외)를 results 끝에 붙입니다.</summary>
    void GetUpgradeOptions(List<BuildingUpgradeOption> results);
}
