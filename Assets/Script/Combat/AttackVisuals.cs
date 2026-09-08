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
        Color fallbackColor,
        float scale = 1f)
    {
        // 방향 정보가 없는 경우(예: 사망 이펙트)는 회전 없이 생성합니다.
        SpawnHitEffect(position, Vector3.zero, prefab, fallbackColor, scale);
    }

    public static void SpawnHitEffect(
        Vector3 position,
        Vector3 incomingDirection,
        GameObject prefab,
        Color fallbackColor,
        float scale = 1f)
    {
        // 입사각의 반대(= 날아온 쪽)를 바라보게 회전합니다.
        Quaternion rotation = GetOppositeIncidenceRotation(incomingDirection);

        if (CombatEffectSpawner.Spawn(prefab, position, rotation, null, scale) != null)
            return;

        GameObject hit = CreateSphere("HitEffect", position, 0.3f * scale, fallbackColor);
        hit.transform.rotation = rotation;
        TempVisual temp = hit.AddComponent<TempVisual>();
        temp.Play(0.2f, 0.3f * scale, 0.9f);
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
        float maxTravelDistance = 0f,
        float pierceHitRadius = 0.5f,
        bool arcing = false,
        float arcHeight = 0f,
        float arcHeightRatio = 0f,
        float minArcHeight = 0f,
        float splashRadius = 0f,
        float splashMinDamageRatio = 1f,
        float arcClimbPower = 1f,
        float arcDivePower = 1f,
        float impactOffsetRadius = 0f,
        float arcPeakTime = 0.5f,
        float lateralWobbleAmount = 0f,
        float lateralWobbleRatio = 0f,
        bool useBallisticArc = false,
        float ballisticGravity = 20f,
        float hitEffectScale = 1f,
        GameObject trailEffectPrefab = null)
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
                maxTravelDistance,
                pierceHitRadius,
                arcing,
                arcHeight,
                arcHeightRatio,
                minArcHeight,
                splashRadius,
                splashMinDamageRatio,
                arcClimbPower,
                arcDivePower,
                impactOffsetRadius,
                arcPeakTime,
                lateralWobbleAmount,
                lateralWobbleRatio,
                useBallisticArc,
                ballisticGravity,
                hitEffectScale,
                trailEffectPrefab) != null)
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
            maxTravelDistance,
            pierceHitRadius,
            arcing,
            arcHeight,
            arcHeightRatio,
            minArcHeight,
            splashRadius,
            splashMinDamageRatio,
            arcClimbPower,
            arcDivePower,
            impactOffsetRadius,
            arcPeakTime,
            lateralWobbleAmount,
            lateralWobbleRatio,
            useBallisticArc,
            ballisticGravity,
            hitEffectScale,
            trailEffectPrefab);
    }

    public static GameObject CreateFallbackProjectile(Vector3 position, Color color)
    {
        GameObject projectile = CreateSphere("Projectile", position, 0.25f, color);
        ApplyAlwaysOnTopLayer(projectile);
        return projectile;
    }

    // 투사체 이동 없이 발사 지점에서 명중 지점까지 순간적으로 그리는 빛줄기(히트스캔 트레일)입니다.
    // 풀링 없이 짧게 재생 후 파괴합니다(머즐 플래시/피격 이펙트와 동일한 방식, 빈도가 낮아 충분히 저렴합니다).
    public static void SpawnHitscanTrail(
        Vector3 start,
        Vector3 end,
        GameObject prefab,
        Color fallbackColor,
        float duration,
        float width)
    {
        GameObject trailObject = prefab != null
            ? Object.Instantiate(prefab)
            : CreateFallbackHitscanTrail(fallbackColor, width);

        if (trailObject == null)
            return;

        // 프리팹을 썼든 기본 라인이든 상관없이 지형에 가려지지 않도록 적용합니다.
        ApplyAlwaysOnTopLayer(trailObject);

        HitscanTrail trail = trailObject.GetComponent<HitscanTrail>();
        if (trail == null)
            trail = trailObject.AddComponent<HitscanTrail>();

        // 프리팹을 지정했다면 그 프리팹에 만들어둔 색상 그라디언트/두께 커브를 그대로 씁니다.
        // 프리팹이 없어 기본 라인을 만든 경우에만 UnitAttacker 값(색상/두께)으로 채웁니다.
        bool overrideColorAndWidth = prefab == null;
        trail.Play(start, end, duration, fallbackColor, width, overrideColorAndWidth);
    }

    static GameObject CreateFallbackHitscanTrail(Color color, float width)
    {
        GameObject trailObject = new GameObject("HitscanTrail");
        LineRenderer line = trailObject.AddComponent<LineRenderer>();

        line.material = GetMaterial();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = width;
        line.endWidth = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        return trailObject;
    }

    // "Effect" 레이어를 URP Renderer의 Render Objects 기능(Depth Test: Always)과 짝지어두면
    // 이 오브젝트가 지형 등에 가려지지 않고 항상 위에 그려집니다. 레이어가 없는 프로젝트에서는
    // 조용히 무시합니다(에러 없이 기본 레이어로 남음). 프리팹 자식들도 렌더러를 가질 수 있으므로
    // 재귀적으로 전부 적용합니다.
    static void ApplyAlwaysOnTopLayer(GameObject target)
    {
        int effectLayer = LayerMask.NameToLayer("Effect");
        if (effectLayer < 0)
            return;

        SetLayerRecursively(target.transform, effectLayer);
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
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
