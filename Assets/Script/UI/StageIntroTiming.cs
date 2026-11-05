using System;

// 로딩 완료와 실제 시간만으로 연출 단계를 진행한다. 게임 시간의 일시정지와 분리한다.
public sealed class StageIntroTiming
{
    public enum Phase { Loading, Revealing, Finished }
    public Phase State { get; private set; } = Phase.Loading;
    public float Progress { get; private set; }
    readonly float revealDuration;
    float elapsed;

    public StageIntroTiming(float reveal)
    {
        revealDuration = Math.Max(0f, reveal);
    }

    public void Tick(float delta, bool ready)
    {
        if (State == Phase.Finished) return;
        if (State == Phase.Loading)
        {
            if (ready) Enter(Phase.Revealing);
            return;
        }
        elapsed += Math.Max(0f, delta);
        Progress = revealDuration > 0f ? Math.Min(1f, elapsed / revealDuration) : 1f;
        if (Progress < 1f) return;
        State = Phase.Finished;
    }

    void Enter(Phase phase)
    {
        State = phase;
        elapsed = 0f;
        Progress = 0f;
    }
}
