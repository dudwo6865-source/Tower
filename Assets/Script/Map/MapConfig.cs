using UnityEngine;

// 하나의 맵(레벨)을 정의하는 데이터 에셋입니다.
// 지형·정적 오브젝트·사전 배치물은 mapRootPrefab(프리팹)에 담고,
// 경제/데이나잇/스포너 웨이브 등 숫자 파라미터는 여기서 관리합니다.
// MapLoader가 이 에셋을 받아 맵을 인스턴스화하고 설정을 주입합니다.
[CreateAssetMenu(menuName = "Tank/Map Config", fileName = "MapConfig")]
public class MapConfig : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("맵 선택 UI 등에 표시할 이름입니다.")]
    public string displayName = "New Map";

    [TextArea]
    [Tooltip("맵 설명(선택).")]
    public string description;

    [Tooltip("맵 선택 UI용 미리보기 이미지(선택).")]
    public Sprite previewImage;

    [Header("Map Content")]
    [Tooltip("MapRoot 컴포넌트가 붙은 맵 루트 프리팹입니다. (지형 + 정적 오브젝트 + NavMeshSurface)")]
    public GameObject mapRootPrefab;

    [Header("Economy (Watt)")]
    [Tooltip("켜면 이 맵의 Watt 설정으로 WattManager를 덮어씁니다.")]
    public bool overrideEconomy = true;
    public float maxWatt = 100f;
    public float startingWatt = 50f;
    public float incomePerSecond = 5f;

    [Header("Day / Night")]
    [Tooltip("켜면 이 맵의 낮/밤 설정으로 DayNightCycle을 덮어씁니다.")]
    public bool overrideDayNight = true;
    public DayNightPhase startPhase = DayNightPhase.Day;
    public float dayDuration = 120f;
    public float nightDuration = 60f;

    [Tooltip("낮 시간대 라이트 색상입니다.")]
    public Color dayLightColor = new Color(1f, 0.95686275f, 0.8392157f, 1f);

    [Tooltip("밤 시간대 라이트 색상입니다.")]
    public Color nightLightColor = new Color(0.35f, 0.45f, 0.75f, 1f);

    [Tooltip("낮 시간대 라이트 강도입니다.")]
    public float dayLightIntensity = 1f;

    [Tooltip("밤 시간대 라이트 강도입니다.")]
    public float nightLightIntensity = 0.35f;

    [Tooltip("낮/밤 전환 시 라이트가 먼저 변하는 시간(초)입니다.")]
    public float lightTransitionDuration = 5f;

    [Header("Wave")]
    [Tooltip("켜면 이 맵의 웨이브 설정으로 WaveManager를 덮어씁니다.")]
    public bool overrideWave = true;

    [Tooltip("웨이브별로 맵의 EnemySpawner들에 적용할 수치입니다. " +
             "스포너는 맵 프리팹에 미리 배치해 두고, 여기서는 세기만 조절합니다.")]
    public WavePlan wavePlan = new WavePlan();

    [Tooltip("켜면 밤 동안에만 아래 보정을 웨이브 수치에 한 번 더 곱합니다.")]
    public bool applyNightBonus = true;

    [Tooltip("밤 동안 추가로 곱할 보정입니다. 전부 1이면 낮과 같습니다.")]
    public WaveTuning nightBonus = new WaveTuning();

    [Header("Win Condition")]
    [Tooltip("켜면 이 스테이지의 승리 조건 설정으로 GameResultManager를 덮어씁니다.")]
    public bool overrideWinCondition = true;

    [Tooltip("본부가 파괴되지 않고 이 밤까지 버티면 승리합니다. (예: 5 = 5번째 밤이 끝나면 승리)")]
    public int survivalNightsToWin = 5;
}
