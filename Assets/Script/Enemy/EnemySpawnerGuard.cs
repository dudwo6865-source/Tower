using UnityEngine;
using UnityEngine.AI;

// 수비대가 공격대를 따라가거나 추격으로 방어 지역을 완전히 비우지 않게 한다.
// 방어 범위를 벗어나도 교전 중인 적(어그로)이 있으면 계속 추격하고, 주변에 적이 없어졌을 때만
// 배치 지점으로 복귀한다. 복귀 중에도 적이 나타나거나 공격받으면 다시 교전한다.
[DisallowMultipleComponent]
public class EnemySpawnerGuard : MonoBehaviour
{
    [Label("복귀 완료 거리")]
    [Min(0.1f)] public float arrivalDistance = 2f;

    [Label("복귀 경로 갱신 간격")]
    [Min(0.1f)] public float repathInterval = 1f;

    [Label("어그로 중 계속 추격")]
    [Tooltip("켜면 방어 범위를 벗어나도 교전 중인 적이 있는 동안은 계속 추격하고, 주변에 적이 없어졌을 때만 복귀합니다. 끄면 방어 범위를 벗어나는 즉시 복귀합니다.")]
    public bool chaseWhileAggro = true;

    [Label("최대 추격 거리")]
    [Tooltip("방어 중심에서 이 거리(m)보다 멀어지면 교전 중이라도 복귀합니다. 적이 수비대를 끝없이 끌고 가는 것을 막는 안전 줄입니다. 0이면 제한하지 않습니다.")]
    [Min(0f)] public float maxChaseDistance = 200f;

    [Label("복귀 중 재교전")]
    [Tooltip("켜면 복귀하는 도중 어그로 범위에 적이 들어오거나 공격받으면 복귀를 멈추고 다시 교전합니다.")]
    public bool reengageWhileReturning = true;

    [Label("재교전 탐색 간격(초)")]
    [Tooltip("복귀 중 주변 적을 찾는 간격입니다. 짧을수록 빨리 반응하지만 수비대가 많으면 부담이 늘어납니다.")]
    [Min(0.1f)] public float reengageScanInterval = 0.5f;

    Vector3 home;
    Vector3 defenseCenter;
    float originalStoppingDistance;
    float radius;
    bool returning;
    float nextPathTime;
    float nextScanTime;
    EnemyCombatAI ai;
    NavMeshAgent agent;
    EntityHealth health;
    SelectableEntity selfEntity;
    UnitAttacker attacker;

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
        selfEntity = GetComponent<SelectableEntity>();
        attacker = GetComponent<UnitAttacker>();
    }

    void LateUpdate()
    {
        if (ai == null || agent == null || !agent.enabled || !agent.isOnNavMesh ||
            (health != null && !health.IsAlive) || Time.timeScale <= 0f) return;

        Vector3 delta = transform.position - defenseCenter;
        delta.y = 0f;
        float sqrFromCenter = delta.sqrMagnitude;

        if (!returning && sqrFromCenter > radius * radius && ShouldReturn(sqrFromCenter))
            BeginReturn();

        if (!returning) return;

        if (TryResumeCombat(sqrFromCenter))
            return;

        delta = transform.position - home;
        delta.y = 0f;
        if (delta.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            agent.ResetPath();
            EndReturn();
            return;
        }

        if (Time.time >= nextPathTime)
        {
            nextPathTime = Time.time + Mathf.Max(0.1f, repathInterval);
            agent.isStopped = false;
            GridMovement.TrySetAgentDestination(agent, home);
        }
    }

    // 방어 범위 밖에서 복귀할지 정한다. 교전 중인 적이 있으면 최대 추격 거리 안에서는 계속 쫓는다.
    bool ShouldReturn(float sqrFromCenter)
    {
        if (!chaseWhileAggro || IsBeyondChaseLimit(sqrFromCenter, 0f))
            return true;

        return !HasAggro();
    }

    bool HasAggro()
    {
        SelectableEntity target = ai.CurrentTarget;

        if (target == null)
            return false;

        EntityHealth targetHealth = target.GetComponent<EntityHealth>();
        return targetHealth != null && targetHealth.IsAlive;
    }

    // margin만큼 안쪽까지 들어와야 '한도 안'으로 본다. 한도 경계에서 복귀·재교전을 반복하지 않게 한다.
    bool IsBeyondChaseLimit(float sqrFromCenter, float margin)
    {
        if (maxChaseDistance <= 0f)
            return false;

        float limit = Mathf.Max(radius, maxChaseDistance - margin);
        return sqrFromCenter > limit * limit;
    }

    // 복귀 중 공격받았거나(전투 AI가 표적을 받아 둠) 주변에 적이 보이면 복귀를 멈추고 다시 교전한다.
    bool TryResumeCombat(float sqrFromCenter)
    {
        if (!reengageWhileReturning || !chaseWhileAggro)
            return false;

        if (IsBeyondChaseLimit(sqrFromCenter, arrivalDistance))
            return false;

        if (HasAggro())
        {
            EndReturn();
            return true;
        }

        if (Time.time < nextScanTime || selfEntity == null)
            return false;

        nextScanTime = Time.time + Mathf.Max(0.1f, reengageScanInterval);

        SelectableEntity enemy = TargetFinder.FindBestEnemyInRange(
            transform.position,
            selfEntity.ownerId,
            ai.aggroRange,
            ai.targetPriority,
            attacker);

        if (enemy == null)
            return false;

        EndReturn();
        ai.NotifyAttackedBy(enemy);
        return true;
    }

    void BeginReturn()
    {
        returning = true;
        ai.CommandStop();
        ai.enabled = false;
        originalStoppingDistance = agent.stoppingDistance;
        agent.stoppingDistance = Mathf.Max(0.05f, arrivalDistance * 0.5f);
        nextPathTime = 0f;
        // 막 복귀를 시작한 자리에서 같은 적을 바로 다시 잡지 않도록 첫 탐색은 한 간격 뒤에 한다.
        nextScanTime = Time.time + Mathf.Max(0.1f, reengageScanInterval);
    }

    void EndReturn()
    {
        returning = false;
        agent.stoppingDistance = originalStoppingDistance;
        ai.enabled = true;
        ai.SetAdvanceToEnemyBuildings(false);
    }

    void OnDisable()
    {
        if (returning && agent != null && agent.enabled && agent.isOnNavMesh)
            agent.stoppingDistance = originalStoppingDistance;
        if (returning && ai != null && (health == null || health.IsAlive)) ai.enabled = true;
        returning = false;
    }
}
