using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치된 건물(타워·생산 건물)을 같은 칸에서 상위 프리팹으로 교체합니다.
/// 분기 목록은 건물을 만든 데이터(BuildableTowerData / BuildableProductionData)의 upgradeOptions에서 읽습니다.
/// 그리드 점유를 먼저 비워야 새 건물이 같은 칸에 등록될 수 있습니다.
/// 생산 건물은 교체 후 집결지·생산한 유닛·생산 중인 레시피·일시정지를 새 건물로 넘깁니다.
/// </summary>
public static class BuildingUpgradeService
{
    // 버튼 상태 검사는 매 프레임 돌기 때문에, 설정 오류 경고는 에셋당 한 번만 남깁니다.
    static readonly HashSet<IBuildablePlacementData> warnedTiers =
        new HashSet<IBuildablePlacementData>();

    // 분기 조회용 임시 버퍼. 매 프레임 할당하지 않도록 재사용한다.
    static readonly List<BuildingUpgradeOption> optionBuffer =
        new List<BuildingUpgradeOption>();

    /// <summary>건설 잠금 중에도 true입니다. 패널을 띄워두고 버튼만 비활성화하기 위함입니다.</summary>
    public static bool CanUpgrade(SelectableEntity building)
    {
        if (!TryResolveCurrent(
                building,
                false,
                out IUpgradableBuildableData current,
                out GridFootprint footprint,
                out _))
            return false;

        optionBuffer.Clear();
        current.GetUpgradeOptions(optionBuffer);

        for (int i = 0; i < optionBuffer.Count; i++)
        {
            if (IsUsableOption(optionBuffer[i], footprint, building))
                return true;
        }

        return false;
    }

    /// <summary>선택 가능한 분기를 results에 채웁니다. 프레임마다 호출되므로 버퍼를 재사용합니다.</summary>
    public static bool TryGetOptions(
        SelectableEntity building,
        List<BuildingUpgradeOption> results)
    {
        results.Clear();

        if (!TryResolveCurrent(
                building,
                false,
                out IUpgradableBuildableData current,
                out GridFootprint footprint,
                out _))
            return false;

        optionBuffer.Clear();
        current.GetUpgradeOptions(optionBuffer);

        for (int i = 0; i < optionBuffer.Count; i++)
        {
            BuildingUpgradeOption option = optionBuffer[i];

            if (IsUsableOption(option, footprint, building))
                results.Add(option);
        }

        return results.Count > 0;
    }

    /// <summary>설치·업그레이드 직후 잠금 중이면 true입니다.</summary>
    public static bool IsConstructionLocked(SelectableEntity building)
    {
        return BuildingConstructionGate.IsFeatureLockedOn(building);
    }

    public static bool CanAfford(BuildingUpgradeOption option)
    {
        if (option == null)
            return false;

        return WattManager.Instance == null ||
            WattManager.Instance.CanAfford(option.ResolveCost());
    }

    /// <summary>교체에 성공하면 새 건물을, 실패하면 null을 돌려줍니다. 선택 갱신은 호출자가 합니다.</summary>
    public static SelectableEntity UpgradeTo(
        SelectableEntity building,
        IBuildablePlacementData targetTier)
    {
        if (targetTier == null)
            return null;

        if (!TryResolveCurrent(
                building,
                true,
                out IUpgradableBuildableData current,
                out GridFootprint footprint,
                out TowerPlacementController placement))
            return null;

        BuildingUpgradeOption option = FindOption(current, targetTier, footprint, building);

        if (option == null)
            return null;

        int cost = option.ResolveCost();

        if (WattManager.Instance != null && !WattManager.Instance.CanAfford(cost))
        {
            placement?.PlayFailedPlacementFeedback();
            return null;
        }

        Vector2Int originCell = footprint.RegisteredOrigin;
        Vector3 position = building.transform.position;

        int localPlayerOwnerId = UnitSelectionManager.Instance != null
            ? UnitSelectionManager.Instance.localPlayerOwnerId
            : building.ownerId;

        // 옛 건물이 칸과 NavMesh를 잡고 있으면 새 건물 등록(TryOccupy)이 실패한다.
        footprint.Release();
        BuildingSpawnUtility.DisableNavMeshObstacles(building.gameObject);

        GameObject upgraded = BuildingSpawnUtility.Spawn(
            option.Target,
            originCell,
            position,
            localPlayerOwnerId,
            placement != null ? placement.defaultFeatureLockDuration : 2f,
            placement != null ? placement.defaultPlaceAnimationTrigger : "Place",
            skipTerrainChecks: true);

        if (upgraded == null)
        {
            footprint.RegisterAtOriginCell(originCell, true);
            return null;
        }

        WattManager.Instance?.TrySpend(cost);

        TransferProductionState(building.gameObject, upgraded);

        // EntityHealth.Die()를 타면 사망 연출과 처치 보상이 발생하므로 조용히 제거한다.
        Object.Destroy(building.gameObject);

        return upgraded.GetComponent<SelectableEntity>();
    }

    // 생산 건물끼리의 업그레이드면 옛 건물의 생산 상태를 새 건물로 넘긴다.
    static void TransferProductionState(GameObject previous, GameObject upgraded)
    {
        ProductionBuilding from = previous.GetComponent<ProductionBuilding>();
        ProductionBuilding to = upgraded.GetComponent<ProductionBuilding>();

        if (from != null && to != null)
            from.TransferStateTo(to);
    }

    static BuildingUpgradeOption FindOption(
        IUpgradableBuildableData current,
        IBuildablePlacementData targetTier,
        GridFootprint footprint,
        SelectableEntity context)
    {
        optionBuffer.Clear();
        current.GetUpgradeOptions(optionBuffer);

        for (int i = 0; i < optionBuffer.Count; i++)
        {
            BuildingUpgradeOption option = optionBuffer[i];

            if (option == null || option.Target != targetTier)
                continue;

            return IsUsableOption(option, footprint, context) ? option : null;
        }

        return null;
    }

    // requireUnlocked: 실제 교체만 건설 잠금을 지킵니다. 조회는 잠금 중에도 통과시켜
    // 업그레이드 직후 패널이 사라졌다가 다시 뜨는 깜빡임을 없앱니다.
    static bool TryResolveCurrent(
        SelectableEntity building,
        bool requireUnlocked,
        out IUpgradableBuildableData current,
        out GridFootprint footprint,
        out TowerPlacementController placement)
    {
        current = null;
        footprint = null;
        placement = TowerPlacementController.Instance;

        if (building == null || building.entityType != SelectableEntityType.Building)
            return false;

        if (requireUnlocked && BuildingConstructionGate.IsFeatureLockedOn(building))
            return false;

        if (UnitSelectionManager.Instance != null &&
            !building.CanBeSelectedBy(UnitSelectionManager.Instance.localPlayerOwnerId))
            return false;

        current = BuildingSourceData.ResolveUpgradableData(building);

        if (current == null || !current.HasUpgradeOptions)
            return false;

        footprint = building.GetComponent<GridFootprint>();

        return footprint != null && footprint.IsRegistered;
    }

    // 자기 자신으로의 순환은 데이터의 GetUpgradeOptions에서 이미 걸렀다.
    static bool IsUsableOption(
        BuildingUpgradeOption option,
        GridFootprint footprint,
        SelectableEntity context)
    {
        if (option == null || !option.IsValid)
            return false;

        IBuildablePlacementData tier = option.Target;

        if (tier.Prefab.scene.IsValid())
        {
            WarnOnce(
                tier,
                context,
                $"'{tier.BuildAssetName}' references a scene object. Assign a Project prefab instead.");

            return false;
        }

        if (tier.GetFootprintCells() != footprint.footprintCells)
        {
            WarnOnce(
                tier,
                context,
                $"'{tier.BuildAssetName}' footprint {tier.GetFootprintCells()} differs from " +
                $"{footprint.footprintCells}. 업그레이드 분기는 같은 칸 수여야 합니다.");

            return false;
        }

        // 같은 자리에 그대로 교체하므로 칸 모양(십자·ㄱ자 등)도 같아야 한다.
        if (!FootprintShape.SameShape(
                tier.GetFootprintCells(),
                GridFootprint.ResolveFootprintMask(tier.Prefab),
                footprint.footprintCells,
                footprint.ActiveMask))
        {
            WarnOnce(
                tier,
                context,
                $"'{tier.BuildAssetName}'의 칸 모양이 현재 건물과 다릅니다. 업그레이드 분기는 같은 칸 모양이어야 합니다.");

            return false;
        }

        return true;
    }

    static void WarnOnce(IBuildablePlacementData tier, Object context, string message)
    {
        if (!warnedTiers.Add(tier))
            return;

        Debug.LogWarning($"BuildingUpgradeService: {message}", context);
    }
}
