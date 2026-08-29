using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 프레임당 무거운 NavMesh 경로 계산(접근점 탐색, SetDestination) 예산을 관리합니다.
/// 초과 요청은 큐에 넣었다가 다음 프레임에 이어서 처리합니다.
/// </summary>
public static class AiPathBudget
{
    public static int MaxHeavyPathRequestsPerFrame = 10;

    static int s_Frame = -1;
    static int s_HeavyUsed;
    static readonly List<PendingDestination> pending =
        new List<PendingDestination>(32);

    struct PendingDestination
    {
        public NavMeshAgent agent;
        public Vector3 destination;
    }

    public static int HeavyUsedThisFrame
    {
        get
        {
            EnsureFrame();
            return s_HeavyUsed;
        }
    }

    public static bool TryAcquireHeavy()
    {
        EnsureFrame();

        if (s_HeavyUsed >= MaxHeavyPathRequestsPerFrame)
            return false;

        s_HeavyUsed++;
        return true;
    }

    public static void EnqueueDestination(NavMeshAgent agent, Vector3 destination)
    {
        if (agent == null)
            return;

        for (int i = 0; i < pending.Count; i++)
        {
            if (pending[i].agent != agent)
                continue;

            pending[i] = new PendingDestination
            {
                agent = agent,
                destination = destination
            };
            return;
        }

        pending.Add(new PendingDestination
        {
            agent = agent,
            destination = destination
        });
    }

    public static void ProcessPending()
    {
        int index = 0;

        while (index < pending.Count)
        {
            PendingDestination request = pending[index];

            if (request.agent == null || !request.agent.isActiveAndEnabled)
            {
                pending.RemoveAt(index);
                continue;
            }

            if (!TryAcquireHeavy())
                return;

            pending.RemoveAt(index);
            GridMovement.TrySetAgentDestinationImmediate(
                request.agent,
                request.destination);
        }
    }

    static void EnsureFrame()
    {
        int frame = Time.frameCount;

        if (frame == s_Frame)
            return;

        s_Frame = frame;
        s_HeavyUsed = 0;
    }
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(-400)]
public class AiPathBudgetSettings : MonoBehaviour
{
    public static AiPathBudgetSettings Instance { get; private set; }

    [Tooltip("프레임당 허용하는 무거운 접근점/우회 경로 계산 횟수입니다. 낮을수록 hitch가 줄고, 적이 움직임을 시작하는 데 조금 더 걸릴 수 있습니다.")]
    [Min(1)]
    public int maxHeavyPathRequestsPerFrame = 10;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Apply();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        AiPathBudget.ProcessPending();
    }

    void OnValidate()
    {
        maxHeavyPathRequestsPerFrame = Mathf.Max(1, maxHeavyPathRequestsPerFrame);

        if (Application.isPlaying)
            Apply();
    }

    void Apply()
    {
        AiPathBudget.MaxHeavyPathRequestsPerFrame =
            Mathf.Max(1, maxHeavyPathRequestsPerFrame);
    }
}
