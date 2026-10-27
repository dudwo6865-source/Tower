using UnityEngine;

// 팝업 창이 켜질 때(OnEnable) 크게 나타나 살짝 줄었다가 원래 크기로 자리 잡는 연출을 재생합니다.
// 결과 창·유물 선택 창처럼 게임을 멈춘 상태(Time.timeScale = 0)에서도 실제 시간으로 움직입니다.
// 창 오브젝트에 직접 붙이면 인스펙터에서 연출을 조절할 수 있고,
// 붙어 있지 않으면 각 UI 스크립트가 창을 열 때 Ensure로 기본값을 붙입니다.
[DisallowMultipleComponent]
public class PopupOpenAnimation : MonoBehaviour
{
    [Header("연출 대상")]
    [Label("크기 연출 대상")]
    [Tooltip("크기가 바뀔 창 본체입니다. 비워두면 이 오브젝트를 씁니다. 화면 전체를 덮는 어두운 배경이 이 오브젝트라면 창 본체만 넣어 주세요.")]
    public RectTransform scaleTarget;

    [Header("연출 값")]
    [Label("시작 크기 배율"), Min(0)]
    [Tooltip("창이 이 배율로 나타납니다.")]
    public float startScale = 1.2f;

    [Label("시작 투명도"), Range(0, 1)]
    [Tooltip("창이 나타날 때의 불투명도입니다. 0.5 = 50%. 연출이 끝나면 100%가 됩니다.")]
    public float startAlpha = 0.5f;

    [Label("줄어드는 크기 배율"), Min(0)]
    [Tooltip("시작 크기에서 이 배율까지 줄었다가 원래 크기(1)로 돌아옵니다.")]
    public float dipScale = 0.9f;

    [Label("줄어드는 시간(초)"), Min(0)]
    [Tooltip("시작 크기에서 줄어드는 크기까지 걸리는 시간입니다.")]
    public float shrinkDuration = 0.1f;

    [Label("복귀 시간(초)"), Min(0)]
    [Tooltip("줄어든 크기에서 원래 크기로 돌아오는 시간입니다. 투명도는 두 구간 동안 100%까지 올라갑니다.")]
    public float settleDuration = 0.08f;

    [Label("투명도 연출 사용")]
    [Tooltip("켜면 시작 투명도에서 100%로 바뀝니다. 이 오브젝트에 CanvasGroup이 없으면 추가합니다.")]
    public bool fadeIn = true;

    [Header("사운드")]
    [Label("열림음 무음")]
    [Tooltip("켜면 이 창이 열릴 때 소리를 내지 않습니다.")]
    public bool muteOpenSound;

    [Label("열림음 바꾸기")]
    [Tooltip("이 창만 다른 소리를 냅니다. 비워두면 UISoundManager의 '팝업 열림음'을 씁니다.")]
    public AudioClip openSoundOverride;

    [Label("열림음 볼륨 배율"), Range(0, 1)]
    [Tooltip("UISoundManager의 '팝업 열림 볼륨'에 곱해집니다.")]
    public float openSoundVolumeScale = 1f;

    CanvasGroup canvasGroup;
    Vector3 restScale;
    bool hasRestScale;
    float elapsed;
    bool playing;

    public static PopupOpenAnimation Ensure(GameObject panel)
    {
        if (panel == null) return null;
        var animation = panel.GetComponent<PopupOpenAnimation>();
        return animation != null ? animation : panel.AddComponent<PopupOpenAnimation>();
    }

    RectTransform Target => scaleTarget != null ? scaleTarget : transform as RectTransform;
    float TotalDuration => shrinkDuration + settleDuration;

    void OnEnable()
    {
        var target = Target;
        if (target != null && !hasRestScale)
        {
            restScale = target.localScale;
            hasRestScale = true;
        }
        if (fadeIn && canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        // 씬에 켜진 채로 놓인 창이 씬을 불러오는 순간 소리를 내지 않게 한다.
        if (!muteOpenSound && Time.timeSinceLevelLoad > 0f)
            UISoundManager.PlayPopupOpen(openSoundOverride, openSoundVolumeScale);
        elapsed = 0f;
        playing = TotalDuration > 0f;
        Apply(playing ? 0f : TotalDuration);
    }

    void OnDisable()
    {
        // 연출 도중 꺼져도 다음에 원래 크기에서 시작하도록 되돌린다.
        playing = false;
        Apply(TotalDuration);
    }

    void Update()
    {
        if (!playing) return;
        elapsed += Time.unscaledDeltaTime;
        Apply(elapsed);
        if (elapsed >= TotalDuration) playing = false;
    }

    void Apply(float time)
    {
        float scale;
        if (time < shrinkDuration)
            scale = Mathf.Lerp(startScale, dipScale, EaseOut(time / shrinkDuration));
        else if (time < TotalDuration)
            scale = Mathf.Lerp(dipScale, 1f, EaseOut((time - shrinkDuration) / settleDuration));
        else
            scale = 1f;

        var target = Target;
        if (target != null && hasRestScale)
            target.localScale = restScale * scale;
        if (fadeIn && canvasGroup != null)
            canvasGroup.alpha = TotalDuration <= 0f ? 1f : Mathf.Lerp(startAlpha, 1f, time / TotalDuration);
    }

    static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }
}
