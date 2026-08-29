using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-280)]
public class BuildZoneManager : MonoBehaviour
{
    public static BuildZoneManager Instance { get; private set; }

    [Tooltip("건설 구역 제공 건물이 없을 때 건설을 허용합니다.")]
    public bool allowBuildWithoutHeadquarters;

    private readonly Dictionary<int, List<BuildZoneProvider>> providersByOwner =
        new Dictionary<int, List<BuildZoneProvider>>();

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

    public void Register(BuildZoneProvider provider)
    {
        if (provider == null)
            return;

        int ownerId = provider.OwnerId;

        if (!providersByOwner.TryGetValue(ownerId, out List<BuildZoneProvider> providers))
        {
            providers = new List<BuildZoneProvider>();
            providersByOwner[ownerId] = providers;
        }

        if (!providers.Contains(provider))
            providers.Add(provider);
    }

    public void Unregister(BuildZoneProvider provider)
    {
        if (provider == null)
            return;

        if (!providersByOwner.TryGetValue(provider.OwnerId, out List<BuildZoneProvider> providers))
            return;

        providers.Remove(provider);

        if (providers.Count == 0)
            providersByOwner.Remove(provider.OwnerId);
    }

    public bool HasHeadquarters(int ownerId)
    {
        return TryGetHeadquarters(ownerId, out _);
    }

    public bool HasBuildZone(int ownerId)
    {
        return providersByOwner.TryGetValue(ownerId, out List<BuildZoneProvider> providers) &&
               providers.Count > 0;
    }

    public bool CanBuildFootprint(
        Vector2Int originCell,
        Vector2Int footprintCells,
        int ownerId)
    {
        if (!providersByOwner.TryGetValue(ownerId, out List<BuildZoneProvider> providers) ||
            providers.Count == 0)
        {
            return allowBuildWithoutHeadquarters;
        }

        for (int i = 0; i < providers.Count; i++)
        {
            BuildZoneProvider provider = providers[i];

            if (provider != null &&
                provider.ContainsFootprint(originCell, footprintCells))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetHeadquarters(int ownerId, out Headquarters headquarters)
    {
        headquarters = null;

        if (!providersByOwner.TryGetValue(ownerId, out List<BuildZoneProvider> providers))
            return false;

        for (int i = 0; i < providers.Count; i++)
        {
            if (providers[i] is Headquarters hq)
            {
                headquarters = hq;
                return true;
            }
        }

        return false;
    }

    public bool TryGetOpposingHeadquarters(int myOwnerId, out Headquarters headquarters)
    {
        foreach (KeyValuePair<int, List<BuildZoneProvider>> pair in providersByOwner)
        {
            if (pair.Key == myOwnerId)
                continue;

            if (TryGetHeadquarters(pair.Key, out headquarters) && headquarters != null)
                return true;
        }

        headquarters = null;
        return false;
    }

    public void GetProviders(int ownerId, List<BuildZoneProvider> results)
    {
        results.Clear();

        if (!providersByOwner.TryGetValue(ownerId, out List<BuildZoneProvider> providers))
            return;

        for (int i = 0; i < providers.Count; i++)
        {
            BuildZoneProvider provider = providers[i];

            if (provider != null)
                results.Add(provider);
        }
    }
}
