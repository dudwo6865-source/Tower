using UnityEngine;
using UnityEngine.AI;

public enum UnitOrderState
{
    Free,
    Stopped,
    Hold,
    Patrol
}

/// <summary>
/// 플레이어 유닛 전투 AI. Move / Attack-Move / Stop / Hold / Patrol 명령을 처리합니다.
/// </summary>
public class UnitCombatAI : MobileCombatAI
{
    [Header("Orders")]
    [Tooltip("순찰 지점 도착로 판정 거리입니다.")]
    public float patrolArrivalDistance = 1.25f;

    bool manualMoveActive;
    bool manualFocusTarget;
    bool attackMoveActive;
    Vector3 attackMoveDestination;

    // 목적지를 아는 이동 명령은 거리로 도착을 판정한다.
    // 경로가 아직 안 잡힌 상태(hasPath=false)를 '도착'으로 오해해 명령이 한 프레임 만에
    // 취소되던 문제가 있었다. (생산 집결지·야간 복귀처럼 경로 설정이 한 프레임 밀리는 경우)
    bool hasManualDestination;
    Vector3 manualDestination;
    UnitOrderState orderState = UnitOrderState.Free;
    Vector3 holdAnchor;
    Vector3 patrolStart;
    Vector3 patrolEnd;
    bool patrolTowardEnd = true;

    void Update()
    {
        if (orderState == UnitOrderState.Stopped)
        {
            StopAgentMovement();
            return;
        }

        if (manualMoveActive)
        {
            if (!UpdateManualMove())
                return;
        }

        if (orderState == UnitOrderState.Hold)
        {
            MaintainHoldPosition();
            UpdateHoldCombat();
            return;
        }

        if (orderState == UnitOrderState.Patrol)
        {
            UpdatePatrol();
            return;
        }

        if (manualFocusTarget || damageFocusTarget)
        {
            if (!HasValidTarget())
            {
                manualFocusTarget = false;
                ClearDamageFocusTarget();
            }
        }
        else
        {
            TickRetarget();
        }

        if (!HasValidTarget())
            return;

        UpdateCombat();
    }

    bool UpdateManualMove()
    {
        if (attackMoveActive)
            TickRetarget();

        // 공격 이동은 교전 중에도 명령을 유지하고, 끝난 뒤 목적지로 이어간다.
        if (attackMoveActive && HasValidTarget())
            return true;

        if (ReachedManualDestination())
        {
            manualMoveActive = false;
            attackMoveActive = false;
            hasManualDestination = false;
            return true;
        }

        if (attackMoveActive)
            ResumeAttackMoveIfNeeded();

        return false;
    }

    bool ReachedManualDestination()
    {
        if (!agent.isOnNavMesh)
            return true;

        if (attackMoveActive)
            return IsWithinFlatDistance(attackMoveDestination, 0.75f);

        if (hasManualDestination)
        {
            return IsWithinFlatDistance(
                manualDestination,
                Mathf.Max(0.75f, agent.stoppingDistance + 0.1f));
        }

        if (agent.pathPending)
            return false;

        if (!agent.hasPath)
            return true;

        return agent.remainingDistance <= agent.stoppingDistance + 0.1f;
    }

    bool IsWithinFlatDistance(Vector3 destination, float distance)
    {
        Vector3 flat = transform.position - destination;
        flat.y = 0f;
        return flat.sqrMagnitude <= distance * distance;
    }

    void ResumeAttackMoveIfNeeded()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        bool alreadyGoing =
            (agent.hasPath || agent.pathPending) &&
            (agent.destination - attackMoveDestination).sqrMagnitude <= 1f;

        if (alreadyGoing)
            return;

        agent.stoppingDistance = 0.15f;
        GridMovement.TrySetAgentDestination(agent, attackMoveDestination, immediate: true);
        hasDestination = false;
        destinationTimer = 0f;
    }

    void MaintainHoldPosition()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        if (IsWithinFlatDistance(holdAnchor, 0.5f))
        {
            if (agent.hasPath)
                agent.ResetPath();
            return;
        }

        // 이미 홀드 지점으로 돌아가는 중이면 매 프레임 경로를 새로 깔지 않는다.
        // (예전에는 밀려난 유닛마다 프레임당 한 번씩 전체 경로를 다시 계산했다)
        bool alreadyReturning =
            (agent.hasPath || agent.pathPending) &&
            (agent.destination - holdAnchor).sqrMagnitude <= 0.25f;

        if (alreadyReturning)
            return;

        GridMovement.TrySetAgentDestination(agent, holdAnchor, immediate: true);
    }

    void UpdateHoldCombat()
    {
        TickRetarget();

        if (!HasValidTarget())
            return;

        if (!attacker.IsInRange(currentTarget))
            return;

        FaceTarget();
        attacker.TryAttack(currentTarget, currentTargetHealth);
    }

    void UpdatePatrol()
    {
        if (!manualFocusTarget && !damageFocusTarget)
            TickRetarget();
        else if (!HasValidTarget())
        {
            manualFocusTarget = false;
            ClearDamageFocusTarget();
        }

        if (HasValidTarget())
        {
            UpdateCombat();
            return;
        }

        Vector3 goal = patrolTowardEnd ? patrolEnd : patrolStart;
        Vector3 flat = transform.position - goal;
        flat.y = 0f;

        if (flat.sqrMagnitude <= patrolArrivalDistance * patrolArrivalDistance)
        {
            patrolTowardEnd = !patrolTowardEnd;
            goal = patrolTowardEnd ? patrolEnd : patrolStart;
            hasDestination = false;
        }

        destinationTimer -= Time.deltaTime;
        if (destinationTimer > 0f && hasDestination)
            return;

        destinationTimer = Mathf.Max(0.05f, destinationRefreshInterval);

        // 이미 같은 순찰 지점으로 가는 중이면 경로를 다시 계산하지 않는다.
        bool alreadyHeadingToGoal =
            hasDestination &&
            agent.isOnNavMesh &&
            (agent.hasPath || agent.pathPending) &&
            (agent.destination - goal).sqrMagnitude <= 0.25f;

        if (alreadyHeadingToGoal)
            return;

        lastDestination = goal;
        hasDestination = GridMovement.TrySetAgentDestination(agent, goal, immediate: true);
    }

    protected override bool CanJoinSharedAggro()
    {
        return orderState != UnitOrderState.Stopped;
    }

    protected override bool PreferImmediatePath() => true;

    protected override void OnAggroInterrupt()
    {
        if (!attackMoveActive)
        {
            orderState = UnitOrderState.Free;
            manualMoveActive = false;
            hasManualDestination = false;
        }

        base.OnAggroInterrupt();
    }

    public override void JoinAttack(SelectableEntity enemy)
    {
        if (orderState != UnitOrderState.Free)
            return;

        if (manualFocusTarget)
            return;

        if (manualMoveActive && !attackMoveActive)
            return;

        base.JoinAttack(enemy);
    }

    public void BeginManualMove()
    {
        BeginManualMove(false, Vector3.zero);
    }

    /// <summary>
    /// 목적지를 아는 이동 명령입니다. 도착 판정을 경로 상태가 아닌 거리로 하므로,
    /// 경로 설정이 한 프레임 늦어져도 명령이 취소되지 않습니다.
    /// </summary>
    public void BeginManualMove(Vector3 destination)
    {
        BeginManualMove(true, destination);
    }

    void BeginManualMove(bool knownDestination, Vector3 destination)
    {
        UnitCommandDebugLog.Log(this, "명령: 수동 이동 시작 (Free)");

        orderState = UnitOrderState.Free;
        manualMoveActive = true;
        manualFocusTarget = false;
        attackMoveActive = false;
        hasManualDestination = knownDestination;
        manualDestination = destination;
        currentTarget = null;
        currentTargetHealth = null;
        hasDestination = false;
    }

    public void SuspendForManualMove()
    {
        BeginManualMove();
    }

    public void AttackTarget(SelectableEntity target)
    {
        if (target == null)
            return;

        UnitCommandDebugLog.Log(this, $"명령: 공격 대상 지정 (target={DescribeTarget(target)})");

        orderState = UnitOrderState.Free;
        manualMoveActive = false;
        manualFocusTarget = true;
        attackMoveActive = false;
        hasManualDestination = false;
        destinationTimer = 0f;
        hasDestination = false;
        RequestImmediatePath();
        SetTarget(target);
    }

    public bool BeginAttackMove(Vector3 destination)
    {
        if (!GridMovement.TrySetAgentDestination(agent, destination, immediate: true))
            return false;

        UnitCommandDebugLog.Log(this, $"명령: 공격 이동 -> {FormatVector(destination)}");

        orderState = UnitOrderState.Free;
        manualMoveActive = true;
        manualFocusTarget = false;
        attackMoveActive = true;
        attackMoveDestination = destination;
        hasManualDestination = false;
        currentTarget = null;
        currentTargetHealth = null;
        ClearDamageFocusTarget();
        hasDestination = false;
        destinationTimer = 0f;
        ResetRetargetTimer();
        return true;
    }

    public void IssueStop()
    {
        UnitCommandDebugLog.Log(this, "명령: 정지 (Stopped)");

        orderState = UnitOrderState.Stopped;
        manualMoveActive = false;
        manualFocusTarget = false;
        ClearDamageFocusTarget();
        attackMoveActive = false;
        hasManualDestination = false;
        currentTarget = null;
        currentTargetHealth = null;
        hasDestination = false;
        StopAgentMovement();
    }

    public void IssueHold()
    {
        UnitCommandDebugLog.Log(this, $"명령: 홀드 (Hold @ {FormatVector(transform.position)})");

        orderState = UnitOrderState.Hold;
        holdAnchor = transform.position;
        manualMoveActive = false;
        manualFocusTarget = false;
        ClearDamageFocusTarget();
        attackMoveActive = false;
        hasManualDestination = false;
        hasDestination = false;
        currentTarget = null;
        currentTargetHealth = null;
        StopAgentMovement();
    }

    public bool IssuePatrol(Vector3 patrolDestination)
    {
        if (!GridMovement.TrySetAgentDestination(agent, patrolDestination, immediate: true))
            return false;

        UnitCommandDebugLog.Log(
            this,
            $"명령: 순찰 {FormatVector(transform.position)} <-> {FormatVector(patrolDestination)}");

        orderState = UnitOrderState.Patrol;
        patrolStart = transform.position;
        patrolEnd = patrolDestination;
        patrolTowardEnd = true;
        manualMoveActive = false;
        manualFocusTarget = false;
        ClearDamageFocusTarget();
        attackMoveActive = false;
        hasManualDestination = false;
        currentTarget = null;
        currentTargetHealth = null;
        destinationTimer = 0f;
        hasDestination = false;
        return true;
    }

    public void Initialize(int unusedLegacyParameter)
    {
        SetTarget(FindTarget());
    }
}
