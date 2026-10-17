using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 씬이 바뀌어도 유지되는 BGM 재생기입니다. 각 씬의 SceneBGM이 곡을 지정하면
/// 이전 곡에서 새 곡으로 크로스페이드합니다. 씬에 없으면 SceneBGM이 런타임에 만듭니다.
/// 인스펙터 값을 조절하려면 첫 씬(로비)의 빈 루트 오브젝트에 직접 붙이세요.
/// </summary>
[DisallowMultipleComponent]
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("재생")]
    [Label("전체 BGM 볼륨")]
    [Tooltip("모든 BGM에 곱해지는 볼륨입니다. 옵션 메뉴의 BGM 볼륨 등에 연결할 수 있습니다.")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;

    [Label("기본 전환 시간(초)")]
    [Tooltip("SceneBGM에서 전환 시간을 따로 정하지 않았을 때(음수) 쓰는 크로스페이드 시간입니다.")]
    [Min(0f)]
    public float defaultFadeDuration = 1f;

    [Label("오디오 믹서 그룹")]
    [Tooltip("BGM을 보낼 Audio Mixer 그룹입니다. 비워두면 기본 출력으로 재생합니다.")]
    public AudioMixerGroup outputMixerGroup;

    // 크로스페이드용으로 두 개를 번갈아 쓴다. active가 지금 들리는 곡이다.
    AudioSource activeSource;
    AudioSource idleSource;
    float activeTargetVolume;
    Coroutine fadeRoutine;

    public AudioClip CurrentClip => activeSource != null ? activeSource.clip : null;
    public bool IsPlaying => activeSource != null && activeSource.isPlaying;

    public static BGMManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        BGMManager existing = FindObjectOfType<BGMManager>();

        if (existing != null)
        {
            existing.Initialize();
            return existing;
        }

        GameObject managerObject = new GameObject("BGMManager");
        return managerObject.AddComponent<BGMManager>();
    }

    void Awake()
    {
        Initialize();
    }

    void Initialize()
    {
        if (Instance == this)
            return;

        // 씬마다 BGMManager를 두었어도 처음 것만 쓴다. 같은 오브젝트의 다른 컴포넌트가
        // 날아가지 않도록 컴포넌트만 지운다.
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad는 루트 오브젝트에만 동작한다.
        if (transform.parent != null)
        {
            Debug.LogWarning(
                "BGMManager: 씬 전환 후에도 유지되도록 루트 오브젝트로 옮깁니다. " +
                "빈 루트 오브젝트에 붙여 두는 것을 권장합니다.",
                this);
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);

        activeSource = CreateSource("BGM A");
        idleSource = CreateSource("BGM B");
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnValidate()
    {
        // 플레이 중 전체 볼륨을 바꾸면 바로 반영한다.
        if (Application.isPlaying && fadeRoutine == null && activeSource != null)
            activeSource.volume = activeTargetVolume * masterVolume;
    }

    AudioSource CreateSource(string sourceName)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.ignoreListenerPause = true;
        source.outputAudioMixerGroup = outputMixerGroup;
        return source;
    }

    /// <summary>
    /// 곡을 재생합니다. 같은 곡이 이미 재생 중이면 처음부터 다시 틀지 않고 볼륨만 맞춥니다
    /// (restartIfSame이 true면 처음부터).
    /// </summary>
    /// <param name="fadeDuration">음수면 기본 전환 시간을 씁니다.</param>
    public void Play(
        AudioClip clip,
        float volume = 1f,
        float fadeDuration = -1f,
        bool loop = true,
        bool restartIfSame = false)
    {
        if (clip == null)
        {
            Stop(fadeDuration);
            return;
        }

        float fade = ResolveFade(fadeDuration);
        volume = Mathf.Clamp01(volume);

        if (activeSource.clip == clip && activeSource.isPlaying && !restartIfSame)
        {
            activeSource.loop = loop;

            // 크로스페이드 도중이었으면 나가던 곡도 마저 끈다.
            StartFade(activeSource, volume, fade, idleSource.isPlaying ? idleSource : null);
            return;
        }

        // 지금 곡은 놀고 있는 소스로 넘겨 페이드아웃하고, 새 곡은 다른 소스에서 페이드인한다.
        AudioSource previous = activeSource;
        activeSource = idleSource;
        idleSource = previous;

        activeSource.outputAudioMixerGroup = outputMixerGroup;
        activeSource.clip = clip;
        activeSource.loop = loop;
        activeSource.volume = 0f;
        activeSource.Play();

        StartFade(activeSource, volume, fade, idleSource);
    }

    /// <summary>지금 곡을 페이드아웃하며 멈춥니다.</summary>
    public void Stop(float fadeDuration = -1f)
    {
        if (activeSource == null)
            return;

        StartFade(null, 0f, ResolveFade(fadeDuration), activeSource);
    }

    float ResolveFade(float fadeDuration)
    {
        return fadeDuration >= 0f ? fadeDuration : defaultFadeDuration;
    }

    void StartFade(AudioSource fadeIn, float targetVolume, float duration, AudioSource fadeOut)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (fadeIn != null)
            activeTargetVolume = targetVolume;

        fadeRoutine = StartCoroutine(FadeRoutine(fadeIn, targetVolume, duration, fadeOut));
    }

    // 결과 창에서 Time.timeScale = 0이 되어도 멈추지 않도록 unscaled 시간으로 페이드한다.
    IEnumerator FadeRoutine(AudioSource fadeIn, float targetVolume, float duration, AudioSource fadeOut)
    {
        float inStart = fadeIn != null ? fadeIn.volume : 0f;
        float outStart = fadeOut != null ? fadeOut.volume : 0f;
        float inEnd = targetVolume * masterVolume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (fadeIn != null)
                fadeIn.volume = Mathf.Lerp(inStart, inEnd, t);

            if (fadeOut != null)
                fadeOut.volume = Mathf.Lerp(outStart, 0f, t);

            yield return null;
        }

        if (fadeIn != null)
            fadeIn.volume = inEnd;

        if (fadeOut != null)
        {
            fadeOut.volume = 0f;
            fadeOut.Stop();
            fadeOut.clip = null;
        }

        fadeRoutine = null;
    }
}
