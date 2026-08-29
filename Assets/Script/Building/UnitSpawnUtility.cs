using UnityEngine;
using UnityEngine.AI;

public static class UnitSpawnUtility
{
    public const float DefaultMaxVerticalDelta = 2.5f;

    public static GameObject SpawnUnit(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        int ownerId,
        int localPlayerOwnerIdForHealthBar = 1)
    {
        return SpawnUnit(
            prefab,
            position,
            rotation,
            ownerId,
            localPlayerOwnerIdForHealthBar,
            resampleNavMesh: true);
    }

    public static GameObject SpawnUnit(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        int ownerId,
        int localPlayerOwnerIdForHealthBar,
        bool resampleNavMesh)
    {
        if (prefab == null)
            return null;

        Vector3 spawnPosition = position;

        if (resampleNavMesh &&
            !TrySampleSpawnSurface(position, out spawnPosition))
        {
            spawnPosition = position;
        }

        GameObject unitObject =
            Object.Instantiate(prefab, spawnPosition, rotation);

        ConfigureSpawnedUnit(unitObject, ownerId, localPlayerOwnerIdForHealthBar);
        return unitObject;
    }

    public static Vector3 SampleNavMeshPosition(Vector3 position, float maxDistance = 10f)
    {
        if (TrySampleSpawnSurface(position, out Vector3 sampled, maxDistance))
            return sampled;

        return position;
    }

    // 스폰 공통: 힌트 높이의 같은 층을 우선하고, 실패 시 해당 XZ의 최상단 NavMesh를 고릅니다.
    public static bool TrySampleSpawnSurface(
        Vector3 hint,
        out Vector3 result,
        float maxDistance = 10f,
        float maxVerticalDelta = DefaultMaxVerticalDelta)
    {
        if (TrySampleNavMeshNearPreferredHeight(
                hint,
                hint.y,
                maxDistance,
                out result,
                maxVerticalDelta))
            return true;

        return TrySampleTopmostAtXZ(hint.x, hint.z, out result);
    }

    // 스캐터 등으로 힌트가 층 밖으로 나가도 preferredY 층의 NavMesh만 고릅니다.
    // anchor가 있으면 실패 시 앵커(건물) 쪽으로 반경을 줄여 재시도합니다.
    public static bool TryResolveSpawnPosition(
        Vector3 hint,
        Vector3 anchor,
        out Vector3 result,
        float maxDistance = 10f,
        float maxVerticalDelta = DefaultMaxVerticalDelta)
    {
        float preferredY = anchor.y;

        if (TrySampleNavMeshNearPreferredHeight(
                hint,
                preferredY,
                maxDistance,
                out result,
                maxVerticalDelta))
            return true;

        Vector3 offset = hint - anchor;
        offset.y = 0f;

        for (int i = 0; i < 4; i++)
        {
            offset *= 0.5f;
            Vector3 candidate = anchor + offset;
            candidate.y = preferredY;

            if (TrySampleNavMeshNearPreferredHeight(
                    candidate,
                    preferredY,
                    maxDistance,
                    out result,
                    maxVerticalDelta))
                return true;
        }

        if (TrySampleNavMeshNearPreferredHeight(
                anchor,
                preferredY,
                maxDistance,
                out result,
                maxVerticalDelta))
            return true;

        return TrySampleTopmostAtXZ(anchor.x, anchor.z, out result);
    }

    // 해당 XZ에서 가장 높은(최상단) walkable 표면을 고릅니다. 다층 맵 랜덤 스폰용.
    public static bool TrySampleTopmostAtXZ(float worldX, float worldZ, out Vector3 result)
    {
        result = new Vector3(worldX, 0f, worldZ);

        float maxY = 64f;
        float minY = -8f;
        float horizontalRadius = 2f;

        MapGrid grid = MapGrid.Instance;

        if (grid != null && grid.IsNavMeshBoundsActive)
        {
            maxY = grid.NavMeshMaxY + Mathf.Max(2f, grid.navMeshSampleHeightOffset);
            minY = grid.NavMeshMinY - 1f;
            horizontalRadius = Mathf.Max(0.75f, grid.cellSize);
        }
        else if (MapPlayBounds.TryResolve(
                     MapPlayBoundsSource.Auto,
                     Vector3.zero,
                     new Vector2(256f, 256f),
                     out MapPlayBoundsData bounds))
        {
            maxY = bounds.Origin.y + 64f;
            minY = bounds.Origin.y - 8f;
        }

        bool found = false;
        float bestY = float.MinValue;
        Vector3 best = result;

        void Consider(NavMeshHit candidate)
        {
            float xz = Vector2.Distance(
                new Vector2(candidate.position.x, candidate.position.z),
                new Vector2(worldX, worldZ));

            if (xz > horizontalRadius * 2.5f)
                return;

            if (!found || candidate.position.y > bestY + 0.01f)
            {
                found = true;
                bestY = candidate.position.y;
                best = candidate.position;
            }
        }

        // 위에서 아래로 슬라이스하며 같은 XZ의 가장 높은 표면을 고른다.
        const int sliceCount = 10;
        float verticalSpan = Mathf.Max(4f, maxY - minY);

        for (int i = 0; i <= sliceCount; i++)
        {
            float t = i / (float)sliceCount;
            Vector3 probe = new Vector3(worldX, Mathf.Lerp(maxY, minY, t), worldZ);

            if (NavMesh.SamplePosition(
                    probe,
                    out NavMeshHit sample,
                    horizontalRadius * 1.5f,
                    NavMesh.AllAreas))
            {
                Consider(sample);
            }
        }

        Vector3 fromAbove = new Vector3(worldX, maxY, worldZ);

        if (NavMesh.SamplePosition(
                fromAbove,
                out NavMeshHit aboveHit,
                verticalSpan + horizontalRadius,
                NavMesh.AllAreas))
        {
            Consider(aboveHit);
        }

        if (!found)
            return false;

        result = best;
        return true;
    }

    public static bool TrySampleNavMeshNearPreferredHeight(
        Vector3 position,
        float preferredY,
        float maxDistance,
        out Vector3 result,
        float maxVerticalDelta = DefaultMaxVerticalDelta)
    {
        result = position;
        float maxYDelta = Mathf.Max(0.5f, maxVerticalDelta);

        if (MapGrid.Instance != null &&
            MapGrid.Instance.TrySampleNavMeshNearHeight(
                position,
                preferredY,
                out NavMeshHit gridHit,
                maxYDelta))
        {
            result = gridHit.position;
            return true;
        }

        Vector3 atPreferred = position;
        atPreferred.y = preferredY;

        float nearRadius = Mathf.Min(maxDistance, 2f);

        if (NavMesh.SamplePosition(atPreferred, out NavMeshHit hit, nearRadius, NavMesh.AllAreas) &&
            Mathf.Abs(hit.position.y - preferredY) <= maxYDelta)
        {
            result = hit.position;
            return true;
        }

        if (!NavMesh.SamplePosition(atPreferred, out hit, maxDistance, NavMesh.AllAreas))
            return false;

        if (Mathf.Abs(hit.position.y - preferredY) > maxYDelta)
            return false;

        result = hit.position;
        return true;
    }

    public static void SnapAgentToPosition(NavMeshAgent agent, Vector3 position)
    {
        if (agent == null || !agent.isActiveAndEnabled)
            return;

        agent.Warp(position);
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
        SnapAgentToPosition(agent, unitObject.transform.position);

        WorldHealthBar healthBar = unitObject.GetComponent<WorldHealthBar>();

        if (healthBar != null)
            healthBar.localPlayerOwnerId = localPlayerOwnerIdForHealthBar;

        if (unitObject.GetComponent<FogOfWarVisibility>() == null)
            unitObject.AddComponent<FogOfWarVisibility>();
    }
}
