using UnityEngine;

// 공격 명령(Attack) 모드에서 마우스가 가리키는 지면 위치에 붉은 원을 표시합니다.
// 대상을 클릭하기 전까지 커서를 따라다니며, 대포(Cannon) 타입 유닛을 선택 중이면
// 원 크기를 그 유닛의 스플래시 범위(splashRadius)만큼 키워 보여줍니다.
public class AttackCommandCursorIndicator : MonoBehaviour
{
    public static AttackCommandCursorIndicator Instance { get; private set; }

    public const float DefaultRadius = 1f;

    static readonly Color IndicatorColor = new Color(1f, 0.25f, 0.2f, 0.85f);

    [SerializeField] float lineWidth = 0.1f;
    [SerializeField] float heightOffset = 0.1f;
    [SerializeField] int segments = 48;

    LineRenderer lineRenderer;
    float currentRadius = -1f;

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

    public static void ShowAt(Vector3 worldPoint, float radius)
    {
        EnsureInstance();

        if (Instance == null)
            return;

        Instance.ShowInternal(worldPoint, radius);
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

        GameObject indicatorObject = new GameObject("AttackCommandCursorIndicator");
        indicatorObject.AddComponent<AttackCommandCursorIndicator>();
    }

    void ShowInternal(Vector3 worldPoint, float radius)
    {
        transform.position = worldPoint + Vector3.up * heightOffset;

        if (!Mathf.Approximately(radius, currentRadius))
            ApplyRadius(radius);

        SetVisible(true);
    }

    void ApplyRadius(float radius)
    {
        currentRadius = radius;

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
        ApplyRadius(DefaultRadius);
    }

    void SetVisible(bool visible)
    {
        if (lineRenderer != null)
            lineRenderer.enabled = visible;
    }
}
