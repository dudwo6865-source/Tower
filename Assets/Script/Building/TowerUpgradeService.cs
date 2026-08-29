using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치된 타워를 같은 칸에서 상위 프리팹으로 교체합니다.
/// 분기 목록은 건물을 만든 BuildableTowerData의 upgradeOptions에서 읽습니다.
/// 그리드 점유를 먼저 비워야 새 건물이 같은 칸에 등록될 수 있습니다.
/// </summary>
public static class TowerUpgradeService
{
    // 버튼 상태 검사는 매 프레임 돌기 때문에, 설정 오류 경고는 에셋당 한 번만 남깁니다.
    static readonly HashSet<BuildableTowerData> warnedTiers =
        new HashSet<BuildableTowerData>();

    /// <summary>건설 잠금 중에도 true입니다. 패널을 띄워두고 버튼만 비활성화하기 위함입니다.</summary>
    public static bool CanUpgrade(SelectableEntity building)
    {
        if (!TryResolveCurrent(
                building,
                false,
                out BuildableTowerData current,
                out GridFootprint footprint,
                out _))
            return false;

        for (int i = 0; i < current.upgradeOptions.Count; i++)
        {
            if (IsUsableOption(current, current.upgradeOptions[i], footprint, building))
                return true;
        }

        return false;
    }

    /// <summary>선택 가능한 분기를 results에 채웁니다. 프레임마다 호출되므로 버퍼를 재사용합니다.</summary>
    public static bool TryGetOptions(
        SelectableEntity building,
        List<TowerUpgradeOption> results)
    {
        results.Clear();

        if (!TryResolveCurrent(
                building,
                false,
                out BuildableTowerData current,
                out GridFootprint footprint,
                out _))
            return false;

        for (int i = 0; i < current.upgradeOptions.Count; i++)
        {
            TowerUpgradeOption option = current.upgradeOptions[i];

            if (IsUsableOption(current, option, footprint, building))
                results.Add(option);
        }

        return results.Count > 0;
    }

    /// <summary>설치·업그레이드 직후 잠금 중이면 true입니다.</summary>
    public static bool IsConstructionLocked(SelectableEntity building)
    {
        return BuildingConstructionGate.IsFeatureLockedOn(building);
    }

    public static bool CanAfford(TowerUpgradeOption option)
    {
        if (option == null)
            return false;

        return WattManager.Instance == null ||
            WattManager.Instance.CanAfford(option.ResolveCost());
    }

    /// <summary>교체에 성공하면 새 건물을, 실패하면 null을 돌려줍니다. 선택 갱신은 호출자가 합니다.</summary>
    public static SelectableEntity UpgradeTo(
        SelectableEntity building,
        BuildableTowerData targetTier)
    {
        if (targetTier == null)
            return null;

        if (!TryResolveCurrent(
                building,
                true,
                out BuildableTowerData current,
                out GridFootprint footprint,
                out TowerPlacementController placement))
            return null;

        TowerUpgradeOption option = FindOption(current, targetTier, footprint, building);

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
            option.tier,
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

        // EntityHealth.Die()를 타면 사망 연출과 처치 보상이 발생하므로 조용히 제거한다.
        Object.Destroy(building.gameObject);

        return upgraded.GetComponent<SelectableEntity>();
    }

    static TowerUpgradeOption FindOption(
        BuildableTowerData current,
        BuildableTowerData targetTier,
        GridFootprint footprint,
        SelectableEntity context)
    {
        for (int i = 0; i < current.upgradeOptions.Count; i++)
        {
            TowerUpgradeOption option = current.upgradeOptions[i];

            if (option == null || option.tier != targetTier)
                continue;

            return IsUsableOption(current, option, footprint, context) ? option : null;
        }

        return null;
    }

    // requireUnlocked: 실제 교체만 건설 잠금을 지킵니다. 조회는 잠금 중에도 통과시켜
    // 업그레이드 직후 패널이 사라졌다가 다시 뜨는 깜빡임을 없앱니다.
    static bool TryResolveCurrent(
        SelectableEntity building,
        bool requireUnlocked,
        out BuildableTowerData current,
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

        current = BuildingSourceData.ResolveTowerData(building);

        if (current == null || !current.HasUpgradeOptions)
            return false;

        footprint = building.GetComponent<GridFootprint>();

        return footprint != null && footprint.IsRegistered;
    }

    static bool IsUsableOption(
        BuildableTowerData current,
        TowerUpgradeOption option,
        GridFootprint footprint,
        SelectableEntity context)
    {
        if (!current.IsUsableOption(option))
            return false;

        BuildableTowerData tier = option.tier;

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

        return true;
    }

    static void WarnOnce(BuildableTowerData tier, Object context, string message)
    {
        if (!warnedTiers.Add(tier))
            return;

        Debug.LogWarning($"TowerUpgradeService: {message}", context);
    }
}
