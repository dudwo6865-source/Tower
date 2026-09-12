using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// EnemySpawner 전용 인스펙터입니다.
// 인스펙터의 숫자들은 '기준값'이고, 실제로 쓰이는 값은 웨이브 보정을 곱한 뒤의
// Effective~ 프로퍼티라 직렬화되지 않습니다. 그래서 플레이 중에 인스펙터만 보면
// 웨이브 수치가 적용됐는지 알 수가 없습니다. 여기서 그 실효값을 그대로 보여주고,
// 씬의 모든 스포너가 같은 웨이브 수치를 받았는지도 한 번에 확인합니다.
[CustomEditor(typeof(EnemySpawner))]
[CanEditMultipleObjects]
public class EnemySpawnerEditor : Editor
{
    const int WavePreviewCount = 5;

    static readonly Color SectionColor = new Color(0.85f, 0.35f, 0.35f, 1f);

    // 플레이 중에는 남은 시간/생존 수가 계속 바뀌므로 매 프레임 다시 그린다.
    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        DrawSectionHeader("웨이브 적용 현황");

        EnemySpawner spawner = (EnemySpawner)target;

        if (Application.isPlaying)
            DrawRuntimeSection(spawner);
        else
            DrawEditModeSection(spawner);
    }

    // ── 플레이 중 ────────────────────────────────────────────────

    void DrawRuntimeSection(EnemySpawner spawner)
    {
        WaveManager waveManager = WaveManager.Instance;

        if (waveManager == null)
        {
            EditorGUILayout.HelpBox(
                "씬에 WaveManager가 없습니다. 이 스포너는 인스펙터 기준값 그대로 동작합니다.",
                MessageType.Warning);
        }

        WaveTuning tuning = spawner.AppliedTuning;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField(
            "적용된 웨이브",
            waveManager != null
                ? $"웨이브 {spawner.AppliedWaveNumber} ({(waveManager.IsNight ? "밤" : "낮")})"
                : $"웨이브 {spawner.AppliedWaveNumber}");

        EditorGUILayout.LabelField("적용된 보정", tuning != null ? tuning.ToShortSummary() : "-");

        EditorGUILayout.Space(4);

        DrawEffectiveRow(
            "한 번에 스폰",
            spawner.BaseEnemiesPerSpawn.ToString(),
            spawner.EffectiveEnemiesPerSpawn.ToString());

        DrawEffectiveRow(
            "스폰 간격(초)",
            $"{spawner.BaseSpawnInterval:0.##}",
            $"{spawner.EffectiveSpawnInterval:0.##}");

        DrawEffectiveRow(
            "동시 생존 상한",
            FormatMaxAlive(spawner.BaseMaxAliveEnemies),
            FormatMaxAlive(spawner.EffectiveMaxAliveEnemies));

        DrawEffectiveRow(
            "파괴 시 방출",
            spawner.enemiesOnDeath.ToString(),
            spawner.EffectiveEnemiesOnDeath.ToString());

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField(
            "현재 생존",
            spawner.EffectiveMaxAliveEnemies > 0
                ? $"{spawner.AliveCount} / {spawner.EffectiveMaxAliveEnemies}"
                : $"{spawner.AliveCount} (상한 없음)");

        EditorGUILayout.LabelField("다음 스폰까지", $"{Mathf.Max(0f, spawner.SpawnCountdown):0.0}초");
        EditorGUILayout.LabelField("상태", DescribeState(spawner));

        EditorGUILayout.EndVertical();

        DrawRuntimeWarnings(spawner);

        EditorGUILayout.Space(6);
        DrawAllSpawnersTable(spawner);

        if (waveManager == null)
            return;

        EditorGUILayout.Space(4);

        if (GUILayout.Button($"다음 웨이브로 진행 (현재 {waveManager.CurrentWaveNumber} → {waveManager.CurrentWaveNumber + 1})"))
            waveManager.SetWave(waveManager.CurrentWaveNumber + 1);
    }

    static string DescribeState(EnemySpawner spawner)
    {
        if (spawner.IsDead)
            return "파괴됨 (스폰 정지)";

        if (!spawner.IsAwakeForCurrentWave)
            return $"대기 중 — 웨이브 {Mathf.Max(1, spawner.activateFromWave)}부터 활동";

        if (spawner.IsBlockedByAliveCap)
            return "생존 상한에 걸려 정지 (적이 죽어야 재개)";

        if (spawner.enemyPrefabs == null || spawner.enemyPrefabs.Count == 0)
            return "스폰할 적 프리팹이 없음";

        return "스폰 중";
    }

    static void DrawRuntimeWarnings(EnemySpawner spawner)
    {
        if (spawner.enemyPrefabs == null || spawner.enemyPrefabs.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Enemy Prefabs가 비어 있어 아무것도 스폰하지 않습니다.",
                MessageType.Error);
        }

        if (spawner.BaseMaxAliveEnemies > 1 && spawner.EffectiveMaxAliveEnemies == 1)
        {
            EditorGUILayout.HelpBox(
                $"동시 생존 상한이 1로 줄었습니다. 웨이브의 Max Alive Multiplier가 0에 가깝습니다.\n" +
                $"(기준 {spawner.BaseMaxAliveEnemies} × 배율 {spawner.AppliedTuning.maxAliveMultiplier:0.##} → 1)\n" +
                "배율 0은 '무제한'이 아니라 '최소 1마리'입니다. 무제한으로 두려면 스포너의 Max Alive Enemies를 0으로 설정하세요.",
                MessageType.Warning);
        }

        if (spawner.EffectiveEnemiesPerSpawn == 0)
        {
            EditorGUILayout.HelpBox(
                "한 번에 스폰할 수가 0이라 스폰되지 않습니다. Spawn Count Multiplier / Bonus를 확인하세요.",
                MessageType.Warning);
        }

        if (spawner.IsBlockedByAliveCap && spawner.EffectiveEnemiesPerSpawn > spawner.EffectiveMaxAliveEnemies)
        {
            EditorGUILayout.HelpBox(
                $"한 번에 {spawner.EffectiveEnemiesPerSpawn}마리를 스폰하려 하지만 상한이 " +
                $"{spawner.EffectiveMaxAliveEnemies}마리라 그만큼만 나옵니다.",
                MessageType.Info);
        }
    }

    // ── 씬의 모든 스포너 비교 ─────────────────────────────────────

    void DrawAllSpawnersTable(EnemySpawner selected)
    {
        List<EnemySpawner> spawners = CollectSpawners();

        EditorGUILayout.LabelField($"씬의 스포너 ({spawners.Count}개)", EditorStyles.boldLabel);

        if (spawners.Count == 0)
        {
            EditorGUILayout.HelpBox("활성화된 스포너가 없습니다.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        DrawTableRow("이름", "스폰", "간격", "상한", "생존", "웨이브", header: true);

        bool allSame = true;

        for (int i = 0; i < spawners.Count; i++)
        {
            EnemySpawner spawner = spawners[i];

            if (spawner == null)
                continue;

            bool matches = Matches(selected, spawner);

            if (!matches)
                allSame = false;

            bool isSelected = spawner == selected;

            DrawTableRow(
                isSelected ? $"▶ {spawner.name}" : spawner.name,
                spawner.EffectiveEnemiesPerSpawn.ToString(),
                $"{spawner.EffectiveSpawnInterval:0.#}",
                FormatMaxAlive(spawner.EffectiveMaxAliveEnemies),
                spawner.AliveCount.ToString(),
                spawner.AppliedWaveNumber.ToString(),
                header: isSelected,
                warn: !matches);
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.HelpBox(
            allSame
                ? $"스포너 {spawners.Count}개 모두 같은 웨이브 보정을 받았습니다.\n" +
                  "(표의 숫자가 서로 다르면 스포너의 기준값이 다른 것입니다.)"
                : "일부 스포너가 다른 웨이브 보정을 받았습니다. 위 표의 빨간 줄을 확인하세요.",
            allSame ? MessageType.Info : MessageType.Warning);

        if (GUILayout.Button("모든 스포너에 현재 웨이브 수치 다시 적용"))
            WaveManager.Instance?.RefreshTuning(true);
    }

    // 스포너마다 기준값이 다를 수 있으므로 실효값이 아니라 '받은 보정'을 비교한다.
    // 같은 웨이브 수치를 받았는지가 확인 대상이다.
    static bool Matches(EnemySpawner a, EnemySpawner b)
    {
        if (a == null || b == null)
            return true;

        if (a.AppliedWaveNumber != b.AppliedWaveNumber)
            return false;

        WaveTuning x = a.AppliedTuning;
        WaveTuning y = b.AppliedTuning;

        if (x == null || y == null)
            return x == y;

        return Mathf.Approximately(x.spawnCountMultiplier, y.spawnCountMultiplier) &&
               x.spawnCountBonus == y.spawnCountBonus &&
               Mathf.Approximately(x.spawnIntervalMultiplier, y.spawnIntervalMultiplier) &&
               Mathf.Approximately(x.maxAliveMultiplier, y.maxAliveMultiplier) &&
               Mathf.Approximately(x.healthMultiplier, y.healthMultiplier) &&
               Mathf.Approximately(x.damageMultiplier, y.damageMultiplier) &&
               Mathf.Approximately(x.speedMultiplier, y.speedMultiplier);
    }

    static List<EnemySpawner> CollectSpawners()
    {
        List<EnemySpawner> result = new List<EnemySpawner>();

        if (Application.isPlaying)
        {
            IReadOnlyList<EnemySpawner> active = EnemySpawner.Active;

            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null)
                    result.Add(active[i]);
            }

            return result;
        }

        result.AddRange(
            FindObjectsByType<EnemySpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

        return result;
    }

    // ── 에디터 모드(플레이 전) 미리보기 ───────────────────────────

    void DrawEditModeSection(EnemySpawner spawner)
    {
        WaveManager waveManager = FindFirstObjectByType<WaveManager>();

        if (waveManager == null)
        {
            EditorGUILayout.HelpBox(
                "씬에 WaveManager가 없습니다. 이 스포너는 위 기준값 그대로 동작합니다.\n" +
                "스테이지 에셋(MapConfig)의 웨이브 값을 쓰려면 씬에 MapLoader와 WaveManager가 모두 있어야 합니다.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "위 숫자는 '기준값'입니다. 실제 값은 웨이브 보정을 곱한 뒤 결정되며 인스펙터에는 표시되지 않습니다.\n" +
            "플레이를 시작하면 여기에 실제 적용된 값이 나옵니다.",
            MessageType.Info);

        if (FindFirstObjectByType<MapLoader>() != null)
        {
            EditorGUILayout.HelpBox(
                "씬에 MapLoader가 있어 플레이 시 스테이지 에셋(MapConfig)의 웨이브 값으로 덮어써질 수 있습니다.\n" +
                "아래 미리보기는 씬 WaveManager에 들어 있는 값 기준입니다.",
                MessageType.Info);
        }

        EditorGUILayout.LabelField(
            $"예상값 미리보기 (웨이브 1~{WavePreviewCount}, 낮 기준)",
            EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        DrawTableRow("웨이브", "스폰", "간격", "상한", "", "", header: true);

        for (int wave = 1; wave <= WavePreviewCount; wave++)
        {
            WaveTuning tuning = waveManager.BuildTuningForWave(wave, false);

            DrawTableRow(
                $"웨이브 {wave}",
                EnemySpawner.GetEffectiveSpawnCount(spawner.enemiesPerSpawn, tuning).ToString(),
                $"{EnemySpawner.GetEffectiveSpawnInterval(spawner.spawnInterval, tuning):0.#}",
                FormatMaxAlive(EnemySpawner.GetEffectiveMaxAlive(spawner.maxAliveEnemies, tuning)),
                "",
                "",
                header: false);
        }

        EditorGUILayout.EndVertical();

        if (spawner.activateFromWave > 1)
        {
            EditorGUILayout.HelpBox(
                $"이 스포너는 웨이브 {spawner.activateFromWave}부터 활동합니다. 그 전까지는 스폰하지 않습니다.",
                MessageType.Info);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(
            $"씬의 스포너 수: {CollectSpawners().Count}개 (웨이브 수치는 전부에 똑같이 적용됩니다)",
            EditorStyles.miniLabel);
    }

    // ── 그리기 유틸 ──────────────────────────────────────────────

    static string FormatMaxAlive(int value)
    {
        return value <= 0 ? "무제한" : value.ToString();
    }

    static void DrawEffectiveRow(string label, string baseValue, string effectiveValue)
    {
        bool changed = baseValue != effectiveValue;

        Color previous = GUI.contentColor;

        if (changed)
            GUI.contentColor = new Color(0.45f, 0.9f, 0.45f, 1f);

        EditorGUILayout.LabelField(
            label,
            changed ? $"{baseValue}  →  {effectiveValue}" : $"{effectiveValue}  (보정 없음)");

        GUI.contentColor = previous;
    }

    static void DrawTableRow(
        string c0,
        string c1,
        string c2,
        string c3,
        string c4,
        string c5,
        bool header,
        bool warn = false)
    {
        GUIStyle style = header ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel;

        Color previous = GUI.contentColor;

        if (warn)
            GUI.contentColor = new Color(0.95f, 0.5f, 0.4f, 1f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(c0, style, GUILayout.MinWidth(90));
        EditorGUILayout.LabelField(c1, style, GUILayout.Width(50));
        EditorGUILayout.LabelField(c2, style, GUILayout.Width(50));
        EditorGUILayout.LabelField(c3, style, GUILayout.Width(55));
        EditorGUILayout.LabelField(c4, style, GUILayout.Width(45));
        EditorGUILayout.LabelField(c5, style, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        GUI.contentColor = previous;
    }

    static void DrawSectionHeader(string title)
    {
        Rect rect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, SectionColor);

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = Color.white;

        Rect labelRect = new Rect(rect.x + 6f, rect.y + 2f, rect.width - 8f, rect.height);
        EditorGUI.LabelField(labelRect, title, style);
    }
}
