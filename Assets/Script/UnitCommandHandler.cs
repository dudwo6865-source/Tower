using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public static class UnitCommandHandler
{
    public static void TryIssueCommandToSelection()
    {
        UnitSelectionManager manager = UnitSelectionManager.Instance;
        if (manager == null || Camera.main == null)
            return;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        SelectableEntity clickedEntity =
            hit.collider.GetComponentInParent<SelectableEntity>();

        if (TryGetEnemyTarget(clickedEntity, manager.localPlayerOwnerId, out SelectableEntity enemy))
            IssueAttack(units, enemy);
        else
            IssueMove(units, hit.point);
    }

    public static bool HasCommandableUnits()
    {
        return TryGetCommandingUnits(out _);
    }

    public static bool TryGetEnemyTarget(
        SelectableEntity entity,
        int localOwnerId,
        out SelectableEntity enemy)
    {
        enemy = null;

        if (entity == null || entity.ownerId == localOwnerId)
            return false;

        EntityHealth health = entity.GetComponent<EntityHealth>();

        if (health != null && !health.IsAlive)
            return false;

        enemy = entity;
        return true;
    }

    public static void IssueStopToSelection()
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return;

        IssueStop(units);
    }

    public static void IssueHoldToSelection()
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return;

        IssueHold(units);
    }

    public static bool IssueMoveToSelection(Vector3 hitPoint)
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return false;

        return IssueMove(units, hitPoint);
    }

    public static bool IssueAttackToSelection(SelectableEntity enemy)
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return false;

        return IssueAttack(units, enemy);
    }

    public static bool IssueAttackMoveToSelection(Vector3 hitPoint)
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return false;

        return IssueAttackMove(units, hitPoint);
    }

    public static bool IssuePatrolToSelection(Vector3 hitPoint)
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return false;

        return IssuePatrol(units, hitPoint);
    }

    static bool TryGetCommandingUnits(out List<SelectableEntity> units)
    {
        units = new List<SelectableEntity>();

        if (UnitSelectionManager.Instance == null)
            return false;

        foreach (SelectableEntity entity in UnitSelectionManager.Instance.GetSelectedEntities())
        {
            if (entity == null || entity.entityType != SelectableEntityType.Unit)
                continue;

            if (entity.GetComponent<NavMeshAgent>() == null)
                continue;

            units.Add(entity);
        }

        return units.Count > 0;
    }

    static void IssueStop(List<SelectableEntity> units)
    {
        foreach (SelectableEntity unit in units)
        {
            UnitCombatAI combatAI = unit.GetComponent<UnitCombatAI>();
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();

            UnitCommandDebugLog.Log(combatAI, "플레이어 명령: 정지");

            if (combatAI != null)
                combatAI.IssueStop();
            else if (agent != null && agent.isOnNavMesh)
                agent.ResetPath();

            if (agent != null)
                agent.velocity = Vector3.zero;
        }

        UnitCommandIndicatorTracker.ClearTracking();
    }

    static void IssueHold(List<SelectableEntity> units)
    {
        foreach (SelectableEntity unit in units)
        {
            UnitCombatAI combatAI = unit.GetComponent<UnitCombatAI>();
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();

            if (combatAI != null)
                combatAI.IssueHold();
            else if (agent != null)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }

        UnitCommandIndicatorTracker.ClearTracking();
    }

    static bool IssueAttack(List<SelectableEntity> units, SelectableEntity enemy)
    {
        bool anyIssued = false;
        var attackTracks = new List<(UnitAttacker attacker, SelectableEntity target)>();

        foreach (SelectableEntity unit in units)
        {
            UnitCombatAI combatAI = unit.GetComponent<UnitCombatAI>();
            UnitAttacker attacker = unit.GetComponent<UnitAttacker>();

            if (combatAI == null || attacker == null)
                continue;

            UnitCommandDebugLog.Log(combatAI, $"플레이어 명령: 공격 -> {enemy.name}");

            combatAI.AttackTarget(enemy);
            attackTracks.Add((attacker, enemy));
            anyIssued = true;
        }

        if (!anyIssued)
            return false;

        UnitCommandIndicatorTracker.TrackAttackTarget(attackTracks, enemy);
        return true;
    }

    static bool IssueMove(List<SelectableEntity> units, Vector3 hitPoint)
    {
        bool anyIssued = false;
        Vector3? indicatorPosition = null;
        var moveTracks = new List<(NavMeshAgent agent, Vector3 destination)>();

        foreach (SelectableEntity unit in units)
        {
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();

            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            Vector2Int footprint = GridMovement.GetFootprintCells(unit);
            Vector3 destination = GridMovement.SnapMoveDestination(hitPoint, footprint);

            if (!GridMovement.TrySetAgentDestination(agent, destination))
                continue;

            UnitCombatAI combatAI = unit.GetComponent<UnitCombatAI>();

            if (combatAI != null)
            {
                UnitCommandDebugLog.Log(combatAI, $"플레이어 명령: 이동 -> ({destination.x:F1}, {destination.y:F1}, {destination.z:F1})");
                combatAI.BeginManualMove();
            }
            else
                agent.isStopped = false;

            moveTracks.Add((agent, destination));
            anyIssued = true;
            indicatorPosition ??= destination;
        }

        if (!anyIssued || !indicatorPosition.HasValue)
            return false;

        UnitCommandIndicatorTracker.TrackMoveToPoint(moveTracks, indicatorPosition.Value);
        return true;
    }

    static bool IssueAttackMove(List<SelectableEntity> units, Vector3 hitPoint)
    {
        bool anyIssued = false;
        Vector3? indicatorPosition = null;
        var moveTracks = new List<(NavMeshAgent agent, Vector3 destination)>();

        foreach (SelectableEntity unit in units)
        {
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();

            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            UnitCombatAI combatAI = unit.GetComponent<UnitCombatAI>();
            UnitAttacker attacker = unit.GetComponent<UnitAttacker>();

            if (combatAI == null || attacker == null)
                continue;

            Vector2Int footprint = GridMovement.GetFootprintCells(unit);
            Vector3 destination = GridMovement.SnapMoveDestination(hitPoint, footprint);

            if (!combatAI.BeginAttackMove(destination))
                continue;

            moveTracks.Add((agent, destination));
            anyIssued = true;
            indicatorPosition ??= destination;
        }

        if (!anyIssued || !indicatorPosition.HasValue)
            return false;

        UnitCommandIndicatorTracker.TrackMoveToPoint(moveTracks, indicatorPosition.Value);
        return true;
    }

    static bool IssuePatrol(List<SelectableEntity> units, Vector3 hitPoint)
    {
        bool anyIssued = false;
        Vector3? indicatorPosition = null;
        var moveTracks = new List<(NavMeshAgent agent, Vector3 destination)>();

        foreach (SelectableEntity unit in units)
        {
            UnitCombatAI combatAI = unit.GetComponent<UnitCombatAI>();
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();

            if (combatAI == null || agent == null)
                continue;

            Vector2Int footprint = GridMovement.GetFootprintCells(unit);
            Vector3 destination = GridMovement.SnapMoveDestination(hitPoint, footprint);

            if (!combatAI.IssuePatrol(destination))
                continue;

            moveTracks.Add((agent, destination));
            anyIssued = true;
            indicatorPosition ??= destination;
        }

        if (!anyIssued || !indicatorPosition.HasValue)
            return false;

        UnitCommandIndicatorTracker.TrackMoveToPoint(moveTracks, indicatorPosition.Value);
        return true;
    }
}
