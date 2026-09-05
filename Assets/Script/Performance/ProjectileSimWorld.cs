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

    // 화염방사기(관통) 투사체용입니다. 명중해도 사라지지 않고 사거리 끝까지 직진합니다.
    // 실제 피해는 Projectile의 트리거 콜라이더(OnTriggerEnter)가 처리하며,
    // 여기서는 이동 방식(추적 → 직진 전환, 사거리 소진)만 관리합니다.
    public byte Piercing;
    public byte Locked;
    public float TraveledDistance;
    public float MaxTravelDistance;
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
        SelectableEntity attacker,
        bool piercing = false,
        float maxTravelDistance = 0f,
        float pierceHitRadius = 0.5f,
        bool arcing = false,
        float arcHeight = 0f,
        float arcHeightRatio = 0f,
        float minArcHeight = 0f,
        float splashRadius = 0f,
        float splashMinDamageRatio = 1f,
        float hitEffectScale = 1f)
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
            attacker,
            piercing,
            maxTravelDistance,
            pierceHitRadius,
            arcing,
            arcHeight,
            arcHeightRatio,
            minArcHeight,
            splashRadius,
            splashMinDamageRatio,
            hitEffectScale);

        // 대포(포물선) 투사체는 자체 Update()에서 직접 움직이므로 Burst 이동 Job에는 등록하지 않습니다.
        if (!arcing)
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

            // 관통(화염방사기) 투사체는 논타겟입니다. 발사 즉시 Locked 상태로 시작해 대상을 쫓지 않습니다.
            if (sim.Piercing == 0 || sim.Locked == 0)
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

        float step = projectile.Speed * DeltaTime;

        // 화염방사기 관통 투사체: 대상 근처에 도달했다면 마지막 방향으로 직진만 하다가
        // 사거리(MaxTravelDistance)에 도달하면 소멸합니다. 대상을 다시 쫓지 않습니다.
        // (실제 피해는 Projectile의 트리거 콜라이더가 처리합니다.)
        if (projectile.Piercing != 0 && projectile.Locked != 0)
        {
            projectile.Position += projectile.LastMoveDirection * step;
            projectile.TraveledDistance += step;

            if (projectile.TraveledDistance >= projectile.MaxTravelDistance)
                projectile.Expired = 1;

            Data[index] = projectile;
            return;
        }

        float3 toTarget = projectile.TargetPosition - projectile.Position;
        float distanceSq = math.lengthsq(toTarget);

        if (distanceSq > 0.0001f)
            projectile.LastMoveDirection = math.normalize(toTarget);

        float distance = math.sqrt(math.max(distanceSq, 0f));

        if (distance <= step)
        {
            projectile.TraveledDistance += distance;
            projectile.Position = projectile.TargetPosition;
        }
        else
        {
            projectile.Position += projectile.LastMoveDirection * step;
            projectile.TraveledDistance += step;
        }

        if (math.distancesq(projectile.Position, projectile.TargetPosition) <= projectile.ImpactDistanceSq)
        {
            if (projectile.Piercing != 0)
            {
                projectile.Locked = 1;

                if (projectile.TraveledDistance >= projectile.MaxTravelDistance)
                    projectile.Expired = 1;
            }
            else
            {
                projectile.Impacted = 1;
            }
        }

        Data[index] = projectile;
    }
}
