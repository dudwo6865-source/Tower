using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class UnitCommandIndicatorTracker : MonoBehaviour
{
    public static UnitCommandIndicatorTracker Instance { get; private set; }

    [SerializeField] float arrivalPadding = 0.35f;

    readonly List<MoveTrackEntry> moveTracks = new List<MoveTrackEntry>();
    readonly List<AttackTrackEntry> attackTracks = new List<AttackTrackEntry>();
    Vector3 moveIndicatorPosition;
    Color moveIndicatorColor = MoveDestinationIndicator.MoveColor;
    SelectableEntity attackTarget;

    struct MoveTrackEntry
    {
        public NavMeshAgent agent;
        public Vector3 destination;
    }

    struct AttackTrackEntry
    {
        public UnitAttacker attacker;
        public SelectableEntity target;
    }

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

    void LateUpdate()
    {
        UpdateMoveTracking();
        UpdateAttackTracking();
    }

    public static void TrackMoveToPoint(
        IEnumerable<(NavMeshAgent agent, Vector3 destination)> agents,
        Vector3 indicatorPosition)
    {
        TrackMoveToPoint(agents, indicatorPosition, MoveDestinationIndicator.MoveColor);
    }

    public static void TrackMoveToPoint(
        IEnumerable<(NavMeshAgent agent, Vector3 destination)> agents,
        Vector3 indicatorPosition,
        Color indicatorColor)
    {
        EnsureInstance();
        if (Instance == null)
            return;

        Instance.BeginMoveTracking(agents, indicatorPosition, indicatorColor);
    }

    public static void TrackAttackTarget(
        IEnumerable<(UnitAttacker attacker, SelectableEntity target)> attackers,
        SelectableEntity target)
    {
        EnsureInstance();
        if (Instance == null || target == null)
            return;

        Instance.BeginAttackTracking(attackers, target);
    }

    public static void ClearTracking()
    {
        if (Instance == null)
            return;

        Instance.ClearAll();
    }

    static void EnsureInstance()
    {
        if (Instance != null)
            return;

        GameObject trackerObject = new GameObject("UnitCommandIndicatorTracker");
        trackerObject.AddComponent<UnitCommandIndicatorTracker>();
    }

    void BeginMoveTracking(
        IEnumerable<(NavMeshAgent agent, Vector3 destination)> agents,
        Vector3 indicatorPosition,
        Color indicatorColor)
    {
        moveTracks.Clear();
        attackTracks.Clear();
        attackTarget = null;
        moveIndicatorPosition = indicatorPosition;
        moveIndicatorColor = indicatorColor;

        foreach ((NavMeshAgent agent, Vector3 destination) entry in agents)
        {
            if (entry.agent == null)
                continue;

            moveTracks.Add(new MoveTrackEntry
            {
                agent = entry.agent,
                destination = entry.destination
            });
        }

        if (moveTracks.Count == 0)
        {
            MoveDestinationIndicator.HideIndicator();
            return;
        }

        AttackTargetIndicator.HideIndicator();
        MoveDestinationIndicator.ShowAt(moveIndicatorPosition, moveIndicatorColor);
    }

    void BeginAttackTracking(
        IEnumerable<(UnitAttacker attacker, SelectableEntity target)> attackers,
        SelectableEntity target)
    {
        moveTracks.Clear();
        attackTracks.Clear();
        attackTarget = target;

        foreach (var entry in attackers)
        {
            if (entry.attacker == null || entry.target == null)
                continue;

            attackTracks.Add(new AttackTrackEntry
            {
                attacker = entry.attacker,
                target = entry.target
            });
        }

        if (attackTracks.Count == 0)
        {
            AttackTargetIndicator.HideIndicator();
            return;
        }

        MoveDestinationIndicator.HideIndicator();
        AttackTargetIndicator.ShowOn(attackTarget);
    }

    void UpdateMoveTracking()
    {
        if (moveTracks.Count == 0)
            return;

        for (int i = moveTracks.Count - 1; i >= 0; i--)
        {
            MoveTrackEntry track = moveTracks[i];

            if (HasReachedMoveDestination(track.agent, track.destination))
                moveTracks.RemoveAt(i);
        }

        if (moveTracks.Count == 0)
            MoveDestinationIndicator.HideIndicator();
        else
            MoveDestinationIndicator.ShowAt(moveIndicatorPosition, moveIndicatorColor);
    }

    void UpdateAttackTracking()
    {
        if (attackTracks.Count == 0)
            return;

        if (!IsAttackTargetValid(attackTarget))
        {
            ClearAttackTracking();
            return;
        }

        for (int i = attackTracks.Count - 1; i >= 0; i--)
        {
            AttackTrackEntry track = attackTracks[i];

            if (track.attacker == null || track.target == null)
            {
                attackTracks.RemoveAt(i);
                continue;
            }

            if (track.attacker.IsInRange(track.target))
                attackTracks.RemoveAt(i);
        }

        if (attackTracks.Count == 0)
            AttackTargetIndicator.HideIndicator();
        else
            AttackTargetIndicator.ShowOn(attackTarget);
    }

    bool HasReachedMoveDestination(NavMeshAgent agent, Vector3 destination)
    {
        if (agent == null || !agent.isActiveAndEnabled)
            return true;

        if (!agent.isOnNavMesh)
            return true;

        float threshold = agent.stoppingDistance + arrivalPadding;

        if (agent.pathPending)
            return false;

        if (agent.hasPath)
            return agent.remainingDistance <= threshold;

        Vector3 flatDelta = agent.transform.position - destination;
        flatDelta.y = 0f;
        return flatDelta.sqrMagnitude <= threshold * threshold;
    }

    static bool IsAttackTargetValid(SelectableEntity target)
    {
        if (target == null || !target.isActiveAndEnabled)
            return false;

        EntityHealth health = target.GetComponent<EntityHealth>();
        return health == null || health.IsAlive;
    }

    void ClearAttackTracking()
    {
        attackTracks.Clear();
        attackTarget = null;
        AttackTargetIndicator.HideIndicator();
    }

    void ClearAll()
    {
        moveTracks.Clear();
        attackTracks.Clear();
        attackTarget = null;
        MoveDestinationIndicator.HideIndicator();
        AttackTargetIndicator.HideIndicator();
    }
}
