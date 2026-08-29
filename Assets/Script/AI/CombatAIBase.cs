using UnityEngine;

[RequireComponent(typeof(SelectableEntity))]
[RequireComponent(typeof(UnitAttacker))]
public abstract class CombatAIBase : MonoBehaviour
{
    [Header("Combat")]
    [Tooltip("이 범위 안의 적을 자동으로 탐지해 교전합니다.")]
    public float aggroRange = 30f;

    [Tooltip("교전 대상 선택 우선순위입니다.")]
    public CombatTargetPriority targetPriority = CombatTargetPriority.Nearest;

    [Tooltip("목표를 다시 탐색하는 간격(초)입니다.")]
    public float retargetInterval = 0.5f;

    [Tooltip("이미 다른 대상과 교전/추격 중일 때, 새 공격자가 현재 대상보다 이만큼(m) 더 가까워야 대상을 전환합니다. 두 적이 번갈아 때릴 때 왔다 갔다 하는 것을 막습니다.")]
    public float damageFocusSwitchMargin = 2f;

    protected bool damageFocusTarget;

    protected UnitAttacker attacker;
    protected SelectableEntity selfEntity;

    protected SelectableEntity currentTarget;
    protected EntityHealth currentTargetHealth;

    private float retargetTimer;

    public SelectableEntity CurrentTarget => currentTarget;

    public void NotifyAttackedBy(SelectableEntity attacker)
    {
        if (!CanRetaliateAgainst(attacker))
            return;

        HandleAttackedBy(attacker);
    }

    protected virtual bool CanRetaliateAgainst(SelectableEntity attacker)
    {
        if (attacker == null || selfEntity == null)
            return false;

        if (attacker == selfEntity)
            return false;

        if (attacker.ownerId == selfEntity.ownerId)
            return false;

        EntityHealth attackerHealth = attacker.GetComponent<EntityHealth>();

        return attackerHealth != null && attackerHealth.IsAlive;
    }

    protected virtual void HandleAttackedBy(SelectableEntity attacker)
    {
        damageFocusTarget = true;
        retargetTimer = retargetInterval;

        // 이미 더 가까운 대상과 교전 중이면, 더 먼 새 공격자는 무시하고 기존 대상을 유지한다.
        if (!ShouldAdoptNewAttacker(attacker))
            return;

        SetTarget(attacker);
    }

    // 새 공격자로 대상을 전환할지 판단한다.
    // 현재 유효한 대상이 없으면 전환하고, 있으면 새 공격자가 마진 이상 더 가까울 때만 전환한다.
    protected bool ShouldAdoptNewAttacker(SelectableEntity attacker)
    {
        if (attacker == null)
            return false;

        if (!HasValidTarget())
            return true;

        if (currentTarget == attacker)
            return false;

        Vector3 selfPos = transform.position;
        float curDist = Vector3.Distance(currentTarget.transform.position, selfPos);
        float newDist = Vector3.Distance(attacker.transform.position, selfPos);

        return newDist < curDist - damageFocusSwitchMargin;
    }

    protected void ClearDamageFocusTarget()
    {
        damageFocusTarget = false;
    }

    // 스폰 직후 전원 동시 재탐색으로 프레임이 멈추지 않도록 타이머를 흩뿌린다.
    protected void StaggerStartupTimers()
    {
        float normalized = (GetInstanceID() & 0xFFFF) / 65535f;
        retargetTimer = 0.05f + normalized * Mathf.Max(0.05f, retargetInterval);
    }

    // 다음 프레임에 즉시 표적을 다시 탐색하도록 재탐색 타이머를 리셋한다.
    protected void ResetRetargetTimer()
    {
        retargetTimer = 0f;
    }

    public virtual void CommandAttackTarget(SelectableEntity target)
    {
        if (target == null)
            return;

        EntityHealth health = target.GetComponent<EntityHealth>();

        if (health == null || !health.IsAlive)
            return;

        damageFocusTarget = true;
        retargetTimer = retargetInterval;
        SetTarget(target);
    }

    public virtual void CommandStop()
    {
        damageFocusTarget = false;
        retargetTimer = 0f;
        SetTarget(null);
    }

    protected virtual void Awake()
    {
        attacker = GetComponent<UnitAttacker>();
        selfEntity = GetComponent<SelectableEntity>();
    }

    protected void TickRetarget()
    {
        if (currentTarget != null && !currentTarget)
        {
            currentTarget = null;
            currentTargetHealth = null;
        }

        retargetTimer -= Time.deltaTime;

        // 현재 표적이 죽었거나 사라졌으면 타이머를 기다리지 않고 즉시 재탐색한다.
        // (여러 마리가 같은 대상을 공격하다 죽으면 동시에 다음 표적으로 전환되도록)
        bool targetLost =
            currentTarget != null &&
            (currentTargetHealth == null || !currentTargetHealth.IsAlive);

        if (damageFocusTarget)
        {
            if (targetLost)
                damageFocusTarget = false;

            return;
        }

        if (retargetTimer > 0f && !targetLost)
            return;

        retargetTimer = retargetInterval;

        SelectableEntity newTarget = FindTarget();

        if (newTarget != null)
        {
            if (newTarget != currentTarget)
                SetTarget(newTarget);
        }
        else if (targetLost)
        {
            SetTarget(null);
        }
    }

    protected virtual SelectableEntity FindTarget()
    {
        return TargetFinder.FindBestEnemyInRange(
            transform.position,
            selfEntity.ownerId,
            aggroRange,
            targetPriority,
            attacker);
    }

    protected void SetTarget(SelectableEntity target)
    {
        if (currentTarget != null && !currentTarget)
        {
            currentTarget = null;
            currentTargetHealth = null;
        }

        if (target == currentTarget)
            return;

        SelectableEntity previous = currentTarget;
        currentTarget = target;
        currentTargetHealth =
            target != null ? target.GetComponent<EntityHealth>() : null;

        MobileCombatAI mobileAI = GetComponent<MobileCombatAI>();
        mobileAI?.LogTargetChange(previous, target);

        OnTargetChanged();
    }

    protected virtual void OnTargetChanged() { }

    protected bool HasValidTarget()
    {
        return currentTarget != null &&
               currentTargetHealth != null &&
               currentTargetHealth.IsAlive;
    }
}
