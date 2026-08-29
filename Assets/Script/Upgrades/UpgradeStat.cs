// 게임 내 한정 업그레이드가 강화하는 스탯 종류입니다.
// 유닛용과 건물용을 구분합니다.
public enum UpgradeStat
{
    // 유닛
    UnitAttackDamage,
    UnitAttackSpeed,
    UnitMoveSpeed,
    UnitMaxHealth,

    // 건물
    BuildingAttackDamage,
    BuildingMaxHealth,
    BuildingSpawnCount,
}

public static class UpgradeStatExtensions
{
    // 건물용 스탯이면 true입니다.
    public static bool IsBuildingStat(this UpgradeStat stat)
    {
        return stat == UpgradeStat.BuildingAttackDamage ||
               stat == UpgradeStat.BuildingMaxHealth ||
               stat == UpgradeStat.BuildingSpawnCount;
    }
}
