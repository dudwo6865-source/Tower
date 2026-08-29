using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 적 전용 전투 AI. 기본 목표는 HQ이며, 어그로 범위 안의 유닛/건물만 가로채 공격합니다.
/// Update가 먼저 도는 유닛이 리더가 되어 타겟을 찾고 경로를 계산하며,
/// 범위 안 아군은 그 표적을 공유하고 SetDestination을 건너뜁니다.
/// </summary>
public class EnemyCombatAI : MobileCombatAI
{
    [Header("Behavior")]
    [Tooltip("어그로 범위에 적이 없으면 상대 HQ로 진군합니다.")]
    public bool advanceToEnemyBuildings = true;

    [Header("Squad")]
    [Tooltip("먼저 Update된 유닛이 리더가 되고, 범위 안 아군은 타겟 탐색과 SetDestination을 하지 않습니다. 어그로 범위의 적 유닛, 또는 사거리 안의 건물이 있으면 그 유닛만 리더를 무시합니다.")]
    public bool shareTargetWithNearbyAllies = true;
    [Tooltip("이 거리 안의 같은 진영 적 유닛을 한 스쿼드로 봅니다.")]
    public float squadRadius = 12f;

    [Tooltip("팔로워가 리더 기준 좌우로 벌어지는 폭(m)입니다. 0이면 리더 좌표로 몰립니다.")]
    public float squadFollowSpread = 2.5f;

    [Tooltip("팔로워가 리더 뒤로 물러나는 간격(m)입니다. 부채꼴 대형의 깊이입니다.")]
    public float squadFollowSpacing = 2f;

    [Header("Building Chase")]
    [Tooltip("직선 거리 대비 실제 경로가 이 배수 이상 길어지면(또는 경로가 끊기면) " +
        "가는 길을 막고 있는 다른 건물을 대신 공격 대상으로 삼습니다.")]
    public float detourRedirectMultiplier = 1.6f;

    [Tooltip("우회 여부를 다시 검사하는 간격(초)입니다.")]
    public float detourCheckInterval = 1f;

    const float SquadClaimInterval = 0.25f;

    // 리더를 빼앗는 데 필요한 거리 우위(m)의 제곱입니다. 2m 마진.
    const float SquadLeaderSwitchMarginSqr = 4f;

    static readonly List<SelectableEntity> squadBuffer = new List<SelectableEntity>(32);

    EnemyCombatAI squadLeader;
    int squadClaimFrame = -1;
    float localEnemyCheckTimer;
    bool hasLocalEnemy;
    float squadClaimCheckTimer;
    float squadOffsetFactor;
    float detourCheckTimer;

    protected override void Awake()
    {
        base.Awake();

        // 인스턴스마다 고정된 좌우 오프셋을 줘서 팔로워가 한 점으로 몰리지 않게 합니다.
        float normalized = (GetInstanceID() & 0xFFFF) / 65535f;
        squadOffsetFactor = normalized * 2f - 1f;

        // 스폰 직후 전원이 같은 프레임에 팔로워를 긁지 않도록 흩뿌립니다.
        squadClaimCheckTimer = normalized * SquadClaimInterval;
        detourCheckTimer = normalized * detourCheckInterval;
    }

    void Update()
    {
        // 사거리 안에서 유효한 표적을 때리는 중이면 스쿼드 추종/재탐색을 완전히 쉰다.
        // (내가 때리는 표적 자체가 "사거리 안의 다른 진영 개체"라 HasLocalEnemy가 항상
        // true가 되어 버려서, 이 스킵이 없으면 매 프레임 follower가 false로 빠지고
        // TickRetarget이 돌아 공격 중에도 다른 표적으로 흔들릴 수 있다.)
        // 표적을 잃거나 사거리를 벗어나야만 다시 리더를 보거나 새로 찾는다.
        if (HasValidTarget() && attacker.IsInRange(currentTarget))
        {
            UpdateCombat();
            return;
        }

        bool wouldFollow = ShouldFollowSquadLeader();
        bool breakForLocalEnemy = wouldFollow && HasLocalEnemy();
        bool follower = wouldFollow && !breakForLocalEnemy;

        if (damageFocusTarget)
        {
            if (!HasValidTarget())
                ClearDamageFocusTarget();
        }
        else if (follower)
        {
            if (!HasValidTarget())
            {
                // 표적이 죽었을 때는 리더 표적을 그대로 받지 않고, 이 유닛 기준으로
                // 가까운 표적을 직접 찾는다(retargetTimer가 알아서 쓰로틀해준다).
                // 주변에 감지되는 게 전혀 없을 때만(막 스폰 직후 등) 리더를 따른다.
                TickRetarget();

                if (!HasValidTarget())
                    AdoptSquadLeaderTarget();
            }
            else if (!attacker.IsInRange(currentTarget))
            {
                // 이미 사거리 안에 공격 가능한 다른 대상이 있으면 그걸 개별적으로 공격한다.
                // 타워가 뭉쳐 있을 때 전원이 리더가 찜한 타워 하나만 보고 정체되는 것을 막는다.
                SelectableEntity nearbyInRange = FindInAttackRangeTarget();
                if (nearbyInRange != null)
                    SetTarget(nearbyInRange);
            }
        }
        else
        {
            TickRetarget();

            // 근처 적 때문에 빠진 팔로워는 새 리더가 되어 무리를 끌어가지 않는다.
            if (!breakForLocalEnemy)
                PromoteToSquadLeader();
        }

        if (!HasValidTarget())
            return;

        // 아직 사거리 밖이면(=이동/우회 중), 다른 건물이 가는 길을 막고 있는지 확인한다.
        if (!attacker.IsInRange(currentTarget))
            TryRedirectToBlockingBuilding();

        UpdateCombat();
    }

    bool ShouldFollowSquadLeader()
    {
        if (!shareTargetWithNearbyAllies)
            return false;

        if (WasClaimedThisFrame() && LeaderCanBeFollowed(squadLeader))
            return true;

        return squadLeader != this &&
               LeaderCanBeFollowed(squadLeader) &&
               IsWithinSquadRange(squadLeader, keep: true);
    }

    bool IsSquadFollower()
    {
        return ShouldFollowSquadLeader();
    }

    bool LeaderCanBeFollowed(EnemyCombatAI leader)
    {
        return IsUsableLeader(leader) && leader.HasValidTarget();
    }

    bool HasLocalEnemy()
    {
        localEnemyCheckTimer -= Time.deltaTime;
        if (localEnemyCheckTimer > 0f)
            return hasLocalEnemy;

        localEnemyCheckTimer = 0.2f;
        hasLocalEnemy = QueryLocalEnemy();
        return hasLocalEnemy;
    }

    bool QueryLocalEnemy()
    {
        if (selfEntity == null)
            return false;

        // 유닛은 어그로 범위, 건물은 사거리 안에서만 리더를 무시한다.
        // 타워 설치 직후 30m 안의 전원이 빠져나와 SetDestination 하는 것을 막는다.
        if (HasOtherOwnerNearby(aggroRange, unitsOnly: true))
            return true;

        float buildingRange = attacker != null ? attacker.AttackRange : 0f;
        if (buildingRange <= 0.01f)
            return false;

        return HasOtherOwnerNearby(buildingRange, unitsOnly: false);
    }

    bool HasOtherOwnerNearby(float range, bool unitsOnly)
    {
        if (range <= 0.01f)
            return false;

        if (SpatialQueryWorld.Instance != null)
        {
            return SpatialQueryWorld.Instance.HasOtherOwnerInRange(
                transform.position,
                selfEntity.ownerId,
                range,
                unitsOnly);
        }

        float rangeSqr = range * range;
        IReadOnlyList<SelectableEntity> all = SelectableRegistry.Entities;

        for (int i = 0; i < all.Count; i++)
        {
            SelectableEntity other = all[i];
            if (other == null || other == selfEntity)
                continue;
            if (other.ownerId == selfEntity.ownerId)
                continue;
            if (unitsOnly && other.entityType != SelectableEntityType.Unit)
                continue;

            EntityHealth health = other.CachedHealth;
            if (health != null && !health.IsAlive)
                continue;

            Vector3 delta = other.transform.position - transform.position;
            if (delta.sqrMagnitude <= rangeSqr)
                return true;
        }

        return false;
    }

    void PromoteToSquadLeader()
    {
        if (!shareTargetWithNearbyAllies || selfEntity == null)
        {
            squadLeader = this;
            return;
        }

        squadLeader = this;

        // 한 번 붙은 팔로워는 IsWithinSquadRange로 계속 유지되므로,
        // 새로 들어오는 아군만 주기적으로 잡아주면 충분합니다.
        squadClaimCheckTimer -= Time.deltaTime;
        if (squadClaimCheckTimer > 0f)
            return;

        squadClaimCheckTimer = SquadClaimInterval;

        if (HasValidTarget())
            ClaimNearbyFollowers();
    }

    bool WasClaimedThisFrame()
    {
        return squadClaimFrame == Time.frameCount &&
               squadLeader != null &&
               squadLeader != this &&
               IsUsableLeader(squadLeader);
    }

    void ClaimNearbyFollowers()
    {
        CollectSquadAllies();

        for (int i = 0; i < squadBuffer.Count; i++)
        {
            SelectableEntity ally = squadBuffer[i];
            if (ally == null || ally.entityType != SelectableEntityType.Unit)
                continue;

            EnemyCombatAI ai = ally.GetComponent<EnemyCombatAI>();
            if (ai == null || !ai.shareTargetWithNearbyAllies || !IsUsableLeader(ai))
                continue;

            if (ai.squadClaimFrame == Time.frameCount)
                continue;

            if (ai.squadLeader != null &&
                ai.squadLeader != this &&
                ai.squadLeader != ai &&
                IsUsableLeader(ai.squadLeader) &&
                ai.IsWithinSquadRange(ai.squadLeader, keep: true))
            {
                if (!IsClearlyCloserToTargetThan(ai.squadLeader))
                    continue;
            }

            ai.squadLeader = this;
            ai.squadClaimFrame = Time.frameCount;
        }
    }

    /// <summary>
    /// "먼저 Update된 쪽이 리더"는 프레임마다 뒤집혀서 스쿼드가 계속 재편성됩니다.
    /// 표적에 뚜렷하게 더 가까울 때만 기존 리더를 빼앗아 위치 기반으로 안정화합니다.
    /// </summary>
    bool IsClearlyCloserToTargetThan(EnemyCombatAI existingLeader)
    {
        if (!HasValidTarget() || existingLeader == null)
            return false;

        Vector3 targetPosition = currentTarget.transform.position;
        float existingSqr =
            (existingLeader.transform.position - targetPosition).sqrMagnitude;
        float mySqr = (transform.position - targetPosition).sqrMagnitude;

        return existingSqr > mySqr + SquadLeaderSwitchMarginSqr;
    }

    void CollectSquadAllies()
    {
        if (SpatialQueryWorld.Instance != null)
        {
            SpatialQueryWorld.Instance.CollectAlliesInRange(
                transform.position,
                selfEntity.ownerId,
                squadRadius,
                selfEntity,
                squadBuffer);
            return;
        }

        squadBuffer.Clear();

        float radiusSqr = squadRadius * squadRadius;
        IReadOnlyList<SelectableEntity> all = SelectableRegistry.Entities;

        for (int i = 0; i < all.Count; i++)
        {
            SelectableEntity other = all[i];
            if (other == null || other == selfEntity)
                continue;
            if (other.ownerId != selfEntity.ownerId)
                continue;
            if (other.entityType != SelectableEntityType.Unit)
                continue;

            Vector3 delta = other.transform.position - transform.position;
            if (delta.sqrMagnitude > radiusSqr)
                continue;

            squadBuffer.Add(other);
        }
    }

    void AdoptSquadLeaderTarget()
    {
        SelectableEntity leaderTarget = squadLeader.CurrentTarget;
        if (leaderTarget != currentTarget)
            SetTarget(leaderTarget);
    }

    /// <summary>
    /// 지금 이 위치에서 실제로 공격 사거리 안에 들어온 대상을 찾는다.
    /// 팔로워가 리더의 표적이 아니라 자기 옆의 다른 대상을 즉시 공격할 수 있게 한다.
    /// </summary>
    SelectableEntity FindInAttackRangeTarget()
    {
        if (selfEntity == null || attacker == null)
            return null;

        float range = attacker.AttackRange;
        if (range <= 0.01f)
            return null;

        // TargetFinder는 중심 좌표 기준 거리라 콜라이더가 큰 건물엔 실제 사거리보다 빡빡할 수 있다.
        // 여유를 두고 후보를 찾은 뒤, 콜라이더 경계 기준인 attacker.IsInRange로 다시 확인한다.
        SelectableEntity candidate = TargetFinder.FindBestEnemyInRange(
            transform.position,
            selfEntity.ownerId,
            range + 2f,
            targetPriority,
            attacker);

        return candidate != null && attacker.IsInRange(candidate) ? candidate : null;
    }

    /// <summary>
    /// 지금 표적(건물)으로 가는 경로가 다른 건물 때문에 끊기거나 크게 우회하고 있으면,
    /// 실제로 길을 막고 있는 그 건물을 대신 공격 대상으로 삼는다.
    /// 막던 건물이 죽으면 다음 TickRetarget/FindInAttackRangeTarget이 원래 방향을 알아서
    /// 이어받으므로, 원래 표적을 따로 기억해뒀다가 되돌릴 필요는 없다.
    /// </summary>
    void TryRedirectToBlockingBuilding()
    {
        if (currentTarget == null || currentTarget.entityType != SelectableEntityType.Building)
            return;

        if (agent == null || !agent.isOnNavMesh || agent.pathPending)
            return;

        detourCheckTimer -= Time.deltaTime;
        if (detourCheckTimer > 0f)
            return;
        detourCheckTimer = detourCheckInterval;

        bool blocked = agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathPartial;
        bool longDetour = false;

        if (!blocked && agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete)
        {
            float straight = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (straight > 0.01f)
            {
                float pathLength = GetPathLength(agent.path.corners);
                longDetour = pathLength > straight * detourRedirectMultiplier;
            }
        }

        if (!blocked && !longDetour)
            return;

        SelectableEntity blocker = FindBuildingBlockingLineTo(currentTarget);
        if (blocker == null || blocker == currentTarget)
            return;

        SetTarget(blocker);
    }

    static float GetPathLength(Vector3[] corners)
    {
        if (corners == null || corners.Length < 2)
            return 0f;

        float length = 0f;

        for (int i = 1; i < corners.Length; i++)
            length += Vector3.Distance(corners[i - 1], corners[i]);

        return length;
    }

    /// <summary>
    /// 나(transform.position)에서 target까지 직선이 지나가는 다른 적 건물을 찾는다.
    /// 여러 개가 겹치면 나한테 더 가까운 쪽(먼저 부딪히는 쪽)을 고른다.
    /// </summary>
    SelectableEntity FindBuildingBlockingLineTo(SelectableEntity target)
    {
        if (selfEntity == null)
            return null;

        Vector3 from = transform.position;
        Vector3 to = target.transform.position;

        SelectableEntity best = null;
        float bestSqr = float.MaxValue;

        IReadOnlyList<SelectableEntity> all = SelectableRegistry.Entities;

        for (int i = 0; i < all.Count; i++)
        {
            SelectableEntity other = all[i];
            if (other == null || other == target || other == selfEntity)
                continue;
            if (other.entityType != SelectableEntityType.Building)
                continue;
            if (other.ownerId == selfEntity.ownerId)
                continue;

            EntityHealth health = other.CachedHealth;
            if (health != null && !health.IsAlive)
                continue;

            if (!SegmentIntersectsBoundsXZ(from, to, other.SelectionBounds))
                continue;

            float sqr = (other.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = other;
            }
        }

        return best;
    }

    // Y를 무시한 2D(XZ) 선분-AABB 교차 검사입니다(Liang-Barsky 클리핑).
    static bool SegmentIntersectsBoundsXZ(Vector3 from, Vector3 to, Bounds bounds)
    {
        float dx = to.x - from.x;
        float dz = to.z - from.z;

        float t0 = 0f;
        float t1 = 1f;

        if (!ClipSegment(-dx, from.x - bounds.min.x, ref t0, ref t1)) return false;
        if (!ClipSegment(dx, bounds.max.x - from.x, ref t0, ref t1)) return false;
        if (!ClipSegment(-dz, from.z - bounds.min.z, ref t0, ref t1)) return false;
        if (!ClipSegment(dz, bounds.max.z - from.z, ref t0, ref t1)) return false;

        return t0 <= t1;
    }

    static bool ClipSegment(float p, float q, ref float t0, ref float t1)
    {
        if (Mathf.Abs(p) < 1e-6f)
            return q >= 0f;

        float r = q / p;

        if (p < 0f)
        {
            if (r > t1) return false;
            if (r > t0) t0 = r;
        }
        else
        {
            if (r < t0) return false;
            if (r < t1) t1 = r;
        }

        return true;
    }

    static bool IsUsableLeader(EnemyCombatAI leader)
    {
        return leader != null && leader.isActiveAndEnabled;
    }

    bool IsWithinSquadRange(EnemyCombatAI other, bool keep)
    {
        if (other == null)
            return false;

        float radius = keep ? squadRadius * 1.25f : squadRadius;
        Vector3 delta = other.transform.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= radius * radius;
    }

    protected override bool ShouldSkipSetDestination()
    {
        if (!IsSquadFollower() || damageFocusTarget)
            return false;

        // 건물 근처에서는 공유 경로를 끊고 각자 외곽으로 붙는다.
        if (IsDirectBuildingChase)
            return false;

        // 공유 경로로는 빠져나올 수 없는 자리에 끼면 스스로 경로를 잡는다.
        if (IsPathStuck())
            return false;

        // 리더와 다른 표적을 쫓고 있으면 공유 경로가 의미 없다 - 직접 경로를 계산한다.
        // (표적을 잃은 팔로워가 자기 기준으로 새 표적을 개별로 찾을 수 있어, 더 이상
        // "팔로워는 항상 리더와 같은 표적"이라고 가정할 수 없다.)
        return LeaderHasSameTarget();
    }

    bool LeaderHasSameTarget()
    {
        return squadLeader != null &&
               squadLeader.HasValidTarget() &&
               currentTarget == squadLeader.CurrentTarget;
    }

    protected override bool TryFollowSquadFallback()
    {
        // 리더의 경로를 그대로 따라가면 리더와 같은 좌표로 몰린다.
        // 경로 계산은 아끼면서, 도착 지점만 인스턴스별로 벌려 놓는다.
        if (TrySteerToSquadSlot())
            return true;

        if (squadLeader != null &&
            squadLeader != this &&
            squadLeader.TryGetFollowablePathCorners(out Vector3[] corners))
        {
            AdoptPathCorners(corners);
            if (TryFollowCachedPath())
                return true;
        }

        return base.TryFollowSquadFallback();
    }

    /// <summary>
    /// 리더를 기준으로 표적 반대쪽에 부채꼴로 벌어진 자리를 잡습니다.
    /// 리더의 현재 위치를 기준점으로 쓰므로 직선 스티어링 거리가 스쿼드 반경 안으로 제한됩니다.
    /// </summary>
    bool TrySteerToSquadSlot()
    {
        if (squadLeader == null || squadLeader == this || currentTarget == null)
            return false;

        if (squadFollowSpread <= 0.01f && squadFollowSpacing <= 0.01f)
            return false;

        Vector3 anchor = squadLeader.transform.position;
        Vector3 toTarget = currentTarget.transform.position - anchor;
        toTarget.y = 0f;

        Vector3 forward = toTarget.sqrMagnitude > 0.0001f
            ? toTarget.normalized
            : squadLeader.transform.forward;

        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 slot =
            anchor
            + right * (squadOffsetFactor * squadFollowSpread)
            - forward * (Mathf.Abs(squadOffsetFactor) * squadFollowSpacing);

        return TrySteerToward(slot);
    }

    public void SetAdvanceToEnemyBuildings(bool enabled)
    {
        advanceToEnemyBuildings = enabled;

        if (!enabled &&
            !damageFocusTarget &&
            currentTarget != null &&
            currentTarget.GetComponent<Headquarters>() != null)
        {
            SetTarget(null);
        }

        ResetRetargetTimer();
    }

    protected override SelectableEntity FindTarget()
    {
        SelectableEntity inAggro = base.FindTarget();

        if (inAggro != null)
            return inAggro;

        if (!advanceToEnemyBuildings || selfEntity == null)
            return null;

        return TargetFinder.FindOpposingHeadquarters(
            selfEntity.ownerId,
            attacker);
    }

    void Reset()
    {
        advanceToEnemyBuildings = true;
        shareTargetWithNearbyAllies = true;
        squadRadius = 12f;
        squadFollowSpread = 2.5f;
        squadFollowSpacing = 2f;
        detourRedirectMultiplier = 1.6f;
        detourCheckInterval = 1f;
        targetPriority = CombatTargetPriority.UnitsFirst;
    }

    // Debug Command Log를 켜면 팔로워에서 리더로 선을 그어 스쿼드 재편성을 눈으로 확인합니다.
    void OnDrawGizmos()
    {
        if (!debugCommandLog || squadLeader == null || squadLeader == this)
            return;

        Vector3 from = transform.position + Vector3.up * 0.5f;
        Vector3 to = squadLeader.transform.position + Vector3.up * 0.5f;

        Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.9f);
        Gizmos.DrawLine(from, to);
        Gizmos.DrawSphere(to, 0.2f);
    }
}
