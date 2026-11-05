using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class UnitAnimator : MonoBehaviour
{
    [Header("애니메이터")]
    [Label("애니메이터")]
    [Tooltip("애니메이터입니다. 비워두면 자식에서 자동으로 찾습니다.")]
    public Animator animator;

    [Header("파라미터")]
    [Label("속도 파라미터")]
    [Tooltip("이동 속도 파라미터입니다. 0에 가까우면 Idle, 크면 Move로 전환합니다.")]
    public string speedParameter = "Speed";

    [Label("공격 트리거")]
    [Tooltip("공격 1회 재생 트리거입니다. 애니메이터에 이 이름의 Trigger 파라미터가 없으면 무시합니다.")]
    public string attackTrigger = "Attack";

    [Label("공격 중 Bool")]
    [Tooltip("공격하는 동안 true가 되는 Bool 파라미터입니다. 마지막 공격 후 '공격 쿨다운 + 공격 상태 유지 시간'이 " +
        "지나면 false로 돌아갑니다. 애니메이터에 이 이름의 Bool 파라미터가 없으면 무시합니다.")]
    public string attackingBool = "IsAttacking";

    [Label("공격 상태 유지 시간")]
    [Tooltip("마지막 공격 후 공격 쿨다운에 더해 이 시간(초)만큼 '공격 중 Bool'을 true로 유지합니다. " +
        "연속 공격 사이에 false로 깜빡이면 늘리세요.")]
    [Min(0f)]
    public float attackingHoldTime = 0.2f;

    [Header("이동")]
    [Label("이동 판정 속도")]
    [Tooltip("이 속도 미만이면 Idle, 이상이면 Move로 판정합니다.")]
    public float moveSpeedThreshold = 0.1f;

    [Label("이동 애니메이션 사용")]
    [Tooltip("이동 애니메이션이 없는 유닛(타워 등)은 끄세요.")]
    public bool useMoveAnimation = true;

    [Label("이동 유지 시간")]
    [Tooltip("속도가 잠깐 '이동 판정 속도' 아래로 떨어져도 이 시간(초) 동안은 직전 이동 속도를 유지합니다. " +
        "추격 중 목적지를 갱신할 때 한 프레임 멈칫해서 Move 애니메이션이 처음부터 다시 재생되는 것을 막습니다. " +
        "멈출 때 Idle 전환이 늦게 느껴지면 줄이세요.")]
    [Min(0f)]
    public float moveHoldTime = 0.2f;

    [Header("공격 재생")]
    [Label("공격 상태 이름")]
    [Tooltip("기본 레이어의 공격 상태 이름입니다. 이벤트 공격 중에는 이 상태가 끝날 때까지 이동 애니메이션 전환과 재공격을 막습니다.")]
    public string attackStateName = "Attack";

    public bool IsAttackAnimationPlaying
    {
        get
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.layerCount == 0)
                return false;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(attackStateName))
                    return next.normalizedTime < 1f;
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            return current.IsName(attackStateName) && current.normalizedTime < 1f;
        }
    }

    private NavMeshAgent agent;
    private EntityHealth health;
    private BossSkillController bossSkills;
    private Vector3 lastAnimatedPosition;
    private bool hasAnimatedPosition;

    private int speedHash;
    private int attackHash;
    private bool hasAttackTrigger;
    private int attackingHash;
    private bool hasAttackingBool;
    private bool isAttacking;
    private float attackingUntil;

    private bool isDead;
    private float lastMovingSpeed;
    private float lastMovingTime = float.NegativeInfinity;

    public bool IsDead => isDead;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        EnsureEventRelay();

        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EntityHealth>();
        bossSkills = GetComponentInParent<BossSkillController>();

        speedHash = Animator.StringToHash(speedParameter);
        attackHash = Animator.StringToHash(attackTrigger);
        attackingHash = Animator.StringToHash(attackingBool);
        hasAttackTrigger = HasParameter(attackTrigger, AnimatorControllerParameterType.Trigger);
        hasAttackingBool = HasParameter(attackingBool, AnimatorControllerParameterType.Bool);
    }

    // 없는 파라미터에 SetTrigger/SetBool하면 매번 경고가 찍히므로 미리 확인해 둔다.
    // 트리거 방식(1회 재생)과 Bool 방식(계속 공격) 컨트롤러가 섞여 있다.
    bool HasParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
            return false;

        int hash = Animator.StringToHash(parameterName);
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hash && parameter.type == type)
                return true;
        }

        return false;
    }

    void SetAttacking(bool value)
    {
        if (isAttacking == value)
            return;

        isAttacking = value;

        if (hasAttackingBool && animator != null)
            animator.SetBool(attackingHash, value);
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDied += HandleDied;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    void Update()
    {
        if (bossSkills != null && bossSkills.IsActive)
        {
            PrepareForSkill();
            lastMovingTime = float.NegativeInfinity;
            return;
        }
        // 경로를 따라 이동을 시작했으면(이동 명령·추격) 유지 시간을 기다리지 않고 바로 끈다.
        // 공격 중엔 경로가 비어 있으므로 회피로 밀리는 것은 여기에 걸리지 않는다.
        if (isAttacking && (Time.time >= attackingUntil || IsFollowingPath()))
            SetAttacking(false);

        if (isDead || animator == null || !useMoveAnimation)
            return;

        if (!CameraVisibility.IsVisible(transform.position))
            return;

        float speed = 0f;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            speed = agent.velocity.magnitude;

        if (Time.deltaTime > 0.0001f)
        {
            if (hasAnimatedPosition)
            {
                float moved = (transform.position - lastAnimatedPosition).magnitude / Time.deltaTime;
                if (moved > speed)
                    speed = moved;
            }

            lastAnimatedPosition = transform.position;
            hasAnimatedPosition = true;
        }

        // 경로 갱신 순간의 짧은 정지는 무시하고 직전 이동 속도를 유지한다.
        if (speed >= moveSpeedThreshold)
        {
            lastMovingSpeed = speed;
            lastMovingTime = Time.time;
        }
        else if (Time.time - lastMovingTime < moveHoldTime)
        {
            speed = lastMovingSpeed;
        }

        UnitAttacker attacker = ResolveAttacker();
        bool keepAttack = attacker != null && attacker.useAttackAnimationEvent && IsAttackAnimationPlaying;
        animator.SetFloat(speedHash, keepAttack ? 0f : speed);
    }

    // 시전 시작 프레임에도 이동·일반 공격 전환이 스킬 상태를 덮어쓰지 않게 한다.
    public void PrepareForSkill()
    {
        SetAttacking(false);
        ResetAttackTrigger();
        hasAnimatedPosition = false;
        if (animator != null && HasParameter(speedParameter, AnimatorControllerParameterType.Float))
            animator.SetFloat(speedHash, 0f);
    }

    // attackCooldown: 다음 공격까지의 간격. 그동안 '공격 중 Bool'을 유지한다.
    public void PlayAttack(float attackCooldown = 0f)
    {
        if (bossSkills != null && bossSkills.IsActive)
            return;
        if (isDead || animator == null)
            return;

        if (hasAttackTrigger)
            animator.SetTrigger(attackHash);

        attackingUntil = Time.time + Mathf.Max(0f, attackCooldown) + attackingHoldTime;
        SetAttacking(true);
    }

    // 아직 소비되지 않은 공격 트리거를 지운다. 공격이 취소됐는데 트리거가 남아 있으면
    // 나중에 피해 없는 공격 모션이 한 번 더 재생된다.
    public void ResetAttackTrigger()
    {
        if (hasAttackTrigger && animator != null)
            animator.ResetTrigger(attackHash);
    }

    // 공격이 끊겼을 때(추격 시작 등) '공격 중 Bool'을 즉시 끈다.
    public void CancelAttacking()
    {
        SetAttacking(false);
    }

    bool IsFollowingPath()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped)
            return false;

        return agent.hasPath || agent.pathPending;
    }

    // 사망 모션은 쓰지 않는다(사망 연출은 EntityHealth의 사망 이펙트가 맡는다).
    // 남은 공격·이동 애니메이션 입력만 멈춘다.
    public void PlayDie()
    {
        if (isDead || animator == null)
            return;

        isDead = true;
        if (hasAttackTrigger)
            animator.ResetTrigger(attackHash);
        SetAttacking(false);
        animator.SetFloat(speedHash, 0f);
    }

    public void OnAttackHit()
    {
        ResolveAttacker()?.ApplyAttackImpact();
    }

    public void OnAttackFire()
    {
        OnAttackHit();
    }

    // 애니메이션 클립 이벤트(한글 이름): 타격 프레임에 피해/투사체를 적용한다. OnAttackHit과 같다.
    public void 공격()
    {
        OnAttackHit();
    }

    // 애니메이션 클립 이벤트(한글 이름): 공격 사운드를 낸다. '애니메이션 이벤트로 공격 사운드'가 켜져 있을 때만 동작한다.
    public void 공격사운드()
    {
        if (isDead)
            return;

        ResolveAttacker()?.PlayAttackSoundFromEvent();
    }

    // Animator가 자식 모델에 있으면 공격 애니메이션 이벤트가 이 컴포넌트에 닿지 않으므로
    // 그 오브젝트에 중계 컴포넌트를 붙여 이벤트를 넘겨받는다.
    void EnsureEventRelay()
    {
        if (animator == null || animator.gameObject == gameObject)
            return;

        AnimationEventRelay relay = animator.GetComponent<AnimationEventRelay>();
        if (relay == null)
            relay = animator.gameObject.AddComponent<AnimationEventRelay>();

        if (relay.target == null)
            relay.target = this;
    }

    UnitAttacker ResolveAttacker()
    {
        UnitAttacker attacker = GetComponent<UnitAttacker>();

        if (attacker != null)
            return attacker;

        return GetComponentInParent<UnitAttacker>();
    }

    void HandleDied()
    {
        PlayDie();
    }
}
