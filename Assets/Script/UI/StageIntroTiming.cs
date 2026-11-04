using System;

// 로딩 완료와 실제 시간만으로 연출 단계를 진행한다. 게임 시간의 일시정지와 분리한다.
public sealed class StageIntroTiming
{
    public enum Phase { Loading, Revealing, Announcement, Dismissing, Finished }
    public Phase State { get; private set; } = Phase.Loading;
    public float Progress { get; private set; }
    readonly float revealDuration;
    readonly float announcementDuration;
    readonly float dismissDuration;
    float elapsed;

    public StageIntroTiming(float reveal, float announcement, float dismiss)
    {
        revealDuration = Math.Max(0f, reveal);
        announcementDuration = Math.Max(0f, announcement);
        dismissDuration = Math.Max(0f, dismiss);
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
        float duration = State == Phase.Revealing ? revealDuration
            : State == Phase.Announcement ? announcementDuration : dismissDuration;
        Progress = duration > 0f ? Math.Min(1f, elapsed / duration) : 1f;
        if (Progress < 1f) return;
        // 한 프레임에 한 단계만 넘겨 화면 공개와 로어 신호가 생략되지 않게 한다.
        Enter(State == Phase.Revealing ? Phase.Announcement
            : State == Phase.Announcement ? Phase.Dismissing : Phase.Finished);
    }

    void Enter(Phase phase)
    {
        State = phase;
        elapsed = 0f;
        Progress = 0f;
    }
}
