using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 생산 건물이 유닛을 생산하는 동안 Base Shader의 이미션(_EmissionMap × _EmissionColor)을 켜서
/// 이미션 맵의 밝은 부분만 빛나게 하는 생산 표시입니다. 생산이 멈추면 서서히 꺼집니다.
/// 보유 유닛 한도에 막히거나 Watt가 부족해 생산하지 못할 때는 '생산 불가 색'으로 맥동 없이 켜 둡니다.
/// MaterialPropertyBlock만 사용하므로 머티리얼 인스턴스를 만들지 않습니다.
/// 피격 플래시(HitFlash)는 별도 프로퍼티(_HitFlashColor)를 쓰므로 서로 겹치지 않습니다.
/// 머티리얼 설정: Base Shader 인스펙터에서 Use Emission을 켜고 이미션 맵을 넣은 뒤,
/// Emission Color는 검정으로 둡니다(생산하지 않을 때의 색).
/// </summary>
[DisallowMultipleComponent]
public class ProductionEmissionIndicator : MonoBehaviour
{
    [Header("대상")]
    [Label("생산 건물")]
    [Tooltip("생산 상태를 읽을 건물입니다. 비워 두면 자신 또는 부모에서 찾습니다.")]
    public ProductionBuilding productionBuilding;

    [Label("발광 색 프로퍼티")]
    [Tooltip("생산 중 색을 넣을 셰이더 프로퍼티 이름입니다. 이 프로퍼티가 있는 머티리얼의 렌더러만 적용됩니다.")]
    public string emissionPropertyName = "_EmissionColor";

    [Header("색")]
    [Label("생산 중 색")]
    [ColorUsage(true, true)]
    [Tooltip("생산 중일 때 이미션 맵에 곱해지는 발광 색입니다. Intensity로 밝기를 조절합니다.")]
    public Color producingColor = new Color(0f, 2f, 2.5f, 1f);

    [Label("첫 생산 대기 중에도 표시")]
    [Tooltip("설치 직후 첫 생산 대기 중에도 생산 중으로 표시할지 여부입니다.")]
    public bool showDuringInitialDelay = true;

    [Label("생산 불가 표시")]
    [Tooltip("보유 유닛 한도에 막히거나 Watt가 부족해 생산하지 못할 때 생산 불가 색으로 표시할지 여부입니다. 끄면 이때 발광이 꺼집니다.")]
    public bool showBlocked = true;

    [Label("생산 불가 색")]
    [ColorUsage(true, true)]
    [Tooltip("보유 유닛 한도 도달·Watt 부족으로 생산하지 못할 때의 발광 색입니다. 이 상태에서는 맥동하지 않습니다.")]
    public Color blockedColor = new Color(2.5f, 0.3f, 0.1f, 1f);

    [Header("전환")]
    [Label("켜짐/꺼짐 시간(초)")]
    [Tooltip("생산 시작·정지·생산 불가 전환 때 발광 색이 바뀌는 데 걸리는 시간(초)입니다. 0이면 즉시 바뀝니다.")]
    [Min(0f)]
    public float fadeDuration = 0.4f;

    [Header("맥동")]
    [Label("맥동 사용")]
    [Tooltip("생산 중 발광 밝기를 주기적으로 오르내리게 합니다.")]
    public bool usePulse = true;

    [Label("맥동 주기(초)")]
    [Tooltip("밝기가 한 번 오르내리는 데 걸리는 평균 시간(초)입니다. 불규칙함이 높으면 실제 주기는 이 값을 중심으로 들쭉날쭉해집니다.")]
    [Min(0.05f)]
    public float pulsePeriod = 1.2f;

    [Label("맥동 최저 밝기")]
    [Tooltip("맥동할 때 가장 어두운 순간의 밝기 비율입니다(1이면 맥동하지 않음).")]
    [Range(0f, 1f)]
    public float pulseMinBrightness = 0.4f;

    [Label("불규칙함")]
    [Tooltip("0이면 일정한 주기로 맥동하고, 1에 가까울수록 밝기와 주기가 무작위로 흔들립니다.")]
    [Range(0f, 1f)]
    public float pulseRandomness = 0.8f;

    [Label("잔반짝임 비중")]
    [Tooltip("느린 밝기 변화 위에 섞이는 빠른 잔떨림의 비중입니다. 높을수록 자잘하게 반짝입니다.")]
    [Range(0f, 1f)]
    public float twinkleAmount = 0.3f;

    [Label("잔반짝임 속도(배)")]
    [Tooltip("잔떨림이 기본 맥동보다 몇 배 빠르게 변할지입니다.")]
    [Min(1f)]
    public float twinkleSpeedMultiplier = 4f;

    struct RendererEmission
    {
        public Renderer renderer;
        public Color offColor;
    }

    readonly List<RendererEmission> renderers = new List<RendererEmission>();
    MaterialPropertyBlock propertyBlock;
    int propertyId;
    float level;
    // 0이면 생산 중 색, 1이면 생산 불가 색.
    float blockedBlend;
    float appliedLevel = -1f;
    float appliedPulse = -1f;
    float appliedBlend = -1f;

    // 건물마다 다른 노이즈 구간을 읽어, 여러 건물이 같은 박자로 반짝이지 않게 한다.
    float noiseSeed;

    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        noiseSeed = Random.Range(0f, 1000f);

        if (productionBuilding == null)
            productionBuilding = GetComponentInParent<ProductionBuilding>();

        CacheRenderers();
    }

    void CacheRenderers()
    {
        renderers.Clear();

        if (string.IsNullOrEmpty(emissionPropertyName))
            return;

        propertyId = Shader.PropertyToID(emissionPropertyName);

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            Material material = renderer.sharedMaterial;

            if (material == null || !material.HasProperty(propertyId))
                continue;

            renderers.Add(new RendererEmission
            {
                renderer = renderer,
                offColor = material.GetColor(propertyId)
            });
        }
    }

    void OnDisable()
    {
        level = 0f;
        blockedBlend = 0f;
        Apply(0f, 1f, 0f);
    }

    void Update()
    {
        if (renderers.Count == 0)
            return;

        bool blocked = showBlocked && productionBuilding != null && productionBuilding.IsProductionBlocked;
        float target = blocked || IsShowingProduction() ? 1f : 0f;
        float step = fadeDuration > 0f ? Time.deltaTime / fadeDuration : float.MaxValue;

        // 꺼진 상태에서 켜질 때는 색을 바로 맞춰, 다른 색을 거쳐 켜지지 않게 한다.
        // 꺼질 때는 마지막 색을 유지한 채 어두워진다.
        if (target > 0f)
        {
            float blendTarget = blocked ? 1f : 0f;
            blockedBlend = level <= 0f ? blendTarget : Mathf.MoveTowards(blockedBlend, blendTarget, step);
        }

        level = Mathf.MoveTowards(level, target, step);

        float pulse = 1f;

        // 생산 불가 색에는 맥동을 넣지 않는다.
        if (usePulse && level > 0f && blockedBlend < 1f)
            pulse = Mathf.Lerp(pulseMinBrightness, 1f, EvaluatePulseWave(Time.time));

        // 값이 그대로면 프로퍼티 블록을 다시 쓰지 않는다.
        if (Mathf.Approximately(level, appliedLevel) &&
            Mathf.Approximately(pulse, appliedPulse) &&
            Mathf.Approximately(blockedBlend, appliedBlend))
            return;

        Apply(level, pulse, blockedBlend);
    }

    // 0~1 맥동 파형. 일정한 코사인 파형과 펄린 노이즈를 '불규칙함' 비율로 섞는다.
    float EvaluatePulseWave(float time)
    {
        float cycles = time / pulsePeriod;
        float regular = 0.5f - 0.5f * Mathf.Cos((cycles + noiseSeed) * 2f * Mathf.PI);

        if (pulseRandomness <= 0f)
            return regular;

        float slow = Mathf.PerlinNoise(noiseSeed, cycles);
        float fast = Mathf.PerlinNoise(noiseSeed + 37.1f, cycles * twinkleSpeedMultiplier);
        float noise = Mathf.Lerp(slow, fast, twinkleAmount);

        // 펄린 노이즈는 0.5 근처에 몰려 있어 그대로 쓰면 밋밋하므로 양끝까지 펼친다.
        noise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, noise));

        return Mathf.Lerp(regular, noise, pulseRandomness);
    }

    bool IsShowingProduction()
    {
        if (productionBuilding == null || !productionBuilding.IsProducing)
            return false;

        return showDuringInitialDelay || !productionBuilding.IsInInitialDelay;
    }

    void Apply(float amount, float pulse, float blend)
    {
        appliedLevel = amount;
        appliedPulse = pulse;
        appliedBlend = blend;
        Color onColor = Color.Lerp(producingColor * pulse, blockedColor, blend);

        foreach (RendererEmission re in renderers)
        {
            if (re.renderer == null)
                continue;

            // 다른 효과(디졸브·피격 플래시)가 넣어 둔 값을 지우지 않도록 기존 블록을 읽어서 고친다.
            re.renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(propertyId, Color.Lerp(re.offColor, onColor, amount));
            re.renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
