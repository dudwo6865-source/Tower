using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(EntityHealth))]
[RequireComponent(typeof(SelectableEntity))]
public class WorldHealthBar : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("콜라이더 위쪽에서 체력바가 떠 있는 추가 높이입니다.")]
    public float heightOffset = 2f;

    [Tooltip("체력바의 월드 기준 너비입니다. 0이면 콜라이더 크기에 맞게 자동 설정합니다.")]
    public float barWidth = 0f;

    [Tooltip("체력바의 월드 기준 높이입니다.")]
    public float barHeight = 0.3f;

    [Tooltip("월드 스페이스 캔버스 스케일입니다.")]
    public float worldScale = 0.03f;

    [Header("Display")]
    [Tooltip("로컬 플레이어 소유자 ID입니다. 아군/적 체력바 표시 규칙에 사용됩니다.")]
    public int localPlayerOwnerId = 1;

    [Tooltip("적 유닛·건물의 체력바를 항상 표시합니다.")]
    public bool alwaysShowEnemyBar = true;

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
    [Tooltip("체력바 한 칸이 나타내는 체력량입니다.")]
    public float healthPerSegment = 10f;

    [Tooltip("칸 사이 간격(캔버스 픽셀)입니다.")]
    public float segmentSpacing = 2f;

    private EntityHealth entityHealth;
    private SelectableEntity selectableEntity;
    private RectTransform segmentsRoot;
    private readonly List<Image> segmentImages = new List<Image>();
    private CanvasGroup canvasGroup;
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
        if (selectableEntity.entityType == SelectableEntityType.Building)
        {
            if (barWidth <= 0f)
                barWidth = 6f;

            if (heightOffset < 0.5f)
                heightOffset = 0.5f;
        }
        else if (barWidth <= 0f)
        {
            barWidth = 1.5f;
        }
    }

    void OnDestroy()
    {
        if (entityHealth == null)
            return;

        entityHealth.OnHealthChanged -= HandleHealthChanged;
        entityHealth.OnDied -= HandleDied;
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

        barAnchor.rotation = camera.transform.rotation;
    }

    void BuildHealthBarUI()
    {
        float resolvedBarWidth = ResolveBarWidth();

        GameObject anchorObject = new GameObject("HealthBarAnchor");
        anchorObject.transform.SetParent(transform, false);
        anchorObject.transform.localPosition = GetBarLocalPosition();

        barAnchor = anchorObject.transform;

        GameObject canvasObject = new GameObject("HealthBarCanvas");
        canvasObject.transform.SetParent(anchorObject.transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 10;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta =
            new Vector2(
                resolvedBarWidth / worldScale,
                barHeight / worldScale);

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

        HorizontalLayoutGroup layout =
            segmentsObject.AddComponent<HorizontalLayoutGroup>();

        layout.spacing = segmentSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
    }

    float ResolveBarWidth()
    {
        if (barWidth > 0f)
            return barWidth;

        Collider collider = selectableEntity.SelectionCollider;

        if (collider == null)
            return 1f;

        float width = Mathf.Max(collider.bounds.size.x, collider.bounds.size.z);
        return Mathf.Clamp(width, 1.2f, 8f);
    }

    Vector3 GetBarLocalPosition()
    {
        Collider collider = selectableEntity.SelectionCollider;

        if (collider == null)
            return Vector3.up * (1f + heightOffset);

        float topHeight =
            transform.InverseTransformPoint(collider.bounds.max).y;

        return Vector3.up * (topHeight + heightOffset);
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
