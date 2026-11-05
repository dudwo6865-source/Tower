using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(EntityHealth))]
[AddComponentMenu("게임/미션/MonsterEgg")]
public class MonsterEgg : MonoBehaviour
{
    [Label("목표 이름")]
    public string displayName = "몬스터 알";
    [Label("부화 우선순위")]
    [Tooltip("작은 수부터 부화합니다. 같은 값이면 맵 계층 순서를 따릅니다.")]
    public int hatchPriority;
    [Label("부화 몬스터 프리팹")]
    [Tooltip("EnemyCombatAI가 붙은 일반 적 프리팹을 연결합니다. 보스 둥지 전용 AI는 사용할 수 없습니다.")]
    public EnemyCombatAI monsterPrefab;
    [Label("부화 위치")]
    [Tooltip("비워두면 알 위치에서 부화합니다. 이동 가능한 NavMesh 위에 배치하세요.")]
    public Transform hatchPoint;
    [Label("체력 배율")]
    [Min(0.01f)] public float healthMultiplier = 3f;
    [Label("공격력 배율")]
    [Min(0.01f)] public float damageMultiplier = 2f;
    [Label("이동 속도 배율")]
    [Min(0.01f)] public float speedMultiplier = 1f;
    [Label("다음 부화 표시")]
    [Tooltip("직접 만든 자식 표시 오브젝트를 연결합니다. 현재 부화 대상일 때만 켭니다. 알 본체나 상위 오브젝트는 연결하지 마세요.")]
    public GameObject nextHatchIndicator;
    [Label("부화 이펙트 프리팹")]
    public GameObject hatchEffectPrefab;

    [Label("알 애니메이션")]
    [Tooltip("자식 모델의 MonsterEggAnimation을 연결합니다. 비워두면 자식에서 찾으며, 없으면 기존처럼 즉시 부화합니다.")]
    public MonsterEggAnimation eggAnimation;

    bool hatching;
    bool usesHatchAnimation;
    Action<EntityHealth> onSpawned;
    Action<string> onHatchFailed;
    Func<bool> canHatch;
    public bool HatchPlaybackMissing => hatching && usesHatchAnimation &&
        (eggAnimation == null || !eggAnimation.PlaybackAvailable);

    public EntityHealth Health { get; private set; }
    public bool Resolved { get; private set; }
    public event Action<MonsterEgg> DestroyedByDamage;

    void Awake()
    {
        Health = GetComponent<EntityHealth>();
        Health.OnDied += HandleDeath;
        if (eggAnimation == null) eggAnimation = GetComponentInChildren<MonsterEggAnimation>(true);
        if (eggAnimation != null) eggAnimation.Bind(this);
        SetNextHatch(false);
    }

    void OnDestroy()
    {
        if (Health != null) Health.OnDied -= HandleDeath;
    }

    void HandleDeath()
    {
        if (Resolved) return;
        Resolved = true;
        SetNextHatch(false);
        DestroyedByDamage?.Invoke(this);
    }

    public string ValidateSetup()
    {
        SelectableEntity entity = GetComponent<SelectableEntity>();
        if (entity == null || entity.entityType != SelectableEntityType.Building)
            return "알의 SelectableEntity 종류를 Building으로 설정하세요.";
        if (GetComponent<EnemySpawner>() != null || GetComponent<CombatAIBase>() != null)
            return "알에는 EnemySpawner와 전투 AI를 붙이지 마세요.";
        if (monsterPrefab == null || !monsterPrefab.enabled || !monsterPrefab.gameObject.activeSelf)
            return "활성 EnemyCombatAI가 붙은 부화 몬스터 프리팹을 연결하세요.";
        SelectableEntity monsterEntity = monsterPrefab.GetComponent<SelectableEntity>();
        if (monsterEntity == null || monsterEntity.entityType != SelectableEntityType.Unit)
            return "부화 몬스터의 SelectableEntity 종류를 Unit으로 설정하세요.";
        if (monsterPrefab.GetComponents<CombatAIBase>().Length != 1 ||
            monsterPrefab.GetComponent<EntityHealth>() == null || monsterPrefab.GetComponent<UnitAttacker>() == null ||
            monsterPrefab.GetComponent<NavMeshAgent>() == null || !monsterPrefab.GetComponent<NavMeshAgent>().enabled)
            return "부화 몬스터에는 전투 AI 하나, EntityHealth, UnitAttacker, 활성 NavMeshAgent가 필요합니다.";
        if (!Positive(healthMultiplier) || !Positive(damageMultiplier) || !Positive(speedMultiplier))
            return "부화 몬스터 배율은 0보다 큰 유한한 값이어야 합니다.";
        if (nextHatchIndicator != null && (!nextHatchIndicator.transform.IsChildOf(transform) || nextHatchIndicator == gameObject))
            return "다음 부화 표시는 알의 자식 오브젝트로 연결하세요.";
        MonsterEggAnimation animation = eggAnimation != null ? eggAnimation : GetComponentInChildren<MonsterEggAnimation>(true);
        if (animation != null) return animation.ValidateSetup(this);
        return null;
    }

    public void SetUrgency(float value)
    {
        if (eggAnimation != null) eggAnimation.SetUrgency(value);
    }

    public void BeginHatch(Action<EntityHealth> spawned, Action<string> failed, Func<bool> allowed)
    {
        if (Resolved || hatching) return;
        hatching = true;
        onSpawned = spawned;
        onHatchFailed = failed;
        canHatch = allowed;
        usesHatchAnimation = eggAnimation != null;
        if (eggAnimation != null) eggAnimation.BeginHatch();
        else SpawnFromAnimation();
    }

    public void SpawnFromAnimation()
    {
        if (!hatching || Resolved || !isActiveAndEnabled || canHatch == null || !canHatch()) return;
        if (!TryHatch(onSpawned, out string error)) ReportHatchFailure(error ?? "부화에 실패했습니다.");
    }

    public void ReportHatchFailure(string error)
    {
        if (!hatching || Resolved) return;
        hatching = false;
        onHatchFailed?.Invoke(error);
    }

    static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

    public void SetNextHatch(bool active)
    {
        if (nextHatchIndicator != null && nextHatchIndicator != gameObject && nextHatchIndicator.transform.IsChildOf(transform))
            nextHatchIndicator.SetActive(active);
    }

    // 성공한 부화만 확정한다. 생성 실패 시 알을 남겨 목표가 사라지지 않게 한다.
    public bool TryHatch(Action<EntityHealth> trackMonster, out string error)
    {
        error = ValidateSetup();
        if (error != null || Resolved || Health == null || !Health.IsAlive) return false;
        Transform point = hatchPoint != null ? hatchPoint : transform;
        GameObject monster = EnemySpawnUtility.SpawnEnemy(monsterPrefab.gameObject, point.position, point.rotation,
            GetComponent<SelectableEntity>().ownerId, healthMultiplier, damageMultiplier, speedMultiplier);
        if (monster == null)
        {
            error = "부화 위치에서 몬스터를 만들지 못했습니다. NavMesh와 부화 위치를 확인하세요.";
            return false;
        }
        NavMeshAgent agent = monster.GetComponent<NavMeshAgent>();
        EntityHealth health = monster.GetComponent<EntityHealth>();
        if (health == null || !health.IsAlive || agent == null || !agent.isOnNavMesh)
        {
            monster.SetActive(false);
            Destroy(monster);
            error = "부화 몬스터의 체력과 NavMesh 에이전트 종류를 확인하세요.";
            return false;
        }
        EnemyCombatAI ai = monster.GetComponent<EnemyCombatAI>();
        ai.advanceToEnemyBuildings = true;
        ai.shareTargetWithNearbyAllies = false;
        trackMonster(health);
        Resolved = true;
        SetNextHatch(false);
        if (hatchEffectPrefab != null) CombatEffectSpawner.Spawn(hatchEffectPrefab, point.position, point.rotation);
        if (eggAnimation != null) eggAnimation.DetachShell();
        // 파괴 이벤트와 처치 통계를 발생시키지 않고 껍질만 제거한다.
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}
