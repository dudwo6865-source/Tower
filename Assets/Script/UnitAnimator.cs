using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class UnitAnimator : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("애니메이터입니다. 비워두면 자식에서 자동으로 찾습니다.")]
    public Animator animator;

    [Header("Parameters")]
    [Tooltip("이동 속도 파라미터입니다. 0에 가까우면 Idle, 크면 Move로 전환합니다.")]
    public string speedParameter = "Speed";

    [Tooltip("공격 1회 재생 트리거입니다.")]
    public string attackTrigger = "Attack";

    [Tooltip("사망 상태 전환 트리거입니다.")]
    public string dieTrigger = "Die";

    [Header("Movement")]
    [Tooltip("이 속도 미만이면 Idle, 이상이면 Move로 판정합니다.")]
    public float moveSpeedThreshold = 0.1f;

    [Tooltip("이동 애니메이션이 없는 유닛(타워 등)은 끄세요.")]
    public bool useMoveAnimation = true;

    private NavMeshAgent agent;
    private EntityHealth health;

    private int speedHash;
    private int attackHash;
    private int dieHash;

    private bool isDead;

    public bool IsDead => isDead;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

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

        float speed = 0f;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            speed = agent.velocity.magnitude;

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
