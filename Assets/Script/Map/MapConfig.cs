using System.Collections.Generic;
using UnityEngine;

// 하나의 맵(레벨)을 정의하는 데이터 에셋입니다.
// 지형·정적 오브젝트·사전 배치물은 mapRootPrefab(프리팹)에 담고,
// 경제/데이나잇/스포너 웨이브 등 숫자 파라미터는 여기서 관리합니다.
// MapLoader가 이 에셋을 받아 맵을 인스턴스화하고 설정을 주입합니다.
[CreateAssetMenu(menuName = "Tank/스테이지 (Map Config)", fileName = "MapConfig")]
public class MapConfig : ScriptableObject
{
    [Header("기본 정보")]
    [Label("표시 이름")]
    [Tooltip("맵 선택 UI 등에 표시할 이름입니다.")]
    public string displayName = "New Map";

    [Label("설명")]
    [TextArea]
    [Tooltip("맵 설명(선택).")]
    public string description;

    [Label("미리보기 이미지")]
    [Tooltip("맵 선택 UI용 미리보기 이미지(선택).")]
    public Sprite previewImage;

    [Header("맵 구성")]
    [Label("맵 루트 프리팹")]
    [Tooltip("MapRoot 컴포넌트가 붙은 맵 루트 프리팹입니다. (지형 + 정적 오브젝트 + NavMeshSurface)")]
    public GameObject mapRootPrefab;

    [Header("경제 (Watt)")]
    [Label("경제 덮어쓰기")]
    [Tooltip("켜면 이 맵의 Watt 설정으로 WattManager를 덮어씁니다.")]
    public bool overrideEconomy = true;
    [Label("최대 Watt")]
    public float maxWatt = 100f;
    [Label("시작 Watt")]
    public float startingWatt = 50f;
    [Label("초당 수입")]
    public float incomePerSecond = 5f;

    [Header("낮 / 밤")]
    [Label("낮/밤 덮어쓰기")]
    [Tooltip("켜면 이 맵의 낮/밤 설정으로 DayNightCycle을 덮어씁니다.")]
    public bool overrideDayNight = true;
    [Label("시작 페이즈")]
    public DayNightPhase startPhase = DayNightPhase.Day;
    [Label("낮 길이(초)")]
    public float dayDuration = 120f;
    [Label("밤 길이(초)")]
    public float nightDuration = 60f;

    [Label("낮 조명 색")]
    [Tooltip("낮 시간대 라이트 색상입니다.")]
    public Color dayLightColor = new Color(1f, 0.95686275f, 0.8392157f, 1f);

    [Label("밤 조명 색")]
    [Tooltip("밤 시간대 라이트 색상입니다.")]
    public Color nightLightColor = new Color(0.35f, 0.45f, 0.75f, 1f);

    [Label("낮 조명 강도")]
    [Tooltip("낮 시간대 라이트 강도입니다.")]
    public float dayLightIntensity = 1f;

    [Label("밤 조명 강도")]
    [Tooltip("밤 시간대 라이트 강도입니다.")]
    public float nightLightIntensity = 0.35f;

    [Label("조명 전환 시간(초)")]
    [Tooltip("낮/밤 전환 시 라이트가 먼저 변하는 시간(초)입니다.")]
    public float lightTransitionDuration = 5f;

    [Header("웨이브")]
    [Label("웨이브 덮어쓰기")]
    [Tooltip("켜면 이 맵의 웨이브 설정으로 WaveManager를 덮어씁니다.")]
    public bool overrideWave = true;

    [Label("웨이브 계획")]
    [Tooltip("웨이브별로 맵의 EnemySpawner들에 적용할 수치입니다. " +
             "스포너는 맵 프리팹에 미리 배치해 두고, 여기서는 세기만 조절합니다.")]
    public WavePlan wavePlan = new WavePlan();

    [Label("밤 보정 적용")]
    [Tooltip("켜면 밤 동안에만 아래 보정을 웨이브 수치에 한 번 더 곱합니다.")]
    public bool applyNightBonus = true;

    [Label("밤 보정")]
    [Tooltip("밤 동안 추가로 곱할 보정입니다. 전부 1이면 낮과 같습니다.")]
    public WaveTuning nightBonus = new WaveTuning();

    [Header("초기 적")]
    [Label("초기 적 덮어쓰기")]
    [Tooltip("켜면 이 스테이지 값으로 InitialEnemyPlacer를 덮어씁니다.")]
    public bool overrideInitialEnemies = true;

    [Label("초기 적 무리 목록")]
    [Tooltip("게임 시작 시 맵에 미리 배치할 적 무리 목록입니다. " +
             "스포너가 계속 뿜는 적과 달리 한 번만 배치되고 다시 채워지지 않습니다.")]
    public List<InitialEnemyGroup> initialEnemies = new List<InitialEnemyGroup>();

    [Label("초기 적 본부 최소 거리")]
    [Tooltip("초기 적을 본부와 최소 이 거리 이상 떨어진 곳에 배치합니다.")]
    public float initialEnemyMinDistanceFromHq = 25f;

    [Label("초기 적 시야 피하기")]
    [Tooltip("켜면 초기 적을 플레이어 시야 밖(안개 속)에 우선 배치합니다.")]
    public bool initialEnemyAvoidPlayerVision = true;

    [Label("초기 적 본부로 진군")]
    [Tooltip("켜면 초기 적이 배치 직후부터 플레이어 본부로 진군합니다. " +
             "끄면 배치된 자리를 지키며 어그로 범위에 들어온 상대만 공격합니다.")]
    public bool initialEnemyAdvanceToPlayerBase = false;

    [Header("승리 조건")]
    [Label("승리 조건 덮어쓰기")]
    [Tooltip("켜면 이 스테이지의 승리 조건 설정으로 GameResultManager를 덮어씁니다.")]
    public bool overrideWinCondition = true;

    [Label("승리까지 버틸 밤 수")]
    [Tooltip("본부가 파괴되지 않고 이 밤까지 버티면 승리합니다. (예: 5 = 5번째 밤이 끝나면 승리)")]
    public int survivalNightsToWin = 5;
}
