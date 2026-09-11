using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 유닛/건물이 피해를 입을 때 렌더러의 Emission Color를 잠깐 플래시 색으로 튕겼다가
// 원래 색으로 되돌리는 피격 피드백입니다. Base Color(곱연산)는 조명에 다시 곱해져
// 그림자·어두운 면에서 거의 안 보이지만, Emission은 조명과 무관하게 더해지므로
// 항상 또렷하게 번쩍입니다. MaterialPropertyBlock만 사용해 머티리얼 인스턴스를
// 만들지 않으므로 배칭이 유지됩니다(BuildingPlacementDissolveFX와 동일한 방식).
// EntityHealth.OnDamaged 이벤트를 구독해 자동으로 재생됩니다.
// (대상 머티리얼의 Emission이 꺼져 있으면 효과가 보이지 않으니, Base Shader
// 인스펙터의 Emission 항목을 켜고 Emission Color를 검정으로 둔 상태여야 합니다.)
[RequireComponent(typeof(EntityHealth))]
[DisallowMultipleComponent]
public class HitFlash : MonoBehaviour
{
    [ColorUsage(true, true)]
    [Tooltip("피격 시 튕기는 발광 색상입니다. 색상 피커의 Intensity 슬라이더로 밝기(강도)를 조절하세요(1보다 큰 값일수록 더 강하게 번쩍입니다).")]
    public Color flashColor = new Color(6f, 6f, 6f, 1f);

    [Tooltip("플래시 색에서 원래 색으로 돌아오는 데 걸리는 시간(초)입니다.")]
    public float duration = 0.15f;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    struct RendererFlash
    {
        public Renderer renderer;
        public Color originalColor;
    }

    readonly List<RendererFlash> renderers = new List<RendererFlash>();
    MaterialPropertyBlock propertyBlock;
    EntityHealth health;
    Coroutine flashRoutine;
    bool cached;

    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        health = GetComponent<EntityHealth>();
    }

    void OnEnable()
    {
        health.OnDamaged += HandleDamaged;
    }

    void OnDisable()
    {
        health.OnDamaged -= HandleDamaged;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        foreach (RendererFlash rf in renderers)
        {
            if (rf.renderer != null)
                rf.renderer.SetPropertyBlock(null);
        }
    }

    void CacheRenderers()
    {
        renderers.Clear();

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(EmissionColorId))
                continue;

            renderers.Add(new RendererFlash
            {
                renderer = renderer,
                originalColor = renderer.sharedMaterial.GetColor(EmissionColorId)
            });
        }

        cached = true;
    }

    void HandleDamaged(float damage, SelectableEntity attacker)
    {
        if (!cached)
            CacheRenderers();

        if (renderers.Count == 0)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        float t = 0f;
        Apply(0f);

        while (t < duration)
        {
            t += Time.deltaTime;
            Apply(Mathf.Clamp01(t / Mathf.Max(0.01f, duration)));
            yield return null;
        }

        // 원래 머티리얼 값으로 완전히 되돌려, 이후엔 프로퍼티 블록 오버라이드를 남기지 않습니다.
        foreach (RendererFlash rf in renderers)
        {
            if (rf.renderer != null)
                rf.renderer.SetPropertyBlock(null);
        }

        flashRoutine = null;
    }

    void Apply(float t)
    {
        foreach (RendererFlash rf in renderers)
        {
            if (rf.renderer == null)
                continue;

            rf.renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, Color.Lerp(flashColor, rf.originalColor, t));
            rf.renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
