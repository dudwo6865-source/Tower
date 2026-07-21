using System;
using UnityEngine;
using UnityEngine.AI;

public static class EnemySpawnUtility
{
    public static bool TrySampleNavMeshPosition(
        Vector3 position,
        out Vector3 result,
        float maxDistance = 25f)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = position;
        return false;
    }

    public static Vector3 SampleNavMeshPosition(Vector3 position, float maxDistance = 25f)
    {
        TrySampleNavMeshPosition(position, out Vector3 result, maxDistance);
        return result;
    }

    public static GameObject SpawnEnemy(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        int enemyOwnerId,
        float healthMultiplier = 1f,
        float damageMultiplier = 1f,
        float speedMultiplier = 1f,
        Action<EntityHealth> onHealthConfigured = null)
    {
        if (prefab == null)
            return null;

        // NavMesh 위 지점을 못 찾으면 잘못된 위치에 에이전트를 만들지 않는다.
        // (엉뚱한 위치에 생성하면 "not close enough to the NavMesh" 오류 발생)
        if (!TrySampleNavMeshPosition(position, out Vector3 spawnPosition))
        {
            Debug.LogWarning(
                $"EnemySpawnUtility: 스폰 위치가 NavMesh에서 너무 멉니다. 생성을 건너뜁니다. pos={position}");
            return null;
        }

        GameObject enemyObject =
            UnityEngine.Object.Instantiate(prefab, spawnPosition, rotation);

        NavMeshAgent spawnedAgent = enemyObject.GetComponent<NavMeshAgent>();

        if (spawnedAgent != null && !spawnedAgent.isOnNavMesh)
            spawnedAgent.Warp(spawnPosition);

        ConfigureEnemy(
            enemyObject,
            enemyOwnerId,
            healthMultiplier,
            damageMultiplier,
            speedMultiplier,
            onHealthConfigured);

        return enemyObject;
    }

    public static void ConfigureEnemy(
        GameObject enemyObject,
        int enemyOwnerId,
        float healthMultiplier,
        float damageMultiplier,
        float speedMultiplier,
        Action<EntityHealth> onHealthConfigured = null)
    {
        if (enemyObject == null)
            return;

        SelectableEntity selectable = enemyObject.GetComponent<SelectableEntity>();

        if (selectable != null)
            selectable.ownerId = enemyOwnerId;

        EntityHealth health = enemyObject.GetComponent<EntityHealth>();

        if (health != null)
        {
            health.SetMaxHealth(health.MaxHealth * healthMultiplier);
            onHealthConfigured?.Invoke(health);
        }

        UnitAttacker attacker = enemyObject.GetComponent<UnitAttacker>();

        if (attacker != null)
            attacker.attackDamage *= damageMultiplier;

        NavMeshAgent agent = enemyObject.GetComponent<NavMeshAgent>();

        if (agent != null)
            agent.speed *= speedMultiplier;

        if (enemyObject.GetComponent<FogOfWarVisibility>() == null)
            enemyObject.AddComponent<FogOfWarVisibility>();
    }

    public static void ApplyAdvanceToEnemyBuildings(
        GameObject enemyObject,
        bool advanceToBase)
    {
        if (enemyObject == null)
            return;

        UnitCombatAI combatAI = enemyObject.GetComponent<UnitCombatAI>();

        if (combatAI != null)
            combatAI.advanceToEnemyBuildings = advanceToBase;
    }
}
