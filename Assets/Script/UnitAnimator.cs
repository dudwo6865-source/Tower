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

    public void PlayAttack()
    {
        if (isDead || animator == null)
            return;

        animator.SetTrigger(attackHash);
    }

    public void PlayDie()
    {
        if (isDead || animator == null)
            return;

        isDead = true;
        animator.ResetTrigger(attackHash);
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
