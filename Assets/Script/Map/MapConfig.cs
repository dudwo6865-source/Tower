using UnityEngine;
using System.Collections.Generic;

// 하나의 맵(레벨)을 정의하는 데이터 에셋입니다.
// 지형·정적 오브젝트·사전 배치물은 mapRootPrefab(프리팹)에 담고,
// 그리드/경제/데이나잇/스포너 웨이브 등 숫자 파라미터는 여기서 관리합니다.
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

    [Tooltip("게임 시작 시 맵에 미리 배치할 적 스포너 수입니다.")]
    public int initialEnemyCount = 0;

    [Tooltip("밤 시작 후 스포너 생성까지 대기(초).")]
    public float nightWaveStartDelay = 3f;

    [Tooltip("밤마다 생성할 스포너 수입니다. 인덱스 0=1번째 밤. 이후 밤을 지정하지 않으면 마지막 값을 계속 사용합니다.")]
    public List<int> spawnersPerNight = new List<int> { 1, 2 };
}
