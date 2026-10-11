using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>생산 레시피를 해금하는 데 필요한 업그레이드 레벨입니다.</summary>
[System.Serializable]
public class UpgradeRequirement
{
    [Label("업그레이드")]
    [Tooltip("연구해야 하는 업그레이드입니다.")]
    public UpgradeDefinition upgrade;

    [Label("필요 레벨")]
    [Tooltip("이 레벨 이상 연구해야 해금됩니다. 0 이하면 1로 취급합니다.")]
    public int minLevel = 1;

    public int RequiredLevel => Mathf.Max(1, minLevel);

    public bool IsMet(int ownerId)
    {
        if (upgrade == null)
            return true;

        UpgradeManager manager = UpgradeManager.Instance;

        // 업그레이드는 플레이어 한 명분만 기록되므로 다른 소유자는 충족하지 못한다.
        if (manager == null || manager.playerOwnerId != ownerId)
            return false;

        return manager.GetLevel(upgrade) >= RequiredLevel;
    }

    public string Describe()
    {
        if (upgrade == null)
            return string.Empty;

        string upgradeName = string.IsNullOrWhiteSpace(upgrade.displayName)
            ? upgrade.name
            : upgrade.displayName;

        return $"{upgradeName} Lv{RequiredLevel}";
    }
}

/// <summary>필요 건물을 어디서 찾을지 정합니다.</summary>
public enum BuildingRequirementScope
{
    [InspectorName("맵 전체")]
    Anywhere,

    [InspectorName("생산 건물 자신")]
    ProducerOnly,
}

/// <summary>생산 레시피를 해금하는 데 필요한 건물입니다. 완공된 건물이 하나 이상 있어야 합니다.</summary>
[System.Serializable]
public class BuildingRequirement
{
    [Label("건물 데이터")]
    [Tooltip("BuildableTowerData 또는 BuildableProductionData를 지정합니다. 이 데이터로 지은 건물이나 거기서 업그레이드한 상위 건물이 있어야 충족합니다. 씬에 미리 놓아 원본 데이터가 없는 건물은 같은 타입 ID면 충족합니다.")]
    public ScriptableObject buildingData;

    [Label("타입 ID")]
    [Tooltip("건물 데이터를 비웠을 때 비교할 entityTypeId입니다. 본부처럼 배치 데이터가 없는 건물에 씁니다.")]
    public string entityTypeId;

    [Label("찾는 범위")]
    [Tooltip("맵 전체: 맵 어딘가에 조건 건물이 있으면 충족합니다.\n생산 건물 자신: 이 레시피를 생산하는 건물 자체가 조건 건물(또는 그 상위 단계)이어야 충족합니다. 예) 배럭 3 조건이면 배럭 3에서만 생산하고, 다른 배럭 1은 배럭 3이 있어도 생산하지 못합니다.")]
    public BuildingRequirementScope scope = BuildingRequirementScope.Anywhere;

    public IBuildablePlacementData Data => buildingData as IBuildablePlacementData;

    public bool IsEmpty => Data == null && string.IsNullOrWhiteSpace(entityTypeId);

    public string ResolveTypeId()
    {
        IBuildablePlacementData data = Data;
        return data != null ? data.GetEntityTypeId() : entityTypeId;
    }

    /// <param name="producer">이 레시피를 생산하는 건물입니다. '생산 건물 자신' 범위에서만 씁니다.</param>
    public bool IsMet(int ownerId, SelectableEntity producer = null)
    {
        if (IsEmpty)
            return true;

        string typeId = ResolveTypeId();
        CollectAcceptedData();

        // 생산 건물 자신만 본다. 건설 중이면 어차피 생산이 멈춰 있으므로 완공 여부는 따지지 않는다.
        if (scope == BuildingRequirementScope.ProducerOnly)
            return producer != null && Matches(producer, typeId);

        IReadOnlyList<SelectableEntity> buildings = BuildingRegistry.Buildings;

        for (int i = 0; i < buildings.Count; i++)
        {
            SelectableEntity building = buildings[i];

            if (building == null || building.ownerId != ownerId)
                continue;

            if (!Matches(building, typeId))
                continue;

            EntityHealth health = building.GetComponent<EntityHealth>();

            if (health != null && !health.IsAlive)
                continue;

            // 건설 연출 중인 건물은 아직 완공되지 않은 것으로 본다.
            if (BuildingConstructionGate.IsFeatureLockedOn(building))
                continue;

            return true;
        }

        return false;
    }

    bool Matches(SelectableEntity building, string typeId)
    {
        if (buildingData != null)
        {
            BuildingSourceData source = building.GetComponent<BuildingSourceData>();

            // 원본 데이터가 기록된 건물은 데이터로만 비교한다.
            // 배럭 1·2·3단계처럼 타입 ID를 공유하는 단계끼리 하위 단계가 상위 단계를 충족시키지 않게 하기 위해서다.
            if (source != null && source.sourceData != null)
                return acceptedData.Contains(source.sourceData);
        }

        // 원본 데이터가 없는 건물(씬에 미리 놓은 건물 등)과 데이터를 비운 조건은 타입 ID로 비교한다.
        return !string.IsNullOrWhiteSpace(typeId) && building.entityTypeId == typeId;
    }

    static readonly HashSet<ScriptableObject> acceptedData = new HashSet<ScriptableObject>();
    static readonly Stack<ScriptableObject> pendingData = new Stack<ScriptableObject>();
    static readonly List<BuildingUpgradeOption> optionBuffer = new List<BuildingUpgradeOption>();

    // 조건 건물과, 거기서 업그레이드로 도달할 수 있는 모든 상위 단계를 모은다.
    // 배럭 3이 있으면 배럭 1·2 조건도 충족한 것으로 본다.
    void CollectAcceptedData()
    {
        acceptedData.Clear();
        pendingData.Clear();

        if (buildingData == null)
            return;

        acceptedData.Add(buildingData);
        pendingData.Push(buildingData);

        while (pendingData.Count > 0)
        {
            if (!(pendingData.Pop() is IUpgradableBuildableData upgradable))
                continue;

            optionBuffer.Clear();
            upgradable.GetUpgradeOptions(optionBuffer);

            for (int i = 0; i < optionBuffer.Count; i++)
            {
                // 업그레이드가 순환해도 한 번 모은 데이터는 다시 따라가지 않는다.
                if (optionBuffer[i].Target is ScriptableObject next && acceptedData.Add(next))
                    pendingData.Push(next);
            }
        }

        optionBuffer.Clear();
    }

    public string Describe()
    {
        // 단계별 데이터(배럭 1·2·3)는 표시 이름이 같을 수 있으므로 데이터 에셋 이름으로 구분한다.
        string buildingName = buildingData != null ? buildingData.name : entityTypeId;

        return scope == BuildingRequirementScope.ProducerOnly
            ? $"{buildingName}(이 건물)"
            : buildingName;
    }
}

public static class ProductionRequirementUtility
{
    static readonly StringBuilder builder = new StringBuilder();

    /// <summary>충족하지 못한 조건을 ", "로 이어 붙인 문자열입니다. 모두 충족하면 빈 문자열입니다.</summary>
    public static string DescribeMissing(
        List<UpgradeRequirement> upgrades,
        List<BuildingRequirement> buildings,
        int ownerId,
        SelectableEntity producer = null)
    {
        builder.Clear();

        if (buildings != null)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingRequirement requirement = buildings[i];

                if (requirement != null && !requirement.IsMet(ownerId, producer))
                    Append(requirement.Describe());
            }
        }

        if (upgrades != null)
        {
            for (int i = 0; i < upgrades.Count; i++)
            {
                UpgradeRequirement requirement = upgrades[i];

                if (requirement != null && !requirement.IsMet(ownerId))
                    Append(requirement.Describe());
            }
        }

        return builder.ToString();
    }

    static void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (builder.Length > 0)
            builder.Append(", ");

        builder.Append(text);
    }
}
