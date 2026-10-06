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
    [Tooltip("공격 1회 재생 트리거입니다.")]
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

    [Label("사망 트리거")]
    [Tooltip("사망 상태 전환 트리거입니다.")]
    public string dieTrigger = "Die";

    [Header("이동")]
    [Label("이동 판정 속도")]
    [Tooltip("이 속도 미만이면 Idle, 이상이면 Move로 판정합니다.")]
    public float moveSpeedThreshold = 0.1f;

    [Label("이동 애니메이션 사용")]
    [Tooltip("이동 애니메이션이 없는 유닛(타워 등)은 끄세요.")]
    public bool useMoveAnimation = true;

    private NavMeshAgent agent;
    private EntityHealth health;
    private Vector3 lastAnimatedPosition;
    private bool hasAnimatedPosition;

    private int speedHash;
    private int attackHash;
    private int dieHash;
    private int attackingHash;
    private bool hasAttackingBool;
    private bool isAttacking;
    private float attackingUntil;

    private bool isDead;

    public bool IsDead => isDead;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        EnsureEventRelay();

        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EntityHealth>();

        speedHash = Animator.StringToHash(speedParameter);
        attackHash = Animator.StringToHash(attackTrigger);
        dieHash = Animator.StringToHash(dieTrigger);
        attackingHash = Animator.StringToHash(attackingBool);
        hasAttackingBool = HasBoolParameter(attackingHash);
    }

    // 없는 파라미터에 SetBool하면 매번 경고가 찍히므로 미리 확인해 둔다.
    bool HasBoolParameter(int hash)
    {
        if (animator == null || string.IsNullOrEmpty(attackingBool))
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Bool)
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

        animator.SetFloat(speedHash, speed);
    }

    // attackCooldown: 다음 공격까지의 간격. 그동안 '공격 중 Bool'을 유지한다.
    public void PlayAttack(float attackCooldown = 0f)
    {
        if (isDead || animator == null)
            return;

        animator.SetTrigger(attackHash);

        attackingUntil = Time.time + Mathf.Max(0f, attackCooldown) + attackingHoldTime;
        SetAttacking(true);
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

    public void PlayDie()
    {
        if (isDead || animator == null)
            return;

        isDead = true;
        animator.ResetTrigger(attackHash);
        SetAttacking(false);
        animator.SetFloat(speedHash, 0f);
        animator.SetTrigger(dieHash);
    }

    public void OnAttackHit()
    {
        ResolveAttacker()?.ApplyAttackImpact();
    }

    public void OnAttackFire()
    {
        OnAttackHit();
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
