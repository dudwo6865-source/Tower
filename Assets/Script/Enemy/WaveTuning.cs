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
    [Label("스폰 수 배율")]
    [Tooltip("한 번에 스폰할 적 수 배율입니다.")]
    public float spawnCountMultiplier = 1f;

    [Label("스폰 수 추가")]
    [Tooltip("배율을 적용한 뒤 더할 적 수입니다.")]
    public int spawnCountBonus;

    [Label("스폰 간격 배율")]
    [Tooltip("스폰 간격 배율입니다. 1보다 작을수록 더 자주 스폰합니다.")]
    public float spawnIntervalMultiplier = 1f;

    [Label("최대 생존 수")]
    [Tooltip("이 웨이브에 스포너 하나가 동시에 유지할 최대 생존 적 수(절댓값)입니다. " +
             "0이면 스포너 인스펙터의 '최대 생존 적 수'를 그대로 씁니다. " +
             "'이후 웨이브 증가율'에서는 웨이브마다 더할 수입니다.")]
    [Min(0)]
    public int maxAliveEnemies;

    [Header("적 스탯 가중치")]
    [Label("체력 배율")]
    [Tooltip("스폰되는 적의 최대 체력 배율입니다.")]
    public float healthMultiplier = 1f;

    [Label("공격력 배율")]
    [Tooltip("스폰되는 적의 공격력 배율입니다.")]
    public float damageMultiplier = 1f;

    [Label("이동 속도 배율")]
    [Tooltip("스폰되는 적의 이동 속도 배율입니다.")]
    public float speedMultiplier = 1f;

    // 인스펙터 제목은 WaveEnemyCompositionDrawer가 한글로 그린다.
    // (이 필드에 [Label]을 붙이면 그 드로어가 쓰이지 않는다)
    [Tooltip("이 웨이브 동안 스포너 하나가 내보낼 적의 종류와 마리 수(총량)입니다.\n" +
             "비워두면 스포너 인스펙터의 '적 프리팹 목록'과 '1회 스폰 수'로 계속 스폰합니다.\n" +
             "채우면 목록 순서대로 스폰 간격마다 '1회 스폰 수'씩 나눠 내보내고, " +
             "다 내보내면 그 웨이브의 주기 스폰을 멈춥니다.\n" +
             "표에 없는 이후 웨이브는 마지막 웨이브의 구성을 그대로 씁니다.")]
    public WaveEnemyComposition enemyComposition = new WaveEnemyComposition();

    /// <summary>웨이브 표에 적 구성이 하나라도 들어 있는지 여부입니다.</summary>
    public bool HasEnemyComposition => enemyComposition != null && enemyComposition.HasAny;

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
        maxAliveEnemies = source.maxAliveEnemies;
        healthMultiplier = source.healthMultiplier;
        damageMultiplier = source.damageMultiplier;
        speedMultiplier = source.speedMultiplier;
        enemyComposition = source.enemyComposition != null
            ? source.enemyComposition.Clone()
            : new WaveEnemyComposition();
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
            maxAliveEnemies = Mathf.Max(0, maxAliveEnemies),
            healthMultiplier = Mathf.Max(0.01f, healthMultiplier),
            damageMultiplier = Mathf.Max(0f, damageMultiplier),
            speedMultiplier = Mathf.Max(0.01f, speedMultiplier),
            enemyComposition = enemyComposition != null
                ? enemyComposition.Clone()
                : new WaveEnemyComposition()
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
        // 최대 생존 수는 절댓값이라 증가율을 웨이브마다 더한다.
        // 0(스포너 값 사용)이면 더할 기준이 없으므로 그대로 둔다.
        if (result.maxAliveEnemies > 0)
            result.maxAliveEnemies = Mathf.Max(1, result.maxAliveEnemies + growth.maxAliveEnemies * times);
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
            // 최대 생존 수는 절댓값이라 곱하지 않고, b에 값이 있으면 b로 덮어쓴다.
            maxAliveEnemies = b.maxAliveEnemies > 0 ? b.maxAliveEnemies : a.maxAliveEnemies,
            healthMultiplier = a.healthMultiplier * b.healthMultiplier,
            damageMultiplier = a.damageMultiplier * b.damageMultiplier,
            speedMultiplier = a.speedMultiplier * b.speedMultiplier,
            // 적 구성은 웨이브 표(a)에만 있다. 밤 보정(b)은 구성을 바꾸지 않는다.
            enemyComposition = a.enemyComposition != null
                ? a.enemyComposition.Clone()
                : new WaveEnemyComposition()
        };
    }

    /// <summary>에디터 미리보기용 한 줄 요약입니다.</summary>
    public string ToShortSummary()
    {
        string count = spawnCountBonus != 0
            ? $"스폰 x{spawnCountMultiplier:0.##}{(spawnCountBonus > 0 ? "+" : "")}{spawnCountBonus}"
            : $"스폰 x{spawnCountMultiplier:0.##}";

        string cap = maxAliveEnemies > 0 ? $" / 상한 {maxAliveEnemies}" : string.Empty;

        string composition = HasEnemyComposition
            ? $" / 구성 {enemyComposition.ToSummary()}"
            : string.Empty;

        return $"{count} / 간격 x{spawnIntervalMultiplier:0.##}{cap} / " +
               $"체력 x{healthMultiplier:0.##} / 공격 x{damageMultiplier:0.##} / 속도 x{speedMultiplier:0.##}" +
               composition;
    }
}

/// <summary>적 구성의 한 줄입니다. (적 프리팹 + 마리 수)</summary>
[Serializable]
public class WaveEnemyEntry
{
    [Label("적 프리팹")]
    [Tooltip("스폰할 적 프리팹입니다.")]
    public GameObject prefab;

    [Label("마리 수")]
    [Tooltip("이 웨이브 동안 스포너 하나가 내보낼 이 적의 수입니다.")]
    [Min(0)]
    public int count = 1;

    public WaveEnemyEntry()
    {
    }

    public WaveEnemyEntry(WaveEnemyEntry source)
    {
        if (source == null)
            return;

        prefab = source.prefab;
        count = Mathf.Max(0, source.count);
    }
}

/// <summary>
/// 한 웨이브에 스포너 하나가 내보낼 적 종류와 마리 수(총량)입니다.
/// 목록 순서대로 스폰합니다. 섞어서 내보내고 싶으면 줄을 나눠 적습니다.
/// (예: 고블린 2 → 오크 1 → 고블린 2)
/// </summary>
[Serializable]
public class WaveEnemyComposition
{
    public List<WaveEnemyEntry> entries = new List<WaveEnemyEntry>();

    /// <summary>프리팹과 1 이상의 수가 들어 있는 줄이 하나라도 있는지 여부입니다.</summary>
    public bool HasAny => TotalCount > 0;

    /// <summary>스포너 하나가 이 웨이브에 내보낼 총 마리 수입니다.</summary>
    public int TotalCount
    {
        get
        {
            if (entries == null)
                return 0;

            int total = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                WaveEnemyEntry entry = entries[i];

                if (entry != null && entry.prefab != null && entry.count > 0)
                    total += entry.count;
            }

            return total;
        }
    }

    public WaveEnemyComposition Clone()
    {
        WaveEnemyComposition clone = new WaveEnemyComposition();

        if (entries == null)
            return clone;

        for (int i = 0; i < entries.Count; i++)
            clone.entries.Add(new WaveEnemyEntry(entries[i]));

        return clone;
    }

    /// <summary>스폰 순서대로 프리팹을 펼쳐 담습니다. (고블린 2, 오크 1 → 고블린, 고블린, 오크)</summary>
    public void BuildSpawnOrder(List<GameObject> result)
    {
        result.Clear();

        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            WaveEnemyEntry entry = entries[i];

            if (entry == null || entry.prefab == null)
                continue;

            for (int n = 0; n < entry.count; n++)
                result.Add(entry.prefab);
        }
    }

    /// <summary>에디터 표시용 요약입니다. (예: "고블린 4 · 오크 1")</summary>
    public string ToSummary()
    {
        if (entries == null)
            return string.Empty;

        List<string> parts = new List<string>();

        for (int i = 0; i < entries.Count; i++)
        {
            WaveEnemyEntry entry = entries[i];

            if (entry == null || entry.prefab == null || entry.count <= 0)
                continue;

            parts.Add($"{entry.prefab.name} {entry.count}");
        }

        return string.Join(" · ", parts);
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
    [Label("웨이브별 수치")]
    [Tooltip("웨이브별 수치입니다. 첫 번째 항목 = 웨이브 1.")]
    public List<WaveTuning> waves = new List<WaveTuning> { new WaveTuning() };

    [Label("이후 웨이브 증가율")]
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
