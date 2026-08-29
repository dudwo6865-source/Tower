using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 이동 유닛 공통 전투(추격/공격/공유 어그로). 플레이어 명령은 UnitCombatAI, 적 AI는 EnemyCombatAI.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public abstract class MobileCombatAI : CombatAIBase
{
    [Header("Movement")]
    [Tooltip("대상에 접근할 때 NavMeshAgent stoppingDistance 상한입니다.")]
    public float stoppingDistance = 2f;

    [Tooltip("움직이는 대상을 추격할 때 목적지를 갱신하는 간격(초)입니다.")]
    public float destinationRefreshInterval = 0.25f;

    [Tooltip("대상이 이 거리(m) 이상 이동했을 때만 추격 목적지를 갱신합니다.")]
    public float destinationMoveThreshold = 1f;

    [Tooltip("사거리 밖에서 진행이 없을 때 경로를 다시 계산하기까지 대기 시간(초)입니다.")]
    public float pathStuckTimeout = 0.75f;

    [Tooltip("정지 후 대상을 바라보는 회전 속도입니다.")]
    public float facingSpeed = 8f;

    [Tooltip("스폰 시 NavMeshAgent avoidancePriority 하한입니다.")]
    public int avoidancePriorityMin = 30;

    [Tooltip("스폰 시 NavMeshAgent avoidancePriority 상한입니다.")]
    public int avoidancePriorityMax = 70;

    [Header("Building Approach")]
    [Tooltip("건물 추격 중 우회 접근점 대신 건물로 직행하기 시작하는 거리(m)입니다. " +
        "건물 외곽과 자신 외곽 사이 간격 기준이며, 0 이하면 Aggro Range를 씁니다.")]
    public float buildingDirectChaseRange;

    [Header("Group Aggro")]
    [Tooltip("켜면 이 유닛이나 같은 오너 건물이 공격받았을 때, 어그로 범위 안의 아군 유닛들이 같은 적을 함께 공격합니다.")]
    public bool shareAggroWithAllies = true;

    [Header("Debug")]
    [Tooltip("전투 AI 결정을 Console에 출력합니다.")]
    public bool debugCommandLog;

    static readonly List<SelectableEntity> rallyBuffer = new List<SelectableEntity>(32);

    protected NavMeshAgent agent;
    public bool HasCachedPath =>
        cachedPathCorners != null &&
        cachedPathCornerIndex < cachedPathCorners.Length;
    protected float destinationTimer;
    protected Vector3 lastDestination;
    protected bool hasDestination;
    bool immediatePathOnce;
    float pathStuckTimer;
    bool hasPathProgressSample;
    float lastProgressRemaining;
    Vector3 lastProgressPosition;
    Vector3 lastTargetPosition;
    Vector3[] cachedPathCorners;
    int cachedPathCornerIndex;
    NavMeshPath buildingContactPath;
    float directChaseCheckTimer;
    bool cachedUseDirectChase;
    bool issuedDirectChase;
    bool facedThisFrame;

    protected override void Awake()
    {
        base.Awake();
        agent = GetComponent<NavMeshAgent>();

        // NavMesh carve(건물 설치) 때 전원 자동 재경로를 막고,
        // stuck / 표적 이동 / 목적지가 없을 때만 다시 찾는다.
        if (agent != null)
        {
            agent.autoRepath = false;
            ApplyManualRotationOwnership();
        }
    }

    protected virtual void Start()
    {
        ApplyChaseStoppingDistance();
        ApplyRandomAvoidancePriority();

        if (agent != null)
        {
            agent.autoRepath = false;
            ApplyManualRotationOwnership();
        }

        GridMovement.EnsureAgentOnNavMesh(agent);
        StaggerStartupTimers();

        float normalized = (GetInstanceID() & 0xFFFF) / 65535f;
        destinationTimer = 0.1f + normalized * Mathf.Max(0.2f, destinationRefreshInterval);
    }

    protected void UpdateCombat()
    {
        ApplyChaseStoppingDistance();

        if (attacker.IsInRange(currentTarget))
        {
            ResetPathStuck();
            StopAndAttack();
            return;
        }

        UpdatePathStuckTimer();
        ChaseTarget();
    }

    protected void ChaseTarget()
    {
        if (currentTarget == null || agent == null)
            return;

        if (!agent.isOnNavMesh)
        {
            GridMovement.EnsureAgentOnNavMesh(agent);
            return;
        }

        if (agent.autoRepath)
            agent.autoRepath = false;

        RefreshCachedPath();
        RefreshDirectChaseMode();

        Vector3 targetPosition = currentTarget.transform.position;
        float moveThreshold = Mathf.Max(0.25f, destinationMoveThreshold);
        bool targetRelocated =
            hasDestination &&
            (lastTargetPosition - targetPosition).sqrMagnitude >= moveThreshold * moveThreshold;
        bool chaseModeChanged = cachedUseDirectChase != issuedDirectChase;

        bool needsPath = !hasDestination;
        bool stuck = IsPathStuck();
        bool hasCompletePath =
            agent.hasPath &&
            !agent.pathPending &&
            agent.pathStatus == NavMeshPathStatus.PathComplete;

        // 완성된 경로가 있고, 그 경로가 현재 표적용일 때만 에이전트에게 맡긴다.
        if (hasCompletePath && !stuck && !needsPath && !targetRelocated && !chaseModeChanged)
            return;

        // carve 때 Unity가 pathPending으로 전원 재계산한다. 캐시가 있으면 취소하고 걷는다.
        if (!stuck && HasCachedPath && !chaseModeChanged)
        {
            CancelUnityRepath();
            if (TryFollowCachedPath())
                return;
        }

        if (ShouldSkipSetDestination())
        {
            FollowWithoutSetDestination();
            return;
        }

        if (!stuck && HasCachedPath && !chaseModeChanged)
            return;

        if (!stuck && !needsPath && !targetRelocated && !chaseModeChanged)
            return;

        if (!chaseModeChanged && targetRelocated && hasCompletePath && !stuck)
        {
            destinationTimer -= Time.deltaTime;
            if (destinationTimer > 0f)
                return;
        }

        destinationTimer = Mathf.Max(0.05f, destinationRefreshInterval);

        Vector3 destination = GetChaseDestination();
        lastTargetPosition = targetPosition;
        lastDestination = destination;
        issuedDirectChase = cachedUseDirectChase;
        hasDestination = GridMovement.TrySetAgentDestination(
            agent,
            destination,
            ConsumeImmediatePath() || PreferImmediatePath() || chaseModeChanged);

        if (hasDestination)
            ResetPathStuck();

        if (debugCommandLog)
        {
            string reason =
                chaseModeChanged ? "mode-changed" :
                stuck ? "stuck" :
                needsPath ? "no-path" : "target-moved";

            UnitCommandDebugLog.Log(
                this,
                hasDestination
                    ? $"추격({reason}/{DescribeChaseMode()}): {FormatVector(destination)} -> {DescribeTarget(currentTarget)}"
                    : $"추격 실패({reason}/{DescribeChaseMode()}): {DescribeTarget(currentTarget)}");
        }
    }

    // 건물 직행(접촉점)인지 우회(인근 NavMesh 접근점)인지와, 그 판정에 쓰인 간격을 함께 보여줍니다.
    string DescribeChaseMode()
    {
        if (currentTarget == null || currentTarget.entityType != SelectableEntityType.Building)
            return "유닛추격";

        string mode = cachedUseDirectChase ? "직행" : "우회";
        float gap = GetHorizontalBoundsGap(currentTarget);

        return $"{mode} gap {gap:0.0}/{GetBuildingDirectChaseRange():0.0}";
    }

    void UpdatePathStuckTimer()
    {
        if (!agent.isOnNavMesh)
            return;

        Vector3 pos = transform.position;
        float progressDistance = 0.4f;

        if (!hasPathProgressSample)
        {
            CapturePathProgress(0f, pos);
            return;
        }

        bool moved =
            (pos - lastProgressPosition).sqrMagnitude >= progressDistance * progressDistance;

        if (moved)
        {
            CapturePathProgress(0f, pos);
            pathStuckTimer = 0f;
            return;
        }

        bool hasCompletePath =
            agent.hasPath &&
            !agent.pathPending &&
            agent.pathStatus == NavMeshPathStatus.PathComplete;

        if (hasCompletePath)
        {
            float remaining = agent.remainingDistance;
            if (float.IsInfinity(remaining) || float.IsNaN(remaining))
                remaining = lastProgressRemaining;

            if (lastProgressRemaining - remaining >= progressDistance)
            {
                CapturePathProgress(remaining, pos);
                pathStuckTimer = 0f;
                return;
            }

            if (remaining > lastProgressRemaining + progressDistance)
            {
                CapturePathProgress(remaining, pos);
                pathStuckTimer = 0f;
                return;
            }
        }

        bool waitingOnPath = agent.pathPending;
        bool stopped = agent.velocity.sqrMagnitude <= 0.15f;

        if (!waitingOnPath && stopped)
            pathStuckTimer += Time.deltaTime;
    }

    protected bool IsPathStuck()
    {
        float timeout = pathStuckTimeout > 0.05f ? pathStuckTimeout : 0.75f;
        return pathStuckTimer >= timeout;
    }

    void CapturePathProgress(float remaining, Vector3 position)
    {
        hasPathProgressSample = true;
        lastProgressRemaining = remaining;
        lastProgressPosition = position;
    }

    void CancelUnityRepath()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        if (!agent.pathPending && !agent.hasPath)
            return;

        agent.ResetPath();
        agent.autoRepath = false;
    }

    void ResetPathStuck()
    {
        pathStuckTimer = 0f;
        hasPathProgressSample = false;
        lastProgressRemaining = 0f;
        lastProgressPosition = Vector3.zero;
    }

    void RefreshCachedPath()
    {
        if (!agent.hasPath || agent.pathPending)
            return;

        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            return;

        Vector3[] corners = agent.path.corners;
        if (corners == null || corners.Length < 2)
            return;

        bool complete = agent.pathStatus == NavMeshPathStatus.PathComplete;
        bool hasCache =
            cachedPathCorners != null &&
            cachedPathCornerIndex < cachedPathCorners.Length;

        // carve 직후 Partial이 짧은 잔여 경로로 캐시를 덮어쓰지 않게 한다.
        if (!complete && hasCache)
            return;

        cachedPathCorners = corners;
        cachedPathCornerIndex = 1;
        Vector3 position = transform.position;

        for (int i = 1; i < cachedPathCorners.Length; i++)
        {
            Vector3 delta = cachedPathCorners[i] - position;
            delta.y = 0f;
            cachedPathCornerIndex = i;

            if (delta.sqrMagnitude > 0.25f)
                break;
        }
    }

    protected bool TryFollowCachedPath()
    {
        if (cachedPathCorners == null ||
            cachedPathCornerIndex >= cachedPathCorners.Length)
        {
            return false;
        }

        Vector3 position = transform.position;

        while (cachedPathCornerIndex < cachedPathCorners.Length)
        {
            Vector3 delta = cachedPathCorners[cachedPathCornerIndex] - position;
            delta.y = 0f;

            if (delta.sqrMagnitude > 0.36f)
                break;

            cachedPathCornerIndex++;
        }

        if (cachedPathCornerIndex >= cachedPathCorners.Length)
            return false;

        Vector3 toCorner = cachedPathCorners[cachedPathCornerIndex] - position;
        toCorner.y = 0f;
        float distance = toCorner.magnitude;

        if (distance < 0.01f)
            return false;

        float step = Mathf.Min(agent.speed * Time.deltaTime, distance);
        Vector3 move = toCorner / distance * step;
        return TryMoveManually(move, toCorner);
    }

    void FollowWithoutSetDestination()
    {
        if (HasCachedPath)
            CancelUnityRepath();

        if (TryFollowCachedPath() || TryFollowSquadFallback())
            return;
    }

    bool TryMoveManually(Vector3 move, Vector3 faceDirection)
    {
        if (agent == null || !agent.isOnNavMesh)
            return false;

        // 잔여 velocity 위에 Move를 쌓으면 속도가 두 배가 된다.
        agent.velocity = Vector3.zero;

        Vector3 before = transform.position;
        agent.Move(move);

        if ((transform.position - before).sqrMagnitude <= 0.0001f)
            return false;

        pathStuckTimer = 0f;
        FaceCachedMoveDirection(faceDirection);
        return true;
    }

    void FaceCachedMoveDirection(Vector3 direction)
    {
        FaceDirection(direction, agent.angularSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 회전은 이 스크립트가 전담합니다. NavMeshAgent가 같은 프레임에 rotation을
    /// 덮어쓰면 조준 방향(UnitAttacker.IsAimedAt)이 흔들려 공격이 나가지 않습니다.
    /// </summary>
    void ApplyManualRotationOwnership()
    {
        agent.updateRotation = false;
    }

    /// <summary>
    /// Agent가 스스로 경로를 따라 걷는 구간에서는 아무도 회전을 시키지 않습니다.
    /// updateRotation을 끈 대신 여기서 진행 방향을 바라보게 합니다.
    /// </summary>
    protected virtual void LateUpdate()
    {
        if (!facedThisFrame && agent != null && agent.isOnNavMesh)
        {
            Vector3 velocity = agent.velocity;
            velocity.y = 0f;

            if (velocity.sqrMagnitude >= 0.04f)
                FaceDirection(velocity, agent.angularSpeed * Time.deltaTime);
        }

        facedThisFrame = false;
    }

    void FaceDirection(Vector3 direction, float maxDegreesDelta)
    {
        if (direction.sqrMagnitude < 0.01f)
            return;

        facedThisFrame = true;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(direction),
            maxDegreesDelta);
    }

    void ClearCachedPath()
    {
        cachedPathCorners = null;
        cachedPathCornerIndex = 0;
    }

    protected virtual bool ShouldSkipSetDestination() => false;

    protected virtual bool PreferImmediatePath() => false;

    protected bool IsDirectBuildingChase => cachedUseDirectChase;

    void RefreshDirectChaseMode()
    {
        directChaseCheckTimer -= Time.deltaTime;
        if (directChaseCheckTimer > 0f)
            return;

        directChaseCheckTimer = Mathf.Max(0.15f, destinationRefreshInterval);
        cachedUseDirectChase = ComputeShouldUseDirectChase();
    }

    bool ComputeShouldUseDirectChase()
    {
        if (currentTarget == null || currentTarget.entityType != SelectableEntityType.Building)
            return false;

        float gap = GetHorizontalBoundsGap(currentTarget);
        float enterRange = GetBuildingDirectChaseRange();
        float exitRange = enterRange + Mathf.Max(2f, destinationMoveThreshold);

        if (cachedUseDirectChase)
            return gap <= exitRange;

        if (gap > enterRange)
            return false;

        float attackRange = attacker != null ? attacker.AttackRange : stoppingDistance;
        if (gap <= attackRange + stoppingDistance + 1f)
            return true;

        // 직행 거리 안이어도 건물로 가는 길이 막혀 있으면 우회 접근점을 유지한다.
        return HasCompletePathToBuildingContact();
    }

    protected float GetBuildingDirectChaseRange()
    {
        float range = buildingDirectChaseRange > 0f ? buildingDirectChaseRange : aggroRange;
        return Mathf.Max(0.5f, range);
    }

    Vector3 GetChaseDestination()
    {
        float attackRange = attacker != null ? attacker.AttackRange : stoppingDistance;

        Vector3 destination = cachedUseDirectChase
            ? GetBuildingContactPoint()
            : TargetFinder.GetApproachPosition(
                transform.position,
                currentTarget,
                stoppingDistance,
                attackRange);

        if (UnitSpawnUtility.TrySampleNavMeshNearPreferredHeight(
                destination,
                transform.position.y,
                Mathf.Max(4f, attackRange),
                out Vector3 sameFloor))
        {
            destination = sameFloor;
        }

        return destination;
    }

    Vector3 GetBuildingContactPoint()
    {
        if (currentTarget == null)
            return transform.position;

        Vector3 closest = currentTarget.SelectionBounds.ClosestPoint(transform.position);
        closest.y = transform.position.y;
        return closest;
    }

    float GetHorizontalBoundsGap(SelectableEntity target)
    {
        if (target == null)
            return float.MaxValue;

        Bounds targetBounds = target.SelectionBounds;
        Bounds selfBounds = selfEntity != null
            ? selfEntity.SelectionBounds
            : new Bounds(transform.position, Vector3.one);

        Vector3 targetPoint = targetBounds.ClosestPoint(selfBounds.center);
        Vector3 selfPoint = selfBounds.ClosestPoint(targetPoint);
        targetPoint = targetBounds.ClosestPoint(selfPoint);

        Vector3 diff = targetPoint - selfPoint;
        diff.y = 0f;
        return diff.magnitude;
    }

    bool HasCompletePathToBuildingContact()
    {
        if (agent == null || !agent.isOnNavMesh)
            return false;

        if (buildingContactPath == null)
            buildingContactPath = new NavMeshPath();

        Vector3 contact = GetBuildingContactPoint();
        float attackRange = attacker != null ? attacker.AttackRange : stoppingDistance;

        if (UnitSpawnUtility.TrySampleNavMeshNearPreferredHeight(
                contact,
                transform.position.y,
                Mathf.Max(4f, attackRange),
                out Vector3 sampled))
        {
            contact = sampled;
        }

        buildingContactPath.ClearCorners();

        if (!NavMesh.CalculatePath(
                transform.position,
                contact,
                NavMesh.AllAreas,
                buildingContactPath))
        {
            return false;
        }

        return buildingContactPath.status == NavMeshPathStatus.PathComplete;
    }

    protected virtual bool TryFollowSquadFallback()
    {
        if (currentTarget == null)
            return false;

        if (hasDestination)
            return TrySteerToward(lastDestination);

        return false;
    }

    protected bool TryGetFollowablePathCorners(out Vector3[] corners)
    {
        if (agent != null &&
            agent.hasPath &&
            !agent.pathPending &&
            agent.pathStatus != NavMeshPathStatus.PathInvalid)
        {
            corners = agent.path.corners;
            if (corners != null && corners.Length >= 2)
                return true;
        }

        if (cachedPathCorners != null &&
            cachedPathCornerIndex < cachedPathCorners.Length)
        {
            corners = cachedPathCorners;
            return true;
        }

        corners = null;
        return false;
    }

    protected bool AdoptPathCorners(Vector3[] corners)
    {
        if (corners == null || corners.Length < 2)
            return false;

        cachedPathCorners = corners;
        cachedPathCornerIndex = 1;
        Vector3 position = transform.position;

        for (int i = 1; i < cachedPathCorners.Length; i++)
        {
            Vector3 delta = cachedPathCorners[i] - position;
            delta.y = 0f;
            cachedPathCornerIndex = i;

            if (delta.sqrMagnitude > 0.25f)
                break;
        }

        return true;
    }

    protected bool TrySteerToward(Vector3 worldPoint)
    {
        if (agent == null || !agent.isOnNavMesh)
            return false;

        Vector3 delta = worldPoint - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;

        if (distance < 0.05f)
            return false;

        float step = Mathf.Min(agent.speed * Time.deltaTime, distance);
        if (!TryMoveManually(delta / distance * step, delta))
            return false;

        lastDestination = worldPoint;
        hasDestination = true;
        return true;
    }

    protected void StopAndAttack()
    {
        if (agent.isOnNavMesh && agent.hasPath)
        {
            agent.ResetPath();
            hasDestination = false;
            ClearCachedPath();
        }

        agent.velocity = Vector3.zero;
        ResetPathStuck();
        FaceTarget();
        attacker.TryAttack(currentTarget, currentTargetHealth);
    }

    protected void FaceTarget()
    {
        if (currentTarget == null)
            return;

        Vector3 aimPoint = currentTarget.SelectionBounds.center;
        Vector3 direction = aimPoint - transform.position;
        direction.y = 0f;

        FaceDirection(direction, facingSpeed * 90f * Time.deltaTime);
    }

    protected void ApplyChaseStoppingDistance()
    {
        if (agent == null || attacker == null)
            return;

        float rangeStop = Mathf.Max(0.1f, attacker.AttackRange * 0.85f);
        agent.stoppingDistance = Mathf.Min(Mathf.Max(0.1f, stoppingDistance), rangeStop);
    }

    protected void ApplyRandomAvoidancePriority()
    {
        if (agent == null)
            return;

        int min = Mathf.Clamp(avoidancePriorityMin, 0, 99);
        int max = Mathf.Clamp(avoidancePriorityMax, 0, 99);

        if (max < min)
        {
            int swap = min;
            min = max;
            max = swap;
        }

        agent.avoidancePriority = Random.Range(min, max + 1);
    }

    protected void StopAgentMovement()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        if (agent.hasPath)
            agent.ResetPath();

        agent.velocity = Vector3.zero;
        hasDestination = false;
        ClearCachedPath();
    }

    protected override void OnTargetChanged()
    {
        attacker?.CancelPendingAttack();
        destinationTimer = 0f;
        hasDestination = false;
        ClearCachedPath();
        ResetPathStuck();
        directChaseCheckTimer = 0f;
        cachedUseDirectChase = false;
        issuedDirectChase = false;
        ApplyChaseStoppingDistance();

        if (agent != null && agent.isOnNavMesh && (agent.hasPath || agent.pathPending))
            agent.ResetPath();
    }

    protected override void HandleAttackedBy(SelectableEntity attackerEntity)
    {
        if (!CanJoinSharedAggro())
            return;

        if (!ShouldAdoptNewAttacker(attackerEntity))
        {
            damageFocusTarget = true;
            return;
        }

        OnAggroInterrupt();
        base.HandleAttackedBy(attackerEntity);
        RallyNearbyAllies(attackerEntity);
    }

    protected void RequestImmediatePath()
    {
        immediatePathOnce = true;
    }

    bool ConsumeImmediatePath()
    {
        if (!immediatePathOnce)
            return false;

        immediatePathOnce = false;
        return true;
    }

    /// <summary>피격 시 공유 어그로/반격에 참여할 수 있는지 (정지 명령 등).</summary>
    protected virtual bool CanJoinSharedAggro() => true;

    /// <summary>피격으로 표적을 바꿀 때 명령 상태 등을 해제한다.</summary>
    protected virtual void OnAggroInterrupt()
    {
        hasDestination = false;
        destinationTimer = 0f;
        ResetPathStuck();
    }

    protected void RallyNearbyAllies(SelectableEntity enemy)
    {
        if (!shareAggroWithAllies || selfEntity == null)
            return;

        RallyAlliesAround(
            transform.position,
            selfEntity.ownerId,
            selfEntity,
            enemy,
            aggroRange);
    }

    public static void RallyAlliesAround(
        Vector3 origin,
        int ownerId,
        SelectableEntity source,
        SelectableEntity enemy,
        float range)
    {
        if (enemy == null || range <= 0f)
            return;

        if (enemy.ownerId == ownerId)
            return;

        if (SpatialQueryWorld.Instance != null)
        {
            SpatialQueryWorld.Instance.CollectAlliesInRange(
                origin,
                ownerId,
                range,
                source,
                rallyBuffer);

            for (int i = 0; i < rallyBuffer.Count; i++)
                TryJoinAllyAttack(rallyBuffer[i], enemy);

            return;
        }

        float radiusSqr = range * range;
        IReadOnlyList<SelectableEntity> entities = SelectableRegistry.Entities;

        for (int i = 0; i < entities.Count; i++)
        {
            SelectableEntity ally = entities[i];
            if (ally == null || ally == source)
                continue;
            if (ally.entityType != SelectableEntityType.Unit)
                continue;
            if (ally.ownerId != ownerId)
                continue;

            Vector3 offset = ally.transform.position - origin;
            if (offset.sqrMagnitude > radiusSqr)
                continue;

            TryJoinAllyAttack(ally, enemy);
        }
    }

    static void TryJoinAllyAttack(SelectableEntity ally, SelectableEntity enemy)
    {
        ally.GetComponent<MobileCombatAI>()?.JoinAttack(enemy);
    }

    public virtual void JoinAttack(SelectableEntity enemy)
    {
        if (enemy == null)
            return;

        if (!CanJoinSharedAggro())
            return;

        if (!CanRetaliateAgainst(enemy))
            return;

        if (currentTarget == enemy)
            return;

        hasDestination = false;
        destinationTimer = 0f;
        CommandAttackTarget(enemy);
    }

    public virtual void LogTargetChange(SelectableEntity previous, SelectableEntity next)
    {
        if (!debugCommandLog || previous == next)
            return;

        UnitCommandDebugLog.Log(
            this,
            $"타겟 변경 {DescribeTarget(previous)} -> {DescribeTarget(next)}");
    }

    protected static string DescribeTarget(SelectableEntity target)
    {
        if (target == null)
            return "없음";

        return $"{target.name}({target.entityType})";
    }

    protected static string FormatVector(Vector3 value)
    {
        return $"({value.x:F1}, {value.y:F1}, {value.z:F1})";
    }
}
