using UnityEngine;

// 유물 하나의 정적 데이터입니다. 기획서 5~9장의 표 한 줄이 에셋 하나에 대응합니다.
// Tools 메뉴 대신 프로젝트 창에서 우클릭 > Create > Tank > Relic Definition으로 만듭니다.
[CreateAssetMenu(fileName = "RelicDefinition", menuName = "Tank/Relic Definition")]
public class RelicDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("유물을 구분하는 고유 ID입니다. 기획서 ID(T01, U01 등)를 그대로 쓰는 것을 권장합니다.")]
    public string relicId = "T01";

    [Tooltip("유물이 속한 계열입니다.")]
    public RelicSeries series = RelicSeries.Tower;

    [Header("Display")]
    [Tooltip("후보 선택/장착 슬롯 UI에 표시할 이름입니다.")]
    public string displayName = "새 유물";

    [TextArea]
    [Tooltip("유물 효과 설명입니다. 툴팁 등에 그대로 표시하면 됩니다.")]
    public string description;

    [Tooltip("유물 아이콘입니다.")]
    public Sprite icon;

    [Header("Effect")]
    [Tooltip("이 유물이 강화하는 대상입니다.")]
    public RelicEffectType effectType = RelicEffectType.TowerAttackDamage;

    [Tooltip("수치 적용 방식입니다.\n" +
        "- Flat: 절댓값으로 더함(예: 유닛 상한 +2)\n" +
        "- Percent: 기본값 대비 퍼센트로 더함(예: 20 = +20%)\n" +
        "  비용을 깎는 효과(건설비, 연구비 등)는 음수를 입력하세요(예: -15 = 15% 할인).")]
    public RelicValueMode valueMode = RelicValueMode.Percent;

    [Tooltip("적용할 수치입니다. valueMode에 따라 의미가 달라집니다.")]
    public float value = 20f;
}
