// 유물이 강화할 수 있는 대상 스탯입니다.
// RelicManager가 RelicDefinition.valueMode(Flat/Percent)와 함께 해석합니다.
// 비용을 깎는 계열(BuildWattCost, ResearchManaCost)은 할인 효과이므로
// 해당 RelicDefinition의 value를 음수로 입력합니다(예: -15 = 15% 할인).
//
// 아래 목록은 기획서 5~9장의 "2차: 단순 수치 Modifier 유물" 15종만 담고 있습니다.
// 상태/이벤트형 효과(둔화, 치명타, 넉백, 자동 회복, 반사 피해 등)는 3차 이후에
// 별도 구조(전투 이벤트 훅)로 추가할 예정입니다.
public enum RelicEffectType
{
    // --- 타워 ---
    TowerAttackDamage,   // T01 강화 탄두: 모든 공격 타워 피해량 +20%
    TowerAttackSpeed,    // T02 고속 구동축: 모든 공격 타워 공격속도 +15%
    TowerAttackRange,    // T03 고배율 조준경: 모든 공격 타워 사거리 +20%

    // --- 유닛 ---
    UnitAttackDamage,    // U01 강화 무장: 모든 유닛 공격력 +20%
    UnitMaxHealth,       // U02 강화 장갑: 모든 유닛 최대 체력 +25%
    UnitMoveSpeed,       // U03 경량 장비: 모든 유닛 이동속도 +20%
    ProductionSpeed,     // U04 자동 생산 모듈: 생산 건물의 생산속도 +25%
    UnitCapPerBuilding,  // U05 확장 병영: 생산 건물 1개당 동시 보유 유닛 상한 +2

    // --- Watt / 경제 ---
    WattIncome,          // W01 고효율 발전기: Watt 생산속도 +20%
    WattMaxCapacity,     // W02 대용량 축전지: 최대 Watt 보유량 +30%
    BuildWattCost,       // W03 절약형 건축법: 모든 건설 Watt 비용 -15%

    // --- 마석 ---
    ManaStoneGain,       // C01 마석 추출장치: 적 처치 시 획득 마석 +20%
    ResearchManaCost,    // C02 정제 기술: 글로벌 연구의 마석 비용 -15%

    // --- 특수 / 방어 ---
    BuildingMaxHealth,   // S02 강화 콘크리트: 모든 아군 건물 최대 체력 +25%
    WallMaxHealth,       // S03 강화 장벽: 벽 계열 건물 최대 체력 +50% (Building.isWallCategory 필요)
}
