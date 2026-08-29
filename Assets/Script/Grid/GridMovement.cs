using UnityEngine;
using UnityEngine.AI;

public static class GridMovement
{
    public static Vector3 SnapMoveDestination(
        Vector3 worldPosition,
        Vector2Int footprintCells)
    {
        if (MapGrid.Instance == null)
            return worldPosition;

        if (footprintCells.x <= 0 || footprintCells.y <= 0)
            footprintCells = Vector2Int.one;

        Vector2Int origin =
            MapGrid.Instance.GetFootprintOriginFromCenterWorld(
                worldPosition,
                footprintCells);

        Vector3 snapped =
            MapGrid.Instance.GetFootprintCenterWorld(origin, footprintCells);

        snapped.y = MapGrid.Instance.SampleGroundHeight(snapped, worldPosition.y);

        if (UnitSpawnUtility.TrySampleNavMeshNearPreferredHeight(
                snapped,
                worldPosition.y,
                MapGrid.Instance.cellSize * 2f,
                out Vector3 preferredHit))
        {
            return preferredHit;
        }

        if (TrySampleNavMesh(snapped, out NavMeshHit hit))
            return hit.position;

        if (TryFindNearestWalkableCell(
                origin,
                footprintCells,
                worldPosition.y,
                out Vector3 fallback))
            return fallback;

        return snapped;
    }

    // preferredY에 가까운 NavMesh 표면으로 목적지를 맞춥니다 (다층 맵용).
    public static Vector3 SnapMoveDestinationNearHeight(
        Vector3 worldPosition,
        float preferredY,
        Vector2Int footprintCells)
    {
        Vector3 hint = worldPosition;
        hint.y = preferredY;
        return SnapMoveDestination(hint, footprintCells);
    }

    public static Vector2Int GetFootprintCells(Component source)
    {
        if (source == null)
            return Vector2Int.one;

        GridFootprint footprint = source.GetComponent<GridFootprint>();

        if (footprint != null)
            return footprint.footprintCells;

        return Vector2Int.one;
    }

    static bool TrySampleNavMesh(Vector3 position, out NavMeshHit hit)
    {
        float sampleRadius = MapGrid.Instance != null
            ? MapGrid.Instance.cellSize * 2f
            : 4f;

        return NavMesh.SamplePosition(
            position,
            out hit,
            sampleRadius,
            NavMesh.AllAreas);
    }

    static bool TryFindNearestWalkableCell(
        Vector2Int originCell,
        Vector2Int footprintCells,
        float preferredY,
        out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (MapGrid.Instance == null)
            return false;

        int maxRadius = 6;

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(z) != radius)
                        continue;

                    Vector2Int candidateOrigin = new Vector2Int(
                        originCell.x + x,
                        originCell.y + z);

                    if (!MapGrid.Instance.IsFootprintInBounds(
                            candidateOrigin,
                            footprintCells))
                        continue;

                    Vector3 candidate =
                        MapGrid.Instance.GetFootprintCenterWorld(
                            candidateOrigin,
                            footprintCells);

                    candidate.y = MapGrid.Instance.SampleGroundHeight(
                        candidate,
                        preferredY);

                    if (!UnitSpawnUtility.TrySampleNavMeshNearPreferredHeight(
                            candidate,
                            preferredY,
                            MapGrid.Instance.cellSize * 2f,
                            out Vector3 sampled))
                        continue;

                    worldPosition = sampled;
                    return true;
                }
            }
        }

        return false;
    }

    public static bool EnsureAgentOnNavMesh(NavMeshAgent agent, float maxDistance = 10f)
    {
        if (agent == null || !agent.isActiveAndEnabled)
            return false;

        if (agent.isOnNavMesh)
            return true;

        Vector3 position = agent.transform.position;

        if (UnitSpawnUtility.TrySampleNavMeshNearPreferredHeight(
                position,
                position.y,
                maxDistance,
                out Vector3 sampled))
        {
            agent.Warp(sampled);
            return agent.isOnNavMesh;
        }

        return false;
    }

    /// <summary>
    /// immediate는 플레이어 명령처럼 반응이 즉시 보여야 하는 경로입니다.
    /// AI 추격은 프레임 예산을 넘기면 큐에 쌓아 다음 프레임으로 분산합니다.
    /// </summary>
    public static bool TrySetAgentDestination(
        NavMeshAgent agent,
        Vector3 destination,
        bool immediate = false)
    {
        if (!EnsureAgentOnNavMesh(agent))
            return false;

        if (immediate || AiPathBudget.TryAcquireHeavy())
            return TrySetAgentDestinationImmediate(agent, destination);

        AiPathBudget.EnqueueDestination(agent, destination);
        return true;
    }

    public static bool TrySetAgentDestinationImmediate(
        NavMeshAgent agent,
        Vector3 destination)
    {
        if (!EnsureAgentOnNavMesh(agent))
            return false;

        if (UnitSpawnUtility.TrySampleNavMeshNearPreferredHeight(
                destination,
                destination.y,
                MapGrid.Instance != null ? MapGrid.Instance.cellSize * 2f : 4f,
                out Vector3 sampledDestination))
        {
            destination = sampledDestination;
        }
        else if (TrySampleNavMesh(destination, out NavMeshHit hit))
        {
            destination = hit.position;
        }

        if (agent.isStopped)
            agent.isStopped = false;

        return agent.SetDestination(destination);
    }
}
