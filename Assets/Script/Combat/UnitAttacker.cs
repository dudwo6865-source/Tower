using UnityEngine;

public enum AttackType
{
    Melee,
    Ranged,
    Flamethrower,
    Cannon
}

[DisallowMultipleComponent]
public class UnitAttacker : MonoBehaviour
{
    [Header("Attack")]
    [Tooltip("근접은 즉시 피해(투사체 없음), 원거리는 투사체 발사, 화염방사기는 명중해도 사라지지 않고 사거리 끝까지 직진하는 관통 투사체를 발사합니다. 대포는 포물선을 그리며 날아가 착탄 지점 주변에 범위 피해를 줍니다(중심에서 멀수록 피해 감소). 사거리 규칙은 동일합니다.")]
    public AttackType attackType = AttackType.Melee;

    [Tooltip("한 번 공격할 때 주는 피해량입니다.")]
    public float attackDamage = 10f;

    [Tooltip("공격이 닿는 가로(XZ) 사거리입니다.")]
    public float attackRange = 2.5f;

    [Tooltip("공격 사이의 최소 간격(초)입니다.")]
    public float attackCooldown = 1f;

    [Header("Animation")]
    [Tooltip("켜면 공격 애니메이션 이벤트(OnAttackHit)에서 피해/투사체를 적용합니다.")]
    public bool useAttackAnimationEvent = true;

    [Header("Ranged")]
    [Tooltip("투사체 속도입니다. 원거리·화염방사기·대포 공격일 때만 사용됩니다. 대포는 이 값을 착탄 지점까지의 수평 이동 속도로 씁니다.")]
    public float projectileSpeed = 25f;

    [Tooltip("투사체가 발사될 위치 오브젝트입니다. 비워두면 이 오브젝트의 위치에서 발사합니다.")]
    public Transform firePoint;

    [Header("Flamethrower")]
    [Tooltip("화염방사기 투사체의 명중 판정 반지름입니다. 이 범위(콜라이더)에 닿은 모든 적이 피해를 입습니다. 투사체 프리팹에 콜라이더가 있으면 그 크기를 그대로 쓰고, 없을 때만 이 값으로 새로 만듭니다. 화염방사기 공격일 때만 사용됩니다.")]
    public float pierceHitRadius = 0.6f;

    [Header("Cannon")]
    [Tooltip("포물선 높이의 상한(m)입니다. 실제 높이는 '발사~착탄 거리 × Arc Height Ratio'로 계산한 뒤 Min~이 값 사이로 clamp됩니다. 대포 공격일 때만 사용됩니다.")]
    public float arcHeight = 4f;

    [Tooltip("거리 대비 포물선 높이 비율입니다. 값이 클수록 가까운 거리에서도 높이 솟아오릅니다. 대포 공격일 때만 사용됩니다.")]
    public float arcHeightRatio = 0.3f;

    [Tooltip("포물선 높이의 하한(m)입니다. 아주 가까운 거리에서도 최소한 이만큼은 솟아오릅니다. 대포 공격일 때만 사용됩니다.")]
    public float minArcHeight = 0.5f;

    [Tooltip("착탄 지점 기준 범위 피해 반경입니다. 이 안의 적(아군 제외)이 모두 피해를 입습니다. 대포 공격일 때만 사용됩니다.")]
    public float splashRadius = 3f;

    [Tooltip("범위 피해 감쇠 비율입니다. 착탄 중심은 100% 피해, 반경 끝은 이 비율(0~1)만큼만 피해를 입습니다. 대포 공격일 때만 사용됩니다.")]
    [Range(0f, 1f)]
    public float splashMinDamageRatio = 0.3f;

    [Tooltip("히트 이펙트가 원래 크기(1배)로 보이는 기준 스플래시 반경입니다. Splash Radius가 이 값보다 크면 이펙트가 커지고, 작으면 작아집니다. 대포 공격일 때만 사용됩니다.")]
    public float hitEffectBaseRadius = 3f;

    [Header("Aim")]
    [Tooltip("켜면 조준(바라보기)이 끝난 뒤에만 공격합니다.")]
    public bool requireFacingToAttack = true;

    [Tooltip("목표 방향과 조준 방향의 허용 각도(도)입니다. 이 안이면 조준 완료로 봅니다.")]
    public float aimAngleTolerance = 8f;

    [Tooltip("조준 기준 트랜스폼입니다. 비워두면 이 오브젝트(유닛) 또는 포탑 AI가 지정한 피벗을 사용합니다.")]
    public Transform aimTransform;

    [Tooltip("aimTransform forward에 더하는 Yaw 보정(도)입니다. 이동 유닛용입니다. 포탑은 aimAxisLocal을 씁니다.")]
    public float aimYawOffset;

    [HideInInspector]
    public Vector3 aimAxisLocal;

    [Header("Visuals")]
    [Tooltip("공격 시 머즐 플래시·피격 이펙트를 표시합니다.")]
    public bool spawnVisualEffects = true;

    [Tooltip("머즐 플래시 / 투사체 색상입니다.")]
    public Color projectileColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Tooltip("피격 이펙트 색상입니다.")]
    public Color hitColor = new Color(1f, 0.5f, 0.2f, 1f);

    [Header("Effect Prefabs")]
    [Tooltip("공격 시 발사 위치에 재생할 머즐 플래시·파티클 프리팹입니다.")]
    public GameObject muzzleFlashPrefab;

    [Tooltip("피격 시 대상 위치에 재생할 히트 이펙트·파티클 프리팹입니다.")]
    public GameObject hitEffectPrefab;

    [Tooltip("원거리 공격 투사체 프리팹입니다.")]
    public GameObject projectilePrefab;

    private float cooldownTimer;
    private UnitAnimator unitAnimator;
    private UnitSound unitSound;
    private SelectableEntity selfEntity;
    private AttackRecoilFX recoilFX;

    private SelectableEntity pendingTarget;
    private EntityHealth pendingTargetHealth;
    private bool pendingAttackActive;

    public float AttackRange => attackRange;
    public bool IsReady => cooldownTimer <= 0f;
    public bool HasPendingAttack => pendingAttackActive;

    // 업그레이드 보너스가 반영된 현재 공격력입니다. (UI 표시용)
    public float EffectiveAttackDamage => GetEffectiveDamage();

    void Awake()
    {
        unitAnimator = GetComponent<UnitAnimator>();
        unitSound = GetComponent<UnitSound>();
        selfEntity = GetComponent<SelectableEntity>();

        recoilFX = GetComponent<AttackRecoilFX>();
        if (recoilFX == null)
            recoilFX = gameObject.AddComponent<AttackRecoilFX>();
    }

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    public bool IsInRange(SelectableEntity target)
    {
        return IsWithinHorizontalRange(target);
    }

    public bool CanEngage(SelectableEntity target)
    {
        return target != null;
    }

    public bool IsWithinHorizontalRange(SelectableEntity target)
    {
        if (target == null)
            return false;

        return GetHorizontalBoundsGap(target) <= attackRange;
    }

    // 디버그 표시용입니다. 사거리 판정에 쓰는 것과 같은 외곽 간격을 돌려줍니다.
    public float GetRangeGap(SelectableEntity target)
    {
        return target != null ? GetHorizontalBoundsGap(target) : float.PositiveInfinity;
    }

    float GetHorizontalBoundsGap(SelectableEntity target)
    {
        Bounds targetBounds = target.SelectionBounds;
        Bounds selfBounds = selfEntity != null
            ? selfEntity.SelectionBounds
            : new Bounds(transform.position, Vector3.one);

        Vector3 targetPoint = targetBounds.ClosestPoint(selfBounds.center);
        Vector3 selfPoint = selfBounds.ClosestPoint(targetPoint);
        targetPoint = targetBounds.ClosestPoint(selfPoint);

        Vector3 diff = targetPoint - selfPoint;
        diff.y = 0f;

        return diff.magnitude;
    }

    public bool TryAttack(SelectableEntity target, EntityHealth targetHealth)
    {
        if (BuildingConstructionGate.IsFeatureLockedOn(this))
            return false;

        if (targetHealth == null || !targetHealth.IsAlive)
            return false;

        if (!IsInRange(target))
            return false;

        // 쿨다운을 소모하기 전에 조준 여부를 먼저 검사한다.
        if (requireFacingToAttack && !IsAimedAt(target))
            return false;

        if (cooldownTimer > 0f)
            return false;

        cooldownTimer = GetEffectiveCooldown();

        if (ShouldUseAttackAnimationEvent())
        {
            pendingTarget = target;
            pendingTargetHealth = targetHealth;
            pendingAttackActive = true;

            if (unitAnimator != null)
                unitAnimator.PlayAttack();

            return true;
        }

        ApplyAttackImpact(target, targetHealth);
        return true;
    }

    // 현재 조준 방향이 대상을 허용 각도 안으로 바라보는지 판정합니다.
    public bool IsAimedAt(SelectableEntity target)
    {
        if (target == null)
            return false;

        Transform pivot = aimTransform != null ? aimTransform : transform;

        // 각도를 잴 수 없는 퇴화 상황(대상이 피벗과 같은 자리)에서는 통과시킵니다.
        // 여기서 막으면 공격이 영영 안 나가는 쪽으로 실패합니다.
        if (!TryGetAimYawDelta(pivot, target, out float yawDelta))
            return true;

        return Mathf.Abs(yawDelta) <= Mathf.Max(0.1f, aimAngleTolerance);
    }

    /// <summary>
    /// 포신 선이 대상을 지나려면 pivot이 Y축으로 얼마나 더 돌아야 하는지(도)를 구합니다.
    /// 회전이 일어나는 축은 pivot이므로 각도도 pivot 기준으로 재야 합니다.
    /// firePoint를 원점으로 잡으면 포탑이 돌 때 firePoint도 pivot 주위를 함께 돌아,
    /// 대상이 가까울수록 오차가 증폭되고(이득 r/(r-d)) 좌우로 떨리게 됩니다.
    /// </summary>
    public bool TryGetAimYawDelta(Transform pivot, SelectableEntity target, out float yawDelta)
    {
        yawDelta = 0f;

        if (pivot == null || target == null)
            return false;

        Vector3 toTarget = target.SelectionBounds.center - pivot.position;
        toTarget.y = 0f;

        Vector3 aimDir = GetAimWorldDirection(pivot);
        aimDir.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f || aimDir.sqrMagnitude < 0.0001f)
            return false;

        aimDir.Normalize();

        // 포신 축에서 옆으로 벗어난 firePoint는 아무리 돌려도 그 어긋남(시차)이 남습니다.
        // 그만큼 미리 틀어 두면 포신 선이 대상을 정확히 지납니다.
        // firePoint가 포신 축 위에 있으면 0이 되어, 포신 길이는 결과에 영향을 주지 않습니다.
        float lateral = GetFirePointLateralOffset(pivot, aimDir);
        float distance = toTarget.magnitude;
        float parallax = Mathf.Asin(Mathf.Clamp(lateral / distance, -1f, 1f)) * Mathf.Rad2Deg;

        yawDelta = Vector3.SignedAngle(aimDir, toTarget, Vector3.up) - parallax;
        return true;
    }

    // 조준 방향 기준으로 firePoint가 오른쪽으로 얼마나 치우쳐 있는지입니다.
    float GetFirePointLateralOffset(Transform pivot, Vector3 aimDir)
    {
        if (firePoint == null)
            return 0f;

        Vector3 offset = firePoint.position - pivot.position;
        offset.y = 0f;

        return Vector3.Dot(Vector3.Cross(Vector3.up, aimDir), offset);
    }

    Vector3 GetAimWorldDirection(Transform pivot)
    {
        if (aimAxisLocal.sqrMagnitude > 0.0001f)
            return pivot.TransformDirection(aimAxisLocal);

        Vector3 aimDir = pivot.forward;
        if (Mathf.Abs(aimYawOffset) > 0.01f)
            aimDir = Quaternion.Euler(0f, aimYawOffset, 0f) * aimDir;
        return aimDir;
    }

    public void ApplyAttackImpact()
    {
        if (!pendingAttackActive)
            return;

        ApplyAttackImpact(pendingTarget, pendingTargetHealth);
        ClearPendingAttack();
    }

    public void CancelPendingAttack()
    {
        ClearPendingAttack();
    }

    bool ShouldUseAttackAnimationEvent()
    {
        return useAttackAnimationEvent && unitAnimator != null;
    }

    void ApplyAttackImpact(SelectableEntity target, EntityHealth targetHealth)
    {
        if (target == null || targetHealth == null || !targetHealth.IsAlive)
            return;

        if (!IsInRange(target))
            return;

        Vector3 firePosition = firePoint != null
            ? firePoint.position
            : transform.position;

        Vector3 targetPoint = target.SelectionBounds.center;
        Quaternion fireRotation =
            CombatEffectSpawner.GetFlatLookRotation(firePosition, targetPoint);

        if (attackType == AttackType.Ranged || attackType == AttackType.Flamethrower || attackType == AttackType.Cannon)
        {
            bool piercing = attackType == AttackType.Flamethrower;
            bool arcing = attackType == AttackType.Cannon;

            // 스플래시 범위가 기준 반경보다 크면 히트 이펙트도 함께 커지고, 작으면 함께 작아집니다.
            float hitEffectScale = (arcing && hitEffectBaseRadius > 0f)
                ? splashRadius / hitEffectBaseRadius
                : 1f;

            AttackVisuals.SpawnProjectile(
                firePosition,
                fireRotation,
                target,
                targetHealth,
                GetEffectiveDamage(),
                projectileSpeed,
                projectilePrefab,
                hitEffectPrefab,
                projectileColor,
                hitColor,
                selfEntity,
                piercing,
                attackRange,
                pierceHitRadius,
                arcing,
                arcHeight,
                arcHeightRatio,
                minArcHeight,
                splashRadius,
                splashMinDamageRatio,
                hitEffectScale);
        }
        else
        {
            targetHealth.TakeDamage(GetEffectiveDamage(), selfEntity);

            if (spawnVisualEffects)
            {
                AttackVisuals.SpawnHitEffect(
                    targetPoint,
                    targetPoint - firePosition,
                    hitEffectPrefab,
                    hitColor);
            }
        }

        if (spawnVisualEffects)
        {
            AttackVisuals.SpawnMuzzleFlash(
                firePosition,
                fireRotation,
                muzzleFlashPrefab,
                projectileColor);
        }

        recoilFX?.Play();

        if (!ShouldUseAttackAnimationEvent() && unitAnimator != null)
            unitAnimator.PlayAttack();

        PlayAttackSound();
    }

    // 업그레이드 보너스를 반영한 실제 공격력입니다.
    float GetEffectiveDamage()
    {
        if (UpgradeManager.Instance == null || selfEntity == null)
            return attackDamage;

        return UpgradeManager.Instance.GetModifiedAttackDamage(
            selfEntity.entityType,
            selfEntity.ownerId,
            attackDamage);
    }

    // 업그레이드 공격속도 보너스를 반영한 실제 공격 쿨다운입니다.
    // (공격속도 업그레이드는 유닛만 대상)
    float GetEffectiveCooldown()
    {
        if (UpgradeManager.Instance == null || selfEntity == null)
            return attackCooldown;

        return UpgradeManager.Instance.GetModifiedAttackCooldown(
            selfEntity.entityType,
            selfEntity.ownerId,
            attackCooldown);
    }

    void PlayAttackSound()
    {
        if (unitSound == null)
            unitSound = GetComponent<UnitSound>();

        if (unitSound != null)
            unitSound.PlayAttack();
    }

    void ClearPendingAttack()
    {
        pendingAttackActive = false;
        pendingTarget = null;
        pendingTargetHealth = null;
    }

    // Scene 뷰에서 사거리·범위 피해 크기를 감으로 넣지 않고 눈으로 보면서 조정할 수 있도록,
    // 선택 시 사거리(노랑)와 공격 타입별 범위(대포: 착탄 예상 지점의 빨강, 화염방사기: 발사 지점의 주황)를 그립니다.
    void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;

        Gizmos.color = new Color(1f, 0.92f, 0.16f, 0.4f);
        Gizmos.DrawWireSphere(origin, attackRange);

        if (attackType != AttackType.Cannon && attackType != AttackType.Flamethrower)
            return;

        Transform pivot = aimTransform != null ? aimTransform : transform;
        Vector3 aimOrigin = firePoint != null ? firePoint.position : origin;
        Vector3 aimDir = GetAimWorldDirection(pivot);

        if (attackType == AttackType.Cannon)
        {
            // 실제 착탄 지점은 타겟 위치에 달려 있으므로, 최대 사거리 지점을 기준으로
            // 범위 피해 반경의 상대적인 크기만 참고용으로 보여줍니다.
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(aimOrigin + aimDir * attackRange, splashRadius);
        }
        else
        {
            Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.5f);
            Gizmos.DrawWireSphere(aimOrigin, pierceHitRadius);
        }
    }
}
