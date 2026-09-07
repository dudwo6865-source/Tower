using UnityEngine;

public static class CombatEffectSpawner
{
    public static GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null,
        float scale = 1f)
    {
        if (prefab == null)
            return null;

        GameObject instance = Object.Instantiate(prefab, position, rotation, parent);

        if (!Mathf.Approximately(scale, 1f))
        {
            instance.transform.localScale *= scale;
            ForceHierarchyScaling(instance);
        }

        ScheduleAutoDestroy(instance);
        return instance;
    }

    // Scaling Mode가 Local/Shape인 파티클 시스템은 부모(루트) 오브젝트의 스케일을 무시합니다.
    // 스폰된 이 인스턴스에 한해 Hierarchy로 강제해, 위에서 적용한 스케일이 실제로 반영되게 합니다.
    // (원본 프리팹 에셋은 건드리지 않습니다.)
    static void ForceHierarchyScaling(GameObject instance)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    public static Quaternion GetFlatLookRotation(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    static void ScheduleAutoDestroy(GameObject instance)
    {
        if (instance == null)
            return;

        float lifetime = EstimateLifetime(instance);

        if (lifetime > 0f)
            Object.Destroy(instance, lifetime);
    }

    static float EstimateLifetime(GameObject instance)
    {
        float maxLifetime = 0f;
        ParticleSystem[] particleSystems =
            instance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;

            if (main.loop)
                continue;

            float startLifetime = GetMaxStartLifetime(main);
            float candidate = main.duration + startLifetime;

            if (candidate > maxLifetime)
                maxLifetime = candidate;
        }

        return maxLifetime;
    }

    static float GetMaxStartLifetime(ParticleSystem.MainModule main)
    {
        ParticleSystem.MinMaxCurve curve = main.startLifetime;

        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.Max(curve.constantMin, curve.constantMax);
            default:
                return curve.constantMax > 0f
                    ? curve.constantMax
                    : curve.constant;
        }
    }
}
