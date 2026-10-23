using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static class BuildingCommandHandler
{
    public static bool HasCommandableBuildings()
    {
        return TryGetCommandingBuildings(out _);
    }

    public static bool ShouldShowBuildingCommandPanel()
    {
        if (UnitCommandHandler.HasCommandableUnits())
            return false;

        return HasCommandableBuildings();
    }

    public static bool TryGetCommandingBuildings(out List<SelectableEntity> buildings)
    {
        buildings = new List<SelectableEntity>();

        if (UnitSelectionManager.Instance == null)
            return false;

        foreach (SelectableEntity entity in UnitSelectionManager.Instance.GetSelectedEntities())
        {
            if (!IsCommandableBuilding(entity))
                continue;

            buildings.Add(entity);
        }

        return buildings.Count > 0;
    }

    public static void IssueStopToSelection()
    {
        if (!TryGetCommandingBuildings(out List<SelectableEntity> buildings))
            return;

        IssueStop(buildings);
    }

    public static bool IssueAttackToSelection(SelectableEntity target)
    {
        if (!TryGetCommandingBuildings(out List<SelectableEntity> buildings))
            return false;

        return IssueAttack(buildings, target);
    }

    /// <summary>공격 대상을 지정할 수 있는 타워가 선택돼 있는지 여부입니다.</summary>
    public static bool HasAttackableTowersSelected()
    {
        if (!TryGetCommandingBuildings(out List<SelectableEntity> buildings))
            return false;

        foreach (SelectableEntity building in buildings)
        {
            if (CanAttack(building))
                return true;
        }

        return false;
    }

    public static bool IssueRallyPointToSelection(Vector3 hitPoint)
    {
        if (!TryGetProductionBuildings(out List<SelectableEntity> buildings))
            return false;

        return IssueRallyPoint(buildings, hitPoint);
    }

    public static bool TryIssueRallyPointFromRightClick()
    {
        if (UnitCommandHandler.HasCommandableUnits())
            return false;

        if (!HasProductionBuildingsSelected())
            return false;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return false;

        if (Camera.main == null)
            return false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return false;

        // 타워와 함께 선택했을 때 적을 우클릭하면 집결지가 아니라 타워 공격 명령이다.
        if (HasAttackableTowersSelected() &&
            UnitCommandHandler.TryGetEnemyTarget(
                hit.collider.GetComponentInParent<SelectableEntity>(),
                UnitSelectionManager.Instance.localPlayerOwnerId,
                out _))
            return false;

        if (!IssueRallyPointToSelection(hit.point))
            return false;

        UnitCommandController.PlayRightClickConfirmPulse(hit.point, UnitCommandMode.RallyPoint);
        return true;
    }

    public static bool HasProductionBuildingsSelected()
    {
        return TryGetProductionBuildings(out _);
    }

    public static bool TryGetProductionBuildings(out List<SelectableEntity> buildings)
    {
        buildings = new List<SelectableEntity>();

        if (UnitSelectionManager.Instance == null)
            return false;

        foreach (SelectableEntity entity in UnitSelectionManager.Instance.GetSelectedEntities())
        {
            if (!CanSetRallyPoint(entity))
                continue;

            if (!entity.CanBeSelectedBy(UnitSelectionManager.Instance.localPlayerOwnerId))
                continue;

            buildings.Add(entity);
        }

        return buildings.Count > 0;
    }

    /// <summary>
    /// 생산 패널 표시 여부를 정하고, 표시할 레시피 목록과 기준 건물을 함께 채웁니다.
    /// 여러 생산 건물을 선택했으면 첫 번째 건물의 목록을 보여줍니다.
    /// </summary>
    public static bool ShouldShowProductionPanel(
        List<ProductionRecipe> results,
        out ProductionBuilding primary)
    {
        results.Clear();
        primary = null;

        if (UnitCommandHandler.HasCommandableUnits())
            return false;

        if (!TryGetProductionBuildings(out List<SelectableEntity> buildings))
            return false;

        foreach (SelectableEntity building in buildings)
        {
            ProductionBuilding production = building.GetComponent<ProductionBuilding>();

            if (production == null)
                continue;

            IReadOnlyList<ProductionRecipe> options = production.RecipeOptions;

            if (options.Count == 0)
                continue;

            for (int i = 0; i < options.Count; i++)
                results.Add(options[i]);

            primary = production;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 선택된 생산 건물 중 이 레시피를 고를 수 있는 건물 모두의 생산 레시피를 바꿉니다.
    /// </summary>
    public static bool IssueRecipeToSelection(ProductionRecipe recipe)
    {
        if (recipe == null)
            return false;

        if (!TryGetProductionBuildings(out List<SelectableEntity> buildings))
            return false;

        bool changed = false;

        foreach (SelectableEntity building in buildings)
        {
            ProductionBuilding production = building.GetComponent<ProductionBuilding>();

            if (production != null && production.SelectRecipe(recipe))
                changed = true;
        }

        return changed;
    }

    /// <summary>선택된 생산 건물이 모두 일시정지 상태인지 여부입니다.</summary>
    public static bool AreSelectedProductionsPaused()
    {
        if (!TryGetProductionBuildings(out List<SelectableEntity> buildings))
            return false;

        foreach (SelectableEntity building in buildings)
        {
            ProductionBuilding production = building.GetComponent<ProductionBuilding>();

            if (production != null && !production.IsPaused)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 선택된 생산 건물의 일시정지를 전환합니다.
    /// 하나라도 생산 중이면 모두 멈추고, 모두 멈춰 있으면 모두 재개합니다.
    /// </summary>
    public static bool ToggleProductionPauseForSelection()
    {
        if (!TryGetProductionBuildings(out List<SelectableEntity> buildings))
            return false;

        bool pause = !AreSelectedProductionsPaused();

        foreach (SelectableEntity building in buildings)
        {
            ProductionBuilding production = building.GetComponent<ProductionBuilding>();

            if (production != null)
                production.SetPaused(pause);
        }

        return true;
    }

    public static bool CanAttack(SelectableEntity entity)
    {
        if (entity == null)
            return false;

        if (BuildingConstructionGate.IsFeatureLockedOn(entity))
            return false;

        return entity.GetComponent<TowerAI>() != null &&
            entity.GetComponent<UnitAttacker>() != null;
    }

    public static bool CanSetRallyPoint(SelectableEntity entity)
    {
        if (entity == null || entity.GetComponent<ProductionBuilding>() == null)
            return false;

        return !BuildingConstructionGate.IsFeatureLockedOn(entity);
    }

    public static bool CanUpgrade(SelectableEntity entity)
    {
        return BuildingUpgradeService.CanUpgrade(entity);
    }

    /// <summary>
    /// 업그레이드 패널 표시 여부를 정하고, 표시할 분기 목록을 함께 채웁니다.
    /// ready는 건설 잠금이 풀려 실제로 누를 수 있는 상태인지를 나타냅니다.
    /// </summary>
    public static bool ShouldShowUpgradePanel(List<BuildingUpgradeOption> results, out bool ready)
    {
        results.Clear();
        ready = false;

        if (UnitCommandHandler.HasCommandableUnits())
            return false;

        if (!TryGetUpgradableBuildings(out List<SelectableEntity> buildings))
            return false;

        foreach (SelectableEntity building in buildings)
        {
            if (!BuildingUpgradeService.TryGetOptions(building, results))
                continue;

            ready = !BuildingUpgradeService.IsConstructionLocked(building);
            return true;
        }

        return false;
    }

    /// <summary>선택된 건물 중 첫 번째 업그레이드 가능한 건물의 분기 목록을 채웁니다.</summary>
    public static bool TryGetUpgradeOptions(List<BuildingUpgradeOption> results)
    {
        results.Clear();

        if (!TryGetUpgradableBuildings(out List<SelectableEntity> buildings))
            return false;

        foreach (SelectableEntity building in buildings)
        {
            if (BuildingUpgradeService.TryGetOptions(building, results))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 선택된 건물 중 이 분기를 가진 것을 모두 교체합니다.
    /// 같은 종류를 여러 개 골랐을 때 한 번에 올라가고, 섞여 있으면 해당하는 것만 바뀝니다.
    /// </summary>
    public static bool IssueUpgradeToSelection(IBuildablePlacementData targetTier)
    {
        if (targetTier == null)
            return false;

        if (!TryGetUpgradableBuildings(out List<SelectableEntity> buildings))
            return false;

        var upgraded = new List<SelectableEntity>();

        foreach (SelectableEntity building in buildings)
        {
            SelectableEntity result = BuildingUpgradeService.UpgradeTo(building, targetTier);

            if (result != null)
                upgraded.Add(result);
        }

        if (upgraded.Count == 0)
            return false;

        UnitSelectionManager.Instance?.SelectOnly(upgraded);
        return true;
    }

    static bool TryGetUpgradableBuildings(out List<SelectableEntity> buildings)
    {
        buildings = new List<SelectableEntity>();

        if (UnitSelectionManager.Instance == null)
            return false;

        foreach (SelectableEntity entity in UnitSelectionManager.Instance.GetSelectedEntities())
        {
            // CanUpgrade가 건물 종류와 소유자까지 함께 검사합니다.
            if (CanUpgrade(entity))
                buildings.Add(entity);
        }

        return buildings.Count > 0;
    }

    static bool IsCommandableBuilding(SelectableEntity entity)
    {
        if (entity == null || entity.entityType != SelectableEntityType.Building)
            return false;

        if (UnitSelectionManager.Instance != null &&
            !entity.CanBeSelectedBy(UnitSelectionManager.Instance.localPlayerOwnerId))
            return false;

        return CanAttack(entity) || CanSetRallyPoint(entity);
    }

    static void IssueStop(List<SelectableEntity> buildings)
    {
        foreach (SelectableEntity building in buildings)
        {
            TowerAI towerAI = building.GetComponent<TowerAI>();

            if (towerAI != null && !BuildingConstructionGate.IsFeatureLockedOn(building))
                towerAI.CommandStop();
        }
    }

    static bool IssueAttack(List<SelectableEntity> buildings, SelectableEntity target)
    {
        bool anyIssued = false;
        var attackTracks = new List<(UnitAttacker attacker, SelectableEntity target)>();

        foreach (SelectableEntity building in buildings)
        {
            // 자기 자신은 공격하지 않는다.
            if (building == target)
                continue;

            TowerAI towerAI = building.GetComponent<TowerAI>();
            UnitAttacker attacker = building.GetComponent<UnitAttacker>();

            if (towerAI == null || attacker == null)
                continue;

            if (BuildingConstructionGate.IsFeatureLockedOn(building))
                continue;

            towerAI.CommandAttackTarget(target);
            attackTracks.Add((attacker, target));
            anyIssued = true;
        }

        if (!anyIssued)
            return false;

        UnitCommandIndicatorTracker.TrackAttackTarget(attackTracks, target);
        return true;
    }

    static bool IssueRallyPoint(List<SelectableEntity> buildings, Vector3 hitPoint)
    {
        Vector3 rallyPoint = UnitSpawnUtility.SampleNavMeshPosition(hitPoint);
        bool anyIssued = false;

        foreach (SelectableEntity building in buildings)
        {
            ProductionBuilding production = building.GetComponent<ProductionBuilding>();

            if (production == null)
                continue;

            if (BuildingConstructionGate.IsFeatureLockedOn(building))
                continue;

            production.SetRallyPoint(rallyPoint);
            anyIssued = true;
        }

        if (!anyIssued)
            return false;

        MoveDestinationIndicator.ShowAt(rallyPoint);
        return true;
    }
}
