using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Tooltip("투사체가 사라지기까지의 최대 생존 시간(초)입니다.")]
    public float maxLifeTime = 5f;

    [Header("회전(스핀) 연출")]
    [Tooltip("비행 중 초당 회전 각도입니다. 0이면 회전하지 않습니다.")]
    public float spinSpeed = 900f;

    [Tooltip("회전축(로컬 좌표)입니다. 발사 방향은 로컬 Z(forward)이므로, 기본값인 로컬 X(오른쪽)처럼 " +
        "forward와 수직한 축을 쓰면 총알이 옆으로 텀블링하며 날아가는 것처럼 보입니다.")]
    public Vector3 spinAxisLocal = Vector3.right;

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

    // 비행 중 발사체를 따라다니는 연기 트레일입니다. 발사체가 풀에 반환되거나 파괴될 때
    // 이 자식만 부모에서 분리해 남겨두고, 자체적으로 잦아들며 사라지게 합니다
    // (연기 프리팹의 ParticleSystem Main 모듈에서 Stop Action을 Destroy로 설정해두면
    // 남은 입자가 다 사라진 뒤 자동으로 파괴되어 따로 정리 코드가 필요 없습니다).
    private GameObject trailEffectInstance;

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
    private float hitEffectScale = 1f;
    private Vector3 arcStartPosition;
    private Vector3 arcImpactPosition;
    private float arcDuration;
    private float arcElapsed;

    // 켜져 있으면 arcHeight류 값을 무시하고, 발사 속도 + 중력만으로 실제 탄도학 공식에 따라
    // 발사각을 계산합니다. 사거리와 무관하게 항상 같은 '무게감'으로 보이게 하기 위한 값입니다.
    private bool ballisticArc;
    private float ballisticGravity;
    private float ballisticInitialVerticalSpeed;

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
        // 이동 로직(Slot 유무, 관통/포물선 여부)과 무관하게 항상 적용되는 순수 시각 연출입니다.
        if (spinSpeed != 0f)
            transform.Rotate(spinAxisLocal, spinSpeed * Time.deltaTime, Space.Self);

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
        float lateralWobbleRatio = 0f,
        bool ballisticArc = false,
        float ballisticGravity = 20f,
        float hitEffectScale = 1f,
        GameObject trailEffectPrefab = null)
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
        this.hitEffectScale = hitEffectScale;
        this.ballisticArc = ballisticArc;
        this.ballisticGravity = Mathf.Max(0.01f, ballisticGravity);
        traveledDistance = 0f;
        pierceLocked = false;
        lifeTimer = 0f;
        fireHeight = transform.position.y;
        pierceHitEntities.Clear();
        ConfigurePierceCollision(piercing);

        // 매 발사마다 새로 생성해 자식으로 붙입니다(비행 중 위치를 자동으로 따라오게).
        // CacheVisuals()가 이미 한 번 실행된 뒤에 붙으므로, 재사용 시 초기화 로직
        // (StopVisuals 등)이 이 인스턴스를 건드리지 않습니다 — 명중/소멸 시 따로 뗍니다.
        if (trailEffectPrefab != null)
            trailEffectInstance = Instantiate(trailEffectPrefab, transform);

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

            if (this.ballisticArc)
                ComputeBallisticTrajectory(horizontalDistance, arcImpactPosition.y - arcStartPosition.y, speed);
            else
                arcDuration = speed > 0.01f
                    ? Mathf.Max(0.05f, horizontalDistance / speed)
                    : 0.05f;

            arcElapsed = 0f;
        }
    }

    // 발사 속도(v)와 중력(g)만으로 목표를 맞히는 발사각을 계산합니다. arcHeight/Ratio 같은 수동 값
    // 없이도, 물리적으로 일관된(사거리와 무관하게 항상 같은 '무게감'의) 포물선이 나오도록 합니다.
    // 낮은 각/높은 각 두 해가 나오는데, 대포처럼 눈에 띄는 곡선을 그리도록 항상 높은 각을 씁니다.
    void ComputeBallisticTrajectory(float horizontalDistance, float heightDelta, float launchSpeed)
    {
        float g = ballisticGravity;
        float v = Mathf.Max(0.01f, launchSpeed);
        float x = Mathf.Max(0.05f, horizontalDistance);

        float a = g * x * x / (2f * v * v);
        float c = heightDelta + a;
        float discriminant = x * x - 4f * a * c;

        if (discriminant < 0f)
        {
            // 이 속도로는 물리적으로 도달 불가능한 거리입니다. 정확히 도달 가능한 최소 속도로 대체합니다.
            float minSpeedSq = g * (heightDelta + Mathf.Sqrt(x * x + heightDelta * heightDelta));
            v = Mathf.Sqrt(Mathf.Max(minSpeedSq, 0.01f));
            a = g * x * x / (2f * v * v);
            c = heightDelta + a;
            discriminant = Mathf.Max(0f, x * x - 4f * a * c);
        }

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float tanAngle = (x + sqrtDiscriminant) / (2f * a);

        float vx = v / Mathf.Sqrt(1f + tanAngle * tanAngle);
        ballisticInitialVerticalSpeed = vx * tanAngle;

        arcDuration = vx > 0.01f ? Mathf.Max(0.05f, x / vx) : Mathf.Max(0.05f, 2f * ballisticInitialVerticalSpeed / g);
    }

    // 발사 지점 -> 착탄 지점을 포물선(비대칭 지원)으로 이동합니다. 도착하면 범위 피해를 적용합니다.
    void UpdateArcMovement()
    {
        Vector3 previousPosition = transform.position;

        arcElapsed += Time.deltaTime;
        float t = arcDuration > 0f ? Mathf.Clamp01(arcElapsed / arcDuration) : 1f;

        Vector3 position = Vector3.Lerp(arcStartPosition, arcImpactPosition, t);
        position.y += ballisticArc ? EvaluateBallisticHeightOffset(Mathf.Min(arcElapsed, arcDuration)) : EvaluateArcHeight(t);

        if (arcLateralOffset != 0f)
            position += arcRightAxis * (arcLateralOffset * Mathf.Sin(t * Mathf.PI));

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

    // 발사~착탄을 잇는 직선을 기준으로, 진짜 중력 포물선이 그 위로 얼마나 부풀어 오르는지를
    // 구합니다(직선 자체는 이미 Lerp로 처리되므로 여기서는 '더해지는 여분의 높이'만 반환).
    // 0.5*g*t*(T-t) 형태로, 시작·끝(t=0, t=T)에서 정확히 0이 되어 Lerp와 매끄럽게 이어집니다.
    float EvaluateBallisticHeightOffset(float elapsed)
    {
        return 0.5f * ballisticGravity * elapsed * (arcDuration - elapsed);
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
            hitFallbackColor,
            hitEffectScale);

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
        DetachTrailEffect();

        if (ProjectileSimWorld.Instance != null)
            ProjectileSimWorld.Instance.Release(this);
        else
            Destroy(gameObject);
    }

    // 연기 트레일을 발사체에서 분리해 그 자리에 남깁니다. 새 입자 생성만 멈추고(Clear는 하지
    // 않음) 이미 나온 연기는 계속 퍼지다 자연스럽게 사라지게 둡니다. 발사체가 풀에 반환되거나
    // 파괴되어도 트레일은 영향받지 않고, 자체 Stop Action(Destroy) 설정으로 알아서 정리됩니다.
    void DetachTrailEffect()
    {
        if (trailEffectInstance == null)
            return;

        trailEffectInstance.transform.SetParent(null, true);

        ParticleSystem[] trailParticles = trailEffectInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < trailParticles.Length; i++)
            trailParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);

        trailEffectInstance = null;
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
        hitEffectScale = 1f;
        arcElapsed = 0f;
        arcDuration = 0f;
        ballisticArc = false;
        ballisticGravity = 20f;
        ballisticInitialVerticalSpeed = 0f;
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
