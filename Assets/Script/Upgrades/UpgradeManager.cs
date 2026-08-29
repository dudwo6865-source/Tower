using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 게임 내 한정 업그레이드를 관리하는 싱글턴입니다.
// - 마석으로 업그레이드 레벨을 구매합니다.
// - 구매 후 researchDuration 동안 연구되며, 완료 시 적용됩니다.
// - 스타크래프트식으로 플레이어의 모든 유닛/건물에 글로벌 적용됩니다.
[DisallowMultipleComponent]
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("Owner")]
    [Tooltip("업그레이드 혜택을 받는 플레이어 ID입니다.")]
    public int playerOwnerId = 1;

    [Header("Upgrades")]
    [Tooltip("이 게임에서 구매할 수 있는 업그레이드 목록입니다.")]
    public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();

    [Tooltip("비워두면 씬에서 ManaStoneManager를 자동으로 찾습니다.")]
    public ManaStoneManager manaStoneManager;

    readonly Dictionary<UpgradeDefinition, int> levels = new Dictionary<UpgradeDefinition, int>();
    readonly Dictionary<NavMeshAgent, float> baseSpeeds = new Dictionary<NavMeshAgent, float>();

    UpgradeDefinition activeResearch;
    float researchElapsed;
    float researchDuration;

    // 레벨/구매/연구 상태가 바뀌면 발생합니다. (UI 갱신용)
    public event Action OnUpgradesChanged;

    public bool IsResearching => activeResearch != null;
    public UpgradeDefinition ActiveResearch => activeResearch;

    // 현재 연구 진행률(0~1). 연구 중이 아니면 0.
    public float ResearchProgress
    {
        get
        {
            if (activeResearch == null || researchDuration <= 0f)
                return 0f;

            return Mathf.Clamp01(researchElapsed / researchDuration);
        }
    }

    // 현재 연구 남은 시간(초).
    public float ResearchRemainingTime
    {
        get
        {
            if (activeResearch == null)
                return 0f;

            return Mathf.Max(0f, researchDuration - researchElapsed);
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (manaStoneManager == null)
            manaStoneManager = ManaStoneManager.Instance;

        if (manaStoneManager == null)
            manaStoneManager = FindObjectOfType<ManaStoneManager>();

        ApplyToAllEntities();
    }

    void Update()
    {
        if (activeResearch == null)
            return;

        researchElapsed += Time.deltaTime;

        if (researchElapsed < researchDuration)
            return;

        CompleteActiveResearch();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int GetLevel(UpgradeDefinition def)
    {
        if (def == null)
            return 0;

        return levels.TryGetValue(def, out int level) ? level : 0;
    }

    public bool IsMaxLevel(UpgradeDefinition def)
    {
        return def != null && GetLevel(def) >= def.maxLevel;
    }

    public int GetNextCost(UpgradeDefinition def)
    {
        if (def == null || IsMaxLevel(def))
            return -1;

        return def.GetCostForNextLevel(GetLevel(def));
    }

    public bool CanPurchase(UpgradeDefinition def)
    {
        if (def == null || IsMaxLevel(def) || IsResearching)
            return false;

        int cost = GetNextCost(def);
        ManaStoneManager mgr = ResolveManaStone();

        return mgr != null && mgr.CanAfford(cost);
    }

    // 마석을 소모하고 연구를 시작합니다. researchDuration이 0이면 즉시 완료합니다.
    public bool TryPurchase(UpgradeDefinition def)
    {
        if (!CanPurchase(def))
            return false;

        int cost = GetNextCost(def);
        ManaStoneManager mgr = ResolveManaStone();

        if (mgr == null || !mgr.TrySpend(cost))
            return false;

        float duration = def.GetResearchDurationForNextLevel(GetLevel(def));

        if (duration <= 0f)
        {
            levels[def] = GetLevel(def) + 1;
            ApplyToAllEntities();
            OnUpgradesChanged?.Invoke();
            return true;
        }

        activeResearch = def;
        researchElapsed = 0f;
        researchDuration = duration;
        OnUpgradesChanged?.Invoke();
        return true;
    }

    void CompleteActiveResearch()
    {
        UpgradeDefinition def = activeResearch;
        activeResearch = null;
        researchElapsed = 0f;
        researchDuration = 0f;

        if (def == null)
            return;

        levels[def] = GetLevel(def) + 1;
        ApplyToAllEntities();
        OnUpgradesChanged?.Invoke();
    }

    public float GetFlatBonus(UpgradeStat stat)
    {
        float total = 0f;

        foreach (UpgradeDefinition def in upgrades)
        {
            if (def == null || def.stat != stat || def.valueMode != UpgradeValueMode.Flat)
                continue;

            total += GetLevel(def) * def.bonusPerLevel;
        }

        return total;
    }

    public float GetPercentBonus(UpgradeStat stat)
    {
        float totalPercent = 0f;

        foreach (UpgradeDefinition def in upgrades)
        {
            if (def == null || def.stat != stat || def.valueMode != UpgradeValueMode.Percent)
                continue;

            totalPercent += GetLevel(def) * def.bonusPerLevel;
        }

        return totalPercent / 100f;
    }

    public float ApplyBonus(UpgradeStat stat, float baseValue)
    {
        return baseValue * (1f + GetPercentBonus(stat)) + GetFlatBonus(stat);
    }

    public float GetModifiedValue(UpgradeStat stat, int ownerId, float baseValue)
    {
        if (ownerId != playerOwnerId)
            return baseValue;

        return ApplyBonus(stat, baseValue);
    }

    public float GetModifiedAttackDamage(SelectableEntityType entityType, int ownerId, float baseDamage)
    {
        UpgradeStat stat = entityType == SelectableEntityType.Building
            ? UpgradeStat.BuildingAttackDamage
            : UpgradeStat.UnitAttackDamage;

        return GetModifiedValue(stat, ownerId, baseDamage);
    }

    public float GetModifiedAttackCooldown(SelectableEntityType entityType, int ownerId, float baseCooldown)
    {
        if (ownerId != playerOwnerId ||
            entityType == SelectableEntityType.Building ||
            baseCooldown <= 0f)
            return baseCooldown;

        float baseRate = 1f / baseCooldown;
        float modifiedRate = ApplyBonus(UpgradeStat.UnitAttackSpeed, baseRate);

        if (modifiedRate <= 0.0001f)
            return baseCooldown;

        return 1f / modifiedRate;
    }

    public int GetModifiedSpawnCount(int ownerId, int baseCount)
    {
        if (ownerId != playerOwnerId)
            return baseCount;

        return Mathf.Max(0, Mathf.RoundToInt(ApplyBonus(UpgradeStat.BuildingSpawnCount, baseCount)));
    }

    public static void NotifySpawned(SelectableEntity entity)
    {
        if (Instance != null)
            Instance.ApplyToEntity(entity);
    }

    public void ApplyToAllEntities()
    {
        IReadOnlyList<SelectableEntity> entities = SelectableRegistry.Entities;

        for (int i = 0; i < entities.Count; i++)
            ApplyToEntity(entities[i]);
    }

    public void ApplyToEntity(SelectableEntity entity)
    {
        if (entity == null || entity.ownerId != playerOwnerId)
            return;

        bool isBuilding = entity.entityType == SelectableEntityType.Building;

        EntityHealth health = entity.GetComponent<EntityHealth>();

        if (health != null)
        {
            UpgradeStat healthStat = isBuilding
                ? UpgradeStat.BuildingMaxHealth
                : UpgradeStat.UnitMaxHealth;

            float baseMax = health.maxHealth;
            float bonus = ApplyBonus(healthStat, baseMax) - baseMax;
            health.SetUpgradeMaxHealthBonus(bonus);
        }

        if (!isBuilding)
            ApplyMoveSpeed(entity);
    }

    void ApplyMoveSpeed(SelectableEntity entity)
    {
        NavMeshAgent agent = entity.GetComponent<NavMeshAgent>();

        if (agent == null)
            return;

        if (!baseSpeeds.TryGetValue(agent, out float baseSpeed))
        {
            baseSpeed = agent.speed;
            baseSpeeds[agent] = baseSpeed;
        }

        agent.speed = ApplyBonus(UpgradeStat.UnitMoveSpeed, baseSpeed);
    }

    ManaStoneManager ResolveManaStone()
    {
        if (manaStoneManager == null)
            manaStoneManager = ManaStoneManager.Instance;

        return manaStoneManager;
    }
}
