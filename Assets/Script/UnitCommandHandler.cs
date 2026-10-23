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

        bool hasUnits = TryGetCommandingUnits(out List<SelectableEntity> units);
        bool hasTowers = BuildingCommandHandler.HasAttackableTowersSelected();

        if (!hasUnits && !hasTowers)
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
        {
            // 타워도 유닛처럼 우클릭한 적을 공격 대상으로 삼는다.
            // 대상 표시가 유닛 쪽을 따라가도록 타워를 먼저 처리한다.
            bool towerIssued = hasTowers && BuildingCommandHandler.IssueAttackToSelection(enemy);
            bool unitIssued = hasUnits && IssueAttack(units, enemy);

            if (towerIssued || unitIssued)
                UnitCommandController.PlayRightClickConfirmPulse(hit.point, UnitCommandMode.Attack);

            return;
        }

        // 타워는 움직일 수 없으므로 지면 우클릭은 유닛에게만 이동 명령으로 전달한다.
        if (hasUnits && IssueMove(units, hit.point))
            UnitCommandController.PlayRightClickConfirmPulse(hit.point, UnitCommandMode.Move);
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

    // 아래 *ToSelection 명령들은 명령 버튼·단축키(명령 모드 포함)로 직접 내린 명령이라
    // 명령 사운드를 낸다. 우클릭 기본 명령(TryIssueCommandToSelection)은 소리를 내지 않는다.
    public static void IssueStopToSelection()
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return;

        IssueStop(units);
        PlayCommandSound(units);
    }

    public static void IssueHoldToSelection()
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return;

        IssueHold(units);
        PlayCommandSound(units);
    }

    public static bool IssueMoveToSelection(Vector3 hitPoint)
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return false;

        return PlayCommandSoundIf(IssueMove(units, hitPoint), units);
    }

    public static bool IssueAttackToSelection(SelectableEntity target)
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return false;

        return PlayCommandSoundIf(IssueAttack(units, target), units);
    }

    public static bool IssueAttackMoveToSelection(Vector3 hitPoint)
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return false;

        return PlayCommandSoundIf(IssueAttackMove(units, hitPoint), units);
    }

    public static bool IssuePatrolToSelection(Vector3 hitPoint)
    {
        if (!TryGetCommandingUnits(out List<SelectableEntity> units))
            return false;

        return PlayCommandSoundIf(IssuePatrol(units, hitPoint), units);
    }

    static void PlayCommandSound(List<SelectableEntity> units)
    {
        UnitSound.PlayVoiceForGroup(units, UnitSoundVoice.Command);
    }

    static bool PlayCommandSoundIf(bool issued, List<SelectableEntity> units)
    {
        if (issued)
            PlayCommandSound(units);

        return issued;
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

            // 밤 복귀 이동 중인 유닛은 추가 명령을 무시한다.
            ProducedUnitMarker marker = entity.GetComponent<ProducedUnitMarker>();

            if (marker != null && marker.IsRecalling)
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
        PerfProbe.BeginCommand("공격");
        bool anyIssued = false;
        var attackTracks = new List<(UnitAttacker attacker, SelectableEntity target)>();

        foreach (SelectableEntity unit in units)
        {
            // 아군 강제 공격은 허용하지만 자기 자신은 공격하지 않는다.
            if (unit == target)
                continue;

            UnitCombatAI combatAI = unit.GetComponent<UnitCombatAI>();
            UnitAttacker attacker = unit.GetComponent<UnitAttacker>();

            if (combatAI == null || attacker == null)
                continue;

            UnitCommandDebugLog.Log(combatAI, $"플레이어 명령: 공격 -> {target.name}");

            combatAI.AttackTarget(target);
            PerfProbe.TrackCommandAgent(unit.GetComponent<NavMeshAgent>());
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
        PerfProbe.BeginCommand("이동");
        bool anyIssued = false;
        var moveTracks = new List<(NavMeshAgent agent, Vector3 destination)>();

        foreach (SelectableEntity unit in units)
        {
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();

            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            Vector2Int footprint = GridMovement.GetFootprintCells(unit);
            Vector3 destination = GridMovement.SnapMoveDestination(hitPoint, footprint);

            if (!GridMovement.TrySetAgentDestination(agent, destination, immediate: true))
                continue;

            UnitCombatAI combatAI = unit.GetComponent<UnitCombatAI>();

            if (combatAI != null)
            {
                UnitCommandDebugLog.Log(combatAI, $"플레이어 명령: 이동 -> ({destination.x:F1}, {destination.y:F1}, {destination.z:F1})");
                combatAI.BeginManualMove(destination);
            }
            else
                agent.isStopped = false;

            PerfProbe.TrackCommandAgent(agent);
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
        PerfProbe.BeginCommand("공격 이동");
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

            PerfProbe.TrackCommandAgent(agent);
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
        PerfProbe.BeginCommand("정찰");
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

            PerfProbe.TrackCommandAgent(agent);
            moveTracks.Add((agent, destination));
            anyIssued = true;
        }

        if (!anyIssued)
            return false;

        UnitCommandIndicatorTracker.TrackMoveToPoint(moveTracks, hitPoint);
        return true;
    }
}
