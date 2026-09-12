using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 웨이브 동안 EnemySpawner에 적용할 보정값입니다.
/// 스포너 인스펙터에 적어둔 값을 '기준값'으로 두고, 여기에 곱하거나 더해서 실제 값을 만듭니다.
/// 그래서 웨이브가 바뀌어도 스포너 원본 설정은 그대로 남습니다.
/// </summary>
[Serializable]
public class WaveTuning
{
    [Header("스폰량")]
    [Tooltip("한 번에 스폰할 적 수 배율입니다.")]
    public float spawnCountMultiplier = 1f;

    [Tooltip("배율을 적용한 뒤 더할 적 수입니다.")]
    public int spawnCountBonus;

    [Tooltip("스폰 간격 배율입니다. 1보다 작을수록 더 자주 스폰합니다.")]
    public float spawnIntervalMultiplier = 1f;

    [Tooltip("동시 생존 수 상한 배율입니다. 스포너 상한이 0(무제한)이면 무시됩니다.")]
    public float maxAliveMultiplier = 1f;

    [Header("적 스탯 가중치")]
    [Tooltip("스폰되는 적의 최대 체력 배율입니다.")]
    public float healthMultiplier = 1f;

    [Tooltip("스폰되는 적의 공격력 배율입니다.")]
    public float damageMultiplier = 1f;

    [Tooltip("스폰되는 적의 이동 속도 배율입니다.")]
    public float speedMultiplier = 1f;

    public WaveTuning()
    {
    }

    public WaveTuning(WaveTuning source)
    {
        CopyFrom(source);
    }

    public void CopyFrom(WaveTuning source)
    {
        if (source == null)
            return;

        spawnCountMultiplier = source.spawnCountMultiplier;
        spawnCountBonus = source.spawnCountBonus;
        spawnIntervalMultiplier = source.spawnIntervalMultiplier;
        maxAliveMultiplier = source.maxAliveMultiplier;
        healthMultiplier = source.healthMultiplier;
        damageMultiplier = source.damageMultiplier;
        speedMultiplier = source.speedMultiplier;
    }

    /// <summary>0이나 음수처럼 게임을 멈추게 하는 값을 안전한 범위로 맞춘 사본입니다.</summary>
    public WaveTuning Sanitized()
    {
        return new WaveTuning
        {
            spawnCountMultiplier = Mathf.Max(0f, spawnCountMultiplier),
            spawnCountBonus = spawnCountBonus,
            // 0 이하가 되면 스폰 간격이 사라져 한 프레임에 무한 스폰한다.
            spawnIntervalMultiplier = Mathf.Max(0.01f, spawnIntervalMultiplier),
            maxAliveMultiplier = Mathf.Max(0f, maxAliveMultiplier),
            healthMultiplier = Mathf.Max(0.01f, healthMultiplier),
            damageMultiplier = Mathf.Max(0f, damageMultiplier),
            speedMultiplier = Mathf.Max(0.01f, speedMultiplier)
        };
    }

    /// <summary>
    /// baseTuning에 growth를 times번 복리로 적용한 값입니다.
    /// 배율은 거듭제곱으로, 가산값은 곱한 횟수만큼 더해서 누적합니다.
    /// </summary>
    public static WaveTuning Compound(WaveTuning baseTuning, WaveTuning growth, int times)
    {
        WaveTuning result = new WaveTuning(baseTuning ?? new WaveTuning());

        if (growth == null || times <= 0)
            return result;

        result.spawnCountMultiplier *= Mathf.Pow(growth.spawnCountMultiplier, times);
        result.spawnCountBonus += growth.spawnCountBonus * times;
        result.spawnIntervalMultiplier *= Mathf.Pow(growth.spawnIntervalMultiplier, times);
        result.maxAliveMultiplier *= Mathf.Pow(growth.maxAliveMultiplier, times);
        result.healthMultiplier *= Mathf.Pow(growth.healthMultiplier, times);
        result.damageMultiplier *= Mathf.Pow(growth.damageMultiplier, times);
        result.speedMultiplier *= Mathf.Pow(growth.speedMultiplier, times);

        return result;
    }

    /// <summary>
    /// 웨이브 표와 밤 보정으로 해당 웨이브의 최종 보정을 만듭니다.
    /// WaveManager(런타임)와 스테이지 에디터(미리보기)가 같은 식을 쓰도록 여기 한 곳에 둡니다.
    /// </summary>
    public static WaveTuning BuildForWave(
        WavePlan plan,
        WaveTuning nightBonus,
        bool applyNightBonus,
        int waveNumber,
        bool night)
    {
        WaveTuning waveTuning = plan != null
            ? plan.Evaluate(waveNumber)
            : new WaveTuning();

        if (!night || !applyNightBonus)
            return waveTuning.Sanitized();

        return Combine(waveTuning, nightBonus).Sanitized();
    }

    /// <summary>두 보정을 곱해서 합칩니다. (예: 웨이브 수치 × 밤 보정)</summary>
    public static WaveTuning Combine(WaveTuning a, WaveTuning b)
    {
        if (a == null)
            return new WaveTuning(b);

        if (b == null)
            return new WaveTuning(a);

        return new WaveTuning
        {
            spawnCountMultiplier = a.spawnCountMultiplier * b.spawnCountMultiplier,
            spawnCountBonus = a.spawnCountBonus + b.spawnCountBonus,
            spawnIntervalMultiplier = a.spawnIntervalMultiplier * b.spawnIntervalMultiplier,
            maxAliveMultiplier = a.maxAliveMultiplier * b.maxAliveMultiplier,
            healthMultiplier = a.healthMultiplier * b.healthMultiplier,
            damageMultiplier = a.damageMultiplier * b.damageMultiplier,
            speedMultiplier = a.speedMultiplier * b.speedMultiplier
        };
    }

    /// <summary>에디터 미리보기용 한 줄 요약입니다.</summary>
    public string ToShortSummary()
    {
        string count = spawnCountBonus != 0
            ? $"스폰 x{spawnCountMultiplier:0.##}{(spawnCountBonus > 0 ? "+" : "")}{spawnCountBonus}"
            : $"스폰 x{spawnCountMultiplier:0.##}";

        return $"{count} / 간격 x{spawnIntervalMultiplier:0.##} / " +
               $"체력 x{healthMultiplier:0.##} / 공격 x{damageMultiplier:0.##} / 속도 x{speedMultiplier:0.##}";
    }
}

/// <summary>
/// 웨이브별 수치 표입니다. 표에 적은 웨이브까지는 그 값을 그대로 쓰고,
/// 그 뒤 웨이브는 마지막 값에 증가율을 복리로 적용해 계속 이어갑니다.
/// 덕분에 웨이브 100개를 일일이 적지 않아도 무한히 이어지는 난이도 곡선이 됩니다.
/// </summary>
[Serializable]
public class WavePlan
{
    [Tooltip("웨이브별 수치입니다. 첫 번째 항목 = 웨이브 1.")]
    public List<WaveTuning> waves = new List<WaveTuning> { new WaveTuning() };

    [Tooltip("표에 없는 이후 웨이브에 매 웨이브마다 복리로 적용할 증가율입니다. " +
             "전부 1이면 마지막 웨이브 값을 그대로 유지합니다.")]
    public WaveTuning growthPerWaveAfterLast = new WaveTuning();

    public int AuthoredWaveCount => waves != null ? waves.Count : 0;

    /// <summary>waveNumber는 1부터 시작합니다(1 = 첫 번째 웨이브).</summary>
    public WaveTuning Evaluate(int waveNumber)
    {
        if (waves == null || waves.Count == 0)
            return new WaveTuning();

        int index = Mathf.Max(0, waveNumber - 1);
        int lastIndex = waves.Count - 1;

        if (index <= lastIndex)
            return (waves[index] ?? new WaveTuning()).Sanitized();

        WaveTuning last = waves[lastIndex] ?? new WaveTuning();
        return WaveTuning.Compound(last, growthPerWaveAfterLast, index - lastIndex).Sanitized();
    }

    public WavePlan Clone()
    {
        WavePlan clone = new WavePlan
        {
            waves = new List<WaveTuning>(),
            growthPerWaveAfterLast = new WaveTuning(growthPerWaveAfterLast)
        };

        if (waves == null)
            return clone;

        for (int i = 0; i < waves.Count; i++)
            clone.waves.Add(new WaveTuning(waves[i]));

        return clone;
    }
}
