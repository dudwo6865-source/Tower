using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;

/// <summary>프레임 병목을 나눠서 재는 항목입니다.</summary>
public enum PerfCategory
{
    EnemySpawn,
    EnemyInit,
    TargetSearch,
    PathCalc,
    Count
}

/// <summary>
/// 적 생성·초기화·목표 탐색·경로 계산에 걸린 시간을 프레임 단위로 모읍니다.
/// Unity Profiler에도 같은 이름의 마커로 보이고, PerfProbeReporter가 켜져 있으면
/// 프레임이 튈 때 항목별 시간을 콘솔에 남깁니다.
/// 플레이어 명령이 실제 이동으로 이어지기까지의 지연도 함께 잽니다.
/// </summary>
public static class PerfProbe
{
    /// <summary>꺼져 있으면 시간 측정을 건너뜁니다. (Profiler 마커는 항상 남습니다)</summary>
    public static bool Enabled;

    static readonly string[] CategoryNames =
    {
        "적 생성(Instantiate)",
        "적 초기화",
        "목표 탐색",
        "경로 계산",
    };

    static readonly ProfilerMarker[] Markers =
    {
        new ProfilerMarker("Tank.EnemySpawn"),
        new ProfilerMarker("Tank.EnemyInit"),
        new ProfilerMarker("Tank.TargetSearch"),
        new ProfilerMarker("Tank.PathCalc"),
    };

    static readonly long[] frameTicks = new long[(int)PerfCategory.Count];
    static readonly int[] frameCounts = new int[(int)PerfCategory.Count];
    static int accumulatingFrame = -1;

    public static string GetName(PerfCategory category) => CategoryNames[(int)category];

    public readonly struct Scope : IDisposable
    {
        readonly PerfCategory category;
        readonly long startTicks;
        readonly bool timed;

        public Scope(PerfCategory category)
        {
            this.category = category;
            Markers[(int)category].Begin();
            timed = Enabled;
            startTicks = timed ? Stopwatch.GetTimestamp() : 0;
        }

        public void Dispose()
        {
            Markers[(int)category].End();

            if (!timed)
                return;

            EnsureFrame();
            frameTicks[(int)category] += Stopwatch.GetTimestamp() - startTicks;
            frameCounts[(int)category]++;
        }
    }

    /// <summary>using (PerfProbe.Measure(PerfCategory.PathCalc)) { ... } 형태로 씁니다.</summary>
    public static Scope Measure(PerfCategory category) => new Scope(category);

    static void EnsureFrame()
    {
        int frame = Time.frameCount;

        if (frame == accumulatingFrame)
            return;

        accumulatingFrame = frame;
        Array.Clear(frameTicks, 0, frameTicks.Length);
        Array.Clear(frameCounts, 0, frameCounts.Length);
    }

    /// <summary>현재 프레임에 모인 항목별 시간(ms)과 횟수를 돌려줍니다.</summary>
    public static void GetFrameStats(PerfCategory category, out double milliseconds, out int count)
    {
        if (accumulatingFrame != Time.frameCount)
        {
            milliseconds = 0;
            count = 0;
            return;
        }

        milliseconds = frameTicks[(int)category] * 1000.0 / Stopwatch.Frequency;
        count = frameCounts[(int)category];
    }

    // ── 플레이어 명령 지연 ────────────────────────────────────────

    public class CommandBatch
    {
        public int id;
        public string name;
        public float issuedTime;
        public int issuedFrame;
        public readonly List<NavMeshAgent> agents = new List<NavMeshAgent>();
        public readonly List<float> pathResolvedTimes = new List<float>();
        public readonly List<float> moveStartTimes = new List<float>();
    }

    static readonly List<CommandBatch> activeBatches = new List<CommandBatch>();
    static CommandBatch openBatch;
    static int nextBatchId = 1;

    public static IReadOnlyList<CommandBatch> ActiveBatches => activeBatches;

    /// <summary>한 번의 플레이어 명령을 시작합니다. 이어서 TrackCommandAgent로 유닛을 등록합니다.</summary>
    public static void BeginCommand(string commandName)
    {
        if (!Enabled)
        {
            openBatch = null;
            return;
        }

        openBatch = new CommandBatch
        {
            id = nextBatchId++,
            name = commandName,
            issuedTime = Time.realtimeSinceStartup,
            issuedFrame = Time.frameCount
        };

        activeBatches.Add(openBatch);
    }

    public static void TrackCommandAgent(NavMeshAgent agent)
    {
        if (!Enabled || openBatch == null || agent == null)
            return;

        openBatch.agents.Add(agent);
        openBatch.pathResolvedTimes.Add(-1f);
        openBatch.moveStartTimes.Add(-1f);
    }

    public static void RemoveBatchAt(int index)
    {
        if (activeBatches[index] == openBatch)
            openBatch = null;

        activeBatches.RemoveAt(index);
    }

    public static void ResetForNewPlaySession()
    {
        activeBatches.Clear();
        openBatch = null;
        accumulatingFrame = -1;
    }
}
