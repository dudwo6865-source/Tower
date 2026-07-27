using System.Collections.Generic;

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



    [Header("Squad Movement")]

    [Tooltip("켜면 같은 표적을 노리는 주변 아군 중 대표(리더) 한 마리만 접근/재배치 경로를 계산하고, 나머지는 리더를 따라 이동합니다. 여러 마리가 한 점으로 몰려 버벅이는 것을 줄입니다.")]

    public bool squadMovement = true;

    [Tooltip("이 거리 안에서 같은 표적을 노리는 같은 오너 유닛끼리 한 분대로 묶여 리더를 따릅니다.")]

    public float squadRadius = 12f;

    [Tooltip("팔로워가 리더 뒤로 정렬할 때의 앞뒤 간격(m)입니다.")]

    public float squadFollowSpacing = 2.5f;

    [Tooltip("팔로워가 리더 뒤에서 좌우로 흩어지는 폭(m)입니다.")]

    public float squadFollowSpread = 3f;



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

    [Header("Group Aggro")]

    [Tooltip("켜면 이 유닛이 공격받았을 때 어그로 범위 안의 같은 오너 유닛들도 같은 적을 함께 공격합니다.")]

    public bool shareAggroWithAllies = true;

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
    private UnitCombatAI cachedSquadLeader;

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



        // 새 표적으로 전환할 때는 경로를 즉시 리셋(정지)하지 않는다.
        // hasDestination=false로 두면 다음 프레임 추격에서 SetDestination으로
        // 부드럽게 경로가 교체되므로, 여기서 멈추면 오히려 정지→재출발 버벅임이 생긴다.
        // 표적이 사라진 경우(currentTarget == null)에만 확실히 멈춘다.

        if (currentTarget == null && agent.isOnNavMesh && agent.hasPath)

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

        if (agent.hasPath &&
            agent.pathStatus == NavMeshPathStatus.PathPartial &&
            agent.remainingDistance <= agent.stoppingDistance + 0.75f)
        {
            return true;
        }

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



        // 분대 팔로워면 개별 접근/재배치 계산을 건너뛰고 리더를 따라간다.
        if (TryFollowSquadLeader())
            return;



        bool approachStuck = IsApproachStuck();
        bool needsDetourRepath = NeedsDetourRepath();



        // 건물은 정지 표적이므로 한 번 잡은 접근 경로를 커밋한다(수동 이동처럼).
        // 부분 경로여도 진행 중이면 유지하고, 정말 멈췄을 때만(IsApproachStuck) 재계산.
        if (!approachStuck && ShouldKeepBuildingChasePath())

            return;



        if (!approachStuck && !needsDetourRepath && ShouldKeepUnitChasePath())

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
            if (needsDetourRepath)
            {
                hasDestination = false;

                if (agent.hasPath)
                    agent.ResetPath();

                if (debugCommandLog)
                {
                    UnitCommandDebugLog.Log(
                        this,
                        $"추격: 부분 경로 감지 -> 우회 경로 재계산 (target={DescribeTarget(currentTarget)})");
                }
            }

            approachPosition = TargetFinder.GetApproachPosition(
                transform.position,
                currentTarget,
                stoppingDistance,
                attacker.AttackRange,
                baseAngleOffset);
        }



        Vector3 destination = approachPosition;



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

            !needsDetourRepath &&

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



    // 같은 표적을 노리는 주변 아군 중 리더가 아니면, 리더 뒤로 정렬해 따라 이동한다.
    // 반환값 true = 팔로워로 처리했으니 개별 접근 로직을 건너뛴다.
    bool TryFollowSquadLeader()
    {
        if (!squadMovement || currentTarget == null || selfEntity == null)
            return false;

        UnitCombatAI leader = ResolveSquadLeader();

        if (leader == null || leader == this)
            return false;

        Vector3 targetPos = currentTarget.transform.position;

        // 리더가 계산해 둔 접근 지점(사거리 안 위치)을 기준으로 삼는다.
        // 없으면 리더의 현재 위치를 기준으로 한다.
        Vector3 anchor = leader.hasDestination
            ? leader.lastDestination
            : leader.transform.position;

        Vector3 toTarget = targetPos - anchor;
        toTarget.y = 0f;

        Vector3 forward = toTarget.sqrMagnitude > 0.0001f
            ? toTarget.normalized
            : transform.forward;

        Vector3 right = Vector3.Cross(Vector3.up, forward);

        // 리더의 접근 지점 주변으로, 유닛 고유 각도값에 따라 좌우로 흩어져 정렬한다.
        // (뒤로 약간 물려 서로 겹치지 않게 한다.)
        Vector3 followPoint =
            anchor
            + right * (approachAngleFactor * squadFollowSpread)
            - forward * (Mathf.Abs(approachAngleFactor) * squadFollowSpacing);

        if (NavMesh.SamplePosition(
                followPoint,
                out NavMeshHit hit,
                squadFollowSpacing + 2f,
                NavMesh.AllAreas))
        {
            followPoint = hit.position;
        }

        if (hasDestination &&
            (lastDestination - followPoint).sqrMagnitude < 0.5f)
            return true;

        lastDestination = followPoint;
        hasDestination = GridMovement.TrySetAgentDestination(agent, followPoint);

        if (hasDestination)
            blockedMovementTimer = 0f;

        if (debugCommandLog)
        {
            UnitCommandDebugLog.Log(
                this,
                $"분대 추격: 리더({leader.name}) 따라감 {FormatVector(followPoint)} (target={DescribeTarget(currentTarget)})");
        }

        return true;
    }



    // 표적에 가장 가까운 분대원을 리더로 선출한다(동률은 InstanceID, 소폭 히스테리시스로 잦은 교체 방지).
    UnitCombatAI ResolveSquadLeader()
    {
        Vector3 targetPos = currentTarget.transform.position;
        Vector3 selfPos = transform.position;
        float radiusSqr = squadRadius * squadRadius;
        const float switchMargin = 1.5f;

        UnitCombatAI best = this;
        float bestDist = Vector3.Distance(targetPos, selfPos);
        int bestId = GetInstanceID();

        UnitCombatAI incumbent = null;
        float incumbentDist = float.MaxValue;

        if (IsValidSquadMember(cachedSquadLeader, selfPos, radiusSqr))
        {
            incumbent = cachedSquadLeader;
            incumbentDist = Vector3.Distance(targetPos, incumbent.transform.position);
        }

        IReadOnlyList<SelectableEntity> entities = SelectableRegistry.Entities;

        for (int i = 0; i < entities.Count; i++)
        {
            SelectableEntity e = entities[i];

            if (e == null || e == selfEntity)
                continue;

            if (e.entityType != SelectableEntityType.Unit)
                continue;

            if (e.ownerId != selfEntity.ownerId)
                continue;

            if ((e.transform.position - selfPos).sqrMagnitude > radiusSqr)
                continue;

            UnitCombatAI ai = e.GetComponent<UnitCombatAI>();

            if (ai == null || !ai.squadMovement || ai.CurrentTarget != currentTarget)
                continue;

            float d = Vector3.Distance(targetPos, e.transform.position);
            int id = ai.GetInstanceID();

            if (d < bestDist - 0.01f ||
                (Mathf.Abs(d - bestDist) <= 0.01f && id < bestId))
            {
                best = ai;
                bestDist = d;
                bestId = id;
            }
        }

        // 기존 리더가 유효하고, 새 후보가 크게 더 가깝지 않으면 리더를 유지한다.
        if (incumbent != null &&
            best != incumbent &&
            incumbentDist - bestDist < switchMargin)
        {
            best = incumbent;
        }

        cachedSquadLeader = best;
        return best;
    }



    bool IsValidSquadMember(UnitCombatAI ai, Vector3 selfPos, float radiusSqr)
    {
        if (ai == null || !ai)
            return false;

        if (!ai.squadMovement)
            return false;

        if (ai != this)
        {
            if (ai.CurrentTarget != currentTarget)
                return false;

            if ((ai.transform.position - selfPos).sqrMagnitude > radiusSqr)
                return false;
        }

        return true;
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



        // 경로 커밋: 부분 경로여도 진행 중이면 유지한다(수동 이동과 동일).
        // 벽 앞에 도달해 실제로 멈추면 IsApproachStuck이 감지해 우회를 재계산한다.
        return agent.remainingDistance > 0.5f;

    }

    bool NeedsDetourRepath()
    {
        if (currentTarget == null || attacker.IsInRange(currentTarget))
            return false;

        if (!agent.isOnNavMesh || !agent.hasPath || agent.pathPending)
            return false;

        return agent.pathStatus == NavMeshPathStatus.PathPartial;
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



    protected override void HandleAttackedBy(SelectableEntity attacker)
    {
        if (orderState == UnitOrderState.Stopped)
            return;

        // 이미 더 가까운 대상을 향해 이동/교전 중이면, 더 먼 새 공격자는 무시하고
        // 현재 대상으로 계속 이동한다(두 적이 번갈아 때릴 때의 왕복 방지).
        if (!ShouldAdoptNewAttacker(attacker))
        {
            damageFocusTarget = true;
            return;
        }

        orderState = UnitOrderState.Free;
        manualMoveActive = false;
        attackMoveActive = false;
        hasDestination = false;
        destinationTimer = 0f;

        base.HandleAttackedBy(attacker);

        RallyNearbyAllies(attacker);
    }



    // 이 유닛이 공격받았을 때, 어그로 범위 안의 같은 오너 유닛들도 같은 적을 함께 노리게 한다.
    void RallyNearbyAllies(SelectableEntity enemy)
    {
        if (!shareAggroWithAllies || enemy == null || selfEntity == null)
            return;

        float radiusSqr = aggroRange * aggroRange;
        Vector3 origin = transform.position;

        IReadOnlyList<SelectableEntity> entities = SelectableRegistry.Entities;

        for (int i = 0; i < entities.Count; i++)
        {
            SelectableEntity ally = entities[i];

            if (ally == null || ally == selfEntity)
                continue;

            if (ally.entityType != SelectableEntityType.Unit)
                continue;

            if (ally.ownerId != selfEntity.ownerId)
                continue;

            Vector3 offset = ally.transform.position - origin;

            if (offset.sqrMagnitude > radiusSqr)
                continue;

            UnitCombatAI allyAI = ally.GetComponent<UnitCombatAI>();

            allyAI?.JoinAttack(enemy);
        }
    }



    // 아군의 요청으로 같은 적을 함께 공격한다. 여기서는 재전파(RallyNearbyAllies)를 하지 않아 연쇄 호출을 막는다.
    public void JoinAttack(SelectableEntity enemy)
    {
        if (enemy == null)
            return;

        // 플레이어가 직접 명령한 상태(정지/홀드/순찰/수동이동/지정공격)면 개입하지 않는다.
        if (orderState != UnitOrderState.Free)
            return;

        if (manualMoveActive || manualFocusTarget)
            return;

        if (!CanRetaliateAgainst(enemy))
            return;

        if (currentTarget == enemy)
            return;

        attackMoveActive = false;
        hasDestination = false;
        destinationTimer = 0f;

        CommandAttackTarget(enemy);
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

        // 이전 전투에서 남은 피격 집중 상태를 초기화한다.
        // (남아 있으면 TickRetarget이 탐색을 건너뛰어, 사거리 안 적을 무시하고 이동만 하게 된다.)
        ClearDamageFocusTarget();

        hasDestination = false;

        destinationTimer = 0f;

        // 이동 중 곧바로 사거리 안 적을 탐지하도록 재탐색을 즉시 수행하게 한다.
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


