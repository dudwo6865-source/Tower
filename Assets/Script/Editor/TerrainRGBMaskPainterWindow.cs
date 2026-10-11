using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public sealed class TerrainRGBMaskPainterWindow : EditorWindow
{
    static readonly int MaskId = Shader.PropertyToID("_RGBMask");
    static readonly int UVModeId = Shader.PropertyToID("_MaskUseUV0");
    static readonly int OriginId = Shader.PropertyToID("_MaskOrigin");
    static readonly int SizeId = Shader.PropertyToID("_MaskSize");
    static readonly string[] ChannelNames = { "기본 (R)", "두 번째 (G)", "세 번째 (B)" };
    static readonly int[] Resolutions = { 256, 512, 1024, 2048 };
    static readonly string[] ResolutionNames = { "256", "512", "1024", "2048" };

    [SerializeField] TerrainRGBMaskData data;
    [SerializeField] GameObject root;
    [SerializeField] Material newMaterial;
    [SerializeField] List<Material> newAdditionalMaterials = new List<Material>();
    [SerializeField] Vector2 newOrigin;
    [SerializeField] Vector2 newSize = new Vector2(100f, 100f);
    [SerializeField] int resolutionIndex = 2;
    [SerializeField] float radius = 4f;
    [SerializeField] float radiusKeyStep = 0.5f;
    [SerializeField] float strength = 0.5f;
    [SerializeField] bool usePressure = true;
    [SerializeField] float hardness = 0.5f;
    [SerializeField] float planeHeight;
    [SerializeField] bool usePlaneFallback;
    [SerializeField] int channel;

    sealed class PreviewTarget
    {
        public Renderer renderer;
        public int slot;
        public MaterialPropertyBlock original;
    }

    readonly List<PreviewTarget> previewTargets = new List<PreviewTarget>();
    readonly HashSet<Transform> targetTransforms = new HashSet<Transform>();
    Texture2D preview;
    Vector2 scroll;
    bool paintEnabled;
    bool toggleKeyHeld;
    bool strokeActive;
    bool strokeChanged;
    int strokeControl;
    int strokeUndoGroup;
    int strokeChannel;
    Vector2 lastPoint;
    bool hasLastPoint;
    Color32[] strokeStart;
    float[] coverage;

    [MenuItem("Tools/맵/RGB 바닥 브러시")]
    public static void Open()
    {
        var window = GetWindow<TerrainRGBMaskPainterWindow>("RGB 바닥 브러시");
        window.minSize = new Vector2(370f, 550f);
        window.Show();
    }

    [OnOpenAsset]
    static bool OpenData(int instanceID, int line)
    {
        var asset = EditorUtility.InstanceIDToObject(instanceID) as TerrainRGBMaskData;
        if (asset == null)
            return false;
        Open();
        var window = GetWindow<TerrainRGBMaskPainterWindow>();
        window.StopPainting();
        window.data = asset;
        return true;
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    void OnDisable()
    {
        StopPainting();
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
    }

    void OnPlayModeChanged(PlayModeStateChange state) => StopPainting();

    void OnHierarchyChanged()
    {
        // 삭제된 지형이나 교체된 머티리얼에 오래된 미리보기를 남기지 않는다.
        if (paintEnabled)
            StopPainting();
        Repaint();
    }

    static bool IsCompatible(Material material)
    {
        return material != null && material.HasProperty(MaskId) && material.HasProperty(UVModeId) &&
            material.HasProperty(OriginId) && material.HasProperty(SizeId) && AssetDatabase.Contains(material);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("평지와 절벽의 RGB 재질을 같은 월드 XZ 마스크로 칠합니다.\n" +
            "편집 원본은 자동 보관되며, 게임에 반영하려면 PNG 저장 및 적용을 누릅니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            var nextData = (TerrainRGBMaskData)EditorGUILayout.ObjectField("편집 원본", data, typeof(TerrainRGBMaskData), false);
            if (nextData != data)
            {
                StopPainting();
                data = nextData;
            }
            var nextRoot = (GameObject)EditorGUILayout.ObjectField("지형 부모 오브젝트", root, typeof(GameObject), true);
            if (nextRoot != root)
            {
                StopPainting();
                root = nextRoot;
            }
            if (GUILayout.Button("선택한 오브젝트를 지형으로 지정"))
            {
                StopPainting();
                root = Selection.activeGameObject;
                FindMaterialAndBounds();
            }

            if (data == null)
                DrawCreationGUI();
            else if (!data.IsValid)
                EditorGUILayout.HelpBox("편집 원본의 픽셀 또는 영역 정보가 올바르지 않습니다.", MessageType.Error);
            else
                DrawPaintingGUI();
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawCreationGUI()
    {
        newMaterial = (Material)EditorGUILayout.ObjectField("기본 머티리얼", newMaterial, typeof(Material), false);
        if (GUILayout.Button("지형에서 머티리얼과 범위 가져오기"))
            FindMaterialAndBounds();
        EditorGUILayout.LabelField("함께 적용할 추가 머티리얼", newAdditionalMaterials.Count.ToString());
        newOrigin = EditorGUILayout.Vector2Field("시작 위치 (X,Z)", newOrigin);
        newSize = EditorGUILayout.Vector2Field("맵 크기 (X,Z)", newSize);
        resolutionIndex = EditorGUILayout.Popup("마스크 해상도", resolutionIndex, ResolutionNames);
        EditorGUILayout.HelpBox("기본 흙으로 채운 새 마스크를 만듭니다. 기존 UV0 마스크가 있다면 자동 변환하지 않습니다. " +
            "맵별로 복제한 지형 머티리얼을 사용하는 것이 좋습니다.", MessageType.None);
        using (new EditorGUI.DisabledScope(!IsCompatible(newMaterial) || newSize.x <= 0f || newSize.y <= 0f))
        {
            if (GUILayout.Button("새 편집 원본 만들기", GUILayout.Height(28f)))
                CreateData();
        }
    }

    void DrawPaintingGUI()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("대상 머티리얼", data.material, typeof(Material), false);
            EditorGUILayout.Vector2Field("시작 위치 (X,Z)", data.origin);
            EditorGUILayout.Vector2Field("맵 크기 (X,Z)", data.size);
            EditorGUILayout.ObjectField("저장된 PNG", data.exportedMask, typeof(Texture2D), false);
        }
        DrawMaterialTargets();
        if (!HasValidMaterials())
        {
            EditorGUILayout.HelpBox("RGB 마스크를 지원하는 머티리얼 에셋을 등록해주세요.", MessageType.Error);
            return;
        }
        channel = GUILayout.Toolbar(channel, ChannelNames);
        radius = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("브러시 반경", "월드 단위 반경입니다."), radius));
        radiusKeyStep = Mathf.Max(0.01f, EditorGUILayout.FloatField(
            new GUIContent("단축키 반경 변경량", "[ 또는 ] 키를 한 번 누를 때 바뀌는 월드 단위 반경입니다."), radiusKeyStep));
        strength = EditorGUILayout.Slider("칠하기 강도", strength, 0.01f, 1f);
        usePressure = EditorGUILayout.Toggle(new GUIContent("필압으로 강도 조절", "펜 입력의 필압을 칠하기 강도에 곱합니다. 마우스는 설정한 강도를 그대로 사용합니다."), usePressure);
        hardness = EditorGUILayout.Slider(new GUIContent("경계 단단함", "0은 부드러운 경계, 1은 단단한 경계입니다."), hardness, 0f, 1f);
        usePlaneFallback = EditorGUILayout.Toggle(new GUIContent("Collider 없으면 기준 평면 사용", "높이가 다른 지형은 Collider를 사용하는 편이 정확합니다."), usePlaneFallback);
        if (usePlaneFallback)
            planeHeight = EditorGUILayout.FloatField("기준 평면 높이", planeHeight);

        using (new EditorGUI.DisabledScope(root == null || EditorUtility.IsPersistent(root)))
        {
            if (GUILayout.Button(paintEnabled ? "칠하기 종료 (B)" : "미리보기 및 칠하기 시작 (B)", GUILayout.Height(30f)))
            {
                if (paintEnabled) StopPainting();
                else StartPainting();
            }
        }
        EditorGUILayout.HelpBox("Scene 뷰에서 왼쪽 드래그: 칠하기 / Shift: 기본(R)으로 복원\n" +
            "B: 시작·종료 / [·]: 반경 축소·확대 (Scene 뷰)\n" +
            "Ctrl+Z: 실행 취소 / Esc: 종료 / Alt·오른쪽 버튼: 카메라 조작\n" +
            "다른 지형·Polybrush 도구의 칠하기 모드는 꺼주세요.", MessageType.None);
        using (new EditorGUI.DisabledScope(strokeActive))
        {
            if (GUILayout.Button("전체를 선택한 재질로 채우기"))
            {
                Undo.RegisterCompleteObjectUndo(data, "바닥 마스크 전체 채우기");
                data.Fill(channel);
                PersistAndRefresh();
            }
            if (GUILayout.Button("PNG 저장 및 머티리얼 적용", GUILayout.Height(30f)))
                ExportMask();
        }
        if (preview != null)
        {
            Rect rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxWidth(256f));
            EditorGUI.DrawPreviewTexture(rect, preview, null, ScaleMode.ScaleToFit);
        }
    }

    bool HasValidMaterials()
    {
        if (data == null) return false;
        var targets = data.GetMaterials();
        return targets.Count > 0 && targets.TrueForAll(IsCompatible);
    }

    List<Material> FindRootMaterials(out Bounds bounds)
    {
        var targets = new List<Material>();
        bounds = default;
        bool found = false;
        if (root == null || EditorUtility.IsPersistent(root)) return targets;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            bool matches = false;
            foreach (var material in renderer.sharedMaterials)
                if (IsCompatible(material))
                {
                    if (!targets.Contains(material)) targets.Add(material);
                    matches = true;
                }
            if (!matches) continue;
            if (!found) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            found = true;
        }
        return targets;
    }

    void DrawMaterialTargets()
    {
        var targets = data.GetMaterials();
        foreach (var material in targets)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("등록된 머티리얼", material, typeof(Material), false);
                if (GUILayout.Button("제외", GUILayout.Width(45f)))
                {
                    bool resume = paintEnabled;
                    StopPainting();
                    Undo.RecordObject(data, "지형 머티리얼 제외");
                    if (data.material == material) data.material = null;
                    data.additionalMaterials?.RemoveAll(item => item == material);
                    PersistAndRefresh();
                    if (resume) StartPainting();
                    GUIUtility.ExitGUI();
                }
            }
        }
        if (GUILayout.Button("지형의 RGB 머티리얼 함께 등록"))
        {
            var found = FindRootMaterials(out Bounds bounds);
            if (found.Count == 0)
            {
                ShowNotification(new GUIContent("지형 아래에 RGB 머티리얼이 없습니다."));
                return;
            }
            bool resume = paintEnabled;
            StopPainting();
            Undo.RecordObject(data, "지형 RGB 머티리얼 등록");
            if (data.additionalMaterials == null) data.additionalMaterials = new List<Material>();
            foreach (var material in found)
                if (!targets.Contains(material)) data.additionalMaterials.Add(material);
            PersistAndRefresh();
            if (resume) StartPainting();
            if (bounds.min.x < data.origin.x - 0.01f || bounds.min.z < data.origin.y - 0.01f ||
                bounds.max.x > data.origin.x + data.size.x + 0.01f || bounds.max.z > data.origin.y + data.size.y + 0.01f)
                ShowNotification(new GUIContent("등록했습니다. 기존 마스크 범위 밖은 칠할 수 없습니다."));
        }
    }

    void FindMaterialAndBounds()
    {
        var targets = FindRootMaterials(out Bounds bounds);
        if (targets.Count == 0)
        {
            ShowNotification(new GUIContent("선택한 지형에서 RGB 머티리얼을 찾지 못했습니다."));
            return;
        }
        if (!targets.Contains(newMaterial)) newMaterial = targets[0];
        newAdditionalMaterials = targets;
        newAdditionalMaterials.Remove(newMaterial);
        newOrigin = new Vector2(bounds.min.x, bounds.min.z);
        newSize = new Vector2(Mathf.Max(0.01f, bounds.size.x), Mathf.Max(0.01f, bounds.size.z));
        planeHeight = bounds.center.y;
    }

    void CreateData()
    {
        string path = EditorUtility.SaveFilePanelInProject("바닥 마스크 편집 원본 저장", "TerrainRGBMask", "asset", "맵별로 편집 원본을 저장합니다.");
        if (string.IsNullOrEmpty(path)) return;
        // 기존 에셋을 덮어쓰지 않는다.
        path = AssetDatabase.GenerateUniqueAssetPath(path);
        data = CreateInstance<TerrainRGBMaskData>();
        data.Initialize(Resolutions[resolutionIndex], newOrigin, newSize, newMaterial);
        data.additionalMaterials = new List<Material>(newAdditionalMaterials);
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssetIfDirty(data);
        EditorGUIUtility.PingObject(data);
    }

    void StartPainting()
    {
        StopPainting();
        if (data == null || !data.IsValid || !HasValidMaterials() || root == null)
            return;
        var targets = new HashSet<Material>(data.GetMaterials());
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (!targets.Contains(materials[i])) continue;
                var saved = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(saved, i);
                previewTargets.Add(new PreviewTarget { renderer = renderer, slot = i, original = saved });
                targetTransforms.Add(renderer.transform);
            }
        }
        if (previewTargets.Count == 0)
        {
            ShowNotification(new GUIContent("지형 아래에서 대상 머티리얼을 사용하는 렌더러를 찾지 못했습니다."));
            return;
        }
        paintEnabled = true;
        RefreshPreview();
        SceneView.RepaintAll();
    }

    void RefreshPreview()
    {
        if (!paintEnabled || data == null || !data.IsValid) return;
        bool created = false;
        if (preview == null || preview.width != data.resolution)
        {
            if (preview != null) DestroyImmediate(preview);
            preview = new Texture2D(data.resolution, data.resolution, TextureFormat.RGBA32, false, true)
            {
                name = "바닥 RGB 마스크 미리보기", hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            created = true;
        }
        preview.SetPixels32(data.pixels);
        preview.Apply(false, false);
        // 같은 텍스처의 픽셀만 갱신하므로 매 드래그마다 모든 렌더러를 다시 설정하지 않는다.
        if (created)
        foreach (var target in previewTargets)
        {
            if (target.renderer == null) continue;
            var block = new MaterialPropertyBlock();
            target.renderer.GetPropertyBlock(block, target.slot);
            if (block.isEmpty) target.renderer.GetPropertyBlock(block);
            block.SetTexture(MaskId, preview);
            block.SetFloat(UVModeId, 0f);
            block.SetVector(OriginId, new Vector4(data.origin.x, data.origin.y, 0f, 0f));
            block.SetVector(SizeId, new Vector4(data.size.x, data.size.y, 0f, 0f));
            target.renderer.SetPropertyBlock(block, target.slot);
        }
        SceneView.RepaintAll();
        Repaint();
    }

    void StopPainting()
    {
        EndStroke();
        paintEnabled = false;
        foreach (var target in previewTargets)
            if (target.renderer != null)
                target.renderer.SetPropertyBlock(target.original.isEmpty ? null : target.original, target.slot);
        previewTargets.Clear();
        targetTransforms.Clear();
        if (preview != null) DestroyImmediate(preview);
        preview = null;
        SceneView.RepaintAll();
    }

    bool TryGetPoint(Vector2 mouse, out Vector3 point)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
        float distance = float.PositiveInfinity;
        point = default;
        foreach (var hit in hits)
        {
            // 타워나 장식은 제외하고, 대상 지형의 렌더러와 같은 오브젝트에 있는 Collider만 받는다.
            if (!targetTransforms.Contains(hit.collider.transform) || hit.distance >= distance)
                continue;
            distance = hit.distance;
            point = hit.point;
        }
        if (float.IsPositiveInfinity(distance))
        {
            if (!usePlaneFallback || !new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f)).Raycast(ray, out distance))
                return false;
            point = ray.GetPoint(distance);
        }
        return point.x >= data.origin.x && point.z >= data.origin.y &&
            point.x <= data.origin.x + data.size.x && point.z <= data.origin.y + data.size.y;
    }

    void OnSceneGUI(SceneView sceneView)
    {
        // 칠하기가 꺼져 있어도 B로 시작할 수 있도록 활성 상태 검사보다 먼저 처리한다.
        if (HandleBrushShortcut(sceneView))
            return;
        if (!paintEnabled || data == null || !data.IsValid || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        Event e = Event.current;
        int control = GUIUtility.GetControlID(GetHashCode(), FocusType.Passive);
        if (e.type == EventType.Ignore || e.type == EventType.MouseLeaveWindow)
        {
            EndStroke();
            return;
        }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            StopPainting();
            e.Use();
            return;
        }
        if (e.rawType == EventType.MouseUp && strokeActive && e.button == 0)
        {
            EndStroke();
            e.Use();
            return;
        }
        if (e.alt || e.control || e.command || Tools.viewToolActive)
        {
            hasLastPoint = false;
            return;
        }
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
        bool hasPoint = TryGetPoint(e.mousePosition, out Vector3 point);
        if (hasPoint && e.type == EventType.Repaint)
        {
            Handles.color = TerrainRGBMaskBrushMath.ChannelColor(e.shift ? 0 : channel);
            Handles.DrawWireDisc(point + Vector3.up * 0.03f, Vector3.up, radius);
            Handles.DrawWireDisc(point + Vector3.up * 0.03f, Vector3.up, radius * hardness);
        }
        if (e.type == EventType.MouseMove) sceneView.Repaint();
        if (!hasPoint)
        {
            hasLastPoint = false;
            if (strokeActive && e.type == EventType.MouseDrag && e.button == 0)
                e.Use();
            return;
        }
        if (e.type == EventType.MouseDown && e.button == 0 && HandleUtility.nearestControl == control && GUIUtility.hotControl == 0)
        {
            BeginStroke(control, e.shift ? 0 : channel);
            Paint(point);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0 && strokeActive && GUIUtility.hotControl == strokeControl)
        {
            Paint(point);
            e.Use();
        }
    }

    bool HandleBrushShortcut(SceneView sceneView)
    {
        Event e = Event.current;
        if (e.rawType == EventType.KeyUp && e.keyCode == KeyCode.B)
            toggleKeyHeld = false;
        if (focusedWindow != sceneView)
        {
            toggleKeyHeld = false;
            return false;
        }
        if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField ||
            EditorApplication.isPlayingOrWillChangePlaymode || e.alt || e.control || e.command ||
            e.shift || Tools.viewToolActive || (GUIUtility.hotControl != 0 && !strokeActive))
            return false;

        if (e.keyCode == KeyCode.B)
        {
            if (!paintEnabled && (data == null || !data.IsValid || !HasValidMaterials() ||
                root == null || EditorUtility.IsPersistent(root)))
                return false;
            // 키를 누르고 있을 때 반복 KeyDown으로 켜졌다 꺼지는 것을 막는다.
            if (!toggleKeyHeld)
            {
                toggleKeyHeld = true;
                if (paintEnabled) StopPainting();
                else StartPainting();
            }
        }
        else if (paintEnabled && (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket))
        {
            float direction = e.keyCode == KeyCode.LeftBracket ? -1f : 1f;
            radius = Mathf.Max(0.01f, radius + direction * radiusKeyStep);
            hasLastPoint = false;
        }
        else
            return false;

        e.Use();
        Repaint();
        sceneView.Repaint();
        return true;
    }

    void BeginStroke(int control, int selectedChannel)
    {
        Undo.IncrementCurrentGroup();
        strokeUndoGroup = Undo.GetCurrentGroup();
        Undo.RegisterCompleteObjectUndo(data, "바닥 RGB 마스크 칠하기");
        strokeStart = (Color32[])data.pixels.Clone();
        coverage = new float[data.pixels.Length];
        strokeChannel = selectedChannel;
        strokeControl = control;
        GUIUtility.hotControl = control;
        strokeActive = true;
        strokeChanged = false;
        hasLastPoint = false;
    }

    void Paint(Vector3 point)
    {
        Vector2 next = new Vector2(point.x, point.z);
        Event input = Event.current;
        float pressure = usePressure && input.pointerType == PointerType.Pen ? Mathf.Clamp01(input.pressure) : 1f;
        bool changed = TerrainRGBMaskBrushMath.PaintSegment(data.pixels, strokeStart, coverage,
            data.resolution, data.origin, data.size, hasLastPoint ? lastPoint : next, next,
            radius, strength * pressure, hardness, strokeChannel);
        lastPoint = next;
        hasLastPoint = true;
        if (!changed) return;
        strokeChanged = true;
        EditorUtility.SetDirty(data);
        RefreshPreview();
    }

    void EndStroke()
    {
        if (!strokeActive) return;
        if (GUIUtility.hotControl == strokeControl) GUIUtility.hotControl = 0;
        strokeActive = false;
        strokeStart = null;
        coverage = null;
        hasLastPoint = false;
        Undo.CollapseUndoOperations(strokeUndoGroup);
        if (strokeChanged && data != null) AssetDatabase.SaveAssetIfDirty(data);
    }

    void PersistAndRefresh()
    {
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        RefreshPreview();
        Repaint();
    }

    void OnUndoRedo()
    {
        bool resume = paintEnabled;
        StopPainting();
        if (data != null && data.IsValid)
        {
            PersistAndRefresh();
            if (resume) StartPainting();
        }
    }

    void ExportMask()
    {
        if (!HasValidMaterials()) return;
        string path = data.exportedMask != null ? AssetDatabase.GetAssetPath(data.exportedMask) : "";
        if (string.IsNullOrEmpty(path))
            path = EditorUtility.SaveFilePanelInProject("게임용 RGB 마스크 저장", data.name + "_RGB", "png", "마스크 PNG를 저장합니다.");
        if (string.IsNullOrEmpty(path)) return;
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            ShowNotification(new GUIContent("Assets 폴더 안의 PNG 파일로 저장해주세요."));
            return;
        }
        Texture2D texture = null;
        try
        {
            texture = new Texture2D(data.resolution, data.resolution, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(data.pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = data.resolution;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            var materials = data.GetMaterials();
            var undoTargets = new List<UnityEngine.Object> { data };
            undoTargets.AddRange(materials);
            Undo.RecordObjects(undoTargets.ToArray(), "바닥 마스크 PNG 적용");
            data.exportedMask = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            foreach (var material in materials)
            {
                material.SetTexture(MaskId, data.exportedMask);
                material.SetFloat(UVModeId, 0f);
                material.SetVector(OriginId, new Vector4(data.origin.x, data.origin.y, 0f, 0f));
                material.SetVector(SizeId, new Vector4(data.size.x, data.size.y, 0f, 0f));
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }
            PersistAndRefresh();
            ShowNotification(new GUIContent("PNG 저장과 머티리얼 적용을 완료했습니다."));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowNotification(new GUIContent("PNG 저장에 실패했습니다. 편집 원본은 유지됩니다."));
        }
        finally
        {
            if (texture != null) DestroyImmediate(texture);
        }
    }
}
