using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class TransparentPortraitExporterWindow : EditorWindow
{
    const string PrefOutputFolder = "Tank.PortraitExport.OutputFolder";
    const string PrefWidth = "Tank.PortraitExport.Width";
    const string PrefHeight = "Tank.PortraitExport.Height";
    const string PrefPadding = "Tank.PortraitExport.Padding";
    const string PrefYaw = "Tank.PortraitExport.Yaw";
    const string PrefPitch = "Tank.PortraitExport.Pitch";
    const string PrefOrthographic = "Tank.PortraitExport.Orthographic";
    const string PrefHideGameplayUi = "Tank.PortraitExport.HideGameplayUi";
    const string PrefImportAsSprite = "Tank.PortraitExport.ImportAsSprite";
    const string PrefAssignPortrait = "Tank.PortraitExport.AssignPortrait";

    Object exportTarget;
    string outputFolder = TransparentPortraitExporter.DefaultOutputFolder;
    string fileNameOverride = string.Empty;
    int width = 512;
    int height = 512;
    float padding = 0.15f;
    float yaw = 35f;
    float pitch = 25f;
    bool orthographic = true;
    bool hideGameplayUi = true;
    bool importAsSprite = true;
    bool assignPortrait = true;
    string statusMessage = string.Empty;

    [MenuItem("Tools/Export/Transparent Portrait PNG")]
    static void OpenWindow()
    {
        TransparentPortraitExporterWindow window = GetWindow<TransparentPortraitExporterWindow>(
            false,
            "Portrait Export",
            true);

        window.minSize = new Vector2(420f, 520f);
        window.LoadSettings();
        window.SyncTargetFromSelection();
        window.Show();
    }

    [MenuItem("Tools/Export/Quick Export Selected Portrait")]
    static void QuickExportSelected()
    {
        List<Object> sources = new List<Object>(TransparentPortraitExporter.GetExportSourcesFromSelection());

        if (sources.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Portrait Export",
                "프로젝트 창 또는 Hierarchy에서 GameObject, 프리팹, UnitData를 선택해 주세요.",
                "확인");

            return;
        }

        TransparentPortraitExporter.ExportSettings settings = LoadQuickSettings();
        string folder = EditorPrefs.GetString(PrefOutputFolder, TransparentPortraitExporter.DefaultOutputFolder);
        StringBuilder log = new StringBuilder();
        int successCount = 0;

        foreach (Object source in sources)
        {
            TransparentPortraitExporter.ExportResult result =
                TransparentPortraitExporter.Export(source, folder, null, settings);

            if (result.success)
            {
                successCount++;
                log.AppendLine(result.message);
            }
            else
            {
                log.AppendLine($"실패 ({source.name}): {result.message}");
            }
        }

        EditorUtility.DisplayDialog(
            "Portrait Export",
            $"{successCount}/{sources.Count}개 저장 완료\n\n{log}",
            "확인");
    }

    [MenuItem("Tools/Export/Quick Export Selected Portrait", true)]
    static bool QuickExportSelectedValidate()
    {
        foreach (Object _ in TransparentPortraitExporter.GetExportSourcesFromSelection())
            return true;

        return false;
    }

    void OnEnable()
    {
        LoadSettings();
        SyncTargetFromSelection();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("투명 배경 초상화 PNG 내보내기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "카메라 뷰로 오브젝트만 렌더링해 알파 PNG로 저장합니다.\n" +
            "GameObject, 프리팹, UnitData를 대상으로 사용할 수 있습니다.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        exportTarget = EditorGUILayout.ObjectField("Export Target", exportTarget, typeof(Object), true);

        if (EditorGUI.EndChangeCheck() && exportTarget != null)
            fileNameOverride = GetDefaultFileName(exportTarget);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("선택 대상 가져오기"))
        {
            SyncTargetFromSelection();

            if (exportTarget != null)
                fileNameOverride = GetDefaultFileName(exportTarget);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("출력", EditorStyles.boldLabel);
        outputFolder = EditorGUILayout.TextField("저장 폴더", outputFolder);
        fileNameOverride = EditorGUILayout.TextField("파일 이름", fileNameOverride);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("카메라", EditorStyles.boldLabel);
        width = EditorGUILayout.IntField("너비", width);
        height = EditorGUILayout.IntField("높이", height);
        padding = EditorGUILayout.Slider("여백", padding, 0f, 0.5f);
        yaw = EditorGUILayout.Slider("Yaw", yaw, -180f, 180f);
        pitch = EditorGUILayout.Slider("Pitch", pitch, -30f, 89f);
        orthographic = EditorGUILayout.ToggleLeft("Orthographic 카메라", orthographic);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("옵션", EditorStyles.boldLabel);
        hideGameplayUi = EditorGUILayout.ToggleLeft("체력바/캔버스 등 게임 UI 숨김", hideGameplayUi);
        importAsSprite = EditorGUILayout.ToggleLeft("PNG를 Sprite로 임포트", importAsSprite);
        assignPortrait = EditorGUILayout.ToggleLeft("SelectableEntity.portrait에 자동 할당", assignPortrait);

        EditorGUILayout.Space(10f);

        using (new EditorGUI.DisabledScope(exportTarget == null))
        {
            if (GUILayout.Button("PNG 내보내기", GUILayout.Height(32f)))
                ExportCurrentTarget();
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }
    }

    void ExportCurrentTarget()
    {
        SaveSettings();

        TransparentPortraitExporter.ExportResult result = TransparentPortraitExporter.Export(
            exportTarget,
            outputFolder,
            fileNameOverride,
            CreateSettings());

        statusMessage = result.message;

        if (!result.success)
        {
            EditorUtility.DisplayDialog("Portrait Export", result.message, "확인");
            return;
        }

        Object pingTarget = AssetDatabase.LoadAssetAtPath<Object>(result.assetPath);

        if (pingTarget != null)
            EditorGUIUtility.PingObject(pingTarget);

        if (result.assignedEntity != null)
            statusMessage += $"\nPortrait 할당: {result.assignedEntity.name}";
    }

    void SyncTargetFromSelection()
    {
        List<Object> sources = new List<Object>(TransparentPortraitExporter.GetExportSourcesFromSelection());

        if (sources.Count == 0)
            return;

        exportTarget = sources[0];
    }

    TransparentPortraitExporter.ExportSettings CreateSettings()
    {
        return new TransparentPortraitExporter.ExportSettings
        {
            width = width,
            height = height,
            padding = padding,
            yaw = yaw,
            pitch = pitch,
            orthographic = orthographic,
            hideGameplayUi = hideGameplayUi,
            importAsSprite = importAsSprite,
            assignPortrait = assignPortrait
        };
    }

    static TransparentPortraitExporter.ExportSettings LoadQuickSettings()
    {
        return new TransparentPortraitExporter.ExportSettings
        {
            width = EditorPrefs.GetInt(PrefWidth, 512),
            height = EditorPrefs.GetInt(PrefHeight, 512),
            padding = EditorPrefs.GetFloat(PrefPadding, 0.15f),
            yaw = EditorPrefs.GetFloat(PrefYaw, 35f),
            pitch = EditorPrefs.GetFloat(PrefPitch, 25f),
            orthographic = EditorPrefs.GetBool(PrefOrthographic, true),
            hideGameplayUi = EditorPrefs.GetBool(PrefHideGameplayUi, true),
            importAsSprite = EditorPrefs.GetBool(PrefImportAsSprite, true),
            assignPortrait = EditorPrefs.GetBool(PrefAssignPortrait, true)
        };
    }

    void LoadSettings()
    {
        outputFolder = EditorPrefs.GetString(PrefOutputFolder, TransparentPortraitExporter.DefaultOutputFolder);
        width = EditorPrefs.GetInt(PrefWidth, 512);
        height = EditorPrefs.GetInt(PrefHeight, 512);
        padding = EditorPrefs.GetFloat(PrefPadding, 0.15f);
        yaw = EditorPrefs.GetFloat(PrefYaw, 35f);
        pitch = EditorPrefs.GetFloat(PrefPitch, 25f);
        orthographic = EditorPrefs.GetBool(PrefOrthographic, true);
        hideGameplayUi = EditorPrefs.GetBool(PrefHideGameplayUi, true);
        importAsSprite = EditorPrefs.GetBool(PrefImportAsSprite, true);
        assignPortrait = EditorPrefs.GetBool(PrefAssignPortrait, true);
    }

    void SaveSettings()
    {
        EditorPrefs.SetString(PrefOutputFolder, outputFolder);
        EditorPrefs.SetInt(PrefWidth, width);
        EditorPrefs.SetInt(PrefHeight, height);
        EditorPrefs.SetFloat(PrefPadding, padding);
        EditorPrefs.SetFloat(PrefYaw, yaw);
        EditorPrefs.SetFloat(PrefPitch, pitch);
        EditorPrefs.SetBool(PrefOrthographic, orthographic);
        EditorPrefs.SetBool(PrefHideGameplayUi, hideGameplayUi);
        EditorPrefs.SetBool(PrefImportAsSprite, importAsSprite);
        EditorPrefs.SetBool(PrefAssignPortrait, assignPortrait);
    }

    static string GetDefaultFileName(Object source)
    {
        if (source is UnitData unitData)
            return unitData.name;

        if (source is GameObject gameObject)
            return gameObject.name;

        if (source is Component component)
            return component.gameObject.name;

        return source != null ? source.name : string.Empty;
    }
}
