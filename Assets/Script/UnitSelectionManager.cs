using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum SelectionTypeFilter
{
    All,
    UnitsOnly,
    BuildingsOnly
}

public class UnitSelectionManager : MonoBehaviour
{
    public static UnitSelectionManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("유닛 더블클릭·스페이스바 중심 이동에 사용할 RTS 카메라 컨트롤러입니다.")]
    public RTSCameraPivotController cameraController;

    [Header("Player")]
    [Tooltip("로컬 플레이어의 소유자 ID입니다. 이 ID와 같은 유닛·건물만 선택할 수 있습니다.")]
    public int localPlayerOwnerId = 1;

    [Header("Selection")]
    [Tooltip("같은 유닛을 연속 클릭할 때 더블클릭으로 인식하는 최대 시간(초)입니다.")]
    public float doubleClickThreshold = 0.3f;

    [Tooltip("이 픽셀 이상 드래그하면 클릭이 아닌 박스 선택으로 처리합니다.")]
    public float dragThreshold = 8f;

    [Tooltip("단일 클릭·더블클릭 선택에만 적용됩니다. 드래그 박스 선택은 종류와 관계없이 범위 안 전체를 고릅니다.")]
    public SelectionTypeFilter typeFilter = SelectionTypeFilter.All;

    [Tooltip("켜면 적(다른 오너) 유닛·건물도 단일 클릭으로 선택해 정보를 볼 수 있습니다. 드래그 박스·다중 선택에는 포함되지 않습니다.")]
    public bool allowEnemySingleSelect = true;

    private readonly List<SelectableEntity> selectedEntities =
        new List<SelectableEntity>();

    private SelectableEntity lastClickedEntity;
    private float lastClickTime;

    private Vector2 dragStartScreen;
    private bool isDragging;
    private bool isBoxDragging;

    private SelectionBoxUI selectionBoxUI;
    private Camera selectionCamera;

    public event System.Action OnSelectionChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        selectionCamera = Camera.main;

        if (selectionBoxUI == null)
            selectionBoxUI = FindObjectOfType<SelectionBoxUI>();

        if (selectionBoxUI == null)
        {
            GameObject boxObject = new GameObject("SelectionBoxUI");
            selectionBoxUI = boxObject.AddComponent<SelectionBoxUI>();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (selectionCamera == null)
            selectionCamera = Camera.main;

        if (Input.GetKeyDown(KeyCode.Space))
            FocusOnSelection();

        if (TowerPlacementController.Instance != null &&
            TowerPlacementController.Instance.IsPlacing)
            return;

        if (isDragging)
        {
            HandleActiveDrag();
            return;
        }

        if (IsPointerOverUI())
            return;

        if (UnitCommandController.HasInstance &&
            UnitCommandController.Instance.TryHandleCommandClick())
        {
            // 명령 클릭 처리됨 — 같은 클릭으로 선택이 바뀌지 않게 함
        }
        else if (!UnitCommandController.HasInstance ||
                 !UnitCommandController.Instance.ShouldBlockSelectionInput())
        {
            HandleSelectionInput();
        }

        HandleCommandInput();
    }

    void HandleCommandInput()
    {
        if (!Input.GetMouseButtonDown(1))
            return;

        if (UnitCommandController.HasInstance &&
            UnitCommandController.Instance.HasPendingMode)
            return;

        if (BuildingCommandHandler.TryIssueRallyPointFromRightClick())
            return;

        UnitCommandHandler.TryIssueCommandToSelection();
    }

    void HandleActiveDrag()
    {
        if (Input.GetMouseButton(0))
        {
            Vector2 current = Input.mousePosition;
            float dragDistance = Vector2.Distance(current, dragStartScreen);

            if (dragDistance >= dragThreshold)
            {
                isBoxDragging = true;
                selectionBoxUI.UpdateBox(dragStartScreen, current);
            }

            return;
        }

        if (!Input.GetMouseButtonUp(0))
            return;

        if (UnitCommandController.HasInstance &&
            UnitCommandController.Instance.ShouldBlockSelectionInput())
        {
            selectionBoxUI.Hide();
            isDragging = false;
            isBoxDragging = false;
            return;
        }

        Vector2 end = Input.mousePosition;

        if (isBoxDragging)
            HandleBoxSelect(dragStartScreen, end);
        else if (!IsPointerOverUI())
            HandleSingleClick();

        selectionBoxUI.Hide();
        isDragging = false;
        isBoxDragging = false;
    }

    void HandleSelectionInput()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (UnitCommandController.HasInstance &&
            UnitCommandController.Instance.ShouldBlockSelectionInput())
            return;

        dragStartScreen = Input.mousePosition;
        isDragging = true;
        isBoxDragging = false;
    }

    void HandleSingleClick()
    {
        Ray ray = selectionCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl))
                DeselectAll();

            return;
        }

        SelectableEntity entity =
            hit.collider.GetComponentInParent<SelectableEntity>();

        // 적(다른 오너)은 항상 단독으로만 선택한다. 시프트/컨트롤/더블클릭 다중 선택은 무시.
        if (entity != null &&
            allowEnemySingleSelect &&
            !CanSelectEntityByOwner(entity))
        {
            DeselectAll();
            SelectEntity(entity);

            lastClickedEntity = entity;
            lastClickTime = Time.time;
            return;
        }

        if (entity == null || !CanSelectEntity(entity))
        {
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl))
                DeselectAll();

            return;
        }

        bool isDoubleClick =
            lastClickedEntity == entity &&
            Time.time - lastClickTime < doubleClickThreshold;

        if (isDoubleClick)
        {
            SelectAllSameTypeOnScreen(entity);

            if (cameraController != null)
                cameraController.FocusOnPosition(GetEntityFocusPoint(entity));
        }
        else if (Input.GetKey(KeyCode.LeftControl))
        {
            ToggleSelectEntity(entity);
        }
        else
        {
            if (!Input.GetKey(KeyCode.LeftShift))
                DeselectAll();

            SelectEntity(entity);
        }

        lastClickedEntity = entity;
        lastClickTime = Time.time;
    }

    void HandleBoxSelect(Vector2 screenStart, Vector2 screenEnd)
    {
        Rect selectionRect = GetScreenRect(screenStart, screenEnd);
        List<SelectableEntity> entitiesInBox = GetEntitiesInRect(selectionRect);

        // 박스 안에 유닛과 건물이 함께 있으면 유닛만 선택한다. (유닛이 없으면 건물 선택 허용)
        entitiesInBox = PreferUnitsInBox(entitiesInBox);

        if (entitiesInBox.Count == 0)
        {
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl))
                DeselectAll();

            return;
        }

        if (Input.GetKey(KeyCode.LeftControl))
        {
            foreach (SelectableEntity entity in entitiesInBox)
                ToggleSelectEntity(entity);
        }
        else
        {
            if (!Input.GetKey(KeyCode.LeftShift))
                DeselectAll();

            foreach (SelectableEntity entity in entitiesInBox)
                SelectEntity(entity);
        }
    }

    void SelectAllSameTypeOnScreen(SelectableEntity referenceEntity)
    {
        if (!Input.GetKey(KeyCode.LeftShift))
            DeselectAll();

        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (!CanSelectEntity(entity))
                continue;

            if (!IsSameSelectionType(entity, referenceEntity))
                continue;

            if (!IsVisibleOnScreen(entity))
                continue;

            SelectEntity(entity);
        }
    }

    // 목록에 유닛이 하나라도 있으면 유닛만 남긴다. 유닛이 없으면 원본(건물 등)을 그대로 반환한다.
    static List<SelectableEntity> PreferUnitsInBox(List<SelectableEntity> entities)
    {
        bool hasUnit = false;

        foreach (SelectableEntity entity in entities)
        {
            if (entity != null && entity.entityType == SelectableEntityType.Unit)
            {
                hasUnit = true;
                break;
            }
        }

        if (!hasUnit)
            return entities;

        List<SelectableEntity> unitsOnly = new List<SelectableEntity>();

        foreach (SelectableEntity entity in entities)
        {
            if (entity != null && entity.entityType == SelectableEntityType.Unit)
                unitsOnly.Add(entity);
        }

        return unitsOnly;
    }

    List<SelectableEntity> GetEntitiesInRect(Rect screenRect)
    {
        List<SelectableEntity> result = new List<SelectableEntity>();

        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (!CanSelectEntityInBox(entity))
                continue;

            if (IsEntityInSelectionRect(entity, screenRect))
                result.Add(entity);
        }

        return result;
    }

    bool IsEntityInSelectionRect(SelectableEntity entity, Rect screenRect)
    {
        Bounds bounds = entity.SelectionBounds;
        Vector3[] corners = GetBoundsCorners(bounds);

        foreach (Vector3 corner in corners)
        {
            Vector3 screenPos = selectionCamera.WorldToScreenPoint(corner);

            if (screenPos.z < 0f)
                continue;

            if (screenRect.Contains(new Vector2(screenPos.x, screenPos.y)))
                return true;
        }

        Vector3 centerScreen =
            selectionCamera.WorldToScreenPoint(bounds.center);

        if (centerScreen.z > 0f &&
            screenRect.Contains(new Vector2(centerScreen.x, centerScreen.y)))
            return true;

        return false;
    }

    bool IsSameSelectionType(
        SelectableEntity a,
        SelectableEntity b)
    {
        if (a == null || b == null)
            return false;

        if (a.entityType != b.entityType)
            return false;

        return string.Equals(
            a.entityTypeId,
            b.entityTypeId,
            System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsVisibleOnScreen(SelectableEntity entity)
    {
        if (selectionCamera == null)
            return false;

        Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
        return IsEntityInSelectionRect(entity, screenRect);
    }

    bool CanSelectEntity(SelectableEntity entity)
    {
        if (!CanSelectEntityByOwner(entity))
            return false;

        if (typeFilter == SelectionTypeFilter.UnitsOnly &&
            entity.entityType != SelectableEntityType.Unit)
            return false;

        if (typeFilter == SelectionTypeFilter.BuildingsOnly &&
            entity.entityType != SelectableEntityType.Building)
            return false;

        return true;
    }

    bool CanSelectEntityInBox(SelectableEntity entity)
    {
        return CanSelectEntityByOwner(entity);
    }

    bool CanSelectEntityByOwner(SelectableEntity entity)
    {
        return entity != null && entity.CanBeSelectedBy(localPlayerOwnerId);
    }

    void SelectEntity(SelectableEntity entity)
    {
        if (selectedEntities.Contains(entity))
            return;

        entity.SetSelected(true);
        selectedEntities.Add(entity);
        NotifySelectionChanged();
    }

    void ToggleSelectEntity(SelectableEntity entity)
    {
        if (selectedEntities.Contains(entity))
        {
            entity.SetSelected(false);
            selectedEntities.Remove(entity);
        }
        else
        {
            SelectEntity(entity);
        }

        RefreshCommandIndicators();
        NotifySelectionChanged();
    }

    void DeselectAll()
    {
        foreach (SelectableEntity entity in selectedEntities)
        {
            if (entity != null)
                entity.SetSelected(false);
        }

        selectedEntities.Clear();
        RefreshCommandIndicators();
        NotifySelectionChanged();
    }

    void NotifySelectionChanged()
    {
        OnSelectionChanged?.Invoke();

        if (UnitCommandController.HasInstance)
            UnitCommandController.Instance.OnSelectionChanged();
    }

    void RefreshCommandIndicators()
    {
        bool hasSelectedUnit = false;

        foreach (SelectableEntity entity in selectedEntities)
        {
            if (entity != null && entity.entityType == SelectableEntityType.Unit)
            {
                hasSelectedUnit = true;
                break;
            }
        }

        if (hasSelectedUnit)
            return;

        MoveDestinationIndicator.HideIndicator();
        AttackTargetIndicator.HideIndicator();
        UnitCommandIndicatorTracker.ClearTracking();
    }

    public void NotifyEntityRemoved(SelectableEntity entity)
    {
        selectedEntities.Remove(entity);

        if (lastClickedEntity == entity)
            lastClickedEntity = null;

        RefreshCommandIndicators();
    }

    public void FocusOnSelection()
    {
        if (cameraController == null)
            return;

        Vector3 center = Vector3.zero;
        int count = 0;

        foreach (SelectableEntity entity in selectedEntities)
        {
            if (entity == null)
                continue;

            center += GetEntityFocusPoint(entity);
            count++;
        }

        if (count == 0)
            return;

        center /= count;

        cameraController.FocusOnPosition(center);
    }

    static Vector3 GetEntityFocusPoint(SelectableEntity entity)
    {
        if (entity == null)
            return Vector3.zero;

        Bounds bounds = entity.SelectionBounds;
        Vector3 point = bounds.center;
        point.y = MapPlayBounds.SampleGroundHeight(point);
        return point;
    }

    public IReadOnlyList<SelectableEntity> GetSelectedEntities()
    {
        return selectedEntities;
    }

    static Rect GetScreenRect(Vector2 start, Vector2 end)
    {
        float xMin = Mathf.Min(start.x, end.x);
        float xMax = Mathf.Max(start.x, end.x);
        float yMin = Mathf.Min(start.y, end.y);
        float yMax = Mathf.Max(start.y, end.y);

        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    static Vector3[] GetBoundsCorners(Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        return new Vector3[]
        {
            center + new Vector3(extents.x, extents.y, extents.z),
            center + new Vector3(extents.x, extents.y, -extents.z),
            center + new Vector3(extents.x, -extents.y, extents.z),
            center + new Vector3(extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x, extents.y, extents.z),
            center + new Vector3(-extents.x, extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y, extents.z),
            center + new Vector3(-extents.x, -extents.y, -extents.z)
        };
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        return EventSystem.current.IsPointerOverGameObject();
    }
}
