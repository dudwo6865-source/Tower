using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UnitCommandController : MonoBehaviour
{
    public static UnitCommandController Instance { get; private set; }

    [Header("입력")]
    [Label("명령 마우스 버튼")]
    [Tooltip("Move/Attack/Patrol 모드에서 지형·대상을 지정할 마우스 버튼입니다. (0=좌클릭, 1=우클릭)")]
    public int commandMouseButton = 0;

    [Label("Esc로 모드 취소")]
    [Tooltip("Esc 키로 대기 중인 명령 모드를 취소합니다.")]
    public bool cancelModeWithEscape = true;

    [Label("우클릭으로 모드 취소")]
    [Tooltip("우클릭으로 대기 중인 명령 모드를 취소합니다. 취소에 쓰인 우클릭은 이동 명령으로 이어지지 않습니다.")]
    public bool cancelModeWithRightClick = true;

    [Header("명령 커서 링")]
    [Label("정찰 커서 색")]
    [Tooltip("정찰(Patrol) 명령의 커서 링 색입니다.")]
    public Color patrolCursorColor = new Color(0.3f, 0.7f, 1f, 0.95f);

    [Label("집결지 커서 색")]
    [Tooltip("집결지(Rally Point) 명령의 커서 링 색입니다.")]
    public Color rallyCursorColor = new Color(1f, 0.85f, 0.25f, 0.95f);

    [Label("지면 표시 유지 시간(초)")]
    [Tooltip("움직일 수 없는 건물만 선택한 채로 바닥에 명령을 찍었을 때, 지면 표시가 남아 있는 시간(초)입니다.")]
    public float groundCommandMarkerSeconds = 1.2f;

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

        UpdateCommandCursorIndicator();
    }

    // 명령 모드가 켜져 있는 동안 마우스 아래 지면 위치에 명령 링을 표시한다.
    void UpdateCommandCursorIndicator()
    {
        if (!HasPendingMode)
            return;

        if (Camera.main == null ||
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            CommandCursorIndicator.HideIndicator();
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            CommandCursorIndicator.HideIndicator();
            return;
        }

        CommandCursorIndicator.ShowAt(
            hit.point,
            GetCursorRadius(ActiveMode),
            GetCursorColor(ActiveMode));
    }

    public Color GetCursorColor(UnitCommandMode mode)
    {
        switch (mode)
        {
            case UnitCommandMode.Attack:
                return MoveDestinationIndicator.AttackMoveColor;

            case UnitCommandMode.Patrol:
                return patrolCursorColor;

            case UnitCommandMode.RallyPoint:
                return rallyCursorColor;

            default:
                return MoveDestinationIndicator.MoveColor;
        }
    }

    float GetCursorRadius(UnitCommandMode mode)
    {
        // 공격 명령만 실제 타격 범위를 보여줄 의미가 있다. 나머지는 기본 크기.
        if (mode != UnitCommandMode.Attack)
            return CommandCursorIndicator.DefaultRadius;

        return GetAttackCursorRadius();
    }

    // 선택 중인 유닛이 대포(Cannon)라면 스플래시 범위만큼 미리보기 원을 키운다.
    float GetAttackCursorRadius()
    {
        float radius = CommandCursorIndicator.DefaultRadius;

        if (UnitSelectionManager.Instance == null)
            return radius;

        int localOwnerId = UnitSelectionManager.Instance.localPlayerOwnerId;

        foreach (SelectableEntity entity in UnitSelectionManager.Instance.GetSelectedEntities())
        {
            if (entity == null || entity.ownerId != localOwnerId)
                continue;

            UnitAttacker attacker = entity.GetComponent<UnitAttacker>();

            if (attacker == null || attacker.attackType != AttackType.Cannon)
                continue;

            radius = Mathf.Max(radius, attacker.splashRadius);
        }

        return radius;
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

        // 명령이 실제로 전달됐을 때만 확정 피드백을 재생한다.
        // CancelMode보다 먼저 호출해야 원이 그 자리에서 한 번 튀고 사라진다.
        CommandCursorIndicator.PlayConfirmPulse(hit.point);

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

        if (HasPendingMode)
            CommandCursorIndicator.PlayActivationPulse();
        else
            CommandCursorIndicator.HideIndicator();
    }

    public void CancelMode()
    {
        SetMode(UnitCommandMode.None);
    }

    /// <summary>
    /// 명령 모드가 대기 중일 때 들어온 우클릭을 '취소'로 소비합니다.
    /// 건물 배치(TowerPlacementController)의 우클릭 취소와 동작을 맞춥니다.
    /// 소비했으면 true를 돌려주므로, 호출한 쪽은 같은 우클릭으로 이동 명령까지 내리지 않게 할 수 있습니다.
    /// </summary>
    public bool TryCancelModeWithRightClick()
    {
        if (!cancelModeWithRightClick || !HasPendingMode)
            return false;

        if (!Input.GetMouseButtonDown(1))
            return false;

        CancelMode();
        return true;
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

        // 소속을 가리지 않는다. 아군을 찍으면 아군을 공격하는 강제 공격이 의도된 동작이다.
        if (UnitCommandHandler.TryGetAttackTarget(
                clickedEntity,
                out SelectableEntity attackTarget))
        {
            if (UnitCommandHandler.IssueAttackToSelection(attackTarget))
                return true;

            if (BuildingCommandHandler.IssueAttackToSelection(attackTarget))
                return true;
        }

        // 공격 이동: 찍은 지점으로 이동하면서 도중에 만나는 적과 교전한다.
        if (UnitCommandHandler.IssueAttackMoveToSelection(hit.point))
            return true;

        // 움직일 수 없는 건물(타워)만 선택한 경우다. 실제로 갈 곳은 없지만
        // 클릭을 삼켜버리면 명령이 씹힌 것처럼 보이므로, 지면에 표시만 남기고 받아들인다.
        if (BuildingCommandHandler.HasCommandableBuildings())
        {
            UnitCommandIndicatorTracker.ShowPointMarker(
                hit.point,
                MoveDestinationIndicator.AttackMoveColor,
                groundCommandMarkerSeconds);

            return true;
        }

        return false;
    }

    public static bool HasInstance => Instance != null;
}
