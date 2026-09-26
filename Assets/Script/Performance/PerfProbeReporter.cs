using System.Text;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PerfProbe가 모은 측정값을 콘솔에 남깁니다. 씬의 아무 오브젝트에 붙여서 씁니다.
/// - 프레임이 기준 시간보다 오래 걸리면 적 생성/초기화/목표 탐색/경로 계산에 쓴 시간을 나눠 보여줍니다.
/// - 플레이어 명령마다 '경로 확정'과 '실제 이동 시작'까지 걸린 시간을 보여줍니다.
/// 측정이 끝나면 컴포넌트를 끄거나 지우면 됩니다.
/// </summary>
[DefaultExecutionOrder(10000)]
public class PerfProbeReporter : MonoBehaviour
{
    [Header("측정")]
    [Label("측정 켜기")]
    [Tooltip("켜면 항목별 시간을 잽니다. 꺼져 있어도 Unity Profiler에는 Tank.* 마커가 보입니다.")]
    public bool enableProbe = true;

    [Label("스파이크 기준(ms)")]
    [Tooltip("한 프레임이 이 시간보다 오래 걸리면 항목별 시간을 콘솔에 남깁니다.")]
    [Min(1f)]
    public float spikeThresholdMs = 25f;

    [Label("스파이크 로그 최소 간격(초)")]
    [Tooltip("스파이크 로그가 너무 많이 쌓이지 않게, 이 간격 안에 난 스파이크는 건너뜁니다.")]
    [Min(0f)]
    public float spikeLogCooldown = 0.25f;

    [Header("플레이어 명령 지연")]
    [Label("명령 지연 기록")]
    [Tooltip("플레이어 명령마다 경로 확정·이동 시작까지 걸린 시간을 남깁니다.")]
    public bool logCommandLatency = true;

    [Label("명령 추적 제한 시간(초)")]
    [Tooltip("이 시간이 지나도 움직이지 않은 유닛은 '시작 못 함'으로 집계하고 추적을 끝냅니다.")]
    [Min(0.5f)]
    public float commandTrackTimeout = 5f;

    [Label("이동 시작 판정 속도")]
    [Tooltip("유닛 속도가 이 값을 넘으면 실제로 움직이기 시작한 것으로 봅니다.")]
    [Min(0.01f)]
    public float moveStartSpeed = 0.2f;

    float lastSpikeLogTime = -999f;
    readonly StringBuilder builder = new StringBuilder(512);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        PerfProbe.Enabled = false;
        PerfProbe.ResetForNewPlaySession();
    }

    void OnEnable()
    {
        PerfProbe.Enabled = enableProbe;
        Debug.Log(
            $"PerfProbe: 측정 시작 — NavMesh.pathfindingIterationsPerFrame = {NavMesh.pathfindingIterationsPerFrame}, " +
            $"AI 경로 예산 = {(AiPathBudget.IsUnlimited ? "제한 없음" : AiPathBudget.MaxHeavyPathRequestsPerFrame + "회/프레임")}",
            this);
    }

    void OnDisable()
    {
        PerfProbe.Enabled = false;
    }

    void LateUpdate()
    {
        PerfProbe.Enabled = enableProbe;

        if (!enableProbe)
            return;

        ReportSpike();

        if (logCommandLatency)
            UpdateCommandLatency();
    }

    // 이전 프레임의 전체 시간과 이번 프레임에 모인 항목별 시간을 함께 보여준다.
    // (deltaTime은 직전 프레임 길이라서, 스파이크 프레임의 항목 시간은 같은 줄이나 바로 다음 줄에 보인다)
    void ReportSpike()
    {
        double totalProbeMs = 0;

        for (int i = 0; i < (int)PerfCategory.Count; i++)
        {
            PerfProbe.GetFrameStats((PerfCategory)i, out double ms, out _);
            totalProbeMs += ms;
        }

        float frameMs = Time.unscaledDeltaTime * 1000f;

        if (frameMs < spikeThresholdMs && totalProbeMs < spikeThresholdMs)
            return;

        if (Time.realtimeSinceStartup - lastSpikeLogTime < spikeLogCooldown)
            return;

        lastSpikeLogTime = Time.realtimeSinceStartup;

        builder.Clear();
        builder.Append($"PerfProbe: 프레임 {Time.frameCount} 스파이크 — 직전 프레임 {frameMs:0.0}ms, 측정 항목 합계 {totalProbeMs:0.0}ms");

        for (int i = 0; i < (int)PerfCategory.Count; i++)
        {
            PerfCategory category = (PerfCategory)i;
            PerfProbe.GetFrameStats(category, out double ms, out int count);

            if (count == 0)
                continue;

            builder.Append($"\n  · {PerfProbe.GetName(category)}: {ms:0.00}ms ({count}회)");
        }

        builder.Append($"\n  · 경로 대기 중(pathPending) 에이전트: {CountPendingAgents(out int agentCount)} / {agentCount}");
        Debug.Log(builder.ToString(), this);
    }

    static int CountPendingAgents(out int agentCount)
    {
        int pending = 0;
        agentCount = 0;

        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (entity == null)
                continue;

            NavMeshAgent agent = entity.GetComponent<NavMeshAgent>();

            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            agentCount++;

            if (agent.pathPending)
                pending++;
        }

        return pending;
    }

    void UpdateCommandLatency()
    {
        var batches = PerfProbe.ActiveBatches;
        float now = Time.realtimeSinceStartup;

        for (int b = batches.Count - 1; b >= 0; b--)
        {
            PerfProbe.CommandBatch batch = batches[b];
            bool allDone = true;

            for (int i = 0; i < batch.agents.Count; i++)
            {
                NavMeshAgent agent = batch.agents[i];

                if (agent == null || !agent.isActiveAndEnabled)
                    continue;

                if (batch.pathResolvedTimes[i] < 0f && !agent.pathPending)
                    batch.pathResolvedTimes[i] = now - batch.issuedTime;

                // 이미 움직이던 유닛은 새 경로를 받기 전에도 속도가 있으므로,
                // 경로가 확정된 뒤의 움직임만 '이동 시작'으로 본다.
                if (batch.moveStartTimes[i] < 0f &&
                    batch.pathResolvedTimes[i] >= 0f &&
                    agent.velocity.sqrMagnitude >= moveStartSpeed * moveStartSpeed)
                    batch.moveStartTimes[i] = now - batch.issuedTime;

                if (batch.moveStartTimes[i] < 0f)
                    allDone = false;
            }

            bool timedOut = now - batch.issuedTime >= commandTrackTimeout;

            if (!allDone && !timedOut)
                continue;

            LogBatch(batch);
            PerfProbe.RemoveBatchAt(b);
        }
    }

    void LogBatch(PerfProbe.CommandBatch batch)
    {
        Summarize(batch.pathResolvedTimes, out float pathAvg, out float pathMax, out int pathMissing);
        Summarize(batch.moveStartTimes, out float moveAvg, out float moveMax, out int moveMissing);

        string message =
            $"PerfProbe: 명령 #{batch.id} ({batch.name}, {batch.agents.Count}기, 프레임 {batch.issuedFrame}) — " +
            $"경로 확정 평균 {pathAvg:0.000}s / 최대 {pathMax:0.000}s" +
            (pathMissing > 0 ? $" (미확정 {pathMissing}기)" : string.Empty) +
            $", 이동 시작 평균 {moveAvg:0.000}s / 최대 {moveMax:0.000}s" +
            (moveMissing > 0 ? $" (시작 못 함 {moveMissing}기)" : string.Empty);

        bool slow = pathMax >= 0.2f || moveMax >= 0.5f || pathMissing > 0 || moveMissing > 0;

        if (slow)
            Debug.LogWarning(message, this);
        else
            Debug.Log(message, this);
    }

    static void Summarize(
        System.Collections.Generic.List<float> values,
        out float average,
        out float max,
        out int missing)
    {
        float sum = 0f;
        int count = 0;
        max = 0f;
        missing = 0;

        foreach (float value in values)
        {
            if (value < 0f)
            {
                missing++;
                continue;
            }

            sum += value;
            count++;
            max = Mathf.Max(max, value);
        }

        average = count > 0 ? sum / count : 0f;
    }
}
