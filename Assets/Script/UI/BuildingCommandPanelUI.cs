using UnityEngine;
using UnityEngine.UI;

public class BuildingCommandPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("건물 선택 시에만 표시할 패널 루트입니다.")]
    public GameObject panelRoot;

    [Header("Buttons")]
    [Tooltip("방어 건물(타워) 공격 대상 지정 버튼입니다.")]
    public Button attackButton;
    [Tooltip("방어 건물의 수동 공격 대상을 해제합니다.")]
    public Button stopButton;
    [Tooltip("생산 건물의 렐리 포인트를 지정합니다.")]
    public Button rallyPointButton;

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
            BindButton(stopButton, commandController.IssueBuildingStop);
            BindButton(rallyPointButton, commandController.BeginRallyPointMode);
            commandController.OnModeChanged += HandleModeChanged;
        }

        if (selectionManager != null)
            selectionManager.OnSelectionChanged += HandleSelectionChanged;

        RefreshVisibility();
    }

    void OnDestroy()
    {
        if (commandController != null)
            commandController.OnModeChanged -= HandleModeChanged;

        if (selectionManager != null)
            selectionManager.OnSelectionChanged -= HandleSelectionChanged;
    }

    void Update()
    {
        RefreshVisibility();

        if (!enableHotkeys || !IsPanelVisible())
            return;

        if (IsTextInputFocused())
            return;

        if (Input.GetKeyDown(KeyCode.A) && IsButtonActive(attackButton))
            commandController?.BeginAttackMode();
        else if (Input.GetKeyDown(KeyCode.S) && IsButtonActive(stopButton))
            commandController?.IssueBuildingStop();
        else if (Input.GetKeyDown(KeyCode.R) && IsButtonActive(rallyPointButton))
            commandController?.BeginRallyPointMode();
    }

    void HandleModeChanged(UnitCommandMode mode)
    {
        RefreshButtonHighlights(mode);
    }

    void HandleSelectionChanged()
    {
        RefreshVisibility();
    }

    public void RefreshVisibility()
    {
        bool visible = BuildingCommandHandler.ShouldShowBuildingCommandPanel();

        if (panelRoot != null)
            panelRoot.SetActive(visible);

        if (!visible)
            return;

        RefreshButtonAvailability();
    }

    void RefreshButtonAvailability()
    {
        if (!BuildingCommandHandler.TryGetCommandingBuildings(out var buildings))
            return;

        bool canAttack = false;
        bool canRally = false;

        foreach (SelectableEntity building in buildings)
        {
            if (BuildingCommandHandler.CanAttack(building))
                canAttack = true;

            if (BuildingCommandHandler.CanSetRallyPoint(building))
                canRally = true;
        }

        SetButtonActive(attackButton, canAttack);
        SetButtonActive(stopButton, canAttack);
        SetButtonActive(rallyPointButton, canRally);
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
        SetSelectedVisual(rallyPointButton, mode == UnitCommandMode.RallyPoint);
        SetSelectedVisual(stopButton, false);
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

    static void SetButtonActive(Button button, bool active)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(active);
    }

    static bool IsButtonActive(Button button)
    {
        return button != null && button.gameObject.activeSelf && button.interactable;
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
