using UnityEngine;

public class TempVisual : MonoBehaviour
{
    private float duration;
    private float startScale;
    private float endScale;
    private float elapsed;
    private Renderer cachedRenderer;
    private Color baseColor;

    public void Play(float lifeTime, float fromScale, float toScale)
    {
        duration = Mathf.Max(0.01f, lifeTime);
        startScale = fromScale;
        endScale = toScale;

        cachedRenderer = GetComponent<Renderer>();

        if (cachedRenderer != null)
            baseColor = cachedRenderer.material.color;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        float scale = Mathf.Lerp(startScale, endScale, t);
        transform.localScale = Vector3.one * scale;

        if (cachedRenderer != null)
        {
            Color color = baseColor;
            color.a = Mathf.Lerp(1f, 0f, t);
            cachedRenderer.material.color = color;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }
}
