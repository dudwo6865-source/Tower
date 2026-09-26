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
    [Header("명령")]
    [Label("정찰 지점 도착 거리")]
    [Tooltip("순찰 지점 도착로 판정 거리입니다.")]
    public float patrolArrivalDistance = 1.25f;

    [Label("이동 중 피격 무시")]
    [Tooltip("켜면 이동 명령을 수행하는 동안 공격받아도 반격하지 않고 목적지까지 이동합니다. 끄면 공격받는 순간 이동을 멈추고 반격합니다. (공격 이동은 원래대로 교전합니다.)")]
    public bool keepMoveOrderWhenAttacked = true;

    [Label("지정 대상 유지")]
    [Tooltip("켜면 플레이어가 지정한 공격 대상이 살아 있는 동안 다른 적에게 공격받아도 대상을 바꾸지 않습니다.")]
    public bool keepAttackOrderWhenAttacked = true;

    [Header("이동 막힘 복구")]
    [Label("막힘 판정 시간(초)")]
    [Tooltip("일반 이동 중 이 시간 동안 목적지에 가까워지지 못하면 막힌 것으로 보고 경로를 다시 찾습니다. 0이면 막힘 복구를 하지 않습니다.")]
    [Min(0f)]
    public float moveStuckTimeout = 1.5f;

    [Label("전진 판정 거리(m)")]
    [Tooltip("막힘 판정 시간 안에 목적지까지 남은 거리가 이만큼 줄어야 전진한 것으로 봅니다.")]
    [Min(0.01f)]
    public float moveProgressThreshold = 0.3f;

    [Label("경로 재탐색 횟수")]
    [Tooltip("막혔을 때 경로를 다시 찾는 최대 횟수입니다. 모두 실패하면 이동을 포기합니다.")]
    [Min(0)]
    public int maxMoveRepathAttempts = 2;

    [Label("혼잡 도착 반경(m)")]
    [Tooltip("여러 유닛이 몰려 목적지에 딱 맞게 서지 못할 때, 목적지에서 이 거리 안에서 막히면 도착한 것으로 봅니다.")]
    [Min(0f)]
    public float crowdedArrivalRadius = 2.5f;

    [Label("이동 불가 표시")]
    [Tooltip("갈 수 없어 이동을 포기하면 목적지에 표시를 남깁니다.")]
    public bool showUnreachableMarker = true;

    [Label("이동 불가 표시 색")]
    [Tooltip("이동을 포기했을 때 목적지에 남기는 표시 색입니다.")]
    public Color unreachableMarkerColor = new Color(1f, 0.8f, 0.1f, 0.95f);

    [Label("이동 불가 표시 시간(초)")]
    [Tooltip("이동 불가 표시가 화면에 남아 있는 시간입니다.")]
    [Min(0.05f)]
    public float unreachableMarkerSeconds = 1.5f;

    // 이동 막힘 판정 상태. 명령마다 새로 시작한다.
    float moveBestDistance;
    float moveStuckTimer;
    int moveRepathCount;

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
        {
            ResumeAttackMoveIfNeeded();
        }
        else if (UpdateMoveStuckRecovery())
        {
            // 도착으로 보거나 포기해서 이동 명령이 끝났다. 이번 프레임부터 일반 AI로 돌아간다.
            return true;
        }

        return false;
    }

    void ResetMoveStuckState()
    {
        moveBestDistance = float.MaxValue;
        moveStuckTimer = 0f;
        moveRepathCount = 0;
    }

    // 일반 이동이 막혔는지 보고 경로 재탐색 → 포기 순으로 복구한다.
    // 이동 명령이 끝났으면(혼잡 도착 또는 포기) true를 반환한다.
    bool UpdateMoveStuckRecovery()
    {
        if (moveStuckTimeout <= 0f || !hasManualDestination)
            return false;

        if (agent == null || !agent.isOnNavMesh || agent.pathPending)
            return false;

        Vector3 flat = transform.position - manualDestination;
        flat.y = 0f;
        float distance = flat.magnitude;

        // 목적지에 가까워지고 있으면 막힘 타이머를 되돌린다.
        if (distance < moveBestDistance - moveProgressThreshold)
        {
            moveBestDistance = distance;
            moveStuckTimer = 0f;
        }
        else
        {
            moveStuckTimer += Time.deltaTime;
        }

        bool pathInvalid = agent.pathStatus == NavMeshPathStatus.PathInvalid;

        // 경로가 끝났는데(혼잡해서 멈췄거나 경로를 잃음) 아직 목적지에 닿지 못한 경우.
        bool pathLost = !agent.hasPath;

        // 갈 수 있는 데까지만 경로가 잡혀(Partial) 그 끝에 도착한 경우.
        bool partialPathEnded =
            agent.pathStatus == NavMeshPathStatus.PathPartial &&
            agent.remainingDistance <= agent.stoppingDistance + 0.1f;

        if (!pathInvalid && !pathLost && !partialPathEnded && moveStuckTimer < moveStuckTimeout)
            return false;

        // 목적지 근처에서 다른 유닛에 막힌 것이면 도착으로 본다.
        if (!pathInvalid && distance <= crowdedArrivalRadius)
        {
            UnitCommandDebugLog.Log(this, $"이동: 목적지 근처 혼잡으로 도착 처리 (남은 거리 {distance:F1}m)");
            EndManualMove();
            return true;
        }

        if (moveRepathCount < maxMoveRepathAttempts)
        {
            moveRepathCount++;
            moveStuckTimer = 0f;
            moveBestDistance = distance;

            UnitCommandDebugLog.Log(this, $"이동: 막힘 감지, 경로 재탐색 {moveRepathCount}/{maxMoveRepathAttempts}");

            if (GridMovement.TrySetAgentDestination(agent, manualDestination, immediate: true))
                return false;
        }

        // 경로 자체는 온전한데(Complete) 못 가는 것은 다른 유닛에 막힌 경우다.
        // 목적지는 갈 수 있는 곳이므로 표시 없이 그 자리에서 멈춘다.
        if (agent.pathStatus == NavMeshPathStatus.PathComplete)
        {
            UnitCommandDebugLog.Log(this, $"이동: 다른 유닛에 막혀 그 자리에서 멈춤 (남은 거리 {distance:F1}m)");
            EndManualMove();

            if (agent.hasPath)
                agent.ResetPath();

            return true;
        }

        UnitCommandDebugLog.Log(this, $"이동: 목적지에 갈 수 없어 이동을 포기 ({FormatVector(manualDestination)})");

        if (showUnreachableMarker)
        {
            UnitCommandIndicatorTracker.ShowPointMarker(
                manualDestination,
                unreachableMarkerColor,
                unreachableMarkerSeconds);
        }

        EndManualMove();

        if (agent.hasPath)
            agent.ResetPath();

        return true;
    }

    void EndManualMove()
    {
        manualMoveActive = false;
        attackMoveActive = false;
        hasManualDestination = false;
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

    // 피격 반격이 플레이어 명령을 덮어쓰지 않게 한다. 교전이 많아질수록 피격이 잦아서
    // 이동/공격 명령이 곧바로 반격으로 바뀌어 '명령이 안 먹는' 것처럼 보였다.
    protected override void HandleAttackedBy(SelectableEntity attackerEntity)
    {
        if (keepMoveOrderWhenAttacked && manualMoveActive && !attackMoveActive)
            return;

        if (keepAttackOrderWhenAttacked && manualFocusTarget && HasValidTarget())
            return;

        base.HandleAttackedBy(attackerEntity);
    }

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
        ResetMoveStuckState();
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
