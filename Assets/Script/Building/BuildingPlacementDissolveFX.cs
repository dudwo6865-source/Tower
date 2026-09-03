using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건물 설치 시 Base Shader의 Dissolve 프로퍼티(_DissolveHeight)와 아웃라인 컬러를
/// MaterialPropertyBlock으로 애니메이션해, 아래에서 위로 차오르며 나타나고
/// 아웃라인이 디졸브 엣지 색에서 원래 색(검정)으로 식어가는 연출을 재생합니다.
/// 디졸브 프레넬 컬러(_DissolveFresnelColor)는 매 프레임 아웃라인 컬러를
/// 그대로 따라가도록 동일 값으로 맞춰줍니다.
/// 머티리얼 인스턴스를 만들지 않으므로(배칭 유지), 프리팹마다 Animator나
/// 별도 머티리얼 없이 모든 건물에 재사용 가능합니다. 목표 높이/컬러는 각
/// Base.mat에 이미 세팅된 값을 그대로 사용합니다.
/// </summary>
[DisallowMultipleComponent]
public class BuildingPlacementDissolveFX : MonoBehaviour
{
    [Tooltip("디졸브가 아래에서 위로 차오르는 시간(초)입니다.")]
    public float duration = 0.8f;

    [Tooltip("메시 하단 경계보다 얼마나 더 아래에서 시작할지(오브젝트 공간 단위)입니다.")]
    public float startMargin = 0.25f;

    public AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);

    static readonly int DissolveHeightId = Shader.PropertyToID("_DissolveHeight");
    static readonly int DissolveEdgeColorId = Shader.PropertyToID("_DissolveEdgeColor");
    static readonly int DissolveFresnelColorId = Shader.PropertyToID("_DissolveFresnelColor");
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    struct RendererSweep
    {
        public Renderer renderer;
        public float startHeight;
        public float endHeight;
        public bool animateOutline;
        public Color outlineStartColor;
        public Color outlineEndColor;
    }

    readonly List<RendererSweep> sweeps = new List<RendererSweep>();
    MaterialPropertyBlock propertyBlock;
    Coroutine playRoutine;
    bool cached;

    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        CacheSweeps();
    }

    void CacheSweeps()
    {
        sweeps.Clear();

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();

            if (meshFilter == null || meshFilter.sharedMesh == null || renderer.sharedMaterial == null)
                continue;

            if (!renderer.sharedMaterial.HasProperty(DissolveHeightId))
                continue;

            bool animateOutline = renderer.sharedMaterial.HasProperty(DissolveEdgeColorId)
                && renderer.sharedMaterial.HasProperty(OutlineColorId)
                && renderer.sharedMaterial.HasProperty(DissolveFresnelColorId);

            sweeps.Add(new RendererSweep
            {
                renderer = renderer,
                startHeight = meshFilter.sharedMesh.bounds.min.y - startMargin,
                endHeight = renderer.sharedMaterial.GetFloat(DissolveHeightId),
                animateOutline = animateOutline,
                outlineStartColor = animateOutline ? renderer.sharedMaterial.GetColor(DissolveEdgeColorId) : default,
                outlineEndColor = animateOutline ? renderer.sharedMaterial.GetColor(OutlineColorId) : default
            });
        }

        cached = true;
    }

    public void Play()
    {
        if (!cached)
            CacheSweeps();

        if (sweeps.Count == 0)
            return;

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        Apply(0f);
        playRoutine = StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            Apply(easing.Evaluate(Mathf.Clamp01(t / duration)));
            yield return null;
        }

        // 원래 머티리얼 값으로 완전히 되돌려, 이후엔 프로퍼티 블록 오버라이드를 남기지 않습니다.
        foreach (RendererSweep sweep in sweeps)
        {
            if (sweep.renderer != null)
                sweep.renderer.SetPropertyBlock(null);
        }

        playRoutine = null;
    }

    void Apply(float t)
    {
        foreach (RendererSweep sweep in sweeps)
        {
            if (sweep.renderer == null)
                continue;

            sweep.renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(DissolveHeightId, Mathf.Lerp(sweep.startHeight, sweep.endHeight, t));

            if (sweep.animateOutline)
            {
                Color outlineColor = Color.Lerp(sweep.outlineStartColor, sweep.outlineEndColor, t);
                propertyBlock.SetColor(OutlineColorId, outlineColor);
                // 디졸브 프레넬 컬러가 아웃라인 컬러를 그대로 따라가게 합니다.
                propertyBlock.SetColor(DissolveFresnelColorId, outlineColor);
            }

            sweep.renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
