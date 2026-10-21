using UnityEngine;
using UnityEngine.AI;

public partial class UnitCombatAI
{
    [Header("대기 아군 비켜주기")]
    [Label("비켜주기 사용")]
    public bool allowIdleYield = true;
    [Label("비켜주기 검사 간격(초)")]
    [Min(0.1f)] public float yieldCheckInterval = 0.3f;
    [Label("비켜주기 요청 거리")]
    [Min(0.1f)] public float yieldRequestDistance = 2.5f;
    [Label("비켜주기 이동 거리")]
    [Min(0.1f)] public float yieldStepDistance = 1.5f;
    [Label("비켜주기 제한 시간(초)")]
    [Min(0.1f)] public float yieldDuration = 1.5f;
    [Label("비켜주기 재요청 대기(초)")]
    [Min(0f)] public float yieldCooldown = 2f;
    [Label("비켜주기 요청 속도 비율")]
    [Tooltip("실제 이동 속도가 최대 속도의 이 비율보다 느릴 때만 요청합니다.")]
    [Range(0f, 1f)] public float yieldSlowSpeedRatio = 0.25f;

    bool yielding;
    float yieldUntil;
    float nextYieldAllowed;
    float nextYieldCheck;
    float beforeYieldStoppingDistance;
    NavMeshPath yieldPath;

    protected override bool HasDirectMoveOrder => manualMoveActive && !attackMoveActive;
    protected override bool IsHoldingPosition => orderState == UnitOrderState.Hold;
    // 사거리 안에서 멈춰 공격 중이면 '공격 자리'가 먼저 걸리므로, 여기선 표적 유무만 본다.
    protected override bool IsApproachingAttackRange => attackMoveActive && HasValidTarget();

    bool CanYield => allowIdleYield && !yielding && Time.time >= nextYieldAllowed &&
        orderState == UnitOrderState.Free && !manualMoveActive && !manualFocusTarget &&
        !damageFocusTarget && !HasValidTarget() && agent != null && agent.enabled &&
        agent.isOnNavMesh && !agent.hasPath && !agent.pathPending && !HasCachedPath;

    void TryRequestAllyYield()
    {
        if (!allowIdleYield || !manualMoveActive ||
            (attackMoveActive && HasValidTarget()) || Time.time < nextYieldCheck ||
            agent == null || !agent.enabled || !agent.isOnNavMesh || !agent.hasPath ||
            agent.pathPending || agent.remainingDistance <= agent.stoppingDistance ||
            agent.velocity.sqrMagnitude > Mathf.Pow(agent.speed * yieldSlowSpeedRatio, 2f))
            return;
        nextYieldCheck = Time.time + Mathf.Max(0.1f, yieldCheckInterval);
        Vector3 direction = agent.steeringTarget - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        direction.Normalize();
        CollectCrowdNeighbors(transform.position, yieldRequestDistance);
        foreach (SelectableEntity entity in crowdNeighbors)
        {
            if (entity == null) continue;
            UnitCombatAI other = entity.GetComponent<UnitCombatAI>();
            if (other == null || !other.isActiveAndEnabled || !other.CanYield) continue;
            Vector3 offset = other.transform.position - transform.position;
            float forward = Vector3.Dot(offset, direction);
            Vector3 lateral = offset - direction * forward;
            float width = agent.radius + other.agent.radius;
            if (forward <= 0f || lateral.sqrMagnitude > width * width) continue;
            if (other.TryYield(direction)) break;
        }
    }

    bool TryYield(Vector3 trafficDirection)
    {
        if (!CanYield || !AiPathBudget.TryAcquireHeavy()) return false;
        CollectCrowdNeighbors(transform.position, yieldStepDistance + yieldRequestDistance);
        Vector3 side = Vector3.Cross(Vector3.up, trafficDirection);
        if ((GetInstanceID() & 1) == 0) side = -side;
        if (yieldPath == null) yieldPath = new NavMeshPath();
        NavMeshQueryFilter filter = new NavMeshQueryFilter
            { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        for (int i = 0; i < 2; i++)
        {
            Vector3 candidate = transform.position + side * yieldStepDistance * (i == 0 ? 1f : -1f);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, attackPositionSampleRadius, filter) ||
                Mathf.Abs(hit.position.y - transform.position.y) > attackPositionHeightTolerance ||
                (hit.position - transform.position).sqrMagnitude < agent.radius * agent.radius ||
                NavMesh.Raycast(transform.position, hit.position, out _, filter) ||
                !IsCrowdPositionFree(hit.position, false) ||
                !agent.CalculatePath(hit.position, yieldPath) ||
                yieldPath.status != NavMeshPathStatus.PathComplete)
                continue;
            beforeYieldStoppingDistance = agent.stoppingDistance;
            agent.stoppingDistance = 0f;
            agent.isStopped = false;
            if (!agent.SetPath(yieldPath))
            {
                agent.stoppingDistance = beforeYieldStoppingDistance;
                continue;
            }
            yielding = true;
            yieldUntil = Time.time + yieldDuration;
            nextYieldAllowed = yieldUntil + yieldCooldown;
            return true;
        }
        nextYieldAllowed = Time.time + yieldCooldown;
        return false;
    }

    bool UpdateYieldMovement()
    {
        if (!yielding) return false;
        TickRetarget();
        if (!allowIdleYield || HasValidTarget() || manualMoveActive ||
            orderState != UnitOrderState.Free || agent == null || !agent.enabled ||
            !agent.isOnNavMesh || Time.time >= yieldUntil ||
            (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.radius)))
        {
            CancelYieldMovement();
            return false;
        }
        return true;
    }

    protected override void OnDisable()
    {
        CancelYieldMovement();
        nextYieldCheck = 0f;
        nextYieldAllowed = 0f;
        base.OnDisable();
    }

    void CancelYieldMovement(bool clearPath = true)
    {
        if (!yielding) return;
        yielding = false;
        if (agent == null) return;
        agent.stoppingDistance = beforeYieldStoppingDistance;
        if (clearPath && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
    }
}
