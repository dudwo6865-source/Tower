using UnityEngine;

public static class AttackVisuals
{
    private static Material sharedMaterial;

    public static void SpawnMuzzleFlash(
        Vector3 position,
        Quaternion rotation,
        GameObject prefab,
        Color fallbackColor)
    {
        if (CombatEffectSpawner.Spawn(prefab, position, rotation) != null)
            return;

        GameObject flash = CreateSphere("MuzzleFlash", position, 0.35f, fallbackColor);
        flash.transform.rotation = rotation;
        TempVisual temp = flash.AddComponent<TempVisual>();
        temp.Play(0.12f, 0.35f, 0.05f);
    }

    public static void SpawnHitEffect(
        Vector3 position,
        GameObject prefab,
        Color fallbackColor)
    {
        if (CombatEffectSpawner.Spawn(prefab, position, Quaternion.identity) != null)
            return;

        GameObject hit = CreateSphere("HitEffect", position, 0.3f, fallbackColor);
        TempVisual temp = hit.AddComponent<TempVisual>();
        temp.Play(0.2f, 0.3f, 0.9f);
    }

    public static void SpawnProjectile(
        Vector3 firePosition,
        Quaternion rotation,
        SelectableEntity target,
        EntityHealth targetHealth,
        float damage,
        float speed,
        GameObject prefab,
        GameObject hitEffectPrefab,
        Color fallbackProjectileColor,
        Color fallbackHitColor,
        SelectableEntity attacker = null)
    {
        GameObject projectileObject;

        if (prefab != null)
            projectileObject = CombatEffectSpawner.Spawn(prefab, firePosition, rotation);
        else
            projectileObject = CreateSphere("Projectile", firePosition, 0.25f, fallbackProjectileColor);

        if (projectileObject == null)
            return;

        Projectile projectile = projectileObject.GetComponent<Projectile>();

        if (projectile == null)
            projectile = projectileObject.AddComponent<Projectile>();

        projectile.Initialize(
            target,
            targetHealth,
            damage,
            speed,
            hitEffectPrefab,
            fallbackHitColor,
            attacker);
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
