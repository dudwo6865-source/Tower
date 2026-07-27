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

        snapped.y = MapGrid.Instance.SampleGroundHeight(snapped);

        if (TrySampleNavMesh(snapped, out NavMeshHit hit))
            return hit.position;

        if (TryFindNearestWalkableCell(origin, footprintCells, out Vector3 fallback))
            return fallback;

        return snapped;
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

                    candidate.y = MapGrid.Instance.SampleGroundHeight(candidate);

                    if (!TrySampleNavMesh(candidate, out NavMeshHit hit))
                        continue;

                    worldPosition = hit.position;
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

        if (!NavMesh.SamplePosition(
                agent.transform.position,
                out NavMeshHit hit,
                maxDistance,
                NavMesh.AllAreas))
        {
            return false;
        }

        agent.Warp(hit.position);
        return agent.isOnNavMesh;
    }

    public static bool TrySetAgentDestination(NavMeshAgent agent, Vector3 destination)
    {
        if (!EnsureAgentOnNavMesh(agent))
            return false;

        if (TrySampleNavMesh(destination, out NavMeshHit hit))
            destination = hit.position;

        // 정지 상태(isStopped)로 남아 있으면 SetDestination을 해도 움직이지 않는다.
        // 새 목적지를 받으면 즉시 이동을 재개하도록 정지를 해제한다.
        if (agent.isStopped)
            agent.isStopped = false;

        return agent.SetDestination(destination);
    }
}
