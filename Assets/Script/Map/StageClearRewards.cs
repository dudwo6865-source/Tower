using System;
using UnityEngine;

[Serializable]
public class StageClearRewards
{
    [Label("별 2개 본부 체력 비율"), Range(0f, 1f)]
    [Tooltip("클리어 순간 본부의 남은 체력 비율이 이 값 이상이면 별 2개를 획득합니다.")]
    public float twoStarHealthRatio = 0.5f;

    [Label("별 3개 본부 체력 비율"), Range(0f, 1f)]
    [Tooltip("별 2개 기준과 이 값 중 높은 값 이상이면 별 3개를 획득합니다.")]
    public float threeStarHealthRatio = 0.8f;

    [Label("별 1개 최초 달성 보너스"), Min(0)]
    public int firstStarCredits = 100;
    [Label("별 2개 최초 달성 보너스"), Min(0)]
    public int secondStarCredits = 50;
    [Label("별 3개 최초 달성 보너스"), Min(0)]
    public int thirdStarCredits = 50;

    [Label("별 1개 반복 보상"), Min(0)]
    public int oneStarRepeatCredits = 20;
    [Label("별 2개 반복 보상"), Min(0)]
    public int twoStarRepeatCredits = 30;
    [Label("별 3개 반복 보상"), Min(0)]
    public int threeStarRepeatCredits = 40;

    public int EvaluateStars(float healthRatio)
    {
        if (healthRatio < 0f) return 1;
        float two = Mathf.Clamp01(twoStarHealthRatio);
        float three = Mathf.Max(two, Mathf.Clamp01(threeStarHealthRatio));
        return healthRatio >= three ? 3 : healthRatio >= two ? 2 : 1;
    }

    public long GetFirstClearBonus(int previousStars, int stars)
    {
        long total = 0;
        if (previousStars < 1 && stars >= 1) total += Mathf.Max(0, firstStarCredits);
        if (previousStars < 2 && stars >= 2) total += Mathf.Max(0, secondStarCredits);
        if (previousStars < 3 && stars >= 3) total += Mathf.Max(0, thirdStarCredits);
        return total;
    }

    public int GetRepeatReward(int stars)
    {
        return Mathf.Max(0, stars >= 3 ? threeStarRepeatCredits
            : stars == 2 ? twoStarRepeatCredits : stars == 1 ? oneStarRepeatCredits : 0);
    }
}
