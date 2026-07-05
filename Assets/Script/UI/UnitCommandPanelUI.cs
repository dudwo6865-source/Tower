using UnityEngine;
using UnityEngine.UI;

public class UnitCommandPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("유닛 선택 시에만 표시할 패널 루트입니다.")]
    public GameObject panelRoot;

    [Header("Buttons")]
    public Button attackButton;
    public Button moveButton;
    public Button stopButton;
    public Button holdButton;
    public Button scoutButton;

    [Header("Hotkeys")]
    public bool enableHotkeys = true;

    UnitCommandController commandController;
    UnitSelectionManager selectionManager;

    void Start()
    {
        commandController = UnitCommandController.Instance;

        if (commandController == null)
            commandController = FindObjectOfType<UnitCommandController>();

        selectionManager = UnitSelectionManager.Instance;

        if (selectionManager == null)
            selectionManager = FindObjectOfType<UnitSelectionManager>();

        if (commandController != null)
        {
            BindButton(attackButton, commandController.BeginAttackMode);
            BindButton(moveButton, commandController.BeginMoveMode);
            BindButton(stopButton, commandController.IssueStop);
            BindButton(holdButton, commandController.IssueHold);
            BindButton(scoutButton, commandController.BeginPatrolMode);
            commandController.OnModeChanged += HandleModeChanged;
        }

        RefreshVisibility();
    }

    void OnDestroy()
    {
        if (commandController != null)
            commandController.OnModeChanged -= HandleModeChanged;
    }

    void Update()
    {
        RefreshVisibility();

        if (!enableHotkeys || !IsPanelVisible())
            return;

        if (IsTextInputFocused())
            return;

        if (Input.GetKeyDown(KeyCode.A))
            commandController?.BeginAttackMode();
        else if (Input.GetKeyDown(KeyCode.M))
            commandController?.BeginMoveMode();
        else if (Input.GetKeyDown(KeyCode.S))
            commandController?.IssueStop();
        else if (Input.GetKeyDown(KeyCode.H))
            commandController?.IssueHold();
        else if (Input.GetKeyDown(KeyCode.P))
            commandController?.BeginPatrolMode();
    }

    void HandleModeChanged(UnitCommandMode mode)
    {
        RefreshButtonHighlights(mode);
    }

    public void RefreshVisibility()
    {
        bool visible = UnitCommandHandler.HasCommandableUnits();

        if (panelRoot != null)
            panelRoot.SetActive(visible);
    }

    bool IsPanelVisible()
    {
        return panelRoot != null && panelRoot.activeSelf;
    }

    static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
            return;

        button.onClick.AddListener(action);
    }

    void RefreshButtonHighlights(UnitCommandMode mode)
    {
        SetSelectedVisual(attackButton, mode == UnitCommandMode.Attack);
        SetSelectedVisual(moveButton, mode == UnitCommandMode.Move);
        SetSelectedVisual(scoutButton, mode == UnitCommandMode.Patrol);
        SetSelectedVisual(stopButton, false);
        SetSelectedVisual(holdButton, false);
    }

    static void SetSelectedVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = selected
            ? new Color(0.75f, 0.9f, 1f, 1f)
            : Color.white;
        button.colors = colors;
    }

    static bool IsTextInputFocused()
    {
        GameObject current = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        if (current == null)
            return false;

        return current.GetComponent<InputField>() != null ||
               current.GetComponent<TMPro.TMP_InputField>() != null;
    }
}
