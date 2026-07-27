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

    // 해당 월드 위치가 현재 로컬 플레이어의 시야(안개 밖)에 보이는지 판정한다.
    // FogOfWarManager가 없으면 시야 판정을 할 수 없으므로 false(보이지 않음)로 취급한다.
    public static bool IsVisibleToLocalPlayer(Vector3 worldPosition)
    {
        FogOfWarManager fog = FogOfWarManager.Instance;
        return fog != null && fog.IsVisible(worldPosition);
    }

    // 중심(center)에서 반경(radius) 안의 NavMesh 위 무작위 지점을 찾는다.
    // avoidPlayerVision이 켜져 있으면 플레이어 시야 밖 지점을 우선 고른다.
    // 시야 밖 지점을 못 찾으면 마지막으로 찾은 NavMesh 위 지점(시야 안일 수 있음)을 반환한다.
    public static Vector3 GetRandomPositionInRadius(
        Vector3 center,
        float radius,
        bool avoidPlayerVision,
        int attempts = 24)
    {
        Vector3 fallback = SampleNavMeshPosition(center);
        int attemptCount = Mathf.Max(1, attempts);

        for (int i = 0; i < attemptCount; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 candidate =
                center + new Vector3(offset.x, 0f, offset.y);

            if (!TrySampleNavMeshPosition(candidate, out Vector3 sampled))
                continue;

            // 최소한 NavMesh 위 지점은 확보해 둔다. (시야 밖을 못 찾을 때의 대비)
            fallback = sampled;

            if (avoidPlayerVision && IsVisibleToLocalPlayer(sampled))
                continue;

            return sampled;
        }

        return fallback;
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
