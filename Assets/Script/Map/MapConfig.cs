using UnityEngine;

// 하나의 맵(레벨)을 정의하는 데이터 에셋입니다.
// 지형·정적 오브젝트·사전 배치물은 mapRootPrefab(프리팹)에 담고,
// 그리드/경제/데이나잇/웨이브 등 숫자 파라미터는 여기서 관리합니다.
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

    [Header("Grid")]
    [Tooltip("한 칸의 월드 크기(m). 로드 시 MapGrid.cellSize에 적용됩니다.")]
    public float cellSize = 2f;

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

    [Header("Wave")]
    [Tooltip("켜면 이 맵의 웨이브 설정으로 WaveManager를 덮어씁니다.")]
    public bool overrideWave = true;

    [Tooltip("게임 시작 시 맵에 미리 흩뿌릴 적 수입니다.")]
    public int initialEnemyCount = 12;

    [Tooltip("초기 배치 적이 아군 건물로 자동 진군할지 여부입니다.")]
    public bool initialEnemiesAdvanceToBase = false;

    [Tooltip("밤 시작 후 첫 웨이브까지 대기(초).")]
    public float nightWaveStartDelay = 3f;

    [Tooltip("웨이브 간격(초). maxWavesPerNight가 1 이상이면 밤 길이 ÷ 웨이브 수로 자동 조정됩니다.")]
    public float waveInterval = 12f;

    [Tooltip("스포너 1개가 웨이브마다 생성할 적 수. 0이면 스포너의 enemiesPerSpawn을 사용합니다.")]
    public int enemiesPerSpawnerPerWave = 0;

    [Tooltip("밤 동안 진행할 최대 웨이브 수. 0이면 밤이 끝날 때까지 계속합니다.")]
    public int maxWavesPerNight = 0;
}
