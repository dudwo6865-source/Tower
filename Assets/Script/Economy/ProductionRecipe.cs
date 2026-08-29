using UnityEngine;

[CreateAssetMenu(
    fileName = "ProductionRecipe",
    menuName = "Tank/Production Recipe")]
public class ProductionRecipe : ScriptableObject
{
    [Header("Unit")]
    [Tooltip("생산할 유닛 프리팹입니다.")]
    public GameObject unitPrefab;

    [Header("Timing")]
    [Tooltip("건물 완공 후 첫 생산까지 대기 시간(초)입니다.")]
    public float initialSpawnDelay = 2f;

    [Tooltip("유닛 한 마리를 생산하는 간격(초)입니다. 낮/밤과 관계없이 이 속도로 생산합니다.")]
    public float spawnInterval = 8f;

    [Header("Limits")]
    [Tooltip("이 건물에서 동시에 살아 있을 수 있는 유닛 수입니다.")]
    public int maxAlivePerBuilding = 5;
}
