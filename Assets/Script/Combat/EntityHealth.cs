using System;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(SelectableEntity))]
public class EntityHealth : MonoBehaviour
{
    [Header("체력")]
    [Label("최대 체력")]
    [Tooltip("이 엔티티의 최대 체력입니다.")]
    public float maxHealth = 100f;

    [Header("사망")]
    [Label("사망 이펙트 프리팹")]
    [Tooltip("사망·파괴 시 재생할 이펙트·파티클 프리팹입니다. 비어 있으면 이펙트 없이 바로 사라집니다.")]
    public GameObject deathEffectPrefab;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;
    // 실제 피해를 입었을 때 발생합니다. (피해량, 공격자)
    public event Action<float, SelectableEntity> OnDamaged;
    // 어떤 엔티티든 사망하면 발생합니다. (게임 통계용)
    public static event Action<EntityHealth> OnAnyDied;

    public SelectableEntity Entity => selectableEntity;

    public float CurrentHealth { get; private set; }
    // 최대 체력 = 기본값 + 업그레이드 보너스입니다.
    public float MaxHealth => Mathf.Max(1f, maxHealth + upgradeMaxHealthBonus);

    public bool IsAlive => CurrentHealth > 0f;

    private SelectableEntity selectableEntity;
    private bool isDying;
    private float upgradeMaxHealthBonus;

    void Awake()
    {
        selectableEntity = GetComponent<SelectableEntity>();
        CurrentHealth = maxHealth;

        if (selectableEntity.entityType == SelectableEntityType.Building)
            BuildingRegistry.Register(selectableEntity);
    }

    void Start()
    {
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    void OnDestroy()
    {
        if (selectableEntity != null &&
            selectableEntity.entityType == SelectableEntityType.Building)
            BuildingRegistry.NotifyRemoved(selectableEntity);
    }

    public void TakeDamage(float damage, SelectableEntity attacker = null)
    {
        if (!IsAlive)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        OnDamaged?.Invoke(damage, attacker);

        NotifyAttacked(attacker);

        if (CurrentHealth <= 0f)
            Die();
    }

    // 업그레이드로 인한 추가 최대 체력을 설정합니다.
    // 보너스가 늘어나면 그만큼 현재 체력도 함께 회복시켜 즉시 체감되게 합니다.
    public void SetUpgradeMaxHealthBonus(float bonus)
    {
        bonus = Mathf.Max(0f, bonus);

        if (Mathf.Approximately(bonus, upgradeMaxHealthBonus))
            return;

        float delta = bonus - upgradeMaxHealthBonus;
        upgradeMaxHealthBonus = bonus;

        if (delta > 0f && IsAlive)
            CurrentHealth += delta;

        CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    void NotifyAttacked(SelectableEntity attacker)
    {
        if (attacker == null)
            return;

        CombatAIBase combatAI = GetComponent<CombatAIBase>();

        if (combatAI != null)
            combatAI.NotifyAttackedBy(attacker);

        RallyAlliesIfBuildingAttacked(attacker, combatAI);
    }

    void RallyAlliesIfBuildingAttacked(SelectableEntity attacker, CombatAIBase combatAI)
    {
        if (selectableEntity == null ||
            selectableEntity.entityType != SelectableEntityType.Building)
        {
            return;
        }

        if (attacker.ownerId == selectableEntity.ownerId)
            return;

        float range = combatAI != null ? combatAI.aggroRange : 30f;

        MobileCombatAI.RallyAlliesAround(
            transform.position,
            selectableEntity.ownerId,
            selectableEntity,
            attacker,
            range);
    }

    public void SetMaxHealth(float value, bool refill = true)
    {
        maxHealth = Mathf.Max(1f, value);

        if (refill || CurrentHealth > MaxHealth)
            CurrentHealth = MaxHealth;

        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    // 돌연변이로 최대 체력이 바뀌어도 남아 있는 체력 비율과 사망 상태를 유지한다.
    public void ScaleMaxHealthPreservingRatio(float multiplier)
    {
        if (!IsAlive || multiplier <= 0f)
            return;
        float ratio = CurrentHealth / MaxHealth;
        maxHealth *= multiplier;
        upgradeMaxHealthBonus *= multiplier;
        CurrentHealth = MaxHealth * ratio;
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void Heal(float amount)
    {
        if (!IsAlive)
            return;

        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    void Die()
    {
        if (isDying)
            return;

        isDying = true;
        OnDied?.Invoke();
        OnAnyDied?.Invoke(this);

        if (selectableEntity.entityType == SelectableEntityType.Building)
            BuildingRegistry.NotifyRemoved(selectableEntity);

        DisableGameplayComponents();

        if (deathEffectPrefab != null)
        {
            CombatEffectSpawner.Spawn(
                deathEffectPrefab,
                selectableEntity.SelectionBounds.center,
                Quaternion.identity);
        }

        Destroy(gameObject);
    }

    void DisableGameplayComponents()
    {
        Collider collider = selectableEntity.SelectionCollider;
        if (collider != null)
            collider.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;

        CombatAIBase combatAI = GetComponent<CombatAIBase>();
        if (combatAI != null)
            combatAI.enabled = false;

        UnitAttacker attacker = GetComponent<UnitAttacker>();
        if (attacker != null)
            attacker.enabled = false;

        UnitMovement movement = GetComponent<UnitMovement>();
        if (movement != null)
            movement.enabled = false;

        NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle != null)
            obstacle.enabled = false;
    }
}
