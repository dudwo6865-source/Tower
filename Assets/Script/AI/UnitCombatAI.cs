using UnityEngine;

using UnityEngine.AI;



public enum UnitOrderState

{

    Free,

    Stopped,

    Hold,

    Patrol

}



[RequireComponent(typeof(NavMeshAgent))]

public class UnitCombatAI : CombatAIBase

{

    [Header("Behavior")]

    [Tooltip("교전 대상이 없을 때 가장 가까운 적 건물로 진군합니다. 공격 유닛(적)에 적합합니다.")]

    public bool advanceToEnemyBuildings;



    [Header("Movement")]

    [Tooltip("대상에 접근할 때 멈추는 거리입니다.")]

    public float stoppingDistance = 2f;



    [Tooltip("움직이는 대상을 추격할 때 목적지를 갱신하는 간격(초)입니다.")]

    public float destinationRefreshInterval = 0.25f;



    [Tooltip("정지 후 대상을 바라보는 회전 속도입니다.")]

    public float facingSpeed = 8f;



    [Tooltip("여러 유닛이 같은 대상을 공격할 때 대상 둘레로 흩어지는 각도 범위(도)입니다. 0이면 한 점으로 모입니다.")]

    public float approachSpreadAngle = 60f;



    [Tooltip("접근이 막혔을 때 다른 위치를 찾는 최대 시도 횟수입니다.")]

    public int stuckRepositionMaxAttempts = 32;



    [Tooltip("사거리 밖에서 거의 움직이지 않을 때 재배치까지 대기 시간(초)입니다.")]

    public float blockedMovementTimeout = 0.6f;



    [Tooltip("순찰 지점 도착로 판정 거리입니다.")]

    public float patrolArrivalDistance = 1.25f;

    [Header("Debug")]
    [Tooltip("이 유닛에 내려지는 명령·AI 이동 결정을 Console에 출력합니다.")]
    public bool debugCommandLog;

    private NavMeshAgent agent;

    private float destinationTimer;

    private bool manualMoveActive;

    private bool manualFocusTarget;

    private bool attackMoveActive;

    private UnitOrderState orderState = UnitOrderState.Free;

    private Vector3 holdAnchor;

    private Vector3 patrolStart;

    private Vector3 patrolEnd;

    private bool patrolTowardEnd = true;

    private Vector3 lastDestination;

    private bool hasDestination;

    private float approachAngleFactor;
    private bool wasInAttackRange;
    private int stuckRepositionAttempts;
    private float blockedMovementTimer;

    float GetUnitChaseStoppingDistance()
    {
        return Mathf.Min(
            stoppingDistance,
            Mathf.Max(0.1f, attacker.AttackRange * 0.35f));
    }

    void ApplyChaseStoppingDistance()
    {
        if (agent == null)
            return;

        if (currentTarget != null &&
            currentTarget.entityType == SelectableEntityType.Unit)
        {
            agent.stoppingDistance = GetUnitChaseStoppingDistance();
        }
        else
        {
            agent.stoppingDistance = stoppingDistance;
        }
    }

    protected override void Awake()

    {

        base.Awake();

        agent = GetComponent<NavMeshAgent>();



        float normalized = (GetInstanceID() & 0xFFFF) / 65535f;

        approachAngleFactor = normalized * 2f - 1f;

    }



    void Start()

    {

        ApplyChaseStoppingDistance();

        agent.autoBraking = false;

        GridMovement.EnsureAgentOnNavMesh(agent);

    }



    void Update()

    {

        if (orderState == UnitOrderState.Stopped)

        {

            StopAgentMovement();

            return;

        }



        if (manualMoveActive)

        {

            if (attackMoveActive)

                TickRetarget();



            if (attackMoveActive && HasValidTarget())

            {

                if (attacker.IsInRange(currentTarget))

                {

                    manualMoveActive = false;

                    attackMoveActive = false;

                    StopAndAttack();

                    return;

                }



                manualMoveActive = false;

                attackMoveActive = false;

            }

            else if (ReachedManualDestination())

            {

                manualMoveActive = false;

                attackMoveActive = false;

            }

            else

            {

                return;

            }

        }



        if (orderState == UnitOrderState.Hold)

        {

            MaintainHoldPosition();

            UpdateHoldCombat();

            return;

        }



        if (orderState == UnitOrderState.Patrol)

        {

            UpdatePatrolMovement();

            UpdatePatrolCombat();

            return;

        }



        if (manualFocusTarget)

        {

            if (!HasValidTarget())

                manualFocusTarget = false;

        }

        else

        {

            TickRetarget();

        }



        if (!HasValidTarget())

            return;



        UpdateFreeCombatMovement();

    }



    void UpdateFreeCombatMovement()

    {

        if (attacker.IsInRange(currentTarget))

        {

            if (!wasInAttackRange)

                UnitCommandDebugLog.Log(this, $"교전: 공격 시작 (target={DescribeTarget(currentTarget)})");

            wasInAttackRange = true;

            stuckRepositionAttempts = 0;

            blockedMovementTimer = 0f;

            StopAndAttack();

        }

        else

        {

            if (wasInAttackRange)

                UnitCommandDebugLog.Log(this, $"교전: 사거리 밖 -> 추격 (target={DescribeTarget(currentTarget)})");

            wasInAttackRange = false;

            UpdateApproachBlockedTimer();

            ChaseTarget();

        }

    }



    protected override SelectableEntity FindTarget()

    {

        SelectableEntity enemy = base.FindTarget();



        if (enemy != null)

            return enemy;



        if (advanceToEnemyBuildings)

            return TargetFinder.FindNearestEnemyBuilding(

                transform.position,

                selfEntity.ownerId,

                attacker);



        return null;

    }



    protected override void OnTargetChanged()

    {

        attacker?.CancelPendingAttack();

        destinationTimer = 0f;

        hasDestination = false;

        wasInAttackRange = false;

        stuckRepositionAttempts = 0;

        blockedMovementTimer = 0f;

        ApplyChaseStoppingDistance();



        if (agent.isOnNavMesh && agent.hasPath)

            agent.ResetPath();

    }



    bool ReachedManualDestination()

    {

        if (!agent.isOnNavMesh)

            return true;



        if (agent.pathPending)

            return false;



        if (!agent.hasPath)

            return true;



        return agent.remainingDistance <= agent.stoppingDistance + 0.1f;

    }



    void StopAndAttack()

    {

        if (agent.isOnNavMesh && agent.hasPath)

        {

            agent.ResetPath();

            hasDestination = false;

        }



        agent.velocity = Vector3.zero;



        FaceTarget();

        attacker.TryAttack(currentTarget, currentTargetHealth);

    }



    void UpdateApproachBlockedTimer()
    {
        if (currentTarget == null || attacker.IsInRange(currentTarget))
        {
            blockedMovementTimer = 0f;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            blockedMovementTimer = 0f;
            return;
        }

        if (agent.velocity.sqrMagnitude > 0.15f)
        {
            blockedMovementTimer = 0f;
            return;
        }

        blockedMovementTimer += Time.deltaTime;
    }

    bool IsApproachStuck()
    {
        if (currentTarget == null || attacker.IsInRange(currentTarget))
            return false;

        if (!agent.isOnNavMesh || agent.pathPending)
            return false;

        if (agent.velocity.sqrMagnitude > 0.15f)
            return false;

        if (agent.remainingDistance <= agent.stoppingDistance + 0.35f)
            return true;

        return blockedMovementTimer >= blockedMovementTimeout;
    }

    void ChaseTarget()

    {

        destinationTimer -= Time.deltaTime;



        if (destinationTimer > 0f)

            return;



        destinationTimer = destinationRefreshInterval;



        if (!agent.isOnNavMesh)

            return;



        bool approachStuck = IsApproachStuck();



        if (!approachStuck && ShouldKeepBuildingChasePath())

            return;



        if (!approachStuck && ShouldKeepUnitChasePath())

            return;



        float baseAngleOffset = approachAngleFactor * approachSpreadAngle;

        Vector3 approachPosition;



        if (approachStuck)
        {
            stuckRepositionAttempts = Mathf.Min(
                stuckRepositionAttempts + 1,
                stuckRepositionMaxAttempts);
            hasDestination = false;

            if (agent.hasPath)
                agent.ResetPath();

            blockedMovementTimer = 0f;

            if (!TargetFinder.TryGetAlternateApproachPosition(
                    transform.position,
                    currentTarget,
                    stoppingDistance,
                    attacker.AttackRange,
                    baseAngleOffset,
                    stuckRepositionAttempts,
                    lastDestination,
                    out approachPosition))
            {
                approachPosition = TargetFinder.GetApproachPosition(
                    transform.position,
                    currentTarget,
                    stoppingDistance,
                    attacker.AttackRange,
                    baseAngleOffset + stuckRepositionAttempts * 22.5f);
            }

            if (debugCommandLog)
            {
                UnitCommandDebugLog.Log(
                    this,
                    $"추격: 접근 막힘 -> 재배치 시도 #{stuckRepositionAttempts} {FormatVector(approachPosition)} " +
                    $"(target={DescribeTarget(currentTarget)}, inRange={attacker.IsInRange(currentTarget)})");
            }
        }
        else
        {
            approachPosition = TargetFinder.GetApproachPosition(
                transform.position,
                currentTarget,
                stoppingDistance,
                attacker.AttackRange,
                baseAngleOffset);
        }



        Vector3 destination = approachPosition;

        if (currentTarget.entityType == SelectableEntityType.Building && !approachStuck)
        {
            destination = GridMovement.SnapMoveDestination(
                approachPosition,
                GridMovement.GetFootprintCells(this));
        }



        float sqrDistToTarget =

            (currentTarget.transform.position - transform.position).sqrMagnitude;



        float sqrDistDestToSelf =

            (destination - transform.position).sqrMagnitude;



        if (sqrDistDestToSelf < 1f &&

            sqrDistToTarget > (stoppingDistance + 5f) * (stoppingDistance + 5f))

        {

            destination = currentTarget.transform.position;



            if (NavMesh.SamplePosition(

                    destination,

                    out NavMeshHit hit,

                    stoppingDistance + 2f,

                    NavMesh.AllAreas))

            {

                destination = hit.position;

            }



            sqrDistDestToSelf =

                (destination - transform.position).sqrMagnitude;



            UnitCommandDebugLog.Log(

                this,

                $"추격: 접근 지점 실패 -> 대상 직접 추격 {FormatVector(destination)} " +

                $"(target={DescribeTarget(currentTarget)}, targetDist={Mathf.Sqrt(sqrDistToTarget):F1}m, " +

                $"destDelta={Mathf.Sqrt(sqrDistDestToSelf):F1}m)");

        }



        if (hasDestination &&

            !approachStuck &&

            (lastDestination - destination).sqrMagnitude < 1f)

            return;



        lastDestination = destination;

        hasDestination = GridMovement.TrySetAgentDestination(agent, destination);

        if (hasDestination)
            blockedMovementTimer = 0f;

        if (!debugCommandLog)

            return;

        UnitCommandDebugLog.Log(
            this,
            hasDestination
                ? $"추격: SetDestination {FormatVector(destination)} (target={DescribeTarget(currentTarget)}, inRange={attacker.IsInRange(currentTarget)})"
                : $"추격 실패: SetDestination 거부 {FormatVector(destination)} (target={DescribeTarget(currentTarget)})");
    }



    bool ShouldKeepUnitChasePath()

    {

        if (currentTarget == null ||

            currentTarget.entityType != SelectableEntityType.Unit ||

            !hasDestination)

            return false;



        if (!agent.isOnNavMesh || !agent.hasPath || agent.pathPending)

            return false;



        if (attacker.IsInRange(currentTarget))

            return false;



        return agent.remainingDistance > GetUnitChaseStoppingDistance() + 0.25f;

    }



    bool ShouldKeepBuildingChasePath()

    {

        if (currentTarget == null ||

            currentTarget.entityType != SelectableEntityType.Building ||

            !hasDestination)

            return false;



        if (!agent.isOnNavMesh || !agent.hasPath || agent.pathPending)

            return false;



        if (attacker.IsInRange(currentTarget))

            return false;



        return agent.remainingDistance > 0.5f;

    }



    void FaceTarget()

    {

        Vector3 direction =

            currentTarget.transform.position - transform.position;



        direction.y = 0f;



        if (direction.sqrMagnitude < 0.01f)

            return;



        Quaternion targetRotation = Quaternion.LookRotation(direction);



        transform.rotation = Quaternion.Slerp(

            transform.rotation,

            targetRotation,

            Time.deltaTime * facingSpeed);

    }



    void StopAgentMovement()

    {

        if (agent.isOnNavMesh && agent.hasPath)

            agent.ResetPath();



        agent.velocity = Vector3.zero;

    }



    void MaintainHoldPosition()

    {

        if (!agent.isOnNavMesh)

            return;



        if (agent.hasPath)

            agent.ResetPath();



        Vector3 position = transform.position;

        position.y = holdAnchor.y;



        if ((position - holdAnchor).sqrMagnitude > 0.04f)

            agent.Warp(holdAnchor);

    }



    void UpdateHoldCombat()

    {

        SelectableEntity targetInRange = FindTargetWithinAttackRange();



        if (targetInRange == null)

        {

            if (currentTarget != null)

                SetTarget(null);



            return;

        }



        SetTarget(targetInRange);



        if (attacker.IsInRange(currentTarget))

            StopAndAttack();

    }



    SelectableEntity FindTargetWithinAttackRange()

    {

        SelectableEntity best = null;

        float bestDistance = float.MaxValue;



        foreach (SelectableEntity entity in SelectableRegistry.Entities)

        {

            if (entity == null || entity.ownerId == selfEntity.ownerId)

                continue;



            EntityHealth health = entity.GetComponent<EntityHealth>();



            if (health != null && !health.IsAlive)

                continue;



            if (!attacker.IsInRange(entity))

                continue;



            float sqrDistance =

                (entity.transform.position - transform.position).sqrMagnitude;



            if (sqrDistance >= bestDistance)

                continue;



            bestDistance = sqrDistance;

            best = entity;

        }



        return best;

    }



    void UpdatePatrolMovement()

    {

        if (HasValidTarget())

            return;



        Vector3 destination = patrolTowardEnd ? patrolEnd : patrolStart;

        float arrivalDistance = Mathf.Max(patrolArrivalDistance, agent.stoppingDistance + 0.1f);



        if ((transform.position - destination).sqrMagnitude <= arrivalDistance * arrivalDistance)

            patrolTowardEnd = !patrolTowardEnd;



        destinationTimer -= Time.deltaTime;



        if (destinationTimer > 0f)

            return;



        destinationTimer = destinationRefreshInterval;



        Vector3 nextDestination = patrolTowardEnd ? patrolEnd : patrolStart;



        if (hasDestination &&

            (lastDestination - nextDestination).sqrMagnitude < 0.25f)

            return;



        lastDestination = nextDestination;

        hasDestination = GridMovement.TrySetAgentDestination(agent, nextDestination);

    }



    void UpdatePatrolCombat()

    {

        if (!HasValidTarget())

        {

            TickRetarget();

            return;

        }



        if (attacker.IsInRange(currentTarget))

            StopAndAttack();

        else

            ChaseTarget();

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

        SetTarget(target);

    }



    public bool BeginAttackMove(Vector3 destination)

    {
        if (!GridMovement.TrySetAgentDestination(agent, destination))

            return false;



        UnitCommandDebugLog.Log(this, $"명령: 공격 이동 -> {FormatVector(destination)}");

        orderState = UnitOrderState.Free;

        manualMoveActive = true;

        manualFocusTarget = false;

        attackMoveActive = true;

        currentTarget = null;

        currentTargetHealth = null;

        hasDestination = false;

        destinationTimer = 0f;

        return true;

    }



    public void IssueStop()

    {
        UnitCommandDebugLog.Log(this, "명령: 정지 (Stopped)");

        orderState = UnitOrderState.Stopped;

        manualMoveActive = false;

        manualFocusTarget = false;

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

        attackMoveActive = false;

        hasDestination = false;

        currentTarget = null;

        currentTargetHealth = null;

        StopAgentMovement();

    }



    public bool IssuePatrol(Vector3 patrolDestination)

    {
        if (!GridMovement.TrySetAgentDestination(agent, patrolDestination))

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

    internal void LogTargetChange(SelectableEntity previous, SelectableEntity next)
    {
        if (!debugCommandLog)
            return;

        if (previous == next)
            return;

        UnitCommandDebugLog.Log(
            this,
            $"타겟 변경 {DescribeTarget(previous)} -> {DescribeTarget(next)}");
    }

    static string DescribeTarget(SelectableEntity target)
    {
        if (target == null)
            return "없음";

        return $"{target.name}({target.entityType})";
    }

    static string FormatVector(Vector3 value)
    {
        return $"({value.x:F1}, {value.y:F1}, {value.z:F1})";
    }

}


