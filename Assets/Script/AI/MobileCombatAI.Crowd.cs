using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public abstract partial class MobileCombatAI
{
    [Header("행동별 회피 우선순위")]
    [Label("동적 우선순위 사용")]
    public bool dynamicAvoidancePriority = true;
    [Label("직접 이동 우선순위")]
    [Tooltip("숫자가 작을수록 우선합니다. 직접 이동 명령은 즉시 반영합니다.")]
    [Range(0, 99)] public int directMovePriority = 20;
    [Label("공격 자리 우선순위")]
    [Range(0, 99)] public int attackPositionPriority = 35;
    [Label("공격 이동 사거리 접근 우선순위")]
    [Tooltip("공격 이동 중 표적이 사거리 밖이라 공격 가능 거리까지 다가갈 때의 우선순위입니다. 공격 자리 우선순위보다 작으면 이미 멈춰 공격 중인 아군을 밀고 들어갑니다.")]
    [Range(0, 99)] public int attackMoveApproachPriority = 30;
    [Label("추격 우선순위")]
    [Range(0, 99)] public int chasePriority = 50;
    [Label("일반 이동 우선순위")]
    [Range(0, 99)] public int travelPriority = 65;
    [Label("대기 우선순위")]
    [Range(0, 99)] public int idlePriority = 85;
    [Label("우선순위 전환 대기(초)")]
    [Min(0f)] public float priorityChangeDelay = 0.4f;
    [Label("유닛별 고정 편차")]
    [Range(0, 10)] public int priorityVariation = 3;

    [Header("적 공격 위치 분산")]
    [Label("공격 위치 분산 사용")]
    [Tooltip("적이 목표 근처에서 분대 경로 대신 빈 공격 위치로 접근합니다.")]
    public bool spreadAttackPositions = true;
    [Label("분산 시작 여유 거리")]
    [Min(0f)] public float spreadApproachDistance = 6f;
    [Label("공격 위치 후보 수")]
    [Range(3, 16)] public int attackPositionCandidates = 8;
    [Label("공격 위치 사거리 비율")]
    [Range(0.1f, 0.9f)] public float attackPositionRangeRatio = 0.65f;
    [Label("공격 위치 간격 여유")]
    [Min(0f)] public float attackPositionPadding = 0.25f;
    [Label("공격 위치 샘플 반경")]
    [Min(0.05f)] public float attackPositionSampleRadius = 0.6f;
    [Label("공격 위치 높이 여유")]
    [Min(0f)] public float attackPositionHeightTolerance = 1f;

    int lastAttackPositionFrame = -10;
    int pendingPriority = -1;
    float priorityPendingSince;
    bool usingDynamicPriority;
    protected readonly List<SelectableEntity> crowdNeighbors = new List<SelectableEntity>(32);
    NavMeshPath crowdPath;
    bool reservedAttackPosition;
    bool issuedSpreadApproach;
    Vector3 attackPosition;
    SelectableEntity attackPositionTarget;

    protected virtual bool HasDirectMoveOrder => false;
    protected virtual bool IsHoldingPosition => false;
    // 공격 이동 중 표적을 잡았지만 아직 사거리 밖이라 다가가는 중인지.
    protected virtual bool IsApproachingAttackRange => false;

    protected virtual void OnDisable()
    {
        reservedAttackPosition = false;
        issuedSpreadApproach = false;
        pendingPriority = -1;
        lastAttackPositionFrame = -10;
    }

    protected bool ShouldSpreadAttackPosition => this is EnemyCombatAI &&
        spreadAttackPositions && HasValidTarget() && attacker != null &&
        GetHorizontalBoundsGap(currentTarget) <= attacker.AttackRange + spreadApproachDistance;

    protected void RefreshDirectMovePriority()
    {
        if (dynamicAvoidancePriority && agent != null)
        {
            usingDynamicPriority = true;
            SetCrowdPriority(directMovePriority);
        }
    }

    void SetCrowdPriority(int value)
    {
        int variation = Mathf.Clamp(priorityVariation, 0, 10);
        int offset = (int)((uint)GetInstanceID() % (uint)(variation * 2 + 1)) - variation;
        int desired = Mathf.Clamp(value + offset, 0, 99);
        if (agent.avoidancePriority != desired)
            agent.avoidancePriority = desired;
    }

    void UpdateCrowdPriority()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;
        if (!ShouldSpreadAttackPosition || attackPositionTarget != currentTarget)
            reservedAttackPosition = false;
        if (!dynamicAvoidancePriority)
        {
            if (usingDynamicPriority) ApplyRandomAvoidancePriority();
            usingDynamicPriority = false;
            return;
        }
        usingDynamicPriority = true;

        int desired = HasDirectMoveOrder ? directMovePriority :
            IsHoldingPosition || Time.frameCount - lastAttackPositionFrame <= 1 ? attackPositionPriority :
            IsApproachingAttackRange ? attackMoveApproachPriority :
            HasValidTarget() ? chasePriority :
            agent.hasPath || HasCachedPath ? travelPriority : idlePriority;
        if (desired != pendingPriority)
        {
            pendingPriority = desired;
            priorityPendingSince = Time.time;
        }
        if (HasDirectMoveOrder || Time.time - priorityPendingSince >= priorityChangeDelay)
            SetCrowdPriority(desired);
    }

    // 공간 검색이 없는 테스트 씬에서도 같은 조건으로 주변 아군을 모은다.
    protected void CollectCrowdNeighbors(Vector3 origin, float radius)
    {
        crowdNeighbors.Clear();
        if (selfEntity == null)
            return;
        if (SpatialQueryWorld.Instance != null)
        {
            SpatialQueryWorld.Instance.CollectAlliesInRange(origin, selfEntity.ownerId,
                radius, selfEntity, crowdNeighbors);
            return;
        }
        foreach (SelectableEntity entity in SelectableRegistry.Entities)
            if (entity != null && entity != selfEntity && entity.ownerId == selfEntity.ownerId &&
                (entity.transform.position - origin).sqrMagnitude <= radius * radius)
                crowdNeighbors.Add(entity);
    }

    protected bool IsCrowdPositionFree(Vector3 point, bool checkReservations)
    {
        foreach (SelectableEntity entity in crowdNeighbors)
        {
            if (entity == null || !entity.isActiveAndEnabled)
                continue;
            MobileCombatAI other = entity.GetComponent<MobileCombatAI>();
            if (other == null || !other.isActiveAndEnabled || other.agent == null)
                continue;
            float spacing = agent.radius + other.agent.radius + attackPositionPadding;
            if ((other.transform.position - point).sqrMagnitude < spacing * spacing)
                return false;
            if (!checkReservations && other.agent.enabled && other.agent.isOnNavMesh &&
                other.agent.hasPath && (other.agent.destination - point).sqrMagnitude < spacing * spacing)
                return false;
            if (checkReservations && other.reservedAttackPosition &&
                other.currentTarget == currentTarget && other.attackPositionTarget == currentTarget &&
                (other.attackPosition - point).sqrMagnitude < spacing * spacing)
                return false;
        }
        return true;
    }

    bool TryGetSpreadDestination(out Vector3 destination)
    {
        destination = default;
        reservedAttackPosition = false;
        if (!ShouldSpreadAttackPosition || agent == null || !agent.isOnNavMesh)
            return false;

        Bounds bounds = currentTarget.SelectionBounds;
        float range = Mathf.Max(0.05f, attacker.AttackRange * attackPositionRangeRatio,
            attacker.minAttackRange + attackPositionPadding);
        float extent = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
        CollectCrowdNeighbors(bounds.center, extent + attacker.AttackRange + spreadApproachDistance);
        Vector3 toward = transform.position - bounds.center;
        float start = Mathf.Atan2(toward.z, toward.x);
        int count = Mathf.Clamp(attackPositionCandidates, 3, 16);
        if (crowdPath == null) crowdPath = new NavMeshPath();
        for (int i = 0; i < count; i++)
        {
            // 가까운 쪽부터 좌우로 번갈아 탐색한다.
            int step = (i + 1) / 2 * (i % 2 == 0 ? -1 : 1);
            float angle = start + step * Mathf.PI * 2f / count;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 outside = bounds.center + direction * (extent + range + agent.radius);
            Vector3 candidate = bounds.ClosestPoint(outside) + direction * (range + agent.radius);
            candidate.y = transform.position.y;
            NavMeshQueryFilter filter = new NavMeshQueryFilter
                { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                    Mathf.Max(0.05f, attackPositionSampleRadius), filter) ||
                Mathf.Abs(hit.position.y - transform.position.y) > attackPositionHeightTolerance ||
                !IsCrowdPositionFree(hit.position, true))
                continue;
            Vector3 gap = hit.position - bounds.ClosestPoint(hit.position);
            gap.y = 0f;
            if (gap.magnitude > attacker.AttackRange + agent.radius ||
                gap.magnitude < attacker.minAttackRange + agent.radius ||
                !agent.CalculatePath(hit.position, crowdPath) ||
                crowdPath.status != NavMeshPathStatus.PathComplete)
                continue;
            destination = hit.position;
            attackPosition = destination;
            attackPositionTarget = currentTarget;
            reservedAttackPosition = true;
            return true;
        }
        // 빈자리를 찾지 못하면 기존 접근 및 막힘 복구를 사용한다.
        return false;
    }
}
