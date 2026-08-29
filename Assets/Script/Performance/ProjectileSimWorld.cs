using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct ProjectileSimData
{
    public float3 Position;
    public float3 TargetPosition;
    public float3 LastMoveDirection;
    public float Speed;
    public float LifeTimer;
    public float MaxLifeTime;
    public float ImpactDistanceSq;
    public byte Active;
    public byte Impacted;
    public byte Expired;
}

[DefaultExecutionOrder(50)]
public class ProjectileSimWorld : MonoBehaviour
{
    public static ProjectileSimWorld Instance { get; private set; }

    const float DefaultImpactDistanceSq = 0.09f;
    const int ParallelThreshold = 32;

    NativeArray<ProjectileSimData> data;
    Projectile[] instances;
    readonly Stack<int> freeSlots = new Stack<int>(64);
    readonly Dictionary<int, Stack<Projectile>> prefabPools = new Dictionary<int, Stack<Projectile>>();
    readonly Stack<Projectile> fallbackPool = new Stack<Projectile>(16);
    Transform poolRoot;
    int slotCount;

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
        AllocateNative(32);

        poolRoot = new GameObject("PooledProjectiles").transform;
        poolRoot.SetParent(transform, false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        DisposeNative();
    }

    void Update()
    {
        Simulate(Time.deltaTime);
    }

    public static Projectile Spawn(
        Vector3 firePosition,
        Quaternion rotation,
        SelectableEntity target,
        EntityHealth targetHealth,
        float damage,
        float speed,
        GameObject prefab,
        GameObject hitEffectPrefab,
        Color fallbackProjectileColor,
        Color fallbackHitColor,
        SelectableEntity attacker)
    {
        ProjectileSimWorld world = Instance;

        if (world == null)
            return null;

        Projectile projectile = world.Acquire(prefab, firePosition, rotation, fallbackProjectileColor);

        if (projectile == null)
            return null;

        projectile.Initialize(
            target,
            targetHealth,
            damage,
            speed,
            hitEffectPrefab,
            fallbackHitColor,
            attacker);

        world.Register(projectile);
        return projectile;
    }

    public void Release(Projectile projectile)
    {
        if (projectile == null)
            return;

        Unregister(projectile);

        if (!projectile.gameObject.activeSelf)
            return;

        projectile.PrepareForPool();
        projectile.gameObject.SetActive(false);
        projectile.transform.SetParent(poolRoot, false);
        GetPool(projectile).Push(projectile);
    }

    Projectile Acquire(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Color fallbackColor)
    {
        Projectile projectile = prefab != null
            ? PopOrCreatePrefab(prefab)
            : PopOrCreateFallback(fallbackColor);

        if (projectile == null)
            return null;

        projectile.transform.SetPositionAndRotation(position, rotation);
        projectile.gameObject.SetActive(true);
        projectile.RestartVisuals(fallbackColor);
        return projectile;
    }

    Projectile PopOrCreatePrefab(GameObject prefab)
    {
        Stack<Projectile> pool = GetPrefabPool(prefab.GetInstanceID());

        if (pool.Count > 0)
            return pool.Pop();

        GameObject instance = Instantiate(prefab, poolRoot);
        instance.name = prefab.name;
        Projectile projectile = instance.GetComponent<Projectile>();

        if (projectile == null)
            projectile = instance.AddComponent<Projectile>();

        projectile.SetPoolKey(prefab.GetInstanceID());
        return projectile;
    }

    Projectile PopOrCreateFallback(Color color)
    {
        if (fallbackPool.Count > 0)
            return fallbackPool.Pop();

        GameObject instance = AttackVisuals.CreateFallbackProjectile(Vector3.zero, color);
        instance.transform.SetParent(poolRoot, false);
        Projectile projectile = instance.GetComponent<Projectile>();

        if (projectile == null)
            projectile = instance.AddComponent<Projectile>();

        projectile.SetPoolKey(0);
        return projectile;
    }

    Stack<Projectile> GetPool(Projectile projectile)
    {
        if (projectile.PoolKey == 0)
            return fallbackPool;

        return GetPrefabPool(projectile.PoolKey);
    }

    Stack<Projectile> GetPrefabPool(int key)
    {
        if (!prefabPools.TryGetValue(key, out Stack<Projectile> pool))
        {
            pool = new Stack<Projectile>(8);
            prefabPools.Add(key, pool);
        }

        return pool;
    }

    void Register(Projectile projectile)
    {
        int slot;

        if (freeSlots.Count > 0)
        {
            slot = freeSlots.Pop();
        }
        else
        {
            slot = slotCount;
            slotCount++;
            EnsureCapacity(slotCount);
        }

        instances[slot] = projectile;
        data[slot] = projectile.CreateSimData(DefaultImpactDistanceSq);
        projectile.AssignSlot(slot);
    }

    void Unregister(Projectile projectile)
    {
        int slot = projectile.Slot;

        if (slot < 0 || slot >= slotCount || instances[slot] != projectile)
        {
            projectile.AssignSlot(-1);
            return;
        }

        instances[slot] = null;
        data[slot] = default;
        projectile.AssignSlot(-1);
        freeSlots.Push(slot);
    }

    void Simulate(float deltaTime)
    {
        if (slotCount <= 0 || freeSlots.Count == slotCount)
            return;

        for (int i = 0; i < slotCount; i++)
        {
            Projectile projectile = instances[i];

            if (projectile == null)
                continue;

            ProjectileSimData sim = data[i];
            sim.TargetPosition = projectile.GetHomingPoint();
            data[i] = sim;
        }

        var job = new ProjectileMoveJob
        {
            Data = data,
            DeltaTime = deltaTime
        };

        if (slotCount >= ParallelThreshold)
            job.Schedule(slotCount, 32).Complete();
        else
            job.Run(slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            Projectile projectile = instances[i];

            if (projectile == null)
                continue;

            ProjectileSimData sim = data[i];
            projectile.ApplySimState(sim);

            if (sim.Impacted != 0)
            {
                projectile.Impact();
                continue;
            }

            if (sim.Expired != 0)
                Release(projectile);
        }
    }

    void EnsureCapacity(int needed)
    {
        int size = data.IsCreated ? data.Length : 32;

        if (size >= needed)
            return;

        while (size < needed)
            size *= 2;

        NativeArray<ProjectileSimData> grown =
            new NativeArray<ProjectileSimData>(size, Allocator.Persistent);

        if (data.IsCreated)
        {
            NativeArray<ProjectileSimData>.Copy(data, grown, data.Length);
            data.Dispose();
        }

        data = grown;

        Projectile[] grownInstances = new Projectile[size];

        if (instances != null)
            System.Array.Copy(instances, grownInstances, instances.Length);

        instances = grownInstances;
    }

    void AllocateNative(int size)
    {
        data = new NativeArray<ProjectileSimData>(size, Allocator.Persistent);
        instances = new Projectile[size];
        slotCount = 0;
        freeSlots.Clear();
    }

    void DisposeNative()
    {
        if (data.IsCreated)
            data.Dispose();
    }
}

[BurstCompile]
public struct ProjectileMoveJob : IJobParallelFor
{
    public NativeArray<ProjectileSimData> Data;
    public float DeltaTime;

    public void Execute(int index)
    {
        ProjectileSimData projectile = Data[index];

        if (projectile.Active == 0)
            return;

        projectile.LifeTimer += DeltaTime;

        if (projectile.LifeTimer >= projectile.MaxLifeTime)
        {
            projectile.Expired = 1;
            Data[index] = projectile;
            return;
        }

        float3 toTarget = projectile.TargetPosition - projectile.Position;
        float distanceSq = math.lengthsq(toTarget);

        if (distanceSq > 0.0001f)
            projectile.LastMoveDirection = math.normalize(toTarget);

        float step = projectile.Speed * DeltaTime;
        float distance = math.sqrt(math.max(distanceSq, 0f));

        if (distance <= step)
            projectile.Position = projectile.TargetPosition;
        else
            projectile.Position += projectile.LastMoveDirection * step;

        if (math.distancesq(projectile.Position, projectile.TargetPosition) <= projectile.ImpactDistanceSq)
            projectile.Impacted = 1;

        Data[index] = projectile;
    }
}
