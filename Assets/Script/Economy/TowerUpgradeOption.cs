using UnityEngine;

/// <summary>
/// 한 타워에서 갈라지는 업그레이드 분기 하나입니다.
/// </summary>
[System.Serializable]
public class TowerUpgradeOption
{
    [Tooltip("업그레이드 시 교체될 상위 타워입니다.")]
    public BuildableTowerData tier;

    [Tooltip("이 분기에 소비되는 Watt입니다. 0 이하면 tier의 건설 비용을 그대로 씁니다.")]
    public int wattCost;

    public bool IsValid => tier != null && tier.Prefab != null;

    public string DisplayName => tier != null ? tier.DisplayName : string.Empty;

    public Sprite Icon => tier != null ? tier.Icon : null;

    public int ResolveCost()
    {
        if (tier == null)
            return 0;

        return wattCost > 0 ? wattCost : tier.WattCost;
    }
}
