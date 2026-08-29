using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// UpgradeDefinition 에셋들을 편집·생성하는 에디터 툴입니다.
// Tools > Upgrades > Generate Upgrade Definitions
public class UpgradeDefinitionGenerator : EditorWindow
{
    // 생성할 업그레이드 한 개의 편집 가능한 설정입니다.
    class Preset
    {
        public bool enabled = true;
        public bool foldout = true;
        public string fileName;
        public UpgradeStat stat;
        public string displayName;
        public UpgradeValueMode valueMode;
        public float bonusPerLevel;
        public int maxLevel;
        public int baseCost;
        public int costPerLevel;
        public float researchDuration = 10f;
        public string description;

        public Preset Clone()
        {
            return new Preset
            {
                enabled = enabled,
                foldout = foldout,
                fileName = fileName,
                stat = stat,
                displayName = displayName,
                valueMode = valueMode,
                bonusPerLevel = bonusPerLevel,
                maxLevel = maxLevel,
                baseCost = baseCost,
                costPerLevel = costPerLevel,
                researchDuration = researchDuration,
                description = description,
            };
        }
    }

    // 기본 세트: 유닛 4종 + 건물 3종
    static Preset[] CreateDefaultPresets()
    {
        return new[]
        {
            new Preset
            {
                fileName = "Upgrade_Unit_AttackDamage",
                stat = UpgradeStat.UnitAttackDamage,
                displayName = "유닛 공격력",
                valueMode = UpgradeValueMode.Flat,
                bonusPerLevel = 3f,
                maxLevel = 5,
                baseCost = 40,
                costPerLevel = 30,
                description = "아군 유닛의 공격력을 강화합니다.",
            },
            new Preset
            {
                fileName = "Upgrade_Unit_AttackSpeed",
                stat = UpgradeStat.UnitAttackSpeed,
                displayName = "유닛 공격속도",
                valueMode = UpgradeValueMode.Percent,
                bonusPerLevel = 10f,
                maxLevel = 5,
                baseCost = 50,
                costPerLevel = 35,
                description = "아군 유닛의 공격속도를 강화합니다.",
            },
            new Preset
            {
                fileName = "Upgrade_Unit_MoveSpeed",
                stat = UpgradeStat.UnitMoveSpeed,
                displayName = "유닛 이동속도",
                valueMode = UpgradeValueMode.Flat,
                bonusPerLevel = 0.5f,
                maxLevel = 3,
                baseCost = 40,
                costPerLevel = 30,
                description = "아군 유닛의 이동속도를 강화합니다.",
            },
            new Preset
            {
                fileName = "Upgrade_Unit_MaxHealth",
                stat = UpgradeStat.UnitMaxHealth,
                displayName = "유닛 체력",
                valueMode = UpgradeValueMode.Percent,
                bonusPerLevel = 15f,
                maxLevel = 5,
                baseCost = 45,
                costPerLevel = 30,
                description = "아군 유닛의 최대 체력을 강화합니다.",
            },
            new Preset
            {
                fileName = "Upgrade_Building_AttackDamage",
                stat = UpgradeStat.BuildingAttackDamage,
                displayName = "건물 공격력",
                valueMode = UpgradeValueMode.Flat,
                bonusPerLevel = 5f,
                maxLevel = 5,
                baseCost = 60,
                costPerLevel = 40,
                description = "아군 건물(타워)의 공격력을 강화합니다.",
            },
            new Preset
            {
                fileName = "Upgrade_Building_MaxHealth",
                stat = UpgradeStat.BuildingMaxHealth,
                displayName = "건물 체력",
                valueMode = UpgradeValueMode.Percent,
                bonusPerLevel = 20f,
                maxLevel = 5,
                baseCost = 60,
                costPerLevel = 40,
                description = "아군 건물의 최대 체력을 강화합니다.",
            },
            new Preset
            {
                fileName = "Upgrade_Building_SpawnCount",
                stat = UpgradeStat.BuildingSpawnCount,
                displayName = "건물 생산 수",
                valueMode = UpgradeValueMode.Flat,
                bonusPerLevel = 1f,
                maxLevel = 3,
                baseCost = 100,
                costPerLevel = 80,
                description = "생산 건물이 낮마다 생산하는 유닛 수를 늘립니다.",
            },
        };
    }

    string outputFolder = "Assets/Data/Upgrades";
    bool overwriteExisting = false;
    bool registerToManager = true;
    List<Preset> presets;
    Vector2 scroll;
    string statusMessage = string.Empty;

    [MenuItem("Tools/Upgrades/Generate Upgrade Definitions")]
    static void OpenWindow()
    {
        UpgradeDefinitionGenerator window = GetWindow<UpgradeDefinitionGenerator>(
            false,
            "Upgrade Generator",
            true);

        window.minSize = new Vector2(440f, 520f);
        window.Show();
    }

    void OnEnable()
    {
        if (presets == null || presets.Count == 0)
            ResetToDefaults();
    }

    void ResetToDefaults()
    {
        presets = new List<Preset>(CreateDefaultPresets());
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("업그레이드 정의 자동 생성", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "아래에서 각 업그레이드의 수치·비용·모드를 수정한 뒤 생성하세요.\n" +
            "생성 후에도 각 에셋에서 다시 조정할 수 있습니다.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        outputFolder = EditorGUILayout.TextField("저장 폴더", outputFolder);
        overwriteExisting = EditorGUILayout.ToggleLeft("이미 있으면 덮어쓰기", overwriteExisting);
        registerToManager = EditorGUILayout.ToggleLeft("씬의 UpgradeManager에 자동 등록", registerToManager);

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("업그레이드 목록", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("모두 선택", EditorStyles.miniButtonLeft, GUILayout.Width(70f)))
            SetAllEnabled(true);
        if (GUILayout.Button("모두 해제", EditorStyles.miniButtonMid, GUILayout.Width(70f)))
            SetAllEnabled(false);
        if (GUILayout.Button("기본값 복원", EditorStyles.miniButtonRight, GUILayout.Width(80f)))
            ResetToDefaults();
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        for (int i = 0; i < presets.Count; i++)
            DrawPreset(presets[i]);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8f);

        if (GUILayout.Button("생성", GUILayout.Height(32f)))
            Generate();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }
    }

    void DrawPreset(Preset preset)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        preset.enabled = EditorGUILayout.Toggle(preset.enabled, GUILayout.Width(18f));

        string modeLabel = preset.valueMode == UpgradeValueMode.Percent
            ? $"+{preset.bonusPerLevel:0.##}% / Lv"
            : $"+{preset.bonusPerLevel:0.##} / Lv";

        preset.foldout = EditorGUILayout.Foldout(
            preset.foldout,
            $"{preset.displayName}   ({modeLabel})",
            true);
        EditorGUILayout.EndHorizontal();

        if (preset.foldout)
        {
            EditorGUI.indentLevel++;

            using (new EditorGUI.DisabledScope(!preset.enabled))
            {
                preset.stat = (UpgradeStat)EditorGUILayout.EnumPopup("스탯", preset.stat);
                preset.displayName = EditorGUILayout.TextField("표시 이름", preset.displayName);
                preset.fileName = EditorGUILayout.TextField("파일 이름", preset.fileName);
                preset.valueMode = (UpgradeValueMode)EditorGUILayout.EnumPopup("수치 모드", preset.valueMode);

                string bonusLabel = preset.valueMode == UpgradeValueMode.Percent
                    ? "레벨당 수치 (%)"
                    : "레벨당 수치";
                preset.bonusPerLevel = EditorGUILayout.FloatField(bonusLabel, preset.bonusPerLevel);

                preset.maxLevel = Mathf.Max(1, EditorGUILayout.IntField("최대 레벨", preset.maxLevel));
                preset.baseCost = Mathf.Max(0, EditorGUILayout.IntField("1레벨 비용 (마석)", preset.baseCost));
                preset.costPerLevel = Mathf.Max(0, EditorGUILayout.IntField("레벨당 추가 비용", preset.costPerLevel));
                preset.researchDuration = Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField("연구 시간 (초)", preset.researchDuration));

                EditorGUILayout.LabelField("설명");
                preset.description = EditorGUILayout.TextArea(
                    preset.description,
                    GUILayout.MinHeight(38f));
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    void SetAllEnabled(bool value)
    {
        foreach (Preset preset in presets)
            preset.enabled = value;
    }

    void Generate()
    {
        if (string.IsNullOrWhiteSpace(outputFolder) ||
            !outputFolder.StartsWith("Assets", System.StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("Upgrade Generator", "저장 폴더는 Assets/ 아래여야 합니다.", "확인");
            return;
        }

        EnsureFolder(outputFolder);

        List<UpgradeDefinition> generated = new List<UpgradeDefinition>();
        int created = 0;
        int updated = 0;
        int skipped = 0;

        foreach (Preset preset in presets)
        {
            if (!preset.enabled)
                continue;

            if (string.IsNullOrWhiteSpace(preset.fileName))
            {
                skipped++;
                continue;
            }

            string path = $"{outputFolder}/{preset.fileName}.asset";

            UpgradeDefinition def = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
            bool exists = def != null;

            if (exists && !overwriteExisting)
            {
                skipped++;
                generated.Add(def);
                continue;
            }

            if (!exists)
                def = CreateInstance<UpgradeDefinition>();

            def.stat = preset.stat;
            def.displayName = preset.displayName;
            def.description = preset.description;
            def.valueMode = preset.valueMode;
            def.bonusPerLevel = preset.bonusPerLevel;
            def.maxLevel = preset.maxLevel;
            def.baseCost = preset.baseCost;
            def.costPerLevel = preset.costPerLevel;
            def.researchDuration = preset.researchDuration;

            if (exists)
            {
                EditorUtility.SetDirty(def);
                updated++;
            }
            else
            {
                AssetDatabase.CreateAsset(def, path);
                created++;
            }

            generated.Add(def);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int registered = 0;

        if (registerToManager)
            registered = RegisterToManager(generated);

        statusMessage =
            $"생성 {created}개, 갱신 {updated}개, 건너뜀 {skipped}개" +
            (registerToManager ? $"\nUpgradeManager 등록 {registered}개" : "");

        if (generated.Count > 0)
            EditorGUIUtility.PingObject(generated[0]);
    }

    int RegisterToManager(List<UpgradeDefinition> definitions)
    {
        UpgradeManager manager = FindObjectOfType<UpgradeManager>();

        if (manager == null)
            return 0;

        Undo.RecordObject(manager, "Register Upgrades");

        int added = 0;

        foreach (UpgradeDefinition def in definitions)
        {
            if (def == null || manager.upgrades.Contains(def))
                continue;

            manager.upgrades.Add(def);
            added++;
        }

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        return added;
    }

    // "Assets/Data/Upgrades" 같은 경로의 폴더를 없으면 단계적으로 생성합니다.
    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');
        string current = parts[0]; // "Assets"

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
