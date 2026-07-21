using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UnitCommandController : MonoBehaviour
{
    public static UnitCommandController Instance { get; private set; }

    [Header("Input")]
    [Tooltip("Move/Attack/Patrol 모드에서 지형·대상을 지정할 마우스 버튼입니다. (0=좌클릭, 1=우클릭)")]
    public int commandMouseButton = 0;

    [Tooltip("Esc 키로 대기 중인 명령 모드를 취소합니다.")]
    public bool cancelModeWithEscape = true;

    public UnitCommandMode ActiveMode { get; private set; } = UnitCommandMode.None;

    public bool HasPendingMode => ActiveMode != UnitCommandMode.None;

    public event Action<UnitCommandMode> OnModeChanged;

    bool suppressSelectionClick;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (Input.GetMouseButtonUp(0))
            suppressSelectionClick = false;

        if (cancelModeWithEscape &&
            Input.GetKeyDown(KeyCode.Escape) &&
            ActiveMode != UnitCommandMode.None)
        {
            CancelMode();
        }
    }

    public bool ShouldBlockSelectionInput()
    {
        return HasPendingMode || suppressSelectionClick;
    }

    public bool TryHandleCommandClick()
    {
        if (ActiveMode == UnitCommandMode.None)
            return false;

        if (commandMouseButton != 0 && !Input.GetMouseButtonDown(commandMouseButton))
            return false;

        if (commandMouseButton == 0 && !Input.GetMouseButtonDown(0))
            return false;

        if (TowerPlacementController.Instance != null &&
            TowerPlacementController.Instance.IsPlacing)
            return false;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return false;

        if (Camera.main == null)
            return false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return false;

        if (!TryExecutePendingMode(hit))
            return false;

        suppressSelectionClick = true;
        CancelMode();
        return true;
    }

    public void SetMode(UnitCommandMode mode)
    {
        if (ActiveMode == mode)
            return;

        ActiveMode = mode;
        OnModeChanged?.Invoke(ActiveMode);
    }

    public void CancelMode()
    {
        SetMode(UnitCommandMode.None);
    }

    public void IssueStop()
    {
        CancelMode();
        UnitCommandHandler.IssueStopToSelection();
    }

    public void IssueHold()
    {
        CancelMode();
        UnitCommandHandler.IssueHoldToSelection();
    }

    public void BeginMoveMode()
    {
        SetMode(UnitCommandMode.Move);
    }

    public void BeginAttackMode()
    {
        SetMode(UnitCommandMode.Attack);
    }

    public void BeginPatrolMode()
    {
        SetMode(UnitCommandMode.Patrol);
    }

    public void BeginRallyPointMode()
    {
        SetMode(UnitCommandMode.RallyPoint);
    }

    public void IssueBuildingStop()
    {
        CancelMode();
        BuildingCommandHandler.IssueStopToSelection();
    }

    public void OnSelectionChanged()
    {
        if (!UnitCommandHandler.HasCommandableUnits() &&
            !BuildingCommandHandler.HasCommandableBuildings())
            CancelMode();
    }

    bool TryExecutePendingMode(RaycastHit hit)
    {
        switch (ActiveMode)
        {
            case UnitCommandMode.Move:
                return UnitCommandHandler.IssueMoveToSelection(hit.point);

            case UnitCommandMode.Attack:
                return TryExecuteAttackMode(hit);

            case UnitCommandMode.Patrol:
                return UnitCommandHandler.IssuePatrolToSelection(hit.point);

            case UnitCommandMode.RallyPoint:
                return BuildingCommandHandler.IssueRallyPointToSelection(hit.point);

            default:
                return false;
        }
    }

    bool TryExecuteAttackMode(RaycastHit hit)
    {
        SelectableEntity clickedEntity =
            hit.collider.GetComponentInParent<SelectableEntity>();

        if (UnitCommandHandler.TryGetAttackTarget(
                clickedEntity,
                out SelectableEntity attackTarget))
        {
            if (UnitCommandHandler.IssueAttackToSelection(attackTarget))
                return true;

            return BuildingCommandHandler.IssueAttackToSelection(attackTarget);
        }

        if (UnitCommandHandler.HasCommandableUnits())
            return UnitCommandHandler.IssueAttackMoveToSelection(hit.point);

        return false;
    }

    public static bool HasInstance => Instance != null;
}
