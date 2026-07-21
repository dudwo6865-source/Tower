using System;
using UnityEngine;

[DisallowMultipleComponent]
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Header("Duration")]
    [Tooltip("낮이 지속되는 시간(초)입니다.")]
    public float dayDuration = 120f;

    [Tooltip("밤이 지속되는 시간(초)입니다.")]
    public float nightDuration = 60f;

    [Header("Start")]
    [Tooltip("게임 시작 시 낮/밤 중 어느 페이즈로 시작할지 지정합니다.")]
    public DayNightPhase startPhase = DayNightPhase.Day;

    [Tooltip("시작 시 자동으로 사이클을 진행합니다.")]
    public bool autoStart = true;

    [Header("Lighting")]
    [Tooltip("낮/밤에 색을 적용할 방향광입니다. 비워두면 RenderSettings.sun 또는 씬의 Directional Light를 찾습니다.")]
    public Light directionalLight;

    [Tooltip("낮 시간대 라이트 색상입니다.")]
    public Color dayLightColor = new Color(1f, 0.95686275f, 0.8392157f, 1f);

    [Tooltip("밤 시간대 라이트 색상입니다.")]
    public Color nightLightColor = new Color(0.35f, 0.45f, 0.75f, 1f);

    [Tooltip("낮 시간대 라이트 강도입니다.")]
    public float dayLightIntensity = 1f;

    [Tooltip("밤 시간대 라이트 강도입니다.")]
    public float nightLightIntensity = 0.35f;

    [Tooltip("페이즈 변경 시 라이트 색/강도를 적용합니다.")]
    public bool applyLightingOnPhaseChange = true;

    [Tooltip("낮/밤 전환 시 라이트가 먼저 변하는 시간(초)입니다. 완료 후 페이즈와 알림이 바뀝니다.")]
    public float lightTransitionDuration = 5f;

    public DayNightPhase CurrentPhase { get; private set; } = DayNightPhase.Day;

    public float PhaseElapsed { get; private set; }

    public float PhaseDuration =>
        CurrentPhase == DayNightPhase.Day ? dayDuration : nightDuration;

    public float PhaseNormalized
    {
        get
        {
            float duration = PhaseDuration;

            if (duration <= 0f)
                return 0f;

            return Mathf.Clamp01(PhaseElapsed / duration);
        }
    }

    public float RemainingPhaseTime =>
        Mathf.Max(0f, PhaseDuration - PhaseElapsed);

    public int CurrentNightNumber => CycleCount + 1;

    public int CurrentDayNumber => CycleCount + 1;

    public bool IsDay => CurrentPhase == DayNightPhase.Day;

    public bool IsNight => CurrentPhase == DayNightPhase.Night;

    public bool IsPhaseTransitioning => isPhaseTransitioning;

    public int CycleCount { get; private set; }

    public event Action<DayNightPhase> OnPhaseStarted;

    DayNightPhase pendingPhase;
    bool isPhaseTransitioning;

    Color lightTransitionStartColor;
    float lightTransitionStartIntensity;
    Color lightTransitionTargetColor;
    float lightTransitionTargetIntensity;
    float lightTransitionElapsed;
    bool isLightTransitioning;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentPhase = startPhase;
        PhaseElapsed = 0f;
        ResolveLightingReference();
    }

    void Start()
    {
        ApplyLightingForPhase(CurrentPhase);

        if (autoStart)
            OnPhaseStarted?.Invoke(CurrentPhase);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        UpdateLightTransition();

        if (!autoStart || isPhaseTransitioning)
            return;

        float duration = PhaseDuration;

        if (duration <= 0f)
            return;

        PhaseElapsed += Time.deltaTime;

        if (PhaseElapsed < duration)
            return;

        PhaseElapsed = duration;
        BeginPhaseTransition(GetNextPhase(CurrentPhase));
    }

    public void SetPhase(DayNightPhase phase, bool resetElapsed = true)
    {
        if (isPhaseTransitioning)
            return;

        if (CurrentPhase == phase)
        {
            if (resetElapsed)
                PhaseElapsed = 0f;

            return;
        }

        BeginPhaseTransition(phase);
    }

    static DayNightPhase GetNextPhase(DayNightPhase phase)
    {
        return phase == DayNightPhase.Day
            ? DayNightPhase.Night
            : DayNightPhase.Day;
    }

    void BeginPhaseTransition(DayNightPhase nextPhase)
    {
        pendingPhase = nextPhase;
        isPhaseTransitioning = true;

        if (!applyLightingOnPhaseChange || directionalLight == null)
        {
            CompletePhaseTransition();
            return;
        }

        lightTransitionTargetColor = GetLightColor(nextPhase);
        lightTransitionTargetIntensity = GetLightIntensity(nextPhase);

        if (lightTransitionDuration <= 0f)
        {
            ApplyLightingValues(
                lightTransitionTargetColor,
                lightTransitionTargetIntensity);

            CompletePhaseTransition();
            return;
        }

        lightTransitionStartColor = directionalLight.color;
        lightTransitionStartIntensity = directionalLight.intensity;
        lightTransitionElapsed = 0f;
        isLightTransitioning = true;
    }

    void CompletePhaseTransition()
    {
        CurrentPhase = pendingPhase;
        PhaseElapsed = 0f;

        if (CurrentPhase == DayNightPhase.Day)
            CycleCount++;

        isPhaseTransitioning = false;
        isLightTransitioning = false;
        OnPhaseStarted?.Invoke(CurrentPhase);
    }

    void UpdateLightTransition()
    {
        if (!isLightTransitioning || directionalLight == null)
            return;

        lightTransitionElapsed += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(
            lightTransitionElapsed / lightTransitionDuration);
        float blend = Mathf.SmoothStep(0f, 1f, normalizedTime);

        ApplyLightingValues(
            Color.Lerp(lightTransitionStartColor, lightTransitionTargetColor, blend),
            Mathf.Lerp(lightTransitionStartIntensity, lightTransitionTargetIntensity, blend));

        if (normalizedTime < 1f)
            return;

        isLightTransitioning = false;

        if (isPhaseTransitioning)
            CompletePhaseTransition();
    }

    void ApplyLightingForPhase(DayNightPhase phase)
    {
        if (!applyLightingOnPhaseChange || directionalLight == null)
            return;

        ApplyLightingValues(GetLightColor(phase), GetLightIntensity(phase));
    }

    void ApplyLightingValues(Color color, float intensity)
    {
        directionalLight.color = color;
        directionalLight.intensity = intensity;
    }

    Color GetLightColor(DayNightPhase phase)
    {
        return phase == DayNightPhase.Day ? dayLightColor : nightLightColor;
    }

    float GetLightIntensity(DayNightPhase phase)
    {
        return phase == DayNightPhase.Day ? dayLightIntensity : nightLightIntensity;
    }

    void ResolveLightingReference()
    {
        if (directionalLight != null)
            return;

        if (RenderSettings.sun != null)
        {
            directionalLight = RenderSettings.sun;
            return;
        }

        Light[] lights = FindObjectsOfType<Light>();

        foreach (Light light in lights)
        {
            if (light.type != LightType.Directional)
                continue;

            directionalLight = light;
            return;
        }
    }
}
