using UnityEngine;
using UnityEngine.AI;

public static class TargetFinder
{
    public static SelectableEntity FindBestEnemyInRange(
        Vector3 fromPosition,
        int myOwnerId,
        float range,
        CombatTargetPriority priority)
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

            float sqrDistance =
                (entity.transform.position - fromPosition).sqrMagnitude;

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
        int myOwnerId)
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

            float sqrDistance =
                (building.transform.position - fromPosition).sqrMagnitude;

            if (sqrDistance >= minSqrDistance)
                continue;

            minSqrDistance = sqrDistance;
            nearest = building;
        }

        return nearest;
    }

    public static Vector3 GetApproachPosition(
        Vector3 fromPosition,
        SelectableEntity target,
        float stoppingDistance,
        float angleOffsetDegrees = 0f)
    {
        if (target == null)
            return fromPosition;

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

        // 건물 중심은 Carve 구역이라 반환하지 않는다.
        return fromPosition;
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
