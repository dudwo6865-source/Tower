using System;
using UnityEngine;

public static class EnemySpawnUtility
{
    public static bool TrySampleNavMeshPosition(
        Vector3 position,
        out Vector3 result,
        float maxDistance = 8f)
    {
        return UnitSpawnUtility.TrySampleSpawnSurface(
            position,
            out result,
            maxDistance);
    }

    public static Vector3 SampleNavMeshPosition(Vector3 position, float maxDistance = 8f)
    {
        if (TrySampleNavMeshPosition(position, out Vector3 result, maxDistance))
            return result;

        return position;
    }

    // 해당 월드 위치가 현재 로컬 플레이어에게 보일 수 있는지 판정한다.
    // FogOfWarManager가 없으면 시야 판정을 할 수 없으므로 false(보이지 않음)로 취급한다.
    public static bool IsVisibleToLocalPlayer(Vector3 worldPosition)
    {
        FogOfWarManager fog = FogOfWarManager.Instance;
        if (fog == null)
            return false;

        // 유닛이 실제로 화면에 나타날 임계값으로 판정한다.
        return fog.IsVisibleForSpawnAvoidance(worldPosition);
    }

    // 중심(center)에서 반경(radius) 안의 NavMesh 위 무작위 지점을 찾는다.
    // 중심과 같은 층을 유지하며, avoidPlayerVision이 켜져 있으면 시야 밖을 우선한다.
    public static Vector3 GetRandomPositionInRadius(
        Vector3 center,
        float radius,
        bool avoidPlayerVision,
        int attempts = 16)
    {
        Vector3 fallback = center;
        bool hasFallback = false;
        int attemptCount = Mathf.Max(1, attempts);
        float sampleRadius = Mathf.Max(2f, radius * 0.5f);

        if (UnitSpawnUtility.TrySampleNavMeshNearPreferredHeight(
                center,
                center.y,
                Mathf.Max(sampleRadius, 8f),
                out Vector3 centerSampled))
        {
            fallback = centerSampled;
            hasFallback = true;
        }

        for (int i = 0; i < attemptCount; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 candidate =
                center + new Vector3(offset.x, 0f, offset.y);
            candidate.y = center.y;

            if (!UnitSpawnUtility.TryResolveSpawnPosition(
                    candidate,
                    center,
                    out Vector3 sampled,
                    sampleRadius))
            {
                continue;
            }

            if (!hasFallback)
            {
                fallback = sampled;
                hasFallback = true;
            }

            if (avoidPlayerVision && IsVisibleToLocalPlayer(sampled))
                continue;

            return sampled;
        }

        if (hasFallback)
            return fallback;

        if (UnitSpawnUtility.TrySampleTopmostAtXZ(center.x, center.z, out Vector3 topmost))
            return topmost;

        return center;
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

        if (!UnitSpawnUtility.TrySampleSpawnSurface(position, out Vector3 spawnPosition))
        {
            Debug.LogWarning(
                $"EnemySpawnUtility: 스폰 위치가 NavMesh에서 너무 멉니다. 생성을 건너뜁니다. pos={position}");
            return null;
        }

        GameObject enemyObject =
            UnityEngine.Object.Instantiate(prefab, spawnPosition, rotation);

        UnityEngine.AI.NavMeshAgent spawnedAgent = enemyObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
        UnitSpawnUtility.SnapAgentToPosition(spawnedAgent, spawnPosition);

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

        UnityEngine.AI.NavMeshAgent agent = enemyObject.GetComponent<UnityEngine.AI.NavMeshAgent>();

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

        EnemyCombatAI enemyAI = enemyObject.GetComponent<EnemyCombatAI>();

        if (enemyAI != null)
            enemyAI.advanceToEnemyBuildings = advanceToBase;
    }

    public static Vector2Int ResolveBuildingFootprint(GameObject source)
    {
        if (source == null)
            return GridFootprint.DefaultBuildingFootprint;

        GridFootprint footprint = source.GetComponent<GridFootprint>();

        if (footprint == null)
            footprint = source.GetComponentInChildren<GridFootprint>(true);

        if (footprint != null &&
            footprint.footprintCells.x > 0 &&
            footprint.footprintCells.y > 0)
        {
            return footprint.footprintCells;
        }

        return GridFootprint.DefaultBuildingFootprint;
    }

    public static SelectableEntity ResolveSelectable(GameObject root)
    {
        if (root == null)
            return null;

        SelectableEntity selectable = root.GetComponent<SelectableEntity>();

        if (selectable != null)
            return selectable;

        return root.GetComponentInChildren<SelectableEntity>(true);
    }

    public static bool IsFootprintVisibleToLocalPlayer(
        Vector3 center,
        Vector2Int footprintCells)
    {
        if (IsVisibleToLocalPlayer(center))
            return true;

        MapGrid grid = MapGrid.Instance;

        if (grid == null)
            return false;

        float halfX = Mathf.Max(1, footprintCells.x) * grid.cellSize * 0.5f;
        float halfZ = Mathf.Max(1, footprintCells.y) * grid.cellSize * 0.5f;

        Vector3[] corners =
        {
            center + new Vector3(-halfX, 0f, -halfZ),
            center + new Vector3(halfX, 0f, -halfZ),
            center + new Vector3(-halfX, 0f, halfZ),
            center + new Vector3(halfX, 0f, halfZ)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            if (IsVisibleToLocalPlayer(corners[i]))
                return true;
        }

        return false;
    }

    public static GameObject SpawnEnemyBuilding(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Vector2Int originCell,
        int enemyOwnerId,
        int localPlayerOwnerId,
        float healthMultiplier = 1f,
        Action<EntityHealth> onHealthConfigured = null)
    {
        if (prefab == null)
            return null;

        GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
        SelectableEntity selectable = ResolveSelectable(instance);
        GameObject buildingObject = selectable != null ? selectable.gameObject : instance;

        if (selectable != null)
        {
            selectable.ownerId = enemyOwnerId;
            selectable.entityType = SelectableEntityType.Building;
        }

        WorldHealthBar healthBar = buildingObject.GetComponent<WorldHealthBar>();

        if (healthBar != null)
            healthBar.localPlayerOwnerId = localPlayerOwnerId;

        EnemySpawner spawner = buildingObject.GetComponent<EnemySpawner>();

        if (spawner != null)
            spawner.enemyOwnerId = enemyOwnerId;

        GridFootprint footprint = GridFootprint.EnsureOnInstance(buildingObject);
        footprint.blockCells = true;
        footprint.carveNavMesh = false;
        footprint.snapTransformOnRegister = true;

        if (!footprint.RegisterAtOriginCell(originCell))
        {
            Debug.LogWarning(
                $"EnemySpawnUtility: 적 건물 footprint 등록에 실패했습니다. origin={originCell}",
                instance);
            UnityEngine.Object.Destroy(instance);
            return null;
        }

        // 스크립트가 자식에만 있는 프리팹이면 빈 루트를 제거하고 건물 오브젝트를 반환한다.
        if (buildingObject != instance)
        {
            buildingObject.transform.SetParent(instance.transform.parent, true);
            UnityEngine.Object.Destroy(instance);
            instance = buildingObject;
        }

        if (instance.GetComponent<FogOfWarVisibility>() == null)
            instance.AddComponent<FogOfWarVisibility>();

        EntityHealth health = instance.GetComponent<EntityHealth>();

        if (health != null)
        {
            if (!Mathf.Approximately(healthMultiplier, 1f))
                health.SetMaxHealth(health.MaxHealth * healthMultiplier);

            onHealthConfigured?.Invoke(health);
        }

        return instance;
    }
}
