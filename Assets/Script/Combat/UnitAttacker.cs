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
        if (targetHealth == null || !targetHealth.IsAlive)
            return false;

        if (!IsInRange(target))
            return false;

        if (cooldownTimer > 0f)
            return false;

        cooldownTimer = attackCooldown;

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
                attackDamage,
                projectileSpeed,
                projectilePrefab,
                hitEffectPrefab,
                projectileColor,
                hitColor,
                selfEntity);
        }
        else
        {
            targetHealth.TakeDamage(attackDamage, selfEntity);

            if (spawnVisualEffects)
            {
                AttackVisuals.SpawnHitEffect(
                    targetPoint,
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
