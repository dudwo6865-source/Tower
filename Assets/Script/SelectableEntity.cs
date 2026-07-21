using UnityEngine;

public enum SelectableEntityType
{
    Unit,
    Building
}

public class SelectableEntity : MonoBehaviour
{
    [Header("Selection")]
    [Tooltip("이 오브젝트의 종류입니다. 유닛 또는 건물.")]
    public SelectableEntityType entityType = SelectableEntityType.Unit;

    [Tooltip("소유 플레이어 ID입니다. 로컬 플레이어와 같아야 선택할 수 있습니다.")]
    public int ownerId = 1;

    [Tooltip("같은 타입 전체 선택(더블클릭)에 사용되는 타입 ID입니다. 예: tank, barracks")]
    public string entityTypeId = "unit";

    [Tooltip("선택/체력바 기준이 되는 콜라이더입니다. 비워두면 자식에서 자동으로 찾습니다. (Root 본의 콜라이더 등)")]
    public Collider selectionCollider;

    [Header("UI")]
    [Tooltip("선택 정보 패널 등에 표시할 초상화입니다. 비워두면 UnitData.portrait를 사용합니다.")]
    public Sprite portrait;

    [Header("Health")]
    [Tooltip("EntityHealth와 WorldHealthBar가 없으면 자동으로 추가합니다.")]
    public bool autoSetupHealth = true;

    public bool IsSelected { get; private set; }

    private SelectionRingIndicator ringIndicator;

    public Collider SelectionCollider
    {
        get
        {
            if (selectionCollider == null)
                selectionCollider = GetComponentInChildren<Collider>();

            return selectionCollider;
        }
    }

    public Bounds SelectionBounds
    {
        get
        {
            Collider collider = SelectionCollider;

            if (collider != null)
                return collider.bounds;

            return new Bounds(transform.position, Vector3.one);
        }
    }

    void Awake()
    {
        if (autoSetupHealth)
            EnsureHealthComponents();
    }

    void OnEnable()
    {
        SelectableRegistry.Register(this);
    }

    void OnDisable()
    {
        IsSelected = false;

        if (ringIndicator != null)
            ringIndicator.SetVisible(false);

        if (UnitSelectionManager.Instance != null)
            UnitSelectionManager.Instance.NotifyEntityRemoved(this);

        SelectableRegistry.Unregister(this);
    }

    public bool CanBeSelectedBy(int localPlayerOwnerId)
    {
        return ownerId == localPlayerOwnerId;
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        SelectionRingIndicator ring = GetOrCreateRingIndicator();
        ring.SetColor(IsEnemyOfLocalPlayer()
            ? SelectionRingIndicator.EnemyRingColor
            : SelectionRingIndicator.AllyRingColor);
        ring.SetVisible(selected);

        WorldHealthBar healthBar = GetComponent<WorldHealthBar>();

        if (healthBar != null)
            healthBar.RefreshVisibility();
    }

    bool IsEnemyOfLocalPlayer()
    {
        UnitSelectionManager manager = UnitSelectionManager.Instance;
        return manager != null && ownerId != manager.localPlayerOwnerId;
    }

    void EnsureHealthComponents()
    {
        EntityHealth health = GetComponent<EntityHealth>();

        if (health == null)
        {
            health = gameObject.AddComponent<EntityHealth>();
            health.maxHealth =
                entityType == SelectableEntityType.Building ? 200f : 80f;
        }

        WorldHealthBar healthBar = GetComponent<WorldHealthBar>();

        if (healthBar != null)
            return;

        gameObject.AddComponent<WorldHealthBar>();
    }

    float GetRingRadius()
    {
        Collider collider = SelectionCollider;

        if (collider == null)
            return 1.5f;

        Vector3 extents = collider.bounds.extents;
        return Mathf.Max(extents.x, extents.z) * 1.1f;
    }

    SelectionRingIndicator GetOrCreateRingIndicator()
    {
        if (ringIndicator != null)
            return ringIndicator;

        GameObject ringObject = new GameObject("SelectionRing");
        ringObject.transform.SetParent(transform, false);

        ringIndicator = ringObject.AddComponent<SelectionRingIndicator>();
        ringIndicator.Initialize(GetRingRadius());

        return ringIndicator;
    }
}
