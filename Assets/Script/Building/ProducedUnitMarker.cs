using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class ProducedUnitMarker : MonoBehaviour
{
    ProductionBuilding source;
    bool released;

    bool recalling;
    Vector3 recallTarget;
    float recallArriveDistance = 1.5f;
    float recallElapsed;

    // 경로가 막혀 도착하지 못할 때 강제로 제거하기까지의 시간(초)입니다.
    const float RecallTimeout = 20f;

    NavMeshAgent agent;
    UnitCombatAI combatAI;

    public ProductionBuilding Source => source;

    // 생산될 때 차지한 인구수입니다. 사라지면 생산 건물의 사용 인구에서 뺀다.
    public int Population { get; private set; }

    // 밤 복귀 이동 중인지 여부입니다. 복귀 중에는 플레이어 명령을 무시합니다.
    public bool IsRecalling => recalling;

    public void Initialize(ProductionBuilding producer, int population)
    {
        source = producer;
        Population = Mathf.Max(0, population);
    }

    // 밤 전환 시 호출: 생산 건물로 이동한 뒤 도착하면 스스로 사라진다.
    public void Recall(Vector3 target, float arriveDistance)
    {
        recallTarget = target;
        recallArriveDistance = Mathf.Max(0.5f, arriveDistance);
        recallElapsed = 0f;
        recalling = true;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (combatAI == null)
            combatAI = GetComponent<UnitCombatAI>();

        // 전투/기존 명령 상태를 끊고 수동 이동으로 건물까지 이동시킨다.
        if (combatAI != null)
            combatAI.BeginManualMove(recallTarget);

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            GridMovement.TrySetAgentDestination(agent, recallTarget);
        }
    }

    void Update()
    {
        if (!recalling)
            return;

        recallElapsed += Time.deltaTime;

        Vector3 delta = transform.position - recallTarget;
        delta.y = 0f;

        bool arrived =
            delta.sqrMagnitude <= recallArriveDistance * recallArriveDistance;

        if (arrived || recallElapsed >= RecallTimeout)
        {
            recalling = false;
            Destroy(gameObject);
        }
    }

    public void Release()
    {
        if (released)
            return;

        released = true;

        if (source != null)
            source.NotifyUnitReleased(this);

        source = null;
    }

    void OnDestroy()
    {
        Release();
    }
}
