using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(SelectableEntity))]
public class EntityHealth : MonoBehaviour
{
    [Header("Health")]
    [Tooltip("이 엔티티의 최대 체력입니다.")]
    public float maxHealth = 100f;

    [Header("Death")]
    [Tooltip("사망 시 가라앉으며 사라지는 연출 시간(초)입니다. 0이면 즉시 제거합니다.")]
    public float deathAnimationDuration = 1f;

    [Tooltip("사망 연출 동안 아래로 가라앉는 거리입니다.")]
    public float deathSinkDistance = 1.5f;

    [Tooltip("사망 시 표시할 이펙트 색상입니다. deathEffectPrefab이 없을 때만 사용됩니다.")]
    public Color deathEffectColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Tooltip("사망·파괴 시 재생할 이펙트·파티클 프리팹입니다.")]
    public GameObject deathEffectPrefab;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsAlive => CurrentHealth > 0f;

    private SelectableEntity selectableEntity;
    private bool isDying;

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

    public void TakeDamage(float damage, SelectableEntity attacker = null)
    {
        if (!IsAlive)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        NotifyAttacked(attacker);

        if (CurrentHealth <= 0f)
            Die();
    }

    void NotifyAttacked(SelectableEntity attacker)
    {
        if (attacker == null)
            return;

        CombatAIBase combatAI = GetComponent<CombatAIBase>();

        if (combatAI != null)
            combatAI.NotifyAttackedBy(attacker);
    }

    public void SetMaxHealth(float value, bool refill = true)
    {
        maxHealth = Mathf.Max(1f, value);

        if (refill || CurrentHealth > maxHealth)
            CurrentHealth = maxHealth;

        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
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
        if (isDying)
            return;

        isDying = true;
        OnDied?.Invoke();

        if (selectableEntity.entityType == SelectableEntityType.Building)
            BuildingRegistry.NotifyRemoved(selectableEntity);

        DisableGameplayComponents();

        Vector3 effectPosition = selectableEntity.SelectionBounds.center;

        if (deathEffectPrefab != null)
            CombatEffectSpawner.Spawn(deathEffectPrefab, effectPosition, Quaternion.identity);
        else
        {
            AttackVisuals.SpawnHitEffect(
                effectPosition,
                null,
                deathEffectColor);
        }

        if (deathAnimationDuration <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(DeathSequence());
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

    IEnumerator DeathSequence()
    {
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + Vector3.down * deathSinkDistance;
        Vector3 startScale = transform.localScale;

        float elapsed = 0f;

        while (elapsed < deathAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / deathAnimationDuration);

            transform.position =
                Vector3.Lerp(startPosition, endPosition, t);

            transform.localScale =
                Vector3.Lerp(startScale, startScale * 0.4f, t);

            yield return null;
        }

        Destroy(gameObject);
    }
}
