using UnityEngine;

public class UnitEnemyAI : UnitCombatAI
{
    void Reset()
    {
        advanceToEnemyBuildings = true;
        targetPriority = CombatTargetPriority.UnitsFirst;
    }
}
