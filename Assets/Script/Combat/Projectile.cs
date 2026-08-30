using Unity.Mathematics;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Tooltip("투사체가 사라지기까지의 최대 생존 시간(초)입니다.")]
    public float maxLifeTime = 5f;

    private SelectableEntity target;
    private SelectableEntity attacker;
    private EntityHealth targetHealth;
    private float damage;
    private float speed;
    private GameObject hitEffectPrefab;
    private Color hitFallbackColor;
    private Vector3 lastKnownPosition;
    private Vector3 lastMoveDirection;
    private ParticleSystem[] cachedParticles;
    private TrailRenderer[] cachedTrails;
    private Renderer cachedRenderer;
    private bool visualsCached;
    private float lifeTimer;

    // 화염방사기(관통) 투사체용입니다. 명중해도 사라지지 않고 사거리 끝까지 직진합니다.
    private bool piercing;
    private float maxTravelDistance;
    private float traveledDistance;
    private bool pierceHitApplied;

    public int Slot { get; private set; } = -1;
    public int PoolKey { get; private set; }

    public void SetPoolKey(int poolKey)
    {
        PoolKey = poolKey;
    }

    public void AssignSlot(int slot)
    {
        Slot = slot;
    }

    void Update()
    {
        if (Slot >= 0)
            return;

        lifeTimer += Time.deltaTime;

        if (lifeTimer >= maxLifeTime)
        {
            ReleaseOrDestroy();
            return;
        }

        float step = speed * Time.deltaTime;

        // 이미 한 번 명중한 관통 투사체는 대상을 더 쫓지 않고 마지막 방향으로 직진합니다.
        if (piercing && pierceHitApplied)
        {
            transform.position += lastMoveDirection * step;
            traveledDistance += step;

            if (traveledDistance >= maxTravelDistance)
                ReleaseOrDestroy();

            return;
        }

        Vector3 destination = GetTargetPoint();
        Vector3 moveDelta = destination - transform.position;

        if (moveDelta.sqrMagnitude > 0.0001f)
            lastMoveDirection = moveDelta.normalized;

        Vector3 previousPosition = transform.position;
        transform.position = Vector3.MoveTowards(transform.position, destination, step);
        traveledDistance += Vector3.Distance(previousPosition, transform.position);

        if ((transform.position - destination).sqrMagnitude <= 0.09f)
        {
            if (piercing)
            {
                ApplyPierceHit();
                pierceHitApplied = true;

                if (traveledDistance >= maxTravelDistance)
                    ReleaseOrDestroy();
            }
            else
            {
                Impact();
            }
        }
    }

    public void Initialize(
        SelectableEntity target,
        EntityHealth targetHealth,
        float damage,
        float speed,
        GameObject hitEffectPrefab,
        Color hitFallbackColor,
        SelectableEntity attacker = null,
        bool piercing = false,
        float maxTravelDistance = 0f)
    {
        this.target = target;
        this.targetHealth = targetHealth;
        this.damage = damage;
        this.speed = speed;
        this.hitEffectPrefab = hitEffectPrefab;
        this.hitFallbackColor = hitFallbackColor;
        this.attacker = attacker;
        this.piercing = piercing;
        this.maxTravelDistance = maxTravelDistance;
        traveledDistance = 0f;
        pierceHitApplied = false;
        lifeTimer = 0f;

        lastKnownPosition = GetTargetPoint();

        Vector3 initialDir = lastKnownPosition - transform.position;
        lastMoveDirection = initialDir.sqrMagnitude > 0.0001f
            ? initialDir.normalized
            : transform.forward;
    }

    public ProjectileSimData CreateSimData(float impactDistanceSq)
    {
        return new ProjectileSimData
        {
            Position = transform.position,
            TargetPosition = lastKnownPosition,
            LastMoveDirection = lastMoveDirection,
            Speed = speed,
            LifeTimer = 0f,
            MaxLifeTime = maxLifeTime,
            ImpactDistanceSq = impactDistanceSq,
            Active = 1,
            Impacted = 0,
            Expired = 0,
            Piercing = (byte)(piercing ? 1 : 0),
            HitApplied = 0,
            PierceHit = 0,
            TraveledDistance = 0f,
            MaxTravelDistance = maxTravelDistance
        };
    }

    public float3 GetHomingPoint()
    {
        lastKnownPosition = GetTargetPoint();
        return lastKnownPosition;
    }

    public void ApplySimState(in ProjectileSimData sim)
    {
        transform.position = sim.Position;
        lastMoveDirection = sim.LastMoveDirection;
    }

    public void Impact()
    {
        if (targetHealth != null && targetHealth.IsAlive)
            targetHealth.TakeDamage(damage, attacker);

        AttackVisuals.SpawnHitEffect(
            transform.position,
            lastMoveDirection,
            hitEffectPrefab,
            hitFallbackColor);

        ReleaseOrDestroy();
    }

    // 화염방사기(관통) 투사체의 명중 처리입니다. 피해와 이펙트만 주고 사라지지 않습니다.
    public void ApplyPierceHit()
    {
        if (targetHealth != null && targetHealth.IsAlive)
            targetHealth.TakeDamage(damage, attacker);

        AttackVisuals.SpawnHitEffect(
            transform.position,
            lastMoveDirection,
            hitEffectPrefab,
            hitFallbackColor);
    }

    void ReleaseOrDestroy()
    {
        if (ProjectileSimWorld.Instance != null)
            ProjectileSimWorld.Instance.Release(this);
        else
            Destroy(gameObject);
    }

    public void PrepareForPool()
    {
        CacheVisuals();
        StopVisuals();
        target = null;
        targetHealth = null;
        attacker = null;
        hitEffectPrefab = null;
        lifeTimer = 0f;
        piercing = false;
        maxTravelDistance = 0f;
        traveledDistance = 0f;
        pierceHitApplied = false;
        Slot = -1;
    }

    public void RestartVisuals(Color fallbackColor)
    {
        CacheVisuals();

        if (cachedRenderer != null && PoolKey == 0)
            cachedRenderer.material.color = fallbackColor;

        if (cachedParticles != null)
        {
            for (int i = 0; i < cachedParticles.Length; i++)
            {
                ParticleSystem particle = cachedParticles[i];

                if (particle == null)
                    continue;

                particle.Clear(true);
                particle.Play(true);
            }
        }

        if (cachedTrails != null)
        {
            for (int i = 0; i < cachedTrails.Length; i++)
            {
                if (cachedTrails[i] != null)
                    cachedTrails[i].Clear();
            }
        }
    }

    Vector3 GetTargetPoint()
    {
        if (target != null && (targetHealth == null || targetHealth.IsAlive))
        {
            lastKnownPosition = target.SelectionBounds.center;
            return lastKnownPosition;
        }

        return lastKnownPosition;
    }

    void CacheVisuals()
    {
        if (visualsCached)
            return;

        cachedParticles = GetComponentsInChildren<ParticleSystem>(true);
        cachedTrails = GetComponentsInChildren<TrailRenderer>(true);
        cachedRenderer = GetComponent<Renderer>();
        visualsCached = true;
    }

    void StopVisuals()
    {
        if (cachedParticles != null)
        {
            for (int i = 0; i < cachedParticles.Length; i++)
            {
                ParticleSystem particle = cachedParticles[i];

                if (particle == null)
                    continue;

                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (cachedTrails != null)
        {
            for (int i = 0; i < cachedTrails.Length; i++)
            {
                if (cachedTrails[i] != null)
                    cachedTrails[i].Clear();
            }
        }
    }
}
