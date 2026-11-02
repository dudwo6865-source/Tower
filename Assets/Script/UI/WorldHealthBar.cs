using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(EntityHealth))]
[RequireComponent(typeof(SelectableEntity))]
public class WorldHealthBar : MonoBehaviour
{
    [Header("체력바 연결")]
    [Label("채움 이미지")]
    [Tooltip("에디터에서 만든 체력바의 Image를 연결합니다. 이미지 유형은 Filled, 채우기 방식은 Horizontal, 시작점은 Left로 설정합니다.")]
    public Image fillImage;

    [Label("표시 그룹")]
    [Tooltip("체력바 전체를 감싸는 CanvasGroup을 연결합니다. 배경·채움·숫자를 모두 이 그룹 아래에 둡니다.")]
    public CanvasGroup displayGroup;

    [Label("체력 숫자")]
    [Tooltip("선택 항목입니다. 연결하면 현재 체력 / 최대 체력을 표시합니다. 보스 체력바에 사용합니다.")]
    public TMP_Text healthText;

    [Header("자동 위치")]
    [Label("콜라이더 아래 자동 배치")]
    [Tooltip("콜라이더의 카메라 쪽 Z 가장자리를 기준으로 체력바 Z 위치만 자동 배치합니다. X와 Y는 프리팹에 설정한 위치를 따르며 콜라이더 높이는 사용하지 않습니다.")]
    public bool autoPositionBelowCollider = true;

    [Label("자동 배치 간격")]
    [Tooltip("콜라이더 가장자리와 체력 채움 영역 사이의 월드 거리입니다. 기본값으로 바로 사용할 수 있습니다.")]
    [Min(0f)]
    public float autoPositionGap = 0.1f;

    [Label("자동 배치 Z 오프셋")]
    [Tooltip("콜라이더 가장자리로 자동 배치한 위치에 더할 월드 Z 거리입니다. 양수는 +Z, 음수는 -Z 방향으로 이동합니다.")]
    public float autoPositionZOffset = 0f;

    [Header("수동 위치 추적 (자동 배치를 끈 경우)")]
    [Label("위치 추적 기준점")]
    [Tooltip("선택 항목입니다. 연결하면 이 기준점의 위치만 유닛 발밑을 따라갑니다. 비워 두면 프리팹에서 설정한 위치를 그대로 사용합니다. 유닛 본체나 그 부모는 연결하지 않습니다.")]
    public Transform followAnchor;

    [Label("높이 오프셋")]
    [Tooltip("위치 추적 기준점을 연결했을 때 바닥에서 띄울 높이입니다. Unit 또는 Building의 데이터가 있으면 데이터의 체력바 높이가 적용됩니다.")]
    public float heightOffset = 0.05f;

    [Label("화면 아래 오프셋")]
    [Tooltip("위치 추적 기준점을 연결했을 때 카메라 쪽 지면 방향으로 이동할 거리입니다.")]
    public float viewDownOffset = 3f;

    [Header("표시")]
    [Label("로컬 플레이어 ID")]
    [Tooltip("아군과 적을 구분하는 로컬 플레이어 소유자 ID입니다.")]
    public int localPlayerOwnerId = 1;

    [Label("적 체력바 항상 표시")]
    [Tooltip("켜면 현재 시야 안에 있는 적의 체력바를 항상 표시합니다. 보스 프리팹에 사용할 수 있습니다.")]
    public bool alwaysShowEnemyBar;

    [Label("선택 시 표시")]
    [Tooltip("선택한 유닛이나 건물의 체력바를 표시합니다.")]
    public bool showWhenSelected = true;

    [Label("피해 후 표시 시간(초)")]
    [Tooltip("피해를 입은 뒤 체력바를 표시하는 시간입니다.")]
    [Min(0f)]
    public float showAfterDamageDuration = 3f;

    [Label("표시 전환 속도")]
    [Tooltip("초당 투명도 변화량입니다. 0이면 즉시 전환합니다. 시야 밖·사망·비활성화 시에는 즉시 숨깁니다.")]
    [Min(0f)]
    public float fadeSpeed = 4f;

    private EntityHealth entityHealth;
    private SelectableEntity selectableEntity;
    private FogOfWarVisibility fogVisibility;
    private float damageShowTimer;
    private float visibleAlpha = 1f;
    private bool lastFogVisible = true;
    private bool validBindings;
    private WorldHealthBarView detachedView;
    private WorldHealthBarView linkedView;
    private readonly Vector3[] fillCorners = new Vector3[4];

    public bool RegisterDetachedView(WorldHealthBarView view)
    {
        if (detachedView != null && detachedView != view)
            return false;
        detachedView = view;
        return true;
    }

    void OnDestroy()
    {
        // 분리된 UI는 유닛 자식이 아니므로 유닛이 파괴될 때 함께 정리한다.
        if (detachedView != null && detachedView.IsDetached)
            Destroy(detachedView.gameObject);
    }

    void Awake()
    {
        entityHealth = GetComponent<EntityHealth>();
        selectableEntity = GetComponent<SelectableEntity>();
        fogVisibility = GetComponent<FogOfWarVisibility>();
        if (fillImage != null)
            linkedView = fillImage.GetComponentInParent<WorldHealthBarView>(true);
        if (displayGroup != null)
            visibleAlpha = displayGroup.alpha;

        validBindings = fillImage != null && displayGroup != null
            && fillImage.transform.IsChildOf(displayGroup.transform)
            && (healthText == null || healthText.transform.IsChildOf(displayGroup.transform));

        if (!validBindings && enabled)
            Debug.LogWarning("WorldHealthBar: 채움 이미지와 표시 그룹을 연결하고, 채움 이미지와 체력 숫자를 표시 그룹 아래에 배치해 주세요. UI는 자동 생성하지 않습니다.", this);
        else if (fillImage != null && fillImage.type != Image.Type.Filled && enabled)
            Debug.LogWarning("WorldHealthBar: 채움 이미지의 Image Type을 Filled로 설정해 주세요.", this);

        if (followAnchor != null && transform.IsChildOf(followAnchor))
        {
            Debug.LogWarning("WorldHealthBar: 위치 추적 기준점에는 유닛 본체나 그 부모를 연결할 수 없습니다. 위치 추적을 해제합니다.", this);
            followAnchor = null;
        }
    }

    void OnEnable()
    {
        entityHealth.OnHealthChanged += HandleHealthChanged;
        entityHealth.OnDamaged += HandleDamaged;
        entityHealth.OnDied += HandleDied;
        UpdateFill(entityHealth.CurrentHealth, entityHealth.MaxHealth);
        RefreshVisibility();
    }

    void Start()
    {
        // 다른 컴포넌트의 초기 데이터 적용이 끝난 값을 반영한다.
        UpdateFill(entityHealth.CurrentHealth, entityHealth.MaxHealth);
        RefreshVisibility();
    }

    void OnDisable()
    {
        if (entityHealth != null)
        {
            entityHealth.OnHealthChanged -= HandleHealthChanged;
            entityHealth.OnDamaged -= HandleDamaged;
            entityHealth.OnDied -= HandleDied;
        }
        damageShowTimer = 0f;
        SetAlpha(0f);
    }

    void Update()
    {
        damageShowTimer = Mathf.Max(0f, damageShowTimer - Time.deltaTime);
        if (!CanDisplay())
        {
            SetAlpha(0f);
            return;
        }

        float targetAlpha = ShouldShowBar() ? visibleAlpha : 0f;
        SetAlpha(fadeSpeed <= 0f ? targetAlpha : Mathf.MoveTowards(
            displayGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed));
    }

    void LateUpdate()
    {
        if (autoPositionBelowCollider)
        {
            // 공용 체력바는 자신의 회전·크기 처리가 끝난 뒤 자동 배치를 호출한다.
            if (linkedView == null || !linkedView.isActiveAndEnabled)
                UpdateAutomaticPosition(linkedView != null ? linkedView.transform
                    : displayGroup != null ? displayGroup.transform : null);
            return;
        }
        if (!validBindings || followAnchor == null || displayGroup.alpha <= 0f)
            return;

        Collider selectionCollider = selectableEntity.SelectionCollider;
        Vector3 position = selectionCollider == null ? transform.position
            : new Vector3(selectionCollider.bounds.center.x, selectionCollider.bounds.min.y,
                selectionCollider.bounds.center.z);
        position += Vector3.up * heightOffset;

        Camera camera = CameraVisibility.MainCamera;
        if (camera != null)
        {
            Vector3 towardCamera = -camera.transform.forward;
            towardCamera.y = 0f;
            position += towardCamera.normalized * viewDownOffset;
        }
        followAnchor.position = position;
    }

    public void UpdateAutomaticPosition(Transform barRoot)
    {
        if (!autoPositionBelowCollider || !isActiveAndEnabled || !validBindings
            || barRoot == null || transform.IsChildOf(barRoot)
            || fillImage == null || displayGroup == null || displayGroup.alpha <= 0f)
            return;

        Collider selectionCollider = selectableEntity.SelectionCollider;
        Bounds bounds = selectionCollider != null && selectionCollider.enabled
            && selectionCollider.gameObject.activeInHierarchy
            ? selectionCollider.bounds : new Bounds(transform.position, Vector3.zero);
        Camera camera = CameraVisibility.MainCamera;
        float cameraDirectionZ = camera != null ? -camera.transform.forward.z : -1f;
        if (camera != null && Mathf.Approximately(cameraDirectionZ, 0f))
            cameraDirectionZ = -camera.transform.up.z;
        float directionZ = cameraDirectionZ > 0f ? 1f : -1f;

        // 실제 채움 영역의 안쪽 끝이 콜라이더 Z 가장자리 밖에 오도록 맞춘다.
        fillImage.rectTransform.GetWorldCorners(fillCorners);
        float nearEdge = float.PositiveInfinity;
        for (int i = 0; i < fillCorners.Length; i++)
            nearEdge = Mathf.Min(nearEdge, (fillCorners[i].z - barRoot.position.z) * directionZ);

        Vector3 position = barRoot.position;
        position.z = bounds.center.z
            + directionZ * (bounds.extents.z + Mathf.Max(0f, autoPositionGap) - nearEdge)
            + autoPositionZOffset;
        barRoot.position = position;
    }

    public void RefreshVisibility()
    {
        SetAlpha(CanDisplay() && ShouldShowBar() ? visibleAlpha : 0f);
    }

    bool CanDisplay()
    {
        return isActiveAndEnabled && validBindings && displayGroup != null
            && fillImage != null && entityHealth != null && entityHealth.IsAlive
            && IsVisibleInFogOfWar();
    }

    bool ShouldShowBar()
    {
        return (alwaysShowEnemyBar && selectableEntity.ownerId != localPlayerOwnerId)
            || (showWhenSelected && selectableEntity.IsSelected)
            || damageShowTimer > 0f;
    }

    bool IsVisibleInFogOfWar()
    {
        FogOfWarManager manager = FogOfWarManager.Instance;
        if (manager == null || selectableEntity.ownerId == manager.LocalPlayerOwnerId)
            return true;
        if (fogVisibility != null)
            return fogVisibility.IsCurrentlyVisible;

        lastFogVisible = manager.EvaluateEntityGameplayVisibility(
            selectableEntity.SelectionBounds, lastFogVisible);
        return lastFogVisible;
    }

    void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateFill(currentHealth, maxHealth);
    }

    void HandleDamaged(float damage, SelectableEntity attacker)
    {
        if (damage > 0f)
            damageShowTimer = showAfterDamageDuration;
    }

    void HandleDied()
    {
        SetAlpha(0f);
    }

    void UpdateFill(float currentHealth, float maxHealth)
    {
        if (fillImage != null)
            fillImage.fillAmount = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        if (healthText != null)
            healthText.text = $"{Mathf.Ceil(Mathf.Max(0f, currentHealth)):N0} / {Mathf.Ceil(Mathf.Max(0f, maxHealth)):N0}";
    }

    void SetAlpha(float alpha)
    {
        if (displayGroup != null)
            displayGroup.alpha = alpha;
    }
}
