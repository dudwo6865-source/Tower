using System;
using UnityEngine;

[Serializable]
public class StageClearRewards
{
    public struct Evaluation
    {
        public double TimeScore { get; private set; }
        public double SpawnerScore { get; private set; }
        public double ManaStoneScore { get; private set; }
        public double TotalScore => TimeScore + SpawnerScore + ManaStoneScore;
        public int Stars { get; private set; }

        public Evaluation(double timeScore, double spawnerScore, double manaStoneScore, int stars)
        {
            TimeScore = timeScore;
            SpawnerScore = spawnerScore;
            ManaStoneScore = manaStoneScore;
            Stars = stars;
        }
    }

    [Header("클리어 점수")]
    [Label("기준 점수"), Min(1)]
    [Tooltip("이 점수의 50% 이상이면 1성, 75% 이상이면 2성, 100% 이상이면 3성입니다. 50% 미만은 0성입니다.")]
    public int targetScore = 1000;
    [Label("게임 시간 1초당 점수"), Min(0)]
    [Tooltip("일시정지를 제외한 게임 시간에 곱합니다. 오래 플레이할수록 점수가 증가합니다.")]
    public int pointsPerSecond = 1;
    [Label("스포너 1개당 점수"), Min(0)]
    public int pointsPerSpawner = 100;
    [Label("마석 1개당 점수"), Min(0)]
    [Tooltip("시작 마석을 제외한 실제 누적 획득량에 곱합니다. 마석을 소비해도 획득 점수는 줄지 않습니다.")]
    public int pointsPerManaStone = 1;

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

    public int TargetScore => Mathf.Max(1, targetScore);
    public double OneStarScore => Math.Ceiling(TargetScore * 0.5);
    public double TwoStarScore => Math.Ceiling(TargetScore * 0.75);

    // 항목별 소수점 이하는 버리고 합산한다. 승리 방식과 무관한 동일한 산식을 사용한다.
    public Evaluation Evaluate(float playTime, int destroyedSpawners, float earnedManaStone)
    {
        double time = ToScore(playTime, pointsPerSecond);
        double spawners = ToScore(destroyedSpawners, pointsPerSpawner);
        double mana = ToScore(earnedManaStone, pointsPerManaStone);
        double total = time + spawners + mana;
        int stars = GetStarsForScore(total);
        return new Evaluation(time, spawners, mana, stars);
    }

    public int GetStarsForScore(double score)
    {
        return score >= TargetScore ? 3 : score >= TwoStarScore ? 2 : score >= OneStarScore ? 1 : 0;
    }

    static double ToScore(double amount, int weight)
    {
        if (double.IsNaN(amount) || double.IsInfinity(amount)) return 0;
        return Math.Floor(Math.Max(0, amount) * Math.Max(0, weight));
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
