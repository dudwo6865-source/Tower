using UnityEngine;

/// <summary>
/// SoundOverlapLimiter의 값을 인스펙터에서 조절합니다. 씬 오브젝트에 하나만 붙이세요.
/// 없으면 코드 기본값(같은 클립 0.06초 안 중복 생략, 개수 제한 없음)이 쓰입니다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-400)]
[AddComponentMenu("게임/사운드/SoundOverlapSettings")]
public class SoundOverlapSettings : MonoBehaviour
{
    public static SoundOverlapSettings Instance { get; private set; }

    [Header("같은 사운드 중복 제한")]
    [Label("중복 제한 사용")]
    [Tooltip("끄면 같은 클립이 동시에 여러 번 재생되어도 모두 그대로 재생합니다.")]
    public bool limiterEnabled = true;

    [Label("중복 무시 시간(초)")]
    [Tooltip("같은 클립이 이 시간 안에 다시 재생되면 생략합니다. 알이 한꺼번에 깨지는 것처럼 같은 순간에 몰린 소리를 한 번만 냅니다. " +
             "너무 크게 잡으면 연사하는 공격 소리가 비어 들릴 수 있습니다. 0이면 간격 제한을 쓰지 않습니다.")]
    [Min(0f)]
    public float duplicateWindow = SoundOverlapLimiter.DefaultDuplicateWindow;

    [Label("클립별 동시 재생 최대 수")]
    [Tooltip("같은 클립이 이미 이만큼 재생 중이면 새 재생을 생략합니다. 0이면 개수 제한을 쓰지 않습니다.")]
    [Min(0)]
    public int maxConcurrentPerClip = SoundOverlapLimiter.DefaultMaxConcurrentPerClip;

    [Label("공격 사운드에도 적용")]
    [Tooltip("켜면 유닛 공격 사운드도 같은 클립 중복 제한을 받습니다. 끄면 공격 사운드는 유닛별 '공격 사운드 최대 겹침 수'만 따릅니다.")]
    public bool applyToAttackSounds = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Apply();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnValidate()
    {
        duplicateWindow = Mathf.Max(0f, duplicateWindow);
        maxConcurrentPerClip = Mathf.Max(0, maxConcurrentPerClip);

        if (Application.isPlaying && Instance == this)
            Apply();
    }

    void Apply()
    {
        SoundOverlapLimiter.Enabled = limiterEnabled;
        SoundOverlapLimiter.DuplicateWindow = Mathf.Max(0f, duplicateWindow);
        SoundOverlapLimiter.MaxConcurrentPerClip = Mathf.Max(0, maxConcurrentPerClip);
        SoundOverlapLimiter.ApplyToAttackSounds = applyToAttackSounds;
    }
}
