using System;

// 시간과 상태만 다뤄 애니메이션 없이도 중복 타격과 취소를 검증한다.
public sealed class BossSkillTiming
{
    public enum Phase { Idle, Preparing, Recovery }
    public Phase State { get; private set; }
    public bool IsActive => State != Phase.Idle;
    public float ReadyAt { get; private set; }
    float earliestHit;
    float deadline;

    public void Reset(float now, float initialDelay)
    {
        State = Phase.Idle;
        ReadyAt = now + Math.Max(0f, initialDelay);
    }

    public bool TryBegin(float now, float cooldown, float preparation, float timeout)
    {
        if (IsActive || now < ReadyAt) return false;
        State = Phase.Preparing;
        ReadyAt = now + Math.Max(0f, cooldown);
        earliestHit = now + Math.Max(0f, preparation);
        deadline = now + Math.Max(preparation + 0.1f, timeout);
        return true;
    }

    public bool TryHit(float now, float recovery)
    {
        if (State != Phase.Preparing || now < earliestHit || now >= deadline) return false;
        State = Phase.Recovery;
        deadline = now + Math.Max(0f, recovery);
        return true;
    }

    public bool HasExpired(float now) => IsActive && now >= deadline;

    // 취소해도 이미 소비한 쿨타임은 유지한다.
    public void Cancel() { State = Phase.Idle; }
}
