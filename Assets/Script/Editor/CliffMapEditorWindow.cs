using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 스타크래프트식 맵 생성기입니다.
// Top + 벽/코너 자동 생성, Hill 한 칸 페인트 시 주변 벽/코너 오토타일.
// Tools > 맵 > 맵 생성기
public class CliffMapEditorWindow : EditorWindow
{
    enum BrushMode
    {
        PaintTop,
        EraseTop,
        PlaceHill,
        EraseHill,
        PaintGround,
        EraseGround,
    }

    CliffPainter painter;
    BrushMode brushMode = BrushMode.PaintTop;
    int brushSize = 1;
    int groundFillSize = 5;
    float hillPickHeight = float.NaN;
    int paintTopLayer; // 0=1층, 1=2층…
    bool groundFillMode; // true면 클릭 한 번에 N×N 채우기
    bool painting;
    bool needsRebuild;
    int strokeUndoGroup = -1;
    int strokeControl;
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

    // 대칭 칠하기: 켜진 축마다 브러시를 대칭 위치에도 함께 칠한다. 둘 다 켜면 4분면 대칭이 된다.
    // 중심은 격자선 좌표(칸 단위, 0.5 단위)이며 0은 그리드 원점 선이다.
    [SerializeField] bool mirrorX;
    [SerializeField] bool mirrorZ;
    [SerializeField] float mirrorCenterX;
    [SerializeField] float mirrorCenterZ;
    readonly List<CliffPainter.CellCoord> mirrorOrigins = new List<CliffPainter.CellCoord>(4);

    int MirrorAxisX2 => Mathf.RoundToInt(mirrorCenterX * 2f);
    int MirrorAxisZ2 => Mathf.RoundToInt(mirrorCenterZ * 2f);

    [MenuItem("Tools/맵/맵 생성기")]
    static void Open()
    {
        CliffMapEditorWindow window = GetWindow<CliffMapEditorWindow>(false, "맵 생성기", true);
        window.minSize = new Vector2(360f, 520f);
        window.Show();
    }

    void OnEnable()
    {
        titleContent = new GUIContent("맵 생성기");
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        AutoFindPainter();
        if (painter != null)
        {
            painter.EnsureLookup();
            CliffPainterUndo.Track(painter);
        }
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;

        FinishStroke();
    }

    // 스테이지 에디터로 맵을 치우고 다시 꺼내면 예전 맵 지형이 사라져 연결이 끊긴다.
    // 창을 다시 열지 않아도 새로 꺼낸 맵의 지형을 자동으로 잡는다.
    void OnHierarchyChanged()
    {
        if (painter != null)
            return;

        painting = false;
        strokeUndoGroup = -1;
        ReleaseStrokeControl();
        AutoFindPainter();
        if (painter != null)
            CliffPainterUndo.Track(painter);
        Repaint();
        SceneView.RepaintAll();
    }

    void OnUndoRedo()
    {
        if (painter == null)
            return;

        painting = false;
        strokeUndoGroup = -1;
        ReleaseStrokeControl();
        strokePreviewCells.Clear();
        strokePreviewList.Clear();
        needsRebuild = false;
        Repaint();
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

        EditorGUILayout.LabelField("맵 생성기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "• Paint Top: -8층~9층 선택 / Place Hill: 벽의 층 자동 감지\n" +
            "• 2층+ Top: 아랫층 가장자리 한 칸 안쪽만 (In 코너 Top 위 제외) / Hill: 해당 층 Top 가장자리\n" +
            "• 새 Top은 바닥이나 언덕이 있는 칸에 배치 / 기존 Top은 높이 변경 가능\n" +
            "• 1층 Top/Hill 칠하면 Ground 대체, 실행 취소하면 원래 바닥 복구\n" +
            "• 월 슬롯은 Ground 데이터는 유지하되 Ground 메시는 Top처럼 생략\n" +
            "• 페인트 OFF(B): 창을 켠 채로 다른 오브젝트 작업",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        CliffPainter selectedPainter = (CliffPainter)EditorGUILayout.ObjectField("맵 지형", painter, typeof(CliffPainter), true);

        if (EditorGUI.EndChangeCheck())
        {
            FinishStroke();
            painter = selectedPainter;
            CliffPainterUndo.Track(painter);
            if (painter != null) Selection.activeGameObject = painter.gameObject;
        }

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
        // 창을 그릴 때마다 Undo를 예약하지 않고 실제 설정 변경만 기록한다.
        var settings = new SerializedObject(painter);
        settings.Update();
        EditorGUILayout.PropertyField(settings.FindProperty("tileSet"), new GUIContent("절벽 타일 세트"));
        EditorGUILayout.PropertyField(settings.FindProperty("hillTileSet"), new GUIContent("언덕 타일 세트"));
        EditorGUILayout.PropertyField(settings.FindProperty("connectTileNormals"), new GUIContent("타일 경계 노멀 연결"));
        if (settings.FindProperty("connectTileNormals").boolValue)
        {
            EditorGUILayout.PropertyField(settings.FindProperty("normalJoinDistance"), new GUIContent("경계 탐색 거리"));
            EditorGUILayout.PropertyField(settings.FindProperty("normalJoinAngle"), new GUIContent("노멀 연결 최대 각도"));
            EditorGUILayout.HelpBox("설정 후 '지금 재생성'을 누르세요. 원본 FBX의 Read/Write Enabled가 필요합니다. 타일 간격과 실제 폭을 먼저 맞추세요.", MessageType.Info);
        }
        EditorGUILayout.PropertyField(settings.FindProperty("gridOrigin"), new GUIContent("그리드 원점"));
        EditorGUILayout.PropertyField(settings.FindProperty("baseHeight"), new GUIContent("저지대 높이 (Y)"));
        if (settings.ApplyModifiedProperties()) MarkDirty();

        EditorGUILayout.BeginHorizontal();
        if (painter.tileSet == null && GUILayout.Button("Cliff Tile Set 생성"))
            CreateDefaultCliffTileSet();
        if (painter.hillTileSet == null && GUILayout.Button("Hill Tile Set 생성"))
            CreateDefaultHillTileSet();
        EditorGUILayout.EndHorizontal();

        if (painter.tileSet != null)
        {
            EditorGUILayout.LabelField(
                $"1층 Top: {painter.TopSurfaceHeight:0.##}  /  절벽면: {painter.EdgeSurfaceHeight:0.##}");
            EditorGUILayout.LabelField($"층 간격: {painter.LayerStepHeight:0.##}");
            if (paintTopLayer != 0)
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

        if (brushMode == BrushMode.PaintTop || brushMode == BrushMode.EraseTop)
        {
            paintTopLayer = EditorGUILayout.IntSlider("층", paintTopLayer + 1, -8, 9) - 1;
            EditorGUILayout.HelpBox(
                paintTopLayer <= 0
                    ? $"{paintTopLayer + 1}층 Top: 지정한 높이로 칠합니다. 0층 이하는 기존 높이도 바꿉니다."
                    : $"{paintTopLayer + 1}층 Top: 아랫층 가장자리 한 칸 안쪽만 배치합니다.",
                MessageType.None);
        }
        else if (IsHillBrush())
        {
            EditorGUILayout.HelpBox("언덕은 가리킨 벽의 층을 자동 감지합니다. 층 선택은 필요하지 않습니다.", MessageType.Info);
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
                    "Ground 지우개: 해당 칸의 Top / Hill / Ground를 모두 지웁니다.",
                    MessageType.Info);
            }
        }

        if (brushMode == BrushMode.EraseTop)
        {
            EditorGUILayout.HelpBox(
                paintTopLayer <= 0
                    ? "Top 지우개: 선택 칸을 바닥으로 바꾸고 주변 8칸의 Top을 벽·코너 자리로 바꿉니다. 벽·코너·언덕을 찍어도 같은 규칙으로 경계가 물러납니다. 기존 바닥은 유지하며 타일 크기는 변경하지 않습니다."
                    : $"Top 지우개: {paintTopLayer + 1}층 이상을 지우고 주변 8칸의 같은 층 Top을 벽·코너 자리로 바꿉니다. 벽·코너·언덕을 찍어도 같은 규칙으로 경계가 물러납니다. 아랫층은 유지합니다.",
                MessageType.Info);
        }

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

        DrawMirrorSettings();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("데이터", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Top 셀: {painter.TopCount}");
        EditorGUILayout.LabelField($"바닥: {(painter.groundCells != null ? painter.groundCells.Count : 0)}");
        EditorGUILayout.LabelField($"언덕: {(painter.hills != null ? painter.hills.Count : 0)}");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("지금 재생성", GUILayout.Height(28f)))
            RebuildNow();
        if (GUILayout.Button(new GUIContent("맵 중심을 원점으로",
                "칠한 칸이 그리드 원점을 중심으로 오도록 칸 좌표를 옮기고, 그리드 원점을 (0, 높이, 0)·맵 루트를 원점으로 맞춘 뒤 다시 생성합니다. 맵 루트 아래 다른 오브젝트도 지형과 함께 옮깁니다."),
                GUILayout.Height(28f)))
            CenterMapOnOrigin();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Top/Hill만 지우기", GUILayout.Height(28f)))
        {
            if (EditorUtility.DisplayDialog(
                    "맵 생성기",
                    "바닥(Ground)은 남기고 Top / Hill만 지울까요?",
                    "지우기",
                    "취소"))
            {
                FinishStroke();
                int group = CliffPainterUndo.Begin(painter, "고지대와 언덕 지우기");
                painter.ClearTopsAndHills();
                RebuildNow();
                MarkDirty();
                CliffPainterUndo.End(group);
            }
        }

        if (GUILayout.Button("전부 지우기", GUILayout.Height(28f)))
        {
            if (EditorUtility.DisplayDialog("맵 생성기", "모든 Top/바닥/언덕을 지울까요?", "지우기", "취소"))
            {
                FinishStroke();
                int group = CliffPainterUndo.Begin(painter, "전체 지형 지우기");
                painter.ClearAllTops();
                RebuildNow();
                MarkDirty();
                CliffPainterUndo.End(group);
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

    void DrawMirrorSettings()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("대칭", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        mirrorX = EditorGUILayout.ToggleLeft(new GUIContent("X축 대칭 (좌우 반전)",
            "칠한 칸을 세로 대칭선 기준으로 좌우(월드 X) 반대편에도 칠합니다."), mirrorX);
        mirrorZ = EditorGUILayout.ToggleLeft(new GUIContent("Y축 대칭 (앞뒤 반전, 월드 Z)",
            "칠한 칸을 가로 대칭선 기준으로 앞뒤(월드 Z, 위에서 본 세로) 반대편에도 칠합니다."), mirrorZ);

        EditorGUI.BeginDisabledGroup(!mirrorX && !mirrorZ);
        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth = 70f;
        mirrorCenterX = SnapHalf(EditorGUILayout.FloatField(new GUIContent("중심 X",
            "대칭선의 X 위치(칸 단위, 0.5 단위)입니다. 0은 그리드 원점 선, 0.5는 0번 칸 가운데입니다."), mirrorCenterX));
        mirrorCenterZ = SnapHalf(EditorGUILayout.FloatField(new GUIContent("중심 Z",
            "대칭선의 Z 위치(칸 단위, 0.5 단위)입니다. 0은 그리드 원점 선, 0.5는 0번 칸 가운데입니다."), mirrorCenterZ));
        EditorGUIUtility.labelWidth = 0f;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("칠한 범위 중심으로", "지금까지 칠한 칸 범위의 정중앙을 대칭 중심으로 잡습니다.")))
            SetMirrorCenterToPaintedBounds();
        if (GUILayout.Button(new GUIContent("그리드 원점으로", "대칭 중심을 그리드 원점 선(0, 0)으로 되돌립니다.")))
        {
            mirrorCenterX = 0f;
            mirrorCenterZ = 0f;
        }
        EditorGUILayout.EndHorizontal();

        if (mirrorX && mirrorZ)
            EditorGUILayout.HelpBox("두 축이 모두 켜져 4분면 대칭으로 칠합니다.", MessageType.None);

        EditorGUILayout.LabelField("이미 칠한 맵을 반대편에 복사", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!mirrorX);
        if (GUILayout.Button(new GUIContent("왼쪽 → 오른쪽", "대칭선 왼쪽(X 작은 쪽) 지형을 오른쪽에 반전 복사합니다. 오른쪽의 기존 지형은 지워집니다.")))
            MirrorCopy(true, false);
        if (GUILayout.Button(new GUIContent("오른쪽 → 왼쪽", "대칭선 오른쪽(X 큰 쪽) 지형을 왼쪽에 반전 복사합니다. 왼쪽의 기존 지형은 지워집니다.")))
            MirrorCopy(true, true);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!mirrorZ);
        if (GUILayout.Button(new GUIContent("아래 → 위", "대칭선 아래(Z 작은 쪽) 지형을 위에 반전 복사합니다. 위쪽의 기존 지형은 지워집니다.")))
            MirrorCopy(false, false);
        if (GUILayout.Button(new GUIContent("위 → 아래", "대칭선 위(Z 큰 쪽) 지형을 아래에 반전 복사합니다. 아래쪽의 기존 지형은 지워집니다.")))
            MirrorCopy(false, true);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();
    }

    static float SnapHalf(float value) => Mathf.Round(value * 2f) * 0.5f;

    void SetMirrorCenterToPaintedBounds()
    {
        if (!painter.TryGetPaintedCellBounds(out CliffPainter.CellCoord min, out CliffPainter.CellCoord max))
        {
            ShowNotification(new GUIContent("칠한 칸이 없습니다."));
            return;
        }

        // 칸 min~max의 바깥 격자선은 min과 max+1이므로 그 가운데가 중심이다.
        mirrorCenterX = (min.x + max.x + 1) * 0.5f;
        mirrorCenterZ = (min.z + max.z + 1) * 0.5f;
        SceneView.RepaintAll();
    }

    // 한쪽 절반의 지형을 대칭선 반대편으로 반전 복사한다.
    void MirrorCopy(bool alongX, bool fromHighSide)
    {
        FinishStroke();
        string axis = alongX ? $"X = {mirrorCenterX:0.#}" : $"Z = {mirrorCenterZ:0.#}";
        if (!EditorUtility.DisplayDialog("맵 생성기",
                $"대칭선({axis}) 기준으로 한쪽 지형을 반대편에 복사합니다. 반대편의 기존 Top/언덕/바닥은 지워집니다.",
                "복사", "취소"))
            return;

        int group = CliffPainterUndo.Begin(painter, "맵 대칭 복사");
        if (alongX)
            painter.MirrorCells(true, MirrorAxisX2, fromHighSide);
        else
            painter.MirrorCells(false, MirrorAxisZ2, fromHighSide);
        RebuildNow();
        MarkDirty();
        CliffPainterUndo.End(group);
        SceneView.RepaintAll();
    }

    // 브러시 스탬프(왼쪽 아래 칸 origin, 크기 stamp)의 대칭 위치 스탬프 원점을 모두 모은다.
    List<CliffPainter.CellCoord> GetMirroredOrigins(CliffPainter.CellCoord origin, int stamp)
    {
        mirrorOrigins.Clear();
        mirrorOrigins.Add(origin);
        int mx = MirrorAxisX2 - origin.x - stamp;
        int mz = MirrorAxisZ2 - origin.z - stamp;
        if (mirrorX)
            AddDistinct(mirrorOrigins, new CliffPainter.CellCoord(mx, origin.z));
        if (mirrorZ)
            AddDistinct(mirrorOrigins, new CliffPainter.CellCoord(origin.x, mz));
        if (mirrorX && mirrorZ)
            AddDistinct(mirrorOrigins, new CliffPainter.CellCoord(mx, mz));
        return mirrorOrigins;
    }

    static void AddDistinct(List<CliffPainter.CellCoord> list, CliffPainter.CellCoord cell)
    {
        if (!list.Contains(cell))
            list.Add(cell);
    }

    void DrawMirrorAxes()
    {
        if ((!mirrorX && !mirrorZ) || !painter.TryGetPaintedCellBounds(out CliffPainter.CellCoord min, out CliffPainter.CellCoord max))
            return;

        float size = painter.TileSize;
        float y = painter.GetTopSurfaceHeight(0) + 0.3f;
        Vector3 o = painter.gridOrigin;
        // 칠한 범위보다 두 칸 넓게 그어 맵 밖에서도 대칭선이 보이게 한다.
        float x0 = o.x + (Mathf.Min(min.x, mirrorCenterX) - 2f) * size;
        float x1 = o.x + (Mathf.Max(max.x + 1, mirrorCenterX) + 2f) * size;
        float z0 = o.z + (Mathf.Min(min.z, mirrorCenterZ) - 2f) * size;
        float z1 = o.z + (Mathf.Max(max.z + 1, mirrorCenterZ) + 2f) * size;
        Color prev = Handles.color;
        Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        if (mirrorX)
        {
            float x = o.x + mirrorCenterX * size;
            Handles.DrawAAPolyLine(4f, new Vector3(x, y, z0), new Vector3(x, y, z1));
        }
        if (mirrorZ)
        {
            float z = o.z + mirrorCenterZ * size;
            Handles.DrawAAPolyLine(4f, new Vector3(x0, y, z), new Vector3(x1, y, z));
        }
        Handles.color = prev;
    }

    void SetPaintEnabled(bool enabled)
    {
        if (paintEnabled == enabled)
            return;

        paintEnabled = enabled;

        if (!paintEnabled)
        {
            FinishStroke();
        }

        Repaint();
        SceneView.RepaintAll();
    }

    void DrawBrushModeToolbars()
    {
        int row1 = GUILayout.Toolbar(
            (int)brushMode <= 1 ? (int)brushMode : -1,
            new[] { "Top 칠하기", "Top 지우기" });

        if (row1 >= 0)
            brushMode = (BrushMode)row1;

        int row2 = GUILayout.Toolbar(
            (int)brushMode >= 2 && (int)brushMode <= 3 ? (int)brushMode - 2 : -1,
            new[] { "언덕 배치", "언덕 지우기" });

        if (row2 >= 0)
            brushMode = (BrushMode)(row2 + 2);

        int row3 = GUILayout.Toolbar(
            (int)brushMode >= 4 ? (int)brushMode - 4 : -1,
            new[] { "바닥 칠하기", "바닥 지우기" });

        if (row3 >= 0)
            brushMode = (BrushMode)(row3 + 4);
    }

    void CreatePainterInScene()
    {
        FinishStroke();
        GameObject go = new GameObject("CliffPainter");
        Undo.RegisterCreatedObjectUndo(go, "Create CliffPainter");
        painter = go.AddComponent<CliffPainter>();
        CliffPainterUndo.Track(painter);
        Selection.activeGameObject = go;
    }

    void CreateDefaultCliffTileSet()
    {
        string folder = EnsureMapsFolder();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/CliffTileSet.asset");
        CliffTileSet set = CreateInstance<CliffTileSet>();
        set.tileSize = 8f;
        set.cliffHeight = 4.8f;
        set.top = LoadMapPrefab("Top.prefab");
        if (set.top == null)
            set.top = LoadMapPrefab("Hill.prefab");
        set.straight = LoadMapPrefab("Wall.prefab") ?? LoadMapPrefab("Wall1.prefab");
        set.outerCorner = LoadMapPrefab("WXW_Out.prefab");
        set.innerCorner = LoadMapPrefab("WxW_In.prefab");
        set.ground = LoadMapPrefab("Ground.prefab");
        AssetDatabase.CreateAsset(set, path);
        AssetDatabase.SaveAssets();
        int group = CliffPainterUndo.Begin(painter, "절벽 타일 세트 연결");
        painter.tileSet = set;
        MarkDirty();
        CliffPainterUndo.End(group);
        EditorGUIUtility.PingObject(set);
    }

    void CreateDefaultHillTileSet()
    {
        string folder = EnsureMapsFolder();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/HillTileSet.asset");
        HillTileSet set = CreateInstance<HillTileSet>();
        set.hill = LoadMapPrefab("Hill.prefab");
        set.wxh = LoadMapPrefab("WXH.prefab");
        set.hxwOuter = LoadMapPrefab("HXW_Out.prefab");
        set.hxwInner = LoadMapPrefab("HXW_In.prefab");
        set.toOuterCorner = LoadMapPrefab("HXH_Out.prefab");
        set.toInnerCorner = LoadMapPrefab("HXH_In.prefab");
        AssetDatabase.CreateAsset(set, path);
        AssetDatabase.SaveAssets();
        int group = CliffPainterUndo.Begin(painter, "언덕 타일 세트 연결");
        painter.hillTileSet = set;
        MarkDirty();
        CliffPainterUndo.End(group);
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

    static GameObject LoadMapPrefab(string name)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Artasseet/Map/MapPrefab/" + name)
            ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Artasseet/Prefabs/Map/MapPrefab/" + name);
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (painter == null || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Event e = Event.current;
        // Alt 상태나 씬 밖에서 버튼을 놓아도 진행 중인 스트로크를 마무리한다.
        if (painting && ((e.rawType == EventType.MouseUp && e.button == 0) ||
            (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)))
        {
            FinishStroke();
            e.Use();
            return;
        }

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
                    FinishStroke();
                    painting = true;
                    strokeControl = controlId;
                    GUIUtility.hotControl = controlId;
                    strokePreviewCells.Clear();
                    strokePreviewList.Clear();
                    ApplyBrushAtMouse(e);
                    if (IsGroundFillBrush())
                    {
                        FinishStroke();
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
                    FinishStroke();
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
        DrawMirrorAxes();

        Color color = brushMode switch
        {
            BrushMode.PaintTop => new Color(0.2f, 1f, 0.4f, 0.35f),
            BrushMode.EraseTop => new Color(1f, 0.3f, 0.2f, 0.35f),
            BrushMode.PlaceHill => new Color(0.3f, 0.75f, 1f, 0.4f),
            BrushMode.EraseHill => new Color(1f, 0.4f, 0.7f, 0.35f),
            BrushMode.PaintGround => new Color(0.7f, 0.5f, 0.25f, 0.4f),
            BrushMode.EraseGround => new Color(0.9f, 0.35f, 0.2f, 0.35f),
            _ => new Color(1f, 1f, 1f, 0.25f),
        };

        bool useEdgeHeight = IsHillBrush() || IsGroundBrush();
        float previewY = useEdgeHeight
            ? painter.GetEdgeSurfaceHeight(IsHillBrush() ? paintTopLayer : 0) + 0.2f
            : painter.GetTopSurfaceHeight(paintTopLayer) + 0.2f;

        if (painting && strokePreviewList.Count > 0)
        {
            Color trailColor = color;
            trailColor.a = Mathf.Min(0.55f, color.a + 0.2f);
            Color trailOutline = new Color(1f, 1f, 1f, 0.85f);

            for (int i = 0; i < strokePreviewList.Count; i++)
                DrawCellPreviewAtHeight(strokePreviewList[i], size, trailColor, IsHillBrush() ? painter.GetEdgeSurfaceHeight(GetHillBrushLayer(strokePreviewList[i])) + 0.2f : previewY, trailOutline);
        }

        if (!TryGetCellUnderMouse(e, out CliffPainter.CellCoord origin))
            return;

        hoverPaintCells.Clear();
        hoverConnectorCells.Clear();

        int stamp = GetStampSize();

        foreach (CliffPainter.CellCoord stampOrigin in GetMirroredOrigins(origin, stamp))
        {
            for (int dx = 0; dx < stamp; dx++)
            {
                for (int dz = 0; dz < stamp; dz++)
                {
                    CliffPainter.CellCoord cell = new CliffPainter.CellCoord(stampOrigin.x + dx, stampOrigin.z + dz);
                    if (hoverPaintCells.Contains(cell))
                        continue;
                    hoverPaintCells.Add(cell);

                    if (brushMode == BrushMode.PlaceHill && painter.CanPlaceHill(cell, GetHillBrushLayer(cell)))
                        painter.CollectAutoConnectorNeighbors(cell, GetHillBrushLayer(cell), hoverConnectorCells);
                }
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
                if (painter.CanPlaceHill(hoverPaintCells[i], GetHillBrushLayer(hoverPaintCells[i])) ||
                    painter.HasHill(hoverPaintCells[i]))
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
        {
            Color cellColor = hoverColor;
            if (brushMode == BrushMode.PaintTop && !painter.CanPlaceTop(hoverPaintCells[i], paintTopLayer) &&
                !painter.HasTop(hoverPaintCells[i]))
                cellColor = new Color(1f, 0.2f, 0.2f, hoverColor.a);
            DrawCellPreviewAtHeight(hoverPaintCells[i], size, cellColor, IsHillBrush() ? painter.GetEdgeSurfaceHeight(GetHillBrushLayer(hoverPaintCells[i])) + 0.2f : previewY);
        }

        if (brushMode == BrushMode.PlaceHill && !hillInvalid)
        {
            Color connColor = new Color(0.5f, 0.85f, 1f, 0.18f);
            for (int i = 0; i < hoverConnectorCells.Count; i++)
            {
                if (hoverPaintCells.Contains(hoverConnectorCells[i]))
                    continue;
                DrawCellPreviewAtHeight(hoverConnectorCells[i], size, connColor, painter.GetEdgeSurfaceHeight(GetHillBrushLayer(hoverConnectorCells[i])) + 0.2f);
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

        int resized = Mathf.Clamp(brushSize + delta, 1, IsGroundBrush() ? 16 : 5);
        if (resized == brushSize)
            return false;

        brushSize = resized;
        return true;
    }

    int GetStampSize()
    {
        if (IsGroundFillBrush())
            return Mathf.Max(1, groundFillSize);
        return Mathf.Max(1, brushSize);
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

        bool missingPrefab = painter.tileSet == null ||
            (brushMode == BrushMode.PaintTop && painter.tileSet.top == null) ||
            (brushMode == BrushMode.PaintGround && painter.tileSet.ground == null) ||
            (brushMode == BrushMode.PlaceHill && (painter.hillTileSet == null || painter.hillTileSet.hill == null));
        if (missingPrefab)
        {
            ShowNotification(new GUIContent("선택한 브러시에 필요한 타일 세트와 프리팹을 연결하세요."));
            return;
        }

        // 같은 스탬프 영역을 연속 드래그하면 스킵
        if (painting && IsStampAlreadyInStroke(origin))
            return;

        int stampSize = GetStampSize();
        // 대칭 위치도 함께 칠한다. 아래 처리 중 목록이 바뀌지 않도록 복사해 둔다.
        var origins = new List<CliffPainter.CellCoord>(GetMirroredOrigins(origin, stampSize));

        if (brushMode == BrushMode.PaintTop)
        {
            bool canPaint = false;
            foreach (CliffPainter.CellCoord o in origins)
                for (int dx = 0; dx < stampSize && !canPaint; dx++)
                    for (int dz = 0; dz < stampSize && !canPaint; dz++)
                        canPaint = painter.CanPlaceTop(new CliffPainter.CellCoord(o.x + dx, o.z + dz), paintTopLayer);
            // 빈 곳이나 이미 같은 높이인 곳을 누르면 빈 실행 취소 기록을 만들지 않는다.
            if (!canPaint) return;
        }

        hasHoverCell = true;
        lastHoverCell = origin;
        CollectStrokePreview(origin);

        if (strokeUndoGroup < 0)
            strokeUndoGroup = CliffPainterUndo.Begin(painter, "맵 지형 칠하기");
        bool changed = false;

        if (brushMode == BrushMode.PaintGround || brushMode == BrushMode.EraseGround)
        {
            foreach (CliffPainter.CellCoord o in origins)
                changed |= painter.FillGround(o, stampSize, erase: brushMode == BrushMode.EraseGround) > 0;
        }
        else if (brushMode == BrushMode.EraseTop)
        {
            // 대칭 칸을 한 번에 넘겨 주변 경계가 같은 규칙으로 물러나게 한다.
            var cells = new HashSet<CliffPainter.CellCoord>();
            foreach (CliffPainter.CellCoord o in origins)
                for (int dx = 0; dx < stampSize; dx++)
                    for (int dz = 0; dz < stampSize; dz++)
                        cells.Add(new CliffPainter.CellCoord(o.x + dx, o.z + dz));
            changed = painter.TryRemoveTops(cells, paintTopLayer);
        }
        else
        {
            foreach (CliffPainter.CellCoord o in origins)
            {
                for (int dx = 0; dx < stampSize; dx++)
                {
                    for (int dz = 0; dz < stampSize; dz++)
                    {
                        CliffPainter.CellCoord cell = new CliffPainter.CellCoord(o.x + dx, o.z + dz);
                        changed |= ApplyBrushToCell(cell);
                    }
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
        foreach (CliffPainter.CellCoord o in GetMirroredOrigins(origin, stamp))
        {
            for (int dx = 0; dx < stamp; dx++)
            {
                for (int dz = 0; dz < stamp; dz++)
                {
                    CliffPainter.CellCoord cell = new CliffPainter.CellCoord(o.x + dx, o.z + dz);
                    if (strokePreviewCells.Add(cell))
                        strokePreviewList.Add(cell);
                }
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
            case BrushMode.PlaceHill:
                return painter.TryFindHillLayer(cell, hillPickHeight, out int layer) && painter.TryAddHill(cell, layer);
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

    int GetHillBrushLayer(CliffPainter.CellCoord cell)
    {
        return painter.TryFindHillLayer(cell, hillPickHeight, out int layer) ? layer : 0;
    }

    bool TryGetCellUnderMouse(Event e, out CliffPainter.CellCoord cell)
    {
        cell = default;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        hillPickHeight = float.NaN;
        // 높이 있는 벽·언덕은 저지대 평면으로 찍으면 뒤쪽 칸이 잡히므로 Top 브러시도 표면을 찍는다.
        if ((IsHillBrush() || brushMode == BrushMode.PaintTop || brushMode == BrushMode.EraseTop) &&
            painter.generatedRoot != null)
        {
            // 다른 씬 오브젝트는 제외하고 생성된 절벽의 실제 표면을 찾는다.
            RaycastHit[] hits = Physics.RaycastAll(ray);
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit surface in hits)
            {
                if (!surface.transform.IsChildOf(painter.generatedRoot) || surface.distance >= nearest)
                    continue;
                nearest = surface.distance;
                hillPickHeight = surface.point.y;
                cell = painter.WorldToCell(surface.point);
            }
            if (!float.IsPositiveInfinity(nearest))
                return true;
        }
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

    // 칠한 칸을 그리드 원점 중심으로 옮기고 그리드 원점과 맵 루트를 월드 원점에 맞춘다.
    // 다른 오브젝트는 실제 타일이 옮겨진 양만큼 함께 옮겨 지형과의 상대 위치를 유지한다.
    void CenterMapOnOrigin()
    {
        FinishStroke();
        if (!painter.TryGetPaintedCellBounds(out CliffPainter.CellCoord min, out CliffPainter.CellCoord max))
        {
            EditorUtility.DisplayDialog("맵 생성기", "칠한 칸이 없습니다.", "확인");
            return;
        }

        int width = max.x - min.x + 1;
        int depth = max.z - min.z + 1;
        int dx = -(min.x + width / 2);
        int dz = -(min.z + depth / 2);
        string oddNote = width % 2 == 1 || depth % 2 == 1
            ? $"\n\n칸 수가 홀수인 방향({(width % 2 == 1 ? "X" : "")}{(depth % 2 == 1 ? "Z" : "")})은 정중앙이 타일 가운데라 중심이 반 칸({painter.TileSize * 0.5f:0.##}m) 어긋납니다."
            : "";

        if (!EditorUtility.DisplayDialog(
                "맵 생성기",
                $"칠한 범위 {width}×{depth}칸의 중심을 그리드 원점으로 옮기고, 그리드 원점을 (0, {painter.gridOrigin.y:0.##}, 0)으로 맞춥니다.\n\n" +
                "맵 루트는 원점으로 옮기고, 루트 아래 다른 오브젝트(본부 마커·둥지 등)는 지형과 같은 양만큼 함께 옮깁니다. " +
                "프리팹 인스턴스에서 실행했다면 원본 프리팹의 루트 위치도 원점으로 바로 저장합니다(이 부분은 실행 취소되지 않습니다). " +
                "나머지 변경은 끝난 뒤 Apply하세요." + oddNote,
                "옮기기",
                "취소"))
        {
            return;
        }

        Transform root = FindMapRoot();
        Transform terrain = painter.EnsureGeneratedRoot();
        bool hadTerrain = TryGetRendererBounds(terrain, out Bounds before);
        List<Transform> others = CollectMapObjects(root, terrain);

        int group = CliffPainterUndo.Begin(painter, "맵 중심을 원점으로");
        Undo.RecordObject(root, "맵 중심을 원점으로");
        Undo.RecordObject(terrain, "맵 중심을 원점으로");
        foreach (Transform other in others)
            Undo.RecordObject(other, "맵 중심을 원점으로");

        // 루트와 생성 부모를 원점에 두어야 MapLoader가 원점에 만들 때도 위치가 그대로 유지된다.
        var positions = new Vector3[others.Count];
        var rotations = new Quaternion[others.Count];
        for (int i = 0; i < others.Count; i++)
        {
            positions[i] = others[i].position;
            rotations[i] = others[i].rotation;
        }
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        terrain.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        painter.ShiftCells(dx, dz);
        painter.gridOrigin = new Vector3(0f, painter.gridOrigin.y, 0f);
        RebuildNow();

        Vector3 delta = hadTerrain && TryGetRendererBounds(terrain, out Bounds after)
            ? after.center - before.center : Vector3.zero;
        for (int i = 0; i < others.Count; i++)
            others[i].SetPositionAndRotation(positions[i] + delta, rotations[i]);

        MarkDirty();
        CliffPainterUndo.End(group);
        bool assetRootFixed = ResetPrefabAssetRoot(root);
        SceneView.RepaintAll();
        ShowNotification(new GUIContent(assetRootFixed
            ? "맵 중심을 원점으로 옮겼습니다. 원본 프리팹 루트도 원점으로 저장했습니다. 나머지는 Apply하세요."
            : "맵 중심을 원점으로 옮겼습니다."));
    }

    // 프리팹 인스턴스 루트의 위치·회전은 Apply되지 않으므로 원본 프리팹의 루트만 직접 원점으로 저장한다.
    // 자식의 로컬 위치는 건드리지 않아 씬에서 맞춘 배치가 Apply 후 그대로 원점 기준이 된다.
    static bool ResetPrefabAssetRoot(Transform root)
    {
        if (!PrefabUtility.IsOutermostPrefabInstanceRoot(root.gameObject))
            return false;

        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject);
        if (string.IsNullOrEmpty(path))
            return false;

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform assetRoot = contents.transform;
            if (assetRoot.localPosition == Vector3.zero && assetRoot.localRotation == Quaternion.identity)
                return false;

            assetRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            PrefabUtility.SaveAsPrefabAsset(contents, path, out bool saved);
            return saved;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    Transform FindMapRoot()
    {
        MapRoot mapRoot = painter.GetComponentInParent<MapRoot>(true);
        return mapRoot != null ? mapRoot.transform : painter.transform;
    }

    // 맵 루트 아래에서 지형 폴더와 맵 생성기 오브젝트 자신을 뺀 나머지 최상위 오브젝트를 모은다.
    List<Transform> CollectMapObjects(Transform root, Transform terrain)
    {
        var result = new List<Transform>();
        var queue = new Queue<Transform>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            Transform parent = queue.Dequeue();
            foreach (Transform child in parent)
            {
                if (child == terrain)
                    continue;
                // 맵 생성기가 붙은 오브젝트(또는 그 조상)는 옮기지 않고 그 아래 오브젝트만 옮긴다.
                if (painter.transform == child || painter.transform.IsChildOf(child))
                    queue.Enqueue(child);
                else
                    result.Add(child);
            }
        }
        return result;
    }

    static bool TryGetRendererBounds(Transform parent, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer renderer in parent.GetComponentsInChildren<Renderer>(true))
        {
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

    void RebuildNow()
    {
        if (painter == null)
            return;

        painter.RebuildGeometry(false);
        needsRebuild = false;
        MarkDirty();
    }

    void MarkDirty()
    {
        if (painter == null)
            return;

        CliffPainterUndo.MarkDirty(painter);
    }

    void ReleaseStrokeControl()
    {
        if (strokeControl != 0 && GUIUtility.hotControl == strokeControl) GUIUtility.hotControl = 0;
        strokeControl = 0;
    }

    void FinishStroke()
    {
        painting = false;
        ReleaseStrokeControl();
        strokePreviewCells.Clear();
        strokePreviewList.Clear();
        if (needsRebuild && painter != null) RebuildNow();
        CliffPainterUndo.End(strokeUndoGroup);
        strokeUndoGroup = -1;
    }
}
