using UnityEngine;

public enum MapPlayBoundsSource
{
    Auto,
    MapGrid,
    Manual
}

public struct MapPlayBoundsData
{
    public bool IsValid;
    public Vector3 Origin;
    public float Width;
    public float Length;

    public Vector3 Center =>
        new Vector3(
            Origin.x + Width * 0.5f,
            Origin.y,
            Origin.z + Length * 0.5f);
}

public static class MapPlayBounds
{
    public static bool TryResolve(
        MapPlayBoundsSource source,
        Vector3 manualOrigin,
        Vector2 manualSize,
        out MapPlayBoundsData data)
    {
        switch (source)
        {
            case MapPlayBoundsSource.MapGrid:
                return TryFromMapGrid(out data);

            case MapPlayBoundsSource.Manual:
                return TryFromManual(manualOrigin, manualSize, out data);

            default:
                if (TryFromMapGrid(out data))
                    return true;

                return TryFromManual(manualOrigin, manualSize, out data);
        }
    }

    public static float SampleGroundHeight(Vector3 worldPosition)
    {
        if (UnitSpawnUtility.TrySampleSpawnSurface(worldPosition, out Vector3 sampled))
            return sampled.y;

        if (MapGrid.Instance != null)
            return MapGrid.Instance.SampleGroundHeight(worldPosition);

        if (FogGroundUtility.TrySampleSurfaceHeight(
                worldPosition.x,
                worldPosition.z,
                out float surfaceY))
        {
            return surfaceY;
        }

        return worldPosition.y;
    }

    public static float SampleGroundHeight(Vector3 worldPosition, float preferredY)
    {
        if (UnitSpawnUtility.TrySampleNavMeshNearPreferredHeight(
                worldPosition,
                preferredY,
                10f,
                out Vector3 sampled))
            return sampled.y;

        if (UnitSpawnUtility.TrySampleTopmostAtXZ(
                worldPosition.x,
                worldPosition.z,
                out sampled))
            return sampled.y;

        return preferredY;
    }

    public static Vector3 GetRaycastOrigin(float worldX, float worldZ, float padding)
    {
        float startY = padding;

        MapGrid grid = MapGrid.Instance;

        if (grid != null && grid.IsNavMeshBoundsActive)
            startY = grid.MapOrigin.y + padding;
        else if (TryResolve(
                     MapPlayBoundsSource.Auto,
                     Vector3.zero,
                     new Vector2(256f, 256f),
                     out MapPlayBoundsData bounds))
            startY = bounds.Origin.y + padding;

        return new Vector3(worldX, startY, worldZ);
    }

    public static float GetRaycastDistance(float padding)
    {
        return padding * 2f + 256f;
    }

    static bool TryFromMapGrid(out MapPlayBoundsData data)
    {
        data = default;

        MapGrid mapGrid = MapGrid.Instance;

        if (mapGrid == null)
            mapGrid = Object.FindObjectOfType<MapGrid>();

        if (mapGrid != null &&
            mapGrid.UsesNavMesh &&
            mapGrid.CellCountX <= 0)
        {
            mapGrid.Refresh();
        }

        if (mapGrid == null || mapGrid.CellCountX <= 0 || mapGrid.CellCountZ <= 0)
            return false;

        data.IsValid = true;
        data.Origin = mapGrid.MapOrigin;
        data.Width = mapGrid.MapSize.x;
        data.Length = mapGrid.MapSize.y;
        return true;
    }

    static bool TryFromManual(
        Vector3 manualOrigin,
        Vector2 manualSize,
        out MapPlayBoundsData data)
    {
        data = default;

        if (manualSize.x <= 0f || manualSize.y <= 0f)
            return false;

        data.IsValid = true;
        data.Origin = manualOrigin;
        data.Width = manualSize.x;
        data.Length = manualSize.y;
        return true;
    }
}
