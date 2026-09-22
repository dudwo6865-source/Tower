// 유물 하나의 현재 상태입니다. (기획서 4장 "유물 데이터 상태")
public enum RelicState
{
    // 현재 후보로 등장할 수 있음
    Available,

    // 현재 5개 슬롯 중 하나에 장착됨
    Owned,

    // 교체로 버려져 해당 스테이지에서 재등장하지 않음
    Destroyed,
}
