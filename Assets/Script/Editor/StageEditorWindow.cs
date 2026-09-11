using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 스테이지(MapConfig) 통합 관리 에디터입니다.
// - 좌측: 프로젝트의 모든 MapConfig(스테이지) 목록 (검색/추가/복제/삭제)
// - 우측: 선택한 스테이지의 Grid/Economy/DayNight/Wave 설정을 카테고리별로 편집
// - 씬 <-> 스테이지 값 동기화(가져오기/적용), 유효성 경고, 미리보기 요약 제공
// Tools > Map > Stage Editor
public class StageEditorWindow : EditorWindow
{
    const string LastSelectedPathKey = "StageEditorWindow.LastSelectedPath";
    const string DefaultStageFolder = "Assets/Data/Maps";

    static readonly Color IdentityColor = new Color(0.30f, 0.45f, 0.55f);
    static readonly Color MapColor = new Color(0.20f, 0.50f, 0.45f);
    static readonly Color GridColor = new Color(0.45f, 0.50f, 0.20f);
    static readonly Color EconomyColor = new Color(0.60f, 0.48f, 0.10f);
    static readonly Color DayNightColor = new Color(0.28f, 0.30f, 0.58f);
    static readonly Color WaveColor = new Color(0.58f, 0.24f, 0.22f);

    List<MapConfig> stages = new List<MapConfig>();
    MapConfig selected;
    SerializedObject serializedObject;

    string searchFilter = "";
    Vector2 listScroll;
    Vector2 detailScroll;

    bool foldIdentity = true;
    bool foldMap = true;
    bool foldGrid = true;
    bool foldEconomy = true;
    bool foldDayNight = true;
    bool foldWave = true;

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

        DrawIdentitySection();
        DrawMapContentSection();
        DrawGridSection();
        DrawEconomySection();
        DrawDayNightSection();
        DrawWaveSection();

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

    void DrawGridSection()
    {
        foldGrid = DrawSectionFoldout("Grid (격자)", GridColor, foldGrid);
        if (!foldGrid)
            return;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("cellSize"), new GUIContent("칸 크기(m)"));

        if (selected.cellSize <= 0f)
            EditorGUILayout.HelpBox("칸 크기는 0보다 커야 합니다.", MessageType.Warning);

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

        EditorGUI.BeginDisabledGroup(!selected.overrideWave);

        EditorGUILayout.LabelField("적 프리팹", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enemyPrefabs"), new GUIContent("밤 스포너 프리팹"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("initialEnemyPrefabs"), new GUIContent("초기 배치 프리팹(비우면 위 목록 사용)"), true);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("초기 배치 (Day Start)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("initialEnemyCount"), new GUIContent("초기 스포너 수"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("initialMinDistanceFromHq"), new GUIContent("본부와 최소 거리"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mapEdgeMargin"), new GUIContent("맵 가장자리 여백"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("randomPositionAttempts"), new GUIContent("배치 위치 샘플 시도 횟수"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("밤 웨이브", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("nightWaveStartDelay"), new GUIContent("밤 시작 후 대기(초)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("nightWaveMinDistanceFromHq"), new GUIContent("본부와 최소 거리"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("nightWaveAvoidPlayerVision"), new GUIContent("플레이어 시야 밖에 우선 배치"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnersPerNight"), new GUIContent("밤마다 스포너 수 (0=1번째 밤)"), true);

        EditorGUI.EndDisabledGroup();

        if (selected.overrideWave)
        {
            if (!HasAnyPrefab(selected.enemyPrefabs) && !HasAnyPrefab(selected.initialEnemyPrefabs))
                EditorGUILayout.HelpBox("적 프리팹이 하나도 없습니다. 이 스테이지에서는 적이 생성되지 않습니다.", MessageType.Warning);

            EditorGUILayout.LabelField(
                $"미리보기 (1~8번째 밤): {BuildSpawnerPreview(selected.spawnersPerNight, 8)}",
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

    static bool HasAnyPrefab(List<GameObject> list) => list != null && list.Count > 0;

    static float SafeDivide(float a, float b) => b <= 0f ? 0f : a / b;

    static string BuildSpawnerPreview(List<int> list, int previewCount)
    {
        if (list == null || list.Count == 0)
            return "(설정 없음)";

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < previewCount; i++)
        {
            int index = Mathf.Clamp(i, 0, list.Count - 1);
            int value = Mathf.Max(0, list[index]);
            sb.Append(value);

            if (i < previewCount - 1)
                sb.Append(", ");
        }

        if (previewCount >= list.Count)
            sb.Append($"  ({list.Count}밤 이후 마지막 값 유지)");

        return sb.ToString();
    }

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

        MapGrid grid = Object.FindFirstObjectByType<MapGrid>();
        if (grid != null)
            selected.cellSize = grid.cellSize;

        WattManager watt = Object.FindFirstObjectByType<WattManager>();
        if (watt != null)
        {
            selected.maxWatt = watt.maxWatt;
            selected.startingWatt = watt.startingWatt;
            selected.incomePerSecond = watt.incomePerSecond;
        }

        DayNightCycle cycle = Object.FindFirstObjectByType<DayNightCycle>();
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

        WaveManager wave = Object.FindFirstObjectByType<WaveManager>();
        if (wave != null)
        {
            selected.enemyPrefabs = new List<GameObject>(wave.enemyPrefabs ?? new List<GameObject>());
            selected.initialEnemyPrefabs = new List<GameObject>(wave.initialEnemyPrefabs ?? new List<GameObject>());
            selected.initialEnemyCount = wave.initialEnemyCount;
            selected.initialMinDistanceFromHq = wave.initialMinDistanceFromHq;
            selected.mapEdgeMargin = wave.mapEdgeMargin;
            selected.randomPositionAttempts = wave.randomPositionAttempts;
            selected.nightWaveStartDelay = wave.nightWaveStartDelay;
            selected.nightWaveMinDistanceFromHq = wave.nightWaveMinDistanceFromHq;
            selected.nightWaveAvoidPlayerVision = wave.nightWaveAvoidPlayerVision;
            selected.spawnersPerNight = new List<int>(wave.spawnersPerNight ?? new List<int>());
        }

        EditorUtility.SetDirty(selected);
        serializedObject.Update();

        if (grid == null && watt == null && cycle == null && wave == null)
            ShowNotification(new GUIContent("씬에서 매니저를 찾지 못했습니다. 씬을 열고 다시 시도하세요."));
        else
            ShowNotification(new GUIContent("씬의 현재 값을 가져왔습니다."));
    }

    void PushToScene()
    {
        if (selected == null)
            return;

        bool appliedAny = false;

        MapGrid grid = Object.FindFirstObjectByType<MapGrid>();
        if (grid != null)
        {
            Undo.RecordObject(grid, "Apply Stage To Scene");
            grid.cellSize = selected.cellSize;
            EditorUtility.SetDirty(grid);
            appliedAny = true;
        }

        if (selected.overrideEconomy)
        {
            WattManager watt = Object.FindFirstObjectByType<WattManager>();
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
            DayNightCycle cycle = Object.FindFirstObjectByType<DayNightCycle>();
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
            WaveManager wave = Object.FindFirstObjectByType<WaveManager>();
            if (wave != null)
            {
                Undo.RecordObject(wave, "Apply Stage To Scene");

                if (HasAnyPrefab(selected.enemyPrefabs))
                    wave.enemyPrefabs = new List<GameObject>(selected.enemyPrefabs);

                if (HasAnyPrefab(selected.initialEnemyPrefabs))
                    wave.initialEnemyPrefabs = new List<GameObject>(selected.initialEnemyPrefabs);

                wave.initialEnemyCount = selected.initialEnemyCount;
                wave.initialMinDistanceFromHq = selected.initialMinDistanceFromHq;
                wave.mapEdgeMargin = selected.mapEdgeMargin;
                wave.randomPositionAttempts = selected.randomPositionAttempts;
                wave.nightWaveStartDelay = selected.nightWaveStartDelay;
                wave.nightWaveMinDistanceFromHq = selected.nightWaveMinDistanceFromHq;
                wave.nightWaveAvoidPlayerVision = selected.nightWaveAvoidPlayerVision;
                wave.spawnersPerNight = selected.spawnersPerNight != null
                    ? new List<int>(selected.spawnersPerNight)
                    : new List<int>();

                EditorUtility.SetDirty(wave);
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

        MapLoader.PendingConfig = selected;

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
