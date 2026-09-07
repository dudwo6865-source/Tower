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

    // 포물선 경로를 따라 '일정 속도'로 움직이기 위한 호(arc-length) 테이블입니다.
    // 시간 t를 그대로 진행률로 쓰면 오르내리는 구간에서 실제 이동 속도가 빨라지거나
    // 느려져 보이므로(수평 이동은 일정해도 수직 성분이 더해지므로), 경로 길이를 미리
    // 샘플링해 '이동한 거리 -> t' 로 변환합니다.
    private const int ArcSampleCount = 24;
    private float[] arcCumulativeDistance;
    private float arcTotalLength;
    private float arcTraveledDistance;

    // 상승/하강 구간을 비대칭으로 만들어 미사일처럼 보이게 하는 값들입니다.
    private float arcClimbPower;
    private float arcDivePower;
    private float arcPeakTime;

    // 비행 중 좌우로 살짝 구불거리게 하는 값입니다(정점에서 최대, 시작·끝점에서는 0).
    private float arcLateralOffset;
    private Vector3 arcRightAxis;

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
        {
            lastMoveDirection = moveDelta.normalized;
            FaceMoveDirection();
        }

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
        float arcHeightRatio = 0f,
        float minArcHeight = 0f,
        float splashRadius = 0f,
        float splashMinDamageRatio = 1f,
        float arcClimbPower = 1f,
        float arcDivePower = 1f,
        float impactOffsetRadius = 0f,
        float arcPeakTime = 0.5f,
        float lateralWobbleAmount = 0f,
        float lateralWobbleRatio = 0f)
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
        FaceMoveDirection();

        if (arcing)
        {
            // 착탄 지점을 발사 시점에 고정합니다(호밍 아님). 화염방사기와 달리 타겟 하나가 아니라
            // 이 지점 주변 반경(splashRadius) 안의 모든 적이 피해를 입습니다.
            arcStartPosition = transform.position;
            arcImpactPosition = lastKnownPosition;

            // 착탄 지점에 랜덤 오차를 더해, 매번 타겟 중심에 정확히 꽂히지 않게 합니다.
            if (impactOffsetRadius > 0f)
            {
                Vector2 offset2D = UnityEngine.Random.insideUnitCircle * impactOffsetRadius;
                arcImpactPosition += new Vector3(offset2D.x, 0f, offset2D.y);
            }

            Vector3 flatStart = new Vector3(arcStartPosition.x, 0f, arcStartPosition.z);
            Vector3 flatImpact = new Vector3(arcImpactPosition.x, 0f, arcImpactPosition.z);
            Vector3 flatDelta = flatImpact - flatStart;
            float horizontalDistance = flatDelta.magnitude;

            // 가까운 거리에서 너무 빠르게 솟았다 내려오지 않도록, 거리에 비례한 높이를
            // Min~Max(arcHeight) 사이로 clamp합니다.
            float requestedHeight = horizontalDistance * arcHeightRatio;
            this.arcHeight = Mathf.Clamp(requestedHeight, minArcHeight, Mathf.Max(minArcHeight, arcHeight));

            this.arcClimbPower = Mathf.Max(0f, arcClimbPower);
            this.arcDivePower = Mathf.Max(0f, arcDivePower);
            this.arcPeakTime = Mathf.Clamp(arcPeakTime, 0.05f, 0.95f);

            // 비행 정점 부근에서 좌우로 치우치는 오프셋입니다. 시작/끝점에서는 0이 되도록
            // Update에서 sin(pi*t)로 감싸므로, 여기서는 최대 치우침 크기만 무작위로 정합니다.
            // 거리에 비례(lateralWobbleRatio)해 계산한 뒤 상한(lateralWobbleAmount)으로 clamp합니다.
            // 가까운 적을 쏠 때는 비례값이 작아져 흔들림이 자동으로 줄어듭니다.
            arcRightAxis = flatDelta.sqrMagnitude > 0.0001f
                ? Vector3.Cross(Vector3.up, flatDelta.normalized)
                : Vector3.right;

            float wobbleRange = horizontalDistance * Mathf.Max(0f, lateralWobbleRatio);
            if (lateralWobbleAmount > 0f)
                wobbleRange = Mathf.Min(wobbleRange, lateralWobbleAmount);

            arcLateralOffset = wobbleRange > 0f
                ? UnityEngine.Random.Range(-wobbleRange, wobbleRange)
                : 0f;

            arcTraveledDistance = 0f;
            BuildArcLengthTable();
        }
    }

    // EvaluateArcPosition(t)을 0~1 구간에서 균일하게 샘플링해 누적 경로 길이 테이블을 만듭니다.
    // UpdateArcMovement는 이 테이블로 '이동한 거리'를 t로 변환하므로, 오르내리는 구간이 있어도
    // 실제 이동 속도(초당 이동 거리)가 항상 speed로 일정하게 유지됩니다.
    void BuildArcLengthTable()
    {
        if (arcCumulativeDistance == null || arcCumulativeDistance.Length != ArcSampleCount + 1)
            arcCumulativeDistance = new float[ArcSampleCount + 1];

        Vector3 previous = EvaluateArcPosition(0f);
        arcCumulativeDistance[0] = 0f;

        for (int i = 1; i <= ArcSampleCount; i++)
        {
            float t = (float)i / ArcSampleCount;
            Vector3 current = EvaluateArcPosition(t);
            arcCumulativeDistance[i] = arcCumulativeDistance[i - 1] + Vector3.Distance(previous, current);
            previous = current;
        }

        arcTotalLength = arcCumulativeDistance[ArcSampleCount];
    }

    // 누적 이동 거리(distance)를 곡선 위 진행률 t(0~1)로 변환합니다.
    float SampleArcParameter(float distance)
    {
        if (arcTotalLength <= 0.0001f || distance >= arcTotalLength)
            return 1f;

        if (distance <= 0f)
            return 0f;

        for (int i = 1; i <= ArcSampleCount; i++)
        {
            if (distance <= arcCumulativeDistance[i])
            {
                float segStart = arcCumulativeDistance[i - 1];
                float segLength = arcCumulativeDistance[i] - segStart;
                float localT = segLength > 0.0001f ? (distance - segStart) / segLength : 0f;
                float t0 = (float)(i - 1) / ArcSampleCount;
                float t1 = (float)i / ArcSampleCount;
                return Mathf.Lerp(t0, t1, localT);
            }
        }

        return 1f;
    }

    // 포물선 경로 위에서 진행률 t(0~1)에 해당하는 월드 좌표를 계산합니다.
    Vector3 EvaluateArcPosition(float t)
    {
        Vector3 position = Vector3.Lerp(arcStartPosition, arcImpactPosition, t);
        position.y += EvaluateArcHeight(t);

        if (arcLateralOffset != 0f)
            position += arcRightAxis * (arcLateralOffset * Mathf.Sin(t * Mathf.PI));

        return position;
    }

    // 발사 지점 -> 착탄 지점을 포물선(비대칭 지원)으로 이동합니다. 도착하면 범위 피해를 적용합니다.
    void UpdateArcMovement()
    {
        Vector3 previousPosition = transform.position;

        arcTraveledDistance += speed * Time.deltaTime;
        float t = SampleArcParameter(arcTraveledDistance);
        Vector3 position = EvaluateArcPosition(t);

        transform.position = position;

        // 발사~착탄 직선이 아니라, 실제 이번 프레임에 움직인 방향(오르내림·좌우 흔들림 포함)을
        // 그대로 바라보게 합니다. 그래야 포물선을 오르내리거나 옆으로 흔들릴 때도 모델이
        // 진행 방향을 향해 자연스럽게 기울어집니다.
        Vector3 frameDelta = position - previousPosition;
        if (frameDelta.sqrMagnitude > 0.000001f)
        {
            lastMoveDirection = frameDelta.normalized;
            FaceMoveDirection();
        }

        if (t >= 1f)
            ImpactArea();
    }

    // lastMoveDirection이 바뀔 때마다 호출해, 투사체가 항상 실제 이동 방향을 바라보게 합니다.
    void FaceMoveDirection()
    {
        if (lastMoveDirection.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(lastMoveDirection, Vector3.up);
    }

    // arcPeakTime 이전은 상승, 이후는 하강 구간입니다. 두 구간 모두 정점에서 기울기가 0이라
    // 이어붙여도 매끄럽습니다(arcClimbPower=arcDivePower=1, arcPeakTime=0.5일 때는
    // 기존의 대칭 포물선 4t(1-t)와 완전히 같은 모양입니다). climb/dive power를 키우면
    // 그 구간의 '먼 쪽 끝'(발사 직후 / 착탄 직전)에서 변화가 가팔라집니다 — dive power를
    // 높이면 정점 높이를 오래 유지하다가 착탄 직전에야 급격히 떨어집니다.
    float EvaluateArcHeight(float t)
    {
        if (t <= arcPeakTime)
        {
            float x = arcPeakTime > 0.0001f ? t / arcPeakTime : 1f;
            return arcHeight * (1f - Mathf.Pow(1f - x, arcClimbPower + 1f));
        }

        float span = 1f - arcPeakTime;
        float y = span > 0.0001f ? (t - arcPeakTime) / span : 1f;
        return arcHeight * (1f - Mathf.Pow(y, arcDivePower + 1f));
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
        FaceMoveDirection();
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
        arcTraveledDistance = 0f;
        arcTotalLength = 0f;
        arcClimbPower = 1f;
        arcDivePower = 1f;
        arcPeakTime = 0.5f;
        arcLateralOffset = 0f;
        arcRightAxis = Vector3.zero;
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
