using UnityEngine;

// MapConfig를 받아 맵을 로드하는 오케스트레이터입니다.
// 가장 이른 실행 순서(-1000)로 두어, 씬의 매니저(WattManager/
// DayNightCycle/WaveManager)들이 자신의 Awake/Start를 실행하기 "전에"
//   1) 설정값을 각 매니저에 주입하고
//   2) 맵 프리팹을 인스턴스화 + NavMesh를 굽습니다.
// 이렇게 하면 매니저들은 주입된 값과 인스턴스화된 스포너를 그대로 읽어
// 별도 수정 없이 정상 동작합니다.
// 씬에 맵(MapRoot)이 이미 놓여 있을 때의 처리 방식입니다.
public enum SceneMapHandling
{
    // 씬에 남아 있는 맵을 제거하고 스테이지의 맵 프리팹을 새로 만듭니다. (겹침 방지)
    Replace,

    // 씬에 있는 맵을 그대로 씁니다. 맵을 씬에 꺼내 편집하는 중에 편합니다.
    UseSceneMap
}

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class MapLoader : MonoBehaviour
{
    public static MapLoader Instance { get; private set; }

    // 맵 선택 화면에서 씬을 로드하기 전에 설정합니다. 있으면 defaultConfig보다 우선합니다.
    public static MapConfig PendingConfig;

#if UNITY_EDITOR
    // 에디터에서 플레이 모드로 들어가면 Enter Play Mode Settings에 따라 도메인이
    // 리로드되며 위 static PendingConfig가 그대로 날아간다. SessionState는 도메인
    // 리로드에도 살아남으므로, Stage Editor가 여기 경로를 남겨두면 Awake에서 복원한다.
    const string PendingConfigPathSessionKey = "MapLoader.PendingConfigPath";

    // Stage Editor 등 에디터 툴에서, 플레이 모드 진입 직전에 다음 로드할 스테이지를 지정할 때 사용합니다.
    public static void SetPendingConfigForNextPlay(MapConfig config)
    {
        PendingConfig = config;

        string path = config != null ? UnityEditor.AssetDatabase.GetAssetPath(config) : "";
        UnityEditor.SessionState.SetString(PendingConfigPathSessionKey, path);
    }
#endif

    [Header("Config")]
    [Tooltip("PendingConfig가 없을 때 로드할 기본 맵입니다. (에디터 단독 테스트용)")]
    public MapConfig defaultConfig;

    [Tooltip("맵 인스턴스를 담을 부모(선택). 비워두면 씬 루트에 생성합니다.")]
    public Transform mapParent;

    [Tooltip("Awake에서 자동으로 맵을 로드합니다.")]
    public bool loadOnAwake = true;

    [Header("Scene Map")]
    [Tooltip("씬에 맵(MapRoot)이 이미 놓여 있을 때 어떻게 할지입니다.\n" +
        "Replace: 씬의 맵을 제거하고 스테이지의 맵 프리팹을 새로 만듭니다. 맵이 두 장 겹치는 것을 막습니다.\n" +
        "Use Scene Map: 씬에 있는 맵을 그대로 쓰고 프리팹을 만들지 않습니다. 맵을 씬에 꺼내 편집하는 중에 씁니다.")]
    public SceneMapHandling sceneMapHandling = SceneMapHandling.Replace;

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

#if UNITY_EDITOR
        // 도메인 리로드로 PendingConfig가 비었으면 SessionState에 남겨둔 경로로 복원한다.
        if (PendingConfig == null)
        {
            string pendingPath = UnityEditor.SessionState.GetString(PendingConfigPathSessionKey, "");

            if (!string.IsNullOrEmpty(pendingPath))
                PendingConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<MapConfig>(pendingPath);
        }
#endif

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
        ApplyEconomyConfig(config);
        ApplyDayNightConfig(config);
        ApplyWaveConfig(config);
        ApplyInitialEnemyConfig(config);
        ApplyWinConditionConfig(config);

        // 재로드 대비: 이전에 만든 맵 인스턴스 제거
        if (CurrentMap != null)
            RemoveMapRoot(CurrentMap);

        CurrentMap = null;

        // 씬에 미리 놓여 있는 맵을 먼저 처리한다. 그대로 쓰기로 했다면 프리팹은 만들지 않는다.
        if (TryTakeSceneMap(out MapRoot sceneMap))
        {
            CurrentMap = sceneMap;
            PrepareLoadedMap();
            return;
        }

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

        PrepareLoadedMap();
    }

    void PrepareLoadedMap()
    {
        if (CurrentMap == null)
            return;

        // 런타임 NavMesh 굽기 옵션이 켜져 있으면 다시 굽는다(소스 메쉬 Read/Write 필요).
        // 꺼져 있으면 프리팹에 미리 구운 NavMesh 데이터가 인스턴스화 시 자동 등록되므로,
        // MapGrid 경계만 갱신한다. (MapGrid.Instance가 아직 없으면 MapGrid가 자신의 Start에서 갱신)
        if (CurrentMap.bakeNavMeshOnStart)
            CurrentMap.BuildNavMesh();
        else
            CurrentMap.RefreshMapGrid();
    }

    /// <summary>
    /// 씬에 이미 놓여 있는 맵을 처리합니다.
    /// 설정이 UseSceneMap이면 첫 번째 맵을 그대로 쓰고, 나머지는(그리고 Replace일 때는 전부) 제거합니다.
    /// 씬에 맵을 꺼내둔 채로 플레이하면 프리팹이 하나 더 생겨 두 장이 겹치기 때문입니다.
    /// </summary>
    bool TryTakeSceneMap(out MapRoot sceneMap)
    {
        sceneMap = null;

        // 이미 꺼진(= 방금 제거 처리한) 맵은 제외한다. 꺼져 있는 맵은 겹치지도 않는다.
        MapRoot[] existing = FindObjectsByType<MapRoot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < existing.Length; i++)
        {
            MapRoot mapRoot = existing[i];

            if (mapRoot == null)
                continue;

            if (sceneMapHandling == SceneMapHandling.UseSceneMap && sceneMap == null)
            {
                sceneMap = mapRoot;

                Debug.Log(
                    $"MapLoader: 씬에 있는 맵 '{mapRoot.name}'을 그대로 사용합니다. " +
                    "(Scene Map Handling = Use Scene Map)",
                    mapRoot);

                continue;
            }

            Debug.LogWarning(
                $"MapLoader: 씬에 남아 있던 맵 '{mapRoot.name}'을 제거했습니다. " +
                "맵은 씬에 두지 말고 스테이지의 Map Root Prefab으로만 관리하세요. " +
                "(스테이지 에디터의 '씬에서 맵 치우기' 버튼을 쓰면 됩니다.)",
                mapRoot);

            RemoveMapRoot(mapRoot);
        }

        return sceneMap != null;
    }

    void RemoveMapRoot(MapRoot mapRoot)
    {
        if (mapRoot == null)
            return;

        // Destroy는 프레임 끝에 처리된다. 그때까지 두면 맵 아래 오브젝트들의 Awake/Start가
        // 돌면서 NavMesh와 격자에 등록돼 버리므로, 먼저 꺼서 그 초기화 자체를 막는다.
        mapRoot.gameObject.SetActive(false);
        Destroy(mapRoot.gameObject);
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
        cycle.dayLightColor = config.dayLightColor;
        cycle.nightLightColor = config.nightLightColor;
        cycle.dayLightIntensity = config.dayLightIntensity;
        cycle.nightLightIntensity = config.nightLightIntensity;
        cycle.lightTransitionDuration = config.lightTransitionDuration;
    }

    void ApplyWaveConfig(MapConfig config)
    {
        if (!config.overrideWave)
            return;

        WaveManager wave = FindFirstObjectByType<WaveManager>();

        if (wave == null)
            return;

        // 사본을 넘긴다. 플레이 중 WaveManager 쪽 값을 만져도 원본 에셋이 더러워지지 않는다.
        wave.wavePlan = config.wavePlan != null
            ? config.wavePlan.Clone()
            : new WavePlan();

        wave.applyNightBonus = config.applyNightBonus;
        wave.nightBonus = new WaveTuning(config.nightBonus);
    }

    void ApplyInitialEnemyConfig(MapConfig config)
    {
        if (!config.overrideInitialEnemies)
            return;

        InitialEnemyPlacer placer = FindFirstObjectByType<InitialEnemyPlacer>();

        if (placer == null)
        {
            // 배치할 적이 없으면 굳이 컴포넌트를 만들지 않는다.
            if (config.initialEnemies == null || config.initialEnemies.Count == 0)
                return;

            // 설정은 있는데 씬에 배치기가 없으면 아무 일도 일어나지 않아 원인을 찾기 어렵다.
            // 로더 자신에게 붙여서 설정한 대로 동작하게 한다.
            placer = gameObject.AddComponent<InitialEnemyPlacer>();

            Debug.Log(
                "MapLoader: 씬에 InitialEnemyPlacer가 없어 자동으로 추가했습니다.",
                this);
        }

        // 사본을 넘긴다. 플레이 중 값을 만져도 원본 에셋이 더러워지지 않는다.
        placer.groups = InitialEnemyGroup.CloneList(config.initialEnemies);
        placer.minDistanceFromHq = config.initialEnemyMinDistanceFromHq;
        placer.avoidPlayerVision = config.initialEnemyAvoidPlayerVision;
    }

    void ApplyWinConditionConfig(MapConfig config)
    {
        if (!config.overrideWinCondition)
            return;

        GameResultManager result = FindFirstObjectByType<GameResultManager>();

        if (result == null)
            return;

        result.survivalNightsToWin = config.survivalNightsToWin;
    }
}
