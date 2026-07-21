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

        if (TryGetEnemyTarget(
                clickedEntity,
                manager.localPlayerOwnerId,
                out SelectableEntity enemy))
            IssueAttack(units, enemy);
        else
            IssueMove(units, hit.point);
    }

    public static bool HasCommandableUnits()
    {
        return TryGetCommandingUnits(out _);
    }

    public static bool TryGetAttackTarget(
        SelectableEntity entity,
        out SelectableEntity target)
    {
        target = null;

        if (entity == null)
            return false;

        EntityHealth health = entity.GetComponent<EntityHealth>();

        if (health == null || !health.IsAlive)
            return false;

        target = entity;
        return true;
    }

    public static bool TryGetEnemyTarget(
        SelectableEntity entity,
        int localOwnerId,
        out SelectableEntity target)
    {
        target = null;

        if (entity == null || entity.ownerId == localOwnerId)
            return false;

        return TryGetAttackTarget(entity, out target);
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

    public static bool IssueAttackToSelection(SelectableEntity target)
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return false;

        return IssueAttack(units, target);
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

        int localOwnerId = UnitSelectionManager.Instance.localPlayerOwnerId;

        foreach (SelectableEntity entity in UnitSelectionManager.Instance.GetSelectedEntities())
        {
            if (entity == null || entity.entityType != SelectableEntityType.Unit)
                continue;

            // 적 유닛이 선택돼 있어도 명령 대상에서 제외한다(정보용 단일 선택).
            if (entity.ownerId != localOwnerId)
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

    static bool IssueAttack(List<SelectableEntity> units, SelectableEntity target)
    {
        bool anyIssued = false;
        var attackTracks = new List<(UnitAttacker attacker, SelectableEntity target)>();

        foreach (SelectableEntity unit in units)
        {
            UnitCombatAI combatAI = unit.GetComponent<UnitCombatAI>();
            UnitAttacker attacker = unit.GetComponent<UnitAttacker>();

            if (combatAI == null || attacker == null)
                continue;

            UnitCommandDebugLog.Log(combatAI, $"플레이어 명령: 공격 -> {target.name}");

            combatAI.AttackTarget(target);
            attackTracks.Add((attacker, target));
            anyIssued = true;
        }

        if (!anyIssued)
            return false;

        UnitCommandIndicatorTracker.TrackAttackTarget(attackTracks, target);
        return true;
    }

    static bool IssueMove(List<SelectableEntity> units, Vector3 hitPoint)
    {
        bool anyIssued = false;
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
        }

        if (!anyIssued)
            return false;

        UnitCommandIndicatorTracker.TrackMoveToPoint(moveTracks, hitPoint);
        return true;
    }

    static bool IssueAttackMove(List<SelectableEntity> units, Vector3 hitPoint)
    {
        bool anyIssued = false;
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
        }

        if (!anyIssued)
            return false;

        UnitCommandIndicatorTracker.TrackMoveToPoint(
            moveTracks,
            hitPoint,
            MoveDestinationIndicator.AttackMoveColor);
        return true;
    }

    static bool IssuePatrol(List<SelectableEntity> units, Vector3 hitPoint)
    {
        bool anyIssued = false;
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
        }

        if (!anyIssued)
            return false;

        UnitCommandIndicatorTracker.TrackMoveToPoint(moveTracks, hitPoint);
        return true;
    }
}
