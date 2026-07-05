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

        if (TryFindReachableApproach(fromPosition, ideal, sampleRadius, out Vector3 approach))
            return approach;

        for (int i = 0; i < 8; i++)
        {
            Vector3 direction =
                Quaternion.Euler(0f, i * 45f, 0f) * towardChaser;

            if (TryFindReachableApproach(
                    fromPosition,
                    closest + direction * holdDistance,
                    sampleRadius,
                    out approach))
                return approach;
        }

        if (TryFindReachableApproach(
                fromPosition,
                target.transform.position,
                sampleRadius,
                out approach))
            return approach;

        return SampleNavMeshNear(ideal, sampleRadius);
    }

    static Vector3 GetBuildingApproachPosition(
        Vector3 fromPosition,
        SelectableEntity target,
        float stoppingDistance,
        float angleOffsetDegrees)
    {
        Vector3 targetPosition = target.transform.position;

        Bounds bounds = target.SelectionBounds;
        float targetRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);

        // Carve 영역 + stoppingDistance + NavMesh 복셀 여유
        float ringRadius = targetRadius + stoppingDistance + 1f;

        Vector3 outward = fromPosition - targetPosition;
        outward.y = 0f;

        if (outward.sqrMagnitude < 0.01f)
            outward = Vector3.forward;

        outward.Normalize();

        if (Mathf.Abs(angleOffsetDegrees) > 0.01f)
            outward = Quaternion.Euler(0f, angleOffsetDegrees, 0f) * outward;

        float sampleRadius = stoppingDistance + targetRadius + 2f;

        // 1차: 자신이 오는 방향의 건물 둘레
        if (TryFindReachableApproach(
                fromPosition,
                targetPosition + outward * ringRadius,
                sampleRadius,
                out Vector3 approach))
            return approach;

        // 2차: 둘레를 여러 각도로 시도 (Carve 때문에 한 방향이 막혀 있을 수 있음)
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction =
                Quaternion.Euler(0f, i * 45f, 0f) * outward;

            if (TryFindReachableApproach(
                    fromPosition,
                    targetPosition + direction * ringRadius,
                    sampleRadius,
                    out approach))
                return approach;
        }

        // 3차: 반경을 넓혀가며 탐색
        for (float radius = ringRadius; radius <= ringRadius + 8f; radius += 2f)
        {
            if (TryFindReachableApproach(
                    fromPosition,
                    targetPosition + outward * radius,
                    radius,
                    out approach))
                return approach;
        }

        return SampleNavMeshNear(targetPosition + outward * ringRadius, sampleRadius);
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
                        out Vector3 reachable))
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
                out Vector3 reachable))
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

    static bool TryFindReachableApproach(
        Vector3 fromPosition,
        Vector3 sampleOrigin,
        float sampleRadius,
        out Vector3 result)
    {
        result = sampleOrigin;

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

        result = hit.position;
        return true;
    }
}
