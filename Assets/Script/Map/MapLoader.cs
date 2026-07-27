using UnityEngine;

// MapConfig를 받아 맵을 로드하는 오케스트레이터입니다.
// 가장 이른 실행 순서(-1000)로 두어, 씬의 매니저(MapGrid/WattManager/
// DayNightCycle/WaveManager)들이 자신의 Awake/Start를 실행하기 "전에"
//   1) 설정값을 각 매니저에 주입하고
//   2) 맵 프리팹을 인스턴스화 + NavMesh를 굽습니다.
// 이렇게 하면 매니저들은 주입된 값과 인스턴스화된 스포너를 그대로 읽어
// 별도 수정 없이 정상 동작합니다.
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class MapLoader : MonoBehaviour
{
    public static MapLoader Instance { get; private set; }

    // 맵 선택 화면에서 씬을 로드하기 전에 설정합니다. 있으면 defaultConfig보다 우선합니다.
    public static MapConfig PendingConfig;

    [Header("Config")]
    [Tooltip("PendingConfig가 없을 때 로드할 기본 맵입니다. (에디터 단독 테스트용)")]
    public MapConfig defaultConfig;

    [Tooltip("맵 인스턴스를 담을 부모(선택). 비워두면 씬 루트에 생성합니다.")]
    public Transform mapParent;

    [Tooltip("Awake에서 자동으로 맵을 로드합니다.")]
    public bool loadOnAwake = true;

    public MapConfig LoadedConfig { get; private set; }
    public MapRoot CurrentMap { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!loadOnAwake)
            return;

        MapConfig config = PendingConfig != null ? PendingConfig : defaultConfig;

        if (config == null)
        {
            Debug.LogWarning(
                "MapLoader: 로드할 MapConfig가 없습니다. (PendingConfig/defaultConfig 모두 비어 있음)",
                this);
            return;
        }

        LoadMap(config);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void LoadMap(MapConfig config)
    {
        if (config == null)
            return;

        LoadedConfig = config;

        // 매니저들이 자신의 Awake/Start에서 읽도록, 먼저 설정값을 주입한다.
        // (MapLoader가 가장 이른 실행 순서이므로 매니저 Awake보다 앞선다.)
        ApplyGridConfig(config);
        ApplyEconomyConfig(config);
        ApplyDayNightConfig(config);
        ApplyWaveConfig(config);

        // 재로드 대비: 기존 맵 인스턴스 제거
        if (CurrentMap != null)
            Destroy(CurrentMap.gameObject);

        if (config.mapRootPrefab == null)
        {
            Debug.LogError(
                $"MapLoader: '{config.displayName}'의 Map Root Prefab이 비어 있습니다.",
                config);
            return;
        }

        GameObject instance = Instantiate(
            config.mapRootPrefab,
            Vector3.zero,
            Quaternion.identity,
            mapParent);

        instance.name = config.mapRootPrefab.name;
        CurrentMap = instance.GetComponent<MapRoot>();

        if (CurrentMap == null)
        {
            Debug.LogError(
                "MapLoader: 맵 프리팹 루트에 MapRoot 컴포넌트가 없습니다.",
                instance);
            return;
        }

        // 런타임 NavMesh 굽기 옵션이 켜져 있으면 다시 굽는다(소스 메쉬 Read/Write 필요).
        // 꺼져 있으면 프리팹에 미리 구운 NavMesh 데이터가 인스턴스화 시 자동 등록되므로,
        // MapGrid 경계만 갱신한다. (MapGrid.Instance가 아직 없으면 MapGrid가 자신의 Start에서 갱신)
        if (CurrentMap.bakeNavMeshOnStart)
            CurrentMap.BuildNavMesh();
        else
            CurrentMap.RefreshMapGrid();
    }

    void ApplyGridConfig(MapConfig config)
    {
        MapGrid grid = FindFirstObjectByType<MapGrid>();

        if (grid != null)
            grid.cellSize = config.cellSize;
    }

    void ApplyEconomyConfig(MapConfig config)
    {
        if (!config.overrideEconomy)
            return;

        WattManager watt = FindFirstObjectByType<WattManager>();

        if (watt == null)
            return;

        watt.maxWatt = config.maxWatt;
        watt.startingWatt = config.startingWatt;
        watt.incomePerSecond = config.incomePerSecond;
    }

    void ApplyDayNightConfig(MapConfig config)
    {
        if (!config.overrideDayNight)
            return;

        DayNightCycle cycle = FindFirstObjectByType<DayNightCycle>();

        if (cycle == null)
            return;

        cycle.startPhase = config.startPhase;
        cycle.dayDuration = config.dayDuration;
        cycle.nightDuration = config.nightDuration;
    }

    void ApplyWaveConfig(MapConfig config)
    {
        if (!config.overrideWave)
            return;

        WaveManager wave = FindFirstObjectByType<WaveManager>();

        if (wave == null)
            return;

        wave.initialEnemyCount = config.initialEnemyCount;
        wave.initialEnemiesAdvanceToBase = config.initialEnemiesAdvanceToBase;
        wave.nightWaveStartDelay = config.nightWaveStartDelay;
        wave.waveInterval = config.waveInterval;
        wave.enemiesPerWave = config.enemiesPerWave;
        wave.maxWavesPerNight = config.maxWavesPerNight;
    }
}
