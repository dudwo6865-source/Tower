using System;
using UnityEngine;

[Serializable]
public class StageClearRewards
{
    [Label("마석 획득 목표"), Min(0)]
    [Tooltip("이번 판에서 실제 획득한 마석의 누적량이 이 값 이상이면 별 하나를 획득합니다. 시작 마석은 제외하며 소비해도 누적량은 줄지 않습니다.")]
    public int manaStoneTarget = 100;

    [Label("적 스포너 파괴 목표"), Min(0)]
    [Tooltip("이번 판에서 파괴한 적 스포너 수가 이 값 이상이면 별 하나를 획득합니다.")]
    public int destroyedSpawnerTarget = 3;

    [Label("생존 웨이브 목표"), Min(0)]
    [Tooltip("밤을 끝까지 버틴 횟수가 이 값 이상이면 별 하나를 획득합니다. 진행 중인 웨이브는 제외합니다.")]
    public int survivedWaveTarget = 5;

    [Label("별 1개 최초 달성 보너스"), Min(0)]
    public int firstStarCredits = 100;
    [Label("별 2개 최초 달성 보너스"), Min(0)]
    public int secondStarCredits = 50;
    [Label("별 3개 최초 달성 보너스"), Min(0)]
    public int thirdStarCredits = 50;

    [Label("별 0개 클리어 보상"), Min(0)]
    public int zeroStarRepeatCredits = 0;
    [Label("별 1개 반복 보상"), Min(0)]
    public int oneStarRepeatCredits = 20;
    [Label("별 2개 반복 보상"), Min(0)]
    public int twoStarRepeatCredits = 30;
    [Label("별 3개 반복 보상"), Min(0)]
    public int threeStarRepeatCredits = 40;

    public bool IsManaStoneGoalMet(float earned) => earned >= Mathf.Max(0, manaStoneTarget);
    public bool IsSpawnerGoalMet(int destroyed) => destroyed >= Mathf.Max(0, destroyedSpawnerTarget);
    public bool IsWaveGoalMet(int survived) => survived >= Mathf.Max(0, survivedWaveTarget);

    public int EvaluateStars(float earnedManaStone, int destroyedSpawners, int survivedWaves)
    {
        return (IsManaStoneGoalMet(earnedManaStone) ? 1 : 0)
            + (IsSpawnerGoalMet(destroyedSpawners) ? 1 : 0)
            + (IsWaveGoalMet(survivedWaves) ? 1 : 0);
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
            : stars == 2 ? twoStarRepeatCredits : stars == 1 ? oneStarRepeatCredits : zeroStarRepeatCredits);
    }
}
