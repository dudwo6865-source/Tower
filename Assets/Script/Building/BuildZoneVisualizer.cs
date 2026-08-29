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

    [Tooltip("지형 위로 띄울 높이입니다. BuildZoneProvider.buildZoneHeightOffset에 더해집니다.")]
    public float heightOffset = 0f;

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

    private readonly List<BuildZoneProvider> displayProviders = new List<BuildZoneProvider>();
    private readonly List<BuildZoneProvider> selectedProviders = new List<BuildZoneProvider>();
    private readonly HashSet<Vector2Int> zoneCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> radiusCellScratch = new HashSet<Vector2Int>();

    // 칸마다 NavMesh를 17번 샘플링하므로, 고스트가 움직여도 바뀌지 않는
    // 기존 구역과 매 칸 바뀌는 미리보기 구역을 따로 캐시합니다.
    private readonly HashSet<Vector2Int> providerCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, float> providerCellHeights =
        new Dictionary<Vector2Int, float>();
    private readonly HashSet<Vector2Int> previewCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, float> previewCellHeights =
        new Dictionary<Vector2Int, float>();

    private int lastProviderSignature;
    private int lastPreviewSignature;
    private bool wasVisible;

    private bool previewActive;
    private Vector2Int previewCenter;
    private int previewRadius;
    private float previewHeightOffset = 0.06f;

    private Vector2Int placementFootprint = Vector2Int.one;

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

        ResolvePlacementPreview();
        ResolvePlacementFootprint();
        ResolveProvidersToDisplay();

        if (displayProviders.Count == 0 && !previewActive)
        {
            SetVisualsActive(false);
            return;
        }

        SyncVisualsRoot();
        SetVisualsActive(true);

        MapGrid grid = MapGrid.Instance;
        int providerSignature = ComputeProviderSignature();
        int previewSignature = ComputePreviewSignature();
        bool dirty = false;

        if (providerSignature != lastProviderSignature)
        {
            RebuildProviderCells(grid);
            lastProviderSignature = providerSignature;
            dirty = true;
        }

        if (previewSignature != lastPreviewSignature)
        {
            RebuildPreviewCells(grid);
            lastPreviewSignature = previewSignature;
            dirty = true;
        }

        if (dirty)
            RebuildZoneMesh(grid);
    }

    void ResolveProvidersToDisplay()
    {
        displayProviders.Clear();
        int localOwnerId = GetLocalOwnerId();

        if (placementController != null && placementController.IsPlacing)
        {
            BuildZoneManager.Instance.GetProviders(localOwnerId, displayProviders);
            return;
        }

        CollectSelectedLocalProviders(localOwnerId, selectedProviders);

        if (selectedProviders.Count > 0)
            displayProviders.AddRange(selectedProviders);
    }

    void CollectSelectedLocalProviders(int localOwnerId, List<BuildZoneProvider> results)
    {
        results.Clear();

        if (UnitSelectionManager.Instance == null)
            return;

        foreach (SelectableEntity entity in
                 UnitSelectionManager.Instance.GetSelectedEntities())
        {
            if (entity == null ||
                entity.entityType != SelectableEntityType.Building)
            {
                continue;
            }

            BuildZoneProvider provider = entity.GetComponent<BuildZoneProvider>();

            if (provider != null && provider.OwnerId == localOwnerId)
                results.Add(provider);
        }
    }

    void ResolvePlacementPreview()
    {
        previewActive = false;
        previewRadius = 0;
        previewCenter = Vector2Int.zero;
        previewHeightOffset = 0.06f;

        if (placementController == null ||
            !placementController.IsPlacing ||
            !placementController.HasPreviewPlacement)
        {
            return;
        }

        IBuildablePlacementData data = placementController.PendingBuildData;

        if (data == null || data.Prefab == null)
            return;

        BuildZoneProvider provider = data.Prefab.GetComponent<BuildZoneProvider>();

        if (provider == null)
            provider = data.Prefab.GetComponentInChildren<BuildZoneProvider>(true);

        if (provider == null || provider.buildRadiusCells <= 0)
            return;

        previewActive = true;
        previewRadius = provider.buildRadiusCells;
        previewHeightOffset = provider.buildZoneHeightOffset;
        previewCenter = BuildZoneProvider.GetFootprintCenterCell(
            placementController.PreviewOriginCell,
            placementController.PendingFootprintCells);
    }

    /// <summary>
    /// 배치 중인 건물의 발자국입니다. 건설 판정(CanBuildFootprint)은 발자국 전체가
    /// 한 구역 안에 들어와야 통과하므로, 표시도 이 크기만큼 좁혀야 실제와 맞습니다.
    /// </summary>
    void ResolvePlacementFootprint()
    {
        placementFootprint = Vector2Int.one;

        if (placementController == null || !placementController.IsPlacing)
            return;

        IBuildablePlacementData data = placementController.PendingBuildData;

        // 구역 밖에도 지을 수 있는 건물이면 구역이 배치를 막지 않으니 그대로 보여줍니다.
        if (data == null || BuildZoneProvider.PrefabCanPlaceOutsideBuildZones(data.Prefab))
            return;

        Vector2Int footprint = placementController.PendingFootprintCells;

        if (footprint.x > 0 && footprint.y > 0)
            placementFootprint = footprint;
    }

    int GetLocalOwnerId()
    {
        if (UnitSelectionManager.Instance != null)
            return UnitSelectionManager.Instance.localPlayerOwnerId;

        if (placementController != null)
            return placementController.localPlayerOwnerId;

        return 1;
    }

    int ComputeProviderSignature()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + displayProviders.Count;

            for (int i = 0; i < displayProviders.Count; i++)
            {
                BuildZoneProvider provider = displayProviders[i];

                if (provider == null)
                    continue;

                provider.RefreshCenterCell();
                Vector3 pos = provider.transform.position;

                hash = hash * 31 + provider.GetInstanceID();
                hash = hash * 31 + provider.CenterCell.x;
                hash = hash * 31 + provider.CenterCell.y;
                hash = hash * 31 + provider.buildRadiusCells;
                hash = hash * 31 + Mathf.RoundToInt(provider.buildZoneHeightOffset * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(pos.x * 10f);
                hash = hash * 31 + Mathf.RoundToInt(pos.z * 10f);
            }

            // 다른 건물을 고르면 발자국이 바뀌므로 구역도 다시 좁혀야 합니다.
            hash = hash * 31 + placementFootprint.x;
            hash = hash * 31 + placementFootprint.y;

            return hash;
        }
    }

    int ComputePreviewSignature()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (previewActive ? 1 : 0);
            hash = hash * 31 + previewCenter.x;
            hash = hash * 31 + previewCenter.y;
            hash = hash * 31 + previewRadius;

            return hash;
        }
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
        {
            if (!active)
                InvalidateZoneCache();

            return;
        }

        wasVisible = active;
        visualsRoot.gameObject.SetActive(active);

        if (!active)
            InvalidateZoneCache();
    }

    void InvalidateZoneCache()
    {
        lastProviderSignature = 0;
        lastPreviewSignature = 0;
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

    void RebuildZoneMesh(MapGrid grid)
    {
        float zoneHeightOffset = ResolveZoneHeightOffset() + heightOffset;
        var fillVertices = new List<Vector3>();
        var fillTriangles = new List<int>();
        var edgeVertices = new List<Vector3>();
        var edgeTriangles = new List<int>();

        zoneCells.Clear();
        zoneCells.UnionWith(providerCells);
        zoneCells.UnionWith(previewCells);

        foreach (Vector2Int cell in zoneCells)
        {
            if (!TryGetCachedSurfaceHeight(cell, out float surfaceY))
                continue;

            AddCellQuad(
                fillVertices,
                fillTriangles,
                grid,
                cell,
                surfaceY,
                zoneHeightOffset);
            AddBoundaryEdges(
                edgeVertices,
                edgeTriangles,
                grid,
                cell,
                surfaceY,
                zoneHeightOffset);
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

    bool TryGetCachedSurfaceHeight(Vector2Int cell, out float surfaceY)
    {
        return providerCellHeights.TryGetValue(cell, out surfaceY) ||
               previewCellHeights.TryGetValue(cell, out surfaceY);
    }

    void RebuildProviderCells(MapGrid grid)
    {
        providerCells.Clear();
        providerCellHeights.Clear();

        for (int i = 0; i < displayProviders.Count; i++)
        {
            BuildZoneProvider provider = displayProviders[i];

            if (provider == null || provider.buildRadiusCells <= 0)
                continue;

            provider.RefreshCenterCell();
            AddPlaceableCells(
                grid,
                provider.CenterCell,
                provider.buildRadiusCells,
                placementFootprint,
                providerCells,
                providerCellHeights);
        }
    }

    void RebuildPreviewCells(MapGrid grid)
    {
        previewCells.Clear();
        previewCellHeights.Clear();

        if (!previewActive || previewRadius <= 0)
            return;

        // 미리보기는 이 건물이 앞으로 만들 구역이라 지금 배치와 무관합니다.
        // 발자국만큼 좁히면 안 되고 구역 자체를 그대로 보여줍니다.
        AddPlaceableCells(
            grid,
            previewCenter,
            previewRadius,
            Vector2Int.one,
            previewCells,
            previewCellHeights);
    }

    /// <summary>
    /// 반경 안의 칸 중, 주어진 발자국이 통째로 이 구역에 들어가는 배치가 실제로 존재하는
    /// 칸만 모읍니다. 칸 하나씩만 검사하면 2x2 이상 건물에서 구역이 한 칸 넓게 보입니다.
    /// </summary>
    void AddPlaceableCells(
        MapGrid grid,
        Vector2Int center,
        int radius,
        Vector2Int footprint,
        HashSet<Vector2Int> results,
        Dictionary<Vector2Int, float> heights)
    {
        radiusCellScratch.Clear();

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.y - radius; z <= center.y + radius; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);

                // 맵 격자 밖이거나 NavMesh가 없는 칸은 건설할 수 없으므로
                // 발자국 검사 전에 빼야 구멍 주변에서도 표시가 맞습니다.
                if (!grid.IsFootprintInRect(cell, Vector2Int.one))
                    continue;

                if (!BuildZoneProvider.ContainsCell(cell, center, radius))
                    continue;

                if (!TryGetBuildableSurfaceHeight(grid, cell, out float surfaceY))
                    continue;

                radiusCellScratch.Add(cell);
                heights[cell] = surfaceY;
            }
        }

        int width = Mathf.Max(1, footprint.x);
        int depth = Mathf.Max(1, footprint.y);

        if (width == 1 && depth == 1)
        {
            results.UnionWith(radiusCellScratch);
            return;
        }

        float floorTolerance = grid.FloorHeightTolerance;

        foreach (Vector2Int origin in radiusCellScratch)
        {
            if (!IsFootprintInside(radiusCellScratch, origin, width, depth))
                continue;

            // 건설 판정은 발자국 전체가 같은 층에 있어야 통과합니다.
            if (!IsFootprintOnSharedFloor(heights, origin, width, depth, floorTolerance))
                continue;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                    results.Add(new Vector2Int(origin.x + x, origin.y + z));
            }
        }
    }

    static bool IsFootprintInside(
        HashSet<Vector2Int> cells,
        Vector2Int origin,
        int width,
        int depth)
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                if (!cells.Contains(new Vector2Int(origin.x + x, origin.y + z)))
                    return false;
            }
        }

        return true;
    }

    static bool IsFootprintOnSharedFloor(
        Dictionary<Vector2Int, float> heights,
        Vector2Int origin,
        int width,
        int depth,
        float tolerance)
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                if (!heights.TryGetValue(
                        new Vector2Int(origin.x + x, origin.y + z),
                        out float y))
                {
                    return false;
                }

                min = Mathf.Min(min, y);
                max = Mathf.Max(max, y);
            }
        }

        return max - min <= tolerance;
    }

    float ResolveZoneHeightOffset()
    {
        for (int i = 0; i < displayProviders.Count; i++)
        {
            if (displayProviders[i] != null)
                return displayProviders[i].buildZoneHeightOffset;
        }

        if (previewActive)
            return previewHeightOffset;

        return 0.06f;
    }

    bool IsCellInDisplayZone(Vector2Int cell)
    {
        return zoneCells.Contains(cell);
    }

    /// <summary>
    /// 건설 판정과 같은 기준으로 칸을 거릅니다. 최상단 NavMesh 높이만 보면
    /// 절벽이나 경사 옆처럼 칸이 일부만 덮인 자리까지 통과해서 구역이 넓게 보입니다.
    /// </summary>
    static bool TryGetBuildableSurfaceHeight(MapGrid grid, Vector2Int cell, out float surfaceY)
    {
        if (grid.UsesNavMesh)
            return grid.TryGetBuildableNavMeshHeight(cell, out surfaceY);

        Vector3 center = grid.GetCellCenterWorld(cell);
        surfaceY = grid.SampleGroundHeight(center);
        return true;
    }

    void AddBoundaryEdges(
        List<Vector3> vertices,
        List<int> triangles,
        MapGrid grid,
        Vector2Int cell,
        float surfaceY,
        float zoneHeightOffset)
    {
        float size = grid.cellSize;
        Vector3 corner = grid.CellCornerToWorld(cell);

        Vector3 bottomLeft = LiftAtHeight(corner, surfaceY, zoneHeightOffset);
        Vector3 bottomRight = LiftAtHeight(
            corner + new Vector3(size, 0f, 0f),
            surfaceY,
            zoneHeightOffset);
        Vector3 topRight = LiftAtHeight(
            corner + new Vector3(size, 0f, size),
            surfaceY,
            zoneHeightOffset);
        Vector3 topLeft = LiftAtHeight(
            corner + new Vector3(0f, 0f, size),
            surfaceY,
            zoneHeightOffset);

        if (ShouldDrawEdge(new Vector2Int(cell.x, cell.y - 1)))
            AddLineQuad(vertices, triangles, bottomLeft, bottomRight);

        if (ShouldDrawEdge(new Vector2Int(cell.x + 1, cell.y)))
            AddLineQuad(vertices, triangles, bottomRight, topRight);

        if (ShouldDrawEdge(new Vector2Int(cell.x, cell.y + 1)))
            AddLineQuad(vertices, triangles, topRight, topLeft);

        if (ShouldDrawEdge(new Vector2Int(cell.x - 1, cell.y)))
            AddLineQuad(vertices, triangles, topLeft, bottomLeft);
    }

    bool ShouldDrawEdge(Vector2Int neighbor)
    {
        return !IsCellInDisplayZone(neighbor);
    }

    void AddCellQuad(
        List<Vector3> vertices,
        List<int> triangles,
        MapGrid grid,
        Vector2Int cell,
        float surfaceY,
        float zoneHeightOffset)
    {
        Vector3 corner = grid.CellCornerToWorld(cell);
        float size = grid.cellSize;
        int start = vertices.Count;

        vertices.Add(LiftAtHeight(corner, surfaceY, zoneHeightOffset));
        vertices.Add(LiftAtHeight(
            corner + new Vector3(size, 0f, 0f),
            surfaceY,
            zoneHeightOffset));
        vertices.Add(LiftAtHeight(
            corner + new Vector3(size, 0f, size),
            surfaceY,
            zoneHeightOffset));
        vertices.Add(LiftAtHeight(
            corner + new Vector3(0f, 0f, size),
            surfaceY,
            zoneHeightOffset));

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

    Vector3 LiftAtHeight(Vector3 worldPoint, float surfaceY, float zoneHeightOffset)
    {
        MapGrid grid = MapGrid.Instance;
        worldPoint.y = surfaceY + zoneHeightOffset;

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
