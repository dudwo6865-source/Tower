using System;

// 유니티 오브젝트 수명과 분리해 알의 중복 처리 및 단계별 시간을 관리한다.
public sealed class EggMissionRules
{
    public enum EggState { Waiting, Destroyed, Hatched, Defeated, Hatching }
    readonly EggState[] states;
    readonly float[] durations;
    public int TotalEggs => states.Length;
    public int ResolvedEggs { get; private set; }
    public int RemainingEggs => TotalEggs - ResolvedEggs;
    public int LivingMonsters { get; private set; }
    public int HatchingEggs { get; private set; }
    public float RemainingSeconds { get; private set; }
    public float StageSeconds => durations[Math.Min(ResolvedEggs, durations.Length - 1)];
    public bool Complete => RemainingEggs == 0 && LivingMonsters == 0 && HatchingEggs == 0;

    public static string ValidateDurations(float[] values, int eggCount)
    {
        if (eggCount <= 0) return "활성 몬스터 알을 하나 이상 배치하세요.";
        if (values == null || values.Length < eggCount) return "알 개수 이상의 단계별 제한 시간을 입력하세요.";
        for (int i = 0; i < values.Length; i++)
        {
            if (float.IsNaN(values[i]) || float.IsInfinity(values[i]) || values[i] <= 0f)
                return "제한 시간은 0보다 큰 유한한 값이어야 합니다.";
            if (i > 0 && values[i] >= values[i - 1])
                return "다음 단계의 제한 시간은 이전 단계보다 짧아야 합니다.";
        }
        return null;
    }

    public EggMissionRules(int eggCount, float[] stageSeconds)
    {
        string error = ValidateDurations(stageSeconds, eggCount);
        if (error != null) throw new ArgumentException(error);
        states = new EggState[eggCount];
        durations = (float[])stageSeconds.Clone();
        RemainingSeconds = durations[0];
    }

    public EggState GetState(int index) => states[index];

    public bool ResolveEgg(int index, bool hatched)
    {
        if (index >= 0 && index < states.Length && states[index] == EggState.Hatching)
        {
            states[index] = hatched ? EggState.Hatched : EggState.Destroyed;
            HatchingEggs--;
            if (hatched) LivingMonsters++;
            return true;
        }
        if (index < 0 || index >= states.Length || states[index] != EggState.Waiting) return false;
        states[index] = hatched ? EggState.Hatched : EggState.Destroyed;
        ResolvedEggs++;
        if (hatched) LivingMonsters++;
        RemainingSeconds = RemainingEggs > 0 ? StageSeconds : 0f;
        return true;
    }

    public bool BeginHatch(int index)
    {
        if (index < 0 || index >= states.Length || states[index] != EggState.Waiting) return false;
        states[index] = EggState.Hatching;
        HatchingEggs++;
        ResolvedEggs++;
        RemainingSeconds = RemainingEggs > 0 ? StageSeconds : 0f;
        return true;
    }

    public void DefeatMonster(int index)
    {
        if (index < 0 || index >= states.Length || states[index] != EggState.Hatched) return;
        states[index] = EggState.Defeated;
        LivingMonsters--;
    }

    public bool Tick(float seconds, bool paused)
    {
        if (paused || RemainingEggs == 0 || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return false;
        RemainingSeconds = Math.Max(0f, RemainingSeconds - seconds);
        return RemainingSeconds <= 0f;
    }
}
