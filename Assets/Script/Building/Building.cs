using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SelectableEntity))]
public class Building : MonoBehaviour
{
    [Tooltip("건물 종류별 스탯 데이터입니다. 할당하면 시작 시 각 컴포넌트에 값을 적용합니다.")]
    public UnitData data;

    [Header("Building")]
    [Tooltip("본부(HQ) 건물이면 Headquarters 컴포넌트를 사용합니다.")]
    public bool isHeadquarters;

    [Tooltip("공격 건물(타워)이면 TowerAI를 사용합니다. UnitCombatAI 대신 포탑 회전용 AI입니다.")]
    public bool useTowerAI = true;

    [Tooltip("일정 간격으로 유닛을 자동 생산하는 건물입니다.")]
    public bool isProductionBuilding;

    [Tooltip("생산 규칙입니다. 비워두면 ProductionBuilding 컴포넌트 값을 사용합니다.")]
    public ProductionRecipe productionRecipe;

    private SelectableEntity selection;
    private EntityHealth health;
    private UnitAttacker attacker;
    private TowerAI towerAI;
    private WorldHealthBar healthBar;
    private UnitSound unitSound;
    private Headquarters headquarters;
    private ProductionBuilding productionBuilding;

    public SelectableEntity Selection => Resolve(ref selection);
    public EntityHealth Health => Resolve(ref health);
    public UnitAttacker Attacker => Resolve(ref attacker);
    public TowerAI TowerAI => Resolve(ref towerAI);
    public WorldHealthBar HealthBar => Resolve(ref healthBar);
    public Headquarters Headquarters => Resolve(ref headquarters);
    public ProductionBuilding ProductionBuilding => Resolve(ref productionBuilding);

    void Awake()
    {
        if (data != null)
            ApplyData(data);
    }

    T Resolve<T>(ref T cached) where T : Component
    {
        if (cached == null)
            cached = GetComponent<T>();

        return cached;
    }

    public void ApplyData(UnitData source)
    {
        if (source == null)
            return;

        ApplySelection(source);
        ApplyHealth(source);
        ApplyDeathEffects(source);
        ApplyAttacker(source);
        ApplyTowerAI(source);
        ApplyHealthBar(source);
        ApplyFogOfWar(source);
        ApplyGrid(source);
        ApplySound(source);
        ApplyHeadquarters(source);
        ApplyProductionBuilding(source);
    }

    void ApplySelection(UnitData source)
    {
        SelectableEntity target = Selection;
        if (target == null)
            return;

        target.entityType = SelectableEntityType.Building;
        target.ownerId = source.ownerId;
        target.entityTypeId = source.entityTypeId;
    }

    void ApplyHealth(UnitData source)
    {
        EntityHealth target = Health;
        if (target == null)
            return;

        target.maxHealth = source.maxHealth;
        target.deathAnimationDuration = source.deathAnimationDuration;
        target.deathSinkDistance = source.deathSinkDistance;
        target.deathEffectColor = source.deathEffectColor;
    }

    void ApplyDeathEffects(UnitData source)
    {
        EntityHealth target = Health;
        if (target == null)
            return;

        target.deathEffectPrefab = source.combatEffects.deathEffectPrefab;
    }

    void ApplyAttacker(UnitData source)
    {
        UnitAttacker target = Attacker;
        if (target == null)
            return;

        target.attackType = source.attackType;
        target.attackDamage = source.attackDamage;
        target.attackRange = source.attackRange;
        target.attackCooldown = source.attackCooldown;
        target.useAttackAnimationEvent = source.useAttackAnimationEvent;
        target.projectileSpeed = source.projectileSpeed;
        target.projectileColor = source.projectileColor;
        target.hitColor = source.hitColor;
        target.muzzleFlashPrefab = source.combatEffects.muzzleFlashPrefab;
        target.hitEffectPrefab = source.combatEffects.hitEffectPrefab;
        target.projectilePrefab = source.combatEffects.projectilePrefab;
    }

    void ApplyTowerAI(UnitData source)
    {
        TowerAI target = TowerAI;
        if (target == null)
            return;

        target.aggroRange = source.aggroRange;
        target.targetPriority = source.targetPriority;
        target.retargetInterval = source.retargetInterval;
        target.facingSpeed = source.facingSpeed;
    }

    void ApplyHealthBar(UnitData source)
    {
        WorldHealthBar target = HealthBar;
        if (target == null)
            return;

        target.heightOffset = source.healthBarHeightOffset;
    }

    void ApplyFogOfWar(UnitData source)
    {
        FogOfWarVisionSource vision = GetComponent<FogOfWarVisionSource>();
        if (vision == null)
            return;

        if (source.visionRange > 0f)
            vision.visionRange = source.visionRange;
    }

    void ApplyGrid(UnitData source)
    {
        if (source.footprintCells.x <= 0 || source.footprintCells.y <= 0)
            return;

        GridFootprint footprint = GetComponent<GridFootprint>();

        if (footprint == null)
            footprint = gameObject.AddComponent<GridFootprint>();

        footprint.footprintCells = source.footprintCells;
        footprint.blockCells = true;
    }

    void ApplySound(UnitData source)
    {
        bool hasClips =
            HasClips(source.attackSoundClips) ||
            HasClips(source.hitSoundClips) ||
            HasClips(source.deathSoundClips);

        if (!hasClips && source.soundVolume <= 0f)
            return;

        UnitSound target = Resolve(ref unitSound);

        if (target == null)
            target = gameObject.AddComponent<UnitSound>();

        target.ApplyClips(
            source.attackSoundClips,
            source.hitSoundClips,
            source.deathSoundClips,
            source.soundVolume > 0f ? source.soundVolume : -1f);
    }

    void ApplyHeadquarters(UnitData source)
    {
        if (!isHeadquarters)
            return;

        Headquarters target = Headquarters;
        if (target == null)
            return;

        target.ownerId = source.ownerId;
    }

    void ApplyProductionBuilding(UnitData source)
    {
        if (!isProductionBuilding)
            return;

        ProductionBuilding target = ProductionBuilding;
        if (target == null)
            return;

        if (productionRecipe != null)
            target.SetRecipe(productionRecipe);
    }

    static bool HasClips(AudioClip[] clips)
    {
        return clips != null && clips.Length > 0;
    }

#if UNITY_EDITOR
    public void EnsureComponents(
        bool wantsAttacker,
        bool wantsTowerAI,
        bool wantsHeadquarters,
        bool wantsProduction)
    {
        GetOrAdd<SelectableEntity>();
        GetOrAdd<EntityHealth>();
        GetOrAdd<WorldHealthBar>();
        GetOrAdd<FogOfWarVisionSource>();
        GetOrAdd<FogOfWarVisibility>();
        GetOrAdd<GridFootprint>();
        GetOrAdd<UnitSound>();

        if (wantsProduction)
            GetOrAdd<ProductionBuilding>();
        else
            RemoveIfPresent<ProductionBuilding>();

        if (wantsAttacker)
            GetOrAdd<UnitAttacker>();
        else
            RemoveIfPresent<UnitAttacker>();

        if (wantsAttacker)
            GetOrAdd<UnitAnimator>();
        else
            RemoveIfPresent<UnitAnimator>();

        if (wantsTowerAI && wantsAttacker)
            GetOrAdd<TowerAI>();
        else
            RemoveIfPresent<TowerAI>();

        if (wantsHeadquarters)
            GetOrAdd<Headquarters>();
        else
            RemoveIfPresent<Headquarters>();

        RemoveIfPresent<Unit>();
        RemoveIfPresent<UnitCombatAI>();
        RemoveIfPresent<UnitMovement>();
        RemoveIfPresent<NavMeshAgent>();

        selection = null;
        health = null;
        attacker = null;
        towerAI = null;
        healthBar = null;
        unitSound = null;
        headquarters = null;
        productionBuilding = null;
    }

    public void EnsureComponentsFromData()
    {
        bool wantsProduction = isProductionBuilding;
        bool wantsAttacker = !wantsProduction && data != null && data.canAttack;
        bool wantsTowerAI = wantsAttacker && useTowerAI;
        bool wantsHeadquarters = isHeadquarters;

        EnsureComponents(wantsAttacker, wantsTowerAI, wantsHeadquarters, wantsProduction);
    }

    T GetOrAdd<T>() where T : Component
    {
        T component = GetComponent<T>();

        if (component == null)
            component = gameObject.AddComponent<T>();

        return component;
    }

    void RemoveIfPresent<T>() where T : Component
    {
        T component = GetComponent<T>();

        if (component != null)
            UnityEngine.Object.DestroyImmediate(component);
    }
#endif
}
