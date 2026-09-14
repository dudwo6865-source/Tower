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
    const string PrefSideOffset = "Tank.PortraitExport.SideOffset";
    const string PrefZoom = "Tank.PortraitExport.Zoom";
    const string PrefOrthographic = "Tank.PortraitExport.Orthographic";
    const string PrefAutoPreview = "Tank.PortraitExport.AutoPreview";
    const string PrefHideGameplayUi = "Tank.PortraitExport.HideGameplayUi";
    const string PrefImportAsSprite = "Tank.PortraitExport.ImportAsSprite";
    const string PrefAssignPortrait = "Tank.PortraitExport.AssignPortrait";
    const string PrefOverwrite = "Tank.PortraitExport.Overwrite";
    const string PrefSupersample = "Tank.PortraitExport.Supersample";
    const string PrefPreviewBackground = "Tank.PortraitExport.PreviewBackground";
    const string PrefShowCamera = "Tank.PortraitExport.ShowCamera";
    const string PrefShowOutput = "Tank.PortraitExport.ShowOutput";
    const string PrefShowOptions = "Tank.PortraitExport.ShowOptions";

    // 기본값 — "기본값으로 되돌리기"와 EditorPrefs 기본값에서 함께 사용합니다.
    const int DefaultWidth = 512;
    const int DefaultHeight = 512;
    const float DefaultPadding = 0.15f;
    const float DefaultYaw = 35f;
    const float DefaultPitch = 25f;
    const float DefaultFov = 30f;
    const float DefaultHeightOffset = 0f;
    const float DefaultSideOffset = 0f;
    const float DefaultZoom = 1f;
    const int DefaultSupersample = 2;

    // 프리셋 이름 입력칸의 컨트롤 이름 — Enter 저장을 판별할 때 씁니다.
    const string PresetNameControl = "PortraitExport.PresetName";

    // 미리보기는 최종 해상도와 무관하게 이 크기로 렌더합니다.
    const int PreviewRenderSize = 384;
    // 값이 바뀔 때마다 렌더하면 드래그가 무거워지므로 최소 간격을 둡니다.
    const double PreviewMinInterval = 0.05;

    enum PreviewBackground
    {
        Checker,
        Dark,
        Light,
        Magenta,
    }

    Object exportTarget;
    string outputFolder = TransparentPortraitExporter.DefaultOutputFolder;
    string fileNameOverride = string.Empty;
    int width = DefaultWidth;
    int height = DefaultHeight;
    float padding = DefaultPadding;
    float yaw = DefaultYaw;
    float pitch = DefaultPitch;
    float fieldOfView = DefaultFov;
    float heightOffset = DefaultHeightOffset;
    float sideOffset = DefaultSideOffset;
    float zoom = DefaultZoom;
    bool orthographic = true;
    bool hideGameplayUi = true;
    bool importAsSprite = true;
    bool assignPortrait = true;
    bool overwriteExisting;
    int supersample = DefaultSupersample;

    string statusMessage = string.Empty;
    MessageType statusType = MessageType.None;
    string lastExportedPath = string.Empty;

    // 미리보기 상태
    bool autoPreview = true;
    PreviewBackground previewBackground = PreviewBackground.Checker;
    Texture2D previewTexture;
    string previewError = string.Empty;
    int lastPreviewHash;
    Object lastPreviewTarget;
    bool hasPreviewResult;
    double lastPreviewTime;
    bool previewPending;
    Vector2 scroll;

    // 저장한 카메라 프리셋
    PortraitAnglePresetLibrary presetLibrary;
    bool presetLibrarySearched;
    string newPresetName = string.Empty;

    // 섹션 접기 상태
    bool showCamera = true;
    bool showOutput = true;
    bool showOptions = true;

    static Texture2D checkerTexture;

    // 선택 중인 프리셋/크기를 강조할 때 쓰는 색입니다.
    static readonly Color HighlightColor = new Color(0.45f, 0.75f, 1f);

    // 기본 각도 프리셋 (표시 이름, yaw, pitch)
    static readonly (string label, float yaw, float pitch)[] AnglePresets =
    {
        ("정면", 0f, 0f),
        ("¾", 35f, 25f),
        ("측면", 90f, 0f),
        ("아이소", 45f, 30f),
        ("탑다운", 0f, 89f),
        ("후면", 180f, 15f),
    };

    static readonly int[] SizePresets = { 128, 256, 512, 1024 };

    static readonly GUIContent[] SupersampleLabels =
    {
        new GUIContent("끄기"),
        new GUIContent("2배"),
        new GUIContent("4배"),
    };

    static readonly int[] SupersampleValues = { 1, 2, 4 };

    static readonly GUIContent[] BackgroundLabels =
    {
        new GUIContent("체커"),
        new GUIContent("어둡게"),
        new GUIContent("밝게"),
        new GUIContent("자홍"),
    };

    [MenuItem("Tools/내보내기/투명 배경 초상화 PNG", false, 0)]
    static void OpenWindow()
    {
        TransparentPortraitExporterWindow window = GetWindow<TransparentPortraitExporterWindow>(
            false,
            "초상화 내보내기",
            true);

        window.minSize = new Vector2(430f, 560f);
        window.LoadSettings();
        window.SyncTargetFromSelection();
        window.Show();
    }

    [MenuItem("Tools/내보내기/선택 항목 바로 내보내기", false, 1)]
    static void QuickExportSelected()
    {
        List<Object> sources = new List<Object>(TransparentPortraitExporter.GetExportSourcesFromSelection());

        if (sources.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "초상화 내보내기",
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
            "초상화 내보내기",
            $"{successCount}/{sources.Count}개 저장 완료\n\n{log}",
            "확인");
    }

    [MenuItem("Tools/내보내기/선택 항목 바로 내보내기", true, 1)]
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
        Selection.selectionChanged += OnSelectionChanged;
    }

    void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        SaveSettings();
        DestroyPreviewTexture();
    }

    void OnFocus()
    {
        // 다른 곳에서 프리셋 에셋을 만들었거나 지웠을 수 있으니 다시 확인합니다.
        presetLibrarySearched = false;
    }

    void OnSelectionChanged()
    {
        // 선택 개수 표시와 "선택 항목 전체 내보내기" 버튼을 최신 상태로 유지합니다.
        Repaint();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("투명 배경 초상화 PNG 내보내기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "카메라 뷰로 오브젝트만 렌더링해 알파 PNG로 저장합니다.\n" +
            "GameObject, 프리팹, UnitData를 대상으로 사용할 수 있습니다.",
            MessageType.Info);

        DrawTargetSection();
        DrawPreviewSection();
        DrawCameraSection();
        DrawOutputSection();
        DrawOptionsSection();
        DrawExportButtons();
        DrawStatus();

        EditorGUILayout.EndScrollView();
    }

    // --- 대상 ---------------------------------------------------------------

    void DrawTargetSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        exportTarget = EditorGUILayout.ObjectField("내보낼 오브젝트", exportTarget, typeof(Object), true);

        if (EditorGUI.EndChangeCheck() && exportTarget != null)
            fileNameOverride = GetDefaultFileName(exportTarget);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("선택 항목 가져오기"))
        {
            SyncTargetFromSelection();

            if (exportTarget != null)
                fileNameOverride = GetDefaultFileName(exportTarget);
        }

        using (new EditorGUI.DisabledScope(exportTarget == null))
        {
            if (GUILayout.Button("씬에서 찾기", GUILayout.Width(90f)))
                EditorGUIUtility.PingObject(exportTarget);
        }

        EditorGUILayout.EndHorizontal();
    }

    // --- 미리보기 -----------------------------------------------------------

    void DrawPreviewSection()
    {
        EditorGUILayout.Space(8f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel, GUILayout.Width(60f));
        GUILayout.FlexibleSpace();
        autoPreview = EditorGUILayout.ToggleLeft("자동 갱신", autoPreview, GUILayout.Width(80f));

        previewBackground = (PreviewBackground)EditorGUILayout.Popup(
            (int)previewBackground,
            BackgroundLabels,
            EditorStyles.miniButton,
            GUILayout.Width(60f));

        using (new EditorGUI.DisabledScope(exportTarget == null))
        {
            if (GUILayout.Button("새로고침", EditorStyles.miniButton, GUILayout.Width(64f)))
            {
                // 프리팹을 새로 만들었거나 UnitData 연결을 바꾼 경우를 위해 캐시도 비웁니다.
                TransparentPortraitExporter.ClearUnitDataCache();
                RefreshPreview();
            }
        }

        EditorGUILayout.EndHorizontal();

        if (autoPreview)
            RefreshPreviewIfDirty();

        Rect rect = GetPreviewRect();

        DrawPreviewBackground(rect);

        if (previewTexture != null)
            GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit, true);
        else if (exportTarget == null)
            EditorGUI.LabelField(rect, "대상을 지정하세요", EditorStyles.centeredGreyMiniLabel);
        else if (!autoPreview)
            EditorGUI.LabelField(rect, "새로고침을 눌러 미리보기", EditorStyles.centeredGreyMiniLabel);

        DrawBorder(rect, new Color(0f, 0f, 0f, 0.55f));

        HandlePreviewInput(rect);

        EditorGUILayout.LabelField(
            "드래그: 회전 · Alt+드래그: 상하좌우 이동 · 휠: 배율",
            EditorStyles.centeredGreyMiniLabel);

        if (!string.IsNullOrEmpty(previewError))
            EditorGUILayout.HelpBox(previewError, MessageType.Warning);
    }

    Rect GetPreviewRect()
    {
        float aspect = height > 0 ? (float)width / height : 1f;
        aspect = Mathf.Clamp(aspect, 0.25f, 4f);

        float available = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 40f);
        float boxWidth = Mathf.Min(available, 360f);
        float boxHeight = boxWidth / aspect;

        // 세로로 긴 비율이면 높이를 먼저 제한해 창 밖으로 넘치지 않게 합니다.
        if (boxHeight > 360f)
        {
            boxHeight = 360f;
            boxWidth = boxHeight * aspect;
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        Rect rect = GUILayoutUtility.GetRect(boxWidth, boxHeight, GUILayout.ExpandWidth(false));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        return rect;
    }

    void DrawPreviewBackground(Rect rect)
    {
        switch (previewBackground)
        {
            case PreviewBackground.Dark:
                EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.13f, 1f));
                break;

            case PreviewBackground.Light:
                EditorGUI.DrawRect(rect, new Color(0.85f, 0.85f, 0.86f, 1f));
                break;

            case PreviewBackground.Magenta:
                EditorGUI.DrawRect(rect, new Color(0.85f, 0.1f, 0.75f, 1f));
                break;

            default:
                DrawCheckerboard(rect);
                break;
        }
    }

    void HandlePreviewInput(Rect rect)
    {
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        Event current = Event.current;

        switch (current.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (rect.Contains(current.mousePosition) && (current.button == 0 || current.button == 2))
                {
                    GUIUtility.hotControl = controlId;
                    GUI.FocusControl(null);
                    current.Use();
                }

                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != controlId)
                    break;

                if (current.alt || current.button == 2)
                {
                    // 대상이 커서를 따라오도록 시점은 반대 방향으로 민다.
                    heightOffset = Mathf.Clamp(heightOffset + current.delta.y * 0.004f, -1f, 1f);
                    sideOffset = Mathf.Clamp(sideOffset - current.delta.x * 0.004f, -1f, 1f);
                }
                else
                {
                    yaw = WrapAngle(yaw + current.delta.x * 0.6f);
                    pitch = Mathf.Clamp(pitch - current.delta.y * 0.6f, -89f, 89f);
                }

                current.Use();
                Repaint();
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    current.Use();
                }

                break;

            case EventType.ScrollWheel:
                if (!rect.Contains(current.mousePosition))
                    break;

                zoom = Mathf.Clamp(zoom * (1f - current.delta.y * 0.04f), 0.25f, 4f);
                current.Use();
                Repaint();
                break;
        }

        if (rect.Contains(current.mousePosition))
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Orbit);
    }

    // --- 카메라 -------------------------------------------------------------

    void DrawCameraSection()
    {
        EditorGUILayout.Space(6f);
        showCamera = EditorGUILayout.Foldout(showCamera, "카메라", true, EditorStyles.foldoutHeader);

        if (!showCamera)
            return;

        EditorGUI.indentLevel++;

        DrawAnglePresets();
        DrawUserPresets();

        EditorGUILayout.Space(2f);
        yaw = DrawNudgeSlider("Yaw (좌우 회전)", yaw, -180f, 180f, 1f, 15f, "N0");
        pitch = DrawNudgeSlider("Pitch (상하 회전)", pitch, -89f, 89f, 1f, 15f, "N0");
        heightOffset = DrawNudgeSlider("높이 (상하 이동)", heightOffset, -1f, 1f, 0.02f, 0.1f, "N2");
        sideOffset = DrawNudgeSlider("좌우 (수평 이동)", sideOffset, -1f, 1f, 0.02f, 0.1f, "N2");

        EditorGUILayout.Space(4f);
        zoom = DrawNudgeSlider("배율 (크게/작게)", zoom, 0.25f, 4f, 0.05f, 0.25f, "N2");
        padding = EditorGUILayout.Slider("여백", padding, 0f, 0.5f);
        orthographic = EditorGUILayout.ToggleLeft("Orthographic 카메라 (원근 없음)", orthographic);

        using (new EditorGUI.DisabledScope(orthographic))
            fieldOfView = EditorGUILayout.Slider("FOV (원근)", fieldOfView, 5f, 90f);

        EditorGUILayout.Space(2f);

        if (GUILayout.Button("카메라 값 초기화", EditorStyles.miniButton))
        {
            ResetCameraDefaults();
            GUI.FocusControl(null);
        }

        EditorGUI.indentLevel--;
    }

    void DrawAnglePresets()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("기본 각도");

        Color previousColor = GUI.backgroundColor;

        foreach ((string label, float presetYaw, float presetPitch) in AnglePresets)
        {
            bool active = Mathf.Abs(Mathf.DeltaAngle(yaw, presetYaw)) < 0.5f
                && Mathf.Abs(pitch - presetPitch) < 0.5f;

            GUI.backgroundColor = active ? HighlightColor : previousColor;

            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                yaw = presetYaw;
                pitch = presetPitch;
                GUI.FocusControl(null);
            }
        }

        GUI.backgroundColor = previousColor;
        EditorGUILayout.EndHorizontal();
    }

    // 저장한 프리셋은 각도뿐 아니라 높이·배율·여백·투영까지 한 번에 되돌립니다.
    void DrawUserPresets()
    {
        PortraitAnglePresetLibrary library = GetPresetLibrary();
        List<PortraitAnglePresetLibrary.Preset> presets = library != null ? library.Presets : null;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(new GUIContent(
            "내 프리셋",
            "현재 카메라 설정(각도·높이·배율·여백·투영)을 통째로 저장해 둡니다."));

        if (presets == null || presets.Count == 0)
            EditorGUILayout.LabelField("저장한 프리셋이 없습니다.", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();

        if (presets != null && presets.Count > 0)
            DrawUserPresetButtons(library, presets);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(" ");

        GUI.SetNextControlName(PresetNameControl);
        newPresetName = EditorGUILayout.TextField(newPresetName);

        // 이름을 치고 Enter를 눌러도 저장되게 합니다.
        bool enterPressed = Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            && GUI.GetNameOfFocusedControl() == PresetNameControl;

        if (enterPressed)
            Event.current.Use();

        bool overwrites = library != null && library.FindByName(newPresetName.Trim()) != null;
        string saveLabel = overwrites ? "덮어쓰기" : "현재 설정 저장";

        if (GUILayout.Button(saveLabel, EditorStyles.miniButton, GUILayout.Width(88f)) || enterPressed)
            SaveCurrentAsPreset();

        using (new EditorGUI.DisabledScope(library == null))
        {
            if (GUILayout.Button(new GUIContent("에셋", "프리셋 에셋을 선택합니다. 인스펙터에서 이름과 순서를 바꿀 수 있습니다."),
                EditorStyles.miniButton, GUILayout.Width(40f)))
            {
                Selection.activeObject = library;
                EditorGUIUtility.PingObject(library);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    void DrawUserPresetButtons(PortraitAnglePresetLibrary library, List<PortraitAnglePresetLibrary.Preset> presets)
    {
        const float cellWidth = 116f;

        float available = Mathf.Max(cellWidth, EditorGUIUtility.currentViewWidth - 40f);
        int perRow = Mathf.Max(1, Mathf.FloorToInt(available / cellWidth));
        int deleteIndex = -1;

        for (int i = 0; i < presets.Count; i++)
        {
            if (i % perRow == 0)
            {
                if (i > 0)
                    EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
            }

            PortraitAnglePresetLibrary.Preset preset = presets[i];

            if (preset == null)
                continue;

            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = IsPresetActive(preset) ? HighlightColor : previousColor;

            if (GUILayout.Button(
                new GUIContent(preset.name, DescribePreset(preset)),
                EditorStyles.miniButtonLeft,
                GUILayout.Width(cellWidth - 24f)))
            {
                ApplyPreset(preset);
                GUI.FocusControl(null);
            }

            GUI.backgroundColor = previousColor;

            if (GUILayout.Button(
                new GUIContent("×", "이 프리셋을 삭제합니다."),
                EditorStyles.miniButtonRight,
                GUILayout.Width(20f)))
            {
                deleteIndex = i;
            }
        }

        EditorGUILayout.EndHorizontal();

        if (deleteIndex >= 0)
            DeletePreset(library, deleteIndex);
    }

    void ApplyPreset(PortraitAnglePresetLibrary.Preset preset)
    {
        yaw = Mathf.Clamp(WrapAngle(preset.yaw), -180f, 180f);
        pitch = Mathf.Clamp(preset.pitch, -89f, 89f);
        heightOffset = Mathf.Clamp(preset.heightOffset, -1f, 1f);
        sideOffset = Mathf.Clamp(preset.sideOffset, -1f, 1f);
        zoom = Mathf.Clamp(preset.zoom, 0.25f, 4f);
        padding = Mathf.Clamp(preset.padding, 0f, 0.5f);
        orthographic = preset.orthographic;
        fieldOfView = Mathf.Clamp(preset.fieldOfView, 5f, 90f);
    }

    bool IsPresetActive(PortraitAnglePresetLibrary.Preset preset)
    {
        if (orthographic != preset.orthographic)
            return false;

        if (!orthographic && Mathf.Abs(fieldOfView - preset.fieldOfView) > 0.1f)
            return false;

        return Mathf.Abs(Mathf.DeltaAngle(yaw, preset.yaw)) < 0.5f
            && Mathf.Abs(pitch - preset.pitch) < 0.5f
            && Mathf.Abs(heightOffset - preset.heightOffset) < 0.005f
            && Mathf.Abs(sideOffset - preset.sideOffset) < 0.005f
            && Mathf.Abs(zoom - preset.zoom) < 0.005f
            && Mathf.Abs(padding - preset.padding) < 0.005f;
    }

    static string DescribePreset(PortraitAnglePresetLibrary.Preset preset)
    {
        string projection = preset.orthographic ? "Orthographic" : $"FOV {preset.fieldOfView:N0}";

        return $"Yaw {preset.yaw:N0}° · Pitch {preset.pitch:N0}°\n" +
            $"높이 {preset.heightOffset:N2} · 좌우 {preset.sideOffset:N2} · 배율 {preset.zoom:N2}\n" +
            $"여백 {preset.padding:N2} · {projection}";
    }

    void SaveCurrentAsPreset()
    {
        string presetName = string.IsNullOrWhiteSpace(newPresetName)
            ? SuggestPresetName()
            : newPresetName.Trim();

        PortraitAnglePresetLibrary library = PortraitAnglePresetLibrary.FindOrCreate();
        presetLibrary = library;
        presetLibrarySearched = true;

        Undo.RecordObject(library, "초상화 프리셋 저장");

        PortraitAnglePresetLibrary.Preset preset = library.FindByName(presetName);
        bool isNew = preset == null;

        if (isNew)
        {
            preset = new PortraitAnglePresetLibrary.Preset();
            library.Presets.Add(preset);
        }

        preset.name = presetName;
        preset.yaw = yaw;
        preset.pitch = pitch;
        preset.heightOffset = heightOffset;
        preset.sideOffset = sideOffset;
        preset.zoom = zoom;
        preset.padding = padding;
        preset.orthographic = orthographic;
        preset.fieldOfView = fieldOfView;

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        newPresetName = string.Empty;
        GUI.FocusControl(null);

        SetStatus(
            isNew ? $"프리셋 \"{presetName}\" 저장" : $"프리셋 \"{presetName}\" 덮어쓰기",
            MessageType.Info);
    }

    void DeletePreset(PortraitAnglePresetLibrary library, int index)
    {
        if (library == null || index < 0 || index >= library.Presets.Count)
            return;

        string presetName = library.Presets[index].name;

        if (!EditorUtility.DisplayDialog(
            "프리셋 삭제",
            $"\"{presetName}\" 프리셋을 삭제할까요?",
            "삭제",
            "취소"))
        {
            return;
        }

        Undo.RecordObject(library, "초상화 프리셋 삭제");
        library.Presets.RemoveAt(index);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        SetStatus($"프리셋 \"{presetName}\" 삭제", MessageType.Info);
    }

    string SuggestPresetName()
    {
        PortraitAnglePresetLibrary library = GetPresetLibrary();
        int index = 1;

        while (library != null && library.FindByName($"프리셋 {index}") != null)
            index++;

        return $"프리셋 {index}";
    }

    // 에셋 탐색은 비싸므로 한 번만 찾고, 창이 포커스를 받을 때 다시 확인합니다.
    PortraitAnglePresetLibrary GetPresetLibrary()
    {
        if (presetLibrary != null)
            return presetLibrary;

        if (presetLibrarySearched)
            return null;

        presetLibrarySearched = true;
        presetLibrary = PortraitAnglePresetLibrary.Find();

        return presetLibrary;
    }

    // --- 출력 ---------------------------------------------------------------

    void DrawOutputSection()
    {
        EditorGUILayout.Space(6f);
        showOutput = EditorGUILayout.Foldout(showOutput, "출력", true, EditorStyles.foldoutHeader);

        if (!showOutput)
            return;

        EditorGUI.indentLevel++;

        EditorGUILayout.BeginHorizontal();
        outputFolder = EditorGUILayout.TextField("저장 폴더", outputFolder);

        if (GUILayout.Button("찾기", EditorStyles.miniButtonLeft, GUILayout.Width(40f)))
            BrowseOutputFolder();

        if (GUILayout.Button("열기", EditorStyles.miniButtonRight, GUILayout.Width(40f)))
            EditorUtility.RevealInFinder(outputFolder);

        EditorGUILayout.EndHorizontal();

        string defaultName = exportTarget != null ? GetDefaultFileName(exportTarget) : "파일 이름";
        fileNameOverride = EditorGUILayout.TextField("파일 이름", fileNameOverride);

        if (string.IsNullOrWhiteSpace(fileNameOverride) && exportTarget != null)
            EditorGUILayout.LabelField(" ", $"비워 두면 \"{defaultName}.png\"", EditorStyles.miniLabel);

        // 한 줄에 너비 × 높이를 묶어 라벨이 겹치지 않게 합니다.
        // 입력 도중 값이 튀지 않도록 확정(Enter/포커스 이동) 후에만 반영합니다.
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("해상도 (px)");
        width = Mathf.Clamp(EditorGUILayout.DelayedIntField(width), 16, 4096);
        EditorGUILayout.LabelField("×", GUILayout.Width(14f));
        height = Mathf.Clamp(EditorGUILayout.DelayedIntField(height), 16, 4096);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("크기 프리셋");

        foreach (int size in SizePresets)
        {
            bool active = width == size && height == size;
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = active ? HighlightColor : previousColor;

            if (GUILayout.Button($"{size}", EditorStyles.miniButton))
            {
                width = size;
                height = size;
                GUI.FocusControl(null);
            }

            GUI.backgroundColor = previousColor;
        }

        EditorGUILayout.EndHorizontal();

        overwriteExisting = EditorGUILayout.ToggleLeft(
            new GUIContent("같은 이름이면 덮어쓰기", "끄면 name_1.png 처럼 새 파일이 계속 늘어납니다."),
            overwriteExisting);

        EditorGUI.indentLevel--;
    }

    void BrowseOutputFolder()
    {
        string start = System.IO.Directory.Exists(outputFolder) ? outputFolder : "Assets";
        string absolute = EditorUtility.OpenFolderPanel("저장 폴더 선택", start, string.Empty);

        if (string.IsNullOrEmpty(absolute))
            return;

        string projectPath = Application.dataPath.Replace('\\', '/');
        absolute = absolute.Replace('\\', '/');

        if (!absolute.StartsWith(projectPath, System.StringComparison.Ordinal))
        {
            SetStatus("프로젝트의 Assets 폴더 안쪽만 선택할 수 있습니다.", MessageType.Warning);
            return;
        }

        outputFolder = "Assets" + absolute.Substring(projectPath.Length);
        GUI.FocusControl(null);
    }

    // --- 옵션 ---------------------------------------------------------------

    void DrawOptionsSection()
    {
        EditorGUILayout.Space(6f);
        showOptions = EditorGUILayout.Foldout(showOptions, "옵션", true, EditorStyles.foldoutHeader);

        if (!showOptions)
            return;

        EditorGUI.indentLevel++;

        hideGameplayUi = EditorGUILayout.ToggleLeft("체력바/캔버스 등 게임 UI 숨김", hideGameplayUi);
        importAsSprite = EditorGUILayout.ToggleLeft("PNG를 Sprite로 임포트", importAsSprite);
        assignPortrait = EditorGUILayout.ToggleLeft("SelectableEntity.portrait에 자동 할당", assignPortrait);

        supersample = EditorGUILayout.IntPopup(
            new GUIContent("외곽선 부드럽게", "높을수록 계단현상이 줄지만 렌더가 느려집니다."),
            supersample,
            SupersampleLabels,
            SupersampleValues);

        EditorGUI.indentLevel--;
    }

    // --- 내보내기 -----------------------------------------------------------

    void DrawExportButtons()
    {
        EditorGUILayout.Space(10f);

        int selectionCount = CountSelectionSources();

        using (new EditorGUI.DisabledScope(exportTarget == null))
        {
            if (GUILayout.Button("PNG 내보내기", GUILayout.Height(34f)))
                ExportCurrentTarget();
        }

        using (new EditorGUI.DisabledScope(selectionCount < 2))
        {
            string label = selectionCount >= 2
                ? $"선택한 {selectionCount}개 모두 내보내기"
                : "선택한 항목 모두 내보내기";

            if (GUILayout.Button(label, GUILayout.Height(24f)))
                ExportSelection();
        }

        if (GUILayout.Button("모든 설정 기본값으로", EditorStyles.miniButton))
        {
            ResetAllDefaults();
            GUI.FocusControl(null);
        }
    }

    void DrawStatus()
    {
        if (string.IsNullOrEmpty(statusMessage))
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(statusMessage, statusType);

        if (string.IsNullOrEmpty(lastExportedPath))
            return;

        if (GUILayout.Button("저장한 PNG 선택", EditorStyles.miniButton))
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(lastExportedPath);

            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }
    }

    // --- 공용 위젯 ----------------------------------------------------------

    // 슬라이더 + 미세 조정(±small, ±big) 버튼을 함께 그립니다.
    float DrawNudgeSlider(string label, float value, float min, float max, float small, float big, string format)
    {
        EditorGUILayout.BeginHorizontal();
        value = EditorGUILayout.Slider(label, value, min, max);

        if (GUILayout.Button("-" + big.ToString(format), EditorStyles.miniButtonLeft, GUILayout.Width(38f)))
            value -= big;

        if (GUILayout.Button("-" + small.ToString(format), EditorStyles.miniButtonMid, GUILayout.Width(38f)))
            value -= small;

        if (GUILayout.Button("+" + small.ToString(format), EditorStyles.miniButtonMid, GUILayout.Width(38f)))
            value += small;

        if (GUILayout.Button("+" + big.ToString(format), EditorStyles.miniButtonRight, GUILayout.Width(38f)))
            value += big;

        EditorGUILayout.EndHorizontal();

        return Mathf.Clamp(value, min, max);
    }

    static void DrawCheckerboard(Rect rect)
    {
        Texture2D checker = GetCheckerTexture();

        // 셀마다 DrawRect를 호출하는 대신 타일 텍스처 한 번으로 그립니다.
        GUI.DrawTextureWithTexCoords(
            rect,
            checker,
            new Rect(0f, 0f, rect.width / checker.width, rect.height / checker.height));
    }

    static Texture2D GetCheckerTexture()
    {
        if (checkerTexture != null)
            return checkerTexture;

        const int cell = 8;
        const int size = cell * 2;

        Color a = new Color(0.24f, 0.24f, 0.24f, 1f);
        Color b = new Color(0.31f, 0.31f, 0.31f, 1f);

        checkerTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point,
        };

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = (x / cell + y / cell) % 2 == 0 ? a : b;
        }

        checkerTexture.SetPixels(pixels);
        checkerTexture.Apply();

        return checkerTexture;
    }

    // 미리보기 영역 경계를 분명히 보여 주는 1px 테두리입니다.
    static void DrawBorder(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, 1f, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), color);
    }

    static float WrapAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    // --- 미리보기 렌더 ------------------------------------------------------

    void RefreshPreviewIfDirty()
    {
        int hash = ComputeSettingsHash();

        // 렌더에 실패한 설정도 "처리 완료"로 기록해야 매 프레임 다시 시도하지 않습니다.
        if (hasPreviewResult && hash == lastPreviewHash && lastPreviewTarget == exportTarget)
            return;

        double now = EditorApplication.timeSinceStartup;

        if (now - lastPreviewTime < PreviewMinInterval)
        {
            previewPending = true;
            Repaint();
            return;
        }

        RefreshPreview();
    }

    void RefreshPreview()
    {
        DestroyPreviewTexture();

        lastPreviewHash = ComputeSettingsHash();
        lastPreviewTarget = exportTarget;
        lastPreviewTime = EditorApplication.timeSinceStartup;
        previewPending = false;
        hasPreviewResult = true;
        previewError = string.Empty;

        if (exportTarget == null)
            return;

        float aspect = height > 0 ? (float)width / height : 1f;
        int previewWidth = aspect >= 1f ? PreviewRenderSize : Mathf.RoundToInt(PreviewRenderSize * aspect);
        int previewHeight = aspect >= 1f ? Mathf.RoundToInt(PreviewRenderSize / aspect) : PreviewRenderSize;

        previewWidth = Mathf.Max(16, previewWidth);
        previewHeight = Mathf.Max(16, previewHeight);

        // 미리보기는 자주 갱신되므로 슈퍼샘플링 비용을 2배까지만 씁니다.
        TransparentPortraitExporter.ExportSettings previewSettings = CreateSettings();
        previewSettings.supersample = Mathf.Min(previewSettings.supersample, 2);

        previewTexture = TransparentPortraitExporter.RenderPreview(
            exportTarget,
            previewSettings,
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
            hash = hash * 31 + Mathf.RoundToInt(sideOffset * 1000f);
            hash = hash * 31 + Mathf.RoundToInt(zoom * 1000f);
            hash = hash * 31 + (orthographic ? 1 : 0);
            hash = hash * 31 + (hideGameplayUi ? 1 : 0);
            hash = hash * 31 + supersample;
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

    void Update()
    {
        // 드래그 중 최소 간격 때문에 미뤄 둔 미리보기를 이어서 갱신합니다.
        if (previewPending && autoPreview)
            Repaint();
    }

    // --- 내보내기 실행 ------------------------------------------------------

    void ExportCurrentTarget()
    {
        SaveSettings();

        TransparentPortraitExporter.ExportResult result = TransparentPortraitExporter.Export(
            exportTarget,
            outputFolder,
            fileNameOverride,
            CreateSettings());

        if (!result.success)
        {
            lastExportedPath = string.Empty;
            SetStatus(result.message, MessageType.Error);
            EditorUtility.DisplayDialog("초상화 내보내기", result.message, "확인");
            return;
        }

        lastExportedPath = result.assetPath;
        string message = result.message;

        if (result.assignedEntity != null)
            message += $"\nPortrait 할당: {result.assignedEntity.name}";

        SetStatus(message, MessageType.Info);

        Object pingTarget = AssetDatabase.LoadAssetAtPath<Object>(result.assetPath);

        if (pingTarget != null)
            EditorGUIUtility.PingObject(pingTarget);
    }

    void ExportSelection()
    {
        SaveSettings();

        List<Object> sources = new List<Object>(TransparentPortraitExporter.GetExportSourcesFromSelection());

        if (sources.Count == 0)
        {
            SetStatus("선택된 대상이 없습니다.", MessageType.Warning);
            return;
        }

        TransparentPortraitExporter.ExportSettings settings = CreateSettings();
        StringBuilder failures = new StringBuilder();
        int successCount = 0;

        try
        {
            for (int i = 0; i < sources.Count; i++)
            {
                Object source = sources[i];

                EditorUtility.DisplayProgressBar(
                    "초상화 내보내기",
                    $"{source.name} ({i + 1}/{sources.Count})",
                    (float)i / sources.Count);

                TransparentPortraitExporter.ExportResult result = TransparentPortraitExporter.Export(
                    source,
                    outputFolder,
                    GetDefaultFileName(source),
                    settings);

                if (result.success)
                {
                    successCount++;
                    lastExportedPath = result.assetPath;
                    continue;
                }

                failures.AppendLine($"· {source.name}: {result.message}");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (failures.Length > 0)
        {
            SetStatus($"{successCount}/{sources.Count}개 저장 완료\n실패:\n{failures}", MessageType.Warning);
            return;
        }

        SetStatus($"{successCount}개 저장 완료: {outputFolder}", MessageType.Info);
    }

    int CountSelectionSources()
    {
        int count = 0;

        foreach (Object _ in TransparentPortraitExporter.GetExportSourcesFromSelection())
            count++;

        return count;
    }

    void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
    }

    void SyncTargetFromSelection()
    {
        List<Object> sources = new List<Object>(TransparentPortraitExporter.GetExportSourcesFromSelection());

        if (sources.Count == 0)
            return;

        exportTarget = sources[0];
    }

    void ResetCameraDefaults()
    {
        yaw = DefaultYaw;
        pitch = DefaultPitch;
        heightOffset = DefaultHeightOffset;
        sideOffset = DefaultSideOffset;
        zoom = DefaultZoom;
        padding = DefaultPadding;
        fieldOfView = DefaultFov;
        orthographic = true;
    }

    void ResetAllDefaults()
    {
        ResetCameraDefaults();
        outputFolder = TransparentPortraitExporter.DefaultOutputFolder;
        width = DefaultWidth;
        height = DefaultHeight;
        supersample = DefaultSupersample;
        overwriteExisting = false;
        hideGameplayUi = true;
        importAsSprite = true;
        assignPortrait = true;
        SetStatus("설정을 기본값으로 되돌렸습니다.", MessageType.Info);
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
            sideOffset = sideOffset,
            zoom = zoom,
            orthographic = orthographic,
            hideGameplayUi = hideGameplayUi,
            importAsSprite = importAsSprite,
            assignPortrait = assignPortrait,
            overwriteExisting = overwriteExisting,
            supersample = supersample
        };
    }

    static TransparentPortraitExporter.ExportSettings LoadQuickSettings()
    {
        return new TransparentPortraitExporter.ExportSettings
        {
            width = EditorPrefs.GetInt(PrefWidth, DefaultWidth),
            height = EditorPrefs.GetInt(PrefHeight, DefaultHeight),
            padding = EditorPrefs.GetFloat(PrefPadding, DefaultPadding),
            yaw = EditorPrefs.GetFloat(PrefYaw, DefaultYaw),
            pitch = EditorPrefs.GetFloat(PrefPitch, DefaultPitch),
            fieldOfView = EditorPrefs.GetFloat(PrefFov, DefaultFov),
            heightOffset = EditorPrefs.GetFloat(PrefHeightOffset, DefaultHeightOffset),
            sideOffset = EditorPrefs.GetFloat(PrefSideOffset, DefaultSideOffset),
            zoom = EditorPrefs.GetFloat(PrefZoom, DefaultZoom),
            orthographic = EditorPrefs.GetBool(PrefOrthographic, true),
            hideGameplayUi = EditorPrefs.GetBool(PrefHideGameplayUi, true),
            importAsSprite = EditorPrefs.GetBool(PrefImportAsSprite, true),
            assignPortrait = EditorPrefs.GetBool(PrefAssignPortrait, true),
            overwriteExisting = EditorPrefs.GetBool(PrefOverwrite, false),
            supersample = EditorPrefs.GetInt(PrefSupersample, DefaultSupersample)
        };
    }

    void LoadSettings()
    {
        outputFolder = EditorPrefs.GetString(PrefOutputFolder, TransparentPortraitExporter.DefaultOutputFolder);
        width = EditorPrefs.GetInt(PrefWidth, DefaultWidth);
        height = EditorPrefs.GetInt(PrefHeight, DefaultHeight);
        padding = EditorPrefs.GetFloat(PrefPadding, DefaultPadding);
        yaw = EditorPrefs.GetFloat(PrefYaw, DefaultYaw);
        pitch = EditorPrefs.GetFloat(PrefPitch, DefaultPitch);
        fieldOfView = EditorPrefs.GetFloat(PrefFov, DefaultFov);
        heightOffset = EditorPrefs.GetFloat(PrefHeightOffset, DefaultHeightOffset);
        sideOffset = EditorPrefs.GetFloat(PrefSideOffset, DefaultSideOffset);
        zoom = EditorPrefs.GetFloat(PrefZoom, DefaultZoom);
        orthographic = EditorPrefs.GetBool(PrefOrthographic, true);
        hideGameplayUi = EditorPrefs.GetBool(PrefHideGameplayUi, true);
        importAsSprite = EditorPrefs.GetBool(PrefImportAsSprite, true);
        assignPortrait = EditorPrefs.GetBool(PrefAssignPortrait, true);
        autoPreview = EditorPrefs.GetBool(PrefAutoPreview, true);
        overwriteExisting = EditorPrefs.GetBool(PrefOverwrite, false);
        supersample = EditorPrefs.GetInt(PrefSupersample, DefaultSupersample);
        previewBackground = (PreviewBackground)EditorPrefs.GetInt(PrefPreviewBackground, (int)PreviewBackground.Checker);
        showCamera = EditorPrefs.GetBool(PrefShowCamera, true);
        showOutput = EditorPrefs.GetBool(PrefShowOutput, true);
        showOptions = EditorPrefs.GetBool(PrefShowOptions, true);
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
        EditorPrefs.SetFloat(PrefSideOffset, sideOffset);
        EditorPrefs.SetFloat(PrefZoom, zoom);
        EditorPrefs.SetBool(PrefOrthographic, orthographic);
        EditorPrefs.SetBool(PrefHideGameplayUi, hideGameplayUi);
        EditorPrefs.SetBool(PrefImportAsSprite, importAsSprite);
        EditorPrefs.SetBool(PrefAssignPortrait, assignPortrait);
        EditorPrefs.SetBool(PrefAutoPreview, autoPreview);
        EditorPrefs.SetBool(PrefOverwrite, overwriteExisting);
        EditorPrefs.SetInt(PrefSupersample, supersample);
        EditorPrefs.SetInt(PrefPreviewBackground, (int)previewBackground);
        EditorPrefs.SetBool(PrefShowCamera, showCamera);
        EditorPrefs.SetBool(PrefShowOutput, showOutput);
        EditorPrefs.SetBool(PrefShowOptions, showOptions);
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
