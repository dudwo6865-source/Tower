using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[AddComponentMenu("게임/미션/BossNest")]
public class BossNest : MonoBehaviour
{
    [Header("보스")]
    [Label("보스 이름")]
    public string displayName = "둥지의 수호자";
    [Label("보스 프리팹")]
    [Tooltip("보스 전투 AI가 붙은 프리팹입니다. 시작 시 한 번만 생성합니다.")]
    public BossCombatAI bossPrefab;
    [Label("대기 위치")]
    [Tooltip("비우면 둥지 중심을 사용합니다. 둥지 범위 안의 이동 가능한 위치를 지정합니다.")]
    public Transform restingPoint;
    [Label("낮밤 사이클")]
    public DayNightCycle cycle;

    [Header("둥지와 출격")]
    [Label("둥지 반경")]
    [Min(1f)] public float radius = 15f;
    [Label("첫 출격 밤")]
    [Min(1)] public int firstAssaultNight = 1;
    [Label("후퇴 피해 비율")]
    [Tooltip("한 번의 출격에서 최대 체력의 이 비율만큼 피해를 받으면 후퇴합니다. 체력은 회복하지 않습니다.")]
    [Range(0.01f, 1f)] public float retreatHealthFraction = 0.2f;
    [Label("둥지 피해 감소율")]
    [Tooltip("보스가 실제 둥지 범위 안에 있으면 방어력 계산 후 피해를 이 비율만큼 줄입니다.")]
    [Range(0f, 0.95f)] public float damageReduction = 0.3f;
    [Label("복귀 도착 거리")]
    [Min(0.1f)] public float arrivalDistance = 1f;
    [Label("생성 위치 보정 거리")]
    [Min(0.1f)] public float spawnSampleDistance = 2f;

    public BossCombatAI Boss { get; private set; }
    public bool WasSpawned { get; private set; }
    public bool Defeated { get; private set; }
    public string Error { get; private set; }
    public Vector3 RestPosition => restingPoint != null ? restingPoint.position : transform.position;
    public bool Contains(Vector3 position)
    {
        Vector3 delta = position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= radius * radius;
    }

    public string ValidateSetup()
    {
        if (bossPrefab == null) return "보스 프리팹을 연결하세요.";
        if (!bossPrefab.gameObject.activeSelf || !bossPrefab.enabled) return "보스 프리팹과 보스 AI를 활성화하세요.";
        if (bossPrefab.GetComponents<CombatAIBase>().Length != 1) return "보스 프리팹에는 보스 전투 AI만 남겨야 합니다.";
        if (bossPrefab.GetComponent<EntityHealth>() == null || bossPrefab.GetComponent<NavMeshAgent>() == null)
            return "보스 프리팹에 체력과 NavMeshAgent가 필요합니다.";
        if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius) || !Contains(RestPosition))
            return "둥지 반경과 범위 안의 대기 위치를 확인하세요.";
        if (firstAssaultNight < 1 || retreatHealthFraction <= 0f || retreatHealthFraction > 1f ||
            float.IsNaN(retreatHealthFraction) || damageReduction < 0f || damageReduction >= 1f || float.IsNaN(damageReduction) ||
            arrivalDistance <= 0f || arrivalDistance >= radius || spawnSampleDistance <= 0f)
            return "출격 밤, 후퇴 비율, 피해 감소율, 도착 거리와 위치 보정 거리를 확인하세요.";
        return null;
    }

    void Start()
    {
        if (cycle == null) cycle = DayNightCycle.Instance;
        if (cycle == null) cycle = FindFirstObjectByType<DayNightCycle>();
        Error = ValidateSetup();
        if (Error == null && cycle == null) Error = "낮밤 사이클을 연결하세요.";
        if (Error != null) { Debug.LogError("보스 둥지: " + Error, this); return; }
        NavMeshAgent prefabAgent = bossPrefab.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = prefabAgent.agentTypeID, areaMask = prefabAgent.areaMask };
        if (!NavMesh.SamplePosition(RestPosition, out NavMeshHit hit, spawnSampleDistance, filter) || !Contains(hit.position))
        {
            Error = "대기 위치 근처에 보스가 이동할 수 있는 NavMesh가 없습니다.";
            Debug.LogError("보스 둥지: " + Error, this);
            return;
        }
        Boss = Instantiate(bossPrefab, hit.position, transform.rotation, transform);
        Boss.Initialize(this, hit.position);
        WasSpawned = true;
    }

    public void NotifyDefeated(BossCombatAI boss)
    {
        if (boss == Boss && WasSpawned) Defeated = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        const int segments = 64;
        Vector3 previous = transform.position + Vector3.right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.DrawWireSphere(RestPosition, arrivalDistance);
    }
}
