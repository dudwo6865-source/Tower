using System;

public interface IDamageable
{
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsAlive { get; }

    void TakeDamage(float damage);

    event Action<float, float> OnHealthChanged;
    event Action OnDied;
}
