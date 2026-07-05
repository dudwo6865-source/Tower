using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(SelectableEntity))]
public class UnitMovement : MonoBehaviour
{
    void Start()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        GridMovement.EnsureAgentOnNavMesh(agent);
    }
}
