using UnityEngine;

public static class CombatEffectSpawner
{
    public static GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null)
    {
        if (prefab == null)
            return null;

        GameObject instance = Object.Instantiate(prefab, position, rotation, parent);
        ScheduleAutoDestroy(instance);
        return instance;
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
