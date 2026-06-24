using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public class PlacementGridVisualizer : MonoBehaviour
{
    [Header("Grid Lines")]
    [Tooltip("미리보기 고스트 중심 기준으로 표시할 격자 반경(칸)입니다.")]
    public int visibleRadius = 10;

    [Tooltip("ON이면 footprint 주변만 겹자를 표시합니다. OFF면 중심 셀 기준 visibleRadius 사각형 안의 NavMesh 칸을 모두 표시합니다.")]
    public bool anchorGridToFootprint = true;

    public Color gridLineColor = new Color(1f, 1f, 1f, 0.65f);

    [Tooltip("격자선 두께(월드 단위)입니다.")]
    public float lineWidth = 0.08f;

    [Tooltip("지형 위로 띄울 높이입니다.")]
    public float heightOffset = 0.12f;

    [Header("Footprint")]
    public Color validFootprintColor = new Color(0.2f, 0.95f, 0.35f, 0.4f);

    public Color invalidFootprintColor = new Color(0.95f, 0.25f, 0.25f, 0.4f);

    private TowerPlacementController placementController;
    private Transform visualsRoot;
    private MeshFilter gridMeshFilter;
    private MeshRenderer gridMeshRenderer;
    private MeshFilter footprintMeshFilter;
    private MeshRenderer footprintMeshRenderer;
    private Mesh gridMesh;
    private Mesh footprintMesh;
    private Material gridMaterial;
    private Material footprintMaterial;

    private Vector2Int lastFootprintOrigin = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2Int lastFootprintSize = Vector2Int.one;
    private bool lastFootprintValid;

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
            SetVisualsActive(false);
            return;
        }

        if (!state.hasPreview)
        {
            SetVisualsActive(false);
            return;
        }

        SyncVisualsRoot();
        SetVisualsActive(true);

        Vector2Int originCell = ResolvePreviewOriginCell(state);

        bool layoutChanged = originCell != lastFootprintOrigin ||
            state.footprintCells != lastFootprintSize;

        if (layoutChanged)
        {
            RebuildGridMesh(originCell, state.footprintCells);
            RebuildFootprintMesh(
                originCell,
                state.footprintCells,
                state.isValid);

            lastFootprintOrigin = originCell;
            lastFootprintSize = state.footprintCells;
            lastFootprintValid = state.isValid;
        }
        else if (state.isValid != lastFootprintValid)
        {
            UpdateFootprintColor(state.isValid);
            lastFootprintValid = state.isValid;
        }
    }

    Vector2Int ResolvePreviewOriginCell(PlacementPreviewState state)
    {
        MapGrid grid = MapGrid.Instance;

        if (grid == null || !state.hasPreview)
            return state.originCell;

        return grid.GetFootprintOriginFromCenterWorld(
            state.centerWorld,
            state.footprintCells);
    }

    void OnDestroy()
    {
        if (gridMesh != null)
            Destroy(gridMesh);

        if (footprintMesh != null)
            Destroy(footprintMesh);

        if (gridMaterial != null)
            Destroy(gridMaterial);

        if (footprintMaterial != null)
            Destroy(footprintMaterial);
    }

    void EnsureVisuals()
    {
        if (visualsRoot != null)
            return;

        GameObject rootObject = new GameObject("PlacementGridVisuals");
        rootObject.transform.SetParent(transform, false);
        visualsRoot = rootObject.transform;

        GameObject gridObject = new GameObject(
            "GridLines",
            typeof(MeshFilter),
            typeof(MeshRenderer));

        gridObject.transform.SetParent(visualsRoot, false);
        gridMeshFilter = gridObject.GetComponent<MeshFilter>();
        gridMeshRenderer = gridObject.GetComponent<MeshRenderer>();
        gridMesh = new Mesh { name = "PlacementGridLines" };
        gridMeshFilter.sharedMesh = gridMesh;
        gridMaterial = CreateGroundOverlayMaterial(gridLineColor);
        gridMeshRenderer.sharedMaterial = gridMaterial;
        ConfigureRenderer(gridMeshRenderer);

        GameObject footprintObject = new GameObject(
            "Footprint",
            typeof(MeshFilter),
            typeof(MeshRenderer));

        footprintObject.transform.SetParent(visualsRoot, false);
        footprintMeshFilter = footprintObject.GetComponent<MeshFilter>();
        footprintMeshRenderer = footprintObject.GetComponent<MeshRenderer>();
        footprintMesh = new Mesh { name = "PlacementFootprint" };
        footprintMeshFilter.sharedMesh = footprintMesh;
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
        {
            lastFootprintOrigin = new Vector2Int(int.MinValue, int.MinValue);
        }
    }

    void InvalidateCache()
    {
        lastFootprintOrigin = new Vector2Int(int.MinValue, int.MinValue);
    }

    void RebuildGridMesh(
        Vector2Int originCell,
        Vector2Int footprintCells)
    {
        MapGrid grid = MapGrid.Instance;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        gridMesh.Clear();

        if (grid == null)
            return;

        int minX;
        int maxX;
        int minZ;
        int maxZ;

        if (anchorGridToFootprint)
        {
            minX = originCell.x - visibleRadius;
            maxX = originCell.x + footprintCells.x + visibleRadius;
            minZ = originCell.y - visibleRadius;
            maxZ = originCell.y + footprintCells.y + visibleRadius;
        }
        else
        {
            Vector2Int centerCell = originCell + new Vector2Int(
                footprintCells.x / 2,
                footprintCells.y / 2);

            minX = centerCell.x - visibleRadius;
            maxX = centerCell.x + visibleRadius + 1;
            minZ = centerCell.y - visibleRadius;
            maxZ = centerCell.y + visibleRadius + 1;
        }

        minX = Mathf.Clamp(minX, 0, grid.CellCountX);
        maxX = Mathf.Clamp(maxX, 0, grid.CellCountX);
        minZ = Mathf.Clamp(minZ, 0, grid.CellCountZ);
        maxZ = Mathf.Clamp(maxZ, 0, grid.CellCountZ);

        if (maxX <= minX || maxZ <= minZ)
            return;

        if (grid.UsesNavMesh)
        {
            for (int x = minX; x < maxX; x++)
            {
                for (int z = minZ; z < maxZ; z++)
                {
                    Vector2Int cell = new Vector2Int(x, z);

                    if (!grid.IsCellOnNavMesh(cell))
                        continue;

                    AddCellBorder(vertices, triangles, grid, cell);
                }
            }
        }
        else
        {
            for (int x = minX; x <= maxX; x++)
            {
                AddLineQuad(
                    vertices,
                    triangles,
                    grid.CellCornerToWorld(new Vector2Int(x, minZ)),
                    grid.CellCornerToWorld(new Vector2Int(x, maxZ)));
            }

            for (int z = minZ; z <= maxZ; z++)
            {
                AddLineQuad(
                    vertices,
                    triangles,
                    grid.CellCornerToWorld(new Vector2Int(minX, z)),
                    grid.CellCornerToWorld(new Vector2Int(maxX, z)));
            }
        }

        gridMesh.SetVertices(vertices);
        gridMesh.SetTriangles(triangles, 0);
        gridMesh.RecalculateBounds();
        gridMesh.RecalculateNormals();
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

        for (int x = 0; x < footprintCells.x; x++)
        {
            for (int z = 0; z < footprintCells.y; z++)
            {
                Vector2Int cell = new Vector2Int(originCell.x + x, originCell.y + z);

                if (grid.UsesNavMesh && !grid.IsCellOnNavMesh(cell))
                    continue;

                AddCellQuad(vertices, triangles, grid, cell);
            }
        }

        footprintMesh.Clear();
        footprintMesh.SetVertices(vertices);
        footprintMesh.SetTriangles(triangles, 0);
        footprintMesh.RecalculateBounds();
        footprintMesh.RecalculateNormals();

        Color color = isValid ? validFootprintColor : invalidFootprintColor;
        footprintMaterial.color = color;
    }

    void UpdateFootprintColor(bool isValid)
    {
        footprintMaterial.color = isValid
            ? validFootprintColor
            : invalidFootprintColor;
    }

    void AddCellBorder(
        List<Vector3> vertices,
        List<int> triangles,
        MapGrid grid,
        Vector2Int cell)
    {
        float size = grid.cellSize;
        Vector3 corner = grid.CellCornerToWorld(cell);

        AddLineQuad(vertices, triangles, corner, corner + new Vector3(size, 0f, 0f));
        AddLineQuad(vertices, triangles, corner + new Vector3(size, 0f, 0f), corner + new Vector3(size, 0f, size));
        AddLineQuad(vertices, triangles, corner + new Vector3(size, 0f, size), corner + new Vector3(0f, 0f, size));
        AddLineQuad(vertices, triangles, corner + new Vector3(0f, 0f, size), corner);
    }

    void AddCellQuad(
        List<Vector3> vertices,
        List<int> triangles,
        MapGrid grid,
        Vector2Int cell)
    {
        Vector3 corner = grid.CellCornerToWorld(cell);
        float size = grid.cellSize;
        int start = vertices.Count;

        vertices.Add(Lift(corner));
        vertices.Add(Lift(corner + new Vector3(size, 0f, 0f)));
        vertices.Add(Lift(corner + new Vector3(size, 0f, size)));
        vertices.Add(Lift(corner + new Vector3(0f, 0f, size)));

        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start);
        triangles.Add(start + 3);
        triangles.Add(start + 2);
    }

    void AddLineQuad(
        List<Vector3> vertices,
        List<int> triangles,
        Vector3 start,
        Vector3 end)
    {
        start = Lift(start);
        end = Lift(end);

        Vector3 direction = end - start;
        float length = direction.magnitude;

        if (length <= 0.001f)
            return;

        Vector3 tangent = direction / length;
        Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized * (lineWidth * 0.5f);
        int startIndex = vertices.Count;

        vertices.Add(start - side);
        vertices.Add(start + side);
        vertices.Add(end + side);
        vertices.Add(end - side);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex);
        triangles.Add(startIndex + 3);
        triangles.Add(startIndex + 2);
    }

    Vector3 Lift(Vector3 worldPoint)
    {
        MapGrid grid = MapGrid.Instance;

        if (grid != null)
            worldPoint.y = grid.SampleGroundHeight(worldPoint);

        worldPoint.y += heightOffset;

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
