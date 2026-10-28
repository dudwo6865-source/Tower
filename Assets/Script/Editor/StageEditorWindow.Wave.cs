using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 스테이지 에디터의 웨이브 섹션입니다. (StageEditorWindow.cs의 나머지 부분)
// - 상단 요약 칩: 스테이지 핵심 수치와 점검 결과를 한 줄로 보여준다.
// - 설정 점검: 적 구성이 빠진 웨이브, 밤 공격대이 꺼진 스포너 같은 실수를 모아 보여준다.
// - 웨이브 구성 표: 줄 = 웨이브, 칸 = 적 종류. 숫자만 고치면 되고, 옆에 색 막대로 총량을 그린다.
// - 웨이브별 상세: 배율·스탯 가중치를 '웨이브 N' 이름으로 펼쳐 편집하고, 복제/이동/삭제한다.
// - 웨이브 N부터 플레이: 후반 웨이브를 바로 확인한다.
public partial class StageEditorWindow
{
    // 적 종류별 색. 어두운/밝은 에디터 모두에서 서로 구분되도록 채도와 밝기를 맞췄다.
    static readonly Color[] EnemyTypePalette =
    {
        new Color(0.91f, 0.45f, 0.38f), // 코랄
        new Color(0.38f, 0.66f, 0.92f), // 파랑
        new Color(0.96f, 0.74f, 0.31f), // 앰버
        new Color(0.45f, 0.78f, 0.50f), // 초록
        new Color(0.72f, 0.54f, 0.90f), // 보라
        new Color(0.33f, 0.79f, 0.76f), // 청록
        new Color(0.92f, 0.55f, 0.76f), // 분홍
        new Color(0.68f, 0.72f, 0.43f), // 올리브
    };

    static readonly Color ChipNeutralColor = new Color(0.32f, 0.36f, 0.42f);
    static readonly Color ChipGoodColor = new Color(0.25f, 0.55f, 0.35f);
    static readonly Color ChipWarnColor = new Color(0.72f, 0.45f, 0.12f);
    static readonly Color ChipErrorColor = new Color(0.70f, 0.25f, 0.22f);
    static readonly Color EmptyWaveTextColor = new Color(0.95f, 0.65f, 0.25f);

    const float GridRowHeight = 20f;
    const float GridBlockHeaderHeight = 22f;
    const float GridIndent = 14f;
    const float GridTypeNameWidth = 150f;
    const float GridCountFieldWidth = 56f;
    const float GridShareLabelWidth = 100f;
    const float GridActionColumnWidth = 92f;
    const float GridChipHeight = 20f;
    const float GridChipGap = 6f;

    // 표에서 누른 버튼(웨이브 추가·삭제·이동 등)은 GUI를 그리는 도중에 바로 고치지 않고
    // 여기에 모아 두었다가 ApplyModifiedProperties 뒤에 한 번에 적용한다.
    // SerializedObject가 들고 있는 값과 엇갈려 수정이 덮어써지는 일을 막는다.
    readonly List<Action> pendingStageEdits = new List<Action>();

    bool foldWaveDetail = true;
    bool foldWaveExtra;
    int playStartWave = 1;

    enum WaveIssueLevel
    {
        Info,
        Warning,
        Error
    }

    struct WaveIssue
    {
        public WaveIssueLevel level;
        public string text;

        public WaveIssue(WaveIssueLevel level, string text)
        {
            this.level = level;
            this.text = text;
        }
    }

    // ── 공통 ─────────────────────────────────────────────────────

    void QueueStageEdit(Action edit)
    {
        pendingStageEdits.Add(edit);
    }

    void RunPendingStageEdits()
    {
        if (pendingStageEdits.Count == 0 || selected == null)
        {
            pendingStageEdits.Clear();
            return;
        }

        Undo.RecordObject(selected, "스테이지 웨이브 편집");

        if (selected.wavePlan == null)
            selected.wavePlan = new WavePlan();

        if (selected.wavePlan.waves == null)
            selected.wavePlan.waves = new List<WaveTuning>();

        foreach (Action edit in pendingStageEdits)
            edit();

        pendingStageEdits.Clear();

        EditorUtility.SetDirty(selected);
        serializedObject.Update();
        Repaint();
    }

    List<WaveTuning> Waves => selected.wavePlan != null && selected.wavePlan.waves != null
        ? selected.wavePlan.waves
        : new List<WaveTuning>();

    static Color StripeColor(int index)
    {
        if (index % 2 == 0)
            return Color.clear;

        return EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.035f)
            : new Color(0f, 0f, 0f, 0.05f);
    }

    static Color HeaderBackgroundColor =>
        EditorGUIUtility.isProSkin
            ? new Color(0f, 0f, 0f, 0.25f)
            : new Color(0f, 0f, 0f, 0.08f);

    static Color BarTrackColor =>
        EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.06f)
            : new Color(0f, 0f, 0f, 0.07f);

    static Color EnemyTypeColor(int index)
    {
        return EnemyTypePalette[Mathf.Abs(index) % EnemyTypePalette.Length];
    }

    // 섹션 안의 소제목. 왼쪽에 섹션 색 막대를 붙여 어느 섹션에 속하는지 눈에 띄게 한다.
    static void DrawSubHeader(string title, string hint, Color accent)
    {
        EditorGUILayout.Space(6);

        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.boldLabel, GUILayout.Height(20));
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2, 3, rect.height - 4), accent);

        Rect labelRect = new Rect(rect.x + 8, rect.y, rect.width - 8, rect.height);
        GUIContent titleContent = new GUIContent(title);
        float titleWidth = EditorStyles.boldLabel.CalcSize(titleContent).x;

        GUI.Label(labelRect, titleContent, EditorStyles.boldLabel);

        if (!string.IsNullOrEmpty(hint))
        {
            Rect hintRect = new Rect(labelRect.x + titleWidth + 8, rect.y + 1, labelRect.width - titleWidth - 8, rect.height);
            GUI.Label(hintRect, hint, EditorStyles.miniLabel);
        }
    }

    float FullDetailWidth => GetSectionColumnWidth() * 2f + SectionColumnGap;

    // ── 상단 요약 칩 ──────────────────────────────────────────────

    void DrawStageSummaryChips()
    {
        List<GUIContent> labels = new List<GUIContent>();
        List<Color> colors = new List<Color>();

        void Add(string text, Color color, string tooltip = "")
        {
            labels.Add(new GUIContent(text, tooltip));
            colors.Add(color);
        }

        List<WaveTuning> waves = Waves;
        int compositionWaves = 0;

        foreach (WaveTuning wave in waves)
        {
            if (wave != null && wave.HasEnemyComposition)
                compositionWaves++;
        }

        if (selected.overrideWave)
        {
            Add($"웨이브 표 {waves.Count}개", WaveColor, "웨이브 표에 직접 적은 웨이브 수입니다.");

            if (compositionWaves > 0)
                Add($"적 구성 {compositionWaves}/{waves.Count}웨이브", WaveColor, "적 구성(웨이브 총량)을 채운 웨이브 수입니다.");
        }
        else
        {
            Add("웨이브: 씬 값 사용", ChipNeutralColor);
        }

        if (selected.overrideDayNight)
        {
            Add(
                $"한 사이클 {selected.dayDuration + selected.nightDuration:0.#}초",
                DayNightColor,
                $"낮 {selected.dayDuration:0.#}초 + 밤 {selected.nightDuration:0.#}초");
        }

        if (selected.overrideWinCondition)
        {
            List<string> win = new List<string>();

            if (selected.winBySurvivingNights)
                win.Add($"{selected.survivalNightsToWin}번째 밤 생존");

            if (selected.winWhenAllSpawnersDestroyed)
                win.Add("스포너 전멸");

            if (selected.winByCapturingPoint)
                win.Add($"거점 점령 {selected.captureDuration:0.#}초");

            Add(win.Count > 0 ? "승리: " + string.Join(" / ", win) : "승리 조건 없음",
                win.Count > 0 ? WinConditionColor : ChipErrorColor);
        }

        Add($"스포너 {GetSpawners().Count}개", ChipNeutralColor, spawnerSource);

        List<WaveIssue> issues = CollectWaveIssues();
        int problems = 0;
        bool hasError = false;

        foreach (WaveIssue issue in issues)
        {
            if (issue.level == WaveIssueLevel.Info)
                continue;

            problems++;
            hasError |= issue.level == WaveIssueLevel.Error;
        }

        if (problems > 0)
            Add($"점검 {problems}건", hasError ? ChipErrorColor : ChipWarnColor, "웨이브 섹션의 '설정 점검'을 확인하세요.");
        else
            Add("점검 통과", ChipGoodColor);

        DrawChipRow(labels, colors);
        EditorGUILayout.Space(4);
    }

    void DrawChipRow(List<GUIContent> labels, List<Color> colors)
    {
        GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            padding = new RectOffset(8, 8, 0, 0)
        };

        float maxWidth = position.width - 16f;
        float lineWidth = 0f;

        EditorGUILayout.BeginHorizontal();

        for (int i = 0; i < labels.Count; i++)
        {
            float width = style.CalcSize(labels[i]).x + 4f;

            // 창이 좁으면 다음 줄로 넘긴다.
            if (lineWidth > 0f && lineWidth + width > maxWidth)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                lineWidth = 0f;
            }

            Rect rect = GUILayoutUtility.GetRect(labels[i], style, GUILayout.Width(width), GUILayout.Height(20));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1, rect.width, rect.height - 2), colors[i]);
            GUI.Label(rect, labels[i], style);

            GUILayout.Space(4);
            lineWidth += width + 4f;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    // ── 웨이브 섹션 ──────────────────────────────────────────────

    void DrawWaveSection()
    {
        foldWave = DrawSectionFoldout("Wave (적 웨이브)", WaveColor, foldWave);
        if (!foldWave)
            return;

        DrawOverrideToggle("overrideWave", "이 스테이지 값으로 WaveManager 덮어쓰기");

        EditorGUILayout.HelpBox(
            "스포너(EnemySpawner)는 맵 프리팹에 미리 배치하고, 여기서는 웨이브마다 그 스포너들이 낼 적을 정합니다.\n" +
            "웨이브 구성 표에 숫자를 적으면 그 웨이브 동안 스포너 하나당 그 수만큼만 나옵니다. " +
            "비워 둔 웨이브는 스포너 자체 프리팹 목록과 밤 총 출격 수를 사용합니다.",
            MessageType.Info);

        if (!selected.overrideWave)
        {
            EditorGUILayout.Space(6);
            return;
        }

        DrawWavePlayBar();
        DrawWaveIssues();
        DrawWaveCompositionGrid();
        DrawWaveDetailList();
        DrawWaveExtraSettings();

        DrawSubHeader("미리보기", "웨이브 = 하루(낮+밤) · N일차 밤에 공격대가 출격합니다", WaveColor);
        DrawSectionRow(DrawWavePreview, DrawSpawnRateSection);

        EditorGUILayout.Space(6);
    }

    // ── 웨이브 N부터 플레이 ───────────────────────────────────────

    void DrawWavePlayBar()
    {
        DrawSubHeader("테스트 플레이", "후반 웨이브를 바로 확인합니다", WaveColor);

        EditorGUILayout.BeginHorizontal();

        if (EditorApplication.isPlaying)
        {
            WaveManager manager = WaveManager.Instance;

            if (manager != null)
            {
                EditorGUILayout.LabelField(
                    $"지금 웨이브 {manager.CurrentWaveNumber} ({(manager.IsNight ? "밤" : "낮")})",
                    EditorStyles.boldLabel,
                    GUILayout.Width(150));

                playStartWave = Mathf.Max(1, EditorGUILayout.IntField(playStartWave, GUILayout.Width(50)));

                if (GUILayout.Button($"웨이브 {playStartWave}(으)로 이동", GUILayout.Width(150)))
                    manager.SetWave(playStartWave);

                if (GUILayout.Button("다음 웨이브", GUILayout.Width(90)))
                    manager.SetWave(manager.CurrentWaveNumber + 1);
            }
            else
            {
                EditorGUILayout.LabelField("플레이 중이지만 씬에 WaveManager가 없습니다.", EditorStyles.miniLabel);
            }
        }
        else
        {
            EditorGUILayout.LabelField("시작 웨이브", GUILayout.Width(70));
            playStartWave = Mathf.Max(1, EditorGUILayout.IntField(playStartWave, GUILayout.Width(50)));

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);

            if (GUILayout.Button($"▶  웨이브 {playStartWave}부터 플레이", GUILayout.Width(180), GUILayout.Height(20)))
                PlayThisStage(playStartWave);

            GUI.backgroundColor = previous;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "웨이브 번호만 건너뜁니다. 낮/밤은 첫 낮부터 시작하고, '생존 승리'의 밤 수도 처음부터 셉니다.",
            EditorStyles.miniLabel);
    }

    // ── 설정 점검 ─────────────────────────────────────────────────

    List<WaveIssue> CollectWaveIssues()
    {
        List<WaveIssue> issues = new List<WaveIssue>();

        if (selected == null || !selected.overrideWave)
            return issues;

        List<WaveTuning> waves = Waves;

        if (waves.Count == 0)
        {
            issues.Add(new WaveIssue(
                WaveIssueLevel.Warning,
                "웨이브 표가 비어 있습니다. 모든 웨이브가 기본값(배율 1)과 스포너 자체 목록으로 진행됩니다."));
            return issues;
        }

        List<int> emptyWaves = new List<int>();
        List<int> zeroSpawnWaves = new List<int>();
        List<int> blankPrefabWaves = new List<int>();
        bool anyComposition = false;

        for (int i = 0; i < waves.Count; i++)
        {
            WaveTuning wave = waves[i];
            if (wave == null)
                continue;

            if (wave.HasEnemyComposition)
            {
                anyComposition = true;

                if (wave.spawnCountMultiplier <= 0f && wave.spawnCountBonus <= 0)
                    zeroSpawnWaves.Add(i + 1);
            }
            else
            {
                emptyWaves.Add(i + 1);
            }

            List<WaveEnemyEntry> entries = wave.enemyComposition != null ? wave.enemyComposition.entries : null;

            if (entries == null)
                continue;

            foreach (WaveEnemyEntry entry in entries)
            {
                if (entry != null && entry.prefab == null && entry.count > 0)
                {
                    blankPrefabWaves.Add(i + 1);
                    break;
                }
            }
        }

        List<SpawnerInfo> spawners = GetSpawners();

        if (anyComposition && emptyWaves.Count > 0)
        {
            issues.Add(new WaveIssue(
                WaveIssueLevel.Warning,
                $"{FormatWaveList(emptyWaves)}은(는) 적 구성이 비어 있어 스포너 자체 프리팹 목록으로 " +
                "스포너의 밤 총 출격 수를 사용합니다. 적 종류별 총량을 정하려면 숫자를 채우세요."));
        }

        if (zeroSpawnWaves.Count > 0)
        {
            issues.Add(new WaveIssue(
                WaveIssueLevel.Error,
                $"{FormatWaveList(zeroSpawnWaves)}은(는) '스폰 수 배율'이 0이라 적 구성을 채워도 한 마리도 나오지 않습니다."));
        }

        if (blankPrefabWaves.Count > 0)
        {
            issues.Add(new WaveIssue(
                WaveIssueLevel.Warning,
                $"{FormatWaveList(blankPrefabWaves)}에 프리팹이 비어 있는 적 구성 줄이 있습니다. 그 줄은 무시됩니다."));
        }

        if (spawners.Count == 0)
        {
            issues.Add(new WaveIssue(
                WaveIssueLevel.Warning,
                "맵 프리팹과 열려 있는 씬에서 EnemySpawner나 스포너를 짓는 건설 마커(StageBuildingSpawnPoint)를 찾지 못했습니다. " +
                "웨이브 설정이 적용될 스포너가 없습니다."));
        }
        else
        {
            List<string> periodicOff = new List<string>();
            List<string> noPrefab = new List<string>();

            foreach (SpawnerInfo info in spawners)
            {
                if (anyComposition && !info.spawnPeriodically)
                    periodicOff.Add(info.name);

                if (emptyWaves.Count > 0 && !info.hasPrefab)
                    noPrefab.Add(info.name);
            }

            if (periodicOff.Count > 0)
            {
                issues.Add(new WaveIssue(
                    WaveIssueLevel.Error,
                    $"스포너 {string.Join(", ", periodicOff)}의 '밤 공격대'이 꺼져 있어 적 구성이 나오지 않습니다. " +
                    "스포너 인스펙터에서 '밤 공격대'을 켜세요."));
            }

            if (noPrefab.Count > 0)
            {
                issues.Add(new WaveIssue(
                    WaveIssueLevel.Warning,
                    $"스포너 {string.Join(", ", noPrefab)}의 적 프리팹 목록이 비어 있습니다. " +
                    "적 구성이 빈 웨이브에서는 이 스포너가 아무것도 내보내지 않습니다."));
            }
        }

        if (selected.overrideWinCondition &&
            selected.winBySurvivingNights &&
            selected.survivalNightsToWin > waves.Count)
        {
            issues.Add(new WaveIssue(
                WaveIssueLevel.Info,
                $"승리까지 {selected.survivalNightsToWin}번째 밤인데 웨이브 표는 {waves.Count}개입니다. " +
                $"{waves.Count + 1}일차부터는 마지막 웨이브 값에 '이후 웨이브 증가율'을 붙여 이어갑니다."));
        }

        // 증가율은 표 이후 매 일차마다 곱해지므로, 배율 0은 '유지'가 아니라 '0으로 만듦'이다.
        WaveTuning growth = selected.wavePlan != null ? selected.wavePlan.growthPerWaveAfterLast : null;

        if (growth != null && waves.Count > 0)
        {
            List<string> zeroNames = new List<string>();

            if (growth.spawnCountMultiplier <= 0f) zeroNames.Add("스폰 수 배율");
            if (growth.healthMultiplier <= 0f) zeroNames.Add("체력 배율");
            if (growth.damageMultiplier <= 0f) zeroNames.Add("공격력 배율");
            if (growth.speedMultiplier <= 0f) zeroNames.Add("이동 속도 배율");

            if (zeroNames.Count > 0)
            {
                issues.Add(new WaveIssue(
                    WaveIssueLevel.Warning,
                    $"'이후 웨이브 증가율'의 {string.Join(", ", zeroNames)}이 0입니다. 증가율은 매 일차 곱해지므로 " +
                    $"{waves.Count + 1}일차부터 그 값이 0이 됩니다(스폰 수는 '스폰 수 추가'만 남음). 그대로 유지하려면 1로 두세요."));
            }
        }

        return issues;
    }

    static string FormatWaveList(List<int> waveNumbers)
    {
        const int maxShown = 6;

        List<string> parts = new List<string>();

        for (int i = 0; i < waveNumbers.Count && i < maxShown; i++)
            parts.Add(waveNumbers[i].ToString());

        string text = string.Join(", ", parts) + "일차";

        if (waveNumbers.Count > maxShown)
            text += $" 외 {waveNumbers.Count - maxShown}개";

        return text;
    }

    void DrawWaveIssues()
    {
        List<WaveIssue> issues = CollectWaveIssues();

        DrawSubHeader("설정 점검", issues.Count == 0 ? "문제 없음" : $"{issues.Count}건", WaveColor);

        if (issues.Count == 0)
        {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label, GUILayout.Height(22));
            EditorGUI.DrawRect(rect, new Color(ChipGoodColor.r, ChipGoodColor.g, ChipGoodColor.b, 0.35f));
            GUI.Label(
                new Rect(rect.x + 8, rect.y, rect.width - 8, rect.height),
                "✓  웨이브 설정에서 문제를 찾지 못했습니다.",
                EditorStyles.boldLabel);
            return;
        }

        foreach (WaveIssue issue in issues)
        {
            MessageType type = issue.level == WaveIssueLevel.Error
                ? MessageType.Error
                : issue.level == WaveIssueLevel.Warning
                    ? MessageType.Warning
                    : MessageType.Info;

            EditorGUILayout.HelpBox(issue.text, type);
        }
    }

    // ── 웨이브 구성 표 ─────────────────────────────────────────────

    // 모든 웨이브의 적 구성에 한 번이라도 나온 프리팹을 처음 나온 순서대로 모은다. (= 표의 열)
    List<GameObject> CollectEnemyTypes()
    {
        List<GameObject> types = new List<GameObject>();

        foreach (WaveTuning wave in Waves)
        {
            if (wave == null || wave.enemyComposition == null || wave.enemyComposition.entries == null)
                continue;

            foreach (WaveEnemyEntry entry in wave.enemyComposition.entries)
            {
                if (entry != null && entry.prefab != null && !types.Contains(entry.prefab))
                    types.Add(entry.prefab);
            }
        }

        return types;
    }

    static int CountOf(WaveTuning wave, GameObject prefab, out int rowCount)
    {
        rowCount = 0;
        int total = 0;

        if (wave == null || wave.enemyComposition == null || wave.enemyComposition.entries == null)
            return 0;

        foreach (WaveEnemyEntry entry in wave.enemyComposition.entries)
        {
            if (entry == null || entry.prefab != prefab)
                continue;

            rowCount++;
            total += Mathf.Max(0, entry.count);
        }

        return total;
    }

    // 웨이브 표의 해당 웨이브에서 밤 공격대으로 적 구성을 내보낼 스포너 수.
    int CountCompositionSpawners(int waveNumber)
    {
        int count = 0;

        foreach (SpawnerInfo info in GetSpawners())
        {
            if (info.spawnPeriodically && waveNumber >= info.activateFromWave)
                count++;
        }

        return count;
    }

    // 웨이브 구성 표. 일차마다 머리줄(일차·합계·맵 전체·편집) 아래에 적 종류별로 한 줄씩 그린다.
    // 줄마다 자기 막대를 따로 그려, 한 막대에 색을 이어 붙였을 때보다 종류별 양을 읽기 쉽게 한다.
    void DrawWaveCompositionGrid()
    {
        DrawSubHeader("웨이브 구성 표", "일차마다 적 종류별로 한 줄씩 · 숫자 = 스포너 하나가 그날 밤 낼 마리 수", WaveColor);

        List<WaveTuning> waves = Waves;
        List<GameObject> types = CollectEnemyTypes();
        float width = FullDetailWidth - 6f;

        DrawEnemyTypeLegend(types, width);

        // 막대 길이 기준: 모든 일차·종류를 통틀어 가장 많은 마리 수. 일차끼리도 길이를 비교할 수 있다.
        int maxCount = 1;

        foreach (WaveTuning wave in waves)
        {
            for (int t = 0; t < types.Count; t++)
                maxCount = Mathf.Max(maxCount, CountOf(wave, types[t], out _));
        }

        if (waves.Count == 0)
        {
            Rect emptyRect = GUILayoutUtility.GetRect(width, GridRowHeight, GUILayout.Width(width));
            GUI.Label(emptyRect, "웨이브가 없습니다. 아래 '+ 웨이브 추가'를 누르세요.", EditorStyles.miniLabel);
        }

        for (int i = 0; i < waves.Count; i++)
            DrawWaveBlock(i, waves[i], types, maxCount, width);

        // 표 이후 웨이브 안내 줄
        if (waves.Count > 0)
        {
            EditorGUILayout.Space(4);
            WaveTuning last = waves[waves.Count - 1];
            string lastComposition = last != null && last.HasEnemyComposition
                ? $"마지막 웨이브 구성({last.enemyComposition.ToSummary()})을 그대로 씁니다."
                : "스포너 자체 목록과 밤 총 출격 수를 사용합니다.";

            Rect afterRect = GUILayoutUtility.GetRect(width, GridRowHeight, GUILayout.Width(width));
            GUI.Label(afterRect, $"{DayLabel(waves.Count + 1)}~  ·  {lastComposition}", EditorStyles.miniLabel);
        }

        DrawGridFooter(types);
    }

    // 표에 쓰인 적 종류 목록. 색 칩마다 이름(누르면 프리팹 찾기)과 표에서 빼기 버튼이 있다.
    void DrawEnemyTypeLegend(List<GameObject> types, float width)
    {
        if (types.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "표에 적 종류가 없습니다. 아래 '적 종류 추가'에 적 프리팹을 끌어다 놓으세요.",
                MessageType.Info);
            return;
        }

        GUIStyle nameStyle = new GUIStyle(EditorStyles.miniBoldLabel) { clipping = TextClipping.Clip };
        const float swatchSpace = 16f;
        const float removeWidth = 18f;

        // 칩 너비를 먼저 재서 줄바꿈 위치를 정한다.
        float[] chipWidths = new float[types.Count];
        int lineCount = 1;
        float lineX = 0f;

        for (int t = 0; t < types.Count; t++)
        {
            float nameWidth = Mathf.Min(160f, nameStyle.CalcSize(new GUIContent(types[t].name)).x);
            chipWidths[t] = swatchSpace + nameWidth + removeWidth + 8f;

            if (lineX > 0f && lineX + chipWidths[t] > width)
            {
                lineCount++;
                lineX = 0f;
            }

            lineX += chipWidths[t] + GridChipGap;
        }

        float height = lineCount * (GridChipHeight + 4f);
        Rect area = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width));

        float x = area.x;
        float y = area.y;

        for (int t = 0; t < types.Count; t++)
        {
            GameObject type = types[t];
            float chipWidth = chipWidths[t];

            if (x > area.x && x + chipWidth > area.xMax)
            {
                x = area.x;
                y += GridChipHeight + 4f;
            }

            Rect chip = new Rect(x, y, chipWidth, GridChipHeight);
            Color color = EnemyTypeColor(t);

            EditorGUI.DrawRect(chip, new Color(color.r, color.g, color.b, 0.18f));
            EditorGUI.DrawRect(new Rect(chip.x, chip.y, 3f, chip.height), color);
            EditorGUI.DrawRect(new Rect(chip.x + 6f, chip.y + 5f, 10f, 10f), color);

            Rect nameRect = new Rect(chip.x + 6f + swatchSpace, chip.y + 1f, chipWidth - swatchSpace - removeWidth - 8f, chip.height - 2f);

            if (GUI.Button(nameRect, new GUIContent(type.name, $"{type.name}\n누르면 Project 창에서 프리팹을 보여줍니다."), nameStyle))
                EditorGUIUtility.PingObject(type);

            Rect removeRect = new Rect(chip.xMax - removeWidth - 1f, chip.y + 2f, removeWidth, chip.height - 4f);

            if (GUI.Button(removeRect, new GUIContent("✕", $"모든 웨이브에서 '{type.name}'을(를) 뺍니다."), EditorStyles.miniButton))
            {
                GameObject captured = type;
                QueueStageEdit(() => RemoveEnemyType(captured));
            }

            x += chipWidth + GridChipGap;
        }

        EditorGUILayout.Space(2);
    }

    void DrawWaveBlock(int index, WaveTuning wave, List<GameObject> types, int maxCount, float width)
    {
        EditorGUILayout.Space(4);

        int total = wave != null && wave.enemyComposition != null ? wave.enemyComposition.TotalCount : 0;

        DrawWaveBlockHeader(index, total, width);

        for (int t = 0; t < types.Count; t++)
            DrawWaveTypeRow(index, wave, types[t], t, total, maxCount, width);
    }

    void DrawWaveBlockHeader(int index, int total, float width)
    {
        Rect header = GUILayoutUtility.GetRect(width, GridBlockHeaderHeight, GUILayout.Width(width));
        EditorGUI.DrawRect(header, HeaderBackgroundColor);
        EditorGUI.DrawRect(new Rect(header.x, header.y, 3f, header.height), WaveColor);

        Rect dayRect = new Rect(header.x + 10f, header.y, 70f, header.height);
        GUI.Label(dayRect, DayLabel(index + 1), EditorStyles.boldLabel);

        Rect summaryRect = new Rect(dayRect.xMax, header.y, header.width - dayRect.width - GridActionColumnWidth - 24f, header.height);

        if (total > 0)
        {
            int spawnerCount = CountCompositionSpawners(index + 1);
            int effectiveTotal = EnemySpawner.GetNightTotal(0, BuildTuning(index + 1, true));
            string mapText = spawnerCount > 0
                ? $"맵 전체 {effectiveTotal * spawnerCount}마리 (스포너 {spawnerCount}개)"
                : "활동 스포너 없음";

            GUI.Label(
                summaryRect,
                new GUIContent(
                    $"스포너당 {total}마리   ·   {mapText}",
                    "스포너당 = 표에 적은 합계\n맵 전체 = 웨이브·밤 배율을 적용한 공격대 총량 × 활동 스포너 수 (돌연변이 제외)"),
                EditorStyles.label);
        }
        else
        {
            GUIStyle emptyStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = EmptyWaveTextColor } };
            GUI.Label(summaryRect, "비어 있음 · 스포너의 밤 총 출격 수 사용", emptyStyle);
        }

        Rect copyRect = new Rect(header.xMax - GridActionColumnWidth - 4f, header.y + 2f, GridActionColumnWidth * 0.5f - 2f, header.height - 4f);
        Rect deleteRect = new Rect(copyRect.xMax + 4f, copyRect.y, copyRect.width, copyRect.height);

        if (GUI.Button(copyRect, new GUIContent("복제", "이 웨이브를 바로 아래에 복사합니다."), EditorStyles.miniButtonLeft))
        {
            int waveIndex = index;
            QueueStageEdit(() => DuplicateWave(waveIndex));
        }

        if (GUI.Button(deleteRect, new GUIContent("삭제", "이 웨이브를 표에서 지웁니다. (Ctrl+Z로 되돌리기)"), EditorStyles.miniButtonRight))
        {
            int waveIndex = index;
            QueueStageEdit(() => DeleteWave(waveIndex));
        }
    }

    // 적 종류 한 줄: [색] 이름 | 마리 수 입력 | 막대 | 마리 수·비율
    void DrawWaveTypeRow(int waveIndex, WaveTuning wave, GameObject type, int typeIndex, int total, int maxCount, float width)
    {
        Rect row = GUILayoutUtility.GetRect(width, GridRowHeight, GUILayout.Width(width));
        EditorGUI.DrawRect(row, StripeColor(typeIndex));

        Color color = EnemyTypeColor(typeIndex);
        int count = CountOf(wave, type, out int rowCount);
        bool empty = count <= 0;

        Color previous = GUI.color;
        if (empty)
            GUI.color = new Color(previous.r, previous.g, previous.b, 0.45f);

        float x = row.x + GridIndent;

        EditorGUI.DrawRect(new Rect(x, row.y + 3f, 4f, row.height - 6f), color);
        x += 10f;

        GUI.Label(
            new Rect(x, row.y, GridTypeNameWidth, row.height),
            new GUIContent(type.name, type.name),
            new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip });
        x += GridTypeNameWidth + 6f;

        Rect fieldRect = new Rect(x, row.y + 1f, GridCountFieldWidth, row.height - 2f);

        if (rowCount > 1)
        {
            // 같은 적을 여러 줄로 나눠 순서를 정한 웨이브는 표에서 합계만 보여준다.
            GUI.Label(
                fieldRect,
                new GUIContent($"Σ {count}", "여러 줄로 나눠 순서를 정한 적입니다. 아래 '웨이브별 상세'에서 편집하세요."),
                new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter });
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            int newCount = EditorGUI.IntField(
                fieldRect,
                count,
                new GUIStyle(EditorStyles.numberField) { alignment = TextAnchor.MiddleCenter });

            if (EditorGUI.EndChangeCheck())
            {
                int capturedWave = waveIndex;
                GameObject capturedType = type;
                int value = Mathf.Max(0, newCount);
                QueueStageEdit(() => SetEnemyCount(capturedWave, capturedType, value));
            }
        }

        x += GridCountFieldWidth + 10f;

        float barWidth = Mathf.Max(40f, row.xMax - GridShareLabelWidth - 8f - x);
        Rect track = new Rect(x, row.y + 5f, barWidth, row.height - 10f);
        EditorGUI.DrawRect(track, BarTrackColor);

        if (!empty)
            EditorGUI.DrawRect(new Rect(track.x, track.y, Mathf.Max(2f, track.width * count / maxCount), track.height), color);

        string share = empty
            ? "-"
            : $"{count}마리 · {Mathf.RoundToInt(100f * count / Mathf.Max(1, total))}%";

        GUI.Label(
            new Rect(track.xMax + 8f, row.y, GridShareLabelWidth, row.height),
            share,
            EditorStyles.miniBoldLabel);

        GUI.color = previous;
    }

    void DrawGridFooter(List<GameObject> types)
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                new GUIContent("+ 웨이브 추가", "마지막 웨이브를 복사해 맨 아래에 추가합니다."),
                GUILayout.Width(110)))
        {
            QueueStageEdit(AddWaveCopyOfLast);
        }

        GUILayout.Space(12);
        EditorGUILayout.LabelField(
            new GUIContent("적 종류 추가", "Project 창의 적 프리팹을 끌어다 놓으면 표에 열이 생깁니다."),
            GUILayout.Width(72));

        GameObject added = (GameObject)EditorGUILayout.ObjectField(null, typeof(GameObject), false, GUILayout.Width(180));

        if (added != null)
        {
            if (types.Contains(added))
                ShowNotification(new GUIContent($"'{added.name}'은(는) 이미 표에 있습니다."));
            else
                QueueStageEdit(() => AddEnemyType(added));
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (types.Count == 0)
        {
            EditorGUILayout.LabelField(
                "적 프리팹을 '적 종류 추가' 칸에 끌어다 놓으면 열이 생기고, 칸에 웨이브별 마리 수를 적을 수 있습니다.",
                EditorStyles.miniLabel);
        }
    }

    // ── 웨이브별 상세 (배율·스탯) ──────────────────────────────────

    void DrawWaveDetailList()
    {
        EditorGUILayout.Space(6);
        foldWaveDetail = EditorGUILayout.Foldout(
            foldWaveDetail,
            "웨이브별 상세 (스폰 배율·스탯 가중치·적 순서)",
            true,
            EditorStyles.foldoutHeader);

        if (!foldWaveDetail)
            return;

        SerializedProperty waves = serializedObject.FindProperty("wavePlan").FindPropertyRelative("waves");

        for (int i = 0; i < waves.arraySize; i++)
        {
            SerializedProperty element = waves.GetArrayElementAtIndex(i);
            WaveTuning tuning = i < Waves.Count ? Waves[i] : null;

            Rect header = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label, GUILayout.Height(22));
            EditorGUI.DrawRect(header, element.isExpanded ? HeaderBackgroundColor : StripeColor(i + 1));
            EditorGUI.DrawRect(new Rect(header.x, header.y, 3, header.height), WaveColor);

            const float buttonWidth = 38f;
            float buttonsWidth = buttonWidth * 4f + 6f;

            Rect foldRect = new Rect(header.x + 16, header.y + 2, 90, header.height - 4);
            element.isExpanded = EditorGUI.Foldout(foldRect, element.isExpanded, DayLabel(i + 1), true, EditorStyles.foldout);

            Rect summaryRect = new Rect(foldRect.xMax + 4, header.y + 3, header.width - foldRect.width - buttonsWidth - 30, header.height - 4);
            GUI.Label(
                summaryRect,
                new GUIContent(tuning != null ? tuning.ToShortSummary() : "", tuning != null ? tuning.ToShortSummary() : ""),
                new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip });

            float bx = header.xMax - buttonsWidth;
            int waveIndex = i;

            using (new EditorGUI.DisabledScope(i == 0))
            {
                if (GUI.Button(new Rect(bx, header.y + 3, buttonWidth, 16), new GUIContent("▲", "위로 이동"), EditorStyles.miniButtonLeft))
                    QueueStageEdit(() => MoveWave(waveIndex, -1));
            }

            bx += buttonWidth;

            using (new EditorGUI.DisabledScope(i == waves.arraySize - 1))
            {
                if (GUI.Button(new Rect(bx, header.y + 3, buttonWidth, 16), new GUIContent("▼", "아래로 이동"), EditorStyles.miniButtonMid))
                    QueueStageEdit(() => MoveWave(waveIndex, 1));
            }

            bx += buttonWidth;

            if (GUI.Button(new Rect(bx, header.y + 3, buttonWidth, 16), new GUIContent("복제", "바로 아래에 복사"), EditorStyles.miniButtonMid))
                QueueStageEdit(() => DuplicateWave(waveIndex));

            bx += buttonWidth;

            if (GUI.Button(new Rect(bx, header.y + 3, buttonWidth, 16), new GUIContent("삭제", "이 웨이브 삭제 (Ctrl+Z로 되돌리기)"), EditorStyles.miniButtonRight))
                QueueStageEdit(() => DeleteWave(waveIndex));

            if (!element.isExpanded)
                continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            SerializedProperty child = element.Copy();
            SerializedProperty end = element.GetEndProperty();
            bool enterChildren = true;

            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                EditorGUILayout.PropertyField(child, true);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button(
                new GUIContent("+ 웨이브 추가 (마지막 웨이브 복사)", "마지막 웨이브를 복사해 맨 아래에 추가합니다."),
                GUILayout.Width(220)))
        {
            QueueStageEdit(AddWaveCopyOfLast);
        }
    }

    void DrawWaveExtraSettings()
    {
        EditorGUILayout.Space(6);
        foldWaveExtra = EditorGUILayout.Foldout(
            foldWaveExtra,
            "표 이후 웨이브 증가율 · 밤 보정",
            true,
            EditorStyles.foldoutHeader);

        if (!foldWaveExtra)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        SerializedProperty plan = serializedObject.FindProperty("wavePlan");

        EditorGUILayout.PropertyField(
            plan.FindPropertyRelative("growthPerWaveAfterLast"),
            new GUIContent("표 이후 웨이브 증가율", "표에 없는 이후 웨이브에 매 웨이브마다 복리로 적용할 증가율입니다."),
            true);

        EditorGUILayout.Space(4);

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("applyNightBonus"),
            new GUIContent("밤에 추가 보정 적용"));

        using (new EditorGUI.DisabledScope(!selected.applyNightBonus))
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("nightBonus"),
                new GUIContent("밤 보정", "밤 동안 웨이브 수치에 한 번 더 곱할 보정입니다. 적 구성(총량)은 바꾸지 않습니다."),
                true);
        }

        EditorGUILayout.EndVertical();
    }

    // ── 웨이브 표 편집 (RunPendingStageEdits 안에서만 호출) ────────────

    void AddWaveCopyOfLast()
    {
        List<WaveTuning> waves = selected.wavePlan.waves;

        // 빈 표에 SerializedProperty로 칸을 늘리면 배율이 0으로 채워진다. 그래서 객체로 직접 만든다.
        waves.Add(waves.Count > 0 && waves[waves.Count - 1] != null
            ? new WaveTuning(waves[waves.Count - 1])
            : new WaveTuning());
    }

    void DuplicateWave(int index)
    {
        List<WaveTuning> waves = selected.wavePlan.waves;

        if (index < 0 || index >= waves.Count)
            return;

        waves.Insert(index + 1, new WaveTuning(waves[index]));
    }

    void DeleteWave(int index)
    {
        List<WaveTuning> waves = selected.wavePlan.waves;

        if (index < 0 || index >= waves.Count)
            return;

        waves.RemoveAt(index);
    }

    void MoveWave(int index, int direction)
    {
        List<WaveTuning> waves = selected.wavePlan.waves;
        int target = index + direction;

        if (index < 0 || index >= waves.Count || target < 0 || target >= waves.Count)
            return;

        (waves[index], waves[target]) = (waves[target], waves[index]);
    }

    void SetEnemyCount(int waveIndex, GameObject prefab, int count)
    {
        List<WaveTuning> waves = selected.wavePlan.waves;

        if (waveIndex < 0 || waveIndex >= waves.Count || prefab == null)
            return;

        WaveTuning wave = waves[waveIndex] ?? (waves[waveIndex] = new WaveTuning());

        if (wave.enemyComposition == null)
            wave.enemyComposition = new WaveEnemyComposition();

        List<WaveEnemyEntry> entries = wave.enemyComposition.entries;

        foreach (WaveEnemyEntry entry in entries)
        {
            if (entry != null && entry.prefab == prefab)
            {
                entry.count = count;
                return;
            }
        }

        // 0으로 둔 칸도 줄은 남겨 둔다. 지우면 그 웨이브에만 있던 열이 표에서 사라진다.
        entries.Add(new WaveEnemyEntry { prefab = prefab, count = count });
    }

    void AddEnemyType(GameObject prefab)
    {
        if (prefab == null)
            return;

        List<WaveTuning> waves = selected.wavePlan.waves;

        if (waves.Count == 0)
            waves.Add(new WaveTuning());

        // 열이 생기도록 첫 웨이브에 0마리 줄을 넣는다. 숫자는 표에서 채운다.
        SetEnemyCount(0, prefab, CountOf(waves[0], prefab, out _));
    }

    void RemoveEnemyType(GameObject prefab)
    {
        foreach (WaveTuning wave in selected.wavePlan.waves)
        {
            if (wave == null || wave.enemyComposition == null || wave.enemyComposition.entries == null)
                continue;

            wave.enemyComposition.entries.RemoveAll(entry => entry != null && entry.prefab == prefab);
        }
    }
}
