using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public class PlacementGridVisualizer : MonoBehaviour
{
    [Header("Footprint")]
    public Color validFootprintColor = new Color(0.2f, 0.95f, 0.35f, 0.4f);

    public Color invalidFootprintColor = new Color(0.95f, 0.25f, 0.25f, 0.4f);

    [Tooltip("지형 위로 띄울 높이입니다.")]
    public float heightOffset = 0.12f;

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
        preferredSampleY = state.centerWorld.y;

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
        if (footprintMesh != null)
            Destroy(footprintMesh);

        if (footprintMaterial != null)
            Destroy(footprintMaterial);
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

    Vector3 Lift(Vector3 worldPoint)
    {
        MapGrid grid = MapGrid.Instance;

        if (grid != null)
            worldPoint.y = grid.SampleGroundHeight(worldPoint, preferredSampleY);

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
