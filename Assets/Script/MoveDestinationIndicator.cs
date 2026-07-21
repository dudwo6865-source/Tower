using UnityEngine;

public class MoveDestinationIndicator : MonoBehaviour
{
    public static MoveDestinationIndicator Instance { get; private set; }

    // 일반 이동은 초록, 공격 이동은 빨강.
    public static readonly Color MoveColor = new Color(0.2f, 1f, 0.35f, 0.95f);
    public static readonly Color AttackMoveColor = new Color(1f, 0.25f, 0.2f, 0.95f);

    [SerializeField] float radius = 1.2f;
    [SerializeField] float lineWidth = 0.12f;
    [SerializeField] float heightOffset = 0.12f;
    [SerializeField] int segments = 48;

    LineRenderer lineRenderer;
    bool isVisible;
    Color currentColor = MoveColor;

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

    public static void ShowAt(Vector3 worldDestination)
    {
        ShowAt(worldDestination, MoveColor);
    }

    public static void ShowAt(Vector3 worldDestination, Color color)
    {
        EnsureInstance();

        if (Instance == null)
            return;

        Instance.ShowInternal(worldDestination, color);
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

        GameObject indicatorObject = new GameObject("MoveDestinationIndicator");
        indicatorObject.AddComponent<MoveDestinationIndicator>();
    }

    void ShowInternal(Vector3 worldDestination, Color color)
    {
        transform.position = worldDestination + Vector3.up * heightOffset;
        SetColor(color);
        SetVisible(true);
    }

    void SetColor(Color color)
    {
        currentColor = color;

        if (lineRenderer == null)
            return;

        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    void BuildRing()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = currentColor;
        lineRenderer.endColor = currentColor;
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
                    0f,
                    Mathf.Sin(angle) * radius));
        }
    }

    void SetVisible(bool visible)
    {
        isVisible = visible;

        if (lineRenderer != null)
            lineRenderer.enabled = visible;
    }
}
