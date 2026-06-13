using UnityEngine;
using UnityEngine.AI;

public static class TargetFinder
{
    public static SelectableEntity FindNearestBuilding(
        Vector3 fromPosition,
        int ownerId)
    {
        SelectableEntity nearest = null;
        float minSqrDistance = float.MaxValue;

        foreach (SelectableEntity building in BuildingRegistry.Buildings)
        {
            if (building == null || building.ownerId != ownerId)
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
        SelectableEntity building,
        float stoppingDistance)
    {
        if (building == null)
            return fromPosition;

        Vector3 buildingPosition = building.transform.position;
        Vector3 direction = buildingPosition - fromPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector3.forward;

        direction.Normalize();

        Vector3 approachPosition =
            buildingPosition - direction * stoppingDistance;

        if (NavMesh.SamplePosition(
                approachPosition,
                out NavMeshHit hit,
                stoppingDistance * 2f,
                NavMesh.AllAreas))
            return hit.position;

        if (NavMesh.SamplePosition(
                buildingPosition,
                out hit,
                stoppingDistance * 4f,
                NavMesh.AllAreas))
            return hit.position;

        return buildingPosition;
    }
}
