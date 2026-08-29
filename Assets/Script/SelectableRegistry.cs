using System;
using System.Collections.Generic;

public static class SelectableRegistry
{
    private static readonly List<SelectableEntity> entities =
        new List<SelectableEntity>();

    public static event Action OnChanged;

    public static IReadOnlyList<SelectableEntity> Entities => entities;

    public static void Register(SelectableEntity entity)
    {
        if (entity == null || entities.Contains(entity))
            return;

        entities.Add(entity);
        OnChanged?.Invoke();
    }

    public static void Unregister(SelectableEntity entity)
    {
        if (entity == null)
            return;

        if (entities.Remove(entity))
            OnChanged?.Invoke();
    }
}
