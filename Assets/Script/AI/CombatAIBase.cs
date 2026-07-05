using UnityEngine;

[RequireComponent(typeof(SelectableEntity))]
[RequireComponent(typeof(UnitAttacker))]
public abstract class CombatAIBase : MonoBehaviour
{
    [Header("Combat")]
    [Tooltip("이 범위 안의 적을 자동으로 탐지해 교전합니다.")]
    public float aggroRange = 12f;

    [Tooltip("교전 대상 선택 우선순위입니다.")]
    public CombatTargetPriority targetPriority = CombatTargetPriority.Nearest;

    [Tooltip("목표를 다시 탐색하는 간격(초)입니다.")]
    public float retargetInterval = 0.5f;

    protected UnitAttacker attacker;
    protected SelectableEntity selfEntity;

    protected SelectableEntity currentTarget;
    protected EntityHealth currentTargetHealth;

    private float retargetTimer;

    public SelectableEntity CurrentTarget => currentTarget;

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

        UnitCombatAI combatAI = GetComponent<UnitCombatAI>();
        combatAI?.LogTargetChange(previous, target);

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
