using UnityEngine;

// 관통 빔(히트스캔) 공격의 순간적인 선 이펙트입니다. LineRenderer로 선을 그린 뒤
// 짧은 시간 동안 옅어지며 사라지고 스스로 파괴됩니다.
public class BeamVisual : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Color baseColor;
    private float duration;
    private float elapsed;

    public void Play(Vector3 start, Vector3 end, float width, Color color, float lifeTime, Material material)
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.useWorldSpace = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.material = material;

        baseColor = color;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        duration = Mathf.Max(0.01f, lifeTime);
        elapsed = 0f;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        Color color = baseColor;
        color.a = Mathf.Lerp(baseColor.a, 0f, t);
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        if (t >= 1f)
            Destroy(gameObject);
    }
}
