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
        {
            Vector3 flat = transform.position - attackMoveDestination;
            flat.y = 0f;
            float arrive = 0.75f;
            return flat.sqrMagnitude <= arrive * arrive;
        }

        if (agent.pathPending)
            return false;

        if (!agent.hasPath)
            return true;

        return agent.remainingDistance <= agent.stoppingDistance + 0.1f;
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
        Vector3 flat = transform.position - holdAnchor;
        flat.y = 0f;

        if (flat.sqrMagnitude <= 0.25f)
        {
            if (agent.isOnNavMesh && agent.hasPath)
                agent.ResetPath();
            return;
        }

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
        UnitCommandDebugLog.Log(this, "명령: 수동 이동 시작 (Free)");

        orderState = UnitOrderState.Free;
        manualMoveActive = true;
        manualFocusTarget = false;
        attackMoveActive = false;
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
