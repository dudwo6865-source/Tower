using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ProductionRecipe",
    menuName = "Tank/생산 레시피")]
public class ProductionRecipe : ScriptableObject
{
    [Header("유닛")]
    [Label("유닛 프리팹")]
    [Tooltip("생산할 유닛 프리팹입니다.")]
    public GameObject unitPrefab;

    [Header("표시")]
    [Label("표시 이름")]
    [Tooltip("생산 패널과, 이 레시피로 생산된 유닛을 선택했을 때 선택 정보 패널에 표시할 이름입니다. 비워두면 유닛 프리팹의 이름을 사용합니다.")]
    public string displayName;

    [Label("아이콘")]
    [Tooltip("생산 패널에 표시할 아이콘입니다. 비워두면 유닛 프리팹의 초상화를 사용합니다.")]
    public Sprite icon;

    [Label("설명")]
    [TextArea]
    public string description;

    [Header("타이밍")]
    [Label("첫 생산 대기(초)")]
    [Tooltip("건물 완공 후 첫 생산까지 대기 시간(초)입니다.")]
    public float initialSpawnDelay = 2f;

    [Label("생산 간격(초)")]
    [Tooltip("유닛 한 마리를 생산하는 간격(초)입니다. 낮/밤과 관계없이 이 속도로 생산합니다.")]
    public float spawnInterval = 8f;

    [Header("비용")]
    [Label("초당 Watt 소모량")]
    [Tooltip("생산이 진행되는 동안 초당 소모하는 Watt입니다. Watt가 부족하면 생산 진행이 멈춥니다. 0이면 소모하지 않습니다.")]
    [Min(0f)]
    public float wattCostPerSecond = 0f;

    [Header("인구")]
    [Label("인구수")]
    [Tooltip("유닛 한 마리가 차지하는 인구수입니다. 생산 건물의 최대 인구수 안에서만 생산합니다. 예: 건물 최대 인구 20, 인구수 1이면 20기까지 생산합니다. 0이면 인구를 차지하지 않습니다.")]
    [Min(0)]
    public int population = 1;

    [Header("해금 조건")]
    [Label("필요 건물")]
    [Tooltip("완공된 상태로 하나 이상 있어야 하는 건물입니다. 모두 충족해야 생산할 수 있습니다.")]
    public List<BuildingRequirement> requiredBuildings = new List<BuildingRequirement>();

    [Label("필요 업그레이드")]
    [Tooltip("연구해야 하는 업그레이드와 레벨입니다. 모두 충족해야 생산할 수 있습니다.")]
    public List<UpgradeRequirement> requiredUpgrades = new List<UpgradeRequirement>();

    // 유닛 한 마리를 만드는 데 드는 총 Watt입니다.
    public float TotalWattCost => Mathf.Max(0f, wattCostPerSecond) * Mathf.Max(0f, spawnInterval);

    public int RequirementCount
    {
        get
        {
            int count = 0;

            if (requiredBuildings != null)
            {
                for (int i = 0; i < requiredBuildings.Count; i++)
                {
                    if (requiredBuildings[i] != null && !requiredBuildings[i].IsEmpty)
                        count++;
                }
            }

            if (requiredUpgrades != null)
            {
                for (int i = 0; i < requiredUpgrades.Count; i++)
                {
                    if (requiredUpgrades[i] != null && requiredUpgrades[i].upgrade != null)
                        count++;
                }
            }

            return count;
        }
    }

    public bool HasRequirements => RequirementCount > 0;

    /// <summary>이 소유자가 지금 이 레시피를 생산할 수 있는지(해금 조건을 모두 채웠는지) 여부입니다.</summary>
    /// <param name="producer">생산하는 건물입니다. '생산 건물 자신' 범위의 건물 조건을 판정할 때 씁니다.</param>
    public bool IsUnlocked(int ownerId, SelectableEntity producer = null)
    {
        if (requiredBuildings != null)
        {
            for (int i = 0; i < requiredBuildings.Count; i++)
            {
                if (requiredBuildings[i] != null && !requiredBuildings[i].IsMet(ownerId, producer))
                    return false;
            }
        }

        if (requiredUpgrades != null)
        {
            for (int i = 0; i < requiredUpgrades.Count; i++)
            {
                if (requiredUpgrades[i] != null && !requiredUpgrades[i].IsMet(ownerId))
                    return false;
            }
        }

        return true;
    }

    /// <summary>아직 채우지 못한 해금 조건을 ", "로 이어 붙여 돌려줍니다.</summary>
    public string DescribeMissingRequirements(int ownerId, SelectableEntity producer = null)
    {
        return ProductionRequirementUtility.DescribeMissing(requiredUpgrades, requiredBuildings, ownerId, producer);
    }

    public string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        if (unitPrefab == null)
            return name;

        return SelectionInfoUtility.GetDisplayName(
            unitPrefab,
            SelectionInfoUtility.GetUnitData(unitPrefab));
    }

    public Sprite GetIcon()
    {
        if (icon != null || unitPrefab == null)
            return icon;

        return SelectionInfoUtility.GetPortrait(
            unitPrefab,
            SelectionInfoUtility.GetUnitData(unitPrefab));
    }
}
