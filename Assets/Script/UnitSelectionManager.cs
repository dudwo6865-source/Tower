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

    [Tooltip("박스 선택 시 포함할 대상 종류를 제한합니다.")]
    public SelectionTypeFilter typeFilter = SelectionTypeFilter.All;

    private readonly List<SelectableEntity> selectedEntities =
        new List<SelectableEntity>();

    private SelectableEntity lastClickedEntity;
    private float lastClickTime;

    private Vector2 dragStartScreen;
    private bool isDragging;
    private bool isBoxDragging;

    private SelectionBoxUI selectionBoxUI;
    private Camera selectionCamera;

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

        if (IsPointerOverUI())
            return;

        HandleSelectionInput();
    }

    void HandleSelectionInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            dragStartScreen = Input.mousePosition;
            isDragging = true;
            isBoxDragging = false;
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            Vector2 current = Input.mousePosition;
            float dragDistance = Vector2.Distance(current, dragStartScreen);

            if (dragDistance >= dragThreshold)
            {
                isBoxDragging = true;
                selectionBoxUI.UpdateBox(dragStartScreen, current);
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            Vector2 end = Input.mousePosition;

            if (isBoxDragging)
                HandleBoxSelect(dragStartScreen, end);
            else
                HandleSingleClick();

            selectionBoxUI.Hide();
            isDragging = false;
            isBoxDragging = false;
        }
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
                cameraController.FocusOnPosition(entity.transform.position);
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

            if (entity.entityTypeId != referenceEntity.entityTypeId)
                continue;

            if (!IsVisibleOnScreen(entity))
                continue;

            SelectEntity(entity);
        }
    }

    List<SelectableEntity> GetEntitiesInRect(Rect screenRect)
    {
        List<SelectableEntity> result = new List<SelectableEntity>();

        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (!CanSelectEntity(entity))
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

    bool IsVisibleOnScreen(SelectableEntity entity)
    {
        Vector3 screenPos =
            selectionCamera.WorldToScreenPoint(entity.transform.position);

        if (screenPos.z < 0f)
            return false;

        return screenPos.x >= 0f &&
               screenPos.x <= Screen.width &&
               screenPos.y >= 0f &&
               screenPos.y <= Screen.height;
    }

    bool CanSelectEntity(SelectableEntity entity)
    {
        if (!entity.CanBeSelectedBy(localPlayerOwnerId))
            return false;

        if (typeFilter == SelectionTypeFilter.UnitsOnly &&
            entity.entityType != SelectableEntityType.Unit)
            return false;

        if (typeFilter == SelectionTypeFilter.BuildingsOnly &&
            entity.entityType != SelectableEntityType.Building)
            return false;

        return true;
    }

    void SelectEntity(SelectableEntity entity)
    {
        if (selectedEntities.Contains(entity))
            return;

        entity.SetSelected(true);
        selectedEntities.Add(entity);
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
    }

    void DeselectAll()
    {
        foreach (SelectableEntity entity in selectedEntities)
            entity.SetSelected(false);

        selectedEntities.Clear();
    }

    public void FocusOnSelection()
    {
        if (selectedEntities.Count == 0 || cameraController == null)
            return;

        Vector3 center = Vector3.zero;

        foreach (SelectableEntity entity in selectedEntities)
            center += entity.transform.position;

        center /= selectedEntities.Count;

        cameraController.FocusOnPosition(center);
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
