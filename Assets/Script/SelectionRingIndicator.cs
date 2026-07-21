using UnityEngine;

public class SelectionRingIndicator : MonoBehaviour
{
    // 아군(로컬 플레이어)은 초록, 적은 빨강.
    public static readonly Color AllyRingColor =
        new Color(0.2f, 1f, 0.35f, 0.95f);
    public static readonly Color EnemyRingColor =
        new Color(1f, 0.25f, 0.2f, 0.95f);

    private LineRenderer lineRenderer;
    private float radius;
    private int segments = 40;
    private float lineWidth = 0.1f;
    private Color ringColor = AllyRingColor;

    public void Initialize(float ringRadius)
    {
        radius = ringRadius;
        BuildRing();
        SetVisible(false);
    }

    public void SetColor(Color color)
    {
        ringColor = color;

        if (lineRenderer == null)
            return;

        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    void BuildRing()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = ringColor;
        lineRenderer.endColor = ringColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;

            lineRenderer.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.08f,
                    Mathf.Sin(angle) * radius));
        }
    }

    public void SetVisible(bool visible)
    {
        if (lineRenderer != null)
            lineRenderer.enabled = visible;
    }
}
