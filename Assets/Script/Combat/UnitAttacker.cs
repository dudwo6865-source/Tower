using UnityEngine;

public enum AttackType
{
    Melee,
    Ranged
}

public class UnitAttacker : MonoBehaviour
{
    [Header("Attack")]
    [Tooltip("공격 방식입니다. 근접은 즉시 피해, 원거리는 투사체를 발사합니다.")]
    public AttackType attackType = AttackType.Melee;

    [Tooltip("한 번 공격할 때 주는 피해량입니다.")]
    public float attackDamage = 10f;

    [Tooltip("공격이 닿는 사거리입니다. 대상 표면까지의 거리로 판정합니다.")]
    public float attackRange = 2.5f;

    [Tooltip("공격 사이의 최소 간격(초)입니다.")]
    public float attackCooldown = 1f;

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

    private float cooldownTimer;
    private UnitAnimator unitAnimator;

    public float AttackRange => attackRange;
    public bool IsReady => cooldownTimer <= 0f;

    void Awake()
    {
        unitAnimator = GetComponent<UnitAnimator>();
    }

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    public bool IsInRange(SelectableEntity target)
    {
        if (target == null)
            return false;

        Vector3 closestPoint =
            target.SelectionBounds.ClosestPoint(transform.position);

        Vector3 diff = closestPoint - transform.position;
        diff.y = 0f;

        return diff.sqrMagnitude <= attackRange * attackRange;
    }

    public bool TryAttack(SelectableEntity target, EntityHealth targetHealth)
    {
        if (targetHealth == null || !targetHealth.IsAlive)
            return false;

        if (cooldownTimer > 0f)
            return false;

        cooldownTimer = attackCooldown;

        Vector3 firePosition = firePoint != null
            ? firePoint.position
            : transform.position;

        if (attackType == AttackType.Ranged)
        {
            AttackVisuals.SpawnProjectile(
                firePosition,
                target,
                targetHealth,
                attackDamage,
                projectileSpeed,
                projectileColor,
                hitColor);
        }
        else
        {
            targetHealth.TakeDamage(attackDamage);

            if (spawnVisualEffects)
                AttackVisuals.SpawnHitEffect(
                    target.SelectionBounds.center,
                    hitColor);
        }

        if (spawnVisualEffects)
            AttackVisuals.SpawnMuzzleFlash(firePosition, projectileColor);

        if (unitAnimator != null)
            unitAnimator.PlayAttack();

        return true;
    }
}
