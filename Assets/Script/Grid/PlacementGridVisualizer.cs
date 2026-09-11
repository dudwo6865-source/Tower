using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public class PlacementGridVisualizer : MonoBehaviour
{
    [Header("Footprint")]
    [Tooltip("건설 가능한 칸을 표시하는 하늘색 레이어 색상입니다.")]
    public Color validFootprintColor = new Color(0.25f, 0.65f, 1f, 0.65f);

    [Tooltip("건설할 수 없는 칸을 표시하는 색상입니다.")]
    public Color invalidFootprintColor = new Color(0.95f, 0.2f, 0.2f, 0.7f);

    [Tooltip("지형 위로 띄울 높이입니다.")]
    public float heightOffset = 0.12f;

    [Header("Cell Shape")]
    [Tooltip("칸 테두리에서 안쪽으로 들어가는 두께(월드 단위)입니다. 값을 키우면 칸끼리 서로 떨어져 보입니다.")]
    public float cellInset = 0.12f;

    [Tooltip("칸 모서리를 둥글게 표시할 반경(월드 단위)입니다.")]
    public float cellCornerRadius = 0.28f;

    [Tooltip("모서리 하나를 표현하는 곡선 분할 수입니다. 값이 클수록 더 둥글게 보입니다.")]
    [Range(1, 12)]
    public int cellCornerSegments = 6;

    private readonly List<Vector2> perimeterScratch = new List<Vector2>();

    private TowerPlacementController placementController;
    private Transform visualsRoot;
    private MeshRenderer footprintMeshRenderer;
    private Mesh footprintMesh;
    private Material footprintMaterial;

    private Vector2Int lastFootprintOrigin = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2Int lastFootprintSize = Vector2Int.one;
    private bool lastFootprintValid;
    private float lastPreferredY = float.NaN;
    private float preferredSampleY;

    // OnValidate가 인스펙터 값(색상/inset/radius 등)이 바뀔 때 바로 다시 그릴 수 있도록
    // 가장 최근에 HandlePreviewChanged로 받은 미리보기 상태를 별도로 보관한다.
    private bool hasCurrentPreview;
    private Vector2Int currentOriginCell;
    private Vector2Int currentFootprintCells = Vector2Int.one;
    private bool currentIsValid;

    void Awake()
    {
        placementController = GetComponent<TowerPlacementController>();

        if (placementController == null)
            placementController = TowerPlacementController.Instance;

        EnsureVisuals();
        SetVisualsActive(false);
    }

    void Start()
    {
        SyncVisualsRoot();
    }

    void OnEnable()
    {
        if (placementController == null)
            placementController = GetComponent<TowerPlacementController>();

        if (placementController == null)
            placementController = TowerPlacementController.Instance;

        if (placementController != null)
            placementController.PreviewChanged += HandlePreviewChanged;
    }

    void OnDisable()
    {
        if (placementController != null)
            placementController.PreviewChanged -= HandlePreviewChanged;

        SetVisualsActive(false);
    }

    void HandlePreviewChanged(PlacementPreviewState state)
    {
        if (MapGrid.Instance == null)
        {
            hasCurrentPreview = false;
            SetVisualsActive(false);
            return;
        }

        if (!state.hasPreview)
        {
            hasCurrentPreview = false;
            SetVisualsActive(false);
            return;
        }

        SyncVisualsRoot();
        SetVisualsActive(true);

        // state.originCell은 TowerPlacementController가 언덕/경계 폴백까지 거쳐
        // 이미 확정한 칸이자 고스트가 실제로 서 있는 칸이다. 여기서 centerWorld로
        // 다시 역산하면 부동소수점 왕복 오차로 가끔 한 칸 어긋나 고스트와
        // 풋프린트 표시가 안 맞는 문제가 있었다.
        Vector2Int originCell = state.originCell;
        preferredSampleY = state.centerWorld.y;

        hasCurrentPreview = true;
        currentOriginCell = originCell;
        currentFootprintCells = state.footprintCells;
        currentIsValid = state.isValid;

        bool layoutChanged = originCell != lastFootprintOrigin ||
            state.footprintCells != lastFootprintSize;
        bool heightChanged = !Mathf.Approximately(preferredSampleY, lastPreferredY);

        if (layoutChanged || heightChanged)
        {
            RebuildFootprintMesh(
                originCell,
                state.footprintCells,
                state.isValid);

            lastFootprintOrigin = originCell;
            lastFootprintSize = state.footprintCells;
            lastFootprintValid = state.isValid;
            lastPreferredY = preferredSampleY;
        }
        else if (state.isValid != lastFootprintValid)
        {
            RebuildFootprintMesh(
                originCell,
                state.footprintCells,
                state.isValid);
            lastFootprintValid = state.isValid;
        }
    }

    void OnDestroy()
    {
        if (footprintMesh != null)
            Destroy(footprintMesh);

        if (footprintMaterial != null)
            Destroy(footprintMaterial);
    }

    void OnValidate()
    {
        // Awake가 아직 실행되지 않았다면(런타임에 막 AddComponent된 직후 Unity가 자동으로
        // 호출하는 경우) 여기서 손대지 않는다. Awake가 곧이어 정상적으로 초기화한다.
        // 이 시점에 EnsureVisuals()로 새 GameObject/컴포넌트를 만들면 OnValidate 안에서
        // AddComponent를 호출하는 셈이 되어 Unity가 경고를 띄운다.
        if (visualsRoot == null || footprintMesh == null || footprintMaterial == null)
            return;

        InvalidateCache();

        // 배치 중에 색상/inset/모서리 반경 같은 인스펙터 값을 바꾸면, 고스트를 움직이지
        // 않아도 바로 반영되도록 지금 보이고 있는 풋프린트를 즉시 다시 그린다.
        if (hasCurrentPreview && visualsRoot.gameObject.activeSelf)
        {
            RebuildFootprintMesh(currentOriginCell, currentFootprintCells, currentIsValid);

            lastFootprintOrigin = currentOriginCell;
            lastFootprintSize = currentFootprintCells;
            lastFootprintValid = currentIsValid;
            lastPreferredY = preferredSampleY;
        }
    }

    void EnsureVisuals()
    {
        if (visualsRoot != null)
            return;

        GameObject rootObject = new GameObject("PlacementFootprintVisuals");
        rootObject.transform.SetParent(transform, false);
        visualsRoot = rootObject.transform;

        GameObject footprintObject = new GameObject(
            "Footprint",
            typeof(MeshFilter),
            typeof(MeshRenderer));

        footprintObject.transform.SetParent(visualsRoot, false);
        footprintMeshRenderer = footprintObject.GetComponent<MeshRenderer>();
        footprintMesh = new Mesh { name = "PlacementFootprint" };
        footprintObject.GetComponent<MeshFilter>().sharedMesh = footprintMesh;
        footprintMaterial = CreateGroundOverlayMaterial(validFootprintColor);
        footprintMeshRenderer.sharedMaterial = footprintMaterial;
        ConfigureRenderer(footprintMeshRenderer);
    }

    static void ConfigureRenderer(MeshRenderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    void SetVisualsActive(bool active)
    {
        if (visualsRoot == null)
            return;

        bool wasActive = visualsRoot.gameObject.activeSelf;

        if (wasActive == active)
            return;

        visualsRoot.gameObject.SetActive(active);

        if (active)
            InvalidateCache();
        else
            lastFootprintOrigin = new Vector2Int(int.MinValue, int.MinValue);
    }

    void InvalidateCache()
    {
        lastFootprintOrigin = new Vector2Int(int.MinValue, int.MinValue);
        lastPreferredY = float.NaN;
    }

    void SyncVisualsRoot()
    {
        if (visualsRoot == null || MapGrid.Instance == null)
            return;

        Transform gridTransform = MapGrid.Instance.transform;

        if (visualsRoot.parent != gridTransform)
            visualsRoot.SetParent(gridTransform, false);

        visualsRoot.localPosition = Vector3.zero;
        visualsRoot.localRotation = Quaternion.identity;
        visualsRoot.localScale = Vector3.one;
    }

    void RebuildFootprintMesh(
        Vector2Int originCell,
        Vector2Int footprintCells,
        bool isValid)
    {
        MapGrid grid = MapGrid.Instance;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        footprintMesh.Clear();

        if (grid == null)
            return;

        bool footprintFullyOnNavMesh =
            !grid.UsesNavMesh ||
            grid.IsFootprintOnNavMesh(originCell, footprintCells, preferredSampleY);

        if (footprintFullyOnNavMesh)
        {
            for (int x = 0; x < footprintCells.x; x++)
            {
                for (int z = 0; z < footprintCells.y; z++)
                {
                    Vector2Int cell = new Vector2Int(originCell.x + x, originCell.y + z);
                    AddCellQuad(vertices, triangles, grid, cell);
                }
            }
        }

        footprintMesh.SetVertices(vertices);
        footprintMesh.SetTriangles(triangles, 0);
        footprintMesh.RecalculateBounds();
        footprintMesh.RecalculateNormals();

        Color color = isValid && footprintFullyOnNavMesh
            ? validFootprintColor
            : invalidFootprintColor;
        footprintMaterial.color = color;
    }

    void AddCellQuad(
        List<Vector3> vertices,
        List<int> triangles,
        MapGrid grid,
        Vector2Int cell)
    {
        Vector3 corner = grid.CellCornerToWorld(cell);
        float size = grid.cellSize;

        // 테두리에서 inset만큼 안으로 들여서 칸끼리 살짝 떨어져 보이게 만들고,
        // 남은 절반 폭을 넘지 않게 반경을 잘라 모서리끼리 겹치지 않게 한다.
        float inset = Mathf.Clamp(cellInset, 0f, size * 0.5f - 0.01f);
        float radius = Mathf.Clamp(cellCornerRadius, 0f, size * 0.5f - inset);

        float min = inset;
        float max = size - inset;

        perimeterScratch.Clear();
        AddRoundedCorner(perimeterScratch, new Vector2(max - radius, min + radius), radius, -90f, 0f);
        AddRoundedCorner(perimeterScratch, new Vector2(max - radius, max - radius), radius, 0f, 90f);
        AddRoundedCorner(perimeterScratch, new Vector2(min + radius, max - radius), radius, 90f, 180f);
        AddRoundedCorner(perimeterScratch, new Vector2(min + radius, min + radius), radius, 180f, 270f);

        int start = vertices.Count;

        for (int i = 0; i < perimeterScratch.Count; i++)
        {
            Vector2 p = perimeterScratch[i];
            vertices.Add(Lift(corner + new Vector3(p.x, 0f, p.y)));
        }

        // 볼록 다각형이라 팬 삼각분할로 충분하다.
        for (int i = 1; i < perimeterScratch.Count - 1; i++)
        {
            triangles.Add(start);
            triangles.Add(start + i);
            triangles.Add(start + i + 1);
        }
    }

    void AddRoundedCorner(
        List<Vector2> points,
        Vector2 arcCenter,
        float radius,
        float startDegrees,
        float endDegrees)
    {
        if (radius <= 0f)
        {
            points.Add(arcCenter);
            return;
        }

        for (int i = 0; i <= cellCornerSegments; i++)
        {
            float t = (float)i / cellCornerSegments;
            float angle = Mathf.Deg2Rad * Mathf.Lerp(startDegrees, endDegrees, t);
            points.Add(arcCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    Vector3 Lift(Vector3 worldPoint)
    {
        MapGrid grid = MapGrid.Instance;

        // 칸마다 SampleGroundHeight로 지면을 다시 재는 대신, 고스트가 실제로
        // 서 있는 footprint 공통 높이(preferredSampleY)를 그대로 쓴다.
        // 고스트 자체가 그 높이 하나로 평평하게 배치되므로, 오버레이도 칸마다
        // 울퉁불퉁해지지 않고 고스트와 정확히 같은 기준으로 맞는다.
        worldPoint.y = preferredSampleY + heightOffset;

        if (grid != null)
            return grid.transform.InverseTransformPoint(worldPoint);

        return worldPoint;
    }

    static Material CreateGroundOverlayMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");

        if (shader == null)
            shader = Shader.Find("Hidden/InternalErrorShader");

        Material material = new Material(shader);
        Texture mainTexture = HealthBarSpriteUtility.GetWhiteSprite().texture;
        material.mainTexture = mainTexture;

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", mainTexture);

        material.color = color;
        material.renderQueue = (int)RenderQueue.Transparent + 10;

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);

        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)CullMode.Off);

        material.doubleSidedGI = true;
        return material;
    }
}
