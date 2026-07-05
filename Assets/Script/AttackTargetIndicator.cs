using UnityEngine;

public class AttackTargetIndicator : MonoBehaviour
{
    public static AttackTargetIndicator Instance { get; private set; }

    static readonly Color IndicatorColor = new Color(1f, 0.25f, 0.2f, 0.95f);

    [SerializeField] float radiusPadding = 0.35f;
    [SerializeField] float lineWidth = 0.12f;
    [SerializeField] float heightOffset = 0.12f;
    [SerializeField] int segments = 48;

    LineRenderer lineRenderer;
    SelectableEntity trackedTarget;
    bool isVisible;

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

    void LateUpdate()
    {
        if (!isVisible || trackedTarget == null)
            return;

        if (!IsTargetValid(trackedTarget))
            return;

        UpdatePosition(trackedTarget);
    }

    public static void ShowOn(SelectableEntity target)
    {
        EnsureInstance();

        if (Instance == null || target == null)
            return;

        Instance.ShowInternal(target);
    }

    public static void HideIndicator()
    {
        if (Instance == null)
            return;

        Instance.SetVisible(false);
    }

    static void EnsureInstance()
    {
        if (Instance != null)
            return;

        GameObject indicatorObject = new GameObject("AttackTargetIndicator");
        indicatorObject.AddComponent<AttackTargetIndicator>();
    }

    void ShowInternal(SelectableEntity target)
    {
        trackedTarget = target;
        UpdatePosition(target);
        SetVisible(true);
    }

    void UpdatePosition(SelectableEntity target)
    {
        Bounds bounds = target.SelectionBounds;
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + radiusPadding;
        ApplyRadius(radius);

        transform.position = bounds.center + Vector3.up * heightOffset;
    }

    void ApplyRadius(float radius)
    {
        if (lineRenderer == null)
            return;

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
        ApplyRadius(1.2f);
    }

    void SetVisible(bool visible)
    {
        isVisible = visible;

        if (!visible)
            trackedTarget = null;

        if (lineRenderer != null)
            lineRenderer.enabled = visible;
    }

    static bool IsTargetValid(SelectableEntity target)
    {
        if (target == null || !target.isActiveAndEnabled)
            return false;

        EntityHealth health = target.GetComponent<EntityHealth>();
        return health == null || health.IsAlive;
    }
}
