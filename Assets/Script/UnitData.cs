using UnityEngine;

[CreateAssetMenu(fileName = "UnitData", menuName = "RTS/Unit Data")]
public class UnitData : ScriptableObject
{
    [Header("기본 (Selection)")]
    [Label("종류")]
    [Tooltip("유닛 또는 건물 종류입니다.")]
    public SelectableEntityType entityType = SelectableEntityType.Unit;

    [Label("소유자 ID")]
    [Tooltip("소유 플레이어 ID입니다. 로컬 플레이어와 같아야 선택할 수 있습니다.")]
    public int ownerId = 1;

    [Label("타입 ID")]
    [Tooltip("같은 타입 전체 선택(더블클릭)에 사용되는 타입 ID입니다. 예: tank, barracks")]
    public string entityTypeId = "unit";

    [Header("UI")]
    [Label("표시 이름")]
    [Tooltip("선택 정보 패널에 표시할 이름입니다. 비워두면 entityTypeId를 사용합니다.")]
    public string displayName;

    [Label("초상화")]
    [Tooltip("선택 정보 패널에 표시할 초상화입니다. SelectableEntity.portrait가 없을 때만 사용됩니다.")]
    public Sprite portrait;

    [Header("체력 (Health)")]
    [Label("최대 체력")]
    [Tooltip("최대 체력입니다.")]
    public float maxHealth = 100f;

    [Label("사망 연출 시간(초)")]
    [Tooltip("사망 시 가라앉으며 사라지는 연출 시간(초)입니다. 0이면 즉시 제거합니다.")]
    public float deathAnimationDuration = 1f;

    [Label("사망 시 가라앉는 거리")]
    [Tooltip("사망 연출 동안 아래로 가라앉는 거리입니다.")]
    public float deathSinkDistance = 1.5f;

    [Label("사망 이펙트 색")]
    [Tooltip("사망 시 표시할 이펙트 색상입니다.")]
    public Color deathEffectColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("공격 (Attacker)")]
    [Label("공격 가능")]
    [Tooltip("공격 컴포넌트를 사용합니다. 건물 등 공격하지 않는 엔티티는 꺼두세요.")]
    public bool canAttack = true;

    [Label("공격 방식")]
    [Tooltip("공격 방식입니다. 근접은 즉시 피해, 원거리는 투사체를 발사합니다.")]
    public AttackType attackType = AttackType.Melee;

    [Label("공격력")]
    [Tooltip("한 번 공격할 때 주는 피해량입니다.")]
    public float attackDamage = 10f;

    [Label("사거리")]
    [Tooltip("공격이 닿는 사거리입니다.")]
    public float attackRange = 2.5f;

    [Label("공격 간격(초)")]
    [Tooltip("공격 사이의 최소 간격(초)입니다.")]
    public float attackCooldown = 1f;

    [Label("애니메이션 이벤트로 공격")]
    [Tooltip("공격 애니메이션 이벤트(OnAttackHit)로 피해/투사체를 적용합니다.")]
    public bool useAttackAnimationEvent = true;

    [Label("투사체 속도")]
    [Tooltip("투사체 속도입니다. 원거리 공격일 때만 사용됩니다.")]
    public float projectileSpeed = 25f;

    [Label("투사체 색")]
    [Tooltip("머즐 플래시 / 투사체 색상입니다.")]
    public Color projectileColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Label("피격 색")]
    [Tooltip("피격 이펙트 색상입니다. 프리팹이 없을 때만 사용됩니다.")]
    public Color hitColor = new Color(1f, 0.5f, 0.2f, 1f);

    [Header("공격 이펙트")]
    [Label("전투 이펙트")]
    public CombatEffectPrefabs combatEffects;

    [Header("전투 AI (MobileCombatAI)")]
    [Label("전투 AI 사용")]
    [Tooltip("전투 AI를 사용합니다. 자동 교전/이동이 필요한 유닛만 켜세요.")]
    public bool hasCombatAI = true;

    [Label("어그로 범위")]
    [Tooltip("이 범위 안의 적을 자동으로 탐지해 교전합니다.")]
    public float aggroRange = 12f;

    [Label("공격 대상 우선순위")]
    [Tooltip("교전 대상 선택 우선순위입니다.")]
    public CombatTargetPriority targetPriority = CombatTargetPriority.Nearest;

    [Label("적 건물로 진군")]
    [Tooltip("교전 대상이 없을 때 상대 HQ로 진군합니다. 적 유닛(EnemyCombatAI)에만 적용됩니다.")]
    public bool advanceToEnemyBuildings;

    [Label("건물 직행 추격 거리")]
    [Tooltip("건물 추격 중 우회 접근점 대신 건물로 직행하기 시작하는 거리입니다. 0 이하면 Aggro Range를 씁니다.")]
    public float buildingDirectChaseRange;

    [Label("정지 거리")]
    [Tooltip("대상에 접근할 때 멈추는 거리입니다.")]
    public float stoppingDistance = 2f;

    [Label("대상 재탐색 간격(초)")]
    [Tooltip("목표를 다시 탐색하는 간격(초)입니다.")]
    public float retargetInterval = 0.5f;

    [Label("목적지 갱신 간격(초)")]
    [Tooltip("움직이는 대상을 추격할 때 목적지를 갱신하는 간격(초)입니다.")]
    public float destinationRefreshInterval = 0.25f;

    [Label("조준 회전 속도")]
    [Tooltip("정지 후 대상을 바라보는 회전 속도입니다.")]
    public float facingSpeed = 8f;

    [Header("이동 (Movement)")]
    [Label("수동 이동 가능")]
    [Tooltip("플레이어가 우클릭으로 수동 이동시킬 수 있습니다.")]
    public bool canMoveManually = true;

    [Label("이동 속도")]
    [Tooltip("이동 속도입니다. NavMeshAgent의 speed에 적용됩니다.")]
    public float moveSpeed = 5f;

    [Label("회전 속도")]
    [Tooltip("회전 속도(도/초)입니다. NavMeshAgent의 angularSpeed에 적용됩니다.")]
    public float angularSpeed = 360f;

    [Label("가속도")]
    [Tooltip("가속도입니다. NavMeshAgent의 acceleration에 적용됩니다.")]
    public float acceleration = 12f;

    [Header("체력바 (Health Bar)")]
    [Label("체력바 높이 오프셋")]
    [Tooltip("콜라이더 위쪽에서 체력바가 떠 있는 추가 높이입니다.")]
    public float healthBarHeightOffset = 0.3f;

    [Header("전장의 안개")]
    [Label("시야 범위")]
    [Tooltip("이 유닛·건물이 밝히는 시야 반경입니다. FogOfWarVisionSource에 적용됩니다.")]
    public float visionRange = 12f;

    [Header("그리드")]
    [Label("점유 칸 수")]
    [Tooltip("이 유닛이 차지하는 칸 수입니다.")]
    public Vector2Int footprintCells = Vector2Int.one;

    [Header("사운드")]
    [Label("공격 사운드")]
    [Tooltip("공격 사운드입니다.")]
    public AudioClip[] attackSoundClips;

    [Label("명중 사운드")]
    [Tooltip("공격이 대상에 맞았을 때의 사운드입니다.")]
    public AudioClip[] attackHitSoundClips;

    [Label("사망 사운드")]
    [Tooltip("사망·파괴 사운드입니다.")]
    public AudioClip[] deathSoundClips;

    [Label("사운드 볼륨")]
    [Tooltip("사운드 볼륨입니다. 0 이하면 UnitSound 인스펙터 값을 유지합니다.")]
    public float soundVolume;
}
