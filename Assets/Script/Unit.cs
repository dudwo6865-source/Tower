using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SelectableEntity))]
public class Unit : MonoBehaviour
{
    [Tooltip("유닛 종류별 스탯 데이터입니다. 할당하면 시작 시 각 컴포넌트에 값을 적용합니다. 비워두면 각 컴포넌트의 인스펙터 값을 그대로 사용합니다.")]
    public UnitData data;

    private SelectableEntity selection;
    private EntityHealth health;
    private UnitAttacker attacker;
    private UnitCombatAI combatAI;
    private UnitMovement movement;
    private WorldHealthBar healthBar;
    private NavMeshAgent navAgent;
    private UnitSound unitSound;

    public SelectableEntity Selection => Resolve(ref selection);
    public EntityHealth Health => Resolve(ref health);
    public UnitAttacker Attacker => Resolve(ref attacker);
    public UnitCombatAI AI => Resolve(ref combatAI);
    public UnitMovement Movement => Resolve(ref movement);
    public WorldHealthBar HealthBar => Resolve(ref healthBar);
    public NavMeshAgent NavAgent => Resolve(ref navAgent);

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
        ApplyAttacker(source);
        ApplyCombatAI(source);
        ApplyMovement(source);
        ApplyHealthBar(source);
        ApplyFogOfWar(source);
        ApplyGrid(source);
        ApplySound(source);
    }

    void ApplyGrid(UnitData source)
    {
        if (source.footprintCells.x <= 0 || source.footprintCells.y <= 0)
            return;

        GridFootprint footprint = GetComponent<GridFootprint>();

        if (footprint == null)
            footprint = gameObject.AddComponent<GridFootprint>();

        footprint.footprintCells = source.footprintCells;
        footprint.blockCells = source.entityType == SelectableEntityType.Building;
    }

    void ApplyMovement(UnitData source)
    {
        NavMeshAgent target = NavAgent;
        if (target == null)
            return;

        if (source.moveSpeed > 0f)
            target.speed = source.moveSpeed;

        if (source.angularSpeed > 0f)
            target.angularSpeed = source.angularSpeed;

        if (source.acceleration > 0f)
            target.acceleration = source.acceleration;
    }

    void ApplySelection(UnitData source)
    {
        SelectableEntity target = Selection;
        if (target == null)
            return;

        target.entityType = source.entityType;
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
    }

    void ApplyCombatAI(UnitData source)
    {
        UnitCombatAI target = AI;
        if (target == null)
            return;

        target.aggroRange = source.aggroRange;
        target.targetPriority = source.targetPriority;
        target.advanceToEnemyBuildings = source.advanceToEnemyBuildings;
        target.stoppingDistance = source.stoppingDistance;
        target.retargetInterval = source.retargetInterval;
        target.destinationRefreshInterval = source.destinationRefreshInterval;
        target.facingSpeed = source.facingSpeed;
    }

    void ApplyHealthBar(UnitData source)
    {
        WorldHealthBar target = HealthBar;
        if (target == null)
            return;

        target.barWidth = source.healthBarWidth;
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

    static bool HasClips(AudioClip[] clips)
    {
        return clips != null && clips.Length > 0;
    }

#if UNITY_EDITOR
    public void EnsureComponents(bool wantsAttacker, bool wantsCombatAI, bool wantsMovement)
    {
        GetOrAdd<SelectableEntity>();
        GetOrAdd<EntityHealth>();
        GetOrAdd<WorldHealthBar>();
        GetOrAdd<FogOfWarVisionSource>();
        GetOrAdd<FogOfWarVisibility>();
        GetOrAdd<UnitSound>();

        if (wantsAttacker || wantsCombatAI)
            GetOrAdd<UnitAttacker>();
        else
            RemoveIfPresent<UnitAttacker>();

        if (wantsAttacker || wantsCombatAI)
            GetOrAdd<UnitAnimator>();
        else
            RemoveIfPresent<UnitAnimator>();

        if (wantsCombatAI)
        {
            GetOrAdd<NavMeshAgent>();
            GetOrAdd<UnitCombatAI>();
        }
        else
        {
            RemoveIfPresent<UnitCombatAI>();
        }

        if (wantsMovement)
            GetOrAdd<UnitMovement>();
        else
            RemoveIfPresent<UnitMovement>();

        selection = null;
        health = null;
        attacker = null;
        combatAI = null;
        movement = null;
        healthBar = null;
        navAgent = null;
        unitSound = null;
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
