using UnityEngine;

public static class UnitCommandDebugLog
{
    public static void Log(MonoBehaviour source, string message)
    {
        if (source == null || !IsEnabled(source))
            return;

        Debug.Log($"[UnitCmd] {source.name}: {message}", source);
    }

    static bool IsEnabled(MonoBehaviour source)
    {
        UnitCombatAI combatAI = source.GetComponent<UnitCombatAI>();
        return combatAI != null && combatAI.debugCommandLog;
    }
}
