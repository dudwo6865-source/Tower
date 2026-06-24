using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(SelectableEntity))]
public class UnitMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    private SelectableEntity selectableEntity;
    private UnitCombatAI combatAI;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        selectableEntity = GetComponent<SelectableEntity>();
        combatAI = GetComponent<UnitCombatAI>();

        GridMovement.EnsureAgentOnNavMesh(agent);
    }

    void Update()
    {
        if (agent == null || !agent.isActiveAndEnabled)
            return;

        if (TowerPlacementController.Instance != null &&
            TowerPlacementController.Instance.IsPlacing)
            return;

        if (!selectableEntity.IsSelected)
            return;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        if (!Input.GetMouseButtonDown(1))
            return;

        if (Camera.main == null)
            return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        Vector2Int footprint = GridMovement.GetFootprintCells(this);
        Vector3 destination = GridMovement.SnapMoveDestination(hit.point, footprint);

        if (!GridMovement.TrySetAgentDestination(agent, destination))
            return;

        if (combatAI != null)
            combatAI.SuspendForManualMove();
    }
}
