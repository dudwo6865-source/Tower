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
    const string PrefFov = "Tank.PortraitExport.Fov";
    const string PrefHeightOffset = "Tank.PortraitExport.HeightOffset";
    const string PrefZoom = "Tank.PortraitExport.Zoom";
    const string PrefOrthographic = "Tank.PortraitExport.Orthographic";
    const string PrefAutoPreview = "Tank.PortraitExport.AutoPreview";
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
    float fieldOfView = 30f;
    float heightOffset = 0f;
    float zoom = 1f;
    bool orthographic = true;
    bool hideGameplayUi = true;
    bool importAsSprite = true;
    bool assignPortrait = true;
    string statusMessage = string.Empty;

    // 미리보기 상태
    bool autoPreview = true;
    Texture2D previewTexture;
    string previewError = string.Empty;
    int lastPreviewHash;
    Object lastPreviewTarget;
    Vector2 scroll;

    // 각도 프리셋 (표시 이름, yaw, pitch)
    static readonly (string label, float yaw, float pitch)[] AnglePresets =
    {
        ("정면", 0f, 0f),
        ("¾", 35f, 25f),
        ("측면", 90f, 0f),
        ("아이소", 45f, 30f),
        ("탑다운", 0f, 89f),
        ("후면", 180f, 15f),
    };

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

    void OnDisable()
    {
        DestroyPreviewTexture();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

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

        // --- 실시간 미리보기 -------------------------------------------------
        DrawPreviewSection();

        // --- 카메라 각도 ---------------------------------------------------
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("카메라 각도", EditorStyles.boldLabel);

        DrawAnglePresets();

        EditorGUILayout.Space(2f);
        yaw = DrawNudgeSlider("Yaw (좌우)", yaw, -180f, 180f, 1f, 5f, "N0");
        pitch = DrawNudgeSlider("Pitch (상하)", pitch, -89f, 89f, 1f, 5f, "N0");
        heightOffset = DrawNudgeSlider("높이 (상하 이동)", heightOffset, -1f, 1f, 0.02f, 0.1f, "N2");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("카메라 렌즈", EditorStyles.boldLabel);
        zoom = DrawNudgeSlider("배율 (크게/작게)", zoom, 0.25f, 4f, 0.05f, 0.25f, "N2");
        padding = EditorGUILayout.Slider("여백", padding, 0f, 0.5f);
        orthographic = EditorGUILayout.ToggleLeft("Orthographic 카메라", orthographic);

        using (new EditorGUI.DisabledScope(orthographic))
            fieldOfView = EditorGUILayout.Slider("FOV (원근)", fieldOfView, 5f, 90f);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("출력", EditorStyles.boldLabel);
        outputFolder = EditorGUILayout.TextField("저장 폴더", outputFolder);
        fileNameOverride = EditorGUILayout.TextField("파일 이름", fileNameOverride);
        width = EditorGUILayout.IntField("너비", width);
        height = EditorGUILayout.IntField("높이", height);

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

        EditorGUILayout.EndScrollView();
    }

    void DrawPreviewSection()
    {
        EditorGUILayout.Space(6f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        autoPreview = EditorGUILayout.ToggleLeft("자동 갱신", autoPreview, GUILayout.Width(90f));

        using (new EditorGUI.DisabledScope(exportTarget == null))
        {
            if (GUILayout.Button("새로고침", GUILayout.Width(70f)))
                RefreshPreview();
        }

        EditorGUILayout.EndHorizontal();

        if (autoPreview)
            RefreshPreviewIfDirty();

        // 미리보기 박스
        float aspect = height > 0 ? (float)width / height : 1f;
        float boxWidth = Mathf.Min(EditorGUIUtility.currentViewWidth - 40f, 240f);
        float boxHeight = boxWidth / Mathf.Max(0.01f, aspect);

        Rect rect = GUILayoutUtility.GetRect(boxWidth, boxHeight, GUILayout.ExpandWidth(false));
        rect.x += (EditorGUIUtility.currentViewWidth - rect.width) * 0.5f - 6f;

        EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 1f));
        DrawCheckerboard(rect);

        if (previewTexture != null)
            GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit, true);
        else if (exportTarget == null)
            EditorGUI.LabelField(rect, "대상을 지정하세요", EditorStyles.centeredGreyMiniLabel);
        else if (!string.IsNullOrEmpty(previewError))
            EditorGUI.LabelField(rect, previewError, EditorStyles.centeredGreyMiniLabel);
    }

    void DrawAnglePresets()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("프리셋", GUILayout.Width(EditorGUIUtility.labelWidth));

        foreach ((string label, float presetYaw, float presetPitch) in AnglePresets)
        {
            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                yaw = presetYaw;
                pitch = presetPitch;
                GUI.FocusControl(null);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    // 슬라이더 + 숫자 입력 + 미세 조정(±small, ±big) 버튼을 함께 그린다.
    float DrawNudgeSlider(string label, float value, float min, float max, float small, float big, string format)
    {
        EditorGUILayout.BeginHorizontal();
        value = EditorGUILayout.Slider(label, value, min, max);

        if (GUILayout.Button("-" + big.ToString(format), EditorStyles.miniButtonLeft, GUILayout.Width(34f)))
            value -= big;

        if (GUILayout.Button("-" + small.ToString(format), EditorStyles.miniButtonMid, GUILayout.Width(34f)))
            value -= small;

        if (GUILayout.Button("+" + small.ToString(format), EditorStyles.miniButtonMid, GUILayout.Width(34f)))
            value += small;

        if (GUILayout.Button("+" + big.ToString(format), EditorStyles.miniButtonRight, GUILayout.Width(34f)))
            value += big;

        EditorGUILayout.EndHorizontal();

        return Mathf.Clamp(value, min, max);
    }

    static void DrawCheckerboard(Rect rect)
    {
        const float cell = 8f;
        Color a = new Color(0.22f, 0.22f, 0.22f, 1f);
        Color b = new Color(0.28f, 0.28f, 0.28f, 1f);

        int cols = Mathf.CeilToInt(rect.width / cell);
        int rows = Mathf.CeilToInt(rect.height / cell);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                Rect c = new Rect(
                    rect.x + x * cell,
                    rect.y + y * cell,
                    Mathf.Min(cell, rect.xMax - (rect.x + x * cell)),
                    Mathf.Min(cell, rect.yMax - (rect.y + y * cell)));

                EditorGUI.DrawRect(c, (x + y) % 2 == 0 ? a : b);
            }
        }
    }

    void RefreshPreviewIfDirty()
    {
        int hash = ComputeSettingsHash();

        if (previewTexture != null && hash == lastPreviewHash && lastPreviewTarget == exportTarget)
            return;

        RefreshPreview();
    }

    void RefreshPreview()
    {
        DestroyPreviewTexture();

        lastPreviewHash = ComputeSettingsHash();
        lastPreviewTarget = exportTarget;
        previewError = string.Empty;

        if (exportTarget == null)
            return;

        // 미리보기는 최종 해상도가 아닌 적당한 크기로 렌더해 빠르게 갱신한다.
        float aspect = height > 0 ? (float)width / height : 1f;
        int previewSize = 256;
        int previewWidth = aspect >= 1f ? previewSize : Mathf.RoundToInt(previewSize * aspect);
        int previewHeight = aspect >= 1f ? Mathf.RoundToInt(previewSize / aspect) : previewSize;

        previewWidth = Mathf.Max(16, previewWidth);
        previewHeight = Mathf.Max(16, previewHeight);

        previewTexture = TransparentPortraitExporter.RenderPreview(
            exportTarget,
            CreateSettings(),
            previewWidth,
            previewHeight,
            out previewError);
    }

    int ComputeSettingsHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + width;
            hash = hash * 31 + height;
            hash = hash * 31 + Mathf.RoundToInt(padding * 1000f);
            hash = hash * 31 + Mathf.RoundToInt(yaw * 100f);
            hash = hash * 31 + Mathf.RoundToInt(pitch * 100f);
            hash = hash * 31 + Mathf.RoundToInt(fieldOfView * 100f);
            hash = hash * 31 + Mathf.RoundToInt(heightOffset * 1000f);
            hash = hash * 31 + Mathf.RoundToInt(zoom * 1000f);
            hash = hash * 31 + (orthographic ? 1 : 0);
            hash = hash * 31 + (hideGameplayUi ? 1 : 0);
            return hash;
        }
    }

    void DestroyPreviewTexture()
    {
        if (previewTexture != null)
        {
            DestroyImmediate(previewTexture);
            previewTexture = null;
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
            fieldOfView = fieldOfView,
            heightOffset = heightOffset,
            zoom = zoom,
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
            fieldOfView = EditorPrefs.GetFloat(PrefFov, 30f),
            heightOffset = EditorPrefs.GetFloat(PrefHeightOffset, 0f),
            zoom = EditorPrefs.GetFloat(PrefZoom, 1f),
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
        fieldOfView = EditorPrefs.GetFloat(PrefFov, 30f);
        heightOffset = EditorPrefs.GetFloat(PrefHeightOffset, 0f);
        zoom = EditorPrefs.GetFloat(PrefZoom, 1f);
        orthographic = EditorPrefs.GetBool(PrefOrthographic, true);
        hideGameplayUi = EditorPrefs.GetBool(PrefHideGameplayUi, true);
        importAsSprite = EditorPrefs.GetBool(PrefImportAsSprite, true);
        assignPortrait = EditorPrefs.GetBool(PrefAssignPortrait, true);
        autoPreview = EditorPrefs.GetBool(PrefAutoPreview, true);
    }

    void SaveSettings()
    {
        EditorPrefs.SetString(PrefOutputFolder, outputFolder);
        EditorPrefs.SetInt(PrefWidth, width);
        EditorPrefs.SetInt(PrefHeight, height);
        EditorPrefs.SetFloat(PrefPadding, padding);
        EditorPrefs.SetFloat(PrefYaw, yaw);
        EditorPrefs.SetFloat(PrefPitch, pitch);
        EditorPrefs.SetFloat(PrefFov, fieldOfView);
        EditorPrefs.SetFloat(PrefHeightOffset, heightOffset);
        EditorPrefs.SetFloat(PrefZoom, zoom);
        EditorPrefs.SetBool(PrefOrthographic, orthographic);
        EditorPrefs.SetBool(PrefHideGameplayUi, hideGameplayUi);
        EditorPrefs.SetBool(PrefImportAsSprite, importAsSprite);
        EditorPrefs.SetBool(PrefAssignPortrait, assignPortrait);
        EditorPrefs.SetBool(PrefAutoPreview, autoPreview);
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
