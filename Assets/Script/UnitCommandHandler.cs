using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public static class UnitCommandHandler
{
    // Profiler에서 명령 처리 비용을 나눠 보기 위한 마커.
    static readonly ProfilerMarker ArrivalSlotMarker = new ProfilerMarker("Tank.CommandArrivalSlots");
    static readonly ProfilerMarker CommandPathMarker = new ProfilerMarker("Tank.CommandPath");

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

    // 도착점만 분산하고 이동 중 대열은 강제하지 않는다.
    // 후보 자리의 NavMesh 검사는 유닛 종류(에이전트 타입·칸 크기·반경)마다 명령당 한 번만 하고,
    // 각 유닛은 검사가 끝난 후보 중에서 고르기만 한다.
    // (예전에는 유닛마다 후보 전체를 다시 검사해 유닛 수의 제곱만큼 NavMesh 질의가 일어났다.)
    sealed class ArrivalPlanner
    {
        sealed class SlotSet
        {
            public Vector3 fallback;
            public bool anchorValid;
            public float clearanceSqr;
            public readonly List<Vector3> positions = new List<Vector3>();
        }

        readonly Vector3 center;
        readonly List<Vector3> rawSlots;
        readonly Dictionary<(int, int, Vector2Int, float), SlotSet> sets =
            new Dictionary<(int, int, Vector2Int, float), SlotSet>();

        public ArrivalPlanner(List<SelectableEntity> units, Vector3 center)
        {
            this.center = center;
            rawSlots = BuildArrivalSlots(units, center);
        }

        public Vector3 Choose(NavMeshAgent agent, Vector2Int footprint)
        {
            using (ArrivalSlotMarker.Auto())
            {
                float radius = Mathf.Max(agent.radius, 0.1f);
                SlotSet set = GetOrBuildSet(agent, footprint, radius);

                if (!set.anchorValid || set.positions.Count == 0)
                    return set.fallback;

                Vector3 agentPos = agent.transform.position;
                float agentDistSqr = Mathf.Max(1f, (agentPos - center).sqrMagnitude);
                int bestIndex = -1;
                float bestScore = float.PositiveInfinity;

                for (int i = 0; i < set.positions.Count; i++)
                {
                    Vector3 p = set.positions[i];
                    // 목적지 가까운 자리를 우선하고, 같은 거리라면 유닛과 가까운 쪽을 고른다.
                    float score = (p - center).sqrMagnitude + (p - agentPos).sqrMagnitude / agentDistSqr;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    bestIndex = i;
                }

                if (bestIndex < 0)
                    return set.fallback;

                Vector3 best = set.positions[bestIndex];
                Reserve(best, radius * 2f);
                return best;
            }
        }

        // 고른 자리 주변 후보를 모든 종류의 후보 목록에서 지워 다른 유닛과 겹치지 않게 한다.
        void Reserve(Vector3 position, float clearance)
        {
            float chooserSqr = clearance * clearance;

            foreach (SlotSet set in sets.Values)
            {
                float limitSqr = Mathf.Max(chooserSqr, set.clearanceSqr);
                List<Vector3> list = set.positions;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if ((list[i] - position).sqrMagnitude >= limitSqr) continue;
                    int last = list.Count - 1;
                    list[i] = list[last];
                    list.RemoveAt(last);
                }
            }
        }

        SlotSet GetOrBuildSet(NavMeshAgent agent, Vector2Int footprint, float radius)
        {
            var key = (agent.agentTypeID, agent.areaMask, footprint, radius);
            if (sets.TryGetValue(key, out SlotSet set))
                return set;

            set = new SlotSet();
            sets[key] = set;
            set.fallback = GridMovement.SnapMoveDestination(center, footprint);
            set.clearanceSqr = radius * 2f * radius * 2f;

            if (rawSlots.Count == 0)
                return set;

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(set.fallback, out NavMeshHit anchor, radius, filter))
                return set;

            set.anchorValid = true;

            for (int i = 0; i < rawSlots.Count; i++)
            {
                Vector3 candidate = GridMovement.SnapMoveDestination(rawSlots[i], footprint);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, filter)) continue;
                // 명령점에서 벽이나 절벽을 넘어야 하는 자리는 배정하지 않는다.
                if (NavMesh.Raycast(anchor.position, hit.position, out _, filter)) continue;

                // 스냅으로 같은 칸에 모인 후보는 하나만 남긴다.
                bool duplicate = false;
                foreach (Vector3 existing in set.positions)
                    if ((existing - hit.position).sqrMagnitude < 0.0001f) { duplicate = true; break; }
                if (!duplicate)
                    set.positions.Add(hit.position);
            }

            return set;
        }
    }

    static List<Vector3> BuildArrivalSlots(List<SelectableEntity> units, Vector3 center)
    {
        var slots = new List<Vector3>();
        UnitCommandController settings = UnitCommandController.Instance;
        if (units.Count < 2 || (settings != null && !settings.spreadMoveDestinations)) return slots;
        float diameter = 0f;
        foreach (SelectableEntity unit in units)
        {
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null) diameter = Mathf.Max(diameter, agent.radius * 2f);
        }
        float spacing = Mathf.Max(diameter + (settings != null ? Mathf.Max(0f, settings.arrivalSpacing) : 0.5f),
            MapGrid.Instance != null ? MapGrid.Instance.cellSize : diameter);
        spacing = Mathf.Max(0.1f, spacing);
        int count = units.Count * (settings != null ? Mathf.Clamp(settings.arrivalSearchMultiplier, 1, 8) : 3);
        slots.Add(center);
        for (int ring = 1; slots.Count < count; ring++)
            for (int x = -ring; x <= ring && slots.Count < count; x++)
                for (int z = -ring; z <= ring && slots.Count < count; z++)
                    if (Mathf.Abs(x) == ring || Mathf.Abs(z) == ring)
                        slots.Add(center + new Vector3(x * spacing, 0f, z * spacing));
        return slots;
    }

    static bool IssueMove(List<SelectableEntity> units, Vector3 hitPoint)
    {
        PerfProbe.BeginCommand("이동");
        bool anyIssued = false;
        var moveTracks = new List<(NavMeshAgent agent, Vector3 destination)>();

        var arrivalPlanner = new ArrivalPlanner(units, hitPoint);

        foreach (SelectableEntity unit in units)
        {
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();

            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            Vector2Int footprint = GridMovement.GetFootprintCells(unit);
            Vector3 destination = arrivalPlanner.Choose(agent, footprint);

            bool pathSet;
            using (CommandPathMarker.Auto())
                pathSet = GridMovement.TrySetAgentDestination(agent, destination, immediate: true);

            if (!pathSet)
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

        var arrivalPlanner = new ArrivalPlanner(units, hitPoint);

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
            Vector3 destination = arrivalPlanner.Choose(agent, footprint);

            bool started;
            using (CommandPathMarker.Auto())
                started = combatAI.BeginAttackMove(destination);

            if (!started)
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
