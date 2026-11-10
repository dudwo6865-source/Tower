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
    [Label("부화 몬스터 프리팹")]
    [Tooltip("EnemyCombatAI 또는 BossCombatAI가 붙은 적 프리팹을 연결합니다. 보스 AI는 둥지 없이 곧바로 본부로 진군하며, 후퇴·둥지 회복은 하지 않습니다.")]
    public MobileCombatAI monsterPrefab;
    [Label("부화 위치")]
    [Tooltip("비워두면 알 위치에서 부화합니다. 이동 가능한 NavMesh 위에 배치하세요.")]
    public Transform hatchPoint;
    [Label("체력 배율")]
    [Min(0.01f)] public float healthMultiplier = 3f;
    [Label("공격력 배율")]
    [Min(0.01f)] public float damageMultiplier = 2f;
    [Label("이동 속도 배율")]
    [Min(0.01f)] public float speedMultiplier = 1f;
    [Label("크기 배율")]
    [Tooltip("부화한 몬스터의 크기(Transform 스케일)에 곱합니다. NavMeshAgent 반경·높이와 콜라이더도 함께 커지며, 사거리·시야 같은 수치는 바뀌지 않습니다.")]
    [Min(0.01f)] public float sizeMultiplier = 1f;
    [Label("다음 부화 표시")]
    [Tooltip("직접 만든 자식 표시 오브젝트를 연결합니다. 부화를 기다리는 동안 켭니다. 알 본체나 상위 오브젝트는 연결하지 마세요.")]
    public GameObject nextHatchIndicator;
    [Label("부화 이펙트 프리팹")]
    public GameObject hatchEffectPrefab;
    [Label("파괴 시에도 부화 이펙트")]
    [Tooltip("켜면 알이 공격받아 파괴될 때도 부화 이펙트를 부화 위치에 출력합니다. EntityHealth의 사망 이펙트와는 별개로 함께 나옵니다.")]
    public bool playHatchEffectOnDestroyed = true;
    [Label("부화 사운드")]
    [Tooltip("몬스터가 나오는 순간 한 번 재생합니다. 알에 UnitSound가 있으면 그 볼륨·거리 감쇠를 쓰고, 없으면 거리와 무관하게 재생합니다.")]
    public AudioClip hatchSound;
    [Label("부화 사운드 볼륨")]
    [Tooltip("UnitSound가 있으면 그 전체 볼륨에 곱하는 배율입니다.")]
    [Range(0f, 2f)] public float hatchSoundVolume = 1f;

    [Label("알 애니메이션")]
    [Tooltip("자식 모델의 MonsterEggAnimation을 연결합니다. 비워두면 자식에서 찾으며, 없으면 기존처럼 즉시 부화합니다.")]
    public MonsterEggAnimation eggAnimation;

    // 선택 정보 패널에 보여줄 부화 상태
    public enum HatchStatus { Preparing, Waiting, NextHatch, Hatching, Resolved, Error }

    bool hatching;
    bool usesHatchAnimation;
    EggMissionController mission;
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
        if (playHatchEffectOnDestroyed && hatchEffectPrefab != null)
        {
            Transform point = hatchPoint != null ? hatchPoint : transform;
            CombatEffectSpawner.Spawn(hatchEffectPrefab, point.position, point.rotation);
        }
        DestroyedByDamage?.Invoke(this);
    }

    public string ValidateSetup()
    {
        SelectableEntity entity = GetComponent<SelectableEntity>();
        if (entity == null || entity.entityType != SelectableEntityType.Building)
            return "알의 SelectableEntity 종류를 Building으로 설정하세요.";
        if (GetComponent<EnemySpawner>() != null || GetComponent<CombatAIBase>() != null)
            return "알에는 EnemySpawner와 전투 AI를 붙이지 마세요.";
        if (monsterPrefab == null || !monsterPrefab.enabled || !monsterPrefab.gameObject.activeSelf ||
            !(monsterPrefab is EnemyCombatAI || monsterPrefab is BossCombatAI))
            return "활성 EnemyCombatAI 또는 BossCombatAI가 붙은 부화 몬스터 프리팹을 연결하세요.";
        SelectableEntity monsterEntity = monsterPrefab.GetComponent<SelectableEntity>();
        if (monsterEntity == null || monsterEntity.entityType != SelectableEntityType.Unit)
            return "부화 몬스터의 SelectableEntity 종류를 Unit으로 설정하세요.";
        if (monsterPrefab.GetComponents<CombatAIBase>().Length != 1 ||
            monsterPrefab.GetComponent<EntityHealth>() == null || monsterPrefab.GetComponent<UnitAttacker>() == null ||
            monsterPrefab.GetComponent<NavMeshAgent>() == null || !monsterPrefab.GetComponent<NavMeshAgent>().enabled)
            return "부화 몬스터에는 전투 AI 하나, EntityHealth, UnitAttacker, 활성 NavMeshAgent가 필요합니다.";
        if (!Positive(healthMultiplier) || !Positive(damageMultiplier) || !Positive(speedMultiplier) ||
            !Positive(sizeMultiplier))
            return "부화 몬스터 배율은 0보다 큰 유한한 값이어야 합니다.";
        if (nextHatchIndicator != null && (!nextHatchIndicator.transform.IsChildOf(transform) || nextHatchIndicator == gameObject))
            return "다음 부화 표시는 알의 자식 오브젝트로 연결하세요.";
        MonsterEggAnimation animation = eggAnimation != null ? eggAnimation : GetComponentInChildren<MonsterEggAnimation>(true);
        if (animation != null) return animation.ValidateSetup(this);
        return null;
    }

    // 알을 품은 미션 컨트롤러에 현재 부화 상태를 묻는다. 컨트롤러가 없으면 준비 중으로 본다.
    public HatchStatus GetHatchStatus(out float remainingSeconds, out float progress, out int order)
    {
        if (mission == null) mission = GetComponentInParent<EggMissionController>();
        if (mission == null)
        {
            remainingSeconds = 0f;
            progress = 0f;
            order = 0;
            return HatchStatus.Preparing;
        }
        return mission.GetEggStatus(this, out remainingSeconds, out progress, out order);
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
        // 프리팹 원래 스케일에 배율을 곱해 몬스터 크기를 바꾼다.
        if (!Mathf.Approximately(sizeMultiplier, 1f)) monster.transform.localScale *= sizeMultiplier;
        NavMeshAgent agent = monster.GetComponent<NavMeshAgent>();
        EntityHealth health = monster.GetComponent<EntityHealth>();
        if (health == null || !health.IsAlive || agent == null || !agent.isOnNavMesh)
        {
            monster.SetActive(false);
            Destroy(monster);
            error = "부화 몬스터의 체력과 NavMesh 에이전트 종류를 확인하세요.";
            return false;
        }
        // 일반 적 AI는 본부로 진군시키고, 보스 AI는 둥지 없이 곧바로 습격 상태로 시작한다.
        EnemyCombatAI enemyAI = monster.GetComponent<EnemyCombatAI>();
        if (enemyAI != null)
        {
            enemyAI.advanceToEnemyBuildings = true;
            enemyAI.shareTargetWithNearbyAllies = false;
        }
        BossCombatAI bossAI = monster.GetComponent<BossCombatAI>();
        if (bossAI != null) bossAI.InitializeWithoutNest();
        trackMonster(health);
        Resolved = true;
        SetNextHatch(false);
        if (hatchEffectPrefab != null) CombatEffectSpawner.Spawn(hatchEffectPrefab, point.position, point.rotation);
        PlayHatchSound(point.position);
        if (eggAnimation != null) eggAnimation.DetachShell();
        // 파괴 이벤트와 처치 통계를 발생시키지 않고 껍질만 제거한다.
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    // 알은 곧바로 제거되므로 분리된 오브젝트에서 끝까지 재생한다.
    void PlayHatchSound(Vector3 position)
    {
        if (hatchSound == null || hatchSoundVolume <= 0f) return;
        UnitSound unitSound = GetComponent<UnitSound>();
        if (unitSound != null)
        {
            unitSound.PlaySkillHit(hatchSound, hatchSoundVolume);
            return;
        }
        if (!SoundOverlapLimiter.TryPlay(hatchSound)) return;
        GameObject soundObject = new GameObject("MonsterEgg_HatchSound");
        soundObject.transform.position = position;
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = Mathf.Clamp01(hatchSoundVolume);
        source.clip = hatchSound;
        source.Play();
        Destroy(soundObject, hatchSound.length + 0.1f);
    }
}
