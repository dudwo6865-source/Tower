using UnityEngine;

public static class CameraVisibility
{
    const float DefaultRadius = 5f;

    static readonly Plane[] frustumPlanes = new Plane[6];
    static int cachedFrame = -1;
    static Camera cachedCamera;
    static bool hasPlanes;

    public static bool IsVisible(Vector3 worldPosition, float radius = DefaultRadius)
    {
        float size = Mathf.Max(0.5f, radius) * 2f;
        return IsVisible(new Bounds(worldPosition, new Vector3(size, size, size)));
    }

    public static bool IsVisible(Bounds bounds)
    {
        EnsureFrustum();

        if (!hasPlanes)
            return true;

        return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
    }

    static void EnsureFrustum()
    {
        int frame = Time.frameCount;

        if (frame == cachedFrame)
            return;

        cachedFrame = frame;
        cachedCamera = Camera.main;
        hasPlanes = cachedCamera != null;

        if (hasPlanes)
            GeometryUtility.CalculateFrustumPlanes(cachedCamera, frustumPlanes);
    }
}
