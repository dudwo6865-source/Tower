using System;
using UnityEngine;

[Serializable]
public class StageClearRewards
{
    public enum ClearRoute { Mission, Conquest, Survival }

    public struct Evaluation
    {
        public int Stars { get; private set; }
        public ClearRoute Route { get; private set; }
        public Evaluation(int stars, ClearRoute route) { Stars = stars; Route = route; }
    }

    [Header("토벌 평가")]
    [Label("토벌 2성 제한 시간(초)"), Min(0f)]
    [Tooltip("스포너 전멸 승리를 이 시간 이내에 달성하면 별 2개를 획득합니다.")]
    public float conquestTwoStarSeconds = 600f;
    [Label("토벌 3성 제한 시간(초)"), Min(0f)]
    [Tooltip("스포너 전멸 승리를 이 시간 이내에 달성하면 별 3개를 획득합니다. 2성보다 긴 값은 2성 제한 시간으로 보정합니다.")]
    public float conquestThreeStarSeconds = 300f;

    [Header("방어 평가")]
    [Label("방어 2성 허용 누적 피해"), Min(0f)]
    [Tooltip("생존 승리 시 본부가 받은 실제 누적 피해가 이 값 이하이면 별 2개를 획득합니다. 수리해도 누적 피해는 줄지 않습니다.")]
    public float survivalTwoStarDamage = 500f;
    [Label("방어 3성 허용 누적 피해"), Min(0f)]
    [Tooltip("생존 승리 시 본부 누적 피해가 이 값 이하이면 별 3개를 획득합니다. 0이면 무피해 방어입니다. 2성보다 큰 값은 2성 기준으로 보정합니다.")]
    public float survivalThreeStarDamage = 100f;

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

    public float ConquestTwoStarLimit => Mathf.Max(0f, conquestTwoStarSeconds);
    public float ConquestThreeStarLimit => Mathf.Min(ConquestTwoStarLimit, Mathf.Max(0f, conquestThreeStarSeconds));
    public float SurvivalTwoStarLimit => Mathf.Max(0f, survivalTwoStarDamage);
    public float SurvivalThreeStarLimit => Mathf.Min(SurvivalTwoStarLimit, Mathf.Max(0f, survivalThreeStarDamage));

    // 승리한 판에만 호출한다. 달성한 승리 경로 중 가장 좋은 성적을 선택한다.
    public Evaluation Evaluate(bool conquestCompleted, bool survivalCompleted, float playTime, float damageTaken)
    {
        var best = new Evaluation(1, ClearRoute.Mission);
        if (conquestCompleted)
        {
            int stars = playTime <= ConquestThreeStarLimit ? 3 : playTime <= ConquestTwoStarLimit ? 2 : 1;
            best = new Evaluation(stars, ClearRoute.Conquest);
        }
        if (survivalCompleted)
        {
            int stars = damageTaken <= SurvivalThreeStarLimit ? 3 : damageTaken <= SurvivalTwoStarLimit ? 2 : 1;
            if (!conquestCompleted || stars > best.Stars)
                best = new Evaluation(stars, ClearRoute.Survival);
        }
        return best;
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
