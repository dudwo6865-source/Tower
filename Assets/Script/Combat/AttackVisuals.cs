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
        // 방향 정보가 없는 경우(예: 사망 이펙트)는 회전 없이 생성합니다.
        SpawnHitEffect(position, Vector3.zero, prefab, fallbackColor);
    }

    public static void SpawnHitEffect(
        Vector3 position,
        Vector3 incomingDirection,
        GameObject prefab,
        Color fallbackColor)
    {
        // 입사각의 반대(= 날아온 쪽)를 바라보게 회전합니다.
        Quaternion rotation = GetOppositeIncidenceRotation(incomingDirection);

        if (CombatEffectSpawner.Spawn(prefab, position, rotation) != null)
            return;

        GameObject hit = CreateSphere("HitEffect", position, 0.3f, fallbackColor);
        hit.transform.rotation = rotation;
        TempVisual temp = hit.AddComponent<TempVisual>();
        temp.Play(0.2f, 0.3f, 0.9f);
    }

    // 입사 방향(공격자 -> 피격 지점)의 반대 방향을 forward(+Z)로 하는 회전을 반환합니다.
    static Quaternion GetOppositeIncidenceRotation(Vector3 incomingDirection)
    {
        Vector3 back = -incomingDirection;

        if (back.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(back.normalized, Vector3.up);
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
        SelectableEntity attacker = null,
        bool piercing = false,
        float maxTravelDistance = 0f)
    {
        if (ProjectileSimWorld.Spawn(
                firePosition,
                rotation,
                target,
                targetHealth,
                damage,
                speed,
                prefab,
                hitEffectPrefab,
                fallbackProjectileColor,
                fallbackHitColor,
                attacker,
                piercing,
                maxTravelDistance) != null)
            return;

        GameObject projectileObject;

        if (prefab != null)
            projectileObject = CombatEffectSpawner.Spawn(prefab, firePosition, rotation);
        else
            projectileObject = CreateFallbackProjectile(firePosition, fallbackProjectileColor);

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
            attacker,
            piercing,
            maxTravelDistance);
    }

    public static GameObject CreateFallbackProjectile(Vector3 position, Color color)
    {
        return CreateSphere("Projectile", position, 0.25f, color);
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
