public enum CombatTargetPriority
{
    Nearest,
    UnitsFirst,
    BuildingsFirst,

    /// <summary>
    /// 현재 아군(같은 ownerId 유닛·건물)을 공격 중인 적을 우선합니다.
    /// </summary>
    AttackersOfAlliesFirst
}
