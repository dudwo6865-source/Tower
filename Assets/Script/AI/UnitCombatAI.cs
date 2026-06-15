using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class UnitCombatAI : CombatAIBase
{
    [Header("Behavior")]
    [Tooltip("교전 대상이 없을 때 가장 가까운 적 건물로 진군합니다. 공격 유닛(적)에 적합합니다.")]
    public bool advanceToEnemyBuildings;

    [Header("Movement")]
    [Tooltip("대상에 접근할 때 멈추는 거리입니다.")]
    public float stoppingDistance = 2f;

    [Tooltip("움직이는 대상을 추격할 때 목적지를 갱신하는 간격(초)입니다.")]
    public float destinationRefreshInterval = 0.25f;

    [Tooltip("정지 후 대상을 바라보는 회전 속도입니다.")]
    public float facingSpeed = 8f;

    [Tooltip("여러 유닛이 같은 대상을 공격할 때 대상 둘레로 흩어지는 각도 범위(도)입니다. 0이면 한 점으로 모입니다.")]
    public float approachSpreadAngle = 60f;

    private NavMeshAgent agent;
    private float destinationTimer;
    private bool manualMoveActive;
    private Vector3 lastDestination;
    private bool hasDestination;
    private float approachAngleFactor;

    protected override void Awake()
    {
        base.Awake();
        agent = GetComponent<NavMeshAgent>();

        // 인스턴스별로 안정적인 -1~1 분산 계수. 같은 방향에서 접근하는 유닛들을 흩어준다.
        float normalized = (GetInstanceID() & 0xFFFF) / 65535f;
        approachAngleFactor = normalized * 2f - 1f;
    }

    void Start()
    {
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = false;
        EnsureOnNavMesh();
    }

    void EnsureOnNavMesh()
    {
        if (agent.isOnNavMesh)
            return;

        if (NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit hit,
                10f,
                NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    void Update()
    {
        if (manualMoveActive)
        {
            if (ReachedManualDestination())
                manualMoveActive = false;
            else
                return;
        }

        TickRetarget();

        if (!HasValidTarget())
            return;

        if (attacker.IsInRange(currentTarget))
            StopAndAttack();
        else
            ChaseTarget();
    }

    protected override SelectableEntity FindTarget()
    {
        SelectableEntity enemy = base.FindTarget();

        if (enemy != null)
            return enemy;

        if (advanceToEnemyBuildings)
            return TargetFinder.FindNearestEnemyBuilding(
                transform.position,
                selfEntity.ownerId);

        return null;
    }

    protected override void OnTargetChanged()
    {
        destinationTimer = 0f;
        hasDestination = false;

        if (currentTarget == null && agent.isOnNavMesh && agent.hasPath)
            agent.ResetPath();
    }

    bool ReachedManualDestination()
    {
        if (!agent.isOnNavMesh)
            return true;

        if (agent.pathPending)
            return false;

        if (!agent.hasPath)
            return true;

        return agent.remainingDistance <= agent.stoppingDistance + 0.1f;
    }

    void StopAndAttack()
    {
        if (agent.isOnNavMesh && agent.hasPath)
        {
            agent.ResetPath();
            hasDestination = false;
        }

        agent.velocity = Vector3.zero;

        FaceTarget();
        attacker.TryAttack(currentTarget, currentTargetHealth);
    }

    void ChaseTarget()
    {
        destinationTimer -= Time.deltaTime;

        if (destinationTimer > 0f)
            return;

        destinationTimer = destinationRefreshInterval;

        if (!agent.isOnNavMesh)
            return;

        Vector3 destination =
            TargetFinder.GetApproachPosition(
                transform.position,
                currentTarget,
                stoppingDistance,
                approachAngleFactor * approachSpreadAngle);

        float sqrDistToTarget =
            (currentTarget.transform.position - transform.position).sqrMagnitude;

        float sqrDistDestToSelf =
            (destination - transform.position).sqrMagnitude;

        // 접근 지점을 못 찾아 현재 위치가 반환된 경우 — 잠시 후 재시도
        if (sqrDistDestToSelf < 1f &&
            sqrDistToTarget > (stoppingDistance + 5f) * (stoppingDistance + 5f))
        {
            hasDestination = false;
            destinationTimer = 0f;
            return;
        }

        if (hasDestination &&
            (lastDestination - destination).sqrMagnitude < 1f)
            return;

        lastDestination = destination;
        hasDestination = agent.SetDestination(destination);
    }

    void FaceTarget()
    {
        Vector3 direction =
            currentTarget.transform.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * facingSpeed);
    }

    public void SuspendForManualMove()
    {
        manualMoveActive = true;
        currentTarget = null;
        currentTargetHealth = null;
        hasDestination = false;
    }

    public void Initialize(int unusedLegacyParameter)
    {
        SetTarget(FindTarget());
    }
}
