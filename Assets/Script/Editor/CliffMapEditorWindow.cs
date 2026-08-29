using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 스타크래프트식 절벽 맵 에디터입니다.
// Top + 벽/코너 자동 생성, Hill 한 칸 페인트 시 주변 벽/코너 오토타일.
// Tools > Map > Cliff Map Editor
public class CliffMapEditorWindow : EditorWindow
{
    enum BrushMode
    {
        PaintTop,
        EraseTop,
        PaintRamp,
        EraseRamp,
        PlaceHill,
        EraseHill,
        PaintGround,
        EraseGround,
    }

    CliffPainter painter;
    BrushMode brushMode = BrushMode.PaintTop;
    int rampDirection = 0;
    int brushSize = 1;
    int groundFillSize = 5;
    int paintTopLayer; // 0=1층, 1=2층…
    bool groundFillMode; // true면 클릭 한 번에 N×N 채우기
    bool painting;
    bool needsRebuild;
    Vector2 scroll;

    // 드래그 중 브러시가 지나온 칸 — 그려질 위치 미리보기
    readonly HashSet<CliffPainter.CellCoord> strokePreviewCells = new HashSet<CliffPainter.CellCoord>();
    readonly List<CliffPainter.CellCoord> strokePreviewList = new List<CliffPainter.CellCoord>(256);
    readonly List<CliffPainter.CellCoord> hoverPaintCells = new List<CliffPainter.CellCoord>(32);
    readonly List<CliffPainter.CellCoord> hoverConnectorCells = new List<CliffPainter.CellCoord>(16);

    bool hasHoverCell;
    CliffPainter.CellCoord lastHoverCell;

    // false면 씬 뷰 클릭/드래그를 가로채지 않아 다른 오브젝트 작업 가능
    bool paintEnabled = true;

    static readonly string[] RampDirectionLabels = { "북 (+Z)", "동 (+X)", "남 (-Z)", "서 (-X)" };

    [MenuItem("Tools/Map/Cliff Map Editor")]
    static void Open()
    {
        CliffMapEditorWindow window = GetWindow<CliffMapEditorWindow>(false, "Cliff Map", true);
        window.minSize = new Vector2(360f, 520f);
        window.Show();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
        AutoFindPainter();
        if (painter != null)
            painter.EnsureLookup();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;

        if (needsRebuild && painter != null)
            RebuildNow();
    }

    void OnUndoRedo()
    {
        if (painter == null)
            return;

        painter.InvalidateLookup();
        painter.EnsureLookup();
        painter.RebuildGeometry();
        needsRebuild = false;
        SceneView.RepaintAll();
    }

    void AutoFindPainter()
    {
        if (painter == null)
            painter = FindObjectOfType<CliffPainter>();
        if (painter != null)
            painter.EnsureLookup();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("절벽 맵 에디터", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "• Paint Top / Place Hill: 층 슬라이더 공유 (0=1층, 1=2층…)\n" +
            "• 2층+ Top: 아랫층 가장자리 한 칸 안쪽만 (In 코너 Top 위 제외) / Hill: 해당 층 Top 가장자리\n" +
            "• 1층 Top/Hill 칠하면 Ground 제거, Ground 없는 칸엔 1층 벽 미생성\n" +
            "• 월 슬롯은 Ground 데이터는 유지하되 Ground 메시는 Top처럼 생략\n" +
            "• 페인트 OFF(B): 창을 켠 채로 다른 오브젝트 작업",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        painter = (CliffPainter)EditorGUILayout.ObjectField("Cliff Painter", painter, typeof(CliffPainter), true);

        if (EditorGUI.EndChangeCheck() && painter != null)
            Selection.activeGameObject = painter.gameObject;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("씬에서 찾기"))
            AutoFindPainter();
        if (GUILayout.Button("새로 만들기"))
            CreatePainterInScene();
        EditorGUILayout.EndHorizontal();

        if (painter == null)
        {
            EditorGUILayout.HelpBox("CliffPainter를 만들거나 지정하세요.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space(6f);
        Undo.RecordObject(painter, "Cliff Painter Settings");

        painter.tileSet = (CliffTileSet)EditorGUILayout.ObjectField(
            "Cliff Tile Set",
            painter.tileSet,
            typeof(CliffTileSet),
            false);

        painter.hillTileSet = (HillTileSet)EditorGUILayout.ObjectField(
            "Hill Tile Set",
            painter.hillTileSet,
            typeof(HillTileSet),
            false);

        EditorGUILayout.BeginHorizontal();
        if (painter.tileSet == null && GUILayout.Button("Cliff Tile Set 생성"))
            CreateDefaultCliffTileSet();
        if (painter.hillTileSet == null && GUILayout.Button("Hill Tile Set 생성"))
            CreateDefaultHillTileSet();
        EditorGUILayout.EndHorizontal();

        painter.gridOrigin = EditorGUILayout.Vector3Field("Grid Origin", painter.gridOrigin);
        painter.baseHeight = EditorGUILayout.FloatField("저지대 높이 (Y)", painter.baseHeight);

        if (painter.tileSet != null)
        {
            EditorGUILayout.LabelField(
                $"1층 Top: {painter.TopSurfaceHeight:0.##}  /  절벽면: {painter.EdgeSurfaceHeight:0.##}");
            EditorGUILayout.LabelField($"층 간격: {painter.LayerStepHeight:0.##}");
            if (paintTopLayer > 0)
            {
                EditorGUILayout.LabelField(
                    $"{paintTopLayer + 1}층 Top: {painter.GetTopSurfaceHeight(paintTopLayer):0.##}  /  " +
                    $"그 아래 벽: {painter.GetEdgeSurfaceHeight(paintTopLayer):0.##}");
            }

            if (painter.CliffHeight <= 0.0001f && painter.LayerStepHeight <= 0.0001f)
            {
                EditorGUILayout.HelpBox(
                    "Cliff Tile Set의 Cliff Height 또는 Layer Step Height를 설정하세요. " +
                    "둘 다 0이면 윗층 높이가 1층과 같습니다.",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("브러시", EditorStyles.boldLabel);
        DrawPaintToggle();
        DrawBrushModeToolbars();

        EditorGUI.BeginDisabledGroup(!paintEnabled);

        if (brushMode == BrushMode.PaintTop || brushMode == BrushMode.EraseTop ||
            brushMode == BrushMode.PlaceHill || brushMode == BrushMode.EraseHill)
        {
            paintTopLayer = EditorGUILayout.IntSlider("층 (0=1층)", paintTopLayer, 0, 8);
            if (brushMode == BrushMode.PaintTop || brushMode == BrushMode.EraseTop)
            {
                EditorGUILayout.HelpBox(
                    paintTopLayer == 0
                        ? "1층 Top: Ground/빈 칸에 칠합니다."
                        : $"{paintTopLayer + 1}층 Top: 아랫층 가장자리 한 칸 안쪽만 (In 코너 Top 위 제외).",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    paintTopLayer == 0
                        ? "1층 Hill: Ground 있는 Top 가장자리에만."
                        : $"{paintTopLayer + 1}층 Hill: 해당 층 Top 가장자리(아랫층 위 링)에 칠합니다.",
                    MessageType.None);
            }
        }

        if (brushMode == BrushMode.PaintTop || brushMode == BrushMode.EraseTop ||
            brushMode == BrushMode.PlaceHill || brushMode == BrushMode.EraseHill)
        {
            brushSize = EditorGUILayout.IntSlider("브러시 크기  [ ]", brushSize, 1, 5);
            EditorGUILayout.LabelField("단축키: [ 축소  /  ] 확대", EditorStyles.miniLabel);
        }

        if (brushMode == BrushMode.PaintGround || brushMode == BrushMode.EraseGround)
        {
            groundFillMode = EditorGUILayout.ToggleLeft("N×N 채우기 (클릭 1회)", groundFillMode);
            if (groundFillMode)
            {
                groundFillSize = EditorGUILayout.IntSlider("채우기 크기 N  [ ]", groundFillSize, 1, 64);
                EditorGUILayout.LabelField("단축키: [ 축소  /  ] 확대", EditorStyles.miniLabel);
            }
            else
            {
                brushSize = EditorGUILayout.IntSlider("브러시 크기  [ ]", brushSize, 1, 16);
                EditorGUILayout.LabelField("단축키: [ 축소  /  ] 확대", EditorStyles.miniLabel);
            }

            if (painter.tileSet != null && painter.tileSet.ground == null)
                EditorGUILayout.HelpBox("Cliff Tile Set에 Ground 프리팹을 지정하세요.", MessageType.Warning);

            if (brushMode == BrushMode.EraseGround)
            {
                EditorGUILayout.HelpBox(
                    "Ground 지우개: 해당 칸의 Top / Hill / Ramp / Ground를 모두 지웁니다.",
                    MessageType.Info);
            }
        }

        if (brushMode == BrushMode.EraseTop)
        {
            EditorGUILayout.HelpBox(
                paintTopLayer == 0
                    ? "Top 지우개: 칸의 모든 Top과 위 지형(Hill/Ramp)을 지우고 Ground로 채웁니다."
                    : $"Top 지우개: {paintTopLayer + 1}층 이상 Top과 그 층 Hill을 지웁니다.",
                MessageType.Info);
        }

        if (brushMode == BrushMode.PaintRamp || brushMode == BrushMode.EraseRamp)
            rampDirection = EditorGUILayout.Popup("램프 방향", rampDirection, RampDirectionLabels);

        if (brushMode == BrushMode.PlaceHill || brushMode == BrushMode.EraseHill)
        {
            EditorGUILayout.HelpBox(
                "선택한 층 Top의 벽/코너 슬롯에 H를 칠합니다.\n" +
                "직교 이웃 → WxH/HxW, 코너 → HXH In/Out.",
                MessageType.None);

            if (painter.hillTileSet == null)
                EditorGUILayout.HelpBox("Hill Tile Set을 지정하세요.", MessageType.Warning);
        }

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("데이터", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Top 셀: {painter.TopCount}");
        EditorGUILayout.LabelField($"바닥: {(painter.groundCells != null ? painter.groundCells.Count : 0)}");
        EditorGUILayout.LabelField($"언덕: {(painter.hills != null ? painter.hills.Count : 0)}");
        EditorGUILayout.LabelField($"램프: {(painter.ramps != null ? painter.ramps.Count : 0)}");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("지금 재생성", GUILayout.Height(28f)))
            RebuildNow();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Top/Hill만 지우기", GUILayout.Height(28f)))
        {
            if (EditorUtility.DisplayDialog(
                    "Cliff Map",
                    "바닥(Ground)은 남기고 Top / Hill / 램프만 지울까요?",
                    "지우기",
                    "취소"))
            {
                Undo.RecordObject(painter, "Clear Tops And Hills");
                painter.ClearTopsAndHills();
                RebuildNow();
                MarkDirty();
            }
        }

        if (GUILayout.Button("전부 지우기", GUILayout.Height(28f)))
        {
            if (EditorUtility.DisplayDialog("Cliff Map", "모든 Top/바닥/언덕/램프를 지울까요?", "지우기", "취소"))
            {
                Undo.RecordObject(painter, "Clear Cliffs");
                painter.ClearAllTops();
                RebuildNow();
                MarkDirty();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    void DrawPaintToggle()
    {
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = paintEnabled
            ? new Color(0.35f, 0.85f, 0.45f)
            : new Color(0.75f, 0.4f, 0.4f);

        string label = paintEnabled
            ? "페인트 ON  (단축키 B — 끄면 씬 선택 가능)"
            : "페인트 OFF (단축키 B — 켜면 칠하기)";

        if (GUILayout.Button(label, GUILayout.Height(32f)))
            SetPaintEnabled(!paintEnabled);

        GUI.backgroundColor = prev;

        if (!paintEnabled)
            EditorGUILayout.HelpBox("페인트가 꺼져 있습니다. 씬에서 다른 오브젝트를 선택·편집할 수 있습니다.", MessageType.Warning);
    }

    void SetPaintEnabled(bool enabled)
    {
        if (paintEnabled == enabled)
            return;

        paintEnabled = enabled;

        if (!paintEnabled)
        {
            painting = false;
            strokePreviewCells.Clear();
            strokePreviewList.Clear();
            if (needsRebuild)
                RebuildNow();
        }

        Repaint();
        SceneView.RepaintAll();
    }

    void DrawBrushModeToolbars()
    {
        int row1 = GUILayout.Toolbar(
            (int)brushMode <= 3 ? (int)brushMode : -1,
            new[] { "Paint Top", "Erase Top", "Ramp", "Erase Ramp" });

        if (row1 >= 0)
            brushMode = (BrushMode)row1;

        int row2 = GUILayout.Toolbar(
            (int)brushMode >= 4 && (int)brushMode <= 5 ? (int)brushMode - 4 : -1,
            new[] { "Place Hill", "Erase Hill" });

        if (row2 >= 0)
            brushMode = (BrushMode)(row2 + 4);

        int row3 = GUILayout.Toolbar(
            (int)brushMode >= 6 ? (int)brushMode - 6 : -1,
            new[] { "Paint Ground", "Erase Ground" });

        if (row3 >= 0)
            brushMode = (BrushMode)(row3 + 6);
    }

    void CreatePainterInScene()
    {
        GameObject go = new GameObject("CliffPainter");
        Undo.RegisterCreatedObjectUndo(go, "Create CliffPainter");
        painter = go.AddComponent<CliffPainter>();
        Selection.activeGameObject = go;
    }

    void CreateDefaultCliffTileSet()
    {
        string folder = EnsureMapsFolder();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/CliffTileSet.asset");
        CliffTileSet set = CreateInstance<CliffTileSet>();
        set.tileSize = 8f;
        set.cliffHeight = 4.8f;
        set.top = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/Top.prefab");
        if (set.top == null)
            set.top = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/Hill.prefab");
        set.straight = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/Wall1.prefab");
        set.outerCorner = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/WXW_Out.prefab");
        set.innerCorner = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/WxW_In.prefab");
        set.ground = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/Ground.prefab");
        AssetDatabase.CreateAsset(set, path);
        AssetDatabase.SaveAssets();
        painter.tileSet = set;
        EditorGUIUtility.PingObject(set);
    }

    void CreateDefaultHillTileSet()
    {
        string folder = EnsureMapsFolder();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/HillTileSet.asset");
        HillTileSet set = CreateInstance<HillTileSet>();
        set.hill = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/Hill.prefab");
        set.wxh = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/HXW_In.prefab");
        set.hxwOuter = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/HXW_Out.prefab");
        set.hxwInner = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/HXW_In.prefab");
        set.toOuterCorner = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/HXH_Out.prefab");
        set.toInnerCorner = LoadPrefab("Assets/Artasseet/Prefabs/Map/MapPrefab/HXH_In.prefab");
        AssetDatabase.CreateAsset(set, path);
        AssetDatabase.SaveAssets();
        painter.hillTileSet = set;
        EditorGUIUtility.PingObject(set);
    }

    static string EnsureMapsFolder()
    {
        string folder = "Assets/Data/Maps";
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Data", "Maps");
        return folder;
    }

    static GameObject LoadPrefab(string path)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (painter == null)
            return;

        Event e = Event.current;

        // 단축키 B: 페인트 ON/OFF (꺼져 있어도 동작)
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.B && !e.control && !e.alt && !e.shift)
        {
            if (EditorGUIUtility.editingTextField)
                return;

            SetPaintEnabled(!paintEnabled);
            e.Use();
            return;
        }

        // 단축키 [ / ]: 브러시(또는 Ground 채우기) 크기 조절
        if (e.type == EventType.KeyDown &&
            (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket) &&
            !e.control && !e.alt && !e.shift)
        {
            if (EditorGUIUtility.editingTextField)
                return;

            int delta = e.keyCode == KeyCode.RightBracket ? 1 : -1;
            if (AdjustBrushSizeByHotkey(delta))
            {
                Repaint();
                SceneView.RepaintAll();
                e.Use();
            }

            return;
        }

        if (!paintEnabled)
            return;

        painter.EnsureLookup();

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlId);

        // 미리보기는 Repaint에서만 그림 (Layout마다 RepaintAll 하던 것이 주요 병목)
        if (e.type == EventType.Repaint)
            DrawHoverPreview(e);
        else if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            RequestHoverRepaintIfCellChanged(e);

        if (e.alt)
            return;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0 && !e.control)
                {
                    painting = true;
                    strokePreviewCells.Clear();
                    strokePreviewList.Clear();
                    ApplyBrushAtMouse(e);
                    if (IsGroundFillBrush())
                    {
                        painting = false;
                        strokePreviewCells.Clear();
                        strokePreviewList.Clear();
                    }
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (painting && e.button == 0 && !IsGroundFillBrush())
                {
                    ApplyBrushAtMouse(e);
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (e.button == 0 && painting)
                {
                    painting = false;
                    strokePreviewCells.Clear();
                    strokePreviewList.Clear();
                    if (needsRebuild)
                        RebuildNow();
                    e.Use();
                }
                break;

            case EventType.KeyDown:
                if (e.keyCode == KeyCode.R &&
                    (brushMode == BrushMode.PaintRamp || brushMode == BrushMode.EraseRamp))
                {
                    rampDirection = (rampDirection + 1) % 4;
                    Repaint();
                    e.Use();
                }
                break;
        }
    }

    void RequestHoverRepaintIfCellChanged(Event e)
    {
        if (!TryGetCellUnderMouse(e, out CliffPainter.CellCoord cell))
        {
            if (hasHoverCell)
            {
                hasHoverCell = false;
                SceneView.RepaintAll();
            }
            return;
        }

        if (hasHoverCell && cell.Equals(lastHoverCell))
            return;

        hasHoverCell = true;
        lastHoverCell = cell;
        SceneView.RepaintAll();
    }

    void DrawHoverPreview(Event e)
    {
        float size = painter.TileSize;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

        Color color = brushMode switch
        {
            BrushMode.PaintTop => new Color(0.2f, 1f, 0.4f, 0.35f),
            BrushMode.EraseTop => new Color(1f, 0.3f, 0.2f, 0.35f),
            BrushMode.PaintRamp => new Color(1f, 0.85f, 0.2f, 0.4f),
            BrushMode.EraseRamp => new Color(1f, 0.5f, 0.1f, 0.35f),
            BrushMode.PlaceHill => new Color(0.3f, 0.75f, 1f, 0.4f),
            BrushMode.EraseHill => new Color(1f, 0.4f, 0.7f, 0.35f),
            BrushMode.PaintGround => new Color(0.7f, 0.5f, 0.25f, 0.4f),
            BrushMode.EraseGround => new Color(0.9f, 0.35f, 0.2f, 0.35f),
            _ => new Color(1f, 1f, 1f, 0.25f),
        };

        bool useEdgeHeight = IsHillBrush() || IsGroundBrush();
        float previewY = useEdgeHeight
            ? painter.GetEdgeSurfaceHeight(IsHillBrush() ? paintTopLayer : 0) + 0.2f
            : brushMode == BrushMode.PaintTop || brushMode == BrushMode.EraseTop
                ? painter.GetTopSurfaceHeight(paintTopLayer) + 0.2f
                : painter.GetTopSurfaceHeight(0) + 0.2f;

        if (painting && strokePreviewList.Count > 0)
        {
            Color trailColor = color;
            trailColor.a = Mathf.Min(0.55f, color.a + 0.2f);
            Color trailOutline = new Color(1f, 1f, 1f, 0.85f);

            for (int i = 0; i < strokePreviewList.Count; i++)
                DrawCellPreviewAtHeight(strokePreviewList[i], size, trailColor, previewY, trailOutline);
        }

        if (!TryGetCellUnderMouse(e, out CliffPainter.CellCoord origin))
            return;

        hoverPaintCells.Clear();
        hoverConnectorCells.Clear();

        int stamp = GetStampSize();

        for (int dx = 0; dx < stamp; dx++)
        {
            for (int dz = 0; dz < stamp; dz++)
            {
                CliffPainter.CellCoord cell = new CliffPainter.CellCoord(origin.x + dx, origin.z + dz);
                hoverPaintCells.Add(cell);

                if (brushMode == BrushMode.PlaceHill && painter.CanPlaceHill(cell, paintTopLayer))
                    painter.CollectAutoConnectorNeighbors(cell, paintTopLayer, hoverConnectorCells);
            }
        }

        if (brushMode == BrushMode.PaintTop)
        {
            bool topInvalid = true;
            for (int i = 0; i < hoverPaintCells.Count; i++)
            {
                if (painter.CanPlaceTop(hoverPaintCells[i], paintTopLayer) ||
                    painter.HasTopLayer(hoverPaintCells[i], paintTopLayer))
                {
                    topInvalid = false;
                    break;
                }
            }

            if (topInvalid)
                color = new Color(1f, 0.2f, 0.2f, 0.35f);
        }

        bool hillInvalid = false;
        if (brushMode == BrushMode.PlaceHill)
        {
            hillInvalid = true;
            for (int i = 0; i < hoverPaintCells.Count; i++)
            {
                if (painter.CanPlaceHill(hoverPaintCells[i], paintTopLayer) ||
                    painter.HasHillOnLayer(hoverPaintCells[i], paintTopLayer))
                {
                    hillInvalid = false;
                    break;
                }
            }

            if (hillInvalid)
                color = new Color(1f, 0.2f, 0.2f, 0.35f);
        }

        Color hoverColor = color;
        hoverColor.a = Mathf.Min(0.65f, color.a + 0.25f);

        for (int i = 0; i < hoverPaintCells.Count; i++)
            DrawCellPreviewAtHeight(hoverPaintCells[i], size, hoverColor, previewY);

        if (brushMode == BrushMode.PlaceHill && !hillInvalid)
        {
            Color connColor = new Color(0.5f, 0.85f, 1f, 0.18f);
            float edgeY = painter.GetEdgeSurfaceHeight(paintTopLayer) + 0.2f;
            for (int i = 0; i < hoverConnectorCells.Count; i++)
            {
                if (hoverPaintCells.Contains(hoverConnectorCells[i]))
                    continue;
                DrawCellPreviewAtHeight(hoverConnectorCells[i], size, connColor, edgeY);
            }
        }
    }

    bool IsHillBrush() => brushMode == BrushMode.PlaceHill || brushMode == BrushMode.EraseHill;
    bool IsGroundBrush() => brushMode == BrushMode.PaintGround || brushMode == BrushMode.EraseGround;
    bool IsGroundFillBrush() => IsGroundBrush() && groundFillMode;

    bool AdjustBrushSizeByHotkey(int delta)
    {
        if (delta == 0)
            return false;

        if (IsGroundFillBrush())
        {
            int next = Mathf.Clamp(groundFillSize + delta, 1, 64);
            if (next == groundFillSize)
                return false;

            groundFillSize = next;
            return true;
        }

        if (brushMode == BrushMode.PaintTop || brushMode == BrushMode.EraseTop ||
            brushMode == BrushMode.PlaceHill || brushMode == BrushMode.EraseHill)
        {
            int next = Mathf.Clamp(brushSize + delta, 1, 5);
            if (next == brushSize)
                return false;

            brushSize = next;
            return true;
        }

        if (IsGroundBrush())
        {
            int next = Mathf.Clamp(brushSize + delta, 1, 16);
            if (next == brushSize)
                return false;

            brushSize = next;
            return true;
        }

        return false;
    }

    int GetStampSize()
    {
        if (IsGroundFillBrush())
            return Mathf.Max(1, groundFillSize);
        if (IsGroundBrush() || brushMode == BrushMode.PaintTop || brushMode == BrushMode.EraseTop ||
            brushMode == BrushMode.PlaceHill || brushMode == BrushMode.EraseHill)
            return Mathf.Max(1, brushSize);
        return 1;
    }

    void DrawCellPreview(
        CliffPainter.CellCoord cell,
        float size,
        Color color,
        bool useEdgeHeight,
        Color? outline = null)
    {
        float y = useEdgeHeight
            ? painter.GetEdgeSurfaceHeight(0) + 0.2f
            : painter.GetTopSurfaceHeight(0) + 0.2f;
        DrawCellPreviewAtHeight(cell, size, color, y, outline);
    }

    void DrawCellPreviewAtHeight(
        CliffPainter.CellCoord cell,
        float size,
        Color color,
        float y,
        Color? outline = null)
    {
        Vector3 center = painter.CellCenterWorld(cell);
        center.y = y;

        Handles.DrawSolidRectangleWithOutline(
            new[]
            {
                center + new Vector3(-size, 0f, -size) * 0.48f,
                center + new Vector3(-size, 0f, size) * 0.48f,
                center + new Vector3(size, 0f, size) * 0.48f,
                center + new Vector3(size, 0f, -size) * 0.48f,
            },
            color,
            outline ?? Color.white);
    }

    void ApplyBrushAtMouse(Event e)
    {
        if (!TryGetCellUnderMouse(e, out CliffPainter.CellCoord origin))
            return;

        // 같은 스탬프 영역을 연속 드래그하면 스킵
        if (painting && IsStampAlreadyInStroke(origin))
            return;

        hasHoverCell = true;
        lastHoverCell = origin;
        CollectStrokePreview(origin);

        Undo.RecordObject(painter, "Cliff Paint");
        bool changed = false;

        if (brushMode == BrushMode.PaintGround || brushMode == BrushMode.EraseGround)
        {
            int size = GetStampSize();
            changed = painter.FillGround(origin, size, erase: brushMode == BrushMode.EraseGround) > 0;
        }
        else
        {
            int stamp = GetStampSize();
            for (int dx = 0; dx < stamp; dx++)
            {
                for (int dz = 0; dz < stamp; dz++)
                {
                    CliffPainter.CellCoord cell = new CliffPainter.CellCoord(origin.x + dx, origin.z + dz);
                    changed |= ApplyBrushToCell(cell);
                }
            }
        }

        if (changed)
        {
            needsRebuild = true;
            MarkDirty();
            // 드래그 중에는 지오메트리 재생성·전체 Repaint 생략 (MouseUp에서 한 번)
            if (!painting || IsGroundFillBrush())
            {
                SceneView.RepaintAll();
                Repaint();
            }
            else
            {
                SceneView.currentDrawingSceneView?.Repaint();
            }

            if (IsGroundFillBrush() && needsRebuild)
                RebuildNow();
        }
        else if (painting)
        {
            SceneView.currentDrawingSceneView?.Repaint();
        }
    }

    void CollectStrokePreview(CliffPainter.CellCoord origin)
    {
        int stamp = GetStampSize();
        for (int dx = 0; dx < stamp; dx++)
        {
            for (int dz = 0; dz < stamp; dz++)
            {
                CliffPainter.CellCoord cell = new CliffPainter.CellCoord(origin.x + dx, origin.z + dz);
                if (strokePreviewCells.Add(cell))
                    strokePreviewList.Add(cell);
            }
        }
    }

    bool IsStampAlreadyInStroke(CliffPainter.CellCoord origin)
    {
        int stamp = GetStampSize();
        for (int dx = 0; dx < stamp; dx++)
        {
            for (int dz = 0; dz < stamp; dz++)
            {
                if (!strokePreviewCells.Contains(new CliffPainter.CellCoord(origin.x + dx, origin.z + dz)))
                    return false;
            }
        }

        return stamp > 0 && strokePreviewCells.Count > 0;
    }

    bool ApplyBrushToCell(CliffPainter.CellCoord cell)
    {
        switch (brushMode)
        {
            case BrushMode.PaintTop:
                return painter.TryAddTop(cell, paintTopLayer);
            case BrushMode.EraseTop:
                return painter.TryRemoveTop(cell, paintTopLayer);
            case BrushMode.PaintRamp:
                return painter.TrySetRamp(cell, rampDirection);
            case BrushMode.EraseRamp:
                return painter.TryRemoveRamp(cell, rampDirection);
            case BrushMode.PlaceHill:
                return painter.TryAddHill(cell, paintTopLayer);
            case BrushMode.EraseHill:
                return painter.TryRemoveHill(cell);
            case BrushMode.PaintGround:
                return painter.TryAddGround(cell);
            case BrushMode.EraseGround:
                return painter.TryEraseGroundStack(cell);
            default:
                return false;
        }
    }

    bool TryGetCellUnderMouse(Event e, out CliffPainter.CellCoord cell)
    {
        cell = default;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        float planeY = painter.baseHeight;

        if (Mathf.Abs(ray.direction.y) < 0.0001f)
            return false;

        float t = (planeY - ray.origin.y) / ray.direction.y;

        if (t < 0f)
            return false;

        Vector3 hit = ray.origin + ray.direction * t;
        cell = painter.WorldToCell(hit);
        return true;
    }

    void RebuildNow()
    {
        if (painter == null)
            return;

        painter.RebuildGeometry();
        needsRebuild = false;
        MarkDirty();
    }

    void MarkDirty()
    {
        if (painter == null)
            return;

        EditorUtility.SetDirty(painter);

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(painter.gameObject.scene);
    }
}
