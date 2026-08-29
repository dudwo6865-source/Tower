using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct SpatialEntry
{
    public float3 Position;
    public int OwnerId;
    public int TargetOwnerId;
    public int CellKey;
    public byte EntityType;
    public byte Alive;
}

[DefaultExecutionOrder(-400)]
public class SpatialQueryWorld : MonoBehaviour
{
    public const byte EntityTypeUnit = 0;
    public const byte EntityTypeBuilding = 1;
    public const int NoTargetOwner = int.MinValue;

    public static SpatialQueryWorld Instance { get; private set; }

    [Tooltip("공간 해시 격자 크기(m)입니다. 어그로 범위가 30이면 8 전후가 적당합니다.")]
    public float cellSize = 8f;

    const int TableSize = 1024;
    const int TableMask = TableSize - 1;

    NativeArray<SpatialEntry> entries;
    NativeArray<int> heads;
    NativeArray<int> next;
    NativeArray<int> bestIndices;
    NativeArray<float> bestDists;
    NativeArray<int> resultIndex;
    NativeArray<float> resultDist;
    NativeArray<int> collectIndices;
    NativeArray<int> collectCount;

    SelectableEntity[] managedEntities;
    int entryCount;
    bool isDirty = true;

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
        AllocateNative(64);
        SelectableRegistry.OnChanged += NotifyDirty;
    }

    void OnDestroy()
    {
        SelectableRegistry.OnChanged -= NotifyDirty;

        if (Instance == this)
            Instance = null;

        DisposeNative();
    }

    void Update()
    {
        Rebuild();
    }

    public void NotifyDirty()
    {
        isDirty = true;
    }

    public void EnsureBuilt()
    {
        if (isDirty)
            Rebuild();
    }

    public SelectableEntity FindBestEnemyInRange(
        Vector3 fromPosition,
        int myOwnerId,
        float range,
        CombatTargetPriority priority,
        UnitAttacker engageFilter)
    {
        EnsureBuilt();

        if (entryCount <= 0)
            return null;

        var job = new FindBestEnemyJob
        {
            Entries = entries,
            Heads = heads,
            Next = next,
            Origin = fromPosition,
            RangeSqr = range * range,
            MyOwnerId = myOwnerId,
            CellSize = math.max(0.5f, cellSize),
            TableMask = TableMask,
            BestIndices = bestIndices,
            BestDists = bestDists
        };
        job.Run();

        SelectableEntity bestAny = ResolveCandidate(bestIndices[0], engageFilter);
        SelectableEntity bestUnit = ResolveCandidate(bestIndices[1], engageFilter);
        SelectableEntity bestBuilding = ResolveCandidate(bestIndices[2], engageFilter);
        SelectableEntity bestAttackerOfAlly = ResolveCandidate(bestIndices[3], engageFilter);

        switch (priority)
        {
            case CombatTargetPriority.UnitsFirst:
                return bestUnit != null ? bestUnit : bestBuilding;

            case CombatTargetPriority.BuildingsFirst:
                return bestBuilding != null ? bestBuilding : bestUnit;

            case CombatTargetPriority.AttackersOfAlliesFirst:
                if (bestAttackerOfAlly != null)
                    return bestAttackerOfAlly;

                return bestUnit != null ? bestUnit : bestBuilding;

            default:
                return bestAny;
        }
    }

    public SelectableEntity FindNearestEnemyBuilding(
        Vector3 fromPosition,
        int myOwnerId,
        UnitAttacker engageFilter)
    {
        EnsureBuilt();

        if (entryCount <= 0)
            return null;

        var job = new FindNearestBuildingJob
        {
            Entries = entries,
            Origin = fromPosition,
            MyOwnerId = myOwnerId,
            Count = entryCount,
            ResultIndex = resultIndex,
            ResultDist = resultDist
        };
        job.Run();

        return ResolveCandidate(resultIndex[0], engageFilter);
    }

    public bool HasOtherOwnerInRange(
        Vector3 origin,
        int excludeOwnerId,
        float range,
        bool unitsOnly)
    {
        EnsureBuilt();

        if (entryCount <= 0)
            return false;

        var job = new HasOtherOwnerJob
        {
            Entries = entries,
            Heads = heads,
            Next = next,
            Origin = origin,
            RangeSqr = range * range,
            ExcludeOwnerId = excludeOwnerId,
            UnitsOnly = unitsOnly ? (byte)1 : (byte)0,
            CellSize = math.max(0.5f, cellSize),
            TableMask = TableMask,
            Result = resultIndex
        };
        job.Run();

        return resultIndex[0] != 0;
    }

    public void CollectAlliesInRange(
        Vector3 origin,
        int ownerId,
        float range,
        SelectableEntity exclude,
        List<SelectableEntity> results)
    {
        results.Clear();
        EnsureBuilt();

        if (entryCount <= 0)
            return;

        var job = new CollectAlliesJob
        {
            Entries = entries,
            Heads = heads,
            Next = next,
            Origin = origin,
            RangeSqr = range * range,
            OwnerId = ownerId,
            CellSize = math.max(0.5f, cellSize),
            TableMask = TableMask,
            Results = collectIndices,
            ResultCount = collectCount
        };
        job.Run();

        int found = collectCount[0];

        for (int i = 0; i < found; i++)
        {
            SelectableEntity ally = GetEntity(collectIndices[i]);

            if (ally == null || ally == exclude)
                continue;

            results.Add(ally);
        }
    }

    SelectableEntity ResolveCandidate(int snapshotIndex, UnitAttacker engageFilter)
    {
        SelectableEntity entity = GetEntity(snapshotIndex);

        if (entity == null)
            return null;

        if (engageFilter != null && !engageFilter.CanEngage(entity))
            return null;

        return entity;
    }

    SelectableEntity GetEntity(int snapshotIndex)
    {
        if (snapshotIndex < 0 || snapshotIndex >= entryCount)
            return null;

        return managedEntities[snapshotIndex];
    }

    void Rebuild()
    {
        IReadOnlyList<SelectableEntity> source = SelectableRegistry.Entities;
        int sourceCount = source.Count;
        EnsureCapacity(sourceCount);

        int write = 0;

        for (int i = 0; i < sourceCount; i++)
        {
            SelectableEntity entity = source[i];

            if (entity == null)
                continue;

            EntityHealth health = entity.CachedHealth;
            CombatAIBase combatAI = entity.CachedCombatAI;
            SelectableEntity currentTarget = combatAI != null ? combatAI.CurrentTarget : null;

            SpatialEntry entry = new SpatialEntry
            {
                Position = entity.transform.position,
                OwnerId = entity.ownerId,
                TargetOwnerId = currentTarget != null ? currentTarget.ownerId : NoTargetOwner,
                EntityType = entity.entityType == SelectableEntityType.Building
                    ? EntityTypeBuilding
                    : EntityTypeUnit,
                Alive = health == null || health.IsAlive ? (byte)1 : (byte)0
            };

            entries[write] = entry;
            managedEntities[write] = entity;
            write++;
        }

        entryCount = write;

        if (entryCount > 0)
        {
            var buildJob = new BuildSpatialHashJob
            {
                Entries = entries,
                Heads = heads,
                Next = next,
                Count = entryCount,
                TableMask = TableMask,
                CellSize = math.max(0.5f, cellSize)
            };
            buildJob.Run();
        }
        else
        {
            for (int i = 0; i < TableSize; i++)
                heads[i] = -1;
        }

        isDirty = false;
    }

    void EnsureCapacity(int needed)
    {
        int size = entries.IsCreated ? entries.Length : 64;

        if (size >= needed && entries.IsCreated)
            return;

        while (size < needed)
            size *= 2;

        DisposeNative();
        AllocateNative(size);
    }

    void AllocateNative(int size)
    {
        entries = new NativeArray<SpatialEntry>(size, Allocator.Persistent);
        heads = new NativeArray<int>(TableSize, Allocator.Persistent);
        next = new NativeArray<int>(size, Allocator.Persistent);
        bestIndices = new NativeArray<int>(4, Allocator.Persistent);
        bestDists = new NativeArray<float>(4, Allocator.Persistent);
        resultIndex = new NativeArray<int>(1, Allocator.Persistent);
        resultDist = new NativeArray<float>(1, Allocator.Persistent);
        collectIndices = new NativeArray<int>(size, Allocator.Persistent);
        collectCount = new NativeArray<int>(1, Allocator.Persistent);
        managedEntities = new SelectableEntity[size];
    }

    void DisposeNative()
    {
        if (entries.IsCreated)
            entries.Dispose();

        if (heads.IsCreated)
            heads.Dispose();

        if (next.IsCreated)
            next.Dispose();

        if (bestIndices.IsCreated)
            bestIndices.Dispose();

        if (bestDists.IsCreated)
            bestDists.Dispose();

        if (resultIndex.IsCreated)
            resultIndex.Dispose();

        if (resultDist.IsCreated)
            resultDist.Dispose();

        if (collectIndices.IsCreated)
            collectIndices.Dispose();

        if (collectCount.IsCreated)
            collectCount.Dispose();
    }
}

public static class SpatialHash
{
    public static int PackCell(int x, int z)
    {
        return ((x + 32768) << 16) | ((z + 32768) & 0xFFFF);
    }

    public static int CellOf(float3 position, float cellSize)
    {
        int x = (int)math.floor(position.x / cellSize);
        int z = (int)math.floor(position.z / cellSize);
        return PackCell(x, z);
    }
}

[BurstCompile]
public struct BuildSpatialHashJob : IJob
{
    public NativeArray<SpatialEntry> Entries;
    public NativeArray<int> Heads;
    public NativeArray<int> Next;
    public int Count;
    public int TableMask;
    public float CellSize;

    public void Execute()
    {
        for (int i = 0; i < Heads.Length; i++)
            Heads[i] = -1;

        for (int i = 0; i < Count; i++)
        {
            SpatialEntry entry = Entries[i];
            entry.CellKey = SpatialHash.CellOf(entry.Position, CellSize);
            Entries[i] = entry;

            int slot = entry.CellKey & TableMask;
            Next[i] = Heads[slot];
            Heads[slot] = i;
        }
    }
}

[BurstCompile]
public struct FindBestEnemyJob : IJob
{
    [ReadOnly] public NativeArray<SpatialEntry> Entries;
    [ReadOnly] public NativeArray<int> Heads;
    [ReadOnly] public NativeArray<int> Next;
    public float3 Origin;
    public float RangeSqr;
    public int MyOwnerId;
    public float CellSize;
    public int TableMask;
    public NativeArray<int> BestIndices;
    public NativeArray<float> BestDists;

    public void Execute()
    {
        int bestAny = -1;
        int bestUnit = -1;
        int bestBuilding = -1;
        int bestAttacker = -1;
        float minAny = float.MaxValue;
        float minUnit = float.MaxValue;
        float minBuilding = float.MaxValue;
        float minAttacker = float.MaxValue;

        float range = math.sqrt(RangeSqr);
        int minX = (int)math.floor((Origin.x - range) / CellSize);
        int maxX = (int)math.floor((Origin.x + range) / CellSize);
        int minZ = (int)math.floor((Origin.z - range) / CellSize);
        int maxZ = (int)math.floor((Origin.z + range) / CellSize);

        for (int cellX = minX; cellX <= maxX; cellX++)
        {
            for (int cellZ = minZ; cellZ <= maxZ; cellZ++)
            {
                int cellKey = SpatialHash.PackCell(cellX, cellZ);
                int index = Heads[cellKey & TableMask];

                while (index != -1)
                {
                    SpatialEntry entry = Entries[index];
                    int nextIndex = Next[index];

                    if (entry.CellKey == cellKey &&
                        entry.Alive != 0 &&
                        entry.OwnerId != MyOwnerId)
                    {
                        float3 delta = entry.Position - Origin;
                        delta.y = 0f;
                        float sqrDistance = math.lengthsq(delta);

                        if (sqrDistance <= RangeSqr)
                        {
                            if (sqrDistance < minAny)
                            {
                                minAny = sqrDistance;
                                bestAny = index;
                            }

                            if (entry.EntityType == SpatialQueryWorld.EntityTypeUnit &&
                                sqrDistance < minUnit)
                            {
                                minUnit = sqrDistance;
                                bestUnit = index;
                            }

                            if (entry.EntityType == SpatialQueryWorld.EntityTypeBuilding &&
                                sqrDistance < minBuilding)
                            {
                                minBuilding = sqrDistance;
                                bestBuilding = index;
                            }

                            if (entry.TargetOwnerId == MyOwnerId &&
                                sqrDistance < minAttacker)
                            {
                                minAttacker = sqrDistance;
                                bestAttacker = index;
                            }
                        }
                    }

                    index = nextIndex;
                }
            }
        }

        BestIndices[0] = bestAny;
        BestIndices[1] = bestUnit;
        BestIndices[2] = bestBuilding;
        BestIndices[3] = bestAttacker;
        BestDists[0] = minAny;
        BestDists[1] = minUnit;
        BestDists[2] = minBuilding;
        BestDists[3] = minAttacker;
    }
}

[BurstCompile]
public struct FindNearestBuildingJob : IJob
{
    [ReadOnly] public NativeArray<SpatialEntry> Entries;
    public float3 Origin;
    public int MyOwnerId;
    public int Count;
    public NativeArray<int> ResultIndex;
    public NativeArray<float> ResultDist;

    public void Execute()
    {
        int best = -1;
        float minSqrDistance = float.MaxValue;

        for (int i = 0; i < Count; i++)
        {
            SpatialEntry entry = Entries[i];

            if (entry.Alive == 0 ||
                entry.EntityType != SpatialQueryWorld.EntityTypeBuilding ||
                entry.OwnerId == MyOwnerId)
            {
                continue;
            }

            float sqrDistance = math.lengthsq(entry.Position - Origin);

            if (sqrDistance >= minSqrDistance)
                continue;

            minSqrDistance = sqrDistance;
            best = i;
        }

        ResultIndex[0] = best;
        ResultDist[0] = minSqrDistance;
    }
}

[BurstCompile]
public struct HasOtherOwnerJob : IJob
{
    [ReadOnly] public NativeArray<SpatialEntry> Entries;
    [ReadOnly] public NativeArray<int> Heads;
    [ReadOnly] public NativeArray<int> Next;
    public float3 Origin;
    public float RangeSqr;
    public int ExcludeOwnerId;
    public byte UnitsOnly;
    public float CellSize;
    public int TableMask;
    public NativeArray<int> Result;

    public void Execute()
    {
        Result[0] = 0;

        float range = math.sqrt(RangeSqr);
        int minX = (int)math.floor((Origin.x - range) / CellSize);
        int maxX = (int)math.floor((Origin.x + range) / CellSize);
        int minZ = (int)math.floor((Origin.z - range) / CellSize);
        int maxZ = (int)math.floor((Origin.z + range) / CellSize);

        for (int cellX = minX; cellX <= maxX; cellX++)
        {
            for (int cellZ = minZ; cellZ <= maxZ; cellZ++)
            {
                int cellKey = SpatialHash.PackCell(cellX, cellZ);
                int index = Heads[cellKey & TableMask];

                while (index != -1)
                {
                    SpatialEntry entry = Entries[index];
                    int nextIndex = Next[index];

                    if (entry.CellKey == cellKey &&
                        entry.Alive != 0 &&
                        entry.OwnerId != ExcludeOwnerId &&
                        (UnitsOnly == 0 || entry.EntityType == SpatialQueryWorld.EntityTypeUnit))
                    {
                        float3 delta = entry.Position - Origin;
                        delta.y = 0f;

                        if (math.lengthsq(delta) <= RangeSqr)
                        {
                            Result[0] = 1;
                            return;
                        }
                    }

                    index = nextIndex;
                }
            }
        }
    }
}

[BurstCompile]
public struct CollectAlliesJob : IJob
{
    [ReadOnly] public NativeArray<SpatialEntry> Entries;
    [ReadOnly] public NativeArray<int> Heads;
    [ReadOnly] public NativeArray<int> Next;
    public float3 Origin;
    public float RangeSqr;
    public int OwnerId;
    public float CellSize;
    public int TableMask;
    public NativeArray<int> Results;
    public NativeArray<int> ResultCount;

    public void Execute()
    {
        int found = 0;
        float range = math.sqrt(RangeSqr);
        int minX = (int)math.floor((Origin.x - range) / CellSize);
        int maxX = (int)math.floor((Origin.x + range) / CellSize);
        int minZ = (int)math.floor((Origin.z - range) / CellSize);
        int maxZ = (int)math.floor((Origin.z + range) / CellSize);

        for (int cellX = minX; cellX <= maxX; cellX++)
        {
            for (int cellZ = minZ; cellZ <= maxZ; cellZ++)
            {
                int cellKey = SpatialHash.PackCell(cellX, cellZ);
                int index = Heads[cellKey & TableMask];

                while (index != -1)
                {
                    SpatialEntry entry = Entries[index];
                    int nextIndex = Next[index];

                    if (entry.CellKey == cellKey &&
                        entry.Alive != 0 &&
                        entry.EntityType == SpatialQueryWorld.EntityTypeUnit &&
                        entry.OwnerId == OwnerId)
                    {
                        float3 delta = entry.Position - Origin;

                        if (math.lengthsq(delta) <= RangeSqr && found < Results.Length)
                        {
                            Results[found] = index;
                            found++;
                        }
                    }

                    index = nextIndex;
                }
            }
        }

        ResultCount[0] = found;
    }
}
