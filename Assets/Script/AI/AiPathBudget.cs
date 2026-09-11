using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 프레임당 무거운 NavMesh 경로 계산(접근점 탐색, SetDestination) 예산을 관리합니다.
/// 기본값은 '제한 없음'이라 모든 요청이 그 프레임에 바로 처리됩니다.
/// 유닛이 아주 많아 프레임이 튈 때만 제한을 걸어 다음 프레임으로 분산할 수 있습니다.
/// </summary>
public static class AiPathBudget
{
    /// <summary>0 이하면 제한 없음입니다.</summary>
    public static int MaxHeavyPathRequestsPerFrame = Unlimited;

    public const int Unlimited = 0;

    public static bool IsUnlimited => MaxHeavyPathRequestsPerFrame <= Unlimited;

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
        if (IsUnlimited)
            return true;

        EnsureFrame();

        if (s_HeavyUsed >= MaxHeavyPathRequestsPerFrame)
            return false;

        s_HeavyUsed++;
        return true;
    }

    /// <summary>플레이 세션이 새로 시작될 때 남아 있던 큐와 카운터를 비웁니다.</summary>
    public static void ResetForNewPlaySession()
    {
        MaxHeavyPathRequestsPerFrame = Unlimited;
        s_Frame = -1;
        s_HeavyUsed = 0;
        pending.Clear();
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

    [Tooltip("프레임당 허용하는 무거운 접근점/우회 경로 계산 횟수입니다. " +
             "0이면 제한 없음(기본)이라 명령이 그 프레임에 바로 반영됩니다. " +
             "유닛이 아주 많아 프레임이 튈 때만 10~20 정도로 올려 제한을 거세요.")]
    [Min(0)]
    public int maxHeavyPathRequestsPerFrame = AiPathBudget.Unlimited;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        AiPathBudget.ResetForNewPlaySession();
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
        maxHeavyPathRequestsPerFrame = Mathf.Max(0, maxHeavyPathRequestsPerFrame);

        if (Application.isPlaying)
            Apply();
    }

    void Apply()
    {
        AiPathBudget.MaxHeavyPathRequestsPerFrame =
            Mathf.Max(AiPathBudget.Unlimited, maxHeavyPathRequestsPerFrame);
    }
}
