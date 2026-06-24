using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(140)]
public class BuildZoneVisualizer : MonoBehaviour
{
    [Header("Zone Fill")]
    public Color zoneFillColor = new Color(0.12f, 0.55f, 1f, 0.24f);

    public Color zoneEdgeColor = new Color(0.35f, 0.85f, 1f, 0.55f);

    [Tooltip("지형 위로 띄울 높이입니다.")]
    public float heightOffset = 0.06f;

    [Tooltip("구역 가장자리 선 두께(월드 단위)입니다.")]
    public float edgeLineWidth = 0.1f;

    private TowerPlacementController placementController;
    private Transform visualsRoot;
    private MeshFilter fillMeshFilter;
    private MeshRenderer fillMeshRenderer;
    private MeshFilter edgeMeshFilter;
    private MeshRenderer edgeMeshRenderer;
    private Mesh fillMesh;
    private Mesh edgeMesh;
    private Material fillMaterial;
    private Material edgeMaterial;

    private Headquarters activeHeadquarters;
    private Vector2Int lastCenterCell = new Vector2Int(int.MinValue, int.MinValue);
    private int lastRadius = -1;
    private bool wasVisible;

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

    void LateUpdate()
    {
        RefreshVisibility();
    }

    void OnDestroy()
    {
        if (fillMesh != null)
            Destroy(fillMesh);

        if (edgeMesh != null)
            Destroy(edgeMesh);

        if (fillMaterial != null)
            Destroy(fillMaterial);

        if (edgeMaterial != null)
            Destroy(edgeMaterial);
    }

    void RefreshVisibility()
    {
        if (MapGrid.Instance == null || BuildZoneManager.Instance == null)
        {
            SetVisualsActive(false);
            return;
        }

        Headquarters headquarters = ResolveHeadquartersToDisplay();

        if (headquarters == null)
        {
            SetVisualsActive(false);
            return;
        }

        SyncVisualsRoot();
        SetVisualsActive(true);

        headquarters.RefreshCenterCell();

        if (headquarters != activeHeadquarters ||
            headquarters.CenterCell != lastCenterCell ||
            headquarters.buildRadiusCells != lastRadius)
        {
            RebuildZoneMesh(headquarters);
            activeHeadquarters = headquarters;
            lastCenterCell = headquarters.CenterCell;
            lastRadius = headquarters.buildRadiusCells;
        }
    }

    Headquarters ResolveHeadquartersToDisplay()
    {
        int localOwnerId = GetLocalOwnerId();

        if (placementController != null &&
            placementController.IsPlacing &&
            BuildZoneManager.Instance.TryGetHeadquarters(
                localOwnerId,
                out Headquarters placementHq))
        {
            return placementHq;
        }

        return GetSelectedLocalHeadquarters(localOwnerId);
    }

    Headquarters GetSelectedLocalHeadquarters(int localOwnerId)
    {
        if (UnitSelectionManager.Instance == null)
            return null;

        foreach (SelectableEntity entity in
                 UnitSelectionManager.Instance.GetSelectedEntities())
        {
            if (entity == null ||
                entity.entityType != SelectableEntityType.Building)
            {
                continue;
            }

            Headquarters headquarters = entity.GetComponent<Headquarters>();

            if (headquarters != null && headquarters.OwnerId == localOwnerId)
                return headquarters;
        }

        return null;
    }

    int GetLocalOwnerId()
    {
        if (UnitSelectionManager.Instance != null)
            return UnitSelectionManager.Instance.localPlayerOwnerId;

        if (placementController != null)
            return placementController.localPlayerOwnerId;

        return 1;
    }

    void EnsureVisuals()
    {
        if (visualsRoot != null)
            return;

        GameObject rootObject = new GameObject("BuildZoneVisuals");
        rootObject.transform.SetParent(transform, false);
        visualsRoot = rootObject.transform;

        GameObject fillObject = new GameObject(
            "ZoneFill",
            typeof(MeshFilter),
            typeof(MeshRenderer));

        fillObject.transform.SetParent(visualsRoot, false);
        fillMeshFilter = fillObject.GetComponent<MeshFilter>();
        fillMeshRenderer = fillObject.GetComponent<MeshRenderer>();
        fillMesh = new Mesh { name = "BuildZoneFill" };
        fillMeshFilter.sharedMesh = fillMesh;
        fillMaterial = CreateGroundOverlayMaterial(zoneFillColor, 5);
        fillMeshRenderer.sharedMaterial = fillMaterial;
        ConfigureRenderer(fillMeshRenderer);

        GameObject edgeObject = new GameObject(
            "ZoneEdge",
            typeof(MeshFilter),
            typeof(MeshRenderer));

        edgeObject.transform.SetParent(visualsRoot, false);
        edgeMeshFilter = edgeObject.GetComponent<MeshFilter>();
        edgeMeshRenderer = edgeObject.GetComponent<MeshRenderer>();
        edgeMesh = new Mesh { name = "BuildZoneEdge" };
        edgeMeshFilter.sharedMesh = edgeMesh;
        edgeMaterial = CreateGroundOverlayMaterial(zoneEdgeColor, 15);
        edgeMeshRenderer.sharedMaterial = edgeMaterial;
        ConfigureRenderer(edgeMeshRenderer);
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

        if (wasVisible == active)
            return;

        wasVisible = active;
        visualsRoot.gameObject.SetActive(active);

        if (!active)
        {
            activeHeadquarters = null;
            lastCenterCell = new Vector2Int(int.MinValue, int.MinValue);
            lastRadius = -1;
        }
    }

    void SyncVisualsRoot()
    {
        if (visualsRoot == null || MapGrid.Instance == null)
            return;

        if (visualsRoot.parent != MapGrid.Instance.transform)
            visualsRoot.SetParent(MapGrid.Instance.transform, false);

        visualsRoot.position = MapGrid.Instance.MapOrigin;
        visualsRoot.rotation = Quaternion.identity;
        visualsRoot.localScale = Vector3.one;
    }

    void RebuildZoneMesh(Headquarters headquarters)
    {
        MapGrid grid = MapGrid.Instance;
        var fillVertices = new List<Vector3>();
        var fillTriangles = new List<int>();
        var edgeVertices = new List<Vector3>();
        var edgeTriangles = new List<int>();

        Vector2Int center = headquarters.CenterCell;
        int radius = headquarters.buildRadiusCells;

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.y - radius; z <= center.y + radius; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);

                if (!headquarters.ContainsCell(cell))
                    continue;

                if (!grid.IsFootprintInBounds(cell, Vector2Int.one))
                    continue;

                if (grid.UsesNavMesh && !grid.IsCellOnNavMesh(cell))
                    continue;

                AddCellQuad(fillVertices, fillTriangles, grid, cell);
                AddBoundaryEdges(
                    edgeVertices,
                    edgeTriangles,
                    grid,
                    cell,
                    headquarters);
            }
        }

        fillMesh.Clear();
        fillMesh.SetVertices(fillVertices);
        fillMesh.SetTriangles(fillTriangles, 0);
        fillMesh.RecalculateBounds();
        fillMesh.RecalculateNormals();

        edgeMesh.Clear();
        edgeMesh.SetVertices(edgeVertices);
        edgeMesh.SetTriangles(edgeTriangles, 0);
        edgeMesh.RecalculateBounds();
        edgeMesh.RecalculateNormals();

        fillMaterial.color = zoneFillColor;
        edgeMaterial.color = zoneEdgeColor;
    }

    void AddBoundaryEdges(
        List<Vector3> vertices,
        List<int> triangles,
        MapGrid grid,
        Vector2Int cell,
        Headquarters headquarters)
    {
        float size = grid.cellSize;
        Vector3 corner = grid.CellCornerToWorld(cell);

        Vector3 bottomLeft = Lift(corner);
        Vector3 bottomRight = Lift(corner + new Vector3(size, 0f, 0f));
        Vector3 topRight = Lift(corner + new Vector3(size, 0f, size));
        Vector3 topLeft = Lift(corner + new Vector3(0f, 0f, size));

        if (!headquarters.ContainsCell(new Vector2Int(cell.x, cell.y - 1)))
            AddLineQuad(vertices, triangles, bottomLeft, bottomRight);

        if (!headquarters.ContainsCell(new Vector2Int(cell.x + 1, cell.y)))
            AddLineQuad(vertices, triangles, bottomRight, topRight);

        if (!headquarters.ContainsCell(new Vector2Int(cell.x, cell.y + 1)))
            AddLineQuad(vertices, triangles, topRight, topLeft);

        if (!headquarters.ContainsCell(new Vector2Int(cell.x - 1, cell.y)))
            AddLineQuad(vertices, triangles, topLeft, bottomLeft);
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
        Vector3 direction = end - start;
        float length = direction.magnitude;

        if (length <= 0.001f)
            return;

        Vector3 tangent = direction / length;
        Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized * (edgeLineWidth * 0.5f);
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
            worldPoint -= grid.MapOrigin;

        return worldPoint;
    }

    static Material CreateGroundOverlayMaterial(Color color, int queueOffset)
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
        material.renderQueue = (int)RenderQueue.Transparent + queueOffset;

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
