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
        // 이동 유닛뿐 아니라 타워(TowerAI)도 같은 플래그로 로그를 켭니다.
        CombatAIBase combatAI = source as CombatAIBase;

        if (combatAI == null)
            combatAI = source.GetComponent<CombatAIBase>();

        return combatAI != null && combatAI.debugCommandLog;
    }
}
