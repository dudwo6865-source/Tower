using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Tooltip("투사체가 사라지기까지의 최대 생존 시간(초)입니다.")]
    public float maxLifeTime = 5f;

    private SelectableEntity target;
    private SelectableEntity attacker;
    private EntityHealth targetHealth;
    private float damage;
    private float speed;
    private GameObject hitEffectPrefab;
    private Color hitFallbackColor;
    private Vector3 lastKnownPosition;
    private float lifeTimer;

    public void Initialize(
        SelectableEntity target,
        EntityHealth targetHealth,
        float damage,
        float speed,
        GameObject hitEffectPrefab,
        Color hitFallbackColor,
        SelectableEntity attacker = null)
    {
        this.target = target;
        this.targetHealth = targetHealth;
        this.damage = damage;
        this.speed = speed;
        this.hitEffectPrefab = hitEffectPrefab;
        this.hitFallbackColor = hitFallbackColor;
        this.attacker = attacker;

        lastKnownPosition = GetTargetPoint();
    }

    void Update()
    {
        lifeTimer += Time.deltaTime;

        if (lifeTimer >= maxLifeTime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = GetTargetPoint();

        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            speed * Time.deltaTime);

        if ((transform.position - destination).sqrMagnitude <= 0.09f)
            Impact();
    }

    Vector3 GetTargetPoint()
    {
        if (target != null && (targetHealth == null || targetHealth.IsAlive))
        {
            lastKnownPosition = target.SelectionBounds.center;
            return lastKnownPosition;
        }

        return lastKnownPosition;
    }

    void Impact()
    {
        if (targetHealth != null && targetHealth.IsAlive)
            targetHealth.TakeDamage(damage, attacker);

        AttackVisuals.SpawnHitEffect(
            transform.position,
            hitEffectPrefab,
            hitFallbackColor);
        Destroy(gameObject);
    }
}
