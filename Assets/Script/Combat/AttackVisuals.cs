using UnityEngine;

public static class AttackVisuals
{
    private static Material sharedMaterial;

    public static void SpawnMuzzleFlash(Vector3 position, Color color)
    {
        GameObject flash = CreateSphere("MuzzleFlash", position, 0.35f, color);
        TempVisual temp = flash.AddComponent<TempVisual>();
        temp.Play(0.12f, 0.35f, 0.05f);
    }

    public static void SpawnHitEffect(Vector3 position, Color color)
    {
        GameObject hit = CreateSphere("HitEffect", position, 0.3f, color);
        TempVisual temp = hit.AddComponent<TempVisual>();
        temp.Play(0.2f, 0.3f, 0.9f);
    }

    public static void SpawnProjectile(
        Vector3 firePosition,
        SelectableEntity target,
        EntityHealth targetHealth,
        float damage,
        float speed,
        Color color,
        Color hitColor)
    {
        GameObject projectileObject =
            CreateSphere("Projectile", firePosition, 0.25f, color);

        Projectile projectile = projectileObject.AddComponent<Projectile>();
        projectile.Initialize(
            target,
            targetHealth,
            damage,
            speed,
            hitColor);
    }

    static GameObject CreateSphere(
        string objectName,
        Vector3 position,
        float diameter,
        Color color)
    {
        GameObject sphere =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);

        sphere.name = objectName;
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * diameter;

        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);

        Renderer renderer = sphere.GetComponent<Renderer>();
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.material = GetMaterial();
        renderer.material.color = color;

        return sphere;
    }

    static Material GetMaterial()
    {
        if (sharedMaterial != null)
            return new Material(sharedMaterial);

        Shader shader =
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Standard");

        sharedMaterial = new Material(shader);
        return new Material(sharedMaterial);
    }
}
