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
    public static int MaxHeavyPathRequestsPerFrame = DefaultMaxHeavyPathRequestsPerFrame;

    /// <summary>
    /// 한 프레임에 허용하는 무거운 경로 계산 기본값입니다. 타워를 지으면 NavMesh가
    /// carve되면서 근처 유닛이 한꺼번에 경로를 다시 잡는데, 그게 한 프레임에 몰리면
    /// 화면이 잠깐 멈춥니다. 예산을 넘긴 유닛은 그 프레임엔 기존 경로로 계속 걷고
    /// 다음 프레임에 다시 시도합니다.
    /// </summary>
    public const int DefaultMaxHeavyPathRequestsPerFrame = 16;

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
        MaxHeavyPathRequestsPerFrame = DefaultMaxHeavyPathRequestsPerFrame;
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

    [Tooltip("프레임당 허용하는 무거운 접근점/추격 경로 계산 횟수입니다. " +
             "0이면 제한 없음이라 유닛이 많을 때 한 프레임에 계산이 몰려 화면이 멈칫합니다. " +
             "예산을 넘긴 유닛은 기존 경로로 계속 걷다가 다음 프레임에 다시 시도합니다. " +
             "플레이어 명령은 이 제한을 받지 않고 항상 즉시 처리됩니다.")]
    [Min(0)]
    public int maxHeavyPathRequestsPerFrame =
        AiPathBudget.DefaultMaxHeavyPathRequestsPerFrame;

    [Tooltip("건물 주변 접근점 하나를 고를 때 돌려볼 경로 계산 최대 횟수입니다. " +
             "표적이 벽이나 타워로 완전히 막혔을 때만 이 횟수까지 올라갑니다. " +
             "낮출수록 가볍고, 올릴수록 막힌 건물 주변을 더 꼼꼼히 돌아봅니다.")]
    [Min(1)]
    public int maxBuildingApproachPathCalculations = 6;

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
        maxBuildingApproachPathCalculations =
            Mathf.Max(1, maxBuildingApproachPathCalculations);

        if (Application.isPlaying)
            Apply();
    }

    void Apply()
    {
        AiPathBudget.MaxHeavyPathRequestsPerFrame =
            Mathf.Max(AiPathBudget.Unlimited, maxHeavyPathRequestsPerFrame);

        TargetFinder.MaxBuildingApproachPathCalculations =
            Mathf.Max(1, maxBuildingApproachPathCalculations);
    }
}
