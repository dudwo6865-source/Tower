using UnityEngine;
using UnityEngine.AI;

public static class TargetFinder
{
    public static SelectableEntity FindBestEnemyInRange(
        Vector3 fromPosition,
        int myOwnerId,
        float range,
        CombatTargetPriority priority,
        UnitAttacker engageFilter = null)
    {
        float rangeSqr = range * range;

        SelectableEntity bestUnit = null;
        SelectableEntity bestBuilding = null;
        SelectableEntity bestAny = null;
        SelectableEntity bestAttackerOfAlly = null;

        float minUnit = float.MaxValue;
        float minBuilding = float.MaxValue;
        float minAny = float.MaxValue;
        float minAttackerOfAlly = float.MaxValue;

        if (SpatialQueryWorld.Instance != null)
        {
            return SpatialQueryWorld.Instance.FindBestEnemyInRange(
                fromPosition,
                myOwnerId,
                range,
                priority,
                engageFilter);
        }

        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (entity == null || entity.ownerId == myOwnerId)
                continue;

            EntityHealth health = entity.CachedHealth;

            if (health != null && !health.IsAlive)
                continue;

            if (engageFilter != null && !engageFilter.CanEngage(entity))
                continue;

            float sqrDistance = GetHorizontalSqrDistance(fromPosition, entity.transform.position);

            if (sqrDistance > rangeSqr)
                continue;

            if (sqrDistance < minAny)
            {
                minAny = sqrDistance;
                bestAny = entity;
            }

            if (entity.entityType == SelectableEntityType.Unit &&
                sqrDistance < minUnit)
            {
                minUnit = sqrDistance;
                bestUnit = entity;
            }

            if (entity.entityType == SelectableEntityType.Building &&
                sqrDistance < minBuilding)
            {
                minBuilding = sqrDistance;
                bestBuilding = entity;
            }

            if (IsAttackingAlly(entity, myOwnerId) &&
                sqrDistance < minAttackerOfAlly)
            {
                minAttackerOfAlly = sqrDistance;
                bestAttackerOfAlly = entity;
            }
        }

        switch (priority)
        {
            case CombatTargetPriority.UnitsFirst:
                return bestUnit != null ? bestUnit : bestBuilding;

            case CombatTargetPriority.BuildingsFirst:
                return bestBuilding != null ? bestBuilding : bestUnit;

            case CombatTargetPriority.AttackersOfAlliesFirst:
                if (bestAttackerOfAlly != null)
                    return bestAttackerOfAlly;

                return bestUnit != null ? bestUnit : bestBuilding;

            default:
                return bestAny;
        }
    }

    static bool IsAttackingAlly(SelectableEntity enemy, int myOwnerId)
    {
        CombatAIBase combatAI = enemy.CachedCombatAI;

        if (combatAI == null)
            return false;

        SelectableEntity theirTarget = combatAI.CurrentTarget;

        return theirTarget != null && theirTarget.ownerId == myOwnerId;
    }

    public static SelectableEntity FindNearestEnemyBuilding(
        Vector3 fromPosition,
        int myOwnerId,
        UnitAttacker engageFilter = null)
    {
        SelectableEntity nearest = null;
        float minSqrDistance = float.MaxValue;

        if (SpatialQueryWorld.Instance != null)
        {
            return SpatialQueryWorld.Instance.FindNearestEnemyBuilding(
                fromPosition,
                myOwnerId,
                engageFilter);
        }

        foreach (SelectableEntity building in BuildingRegistry.Buildings)
        {
            if (building == null || building.ownerId == myOwnerId)
                continue;

            EntityHealth health = building.CachedHealth;

            if (health != null && !health.IsAlive)
                continue;

            if (engageFilter != null && !engageFilter.CanEngage(building))
                continue;

            float sqrDistance =
                (building.transform.position - fromPosition).sqrMagnitude;

            if (sqrDistance >= minSqrDistance)
                continue;

            minSqrDistance = sqrDistance;
            nearest = building;
        }

        return nearest;
    }

    public static SelectableEntity FindOpposingHeadquarters(
        int myOwnerId,
        UnitAttacker engageFilter = null)
    {
        if (BuildZoneManager.Instance != null &&
            BuildZoneManager.Instance.TryGetOpposingHeadquarters(
                myOwnerId,
                out Headquarters headquarters) &&
            headquarters != null)
        {
            SelectableEntity entity = headquarters.GetComponent<SelectableEntity>();

            if (IsUsableEnemy(entity, myOwnerId, engageFilter))
                return entity;
        }

        foreach (SelectableEntity building in BuildingRegistry.Buildings)
        {
            if (!IsUsableEnemy(building, myOwnerId, engageFilter))
                continue;

            if (building.GetComponent<Headquarters>() != null)
                return building;
        }

        return null;
    }

    static bool IsUsableEnemy(
        SelectableEntity entity,
        int myOwnerId,
        UnitAttacker engageFilter)
    {
        if (entity == null || entity.ownerId == myOwnerId)
            return false;

        EntityHealth health = entity.CachedHealth;

        if (health != null && !health.IsAlive)
            return false;

        return engageFilter == null || engageFilter.CanEngage(entity);
    }

    static float GetHorizontalSqrDistance(Vector3 fromPosition, Vector3 toPosition)
    {
        Vector3 delta = toPosition - fromPosition;
        delta.y = 0f;
        return delta.sqrMagnitude;
    }

    public static Vector3 GetApproachPosition(
        Vector3 fromPosition,
        SelectableEntity target,
        float stoppingDistance,
        float attackRange,
        float angleOffsetDegrees = 0f)
    {
        if (target == null)
            return fromPosition;

        if (target.entityType == SelectableEntityType.Unit)
        {
            return GetUnitApproachPosition(
                fromPosition,
                target,
                attackRange,
                angleOffsetDegrees);
        }

        return GetBuildingApproachPosition(
            fromPosition,
            target,
            stoppingDistance,
            angleOffsetDegrees);
    }

    static Vector3 GetUnitApproachPosition(
        Vector3 fromPosition,
        SelectableEntity target,
        float attackRange,
        float angleOffsetDegrees)
    {
        Bounds bounds = target.SelectionBounds;
        float targetRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float sampleRadius = attackRange + targetRadius + 2f;

        Vector3 closest = bounds.ClosestPoint(fromPosition);
        Vector3 towardChaser = fromPosition - closest;
        towardChaser.y = 0f;

        if (towardChaser.sqrMagnitude < 0.01f)
        {
            towardChaser = fromPosition - target.transform.position;
            towardChaser.y = 0f;
        }

        if (towardChaser.sqrMagnitude < 0.01f)
            towardChaser = Vector3.forward;

        towardChaser.Normalize();

        if (Mathf.Abs(angleOffsetDegrees) > 0.01f)
            towardChaser = Quaternion.Euler(0f, angleOffsetDegrees, 0f) * towardChaser;

        float holdDistance = Mathf.Clamp(
            attackRange * 0.7f,
            0.2f,
            attackRange + targetRadius);

        Vector3 ideal = closest + towardChaser * holdDistance;

        // complete 우선 직근 → 각도 확장 → 실패 시 partial 폴백 (이전 이중 전수 루프 제거).
        if (TryFindReachableApproach(
                fromPosition,
                ideal,
                sampleRadius,
                out Vector3 approach,
                out _,
                out _,
                requireCompletePath: true))
            return approach;

        for (int i = 1; i < 8; i++)
        {
            Vector3 direction =
                Quaternion.Euler(0f, i * 45f, 0f) * towardChaser;

            if (TryFindReachableApproach(
                    fromPosition,
                    closest + direction * holdDistance,
                    sampleRadius,
                    out approach,
                    out _,
                    out _,
                    requireCompletePath: true))
                return approach;
        }

        if (TryFindReachableApproach(
                fromPosition,
                ideal,
                sampleRadius,
                out approach,
                out _,
                out _))
            return approach;

        return SampleNavMeshNear(ideal, sampleRadius);
    }

    static Vector3 GetBuildingApproachPosition(
        Vector3 fromPosition,
        SelectableEntity target,
        float stoppingDistance,
        float angleOffsetDegrees)
    {
        // 한 번의 탐색으로 complete/partial을 함께 고른다 (이전: complete 전수 + partial 전수).
        if (TryFindBestBuildingApproach(
                fromPosition,
                target,
                stoppingDistance,
                angleOffsetDegrees,
                out Vector3 bestApproach,
                out _,
                out _))
        {
            return bestApproach;
        }

        Vector3 targetPosition = target.transform.position;
        Bounds bounds = target.SelectionBounds;
        float targetRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float ringRadius = targetRadius + stoppingDistance + 1f;

        Vector3 outward = fromPosition - targetPosition;
        outward.y = 0f;

        if (outward.sqrMagnitude < 0.01f)
            outward = Vector3.forward;

        outward.Normalize();

        if (Mathf.Abs(angleOffsetDegrees) > 0.01f)
            outward = Quaternion.Euler(0f, angleOffsetDegrees, 0f) * outward;

        return SampleNavMeshNear(
            targetPosition + outward * ringRadius,
            stoppingDistance + targetRadius + 2f);
    }

    public static bool TryGetAlternateApproachPosition(
        Vector3 fromPosition,
        SelectableEntity target,
        float stoppingDistance,
        float attackRange,
        float baseAngleOffsetDegrees,
        int attemptOffset,
        Vector3 avoidPosition,
        out Vector3 approachPosition)
    {
        approachPosition = fromPosition;

        if (target == null)
            return false;

        // 가벼운 링 샘플만 사용 (GetApproachPosition 전수 탐색을 중첩 호출하지 않음).
        const int directionsPerRound = 8;
        const float angleStep = 360f / directionsPerRound;
        float minMoveSqr = 0.25f;
        float avoidSqr = 1f;

        for (int i = 0; i < directionsPerRound; i++)
        {
            float angle = baseAngleOffsetDegrees + (attemptOffset * angleStep) + i * angleStep;

            if (TryPickApproachCandidate(
                    fromPosition,
                    target,
                    stoppingDistance,
                    attackRange,
                    angle,
                    avoidPosition,
                    avoidSqr,
                    minMoveSqr,
                    out approachPosition))
            {
                return true;
            }
        }

        Vector3 targetPosition = target.transform.position;
        Bounds bounds = target.SelectionBounds;
        float targetRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float baseRing = targetRadius + attackRange + stoppingDistance * 0.5f;

        for (int ring = 1; ring <= 2; ring++)
        {
            float ringRadius = baseRing + ring * 1.5f;
            float sampleRadius = attackRange + targetRadius + 2f + ring;

            for (int i = 0; i < directionsPerRound; i++)
            {
                float angleDeg = baseAngleOffsetDegrees + (attemptOffset + i) * angleStep;
                Vector3 direction = Quaternion.Euler(0f, angleDeg, 0f) * Vector3.forward;
                Vector3 sampleOrigin = targetPosition + direction * ringRadius;

                if (!TryFindReachableApproach(
                        fromPosition,
                        sampleOrigin,
                        sampleRadius,
                        out Vector3 reachable,
                        out _,
                        out _))
                {
                    continue;
                }

                if ((reachable - fromPosition).sqrMagnitude < minMoveSqr)
                    continue;

                if ((reachable - avoidPosition).sqrMagnitude < avoidSqr)
                    continue;

                approachPosition = reachable;
                return true;
            }
        }

        return false;
    }

    static bool TryPickApproachCandidate(
        Vector3 fromPosition,
        SelectableEntity target,
        float stoppingDistance,
        float attackRange,
        float angleOffsetDegrees,
        Vector3 avoidPosition,
        float avoidSqr,
        float minMoveSqr,
        out Vector3 approachPosition)
    {
        approachPosition = fromPosition;

        Vector3 targetPosition = target.transform.position;
        Bounds bounds = target.SelectionBounds;
        float targetRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float ringRadius = targetRadius + stoppingDistance + 1f;
        float sampleRadius = attackRange + targetRadius + 2f;

        Vector3 outward = fromPosition - targetPosition;
        outward.y = 0f;

        if (outward.sqrMagnitude < 0.01f)
            outward = Vector3.forward;

        outward.Normalize();
        outward = Quaternion.Euler(0f, angleOffsetDegrees, 0f) * outward;

        Vector3 sampleOrigin = targetPosition + outward * ringRadius;

        if (!TryFindReachableApproach(
                fromPosition,
                sampleOrigin,
                sampleRadius,
                out Vector3 reachable,
                out _,
                out _))
        {
            return false;
        }

        if ((reachable - fromPosition).sqrMagnitude < minMoveSqr)
            return false;

        if ((reachable - avoidPosition).sqrMagnitude < avoidSqr)
            return false;

        approachPosition = reachable;
        return true;
    }

    static Vector3 SampleNavMeshNear(Vector3 worldPosition, float sampleRadius)
    {
        if (NavMesh.SamplePosition(
                worldPosition,
                out NavMeshHit hit,
                sampleRadius,
                NavMesh.AllAreas))
        {
            return hit.position;
        }

        return worldPosition;
    }

    static bool TryFindBestBuildingApproach(
        Vector3 fromPosition,
        SelectableEntity target,
        float stoppingDistance,
        float angleOffsetDegrees,
        out Vector3 bestApproach,
        out NavMeshPathStatus bestStatus,
        out float bestPathLength)
    {
        bestApproach = fromPosition;
        bestStatus = NavMeshPathStatus.PathInvalid;
        bestPathLength = float.MaxValue;

        if (target == null)
            return false;

        Vector3 targetPosition = target.transform.position;
        Bounds bounds = target.SelectionBounds;
        float targetRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float ringRadius = targetRadius + stoppingDistance + 1f;
        float sampleRadius = stoppingDistance + targetRadius + 2f;

        Vector3 outward = fromPosition - targetPosition;
        outward.y = 0f;

        if (outward.sqrMagnitude < 0.01f)
            outward = Vector3.forward;

        outward.Normalize();

        if (Mathf.Abs(angleOffsetDegrees) > 0.01f)
            outward = Quaternion.Euler(0f, angleOffsetDegrees, 0f) * outward;

        bool found = false;
        Vector3 currentApproach = bestApproach;
        NavMeshPathStatus currentStatus = bestStatus;
        float currentPathLength = bestPathLength;
        int pathBudget = MaxBuildingApproachPathCalculations;

        // 1) 적 방향(바깥) 직근 접근점 우선 — 대부분 여기서 PathComplete로 끝난다.
        if (TryConsiderBuildingApproach(
                fromPosition,
                targetPosition + outward * ringRadius,
                sampleRadius,
                requireCompletePath: false,
                ref currentApproach,
                ref currentStatus,
                ref currentPathLength,
                ref pathBudget))
        {
            found = true;

            if (currentStatus == NavMeshPathStatus.PathComplete)
            {
                bestApproach = currentApproach;
                bestStatus = currentStatus;
                bestPathLength = currentPathLength;
                return true;
            }
        }

        // 2) 축소된 링/방향 탐색. PathComplete를 찾으면 즉시 종료.
        const int directionCount = 8;
        const int maxRing = 3;
        float angleStep = 360f / directionCount;

        for (int ring = 0; ring <= maxRing && pathBudget > 0; ring++)
        {
            float radius = ringRadius + ring * 2f;
            float ringSampleRadius = Mathf.Max(sampleRadius, radius * 0.35f);

            for (int i = 0; i < directionCount && pathBudget > 0; i++)
            {
                // ring 0, i 0은 직근과 동일하므로 스킵
                if (ring == 0 && i == 0)
                    continue;

                Vector3 direction =
                    Quaternion.Euler(0f, i * angleStep, 0f) * outward;

                if (!TryConsiderBuildingApproach(
                        fromPosition,
                        targetPosition + direction * radius,
                        ringSampleRadius,
                        requireCompletePath: false,
                        ref currentApproach,
                        ref currentStatus,
                        ref currentPathLength,
                        ref pathBudget))
                {
                    continue;
                }

                found = true;

                if (currentStatus == NavMeshPathStatus.PathComplete)
                {
                    bestApproach = currentApproach;
                    bestStatus = currentStatus;
                    bestPathLength = currentPathLength;
                    return true;
                }
            }
        }

        if (!found)
            return false;

        bestApproach = currentApproach;
        bestStatus = currentStatus;
        bestPathLength = currentPathLength;
        return true;
    }

    const int MaxBuildingApproachPathCalculations = 20;

    static readonly NavMeshPath SharedPath = new NavMeshPath();

    static bool TryConsiderBuildingApproach(
        Vector3 fromPosition,
        Vector3 sampleOrigin,
        float sampleRadius,
        bool requireCompletePath,
        ref Vector3 bestApproach,
        ref NavMeshPathStatus bestStatus,
        ref float bestPathLength,
        ref int pathBudget)
    {
        if (pathBudget <= 0)
            return false;

        pathBudget--;

        if (!TryFindReachableApproach(
                fromPosition,
                sampleOrigin,
                sampleRadius,
                out Vector3 approach,
                out NavMeshPathStatus status,
                out float pathLength,
                requireCompletePath))
        {
            return false;
        }

        if (!IsBetterApproachCandidate(status, pathLength, bestStatus, bestPathLength))
            return false;

        bestApproach = approach;
        bestStatus = status;
        bestPathLength = pathLength;
        return true;
    }

    static bool IsBetterApproachCandidate(
        NavMeshPathStatus candidateStatus,
        float candidatePathLength,
        NavMeshPathStatus currentBestStatus,
        float currentBestPathLength)
    {
        if (currentBestPathLength >= float.MaxValue)
            return true;

        if (candidateStatus == NavMeshPathStatus.PathComplete &&
            currentBestStatus != NavMeshPathStatus.PathComplete)
        {
            return true;
        }

        if (candidateStatus != NavMeshPathStatus.PathComplete &&
            currentBestStatus == NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        return candidatePathLength < currentBestPathLength;
    }

    static float CalculatePathLength(NavMeshPath path)
    {
        if (path == null || path.corners == null || path.corners.Length < 2)
            return 0f;

        float length = 0f;

        for (int i = 1; i < path.corners.Length; i++)
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);

        return length;
    }

    static bool TryFindReachableApproach(
        Vector3 fromPosition,
        Vector3 sampleOrigin,
        float sampleRadius,
        out Vector3 result,
        out NavMeshPathStatus pathStatus,
        out float pathLength,
        bool requireCompletePath = false)
    {
        result = sampleOrigin;
        pathStatus = NavMeshPathStatus.PathInvalid;
        pathLength = float.MaxValue;

        Vector3 sampled;

        if (UnitSpawnUtility.TrySampleNavMeshNearPreferredHeight(
                sampleOrigin,
                fromPosition.y,
                sampleRadius,
                out sampled))
        {
        }
        else if (NavMesh.SamplePosition(
                     sampleOrigin,
                     out NavMeshHit hit,
                     sampleRadius,
                     NavMesh.AllAreas))
        {
            sampled = hit.position;
        }
        else
        {
            return false;
        }

        SharedPath.ClearCorners();

        if (!NavMesh.CalculatePath(
                fromPosition,
                sampled,
                NavMesh.AllAreas,
                SharedPath))
            return false;

        if (SharedPath.status == NavMeshPathStatus.PathInvalid)
            return false;

        if (requireCompletePath && SharedPath.status != NavMeshPathStatus.PathComplete)
            return false;

        result = sampled;
        pathStatus = SharedPath.status;
        pathLength = CalculatePathLength(SharedPath);
        return true;
    }
}
