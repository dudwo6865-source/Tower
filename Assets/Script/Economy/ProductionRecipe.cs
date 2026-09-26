using UnityEngine;

[CreateAssetMenu(
    fileName = "ProductionRecipe",
    menuName = "Tank/Production Recipe")]
public class ProductionRecipe : ScriptableObject
{
    [Header("유닛")]
    [Label("유닛 프리팹")]
    [Tooltip("생산할 유닛 프리팹입니다.")]
    public GameObject unitPrefab;

    [Header("타이밍")]
    [Label("첫 생산 대기(초)")]
    [Tooltip("건물 완공 후 첫 생산까지 대기 시간(초)입니다.")]
    public float initialSpawnDelay = 2f;

    [Label("생산 간격(초)")]
    [Tooltip("유닛 한 마리를 생산하는 간격(초)입니다. 낮/밤과 관계없이 이 속도로 생산합니다.")]
    public float spawnInterval = 8f;

    [Header("비용")]
    [Label("초당 Watt 소모량")]
    [Tooltip("생산이 진행되는 동안 초당 소모하는 Watt입니다. Watt가 부족하면 생산 진행이 멈춥니다. 0이면 소모하지 않습니다.")]
    [Min(0f)]
    public float wattCostPerSecond = 0f;

    [Header("제한")]
    [Label("건물당 최대 유닛 수")]
    [Tooltip("이 건물에서 동시에 살아 있을 수 있는 유닛 수입니다.")]
    public int maxAlivePerBuilding = 5;
}
