using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 스테이지(MapConfig) 통합 관리 에디터입니다.
// - 좌측: 프로젝트의 모든 MapConfig(스테이지) 목록 (검색/추가/복제/삭제)
// - 우측: 선택한 스테이지의 설정을 카테고리(섹션)별로 편집. 섹션을 2열로 배치해
//   (Identity|Map Content, Economy|Day-Night, Wave|Win Condition) 세로로
//   너무 길어지지 않게 한다.
// - 씬 <-> 스테이지 값 동기화(가져오기/적용), 유효성 경고, 미리보기 요약 제공
// Tools > Map > Stage Editor
public class StageEditorWindow : EditorWindow
{
    const string LastSelectedPathKey = "StageEditorWindow.LastSelectedPath";
    const string DefaultStageFolder = "Assets/Data/Maps";

    static readonly Color IdentityColor = new Color(0.30f, 0.45f, 0.55f);
    static readonly Color MapColor = new Color(0.20f, 0.50f, 0.45f);
    static readonly Color EconomyColor = new Color(0.60f, 0.48f, 0.10f);
    static readonly Color DayNightColor = new Color(0.28f, 0.30f, 0.58f);
    static readonly Color WaveColor = new Color(0.58f, 0.24f, 0.22f);
    static readonly Color WinConditionColor = new Color(0.55f, 0.42f, 0.12f);

    List<MapConfig> stages = new List<MapConfig>();
    MapConfig selected;
    SerializedObject serializedObject;

    string searchFilter = "";
    Vector2 listScroll;
    Vector2 detailScroll;

    bool foldIdentity = true;
    bool foldMap = true;
    bool foldEconomy = true;
    bool foldDayNight = true;
    bool foldWave = true;
    bool foldWinCondition = true;

    // 분당 스폰 수 계산용. 스포너는 맵 프리팹 또는 열려 있는 씬에서 스캔해 캐시한다.
    bool foldSpawnRateDetail;
    int spawnRateWave = 1;
    List<SpawnerInfo> spawnerCache;
    MapConfig spawnerCacheOwner;
    string spawnerSource = "";

    [MenuItem("Tools/Map/Stage Editor (스테이지 에디터)")]
    static void Open()
    {
        StageEditorWindow window = GetWindow<StageEditorWindow>(false, "Stage Editor", true);
        window.minSize = new Vector2(760f, 560f);
        window.Show();
    }

    void OnEnable()
    {
        RefreshStageList();
        RestoreLastSelection();
    }

    void OnFocus()
    {
        RefreshStageList();

        // 다른 창에서 스포너를 고쳤을 수 있으니 다시 스캔하게 한다.
        InvalidateSpawnerCache();
    }

    void RefreshStageList()
    {
        stages.Clear();

        string[] guids = AssetDatabase.FindAssets("t:MapConfig");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MapConfig config = AssetDatabase.LoadAssetAtPath<MapConfig>(path);
            if (config != null)
                stages.Add(config);
        }

        stages.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase));

        if (selected != null && !stages.Contains(selected))
            SetSelected(null);
    }

    void RestoreLastSelection()
    {
        string path = EditorPrefs.GetString(LastSelectedPathKey, "");
        if (string.IsNullOrEmpty(path))
            return;

        MapConfig config = AssetDatabase.LoadAssetAtPath<MapConfig>(path);
        if (config != null)
            SetSelected(config);
    }

    void SetSelected(MapConfig config)
    {
        selected = config;
        InvalidateSpawnerCache();
        serializedObject = selected != null ? new SerializedObject(selected) : null;
        EditorPrefs.SetString(LastSelectedPathKey, selected != null ? AssetDatabase.GetAssetPath(selected) : "");
    }

    void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawStageList();
        DrawStageDetail();
        EditorGUILayout.EndHorizontal();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("새 스테이지", EditorStyles.toolbarButton, GUILayout.Width(90)))
            CreateNewStage();

        EditorGUI.BeginDisabledGroup(selected == null);
        if (GUILayout.Button("복제", EditorStyles.toolbarButton, GUILayout.Width(60)))
            DuplicateSelected();
        if (GUILayout.Button("삭제", EditorStyles.toolbarButton, GUILayout.Width(60)))
            DeleteSelected();
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(8);

        if (GUILayout.Button("전체 저장", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("모든 스테이지를 저장했습니다."));
        }

        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70)))
            RefreshStageList();

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"총 {stages.Count}개 스테이지", EditorStyles.toolbarButton, GUILayout.Width(110));

        EditorGUILayout.EndHorizontal();
    }

    void DrawStageList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(240));

        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);

        // 표시 이름 중복 여부 - 여러 스테이지가 같은 이름이면 헷갈리므로 경고 아이콘을 붙인다.
        HashSet<string> seenNames = new HashSet<string>();
        HashSet<string> duplicateNames = new HashSet<string>();
        foreach (MapConfig cfg in stages)
        {
            if (cfg == null)
                continue;
            if (!seenNames.Add(cfg.displayName))
                duplicateNames.Add(cfg.displayName);
        }

        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));

        foreach (MapConfig cfg in stages)
        {
            if (cfg == null)
                continue;

            if (!string.IsNullOrEmpty(searchFilter) &&
                cfg.displayName.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            bool isSelected = cfg == selected;
            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(6, 6, 4, 4),
            };

            Rect rowRect = GUILayoutUtility.GetRect(GUIContent.none, style, GUILayout.Height(28));

            if (isSelected)
                EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.42f, 0.65f, 0.55f));
            else if (GUI.enabled && rowRect.Contains(Event.current.mousePosition))
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.04f));

            string label = cfg.displayName;
            if (duplicateNames.Contains(cfg.displayName))
                label += "  ⚠"; // 이름 중복

            if (cfg.mapRootPrefab == null)
                label += "  ⛔"; // 필수값(맵 루트 프리팹) 누락

            if (GUI.Button(rowRect, label, style))
                SetSelected(cfg);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawStageDetail()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        if (selected == null || serializedObject == null || serializedObject.targetObject == null)
        {
            EditorGUILayout.HelpBox("왼쪽 목록에서 스테이지를 선택하거나, '새 스테이지'로 새로 만드세요.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        serializedObject.Update();

        DrawSceneSyncBar();

        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 110f;

        // 섹션 자체를 2열로 배치해 세로로 길어지는 걸 줄인다.
        DrawSectionRow(DrawIdentitySection, DrawMapContentSection);
        DrawSectionRow(DrawEconomySection, DrawDayNightSection);
        DrawSectionRow(DrawWaveSection, DrawWinConditionSection);

        EditorGUIUtility.labelWidth = previousLabelWidth;

        EditorGUILayout.EndScrollView();

        if (serializedObject.ApplyModifiedProperties())
        {
            // 값이 바뀌면 즉시 반영되도록. (파일 저장 자체는 '전체 저장' 또는 프로젝트 저장 시 이뤄짐)
        }

        EditorGUILayout.EndVertical();
    }

    void DrawSceneSyncBar()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(selected), EditorStyles.miniLabel);

        if (GUILayout.Button("가져오기(씬→스테이지)", GUILayout.Width(160)))
            PullFromScene();

        if (GUILayout.Button("적용(스테이지→씬)", GUILayout.Width(140)))
            PushToScene();

        if (GUILayout.Button("이 스테이지로 플레이", GUILayout.Width(140)))
            PlayThisStage();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
    }

    const float SectionColumnGap = 8f;

    // 두 섹션을 좌우로 나란히, 같은 폭으로 그린다. (예: Identity | Map Content)
    // ExpandWidth만 쓰면 내용물(필드 개수·썸네일 등)에 따라 두 칸의 최소 폭이
    // 달라져서 반반으로 안 나뉘고, 창을 늘릴 때도 한쪽만 더 늘어난다.
    // 그래서 매 OnGUI마다 창 폭 기준으로 칼럼 폭을 직접 계산해 고정 폭으로 준다.
    void DrawSectionRow(Action left, Action right)
    {
        float columnWidth = GetSectionColumnWidth();

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.Width(columnWidth));
        left();
        EditorGUILayout.EndVertical();

        GUILayout.Space(SectionColumnGap);

        EditorGUILayout.BeginVertical(GUILayout.Width(columnWidth));
        right();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    float GetSectionColumnWidth()
    {
        const float listPanelWidth = 240f;
        const float outerPadding = 24f; // 스크롤바 + 좌우 여백

        float detailWidth = Mathf.Max(300f, position.width - listPanelWidth - outerPadding);
        return Mathf.Max(150f, (detailWidth - SectionColumnGap) * 0.5f);
    }

    // ---------- Sections ----------

    void DrawIdentitySection()
    {
        foldIdentity = DrawSectionFoldout("Identity (이름/설명)", IdentityColor, foldIdentity);
        if (!foldIdentity)
            return;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("이름"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"), new GUIContent("설명"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("previewImage"), new GUIContent("미리보기 이미지"));
        EditorGUILayout.EndVertical();

        if (selected.previewImage != null)
        {
            Texture2D preview = AssetPreview.GetAssetPreview(selected.previewImage);
            if (preview != null)
                GUILayout.Label(preview, GUILayout.Width(64), GUILayout.Height(64));
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6);
    }

    void DrawMapContentSection()
    {
        foldMap = DrawSectionFoldout("Map Content (맵 프리팹)", MapColor, foldMap);
        if (!foldMap)
            return;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("mapRootPrefab"), new GUIContent("맵 루트 프리팹"));

        if (selected.mapRootPrefab == null)
        {
            EditorGUILayout.HelpBox(
                "맵 루트 프리팹이 없으면 이 스테이지를 로드할 수 없습니다. MapRoot 컴포넌트가 붙은 프리팹을 지정하세요.",
                MessageType.Error);
        }

        EditorGUILayout.Space(6);
    }

    void DrawEconomySection()
    {
        foldEconomy = DrawSectionFoldout("Economy / Watt (자원)", EconomyColor, foldEconomy);
        if (!foldEconomy)
            return;

        DrawOverrideToggle("overrideEconomy", "이 스테이지 값으로 WattManager 덮어쓰기");

        EditorGUI.BeginDisabledGroup(!selected.overrideEconomy);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxWatt"), new GUIContent("최대 Watt"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startingWatt"), new GUIContent("시작 Watt"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("incomePerSecond"), new GUIContent("초당 충전량"));
        EditorGUI.EndDisabledGroup();

        if (selected.overrideEconomy)
        {
            if (selected.startingWatt > selected.maxWatt)
                EditorGUILayout.HelpBox("시작 Watt가 최대 Watt보다 큽니다. 시작 즉시 최대치로 잘립니다.", MessageType.Warning);

            if (selected.incomePerSecond <= 0f)
                EditorGUILayout.HelpBox("초당 충전량이 0 이하입니다. Watt는 건물/이벤트로만 얻게 됩니다.", MessageType.Info);
            else
                EditorGUILayout.LabelField(
                    $"미리보기: 시작 {selected.startingWatt:0.#} → 최대 {selected.maxWatt:0.#}까지 " +
                    $"약 {SafeDivide(selected.maxWatt - selected.startingWatt, selected.incomePerSecond):0.#}초 소요",
                    EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6);
    }

    void DrawDayNightSection()
    {
        foldDayNight = DrawSectionFoldout("Day / Night (낮과 밤)", DayNightColor, foldDayNight);
        if (!foldDayNight)
            return;

        DrawOverrideToggle("overrideDayNight", "이 스테이지 값으로 DayNightCycle 덮어쓰기");

        EditorGUI.BeginDisabledGroup(!selected.overrideDayNight);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startPhase"), new GUIContent("시작 페이즈"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("dayDuration"), new GUIContent("낮 지속시간(초)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("nightDuration"), new GUIContent("밤 지속시간(초)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lightTransitionDuration"), new GUIContent("라이트 전환시간(초)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("dayLightColor"), new GUIContent("낮 라이트 색상"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("dayLightIntensity"), new GUIContent("낮 라이트 강도"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("nightLightColor"), new GUIContent("밤 라이트 색상"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("nightLightIntensity"), new GUIContent("밤 라이트 강도"));
        EditorGUI.EndDisabledGroup();

        if (selected.overrideDayNight)
        {
            if (selected.dayDuration <= 0f || selected.nightDuration <= 0f)
                EditorGUILayout.HelpBox("낮/밤 지속시간은 0보다 커야 정상적으로 순환합니다.", MessageType.Warning);

            if (selected.lightTransitionDuration > selected.dayDuration ||
                selected.lightTransitionDuration > selected.nightDuration)
            {
                EditorGUILayout.HelpBox(
                    "라이트 전환시간이 낮 또는 밤 지속시간보다 길면, 페이즈가 끝나기 전에 다음 전환이 겹칠 수 있습니다.",
                    MessageType.Warning);
            }

            EditorGUILayout.LabelField(
                $"미리보기: 한 사이클(낮+밤) = {selected.dayDuration + selected.nightDuration:0.#}초 " +
                $"(낮 {selected.dayDuration:0.#}s / 밤 {selected.nightDuration:0.#}s)",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6);
    }

    void DrawWaveSection()
    {
        foldWave = DrawSectionFoldout("Wave (적 웨이브)", WaveColor, foldWave);
        if (!foldWave)
            return;

        DrawOverrideToggle("overrideWave", "이 스테이지 값으로 WaveManager 덮어쓰기");

        EditorGUILayout.HelpBox(
            "스포너(EnemySpawner)는 맵 프리팹에 미리 배치합니다.\n" +
            "여기서는 웨이브마다 그 스포너들의 스폰량과 스폰되는 적의 스탯 가중치만 조절합니다.",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(!selected.overrideWave);

        SerializedProperty plan = serializedObject.FindProperty("wavePlan");

        EditorGUILayout.LabelField("웨이브별 수치", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            plan.FindPropertyRelative("waves"),
            new GUIContent("웨이브 표 (0번 = 웨이브 1)"),
            true);

        EditorGUILayout.PropertyField(
            plan.FindPropertyRelative("growthPerWaveAfterLast"),
            new GUIContent("표 이후 웨이브 증가율"),
            true);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("밤 보정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("applyNightBonus"),
            new GUIContent("밤에 추가 보정 적용"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("nightBonus"),
            new GUIContent("밤 보정"),
            true);

        EditorGUI.EndDisabledGroup();

        if (selected.overrideWave)
        {
            DrawWavePreview();
            DrawSpawnRateSection();
        }

        EditorGUILayout.Space(6);
    }

    const int WavePreviewCount = 8;

    void DrawWavePreview()
    {
        WavePlan plan = selected.wavePlan;

        if (plan == null || plan.AuthoredWaveCount == 0)
        {
            EditorGUILayout.HelpBox(
                "웨이브 표가 비어 있습니다. 모든 웨이브가 기본값(배율 1)으로 진행됩니다.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(
            $"미리보기 (웨이브 1~{WavePreviewCount})",
            EditorStyles.boldLabel);

        for (int wave = 1; wave <= WavePreviewCount; wave++)
        {
            WaveTuning tuning = plan.Evaluate(wave);
            bool extrapolated = wave > plan.AuthoredWaveCount;

            EditorGUILayout.LabelField(
                $"웨이브 {wave}{(extrapolated ? " (증가율)" : "")}",
                tuning.ToShortSummary(),
                EditorStyles.miniLabel);
        }

        if (selected.applyNightBonus)
        {
            WaveTuning night = WaveTuning.Combine(
                plan.Evaluate(1),
                selected.nightBonus).Sanitized();

            EditorGUILayout.LabelField(
                "웨이브 1 (밤)",
                night.ToShortSummary(),
                EditorStyles.miniLabel);
        }

        if (IsGrowthFlat(plan.growthPerWaveAfterLast))
        {
            EditorGUILayout.HelpBox(
                $"증가율이 모두 1이라 웨이브 {plan.AuthoredWaveCount} 이후에는 난이도가 더 오르지 않습니다.",
                MessageType.Info);
        }
    }

    static bool IsGrowthFlat(WaveTuning growth)
    {
        if (growth == null)
            return true;

        return Mathf.Approximately(growth.spawnCountMultiplier, 1f) &&
               growth.spawnCountBonus == 0 &&
               Mathf.Approximately(growth.spawnIntervalMultiplier, 1f) &&
               Mathf.Approximately(growth.maxAliveMultiplier, 1f) &&
               Mathf.Approximately(growth.healthMultiplier, 1f) &&
               Mathf.Approximately(growth.damageMultiplier, 1f) &&
               Mathf.Approximately(growth.speedMultiplier, 1f);
    }

    // ── 분당 스폰 몬스터 수 ───────────────────────────────────────
    // 웨이브 수치(스폰량/간격)와 실제 스포너 수를 곱해서 "분당 몇 마리가 나오는지"를 보여준다.
    // 스포너 인스펙터 값만 봐서는 체감 난이도를 가늠할 수 없어서 여기서 합산한다.

    struct SpawnerInfo
    {
        public string name;
        public int enemiesPerSpawn;
        public float spawnInterval;
        public int maxAliveEnemies;
        public int activateFromWave;
        public bool spawnPeriodically;
        public bool hasPrefab;
    }

    void InvalidateSpawnerCache()
    {
        spawnerCache = null;
        spawnerCacheOwner = null;
    }

    List<SpawnerInfo> GetSpawners()
    {
        if (spawnerCache != null && spawnerCacheOwner == selected)
            return spawnerCache;

        spawnerCache = new List<SpawnerInfo>();
        spawnerCacheOwner = selected;
        spawnerSource = "";

        if (selected == null)
            return spawnerCache;

        List<EnemySpawner> found = new List<EnemySpawner>();

        // 스포너는 맵 프리팹에 미리 배치하는 것이 원칙이다. 거기 없으면 열려 있는 씬에서 찾는다.
        if (selected.mapRootPrefab != null)
        {
            found.AddRange(selected.mapRootPrefab.GetComponentsInChildren<EnemySpawner>(true));

            if (found.Count > 0)
                spawnerSource = $"맵 프리팹 '{selected.mapRootPrefab.name}'";
        }

        if (found.Count == 0)
        {
            found.AddRange(
                FindObjectsByType<EnemySpawner>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None));

            if (found.Count > 0)
                spawnerSource = $"열려 있는 씬 '{SceneManager.GetActiveScene().name}'";
        }

        foreach (EnemySpawner spawner in found)
        {
            if (spawner == null)
                continue;

            spawnerCache.Add(new SpawnerInfo
            {
                name = spawner.name,
                enemiesPerSpawn = spawner.enemiesPerSpawn,
                spawnInterval = spawner.spawnInterval,
                maxAliveEnemies = spawner.maxAliveEnemies,
                activateFromWave = Mathf.Max(1, spawner.activateFromWave),
                spawnPeriodically = spawner.spawnPeriodically,
                hasPrefab = spawner.enemyPrefabs != null && spawner.enemyPrefabs.Count > 0
            });
        }

        return spawnerCache;
    }

    WaveTuning BuildTuning(int waveNumber, bool night)
    {
        return WaveTuning.BuildForWave(
            selected.wavePlan,
            selected.nightBonus,
            selected.applyNightBonus,
            waveNumber,
            night);
    }

    // 한 스포너가 1분 동안 스폰하는 몬스터 수입니다. 주기 스폰만 계산합니다.
    static float SpawnsPerMinute(SpawnerInfo info, WaveTuning tuning, int waveNumber)
    {
        if (!info.spawnPeriodically || !info.hasPrefab || waveNumber < info.activateFromWave)
            return 0f;

        int count = EnemySpawner.GetEffectiveSpawnCount(info.enemiesPerSpawn, tuning);
        float interval = EnemySpawner.GetEffectiveSpawnInterval(info.spawnInterval, tuning);

        if (count <= 0 || interval <= 0f)
            return 0f;

        return count * 60f / interval;
    }

    void DrawSpawnRateSection()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("분당 스폰 몬스터 수", EditorStyles.boldLabel);

        List<SpawnerInfo> spawners = GetSpawners();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            spawners.Count > 0
                ? $"스포너 {spawners.Count}개 · 출처: {spawnerSource}"
                : "스포너를 찾지 못했습니다.",
            EditorStyles.miniLabel);

        if (GUILayout.Button("다시 스캔", EditorStyles.miniButton, GUILayout.Width(70)))
            InvalidateSpawnerCache();

        EditorGUILayout.EndHorizontal();

        if (spawners.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "맵 프리팹에도, 열려 있는 씬에도 EnemySpawner가 없습니다.\n" +
                "스포너를 맵 프리팹에 배치하거나 그 씬을 연 뒤 '다시 스캔'을 누르세요.",
                MessageType.Info);
            return;
        }

        bool showNight = selected.applyNightBonus;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField(
            "웨이브",
            showNight ? "분당 스폰 수 (낮 / 밤, 스포너 전체 합계)" : "분당 스폰 수 (스포너 전체 합계)",
            EditorStyles.miniBoldLabel);

        for (int wave = 1; wave <= WavePreviewCount; wave++)
        {
            WaveTuning dayTuning = BuildTuning(wave, false);
            WaveTuning nightTuning = BuildTuning(wave, true);

            float dayTotal = 0f;
            float nightTotal = 0f;
            int activeSpawners = 0;

            foreach (SpawnerInfo info in spawners)
            {
                float perMinute = SpawnsPerMinute(info, dayTuning, wave);

                dayTotal += perMinute;
                nightTotal += SpawnsPerMinute(info, nightTuning, wave);

                if (perMinute > 0f)
                    activeSpawners++;
            }

            string amount = showNight
                ? $"{dayTotal:0}마리 / {nightTotal:0}마리"
                : $"{dayTotal:0}마리";

            string detail = activeSpawners > 0
                ? $"  (스포너 {activeSpawners}개 · 개당 {dayTotal / activeSpawners:0}마리)"
                : "  (활동 중인 스포너 없음)";

            EditorGUILayout.LabelField($"웨이브 {wave}", amount + detail, EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();

        DrawAliveCapNote(spawners);

        EditorGUILayout.HelpBox(
            "주기 스폰(Spawn Periodically)만 계산한 값입니다.\n" +
            "근처에 아군이 있거나 피격당했을 때의 추가 스폰, 스포너 파괴 시 방출은 포함하지 않습니다.",
            MessageType.None);

        DrawSpawnRateDetail(spawners);
    }

    void DrawAliveCapNote(List<SpawnerInfo> spawners)
    {
        WaveTuning tuning = BuildTuning(1, false);

        int capTotal = 0;
        bool hasUnlimited = false;

        foreach (SpawnerInfo info in spawners)
        {
            int cap = EnemySpawner.GetEffectiveMaxAlive(info.maxAliveEnemies, tuning);

            if (cap <= 0)
                hasUnlimited = true;
            else
                capTotal += cap;
        }

        if (hasUnlimited)
            return;

        EditorGUILayout.HelpBox(
            $"동시 생존 상한 합계는 {capTotal}마리입니다(웨이브 1 기준). " +
            "적이 죽지 않으면 스폰이 여기서 멈추므로, 위 분당 수치는 최대치입니다.",
            MessageType.Info);
    }

    void DrawSpawnRateDetail(List<SpawnerInfo> spawners)
    {
        foldSpawnRateDetail = EditorGUILayout.Foldout(foldSpawnRateDetail, "스포너별 상세", true);

        if (!foldSpawnRateDetail)
            return;

        spawnRateWave = Mathf.Max(1, EditorGUILayout.IntField("기준 웨이브", spawnRateWave));

        WaveTuning tuning = BuildTuning(spawnRateWave, false);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        foreach (SpawnerInfo info in spawners)
        {
            int cap = EnemySpawner.GetEffectiveMaxAlive(info.maxAliveEnemies, tuning);
            string capText = cap <= 0 ? "무제한" : $"{cap}마리";

            EditorGUILayout.LabelField(
                info.name,
                $"{DescribeSpawner(info, tuning, spawnRateWave)} · 상한 {capText}",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    static string DescribeSpawner(SpawnerInfo info, WaveTuning tuning, int waveNumber)
    {
        if (!info.hasPrefab)
            return "적 프리팹이 비어 있음";

        if (!info.spawnPeriodically)
            return "주기 스폰 꺼짐 (아군 접근/피격 때만 스폰)";

        if (waveNumber < info.activateFromWave)
            return $"웨이브 {info.activateFromWave}부터 활동";

        int count = EnemySpawner.GetEffectiveSpawnCount(info.enemiesPerSpawn, tuning);
        float interval = EnemySpawner.GetEffectiveSpawnInterval(info.spawnInterval, tuning);
        float perMinute = SpawnsPerMinute(info, tuning, waveNumber);

        return $"{count}마리 / {interval:0.#}초 → 분당 {perMinute:0}마리";
    }

    void DrawWinConditionSection()
    {
        foldWinCondition = DrawSectionFoldout("Win Condition (승리 조건)", WinConditionColor, foldWinCondition);
        if (!foldWinCondition)
            return;

        DrawOverrideToggle("overrideWinCondition", "이 스테이지 값으로 GameResultManager 덮어쓰기");

        EditorGUI.BeginDisabledGroup(!selected.overrideWinCondition);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("survivalNightsToWin"), new GUIContent("생존 목표 (몇 번째 밤까지)"));
        EditorGUI.EndDisabledGroup();

        if (selected.overrideWinCondition)
        {
            if (selected.survivalNightsToWin <= 0)
                EditorGUILayout.HelpBox("생존 목표가 0 이하입니다. 게임 시작과 거의 동시에 승리 조건이 충족됩니다.", MessageType.Warning);

            EditorGUILayout.LabelField(
                $"미리보기: 본부가 파괴되지 않고 {selected.survivalNightsToWin}번째 밤이 끝나면 승리합니다.",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "다른 승리/패배 조건은 GameResultManager.EndGame()을 호출하는 방식으로 나중에 추가할 수 있습니다.",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6);
    }

    // ---------- Helpers ----------

    bool DrawSectionFoldout(string title, Color color, bool expanded)
    {
        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.boldLabel, GUILayout.Height(24));
        EditorGUI.DrawRect(rect, color);

        GUIStyle style = new GUIStyle(EditorStyles.foldout)
        {
            normal = { textColor = Color.white },
            onNormal = { textColor = Color.white },
            focused = { textColor = Color.white },
            fontStyle = FontStyle.Bold,
        };

        Rect foldoutRect = new Rect(rect.x + 6, rect.y + 3, rect.width - 12, rect.height - 6);
        return EditorGUI.Foldout(foldoutRect, expanded, title, true, style);
    }

    void DrawOverrideToggle(string propertyName, string label)
    {
        SerializedProperty prop = serializedObject.FindProperty(propertyName);
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = prop.boolValue ? new Color(0.55f, 0.85f, 0.55f) : new Color(0.85f, 0.55f, 0.55f);
        EditorGUILayout.PropertyField(prop, new GUIContent(label));
        GUI.backgroundColor = prev;
    }

    static float SafeDivide(float a, float b) => b <= 0f ? 0f : a / b;

    // ---------- Stage list actions ----------

    void CreateNewStage()
    {
        EnsureFolderExists(DefaultStageFolder);

        string path = EditorUtility.SaveFilePanelInProject(
            "새 스테이지 만들기",
            "NewStage",
            "asset",
            "스테이지(MapConfig) 에셋을 저장할 위치를 선택하세요.",
            DefaultStageFolder);

        if (string.IsNullOrEmpty(path))
            return;

        MapConfig config = ScriptableObject.CreateInstance<MapConfig>();
        config.displayName = System.IO.Path.GetFileNameWithoutExtension(path);

        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RefreshStageList();
        SetSelected(config);
        EditorGUIUtility.PingObject(config);
    }

    void DuplicateSelected()
    {
        if (selected == null)
            return;

        string sourcePath = AssetDatabase.GetAssetPath(selected);
        string newPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);

        if (AssetDatabase.CopyAsset(sourcePath, newPath))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            MapConfig copy = AssetDatabase.LoadAssetAtPath<MapConfig>(newPath);
            RefreshStageList();
            SetSelected(copy);
            EditorGUIUtility.PingObject(copy);
        }
    }

    void DeleteSelected()
    {
        if (selected == null)
            return;

        string path = AssetDatabase.GetAssetPath(selected);

        if (!EditorUtility.DisplayDialog(
                "스테이지 삭제",
                $"'{selected.displayName}' 스테이지를 삭제할까요?\n({path})\n\n이 작업은 되돌릴 수 없습니다.",
                "삭제",
                "취소"))
        {
            return;
        }

        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();

        SetSelected(null);
        RefreshStageList();
    }

    // ---------- Scene sync ----------

    void PullFromScene()
    {
        if (selected == null)
            return;

        Undo.RecordObject(selected, "Pull Stage From Scene");

        WattManager watt = UnityEngine.Object.FindFirstObjectByType<WattManager>();
        if (watt != null)
        {
            selected.maxWatt = watt.maxWatt;
            selected.startingWatt = watt.startingWatt;
            selected.incomePerSecond = watt.incomePerSecond;
        }

        DayNightCycle cycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
        if (cycle != null)
        {
            selected.startPhase = cycle.startPhase;
            selected.dayDuration = cycle.dayDuration;
            selected.nightDuration = cycle.nightDuration;
            selected.dayLightColor = cycle.dayLightColor;
            selected.nightLightColor = cycle.nightLightColor;
            selected.dayLightIntensity = cycle.dayLightIntensity;
            selected.nightLightIntensity = cycle.nightLightIntensity;
            selected.lightTransitionDuration = cycle.lightTransitionDuration;
        }

        WaveManager wave = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
        if (wave != null)
        {
            selected.wavePlan = wave.wavePlan != null ? wave.wavePlan.Clone() : new WavePlan();
            selected.applyNightBonus = wave.applyNightBonus;
            selected.nightBonus = new WaveTuning(wave.nightBonus);
        }

        GameResultManager result = UnityEngine.Object.FindFirstObjectByType<GameResultManager>();
        if (result != null)
            selected.survivalNightsToWin = result.survivalNightsToWin;

        EditorUtility.SetDirty(selected);
        serializedObject.Update();

        if (watt == null && cycle == null && wave == null && result == null)
            ShowNotification(new GUIContent("씬에서 매니저를 찾지 못했습니다. 씬을 열고 다시 시도하세요."));
        else
            ShowNotification(new GUIContent("씬의 현재 값을 가져왔습니다."));
    }

    void PushToScene()
    {
        if (selected == null)
            return;

        bool appliedAny = false;

        if (selected.overrideEconomy)
        {
            WattManager watt = UnityEngine.Object.FindFirstObjectByType<WattManager>();
            if (watt != null)
            {
                Undo.RecordObject(watt, "Apply Stage To Scene");
                watt.maxWatt = selected.maxWatt;
                watt.startingWatt = selected.startingWatt;
                watt.incomePerSecond = selected.incomePerSecond;
                EditorUtility.SetDirty(watt);
                appliedAny = true;
            }
        }

        if (selected.overrideDayNight)
        {
            DayNightCycle cycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null)
            {
                Undo.RecordObject(cycle, "Apply Stage To Scene");
                cycle.startPhase = selected.startPhase;
                cycle.dayDuration = selected.dayDuration;
                cycle.nightDuration = selected.nightDuration;
                cycle.dayLightColor = selected.dayLightColor;
                cycle.nightLightColor = selected.nightLightColor;
                cycle.dayLightIntensity = selected.dayLightIntensity;
                cycle.nightLightIntensity = selected.nightLightIntensity;
                cycle.lightTransitionDuration = selected.lightTransitionDuration;
                EditorUtility.SetDirty(cycle);
                appliedAny = true;
            }
        }

        if (selected.overrideWave)
        {
            WaveManager wave = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
            if (wave != null)
            {
                Undo.RecordObject(wave, "Apply Stage To Scene");

                wave.wavePlan = selected.wavePlan != null
                    ? selected.wavePlan.Clone()
                    : new WavePlan();

                wave.applyNightBonus = selected.applyNightBonus;
                wave.nightBonus = new WaveTuning(selected.nightBonus);

                EditorUtility.SetDirty(wave);
                appliedAny = true;
            }
        }

        if (selected.overrideWinCondition)
        {
            GameResultManager result = UnityEngine.Object.FindFirstObjectByType<GameResultManager>();
            if (result != null)
            {
                Undo.RecordObject(result, "Apply Stage To Scene");
                result.survivalNightsToWin = selected.survivalNightsToWin;
                EditorUtility.SetDirty(result);
                appliedAny = true;
            }
        }

        if (appliedAny)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            ShowNotification(new GUIContent("스테이지 값을 씬에 적용했습니다."));
        }
        else
        {
            ShowNotification(new GUIContent("씬에서 매니저를 찾지 못했습니다. 씬을 열고 다시 시도하세요."));
        }
    }

    void PlayThisStage()
    {
        if (selected == null)
            return;

        // static 필드만 설정하면 플레이 모드 진입 시 도메인 리로드로 초기화되어
        // MapLoader.Awake()가 값을 받기 전에 사라진다. SessionState까지 같이 남겨
        // 리로드 후에도 복원되게 한다. (MapLoader.SetPendingConfigForNextPlay 참고)
        MapLoader.SetPendingConfigForNextPlay(selected);

        if (!EditorApplication.isPlaying)
            EditorApplication.isPlaying = true;

        ShowNotification(new GUIContent($"'{selected.displayName}' 스테이지로 플레이합니다."));
    }

    static void EnsureFolderExists(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(folder);

        if (string.IsNullOrEmpty(parent))
            return;

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolderExists(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
