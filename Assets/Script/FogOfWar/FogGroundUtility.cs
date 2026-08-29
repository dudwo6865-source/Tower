using UnityEngine;
using UnityEngine.AI;

public enum FogSurfaceSampleMode
{
    Hybrid,
    NavMeshOnly,
    VisualGeometry
}

public static class FogGroundUtility
{
    public static FogSurfaceSampleMode overlaySampleMode =
        FogSurfaceSampleMode.VisualGeometry;

    public static LayerMask groundRaycastMask = ~0;

    [Tooltip("표면 레이캐스트 시작 높이(맵 최고점 기준 추가값)입니다.")]
    public static float raycastHeightPadding = 32f;

    public static Vector3 SnapToGround(Vector3 worldPosition)
    {
        if (TrySampleSurfaceHeight(
                worldPosition.x,
                worldPosition.z,
                FogSurfaceSampleMode.Hybrid,
                out float surfaceY))
        {
            worldPosition.y = surfaceY;
        }

        return worldPosition;
    }

    public static bool TrySampleSurfaceHeight(
        float worldX,
        float worldZ,
        out float surfaceY)
    {
        return TrySampleSurfaceHeight(
            worldX,
            worldZ,
            overlaySampleMode,
            out surfaceY);
    }

    public static bool TrySampleSurfaceHeight(
        float worldX,
        float worldZ,
        FogSurfaceSampleMode mode,
        out float surfaceY)
    {
        switch (mode)
        {
            case FogSurfaceSampleMode.NavMeshOnly:
                return TrySampleNavMeshHeight(worldX, worldZ, out surfaceY);

            case FogSurfaceSampleMode.VisualGeometry:
                if (TrySampleRaycastHeight(worldX, worldZ, out surfaceY))
                    return true;

                return TrySampleNavMeshHeight(worldX, worldZ, out surfaceY);

            case FogSurfaceSampleMode.Hybrid:
            default:
                if (TrySampleNavMeshHeight(worldX, worldZ, out surfaceY))
                    return true;

                return TrySampleRaycastHeight(worldX, worldZ, out surfaceY);
        }
    }

    static bool TrySampleNavMeshHeight(float worldX, float worldZ, out float surfaceY)
    {
        if (UnitSpawnUtility.TrySampleTopmostAtXZ(worldX, worldZ, out Vector3 topmost))
        {
            surfaceY = topmost.y;
            return true;
        }

        Vector3 worldPoint = new Vector3(worldX, 0f, worldZ);
        MapGrid grid = MapGrid.Instance;

        if (grid != null &&
            grid.TrySampleNavMeshAtXZ(worldPoint, out NavMeshHit navHit))
        {
            surfaceY = navHit.position.y;
            return true;
        }

        if (NavMesh.SamplePosition(
                GetRaycastOrigin(worldX, worldZ),
                out navHit,
                GetRaycastDistance(),
                NavMesh.AllAreas))
        {
            surfaceY = navHit.position.y;
            return true;
        }

        surfaceY = 0f;
        return false;
    }

    static bool TrySampleRaycastHeight(float worldX, float worldZ, out float surfaceY)
    {
        Vector3 origin = GetRaycastOrigin(worldX, worldZ);
        float distance = GetRaycastDistance();

        if (Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                distance,
                groundRaycastMask,
                QueryTriggerInteraction.Ignore))
        {
            surfaceY = hit.point.y;
            return true;
        }

        surfaceY = 0f;
        return false;
    }

    static Vector3 GetRaycastOrigin(float worldX, float worldZ)
    {
        float startY = raycastHeightPadding;

        MapGrid grid = MapGrid.Instance;

        if (grid != null && grid.IsNavMeshBoundsActive)
            startY = grid.MapOrigin.y + raycastHeightPadding;
        else if (MapPlayBounds.TryResolve(
                     MapPlayBoundsSource.Auto,
                     Vector3.zero,
                     new Vector2(256f, 256f),
                     out MapPlayBoundsData bounds))
            startY = bounds.Origin.y + raycastHeightPadding;

        return new Vector3(worldX, startY, worldZ);
    }

    static float GetRaycastDistance()
    {
        return raycastHeightPadding * 2f + 256f;
    }
}
