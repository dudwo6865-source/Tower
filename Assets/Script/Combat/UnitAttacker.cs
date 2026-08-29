using UnityEngine;

public enum AttackType
{
    Melee,
    Ranged
}

[DisallowMultipleComponent]
public class UnitAttacker : MonoBehaviour
{
    [Header("Attack")]
    [Tooltip("근접은 즉시 피해(투사체 없음), 원거리는 투사체 발사. 사거리 규칙은 동일합니다.")]
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
    [Tooltip("투사체 속도입니다. 원거리 공격일 때만 사용됩니다.")]
    public float projectileSpeed = 25f;

    [Tooltip("투사체가 발사될 위치 오브젝트입니다. 비워두면 이 오브젝트의 위치에서 발사합니다.")]
    public Transform firePoint;

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
        Vector3 origin = firePoint != null ? firePoint.position : pivot.position;
        Vector3 toTarget = target.SelectionBounds.center - origin;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
            return true;

        Vector3 aimDir = GetAimWorldDirection(pivot);
        aimDir.y = 0f;
        if (aimDir.sqrMagnitude < 0.0001f)
            return false;

        float angle = Vector3.Angle(aimDir.normalized, toTarget.normalized);
        return angle <= Mathf.Max(0.1f, aimAngleTolerance);
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

        if (attackType == AttackType.Ranged)
        {
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
                selfEntity);
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
}
