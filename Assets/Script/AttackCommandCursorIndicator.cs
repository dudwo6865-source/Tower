using UnityEngine;

// 공격 명령(Attack) 모드에서 마우스가 가리키는 지면 위치에 붉은 원을 표시합니다.
// 대상을 클릭하기 전까지 커서를 따라다니며, 대포(Cannon) 타입 유닛을 선택 중이면
// 원 크기를 그 유닛의 스플래시 범위(splashRadius)만큼 키워 보여줍니다.
//
// 원은 두 가지로 움직여 입력 상태를 알려줍니다.
// - 대기 맥동: 명령 모드가 켜져 있는 동안 계속 조금씩 커졌다 작아집니다.
// - 펄스: 명령 모드에 들어갈 때와 명령을 확정할 때 한 번 빠르게 커졌다 돌아옵니다.
//   확정 펄스는 사라지면서 흐려지고, 연출이 끝난 뒤에 원을 감춥니다.
public class AttackCommandCursorIndicator : MonoBehaviour
{
    public static AttackCommandCursorIndicator Instance { get; private set; }

    public const float DefaultRadius = 1f;

    static readonly Color IndicatorColor = new Color(1f, 0.25f, 0.2f, 0.85f);

    [Header("Ring")]
    [SerializeField] float lineWidth = 0.1f;
    [SerializeField] float heightOffset = 0.1f;
    [SerializeField] int segments = 48;

    [Header("대기 맥동 (명령 모드가 켜져 있는 동안)")]
    [Tooltip("반지름이 커졌다 작아지는 폭입니다. 0.08이면 ±8%입니다.")]
    [SerializeField] float idlePulseScale = 0.08f;

    [Tooltip("1초에 커졌다 작아지기를 몇 번 반복할지입니다.")]
    [SerializeField] float idlePulseCyclesPerSecond = 0.9f;

    [Header("펄스 (명령 입력 / 확정 피드백)")]
    [Tooltip("명령 모드에 들어갈 때 한 번 튀는 크기입니다. 0.25면 최대 +25%입니다.")]
    [SerializeField] float activationPulseScale = 0.25f;

    [SerializeField] float activationPulseDuration = 0.18f;

    [Tooltip("명령을 확정할 때 튀는 크기입니다. 대기 맥동보다 확실히 커야 눈에 띕니다.")]
    [SerializeField] float confirmPulseScale = 0.5f;

    [SerializeField] float confirmPulseDuration = 0.22f;

    LineRenderer lineRenderer;

    // 명령 모드가 지정한 원래 반지름. 맥동은 여기에 배율로 얹는다.
    float baseRadius = DefaultRadius;

    // 지금 라인에 반영돼 있는 반지름. 값이 바뀔 때만 점을 다시 만든다.
    float appliedRadius = -1f;
    float appliedAlphaScale = -1f;

    float pulseTimer;
    float pulseDuration;
    float pulseScale;
    bool hideWhenPulseEnds;

    // 명령 모드에 들어간 순간 커서가 UI 위에 있으면 원이 아직 안 보인다.
    // 그때는 예약해 두었다가 원이 실제로 나타나는 첫 프레임에 튀게 한다.
    bool pendingActivationPulse;

    bool IsPulsing => pulseTimer > 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildRing();
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void ShowAt(Vector3 worldPoint, float radius)
    {
        EnsureInstance();

        if (Instance == null)
            return;

        Instance.ShowInternal(worldPoint, radius);
    }

    public static void HideIndicator()
    {
        if (Instance == null)
            return;

        Instance.HideInternal();
    }

    /// <summary>명령 모드에 들어갔을 때의 피드백입니다. 한 번 튄 뒤 대기 맥동으로 이어집니다.</summary>
    public static void PlayActivationPulse()
    {
        EnsureInstance();

        if (Instance == null)
            return;

        Instance.pendingActivationPulse = true;
    }

    /// <summary>
    /// 명령을 확정했을 때의 피드백입니다. 확정 지점에서 한 번 크게 튀고 흐려진 뒤 사라집니다.
    /// 연출이 끝날 때까지는 HideIndicator를 호출해도 원이 남습니다.
    /// </summary>
    public static void PlayConfirmPulse(Vector3 worldPoint)
    {
        EnsureInstance();

        if (Instance == null)
            return;

        Instance.pendingActivationPulse = false;
        Instance.transform.position = worldPoint + Vector3.up * Instance.heightOffset;
        Instance.SetVisible(true);

        Instance.StartPulse(
            Instance.confirmPulseDuration,
            Instance.confirmPulseScale,
            hideWhenDone: true);
    }

    static void EnsureInstance()
    {
        if (Instance != null)
            return;

        GameObject indicatorObject = new GameObject("AttackCommandCursorIndicator");
        indicatorObject.AddComponent<AttackCommandCursorIndicator>();
    }

    void ShowInternal(Vector3 worldPoint, float radius)
    {
        // 확정 연출 중에는 커서를 따라가지 않고 확정 지점에 그대로 머문다.
        if (!hideWhenPulseEnds || !IsPulsing)
            transform.position = worldPoint + Vector3.up * heightOffset;

        baseRadius = radius;

        bool wasVisible = lineRenderer != null && lineRenderer.enabled;
        SetVisible(true);

        if (pendingActivationPulse && !wasVisible)
        {
            pendingActivationPulse = false;
            StartPulse(activationPulseDuration, activationPulseScale, hideWhenDone: false);
        }
    }

    void HideInternal()
    {
        // 확정 연출이 재생 중이면 끝까지 보여준 뒤에 감춘다.
        if (IsPulsing && hideWhenPulseEnds)
            return;

        SetVisible(false);
    }

    void StartPulse(float duration, float scale, bool hideWhenDone)
    {
        pulseDuration = Mathf.Max(0.01f, duration);
        pulseTimer = pulseDuration;
        pulseScale = scale;
        hideWhenPulseEnds = hideWhenDone;
    }

    // 커서 위치와 반지름은 UnitCommandController가 Update에서 정하므로,
    // 맥동은 그 뒤인 LateUpdate에서 얹는다.
    void LateUpdate()
    {
        if (IsPulsing)
        {
            // 일시정지(timeScale 0) 중에도 입력 피드백은 움직여야 한다.
            pulseTimer -= Time.unscaledDeltaTime;

            if (pulseTimer <= 0f)
            {
                pulseTimer = 0f;

                if (hideWhenPulseEnds)
                {
                    hideWhenPulseEnds = false;
                    SetVisible(false);
                }
            }
        }

        if (lineRenderer == null || !lineRenderer.enabled)
            return;

        ApplyRadius(baseRadius * GetPulseScale());
        ApplyAlphaScale(GetAlphaScale());
    }

    float GetPulseScale()
    {
        if (IsPulsing)
        {
            // 0 → 1 로 진행하는 정규화 시간. Sin(πt)는 0에서 시작해 가운데서 최대가 되고 다시 0이 된다.
            float t = 1f - Mathf.Clamp01(pulseTimer / pulseDuration);
            return 1f + pulseScale * Mathf.Sin(t * Mathf.PI);
        }

        float phase = Time.unscaledTime * idlePulseCyclesPerSecond * Mathf.PI * 2f;
        return 1f + idlePulseScale * Mathf.Sin(phase);
    }

    float GetAlphaScale()
    {
        // 확정 펄스는 퍼지면서 흐려진다. 그 외에는 원래 색 그대로.
        if (!IsPulsing || !hideWhenPulseEnds)
            return 1f;

        return Mathf.Clamp01(pulseTimer / pulseDuration);
    }

    void ApplyRadius(float radius)
    {
        if (lineRenderer == null)
            return;

        if (Mathf.Approximately(radius, appliedRadius))
            return;

        appliedRadius = radius;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;

            lineRenderer.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius));
        }
    }

    void ApplyAlphaScale(float alphaScale)
    {
        if (lineRenderer == null)
            return;

        if (Mathf.Approximately(alphaScale, appliedAlphaScale))
            return;

        appliedAlphaScale = alphaScale;

        Color color = IndicatorColor;
        color.a *= alphaScale;

        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    void BuildRing()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = IndicatorColor;
        lineRenderer.endColor = IndicatorColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = segments;

        appliedAlphaScale = 1f;
        ApplyRadius(DefaultRadius);
    }

    void SetVisible(bool visible)
    {
        if (lineRenderer != null)
            lineRenderer.enabled = visible;
    }
}
