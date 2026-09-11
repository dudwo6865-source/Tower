using UnityEngine;
using System.Collections.Generic;

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

    [Tooltip("밤마다 생성할 적 건물(스포너) 프리팹 목록입니다. 비워두면 WaveManager에 이미 설정된 프리팹을 그대로 사용합니다.")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();

    [Tooltip("초기 배치에 사용할 적 건물 프리팹 목록입니다. 비워두면 위 Enemy Prefabs를 사용합니다.")]
    public List<GameObject> initialEnemyPrefabs = new List<GameObject>();

    [Tooltip("게임 시작 시 맵에 미리 배치할 적 스포너 수입니다.")]
    public int initialEnemyCount = 0;

    [Tooltip("본부와 최소 이 거리 이상 떨어진 곳에만 초기 배치합니다.")]
    public float initialMinDistanceFromHq = 25f;

    [Tooltip("맵 가장자리에서 안쪽으로 둘 여백입니다.")]
    public float mapEdgeMargin = 8f;

    [Tooltip("랜덤 위치 샘플 최대 시도 횟수입니다. 맵이 크거나 배치가 자주 실패하면 늘리세요.")]
    public int randomPositionAttempts = 32;

    [Tooltip("밤 시작 후 스포너 생성까지 대기(초).")]
    public float nightWaveStartDelay = 3f;

    [Tooltip("밤마다 생성할 스포너 수입니다. 인덱스 0=1번째 밤. 이후 밤을 지정하지 않으면 마지막 값을 계속 사용합니다.")]
    public List<int> spawnersPerNight = new List<int> { 1, 2 };

    [Tooltip("밤 스포너를 본부와 최소 이 거리 이상 떨어진 곳에 배치합니다.")]
    public float nightWaveMinDistanceFromHq = 20f;

    [Tooltip("켜면 밤 스포너를 플레이어 시야 밖(안개 속)에 우선 배치합니다.")]
    public bool nightWaveAvoidPlayerVision = true;

    [Header("Win Condition")]
    [Tooltip("켜면 이 스테이지의 승리 조건 설정으로 GameResultManager를 덮어씁니다.")]
    public bool overrideWinCondition = true;

    [Tooltip("본부가 파괴되지 않고 이 밤까지 버티면 승리합니다. (예: 5 = 5번째 밤이 끝나면 승리)")]
    public int survivalNightsToWin = 5;
}
