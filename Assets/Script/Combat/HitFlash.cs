using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 유닛/건물이 피해를 입을 때 렌더러의 Base Color를 잠깐 플래시 색으로 튕겼다가
// 원래 색으로 되돌리는 피격 피드백입니다. MaterialPropertyBlock만 사용해
// 머티리얼 인스턴스를 만들지 않으므로 배칭이 유지됩니다(BuildingPlacementDissolveFX와
// 동일한 방식). EntityHealth.OnDamaged 이벤트를 구독해 자동으로 재생됩니다.
[RequireComponent(typeof(EntityHealth))]
[DisallowMultipleComponent]
public class HitFlash : MonoBehaviour
{
    [Tooltip("피격 시 튕기는 색상입니다.")]
    public Color flashColor = Color.white;

    [Tooltip("플래시 색에서 원래 색으로 돌아오는 데 걸리는 시간(초)입니다.")]
    public float duration = 0.15f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

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
            if (renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(BaseColorId))
                continue;

            renderers.Add(new RendererFlash
            {
                renderer = renderer,
                originalColor = renderer.sharedMaterial.GetColor(BaseColorId)
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
            propertyBlock.SetColor(BaseColorId, Color.Lerp(flashColor, rf.originalColor, t));
            rf.renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
