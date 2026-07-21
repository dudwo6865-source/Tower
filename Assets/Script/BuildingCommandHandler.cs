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

        return IssueRallyPointToSelection(hit.point);
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

    public static bool CanAttack(SelectableEntity entity)
    {
        if (entity == null)
            return false;

        return entity.GetComponent<TowerAI>() != null &&
            entity.GetComponent<UnitAttacker>() != null;
    }

    public static bool CanSetRallyPoint(SelectableEntity entity)
    {
        return entity != null && entity.GetComponent<ProductionBuilding>() != null;
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

            if (towerAI != null)
                towerAI.CommandStop();
        }
    }

    static bool IssueAttack(List<SelectableEntity> buildings, SelectableEntity target)
    {
        bool anyIssued = false;
        var attackTracks = new List<(UnitAttacker attacker, SelectableEntity target)>();

        foreach (SelectableEntity building in buildings)
        {
            TowerAI towerAI = building.GetComponent<TowerAI>();
            UnitAttacker attacker = building.GetComponent<UnitAttacker>();

            if (towerAI == null || attacker == null)
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

            production.SetRallyPoint(rallyPoint);
            anyIssued = true;
        }

        if (!anyIssued)
            return false;

        MoveDestinationIndicator.ShowAt(rallyPoint);
        return true;
    }
}
