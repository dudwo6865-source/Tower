using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(SelectableEntity))]
public class UnitMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    private SelectableEntity selectableEntity;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        selectableEntity = GetComponent<SelectableEntity>();
    }

    void Update()
    {
        if (!selectableEntity.IsSelected)
            return;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
                agent.SetDestination(hit.point);
        }
    }
}
