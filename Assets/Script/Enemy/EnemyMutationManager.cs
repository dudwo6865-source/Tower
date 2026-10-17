using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyMutationManager : MonoBehaviour
{
    public static EnemyMutationManager Instance { get; private set; }

    [Label("돌연변이 설정")]
    public EnemyMutationSettings settings = new EnemyMutationSettings();
    [Label("결과 로그")]
    public bool logResults = true;

    public float SpawnBonus { get; private set; }
    public float DamageBonus { get; private set; }
    public float HealthBonus { get; private set; }
    public float SpeedBonus { get; private set; }
    // 방어력 돌연변이로 더해진 방어력(덧셈 값)입니다.
    public float ArmorBonus { get; private set; }
    public int MutationCount { get; private set; }
    public event Action<string> OnMutationApplied;
    public event Action<EnemyMutationType, float> OnMutationRolled;
    public event Action OnChanged;
    readonly HashSet<int> processedSpawners = new HashSet<int>();

    public static EnemyMutationManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        EnemyMutationManager existing = FindObjectOfType<EnemyMutationManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }
        return new GameObject("적 돌연변이 관리자").AddComponent<EnemyMutationManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Configure(EnemyMutationSettings value)
    {
        settings = value != null ? value.Clone() : new EnemyMutationSettings();
        SpawnBonus = DamageBonus = HealthBonus = SpeedBonus = ArmorBonus = 0f;
        MutationCount = 0;
        processedSpawners.Clear();
        foreach (EnemySpawner spawner in EnemySpawner.Active)
            if (spawner != null) spawner.ResetMutationSpawnBudget();
        // 같은 씬에서 맵을 다시 불러올 때 기존 개체의 보정도 원복한다.
        foreach (EnemyMutationTarget target in FindObjectsOfType<EnemyMutationTarget>(true))
            target.Apply(1f, 1f, 1f, 0f);
        OnChanged?.Invoke();
    }

    public float GetSpawnBonus(int ownerId) =>
        settings.enabled && ownerId == settings.enemyOwnerId ? SpawnBonus : 0f;

    public void NotifySpawnerDestroyed(EnemySpawner spawner)
    {
        if (spawner == null || !settings.enabled || spawner.enemyOwnerId != settings.enemyOwnerId ||
            (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver) ||
            !processedSpawners.Add(spawner.GetInstanceID())) return;

        float damageWeight = settings.damageIncrease > 0f ? Mathf.Max(0f, settings.damageWeight) : 0f;
        float healthWeight = settings.healthIncrease > 0f ? Mathf.Max(0f, settings.healthWeight) : 0f;
        float speedWeight = settings.speedIncrease > 0f ? Mathf.Max(0f, settings.speedWeight) : 0f;
        float armorWeight = settings.armorIncrease > 0f ? Mathf.Max(0f, settings.armorWeight) : 0f;
        float total = damageWeight + healthWeight + speedWeight + armorWeight;
        bool canSpawn = settings.spawnIncrease > 0f;
        if (!canSpawn && total <= 0f) return;

        EnemyMutationType type;
        float increase;
        if (canSpawn && (total <= 0f || UnityEngine.Random.value < Mathf.Clamp01(settings.spawnChance)))
        {
            type = EnemyMutationType.Spawn;
            increase = settings.spawnIncrease;
            SpawnBonus += increase;
        }
        else
        {
            // 가중치 구간을 차례로 확인한다. 부동소수점 오차로 끝까지 남으면 마지막으로 가중치가 있는 항목을 고른다.
            float roll = UnityEngine.Random.value * total;
            if (damageWeight > 0f && (roll < damageWeight || healthWeight + speedWeight + armorWeight <= 0f))
            {
                type = EnemyMutationType.Damage;
                increase = settings.damageIncrease;
                DamageBonus += increase;
            }
            else if (healthWeight > 0f && (roll < damageWeight + healthWeight || speedWeight + armorWeight <= 0f))
            {
                type = EnemyMutationType.Health;
                increase = settings.healthIncrease;
                HealthBonus += increase;
            }
            else if (speedWeight > 0f && (roll < damageWeight + healthWeight + speedWeight || armorWeight <= 0f))
            {
                type = EnemyMutationType.Speed;
                increase = settings.speedIncrease;
                SpeedBonus += increase;
            }
            else
            {
                type = EnemyMutationType.Armor;
                increase = settings.armorIncrease;
                ArmorBonus += increase;
            }
        }

        MutationCount++;
        // 파괴 시에만 순회한다. 안개에 숨은 적과 비활성 상태의 초기 배치 적도 포함한다.
        if (type != EnemyMutationType.Spawn)
            foreach (SelectableEntity entity in FindObjectsOfType<SelectableEntity>(true))
                ApplyTo(entity.gameObject);

        string amount = type == EnemyMutationType.Armor ? increase.ToString("0.#") : increase.ToString("P0");
        string message = $"돌연변이 발생: {GetName(type)} +{amount}";
        if (logResults) Debug.Log(message, this);
        EnemyMutationHUD.EnsureInstance();
        OnChanged?.Invoke();
        OnMutationApplied?.Invoke(message);
        OnMutationRolled?.Invoke(type, increase);
    }

    public void ApplyTo(GameObject enemy)
    {
        if (!settings.enabled || enemy == null ||
            (HealthBonus == 0f && DamageBonus == 0f && SpeedBonus == 0f && ArmorBonus == 0f)) return;
        SelectableEntity entity = enemy.GetComponent<SelectableEntity>();
        EntityHealth health = enemy.GetComponent<EntityHealth>();
        if (entity == null || entity.ownerId != settings.enemyOwnerId ||
            entity.entityType != SelectableEntityType.Unit || health == null || !health.IsAlive) return;
        EnemyMutationTarget target = enemy.GetComponent<EnemyMutationTarget>();
        if (target == null) target = enemy.AddComponent<EnemyMutationTarget>();
        target.Apply(1f + HealthBonus, 1f + DamageBonus, 1f + SpeedBonus, ArmorBonus);
    }

    public string Summary => $"돌연변이 {MutationCount}회\n밤 공격대 +{SpawnBonus:P0} · 공격력 +{DamageBonus:P0}\n체력 +{HealthBonus:P0} · 이동 속도 +{SpeedBonus:P0} · 방어력 +{ArmorBonus:0.#}";

    static string GetName(EnemyMutationType type)
    {
        switch (type)
        {
            case EnemyMutationType.Spawn: return "밤 공격대 수";
            case EnemyMutationType.Damage: return "적 공격력";
            case EnemyMutationType.Health: return "적 체력";
            case EnemyMutationType.Armor: return "적 방어력";
            default: return "적 이동 속도";
        }
    }
}
