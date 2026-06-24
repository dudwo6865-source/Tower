using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-280)]
public class BuildZoneManager : MonoBehaviour
{
    public static BuildZoneManager Instance { get; private set; }

    [Tooltip("HQ가 없을 때 건설을 허용합니다.")]
    public bool allowBuildWithoutHeadquarters;

    private readonly Dictionary<int, Headquarters> headquartersByOwner =
        new Dictionary<int, Headquarters>();

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

    public void Register(Headquarters headquarters)
    {
        if (headquarters == null)
            return;

        headquartersByOwner[headquarters.OwnerId] = headquarters;
    }

    public void Unregister(Headquarters headquarters)
    {
        if (headquarters == null)
            return;

        if (headquartersByOwner.TryGetValue(
                headquarters.OwnerId,
                out Headquarters current) &&
            current == headquarters)
        {
            headquartersByOwner.Remove(headquarters.OwnerId);
        }
    }

    public bool HasHeadquarters(int ownerId)
    {
        return headquartersByOwner.ContainsKey(ownerId);
    }

    public bool CanBuildFootprint(
        Vector2Int originCell,
        Vector2Int footprintCells,
        int ownerId)
    {
        if (!TryGetHeadquarters(ownerId, out Headquarters headquarters))
            return allowBuildWithoutHeadquarters;

        return headquarters.ContainsFootprint(originCell, footprintCells);
    }

    public bool TryGetHeadquarters(int ownerId, out Headquarters headquarters)
    {
        return headquartersByOwner.TryGetValue(ownerId, out headquarters);
    }
}
