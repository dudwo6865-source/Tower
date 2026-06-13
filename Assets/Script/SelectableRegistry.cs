using System.Collections.Generic;

public static class SelectableRegistry
{
    private static readonly List<SelectableEntity> entities =
        new List<SelectableEntity>();

    public static IReadOnlyList<SelectableEntity> Entities => entities;

    public static void Register(SelectableEntity entity)
    {
        if (entity == null || entities.Contains(entity))
            return;

        entities.Add(entity);
    }

    public static void Unregister(SelectableEntity entity)
    {
        if (entity == null)
            return;

        entities.Remove(entity);
    }
}
