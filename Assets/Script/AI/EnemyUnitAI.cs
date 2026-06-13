using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(SelectableEntity))]
public class EnemyUnitAI : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("공격 대상(이동 목표)의 소유자 ID입니다. 아군 건물은 보통 1입니다.")]
    public int targetOwnerId = 1;

    [Tooltip("건물에 접근할 때 멈추는 거리입니다.")]
    public float stoppingDistance = 3f;

    [Tooltip("목표가 없을 때 건물을 다시 찾는 간격(초)입니다.")]
    public float retargetInterval = 1f;

    private NavMeshAgent agent;
    private SelectableEntity currentTarget;
    private float retargetTimer;

    public SelectableEntity CurrentTarget => currentTarget;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = stoppingDistance;
    }

    void OnEnable()
    {
        BuildingRegistry.OnBuildingRemoved += HandleBuildingRemoved;
    }

    void OnDisable()
    {
        BuildingRegistry.OnBuildingRemoved -= HandleBuildingRemoved;
    }

    void Start()
    {
        AssignTarget();
    }

    void Update()
    {
        if (currentTarget == null)
        {
            retargetTimer -= Time.deltaTime;

            if (retargetTimer <= 0f)
            {
                AssignTarget();
                retargetTimer = retargetInterval;
            }

            return;
        }

        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            AssignTarget();
    }

    public void Initialize(int ownerIdToAttack)
    {
        targetOwnerId = ownerIdToAttack;
        AssignTarget();
    }

    public void AssignTarget()
    {
        currentTarget =
            TargetFinder.FindNearestBuilding(
                transform.position,
                targetOwnerId);

        if (currentTarget == null)
        {
            agent.ResetPath();
            return;
        }

        MoveToBuilding(currentTarget);
    }

    void MoveToBuilding(SelectableEntity building)
    {
        Vector3 destination =
            TargetFinder.GetApproachPosition(
                transform.position,
                building,
                stoppingDistance);

        agent.SetDestination(destination);
    }

    void HandleBuildingRemoved(SelectableEntity building)
    {
        if (currentTarget != building)
            return;

        currentTarget = null;
        AssignTarget();
    }
}
