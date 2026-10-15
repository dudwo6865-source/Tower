using UnityEngine;
using UnityEngine.AI;

// 수비대가 공격대를 따라가거나 추격으로 방어 지역을 완전히 비우지 않게 한다.
[DisallowMultipleComponent]
public class EnemySpawnerGuard : MonoBehaviour
{
    [Label("복귀 완료 거리")]
    [Min(0.1f)] public float arrivalDistance = 2f;
    [Label("복귀 경로 갱신 간격")]
    [Min(0.1f)] public float repathInterval = 1f;
    Vector3 home;
    Vector3 defenseCenter;
    float originalStoppingDistance;
    float radius;
    bool returning;
    float nextPathTime;
    EnemyCombatAI ai;
    NavMeshAgent agent;
    EntityHealth health;

    public void Initialize(Vector3 center, float defenseRadius)
    {
        defenseCenter = center;
        home = transform.position;
        Vector3 initialOffset = home - center;
        initialOffset.y = 0f;
        // 지형 보정으로 배치 지점이 밀렸더라도 자기 복귀 지점을 이탈로 판단하지 않는다.
        radius = Mathf.Max(Mathf.Max(1f, defenseRadius), initialOffset.magnitude + arrivalDistance);
        ai = GetComponent<EnemyCombatAI>();
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EntityHealth>();
    }
    void LateUpdate()
    {
        if (ai == null || agent == null || !agent.enabled || !agent.isOnNavMesh ||
            (health != null && !health.IsAlive) || Time.timeScale <= 0f) return;
        Vector3 delta = transform.position - defenseCenter;
        delta.y = 0f;
        if (!returning && delta.sqrMagnitude > radius * radius)
        {
            returning = true;
            ai.CommandStop();
            ai.enabled = false;
            originalStoppingDistance = agent.stoppingDistance;
            agent.stoppingDistance = Mathf.Max(0.05f, arrivalDistance * 0.5f);
            nextPathTime = 0f;
        }
        if (!returning) return;
        delta = transform.position - home;
        delta.y = 0f;
        if (delta.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            returning = false;
            agent.ResetPath();
            agent.stoppingDistance = originalStoppingDistance;
            ai.enabled = true;
            ai.SetAdvanceToEnemyBuildings(false);
            return;
        }
        if (Time.time >= nextPathTime)
        {
            nextPathTime = Time.time + Mathf.Max(0.1f, repathInterval);
            agent.isStopped = false;
            GridMovement.TrySetAgentDestination(agent, home);
        }
    }
    void OnDisable()
    {
        if (returning && agent != null && agent.enabled && agent.isOnNavMesh)
            agent.stoppingDistance = originalStoppingDistance;
        if (returning && ai != null && (health == null || health.IsAlive)) ai.enabled = true;
        returning = false;
    }
}
