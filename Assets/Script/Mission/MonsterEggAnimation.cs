using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[AddComponentMenu("게임/미션/MonsterEggAnimation")]
public class MonsterEggAnimation : MonoBehaviour
{
    [Header("애니메이션 상태")]
    [Label("대기 상태 이름")]
    [Tooltip("Animator의 전체 상태 경로입니다. 대기 클립은 반복 재생으로 설정하세요.")]
    public string idleState = "Base Layer.Idle";
    [Label("부화 상태 이름")]
    [Tooltip("부화 클립에 OnEggHatch와 OnEggHatchFinished 이벤트를 넣으세요. 자동 전이는 필요하지 않습니다.")]
    public string hatchState = "Base Layer.Hatch";
    [Label("애니메이션 레이어")]
    [Min(0)] public int animationLayer;
    [Label("평상시 대기 속도")]
    [Min(0.01f)] public float idleSpeed = 1f;
    [Label("부화 직전 대기 속도")]
    [Min(0.01f)] public float urgentIdleSpeed = 3f;
    [Label("부화 재생 속도")]
    [Min(0.01f)] public float hatchSpeed = 1f;
    [Label("긴장도 변화 곡선")]
    [Tooltip("가로축은 현재 제한 시간의 진행률, 세로축은 속도와 맥박의 강화 정도입니다. 현재 부화 예정인 알에만 적용합니다.")]
    public AnimationCurve urgencyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Label("부화 이벤트 대기 한도(초)")]
    [Tooltip("게임 시간 기준입니다. 소환 이벤트가 없으면 오류를 표시하며 자동 소환하지 않습니다. 소환 후 종료 이벤트가 없으면 이 시간이 지났을 때 껍질을 정리합니다.")]
    [Min(0.1f)] public float hatchTimeout = 15f;

    [Header("이미션 맥박")]
    [Label("이미션 맥박 사용")]
    public bool pulseEmission = true;
    [Label("발광 렌더러")]
    [Tooltip("발광시킬 모델의 렌더러를 연결합니다. 원본 머티리얼에서 이미션 기능과 맵을 먼저 설정하세요.")]
    public Renderer[] emissionRenderers;
    [Label("발광 색 프로퍼티")]
    public string emissionProperty = "_EmissionColor";
    [Label("발광 색")]
    [ColorUsage(true, true)] public Color emissionColor = Color.red;
    [Label("최소 발광 강도")]
    [Min(0f)] public float minimumEmission = 0.1f;
    [Label("평상시 최대 발광 강도")]
    [Min(0f)] public float idleEmission = 1f;
    [Label("부화 직전 최대 발광 강도")]
    [Min(0f)] public float urgentEmission = 5f;
    [Label("평상시 맥박(회/초)")]
    [Min(0f)] public float idlePulseRate = 0.6f;
    [Label("부화 직전 맥박(회/초)")]
    [Min(0f)] public float urgentPulseRate = 2.5f;
    [Label("맥박 밝기 곡선")]
    [Tooltip("가로축 0~1이 맥박 한 주기입니다. 기본값은 두 번 뛰고 쉬는 모양입니다. 세로축 0~1로 최소/최대 밝기를 섞습니다.")]
    public AnimationCurve pulseCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(0.22f, 0f),
        new Keyframe(0.34f, 0.65f), new Keyframe(0.48f, 0f), new Keyframe(1f, 0f));

    sealed class EmissionSlot
    {
        public Renderer renderer;
        public int index;
        public MaterialPropertyBlock original;
    }

    readonly List<EmissionSlot> slots = new List<EmissionSlot>();
    MaterialPropertyBlock properties;
    Animator animator;
    MonsterEgg egg;
    float urgency;
    float pulsePhase;
    float hatchElapsed;
    bool hatching;
    bool detached;
    bool failed;
    bool emissionApplied;
    int emissionId;
    public bool PlaybackAvailable => isActiveAndEnabled && animator != null && animator.isActiveAndEnabled;

    void Awake()
    {
        animator = GetComponent<Animator>();
        properties = new MaterialPropertyBlock();
        CacheEmission();
    }

    public void Bind(MonsterEgg owner) { egg = owner; }

    public string ValidateSetup(MonsterEgg owner)
    {
        if (transform == owner.transform || !transform.IsChildOf(owner.transform))
            return "MonsterEggAnimation은 알 본체 아래 별도 자식 모델의 Animator에 붙이세요.";
        if (!isActiveAndEnabled) return "알 애니메이션 모델과 컴포넌트를 활성화하세요.";
        Animator targetAnimator = GetComponent<Animator>();
        if (targetAnimator == null || !targetAnimator.enabled || targetAnimator.runtimeAnimatorController == null)
            return "알 모델의 Animator에 컨트롤러를 연결하고 활성화하세요.";
        if (string.IsNullOrWhiteSpace(idleState) || string.IsNullOrWhiteSpace(hatchState) || animationLayer < 0)
            return "알의 대기·부화 상태 이름과 레이어를 설정하세요.";
        if (!Positive(idleSpeed) || !Positive(urgentIdleSpeed) || !Positive(hatchSpeed) || !Positive(hatchTimeout))
            return "애니메이션 속도와 이벤트 대기 한도는 0보다 큰 유한한 값이어야 합니다.";
        if (owner.nextHatchIndicator != null && transform.IsChildOf(owner.nextHatchIndicator.transform))
            return "다음 부화 표시 안에 애니메이션 모델을 넣지 마세요.";
        return null;
    }

    static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

    void Start()
    {
        if (egg == null) egg = GetComponentInParent<MonsterEgg>();
        // 시야 밖에서도 이벤트가 실행되고 일시정지에서는 시간이 멈추게 한다.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        if (!HasState(idleState) || !HasState(hatchState))
        {
            failed = true;
            Debug.LogError("알 애니메이션: 대기·부화 상태의 전체 경로와 레이어를 확인하세요.", this);
            return;
        }
        if (!hatching) animator.Play(idleState, animationLayer, 0f);
    }

    bool HasState(string state) => animationLayer < animator.layerCount && animationLayer >= 0 &&
        animator.HasState(animationLayer, Animator.StringToHash(state));

    public void SetUrgency(float value) { if (!hatching) urgency = Mathf.Clamp01(value); }

    public void BeginHatch()
    {
        hatching = true;
        hatchElapsed = 0f;
        urgency = 1f;
        if (failed || !HasState(hatchState))
        {
            Fail("부화 애니메이션 상태를 찾지 못했습니다.");
            return;
        }
        animator.speed = hatchSpeed;
        animator.Play(hatchState, animationLayer, 0f);
    }

    bool Paused => StageIntroController.IsPlaying || Time.timeScale <= 0f ||
        (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver);

    void Update()
    {
        if (animator == null) return;
        if (failed || Paused) { animator.speed = 0f; return; }
        float strength = urgencyCurve != null ? Mathf.Clamp01(urgencyCurve.Evaluate(urgency)) : urgency;
        animator.speed = hatching ? hatchSpeed : Mathf.Lerp(idleSpeed, urgentIdleSpeed, strength);
        if (hatching)
        {
            hatchElapsed += Time.deltaTime;
            if (hatchElapsed >= hatchTimeout)
            {
                if (detached) Destroy(gameObject);
                else Fail("부화 소환 이벤트가 없습니다. 클립에 OnEggHatch를 추가하거나 대기 한도를 늘리세요.");
                return;
            }
        }
        if (!pulseEmission) { RestoreEmission(); return; }
        pulsePhase = Mathf.Repeat(pulsePhase + Time.deltaTime * Mathf.Max(0f, Mathf.Lerp(idlePulseRate, urgentPulseRate, strength)), 1f);
        float pulse = pulseCurve != null ? Mathf.Clamp01(pulseCurve.Evaluate(pulsePhase)) : 0f;
        float peak = Mathf.Max(minimumEmission, Mathf.Lerp(idleEmission, urgentEmission, strength));
        Color color = emissionColor * Mathf.Lerp(Mathf.Max(0f, minimumEmission), peak, pulse);
        foreach (EmissionSlot slot in slots)
        {
            if (slot.renderer == null) continue;
            // 개별 슬롯 설정이 없던 렌더러는 피격 플래시 등 공통 설정을 함께 유지한다.
            if (slot.original.isEmpty) slot.renderer.GetPropertyBlock(properties);
            else slot.renderer.GetPropertyBlock(properties, slot.index);
            properties.SetColor(emissionId, color);
            slot.renderer.SetPropertyBlock(properties, slot.index);
        }
        emissionApplied = true;
    }

    // 부화 클립에서 몬스터가 나와야 하는 프레임에 호출한다.
    public void OnEggHatch()
    {
        if (hatching && !detached && !failed && !Paused && egg != null) egg.SpawnFromAnimation();
    }

    // 부화 클립의 마지막 프레임 직전에 호출한다.
    public void OnEggHatchFinished()
    {
        if (!hatching || failed || Paused) return;
        if (detached) Destroy(gameObject);
        else Fail("소환 이벤트보다 종료 이벤트가 먼저 실행되었습니다. OnEggHatch를 앞에 배치하세요.");
    }

    public void DetachShell()
    {
        detached = true;
        // 공격 가능한 알 본체는 제거하되 껍질의 남은 애니메이션은 끝까지 재생한다.
        transform.SetParent(null, true);
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        egg = null;
    }

    void Fail(string message)
    {
        failed = true;
        animator.speed = 0f;
        if (egg != null) egg.ReportHatchFailure(message);
    }

    void CacheEmission()
    {
        if (emissionRenderers == null || string.IsNullOrEmpty(emissionProperty)) return;
        emissionId = Shader.PropertyToID(emissionProperty);
        var seen = new HashSet<Renderer>();
        foreach (Renderer targetRenderer in emissionRenderers)
        {
            if (targetRenderer == null || !seen.Add(targetRenderer)) continue;
            Material[] materials = targetRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !materials[i].HasProperty(emissionId)) continue;
                var original = new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(original, i);
                slots.Add(new EmissionSlot { renderer = targetRenderer, index = i, original = original });
            }
        }
        if (pulseEmission && emissionRenderers.Length > 0 && slots.Count == 0)
            Debug.LogWarning("알 이미션: 연결한 머티리얼에서 발광 색 프로퍼티를 찾지 못했습니다. 셰이더의 프로퍼티 이름을 확인하세요.", this);
    }

    void RestoreEmission()
    {
        if (!emissionApplied) return;
        foreach (EmissionSlot slot in slots)
            if (slot.renderer != null) slot.renderer.SetPropertyBlock(slot.original, slot.index);
        emissionApplied = false;
    }

    void OnDisable() { RestoreEmission(); }
}
