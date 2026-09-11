using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 배치 미리보기 풋프린트와 건설 가능 구역(Build Zone)을 한 컴포넌트에서 표시합니다.
// 예전에는 PlacementGridVisualizer(풋프린트)와 BuildZoneVisualizer(구역)로 나뉘어 있었는데,
// 인스펙터에서 두 곳을 오가며 조정해야 해서 혼동되기 쉬웠다. 두 표시 모두 같은 "칸 하나를
// inset+둥근 모서리 타일로 그린다"는 방식을 공유하므로 한 컴포넌트로 합쳤다.
[DisallowMultipleComponent]
[DefaultExecutionOrder(140)]
public class GridVisualizer : MonoBehaviour
{
    [Header("Footprint (배치 미리보기)")]
    [Tooltip("건설 가능한 칸을 표시하는 하늘색 레이어 색상입니다.")]
    public Color validFootprintColor = new Color(0.25f, 0.65f, 1f, 0.65f);

    [Tooltip("건설할 수 없는 칸을 표시하는 색상입니다.")]
    public Color invalidFootprintColor = new Color(0.95f, 0.2f, 0.2f, 0.7f);

    [Tooltip("풋프린트를 지형 위로 띄울 높이입니다.")]
    public float heightOffset = 0.12f;

    [Header("Build Zone (건설 가능 구역)")]
    [Tooltip("건설 가능 구역의 칸 색상입니다.")]
    public Color zoneFillColor = new Color(0.12f, 0.55f, 1f, 0.3f);

    [Tooltip("구역을 지형 위로 띄울 추가 높이입니다. BuildZoneProvider.buildZoneHeightOffset에 더해집니다.")]
    public float zoneHeightOffset;

    [Header("Cell Shape (풋프린트/구역 공통)")]
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

    // ------------------------------- Footprint -------------------------------

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

    // ------------------------------- Build Zone -------------------------------

    private MeshRenderer zoneMeshRenderer;
    private Mesh zoneMesh;
    private Material zoneMaterial;

    private readonly List<BuildZoneProvider> displayProviders = new List<BuildZoneProvider>();
    private readonly List<BuildZoneProvider> selectedProviders = new List<BuildZoneProvider>();
    private readonly HashSet<Vector2Int> zoneCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> radiusCellScratch = new HashSet<Vector2Int>();

    // 칸마다 NavMesh를 여러 번 샘플링하므로, 고스트가 움직여도 바뀌지 않는 기존 구역과
    // 매 칸 바뀌는 미리보기 구역을 따로 캐시한다.
    private readonly HashSet<Vector2Int> providerCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, float> providerCellHeights =
        new Dictionary<Vector2Int, float>();
    private readonly HashSet<Vector2Int> previewZoneCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, float> previewZoneCellHeights =
        new Dictionary<Vector2Int, float>();

    private int lastProviderSignature;
    private int lastPreviewSignature;

    private bool previewZoneActive;
    private Vector2Int previewZoneCenter;
    private int previewZoneRadius;
    private float previewZoneHeightOffset = 0.06f;

    private Vector2Int placementFootprint = Vector2Int.one;

    void Awake()
    {
        placementController = GetComponent<TowerPlacementController>();

        if (placementController == null)
            placementController = TowerPlacementController.Instance;

        EnsureVisuals();
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

        SetFootprintVisible(false);
        SetZoneVisible(false);
    }

    void LateUpdate()
    {
        RefreshZoneVisibility();
    }

    void OnDestroy()
    {
        if (footprintMesh != null)
            Destroy(footprintMesh);

        if (footprintMaterial != null)
            Destroy(footprintMaterial);

        if (zoneMesh != null)
            Destroy(zoneMesh);

        if (zoneMaterial != null)
            Destroy(zoneMaterial);
    }

    void OnValidate()
    {
        // Awake가 아직 실행되지 않았다면(런타임에 막 AddComponent된 직후 Unity가 자동으로
        // 호출하는 경우) 여기서 손대지 않는다. Awake가 곧이어 정상적으로 초기화한다.
        // 이 시점에 EnsureVisuals()로 새 GameObject/컴포넌트를 만들면 OnValidate 안에서
        // AddComponent를 호출하는 셈이 되어 Unity가 경고를 띄운다.
        if (visualsRoot == null || footprintMesh == null || footprintMaterial == null)
            return;

        InvalidateFootprintCache();
        InvalidateZoneCache();

        // 배치 중에 색상/inset/모서리 반경 같은 인스펙터 값을 바꾸면, 고스트를 움직이지
        // 않아도 바로 반영되도록 지금 보이고 있는 풋프린트를 즉시 다시 그린다.
        // 구역(zone)은 매 프레임 LateUpdate에서 갱신되므로 캐시 무효화만으로 충분하다.
        if (hasCurrentPreview && footprintMeshRenderer != null && footprintMeshRenderer.gameObject.activeSelf)
        {
            RebuildFootprintMesh(currentOriginCell, currentFootprintCells, currentIsValid);

            lastFootprintOrigin = currentOriginCell;
            lastFootprintSize = currentFootprintCells;
            lastFootprintValid = currentIsValid;
            lastPreferredY = preferredSampleY;
        }
    }

    // ================================ Footprint ================================

    void HandlePreviewChanged(PlacementPreviewState state)
    {
        if (MapGrid.Instance == null)
        {
            hasCurrentPreview = false;
            SetFootprintVisible(false);
            return;
        }

        if (!state.hasPreview)
        {
            hasCurrentPreview = false;
            SetFootprintVisible(false);
            return;
        }

        SyncVisualsRoot();
        SetFootprintVisible(true);

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

    void SetFootprintVisible(bool active)
    {
        if (footprintMeshRenderer == null)
            return;

        GameObject go = footprintMeshRenderer.gameObject;

        if (go.activeSelf == active)
            return;

        go.SetActive(active);

        if (active)
            InvalidateFootprintCache();
        else
            lastFootprintOrigin = new Vector2Int(int.MinValue, int.MinValue);
    }

    void InvalidateFootprintCache()
    {
        lastFootprintOrigin = new Vector2Int(int.MinValue, int.MinValue);
        lastPreferredY = float.NaN;
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
            float worldHeight = preferredSampleY + heightOffset;

            for (int x = 0; x < footprintCells.x; x++)
            {
                for (int z = 0; z < footprintCells.y; z++)
                {
                    Vector2Int cell = new Vector2Int(originCell.x + x, originCell.y + z);
                    AddCellTile(vertices, triangles, grid, cell, worldHeight);
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

    // ================================ Build Zone ================================

    void RefreshZoneVisibility()
    {
        if (MapGrid.Instance == null || BuildZoneManager.Instance == null)
        {
            SetZoneVisible(false);
            return;
        }

        ResolveZonePreview();
        ResolvePlacementFootprint();
        ResolveProvidersToDisplay();

        if (displayProviders.Count == 0 && !previewZoneActive)
        {
            SetZoneVisible(false);
            return;
        }

        SyncVisualsRoot();
        SetZoneVisible(true);

        MapGrid grid = MapGrid.Instance;
        int providerSignature = ComputeProviderSignature();
        int previewSignature = ComputeZonePreviewSignature();
        bool dirty = false;

        if (providerSignature != lastProviderSignature)
        {
            RebuildProviderCells(grid);
            lastProviderSignature = providerSignature;
            dirty = true;
        }

        if (previewSignature != lastPreviewSignature)
        {
            RebuildPreviewZoneCells(grid);
            lastPreviewSignature = previewSignature;
            dirty = true;
        }

        if (dirty)
            RebuildZoneMesh(grid);
    }

    void SetZoneVisible(bool active)
    {
        if (zoneMeshRenderer == null)
            return;

        GameObject go = zoneMeshRenderer.gameObject;

        if (go.activeSelf == active)
            return;

        go.SetActive(active);

        if (!active)
            InvalidateZoneCache();
    }

    void InvalidateZoneCache()
    {
        lastProviderSignature = 0;
        lastPreviewSignature = 0;
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

    void ResolveZonePreview()
    {
        previewZoneActive = false;
        previewZoneRadius = 0;
        previewZoneCenter = Vector2Int.zero;
        previewZoneHeightOffset = 0.06f;

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

        previewZoneActive = true;
        previewZoneRadius = provider.buildRadiusCells;
        previewZoneHeightOffset = provider.buildZoneHeightOffset;
        previewZoneCenter = BuildZoneProvider.GetFootprintCenterCell(
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

    int ComputeZonePreviewSignature()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (previewZoneActive ? 1 : 0);
            hash = hash * 31 + previewZoneCenter.x;
            hash = hash * 31 + previewZoneCenter.y;
            hash = hash * 31 + previewZoneRadius;

            return hash;
        }
    }

    void RebuildZoneMesh(MapGrid grid)
    {
        float extraZoneHeightOffset = ResolveZoneHeightOffset() + zoneHeightOffset;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        zoneCells.Clear();
        zoneCells.UnionWith(providerCells);
        zoneCells.UnionWith(previewZoneCells);

        foreach (Vector2Int cell in zoneCells)
        {
            if (!TryGetCachedZoneSurfaceHeight(cell, out float surfaceY))
                continue;

            AddCellTile(vertices, triangles, grid, cell, surfaceY + extraZoneHeightOffset);
        }

        zoneMesh.Clear();
        zoneMesh.SetVertices(vertices);
        zoneMesh.SetTriangles(triangles, 0);
        zoneMesh.RecalculateBounds();
        zoneMesh.RecalculateNormals();

        zoneMaterial.color = zoneFillColor;
    }

    bool TryGetCachedZoneSurfaceHeight(Vector2Int cell, out float surfaceY)
    {
        return providerCellHeights.TryGetValue(cell, out surfaceY) ||
               previewZoneCellHeights.TryGetValue(cell, out surfaceY);
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

    void RebuildPreviewZoneCells(MapGrid grid)
    {
        previewZoneCells.Clear();
        previewZoneCellHeights.Clear();

        if (!previewZoneActive || previewZoneRadius <= 0)
            return;

        // 미리보기는 이 건물이 앞으로 만들 구역이라 지금 배치와 무관합니다.
        // 발자국만큼 좁히면 안 되고 구역 자체를 그대로 보여줍니다.
        AddPlaceableCells(
            grid,
            previewZoneCenter,
            previewZoneRadius,
            Vector2Int.one,
            previewZoneCells,
            previewZoneCellHeights);
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

        if (previewZoneActive)
            return previewZoneHeightOffset;

        return 0.06f;
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

    // ============================ Shared Cell Tile Geometry ============================

    void EnsureVisuals()
    {
        if (visualsRoot != null)
            return;

        GameObject rootObject = new GameObject("GridVisuals");
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
        footprintMaterial = CreateGroundOverlayMaterial(validFootprintColor, 10);
        footprintMeshRenderer.sharedMaterial = footprintMaterial;
        ConfigureRenderer(footprintMeshRenderer);
        footprintObject.SetActive(false);

        GameObject zoneObject = new GameObject(
            "BuildZone",
            typeof(MeshFilter),
            typeof(MeshRenderer));

        zoneObject.transform.SetParent(visualsRoot, false);
        zoneMeshRenderer = zoneObject.GetComponent<MeshRenderer>();
        zoneMesh = new Mesh { name = "BuildZoneFill" };
        zoneObject.GetComponent<MeshFilter>().sharedMesh = zoneMesh;
        zoneMaterial = CreateGroundOverlayMaterial(zoneFillColor, 5);
        zoneMeshRenderer.sharedMaterial = zoneMaterial;
        ConfigureRenderer(zoneMeshRenderer);
        zoneObject.SetActive(false);
    }

    static void ConfigureRenderer(MeshRenderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
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

    /// <summary>
    /// 칸 하나를 테두리에서 inset만큼 들여, 모서리를 cellCornerRadius로 둥글린 타일로
    /// 그린다. 풋프린트/구역 모두 이 메서드 하나로 그려서 항상 같은 모양으로 보인다.
    /// </summary>
    void AddCellTile(
        List<Vector3> vertices,
        List<int> triangles,
        MapGrid grid,
        Vector2Int cell,
        float worldHeight)
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
            Vector3 worldPoint = corner + new Vector3(p.x, 0f, p.y);
            worldPoint.y = worldHeight;
            vertices.Add(grid.transform.InverseTransformPoint(worldPoint));
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
