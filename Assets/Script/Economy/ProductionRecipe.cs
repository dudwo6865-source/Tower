using UnityEngine;

[CreateAssetMenu(
    fileName = "ProductionRecipe",
    menuName = "Tank/Production Recipe")]
public class ProductionRecipe : ScriptableObject
{
    [Header("Unit")]
    [Tooltip("생산할 유닛 프리팹입니다.")]
    public GameObject unitPrefab;

    [Header("Limits")]
    [Tooltip("이 건물에서 동시에 살아 있을 수 있는 유닛 수입니다. 매 낮 시작 시 이 수만큼 즉시 생산합니다.")]
    public int maxAlivePerBuilding = 5;
}
