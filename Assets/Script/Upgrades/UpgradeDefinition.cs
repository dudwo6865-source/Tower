using UnityEngine;

// 업그레이드 수치의 적용 방식입니다.
public enum UpgradeValueMode
{
    // 절댓값(고정 수치)으로 더합니다. 예: 공격력 +3
    Flat,

    // 퍼센트로 기본값에 비례해 더합니다. 예: 공격력 +10% (bonusPerLevel = 10)
    Percent,
}

// 게임 내 한정 업그레이드 하나의 정의입니다. (스타크래프트식 글로벌 업그레이드)
// 레벨당 절댓값 또는 퍼센트로 강화되며, 마석으로 구매합니다.
[CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Tank/업그레이드 정의")]
public class UpgradeDefinition : ScriptableObject
{
    [Header("대상")]
    [Label("강화 스탯")]
    [Tooltip("이 업그레이드가 강화하는 스탯입니다.")]
    public UpgradeStat stat = UpgradeStat.UnitAttackDamage;

    [Label("수치 방식")]
    [Tooltip("수치 적용 방식입니다.\n- Flat: 절댓값으로 더함(예: +3)\n- Percent: 기본값 대비 퍼센트로 더함(예: 10 = +10%)")]
    public UpgradeValueMode valueMode = UpgradeValueMode.Flat;

    [Header("표시")]
    [Label("표시 이름")]
    [Tooltip("업그레이드 버튼에 표시할 이름입니다.")]
    public string displayName = "공격력 강화";

    [Label("설명")]
    [TextArea]
    [Tooltip("설명입니다.")]
    public string description;

    [Label("아이콘")]
    [Tooltip("업그레이드 버튼 아이콘입니다.")]
    public Sprite icon;

    [Header("레벨")]
    [Label("최대 레벨")]
    [Tooltip("최대 강화 레벨입니다.")]
    public int maxLevel = 3;

    [Label("레벨당 보너스")]
    [Tooltip("레벨당 강화 수치입니다.\n" +
             "- Flat 모드: 해당 수치만큼 절댓값 + (공격력/체력/이동속도/스폰수 등)\n" +
             "- Percent 모드: 기본값 대비 퍼센트 + (예: 10 = 레벨당 +10%)\n" +
             "  공격속도는 초당 공격 횟수를 기준으로 적용됩니다.")]
    public float bonusPerLevel = 3f;

    [Header("비용")]
    [Label("기본 비용")]
    [Tooltip("1레벨 구매 비용(마석)입니다.")]
    public int baseCost = 40;

    [Label("레벨당 추가 비용")]
    [Tooltip("레벨이 오를수록 추가되는 비용입니다. n레벨 비용 = baseCost + costPerLevel * (현재레벨).")]
    public int costPerLevel = 30;

    [Header("연구")]
    [Label("연구 시간(초)")]
    [Tooltip("다음 레벨 연구에 걸리는 시간(초)입니다. 0이면 구매 즉시 완료됩니다.")]
    public float researchDuration = 10f;

    // 현재 레벨에서 다음 레벨로 올릴 때의 비용입니다.
    public int GetCostForNextLevel(int currentLevel)
    {
        return baseCost + costPerLevel * Mathf.Max(0, currentLevel);
    }

    // 현재 레벨에서 다음 레벨 연구에 걸리는 시간(초)입니다.
    public float GetResearchDurationForNextLevel(int currentLevel)
    {
        return Mathf.Max(0f, researchDuration);
    }
}
