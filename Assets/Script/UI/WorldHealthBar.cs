using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(EntityHealth))]
[RequireComponent(typeof(SelectableEntity))]
public class WorldHealthBar : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("콜라이더 위쪽에서 체력바가 떠 있는 추가 높이입니다.")]
    public float heightOffset = 3f;

    [Tooltip("체력바의 월드 기준 높이입니다.")]
    public float barHeight = 0.5f;

    [Tooltip("월드 스페이스 캔버스 스케일입니다.")]
    public float worldScale = 0.07f;

    [Tooltip("체력바 한 칸의 월드 기준 너비입니다.")]
    public float segmentWorldWidth = 0.4f;

    [Header("Display")]
    [Tooltip("로컬 플레이어 소유자 ID입니다. 아군/적 체력바 표시 규칙에 사용됩니다.")]
    public int localPlayerOwnerId = 1;

    [Tooltip("적 유닛·건물의 체력바를 항상 표시합니다.")]
    public bool alwaysShowEnemyBar = false;

    [Tooltip("아군이 선택되었을 때 체력바를 표시합니다.")]
    public bool showWhenSelected = true;

    [Tooltip("피해를 입은 뒤 체력바를 표시하는 시간(초)입니다.")]
    public float showAfterDamageDuration = 3f;

    [Header("Colors")]
    [Tooltip("아군 체력바 색상입니다.")]
    public Color allyFillColor = new Color(0.2f, 0.9f, 0.3f, 1f);

    [Tooltip("적군 체력바 색상입니다.")]
    public Color enemyFillColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Tooltip("체력이 낮을 때 바 색상입니다.")]
    public Color lowHealthColor = new Color(0.95f, 0.55f, 0.1f, 1f);

    [Tooltip("낮은 체력으로 판정하는 비율입니다.")]
    [Range(0f, 1f)]
    public float lowHealthThreshold = 0.35f;

    [Header("Segments")]
    [Tooltip("체력바 한 칸이 나타내는 체력량입니다. 최대 체력 ÷ 이 값 = 칸 개수입니다.")]
    public float healthPerSegment = 10f;

    [Tooltip("칸 사이 간격(캔버스 픽셀)입니다.")]
    public float segmentSpacing = 3f;

    [Tooltip("한 줄에 표시할 최대 칸 수입니다. 초과하면 다음 줄로 넘깁니다.")]
    public int maxSegmentsPerRow = 15;

    private EntityHealth entityHealth;
    private SelectableEntity selectableEntity;
    private RectTransform segmentsRoot;
    private GridLayoutGroup segmentsGrid;
    private readonly List<Image> segmentImages = new List<Image>();
    private CanvasGroup canvasGroup;
    private RectTransform healthBarCanvasRect;
    private Transform barAnchor;
    private float damageShowTimer;
    private bool lastFogVisible = true;
    private FogOfWarVisibility fogVisibility;
    private Color baseFillColor;
    private Sprite defaultSprite;
    private Material overlayMaterial;

    void Awake()
    {
        entityHealth = GetComponent<EntityHealth>();
        selectableEntity = GetComponent<SelectableEntity>();
        fogVisibility = GetComponent<FogOfWarVisibility>();

        entityHealth.OnHealthChanged += HandleHealthChanged;
        entityHealth.OnDied += HandleDied;

        defaultSprite = HealthBarSpriteUtility.GetWhiteSprite();
        overlayMaterial = HealthBarSpriteUtility.GetOverlayMaterial();
        baseFillColor = GetBaseFillColor();
    }

    void Start()
    {
        ApplyDefaultLayout();

        if (barAnchor == null)
            BuildHealthBarUI();

        UpdateFill(entityHealth.CurrentHealth, entityHealth.MaxHealth);
        RefreshVisibility();
    }

    void ApplyDefaultLayout()
    {
        if (selectableEntity.entityType == SelectableEntityType.Building &&
            heightOffset < 0.5f)
        {
            heightOffset = 0.5f;
        }
    }

    void OnDestroy()
    {
        if (entityHealth != null)
        {
            entityHealth.OnHealthChanged -= HandleHealthChanged;
            entityHealth.OnDied -= HandleDied;
        }

        if (barAnchor != null)
            Destroy(barAnchor.gameObject);
    }

    void Update()
    {
        bool shouldShow = ShouldShowBar();
        float targetAlpha = shouldShow ? 1f : 0f;

        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha,
            targetAlpha,
            Time.deltaTime * 4f);

        if (damageShowTimer > 0f)
            damageShowTimer -= Time.deltaTime;
    }

    void LateUpdate()
    {
        Camera camera = Camera.main;

        if (camera == null || barAnchor == null)
            return;

        barAnchor.position = GetBarWorldPosition();
        barAnchor.rotation = camera.transform.rotation;
    }

    void BuildHealthBarUI()
    {
        int segmentCount = GetSegmentCount(entityHealth.MaxHealth);

        GameObject anchorObject = new GameObject($"{gameObject.name}_HealthBar");
        anchorObject.transform.SetParent(WorldHealthBarRoot.Transform, false);

        barAnchor = anchorObject.transform;
        barAnchor.position = GetBarWorldPosition();

        if (Camera.main != null)
            barAnchor.rotation = Camera.main.transform.rotation;

        GameObject canvasObject = new GameObject("HealthBarCanvas");
        canvasObject.transform.SetParent(anchorObject.transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 10;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        healthBarCanvasRect = canvasRect;
        ApplyBarSize(segmentCount);

        canvasObject.transform.localScale =
            Vector3.one * worldScale;

        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(canvasObject.transform, false);

        RectTransform backgroundRect =
            backgroundObject.AddComponent<RectTransform>();

        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.sprite = defaultSprite;
        backgroundImage.material = overlayMaterial;
        backgroundImage.color = new Color(0f, 0f, 0f, 0.65f);
        backgroundImage.raycastTarget = false;

        GameObject segmentsObject = new GameObject("Segments");
        segmentsObject.transform.SetParent(backgroundObject.transform, false);

        segmentsRoot = segmentsObject.AddComponent<RectTransform>();
        segmentsRoot.anchorMin = Vector2.zero;
        segmentsRoot.anchorMax = Vector2.one;
        segmentsRoot.offsetMin = new Vector2(2f, 2f);
        segmentsRoot.offsetMax = new Vector2(-2f, -2f);

        segmentsGrid = segmentsObject.AddComponent<GridLayoutGroup>();
        ConfigureSegmentsGrid();
    }

    void ConfigureSegmentsGrid()
    {
        if (segmentsGrid == null)
            return;

        segmentsGrid.cellSize = new Vector2(
            GetSegmentCanvasWidth(),
            GetSegmentCanvasHeight());

        segmentsGrid.spacing = new Vector2(segmentSpacing, segmentSpacing);
        segmentsGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        segmentsGrid.constraintCount = Mathf.Max(1, maxSegmentsPerRow);
        segmentsGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        segmentsGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        segmentsGrid.childAlignment = TextAnchor.MiddleCenter;
    }

    void GetSegmentLayout(int segmentCount, out int columns, out int rows)
    {
        segmentCount = Mathf.Max(1, segmentCount);
        int maxPerRow = Mathf.Max(1, maxSegmentsPerRow);
        columns = Mathf.Min(segmentCount, maxPerRow);
        rows = Mathf.CeilToInt(segmentCount / (float)maxPerRow);
    }

    float ResolveBarWidth(int segmentCount)
    {
        GetSegmentLayout(segmentCount, out int columns, out _);

        float spacingWorld = segmentSpacing * worldScale;
        float paddingWorld = 4f * worldScale;

        return columns * segmentWorldWidth
            + Mathf.Max(0, columns - 1) * spacingWorld
            + paddingWorld;
    }

    float ResolveBarHeight(int segmentCount)
    {
        GetSegmentLayout(segmentCount, out _, out int rows);

        float spacingWorld = segmentSpacing * worldScale;
        float paddingWorld = 4f * worldScale;

        return rows * barHeight
            + Mathf.Max(0, rows - 1) * spacingWorld
            + paddingWorld;
    }

    void ApplyBarSize(int segmentCount)
    {
        if (healthBarCanvasRect == null)
            return;

        ConfigureSegmentsGrid();
        healthBarCanvasRect.sizeDelta = new Vector2(
            ResolveBarWidth(segmentCount) / worldScale,
            ResolveBarHeight(segmentCount) / worldScale);
    }

    float GetSegmentCanvasWidth()
    {
        return segmentWorldWidth / worldScale;
    }

    float GetSegmentCanvasHeight()
    {
        return barHeight / worldScale;
    }

    Vector3 GetBarWorldPosition()
    {
        Collider collider = selectableEntity.SelectionCollider;

        if (collider == null)
            return transform.position + Vector3.up * (1f + heightOffset);

        Bounds bounds = collider.bounds;
        return new Vector3(
            bounds.center.x,
            bounds.max.y + heightOffset,
            bounds.center.z);
    }

    public void RefreshVisibility()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = ShouldShowBar() ? 1f : 0f;
    }

    bool ShouldShowBar()
    {
        if (!IsVisibleInFogOfWar())
            return false;

        if (alwaysShowEnemyBar &&
            selectableEntity.ownerId != localPlayerOwnerId)
            return true;

        if (showWhenSelected && selectableEntity.IsSelected)
            return true;

        if (damageShowTimer > 0f)
            return true;

        return false;
    }

    bool IsVisibleInFogOfWar()
    {
        FogOfWarManager manager = FogOfWarManager.Instance;

        if (manager == null)
            return true;

        if (selectableEntity.ownerId == manager.LocalPlayerOwnerId)
            return true;

        if (fogVisibility != null)
            return fogVisibility.IsCurrentlyVisible;

        lastFogVisible = manager.EvaluateEntityGameplayVisibility(
            selectableEntity.SelectionBounds,
            lastFogVisible);
        return lastFogVisible;
    }

    void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateFill(currentHealth, maxHealth);

        if (currentHealth < maxHealth)
            damageShowTimer = showAfterDamageDuration;
    }

    void HandleDied()
    {
        if (barAnchor != null)
            barAnchor.gameObject.SetActive(false);
    }

    void UpdateFill(float currentHealth, float maxHealth)
    {
        if (segmentsRoot == null || maxHealth <= 0f || healthPerSegment <= 0f)
            return;

        float ratio = currentHealth / maxHealth;
        Color fillColor =
            ratio <= lowHealthThreshold ? lowHealthColor : baseFillColor;

        int segmentCount = GetSegmentCount(maxHealth);
        EnsureSegmentCount(segmentCount);
        ApplyBarSize(segmentCount);

        for (int i = 0; i < segmentCount; i++)
        {
            Image segment = segmentImages[i];
            float fillAmount = GetSegmentFillAmount(i, currentHealth, maxHealth);

            segment.fillAmount = fillAmount;
            segment.color = fillColor;
        }
    }

    int GetSegmentCount(float maxHealth)
    {
        return Mathf.Max(1, Mathf.CeilToInt(maxHealth / healthPerSegment));
    }

    float GetSegmentFillAmount(
        int segmentIndex,
        float currentHealth,
        float maxHealth)
    {
        float segmentStart = segmentIndex * healthPerSegment;

        if (segmentStart >= maxHealth)
            return 0f;

        float segmentEnd =
            Mathf.Min(segmentStart + healthPerSegment, maxHealth);

        float segmentCapacity = segmentEnd - segmentStart;

        if (segmentCapacity <= 0f)
            return 0f;

        float remaining =
            Mathf.Clamp(currentHealth - segmentStart, 0f, segmentCapacity);

        return remaining / segmentCapacity;
    }

    void EnsureSegmentCount(int count)
    {
        while (segmentImages.Count < count)
        {
            GameObject segmentObject = new GameObject(
                $"Segment{segmentImages.Count}",
                typeof(RectTransform));

            segmentObject.transform.SetParent(segmentsRoot, false);

            Image segmentImage = segmentObject.AddComponent<Image>();
            segmentImage.sprite = defaultSprite;
            segmentImage.material = overlayMaterial;
            segmentImage.type = Image.Type.Filled;
            segmentImage.fillMethod = Image.FillMethod.Horizontal;
            segmentImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            segmentImage.raycastTarget = false;

            segmentImages.Add(segmentImage);
        }

        for (int i = 0; i < segmentImages.Count; i++)
            segmentImages[i].gameObject.SetActive(i < count);
    }

    void UpdateVisibility(bool visible)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;
    }

    Color GetBaseFillColor()
    {
        return selectableEntity.ownerId == localPlayerOwnerId
            ? allyFillColor
            : enemyFillColor;
    }
}
