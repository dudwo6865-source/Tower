using System;
using System.Collections.Generic;

// 유니티 오브젝트 수명과 분리해 알의 중복 처리, 알별 부화 시간, 파괴 목표를 관리한다.
public sealed class EggMissionRules
{
    public enum EggState { Waiting, Destroyed, Hatched, Hatching }
    readonly List<EggState> states = new List<EggState>();
    readonly List<float> remaining = new List<float>();
    public int TargetDestroyCount { get; }
    public float HatchSeconds { get; }
    public int TotalEggs => states.Count;
    public int DestroyedEggs { get; private set; }
    public int HatchedEggs { get; private set; }
    public int HatchingEggs { get; private set; }
    public int WaitingEggs { get; private set; }
    public bool Complete => DestroyedEggs >= TargetDestroyCount;

    public EggMissionRules(int targetDestroyCount, float hatchSeconds)
    {
        if (targetDestroyCount < 1) throw new ArgumentException("목표 파괴 수는 1 이상이어야 합니다.");
        if (float.IsNaN(hatchSeconds) || float.IsInfinity(hatchSeconds) || hatchSeconds <= 0f)
            throw new ArgumentException("알 부화 시간은 0보다 큰 유한한 값이어야 합니다.");
        TargetDestroyCount = targetDestroyCount;
        HatchSeconds = hatchSeconds;
    }

    // 새 알을 등록하고 부화 시간을 처음부터 센다.
    public int AddEgg()
    {
        states.Add(EggState.Waiting);
        remaining.Add(HatchSeconds);
        WaitingEggs++;
        return states.Count - 1;
    }

    public EggState GetState(int index) => states[index];
    public float GetRemaining(int index) => states[index] == EggState.Waiting ? remaining[index] : 0f;
    public float GetProgress(int index) => states[index] == EggState.Waiting
        ? Math.Min(1f, Math.Max(0f, 1f - remaining[index] / HatchSeconds)) : 1f;

    // 부화 대기 중이거나 부화 연출 중인 알을 파괴(또는 부화 완료)로 확정한다.
    public bool ResolveEgg(int index, bool hatched)
    {
        if (index < 0 || index >= states.Count) return false;
        EggState state = states[index];
        if (state != EggState.Waiting && state != EggState.Hatching) return false;
        if (state == EggState.Waiting) WaitingEggs--;
        else HatchingEggs--;
        states[index] = hatched ? EggState.Hatched : EggState.Destroyed;
        if (hatched) HatchedEggs++;
        else DestroyedEggs++;
        return true;
    }

    public bool BeginHatch(int index)
    {
        if (index < 0 || index >= states.Count || states[index] != EggState.Waiting) return false;
        states[index] = EggState.Hatching;
        WaitingEggs--;
        HatchingEggs++;
        return true;
    }

    // 대기 중인 알의 시간을 줄이고, 부화 시간이 끝난 알의 번호를 expired에 담는다.
    public void Tick(float seconds, List<int> expired)
    {
        expired.Clear();
        if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i] != EggState.Waiting) continue;
            remaining[i] = Math.Max(0f, remaining[i] - seconds);
            if (remaining[i] <= 0f) expired.Add(i);
        }
    }

    // 가장 먼저 부화할 알의 남은 시간. 대기 중인 알이 없으면 -1이다.
    public float SoonestRemaining()
    {
        float best = -1f;
        for (int i = 0; i < states.Count; i++)
            if (states[i] == EggState.Waiting && (best < 0f || remaining[i] < best)) best = remaining[i];
        return best;
    }
}
