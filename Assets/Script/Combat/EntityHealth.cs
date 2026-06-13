using System;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(SelectableEntity))]
public class EntityHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [Tooltip("이 엔티티의 최대 체력입니다.")]
    public float maxHealth = 100f;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsAlive => CurrentHealth > 0f;

    private SelectableEntity selectableEntity;

    void Awake()
    {
        selectableEntity = GetComponent<SelectableEntity>();
        CurrentHealth = maxHealth;

        if (selectableEntity.entityType == SelectableEntityType.Building)
            BuildingRegistry.Register(selectableEntity);
    }

    void Start()
    {
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    void OnDestroy()
    {
        if (selectableEntity != null &&
            selectableEntity.entityType == SelectableEntityType.Building)
            BuildingRegistry.NotifyRemoved(selectableEntity);
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (!IsAlive)
            return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    void Die()
    {
        OnDied?.Invoke();
        Destroy(gameObject);
    }
}
