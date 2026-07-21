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

        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (entity == null || entity.ownerId == myOwnerId)
                continue;

            EntityHealth health = entity.GetComponent<EntityHealth>();

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
        CombatAIBase combatAI = enemy.GetComponent<CombatAIBase>();

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

        foreach (SelectableEntity building in BuildingRegistry.Buildings)
        {
            if (building == null || building.ownerId == myOwnerId)
                continue;

            EntityHealth health = building.GetComponent<EntityHealth>();

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

        if (TryFindReachableApproach(
                fromPosition,
                ideal,
                sampleRadius,
                out Vector3 approach,
                out _,
                out _,
                requireCompletePath: true))
            return approach;

        for (int i = 0; i < 8; i++)
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
                target.transform.position,
                sampleRadius,
                out approach,
                out _,
                out _,
                requireCompletePath: true))
            return approach;

        if (TryFindReachableApproach(
                fromPosition,
                ideal,
                sampleRadius,
                out approach,
                out _,
                out _))
            return approach;

        for (int i = 0; i < 8; i++)
        {
            Vector3 direction =
                Quaternion.Euler(0f, i * 45f, 0f) * towardChaser;

            if (TryFindReachableApproach(
                    fromPosition,
                    closest + direction * holdDistance,
                    sampleRadius,
                    out approach,
                    out _,
                    out _))
                return approach;
        }

        return SampleNavMeshNear(ideal, sampleRadius);
    }

    static Vector3 GetBuildingApproachPosition(
        Vector3 fromPosition,
        SelectableEntity target,
        float stoppingDistance,
        float angleOffsetDegrees)
    {
        // 직진이 막혀 있어도 PathComplete인 우회 접근점 중 가장 짧은 경로를 고른다.
        if (TryFindBestBuildingApproach(
                fromPosition,
                target,
                stoppingDistance,
                angleOffsetDegrees,
                preferCompletePath: true,
                out Vector3 bestApproach,
                out NavMeshPathStatus status,
                out _))
        {
            if (status == NavMeshPathStatus.PathComplete)
                return bestApproach;
        }

        if (TryFindBestBuildingApproach(
                fromPosition,
                target,
                stoppingDistance,
                angleOffsetDegrees,
                preferCompletePath: false,
                out bestApproach,
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

        const int directionsPerRound = 16;
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

        for (int ring = 1; ring <= 4; ring++)
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

        Bounds bounds = target.SelectionBounds;
        float targetRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float sampleRadius = attackRange + targetRadius + 2f;

        Vector3 candidate = GetApproachPosition(
            fromPosition,
            target,
            stoppingDistance,
            attackRange,
            angleOffsetDegrees);

        if ((candidate - fromPosition).sqrMagnitude < minMoveSqr)
            return false;

        if ((candidate - avoidPosition).sqrMagnitude < avoidSqr)
            return false;

        if (!TryFindReachableApproach(
                fromPosition,
                candidate,
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
        bool preferCompletePath,
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

        const int directionCount = 16;
        float angleStep = 360f / directionCount;

        for (int ring = 0; ring <= 8; ring++)
        {
            float radius = ringRadius + ring * 2f;
            float ringSampleRadius = Mathf.Max(sampleRadius, radius * 0.35f);

            for (int i = 0; i < directionCount; i++)
            {
                Vector3 direction =
                    Quaternion.Euler(0f, i * angleStep, 0f) * outward;

                if (TryConsiderBuildingApproach(
                        fromPosition,
                        targetPosition + direction * radius,
                        ringSampleRadius,
                        preferCompletePath,
                        ref currentApproach,
                        ref currentStatus,
                        ref currentPathLength))
                {
                    found = true;
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

    static bool TryConsiderBuildingApproach(
        Vector3 fromPosition,
        Vector3 sampleOrigin,
        float sampleRadius,
        bool requireCompletePath,
        ref Vector3 bestApproach,
        ref NavMeshPathStatus bestStatus,
        ref float bestPathLength)
    {
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

        if (!NavMesh.SamplePosition(
                sampleOrigin,
                out NavMeshHit hit,
                sampleRadius,
                NavMesh.AllAreas))
            return false;

        NavMeshPath path = new NavMeshPath();

        if (!NavMesh.CalculatePath(
                fromPosition,
                hit.position,
                NavMesh.AllAreas,
                path))
            return false;

        if (path.status == NavMeshPathStatus.PathInvalid)
            return false;

        if (requireCompletePath && path.status != NavMeshPathStatus.PathComplete)
            return false;

        result = hit.position;
        pathStatus = path.status;
        pathLength = CalculatePathLength(path);
        return true;
    }
}
