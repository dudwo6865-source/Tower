using System.Collections.Generic;
using UnityEngine;

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

    static readonly List<SelectableEntity> squadBuffer = new List<SelectableEntity>(32);

    EnemyCombatAI squadLeader;
    int squadClaimFrame = -1;
    float localEnemyCheckTimer;
    bool hasLocalEnemy;

    void Update()
    {
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
            AdoptSquadLeaderTarget();
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
                continue;
            }

            ai.squadLeader = this;
            ai.squadClaimFrame = Time.frameCount;
        }
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

        if (!hasLocalEnemy)
            return true;

        // 리더와 같은 건물을 때리면 경로를 다시 잡지 않는다.
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
        if (squadLeader != null &&
            squadLeader.TryGetFollowablePathCorners(out Vector3[] corners))
        {
            AdoptPathCorners(corners);
            if (TryFollowCachedPath())
                return true;
        }

        return base.TryFollowSquadFallback();
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
        targetPriority = CombatTargetPriority.UnitsFirst;
    }
}
