using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(EntityHealth))]
[RequireComponent(typeof(SelectableEntity))]
public class WorldHealthBar : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("콜라이더 위쪽에서 체력바가 떠 있는 추가 높이입니다.")]
    public float heightOffset = 0.3f;

    [Tooltip("체력바의 월드 기준 너비입니다. 0이면 콜라이더 크기에 맞게 자동 설정합니다.")]
    public float barWidth = 0f;

    [Tooltip("체력바의 월드 기준 높이입니다.")]
    public float barHeight = 0.12f;

    [Tooltip("월드 스페이스 캔버스 스케일입니다.")]
    public float worldScale = 0.01f;

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

    private EntityHealth entityHealth;
    private SelectableEntity selectableEntity;
    private Image fillImage;
    private CanvasGroup canvasGroup;
    private Transform barAnchor;
    private float damageShowTimer;
    private Color baseFillColor;
    private Sprite defaultSprite;
    private Material overlayMaterial;

    void Awake()
    {
        entityHealth = GetComponent<EntityHealth>();
        selectableEntity = GetComponent<SelectableEntity>();

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

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(backgroundObject.transform, false);

        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        fillImage = fillObject.AddComponent<Image>();
        fillImage.sprite = defaultSprite;
        fillImage.material = overlayMaterial;
        fillImage.color = baseFillColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.raycastTarget = false;
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
        if (alwaysShowEnemyBar &&
            selectableEntity.ownerId != localPlayerOwnerId)
            return true;

        if (showWhenSelected && selectableEntity.IsSelected)
            return true;

        if (damageShowTimer > 0f)
            return true;

        return false;
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
        if (fillImage == null || maxHealth <= 0f)
            return;

        float ratio = currentHealth / maxHealth;
        fillImage.fillAmount = ratio;

        if (ratio <= lowHealthThreshold)
            fillImage.color = lowHealthColor;
        else
            fillImage.color = baseFillColor;
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
