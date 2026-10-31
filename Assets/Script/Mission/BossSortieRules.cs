// 출격과 후퇴의 판정은 낮밤 이벤트 순서와 무관하게 한 곳에서 관리한다.
public sealed class BossSortieRules
{
    public enum Phase { Nest, Assault, Retreat }
    public Phase State { get; private set; }
    public int LastSortieNight { get; private set; } = -1;
    float departureDamage;
    float retreatDamage;

    public void Tick(bool isNight, int night, int firstNight, bool engaged,
        bool arrived, float totalDamage, float maxHealth, float retreatFraction)
    {
        if (State == Phase.Assault && (!isNight || totalDamage - departureDamage >= retreatDamage))
            State = Phase.Retreat;
        if (State == Phase.Retreat && arrived)
            State = Phase.Nest;
        if (State == Phase.Nest && isNight && night >= firstNight &&
            night > LastSortieNight && !engaged)
        {
            State = Phase.Assault;
            LastSortieNight = night;
            departureDamage = totalDamage;
            retreatDamage = maxHealth * retreatFraction;
        }
    }
}
