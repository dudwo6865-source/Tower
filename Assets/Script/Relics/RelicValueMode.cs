// 유물 수치의 적용 방식입니다. UpgradeValueMode와 같은 개념이지만
// 유물 전용 데이터(RelicDefinition)에서 쓰기 위해 별도로 둡니다.
public enum RelicValueMode
{
    // 절댓값(고정 수치)으로 더합니다. 예: 유닛 상한 +2
    Flat,

    // 퍼센트로 기본값에 비례해 더합니다. 예: 피해량 +20%(value = 20)
    // 비용을 깎는 효과(건설비, 연구비 등)는 value를 음수로 입력합니다. 예: -15
    Percent,
}
