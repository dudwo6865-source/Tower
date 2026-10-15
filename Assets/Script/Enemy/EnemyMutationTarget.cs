using UnityEngine;
using UnityEngine.AI;

// 적용했던 배율과의 비율만 반영하여 재적용과 중복 추첨으로 능력치가 곱누적되지 않게 한다.
[DisallowMultipleComponent]
public class EnemyMutationTarget : MonoBehaviour
{
    float healthMultiplier = 1f;
    float damageMultiplier = 1f;
    float speedMultiplier = 1f;

    public void Apply(float health, float damage, float speed)
    {
        EntityHealth targetHealth = GetComponent<EntityHealth>();
        if (targetHealth != null && !targetHealth.IsAlive)
            return;

        if (targetHealth != null && !Mathf.Approximately(healthMultiplier, health))
            targetHealth.ScaleMaxHealthPreservingRatio(health / healthMultiplier);
        UnitAttacker attacker = GetComponent<UnitAttacker>();
        if (attacker != null)
            attacker.attackDamage *= damage / damageMultiplier;
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.speed *= speed / speedMultiplier;

        healthMultiplier = health;
        damageMultiplier = damage;
        speedMultiplier = speed;
    }
}
