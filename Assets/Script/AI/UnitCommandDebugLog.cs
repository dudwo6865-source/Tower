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
        MobileCombatAI combatAI = source.GetComponent<MobileCombatAI>();
        return combatAI != null && combatAI.debugCommandLog;
    }
}
