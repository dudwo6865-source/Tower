using System.Collections.Generic;
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
    private bool pierceLocked;
    private float pierceHitRadius;
    private float fireHeight;
    private Collider pierceCollider;
    private Rigidbody pierceRigidbody;
    private readonly HashSet<SelectableEntity> pierceHitEntities = new HashSet<SelectableEntity>();

    // 대포(포물선) 투사체용입니다. 발사 시 착탄 지점을 스냅샷해 그 자리로 날아간 뒤 범위 피해를 줍니다.
    private bool arcing;
    private float arcHeight;
    private float splashRadius;
    private float splashMinDamageRatio;
    private Vector3 arcStartPosition;
    private Vector3 arcImpactPosition;
    private float arcDuration;
    private float arcElapsed;

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

        if (arcing)
        {
            UpdateArcMovement();
            return;
        }

        float step = speed * Time.deltaTime;

        // 이미 대상 근처에 도달한 관통 투사체는 더 쫓지 않고 마지막 방향으로 직진합니다.
        if (piercing && pierceLocked)
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
                pierceLocked = true;

                if (traveledDistance >= maxTravelDistance)
                    ReleaseOrDestroy();
            }
            else
            {
                Impact();
            }
        }
    }

    // 화염방사기(관통) 투사체의 콜라이더에 닿은 모든 적에게 피해를 줍니다. (아군·중복 피격 제외)
    void OnTriggerEnter(Collider other)
    {
        if (!piercing)
            return;

        SelectableEntity entity = other.GetComponentInParent<SelectableEntity>();

        if (entity == null || entity == attacker)
            return;

        if (attacker != null && entity.ownerId == attacker.ownerId)
            return;

        if (!pierceHitEntities.Add(entity))
            return;

        EntityHealth health = entity.CachedHealth;

        if (health == null || !health.IsAlive)
            return;

        health.TakeDamage(damage, attacker);

        AttackVisuals.SpawnHitEffect(
            transform.position,
            lastMoveDirection,
            hitEffectPrefab,
            hitFallbackColor);
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
        float maxTravelDistance = 0f,
        float pierceHitRadius = 0.5f,
        bool arcing = false,
        float arcHeight = 0f,
        float splashRadius = 0f,
        float splashMinDamageRatio = 1f)
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
        this.pierceHitRadius = pierceHitRadius;
        this.arcing = arcing;
        this.arcHeight = arcHeight;
        this.splashRadius = splashRadius;
        this.splashMinDamageRatio = splashMinDamageRatio;
        traveledDistance = 0f;
        pierceLocked = false;
        lifeTimer = 0f;
        fireHeight = transform.position.y;
        pierceHitEntities.Clear();
        ConfigurePierceCollision(piercing);

        lastKnownPosition = GetTargetPoint();

        Vector3 initialDir = lastKnownPosition - transform.position;
        lastMoveDirection = initialDir.sqrMagnitude > 0.0001f
            ? initialDir.normalized
            : transform.forward;

        if (arcing)
        {
            // 착탄 지점을 발사 시점에 고정합니다(호밍 아님). 화염방사기와 달리 타겟 하나가 아니라
            // 이 지점 주변 반경(splashRadius) 안의 모든 적이 피해를 입습니다.
            arcStartPosition = transform.position;
            arcImpactPosition = lastKnownPosition;

            Vector3 flatStart = new Vector3(arcStartPosition.x, 0f, arcStartPosition.z);
            Vector3 flatImpact = new Vector3(arcImpactPosition.x, 0f, arcImpactPosition.z);
            float horizontalDistance = Vector3.Distance(flatStart, flatImpact);

            arcDuration = speed > 0.01f
                ? Mathf.Max(0.05f, horizontalDistance / speed)
                : 0.05f;
            arcElapsed = 0f;
        }
    }

    // 발사 지점 -> 착탄 지점을 포물선으로 이동합니다. 도착하면 범위 피해를 적용합니다.
    void UpdateArcMovement()
    {
        arcElapsed += Time.deltaTime;
        float t = arcDuration > 0f ? Mathf.Clamp01(arcElapsed / arcDuration) : 1f;

        Vector3 position = Vector3.Lerp(arcStartPosition, arcImpactPosition, t);
        position.y += arcHeight * 4f * t * (1f - t);
        transform.position = position;

        Vector3 direction = arcImpactPosition - arcStartPosition;
        if (direction.sqrMagnitude > 0.0001f)
            lastMoveDirection = direction.normalized;

        if (t >= 1f)
            ImpactArea();
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
            Locked = 0,
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

    // 대포(포물선) 투사체의 착탄 처리입니다. 단일 타겟이 아니라 착탄 지점 반경(splashRadius) 안의
    // 모든 적에게, 중심에서 멀수록 감쇠되는 피해를 줍니다.
    public void ImpactArea()
    {
        ApplySplashDamage(transform.position, splashRadius, splashMinDamageRatio, damage, attacker);

        AttackVisuals.SpawnHitEffect(
            transform.position,
            lastMoveDirection,
            hitEffectPrefab,
            hitFallbackColor);

        ReleaseOrDestroy();
    }

    static void ApplySplashDamage(
        Vector3 center,
        float radius,
        float minDamageRatio,
        float damage,
        SelectableEntity attacker)
    {
        if (radius <= 0f)
            return;

        float radiusSq = radius * radius;
        IReadOnlyList<SelectableEntity> entities = SelectableRegistry.Entities;

        for (int i = 0; i < entities.Count; i++)
        {
            SelectableEntity entity = entities[i];

            if (entity == null || entity == attacker)
                continue;

            // 아군(같은 소유자)은 범위 피해에서 제외합니다.
            if (attacker != null && entity.ownerId == attacker.ownerId)
                continue;

            Vector3 delta = entity.SelectionBounds.center - center;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;

            if (sqrDistance > radiusSq)
                continue;

            EntityHealth health = entity.CachedHealth;

            if (health == null || !health.IsAlive)
                continue;

            float distanceRatio = Mathf.Sqrt(sqrDistance) / radius;
            float damageRatio = Mathf.Lerp(1f, minDamageRatio, distanceRatio);

            health.TakeDamage(damage * damageRatio, attacker);
        }
    }

    // 화염방사기(관통) 투사체에 트리거 콜라이더를 붙이거나 켜고 끕니다.
    // 콜라이더에 닿는 모든 적에게 OnTriggerEnter로 피해를 줍니다.
    void ConfigurePierceCollision(bool active)
    {
        if (!active)
        {
            if (pierceCollider != null)
                pierceCollider.enabled = false;

            return;
        }

        if (pierceCollider == null)
        {
            pierceCollider = GetComponentInChildren<Collider>();

            if (pierceCollider == null)
            {
                SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
                sphere.radius = pierceHitRadius;
                pierceCollider = sphere;
            }
        }

        pierceCollider.isTrigger = true;
        pierceCollider.enabled = true;

        if (pierceRigidbody == null)
            pierceRigidbody = GetComponent<Rigidbody>();

        if (pierceRigidbody == null)
            pierceRigidbody = gameObject.AddComponent<Rigidbody>();

        pierceRigidbody.isKinematic = true;
        pierceRigidbody.useGravity = false;
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
        pierceLocked = false;
        pierceHitEntities.Clear();
        ConfigurePierceCollision(false);
        arcing = false;
        arcHeight = 0f;
        splashRadius = 0f;
        splashMinDamageRatio = 1f;
        arcElapsed = 0f;
        arcDuration = 0f;
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
            lastKnownPosition = target.SelectionBounds.center;

        // 화염방사기는 발사 높이를 그대로 유지합니다. 땅으로 파고들거나 위로 솟지 않습니다.
        if (piercing)
            lastKnownPosition.y = fireHeight;

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
