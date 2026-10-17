using UnityEngine;

/// <summary>
/// 이 씬에서 틀 BGM을 지정합니다. 씬에 아무 오브젝트에나 붙이면 씬이 시작될 때
/// BGMManager에 곡을 넘깁니다. BGMManager가 없으면 자동으로 만듭니다.
/// </summary>
[DisallowMultipleComponent]
public class SceneBGM : MonoBehaviour
{
    [Header("BGM")]
    [Label("BGM 클립")]
    [Tooltip("이 씬에서 재생할 곡입니다. 비워두면 '비어 있으면 끄기'에 따라 BGM을 끄거나 이전 곡을 그대로 둡니다.")]
    public AudioClip clip;

    [Label("볼륨")]
    [Tooltip("이 곡의 볼륨입니다. BGMManager의 '전체 BGM 볼륨'과 곱해집니다.")]
    [Range(0f, 1f)]
    public float volume = 0.6f;

    [Label("반복 재생")]
    [Tooltip("곡이 끝나면 처음부터 다시 재생합니다.")]
    public bool loop = true;

    [Label("전환 시간(초)")]
    [Tooltip("이전 곡에서 이 곡으로 넘어가는 크로스페이드 시간입니다. 음수면 BGMManager의 '기본 전환 시간'을 씁니다.")]
    public float fadeDuration = -1f;

    [Header("옵션")]
    [Label("같은 곡이면 처음부터")]
    [Tooltip("이전 씬과 같은 곡일 때, 끄면 끊기지 않고 이어서 재생하고, 켜면 처음부터 다시 재생합니다.")]
    public bool restartIfSameClip;

    [Label("비어 있으면 끄기")]
    [Tooltip("BGM 클립이 비어 있을 때, 켜면 이전 씬의 곡을 페이드아웃해 끄고, 끄면 이전 곡을 그대로 이어서 재생합니다.")]
    public bool stopWhenEmpty = true;

    void Start()
    {
        Apply();
    }

    /// <summary>이 씬의 BGM 설정을 다시 적용합니다.</summary>
    public void Apply()
    {
        if (clip == null && !stopWhenEmpty)
            return;

        BGMManager manager = BGMManager.EnsureInstance();

        if (clip == null)
        {
            manager.Stop(fadeDuration);
            return;
        }

        manager.Play(clip, volume, fadeDuration, loop, restartIfSameClip);
    }
}
