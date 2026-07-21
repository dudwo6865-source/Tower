using System;
using System.Collections.Generic;

public static class BuildingRegistry
{
    private static readonly List<SelectableEntity> buildings =
        new List<SelectableEntity>();

    public static event Action<SelectableEntity> OnBuildingRegistered;
    public static event Action<SelectableEntity> OnBuildingRemoved;

    public static IReadOnlyList<SelectableEntity> Buildings => buildings;

    public static void Register(SelectableEntity building)
    {
        if (building == null ||
            building.entityType != SelectableEntityType.Building ||
            buildings.Contains(building))
            return;

        buildings.Add(building);
        OnBuildingRegistered?.Invoke(building);
    }

    public static void Unregister(SelectableEntity building)
    {
        if (building == null)
            return;

        buildings.Remove(building);
    }

    public static void NotifyRemoved(SelectableEntity building)
    {
        if (building == null || !buildings.Remove(building))
            return;

        OnBuildingRemoved?.Invoke(building);
    }
}
