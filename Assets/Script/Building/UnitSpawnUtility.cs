using UnityEngine;
using UnityEngine.AI;

public static class UnitSpawnUtility
{
    public static GameObject SpawnUnit(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        int ownerId,
        int localPlayerOwnerIdForHealthBar = 1)
    {
        if (prefab == null)
            return null;

        Vector3 spawnPosition = SampleNavMeshPosition(position);

        GameObject unitObject =
            Object.Instantiate(prefab, spawnPosition, rotation);

        ConfigureSpawnedUnit(unitObject, ownerId, localPlayerOwnerIdForHealthBar);
        return unitObject;
    }

    public static Vector3 SampleNavMeshPosition(Vector3 position, float maxDistance = 10f)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            return hit.position;

        return position;
    }

    static void ConfigureSpawnedUnit(
        GameObject unitObject,
        int ownerId,
        int localPlayerOwnerIdForHealthBar)
    {
        SelectableEntity selectable = unitObject.GetComponent<SelectableEntity>();

        if (selectable != null)
            selectable.ownerId = ownerId;

        Unit unit = unitObject.GetComponent<Unit>();

        if (unit != null && unit.data != null)
            unit.ApplyData(unit.data);

        NavMeshAgent agent = unitObject.GetComponent<NavMeshAgent>();

        if (agent != null && agent.enabled)
            agent.Warp(unitObject.transform.position);

        WorldHealthBar healthBar = unitObject.GetComponent<WorldHealthBar>();

        if (healthBar != null)
            healthBar.localPlayerOwnerId = localPlayerOwnerIdForHealthBar;

        if (unitObject.GetComponent<FogOfWarVisibility>() == null)
            unitObject.AddComponent<FogOfWarVisibility>();
    }
}
