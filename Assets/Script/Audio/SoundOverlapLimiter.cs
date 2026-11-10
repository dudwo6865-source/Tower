using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 같은 클립이 거의 동시에 여러 번 재생되어 소리가 겹쳐 커지는 것을 막습니다.
/// 알이 한꺼번에 부화하거나 여러 적이 같은 순간에 죽을 때처럼, 같은 소리가 같은 프레임에
/// 몰리면 한 번만 재생합니다. 설정은 씬의 SoundOverlapSettings가 있으면 그 값을, 없으면 코드 기본값을 씁니다.
/// UI 사운드와 BGM은 이 제한을 거치지 않습니다.
/// </summary>
public static class SoundOverlapLimiter
{
    public const float DefaultDuplicateWindow = 0.06f;
    public const int DefaultMaxConcurrentPerClip = 0;

    public static bool Enabled = true;
    // 같은 클립을 다시 재생할 수 있는 최소 간격(초, 실제 시간)이다. 0이면 간격 제한을 쓰지 않는다.
    public static float DuplicateWindow = DefaultDuplicateWindow;
    // 같은 클립의 동시 재생 최대 수다. 0이면 개수 제한을 쓰지 않는다.
    public static int MaxConcurrentPerClip = DefaultMaxConcurrentPerClip;
    public static bool ApplyToAttackSounds = true;

    sealed class Entry
    {
        public float lastPlayTime = float.NegativeInfinity;
        public readonly List<float> endTimes = new List<float>(4);
    }

    static readonly Dictionary<AudioClip, Entry> entries = new Dictionary<AudioClip, Entry>();
    const float MinPitchForLength = 0.01f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetForNewPlaySession()
    {
        Enabled = true;
        DuplicateWindow = DefaultDuplicateWindow;
        MaxConcurrentPerClip = DefaultMaxConcurrentPerClip;
        ApplyToAttackSounds = true;
        entries.Clear();
    }

    /// <summary>
    /// 이 클립을 지금 재생해도 되는지 검사하고, 된다면 재생한 것으로 기록합니다.
    /// false면 소리를 생략하세요. allowOverlap이 true면 검사 없이 통과합니다.
    /// </summary>
    public static bool TryPlay(AudioClip clip, float pitch = 1f, bool allowOverlap = false)
    {
        if (clip == null)
            return false;

        if (!Enabled || allowOverlap)
            return true;

        if (!entries.TryGetValue(clip, out Entry entry))
        {
            // 씬을 오가며 사라진 클립이 쌓이지 않게 가끔 정리한다.
            if (entries.Count >= 256)
                PruneDestroyedClips();

            entry = new Entry();
            entries.Add(clip, entry);
        }

        float now = Time.unscaledTime;

        if (DuplicateWindow > 0f && now < entry.lastPlayTime + DuplicateWindow)
            return false;

        if (MaxConcurrentPerClip > 0)
        {
            entry.endTimes.RemoveAll(end => end <= now);

            if (entry.endTimes.Count >= MaxConcurrentPerClip)
                return false;
        }

        entry.lastPlayTime = now;

        if (MaxConcurrentPerClip > 0)
            entry.endTimes.Add(now + clip.length / Mathf.Max(MinPitchForLength, Mathf.Abs(pitch)));

        return true;
    }

    static void PruneDestroyedClips()
    {
        List<AudioClip> dead = null;

        foreach (AudioClip key in entries.Keys)
        {
            if (key != null)
                continue;

            if (dead == null)
                dead = new List<AudioClip>();
            dead.Add(key);
        }

        if (dead == null)
            return;

        foreach (AudioClip key in dead)
            entries.Remove(key);
    }
}
