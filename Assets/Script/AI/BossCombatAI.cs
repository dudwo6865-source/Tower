using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(EntityHealth))]
[AddComponentMenu("게임/전투/보스 전투 AI")]
public class BossCombatAI : MobileCombatAI
{
    [Header("보스 이동")]
    [Label("진군 중 최대 추격 거리")]
    [Tooltip("진군 중 표적을 처음 발견한 위치에서 이 거리 이상 추격하지 않습니다.")]
    [Min(1f)] public float pursuitDistance = 15f;
    [Label("추격 포기 후 진군 시간")]
    [Tooltip("유닛에게 끝없이 유인되지 않도록 추격 포기 후 본부 방향으로 이동하는 시간입니다.")]
    [Min(0.1f)] public float resumeMarchDuration = 3f;
    [Label("막힌 경로 재검사 간격")]
    [Min(0.1f)] public float routeCheckInterval = 0.5f;

    public BossNest Nest { get; private set; }
    public EntityHealth Health { get; private set; }
    public BossSortieRules.Phase State => rules.State;
    public float IncomingDamageMultiplier => isActiveAndEnabled && Nest != null && Nest.isActiveAndEnabled && Nest.Contains(transform.position)
        ? 1f - Mathf.Clamp01(Nest.damageReduction) : 1f;
    public string StateLabel => State == BossSortieRules.Phase.Retreat ? "둥지로 후퇴 중" :
        State == BossSortieRules.Phase.Assault ? "본부 습격 중" : HasValidTarget() ? "둥지에서 교전 중" : "둥지에서 대기 중";

    readonly BossSortieRules rules = new BossSortieRules();
    Vector3 restPosition;
    Vector3 pursuitOrigin;
    bool pursuing;
    float marchUntil;
    float nextRouteCheck;
    float nextTargetCheck;
    readonly NavMeshPath route = new NavMeshPath();
    SelectableEntity localTarget;
    SelectableEntity blocker;

    protected override void Awake()
    {
        base.Awake();
        Health = GetComponent<EntityHealth>();
        shareAggroWithAllies = false;
        Health.OnDied += HandleDied;
        Health.OnDamaged += HandleDamaged;
    }

    public void Initialize(BossNest nest, Vector3 position)
    {
        Nest = nest;
        restPosition = position;
    }

    void OnDestroy()
    {
        if (Health != null) Health.OnDied -= HandleDied;
        if (Health != null) Health.OnDamaged -= HandleDamaged;
    }

    void HandleDied() { Nest?.NotifyDefeated(this); }
    protected override void OnDisable()
    {
        base.OnDisable();
        attacker?.CancelPendingAttack();
        if (agent != null && agent.enabled) StopAgentMovement();
    }
    void HandleDamaged(float damage, SelectableEntity source)
    {
        if (Nest == null || Nest.cycle == null || State != BossSortieRules.Phase.Assault) return;
        var previous = State;
        rules.Tick(Nest.cycle.IsNight, Nest.cycle.CurrentNightNumber, Nest.firstAssaultNight,
            true, false, Health.TotalDamageTaken, Health.MaxHealth, Nest.retreatHealthFraction);
        if (previous != State) ResetMovementForState();
    }

    void ResetMovementForState()
    {
        SetTarget(null);
        attacker.CancelPendingAttack();
        StopAgentMovement();
        blocker = null;
        localTarget = null;
        pursuing = false;
        nextRouteCheck = 0f;
        nextTargetCheck = 0f;
    }
    protected override bool CanJoinSharedAggro() => false;
    protected override void HandleAttackedBy(SelectableEntity source) { }
    public override void CommandAttackTarget(SelectableEntity target) { }

    void Update()
    {
        if (Nest == null || !Nest.isActiveAndEnabled || Health == null || !Health.IsAlive ||
            (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver))
        {
            attacker.CancelPendingAttack();
            StopAgentMovement();
            return;
        }
        if (Time.deltaTime <= 0f || Nest.cycle == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        bool atRest = HorizontalDistance(transform.position, restPosition) <= Nest.arrivalDistance;
        bool atBoundary = !Nest.Contains(transform.position);
        if (Time.time >= nextTargetCheck || (localTarget != null && !IsAlive(localTarget)) ||
            (State == BossSortieRules.Phase.Retreat && atRest))
        {
            nextTargetCheck = Time.time + Mathf.Max(0.1f, retargetInterval);
            localTarget = FindLocalTarget(atRest);
        }
        var previous = State;
        rules.Tick(Nest.cycle.IsNight, Nest.cycle.CurrentNightNumber, Nest.firstAssaultNight,
            localTarget != null, atRest, Health.TotalDamageTaken, Health.MaxHealth, Nest.retreatHealthFraction);
        if (State != previous)
        {
            ResetMovementForState();
        }

        if (State == BossSortieRules.Phase.Retreat)
        {
            MoveAlongRoute(restPosition);
            return;
        }
        if (State == BossSortieRules.Phase.Nest)
        {
            if (atBoundary || localTarget == null)
            {
                if (!atRest) MoveAlongRoute(restPosition);
                else { SetTarget(null); StopAgentMovement(); }
            }
            else Engage(localTarget);
            return;
        }

        if (pursuing && HorizontalDistance(transform.position, pursuitOrigin) >= pursuitDistance)
        {
            marchUntil = Time.time + resumeMarchDuration;
            pursuing = false;
        }
        if (localTarget != null && Time.time >= marchUntil)
        {
            if (!pursuing) { pursuitOrigin = transform.position; pursuing = true; }
            Engage(localTarget);
            return;
        }
        pursuing = false;
        SelectableEntity hq = TargetFinder.FindOpposingHeadquarters(selfEntity.ownerId, attacker);
        if (hq == null) { SetTarget(null); StopAgentMovement(); return; }
        if (attacker.IsInRange(hq)) Engage(hq);
        else MoveAlongRoute(hq.transform.position);
    }

    SelectableEntity FindLocalTarget(bool atRest)
    {
        if (State == BossSortieRules.Phase.Retreat && !atRest) return null;
        SelectableEntity best = null;
        float distance = aggroRange;
        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (!IsAlive(entity) || entity.ownerId == selfEntity.ownerId || !attacker.CanEngage(entity)) continue;
            if (State != BossSortieRules.Phase.Assault && !Nest.Contains(entity.transform.position) && !attacker.IsInRange(entity)) continue;
            float candidateDistance = HorizontalDistance(transform.position, entity.transform.position);
            if (candidateDistance > distance) continue;
            distance = candidateDistance;
            best = entity;
        }
        return best;
    }

    static bool IsAlive(SelectableEntity entity) => entity != null && entity.isActiveAndEnabled &&
        entity.CachedHealth != null && entity.CachedHealth.IsAlive;

    void Engage(SelectableEntity target)
    {
        SetTarget(target);
        UpdateCombat();
    }

    // 후퇴 중에는 이동 경로를 실제로 가로막는 건물만 공격한다.
    void MoveAlongRoute(Vector3 destination)
    {
        if (Time.time >= nextRouteCheck)
        {
            nextRouteCheck = Time.time + Mathf.Max(0.1f, routeCheckInterval);
            blocker = null;
            NavMesh.CalculatePath(transform.position, destination,
                new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }, route);
            Vector3[] corners = route.corners;
            Vector3 end = corners.Length > 0 ? corners[corners.Length - 1] : transform.position;
            if (route.status != NavMeshPathStatus.PathComplete)
                blocker = FindBlocker(end, destination);
            if (blocker == null && corners.Length > 1)
                blocker = FindBlocker(corners[0], corners[1]);
            if (blocker == null)
            {
                SetTarget(null);
                attacker.CancelPendingAttack();
                agent.stoppingDistance = Mathf.Min(stoppingDistance, Nest.arrivalDistance);
                if (corners.Length > 0) agent.SetPath(route);
            }
        }
        if (IsAlive(blocker)) Engage(blocker);
    }

    SelectableEntity FindBlocker(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        float length = direction.magnitude;
        if (length <= 0f) return null;
        var ray = new Ray(new Vector3(from.x, 0f, from.z), direction / length);
        SelectableEntity nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (SelectableEntity building in BuildingRegistry.Buildings)
        {
            if (!IsAlive(building) || building.ownerId == selfEntity.ownerId || !attacker.CanEngage(building)) continue;
            Bounds bounds = building.SelectionBounds;
            bounds.center = new Vector3(bounds.center.x, 0f, bounds.center.z);
            bounds.Expand(new Vector3(agent.radius * 2f, 1f, agent.radius * 2f));
            if (!bounds.IntersectRay(ray, out float hit) || hit > length) continue;
            float distance = HorizontalDistance(transform.position, building.transform.position);
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = building;
        }
        return nearest;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
