using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    [Header("Player")]
    [Tooltip("이 ownerId와 같은 유닛·건물의 시야로 안개를 밝힙니다.")]
    public int localPlayerOwnerId = 1;

    [Header("Grid")]
    [Tooltip("안개 그리드 가로 해상도입니다.")]
    public int gridWidth = 256;

    [Tooltip("안개 그리드 세로 해상도입니다.")]
    public int gridHeight = 256;

    [Header("Vision")]
    [Tooltip("시야 원 가장자리를 부드럽게 fade-out하는 거리(월드 단위)입니다.")]
    public float visionEdgeSoftness = 4f;

    [Tooltip("게임플레이 시야 판정에 필요한 최소 visible 값(0~1)입니다.")]
    [Range(0f, 1f)]
    public float visibilityThreshold = 0.35f;

    [Header("Entity Visibility")]
    [Tooltip("유닛/건물 가시성 판정 시 bounds를 XZ로 확장하는 여유(미터)입니다.")]
    public float entityVisibilityPadding = 0.75f;

    [Tooltip("숨겨진 유닛이 다시 보일 때: 샘플 중 하나라도 이 값 이상이면 표시합니다.")]
    [Range(0f, 1f)]
    public float entityShowThreshold = 0.08f;

    [Tooltip("보이는 유닛을 숨길 때: 모든 샘플이 이 값 이하면 완전히 시야 밖으로 판정합니다.")]
    [Range(0f, 1f)]
    public float entityHideThreshold = 0.02f;

    [Header("Auto Grid Resolution")]
    [Tooltip("켜면 gridWidth/gridHeight 대신 맵 크기와 Auto Grid Cell Size로 해상도를 자동 계산합니다.")]
    public bool autoGridResolution;

    [Tooltip("자동 해상도 사용 시, 안개 그리드 한 칸이 덮는 월드 크기(미터)입니다. 작을수록 정밀하지만 무거워집니다.")]
    public float autoGridCellSize = 1f;

    [Tooltip("자동 해상도의 최소/최대 한 변 칸 수입니다.")]
    public Vector2Int autoGridResolutionRange = new Vector2Int(64, 512);

    [Header("Vision Blocking")]
    [Tooltip("FogOfWarVisionBlocker가 있는 오브젝트(예: 벽) 뒤로는 시야가 뚫고 나가지 못하게 막습니다.")]
    public bool enableVisionBlocking = true;

    [Header("Elevation Vision")]
    [Tooltip("켜면 지형 높이 차이로 시야가 가려집니다(고지대 유리): 높은 곳에서는 낮은 곳이 보이고, 낮은 곳에서는 앞을 가로막는 높은 지형 너머가 안 보입니다.")]
    public bool enableElevationVision = true;

    [Tooltip("지형 높이맵 해상도(가로 기준 칸 수)입니다. Start() 시점에 한 번만 굽습니다. 낮을수록 굽는 속도가 빠르고 가볍습니다.")]
    public int elevationGridResolution = 64;

    [Tooltip("시야 소스의 기본 눈높이(지면 기준, 미터)입니다. 유닛별로 다르게 하려면 FogOfWarVisionSource의 Eye Height Override를 쓰세요.")]
    public float defaultEyeHeight = 1.5f;

    [Header("Update")]
    [Tooltip("안개 텍스처를 갱신하는 간격(초)입니다.")]
    public float updateInterval = 0.1f;

    [Header("Overlay")]
    [Tooltip("월드 위 안개 오버레이를 자동 생성합니다.")]
    public bool createWorldOverlay = true;

    [Tooltip("MapGrid가 있으면 bounds/표면 높이를 MapGrid(NavMesh) 기준으로 사용합니다.")]
    public bool useMapGridWhenAvailable = true;

    [Header("Surface Sampling")]
    [Tooltip("오버레이 메쉬 높이 샘플 방식입니다. 경사면이 있으면 Visual Geometry를 권장합니다.")]
    public FogSurfaceSampleMode overlaySampleMode =
        FogSurfaceSampleMode.VisualGeometry;

    [Tooltip("메쉬 Collider를 레이캐스트로 찾을 레이어입니다.")]
    public LayerMask groundRaycastMask = ~0;

    [Tooltip("표면 레이캐스트 시작 높이(지형 위 추가값)입니다.")]
    public float surfaceRaycastHeightPadding = 32f;

    [Tooltip("어떤 표면 샘플도 실패한 정점/삼각형을 오버레이에서 제외합니다.")]
    public bool hideOverlayWithoutSurface = true;

    [Tooltip("지형 표면을 따라가는 메쉬 세그먼트 수입니다. 높을수록 경사에 잘 맞지만 무거워집니다.")]
    public int overlayMeshSegments = 64;

    [Tooltip("지형 표면 위로 띄울 높이입니다. 유닛/지형이 안개를 뚫을 때 올리세요.")]
    public float overlayHeightOffset = 1.2f;

    [Tooltip("플레이 맵 바깥으로 오버레이 메쉬를 확장하는 거리(미터)입니다. 카메라 각도로 가장자리가 비칠 때 늘리세요.")]
    public float overlayMeshPadding = 48f;

    [Header("Colors")]
    public Color unexploredColor = new Color(0f, 0f, 0f, 0.95f);
    public Color exploredColor = new Color(0f, 0f, 0f, 0.55f);

    public Texture2D FogTexture => fogTexture;
    public int LocalPlayerOwnerId => localPlayerOwnerId;

    private readonly List<FogOfWarVisionSource> visionSources =
        new List<FogOfWarVisionSource>();

    private Vector3 mapOrigin;
    private Vector2 mapSize;

    private Texture2D fogTexture;
    private Color32[] fogPixels;
    private bool[] visionBlocked;
    private int[] blockerIdByCell;
    private readonly List<List<int>> blockerCells = new List<List<int>>();
    private readonly List<float> blockerTopHeight = new List<float>();

    // 안개 그리드와 같은 해상도로 미리 펼쳐둔 지형 높이(칸당 값 1개). 레이마칭 중에는
    // 나눗셈·보간 없이 배열 조회 한 번으로 끝나도록 Start()에서 한 번만 굽는다.
    private float[] elevationHeightByCell;

    private float cellSizeX;
    private float cellSizeZ;

    const float MinElevationSampleDistance = 0.5f;
    const float ElevationSlopeEpsilon = 0.01f;
    const float BlockerLookOverMargin = 0.1f;

    private Material worldFogMaterial;
    private Material uiFogMaterial;
    private Renderer overlayRenderer;
    private Mesh overlayMesh;

    private RawImage minimapFogOverlay;
    private float updateTimer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeTexture();
        CreateMaterials();
    }

    void Start()
    {
        InitializeMap();

        if (autoGridResolution)
            ApplyAutoGridResolution();

        RecomputeCellSize();
        ApplySurfaceSamplingSettings();
        ApplyMaterialSettings(worldFogMaterial);
        ApplyMaterialSettings(uiFogMaterial);

        if (createWorldOverlay)
            CreateWorldOverlay();

        BuildVisionBlockerGrid();
        BuildElevationHeightGrid();

        FogOfWarVisionSource[] existingSources =
            FindObjectsOfType<FogOfWarVisionSource>();

        foreach (FogOfWarVisionSource source in existingSources)
            Register(source);

        UpdateFogTexture();

        RTSMinimap minimap = FindObjectOfType<RTSMinimap>();

        if (minimap != null)
            BindMinimap(minimap.GetComponent<RectTransform>());
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (fogTexture != null)
            Destroy(fogTexture);

        if (worldFogMaterial != null)
            Destroy(worldFogMaterial);

        if (uiFogMaterial != null)
            Destroy(uiFogMaterial);

        if (overlayMesh != null)
            Destroy(overlayMesh);
    }

    void Update()
    {
        updateTimer -= Time.deltaTime;

        if (updateTimer > 0f)
            return;

        updateTimer = updateInterval;
        UpdateFogTexture();
    }

    void InitializeMap()
    {
        if (useMapGridWhenAvailable && MapGrid.Instance != null)
        {
            MapGrid grid = MapGrid.Instance;

            if (grid.UsesNavMesh)
                grid.Refresh();

            mapOrigin = grid.MapOrigin;
            mapSize = grid.MapSize;

            if (mapSize.x > 0f && mapSize.y > 0f)
                return;
        }

        if (MapPlayBounds.TryResolve(
                MapPlayBoundsSource.Auto,
                Vector3.zero,
                new Vector2(256f, 256f),
                out MapPlayBoundsData bounds))
        {
            mapOrigin = bounds.Origin;
            mapSize = new Vector2(bounds.Width, bounds.Length);
            return;
        }

        Debug.LogWarning(
            "FogOfWarManager: Map bounds not found. Using default 256x256 at origin.");

        mapOrigin = Vector3.zero;
        mapSize = new Vector2(256f, 256f);
    }

    void ApplyAutoGridResolution()
    {
        if (mapSize.x <= 0f || mapSize.y <= 0f || autoGridCellSize <= 0f)
            return;

        int newWidth = Mathf.Clamp(
            Mathf.RoundToInt(mapSize.x / autoGridCellSize),
            autoGridResolutionRange.x,
            autoGridResolutionRange.y);

        int newHeight = Mathf.Clamp(
            Mathf.RoundToInt(mapSize.y / autoGridCellSize),
            autoGridResolutionRange.x,
            autoGridResolutionRange.y);

        if (newWidth == gridWidth && newHeight == gridHeight)
            return;

        gridWidth = newWidth;
        gridHeight = newHeight;

        if (fogTexture != null)
            Destroy(fogTexture);

        InitializeTexture();
    }

    void RecomputeCellSize()
    {
        cellSizeX = gridWidth > 0 ? mapSize.x / gridWidth : 0f;
        cellSizeZ = gridHeight > 0 ? mapSize.y / gridHeight : 0f;
    }

    void ApplySurfaceSamplingSettings()
    {
        FogGroundUtility.overlaySampleMode = overlaySampleMode;
        FogGroundUtility.groundRaycastMask = groundRaycastMask;
        FogGroundUtility.raycastHeightPadding = surfaceRaycastHeightPadding;
    }

    void InitializeTexture()
    {
        fogTexture = new Texture2D(
            gridWidth,
            gridHeight,
            TextureFormat.RG16,
            false,
            true);

        fogTexture.filterMode = FilterMode.Bilinear;
        fogTexture.wrapMode = TextureWrapMode.Clamp;

        fogPixels = new Color32[gridWidth * gridHeight];

        for (int i = 0; i < fogPixels.Length; i++)
            fogPixels[i] = new Color32(0, 0, 0, 255);

        fogTexture.SetPixels32(fogPixels);
        fogTexture.Apply(false);
    }

    void CreateMaterials()
    {
        Shader worldShader = Shader.Find("RTS/FogOfWar");
        Shader uiShader = Shader.Find("RTS/FogOfWarUI");

        if (worldShader == null)
            Debug.LogError("FogOfWarManager: RTS/FogOfWar shader not found");

        if (uiShader == null)
            Debug.LogError("FogOfWarManager: RTS/FogOfWarUI shader not found");

        worldFogMaterial = new Material(worldShader);
        uiFogMaterial = new Material(uiShader);

        ApplyMaterialSettings(worldFogMaterial);
        ApplyMaterialSettings(uiFogMaterial);
    }

    void ApplyMaterialSettings(Material material)
    {
        if (material == null)
            return;

        material.SetTexture("_FogTex", fogTexture);
        material.SetVector(
            "_MapOrigin",
            new Vector4(mapOrigin.x, mapOrigin.z, 0f, 0f));
        material.SetVector(
            "_MapSize",
            new Vector4(mapSize.x, mapSize.y, 0f, 0f));
        material.SetColor("_UnexploredColor", unexploredColor);
        material.SetColor("_ExploredColor", exploredColor);
    }

    void CreateWorldOverlay()
    {
        if (mapSize.x <= 0f || mapSize.y <= 0f)
            return;

        GameObject overlayObject = new GameObject("FogOfWarOverlay");
        overlayObject.transform.SetParent(transform, false);

        float padding = Mathf.Max(0f, overlayMeshPadding);
        overlayObject.transform.position = new Vector3(
            mapOrigin.x - padding,
            mapOrigin.y,
            mapOrigin.z - padding);
        overlayObject.transform.rotation = Quaternion.identity;

        overlayMesh = BuildSurfaceFollowingMesh(
            overlayMeshSegments,
            overlayMeshSegments);

        if (overlayMesh == null)
            return;

        MeshFilter meshFilter = overlayObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = overlayMesh;

        overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
        overlayRenderer.sharedMaterial = worldFogMaterial;
        overlayRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
    }

    Mesh BuildSurfaceFollowingMesh(int segmentsX, int segmentsZ)
    {
        int vertCountX = segmentsX + 1;
        int vertCountZ = segmentsZ + 1;
        int vertCount = vertCountX * vertCountZ;

        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[segmentsX * segmentsZ * 6];
        float sinkY = mapOrigin.y - 100f;
        float padding = Mathf.Max(0f, overlayMeshPadding);
        float meshWidth = mapSize.x + padding * 2f;
        float meshLength = mapSize.y + padding * 2f;
        float worldMinX = mapOrigin.x - padding;
        float worldMinZ = mapOrigin.z - padding;

        for (int z = 0; z < vertCountZ; z++)
        {
            float v = z / (float)segmentsZ;

            for (int x = 0; x < vertCountX; x++)
            {
                float u = x / (float)segmentsX;
                float worldX = worldMinX + u * meshWidth;
                float worldZ = worldMinZ + v * meshLength;
                int index = z * vertCountX + x;

                uvs[index] = new Vector2(u, v);

                if (TrySampleOverlaySurfaceHeight(
                        worldX,
                        worldZ,
                        out float surfaceY))
                {
                    vertices[index] = new Vector3(
                        u * meshWidth,
                        surfaceY - mapOrigin.y + overlayHeightOffset,
                        v * meshLength);
                }
                else
                {
                    vertices[index] = new Vector3(
                        u * meshWidth,
                        hideOverlayWithoutSurface ? sinkY - mapOrigin.y : overlayHeightOffset,
                        v * meshLength);
                }
            }
        }

        int triangleIndex = 0;

        for (int z = 0; z < segmentsZ; z++)
        {
            for (int x = 0; x < segmentsX; x++)
            {
                int bottomLeft = z * vertCountX + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + vertCountX;
                int topRight = topLeft + 1;

                if (hideOverlayWithoutSurface &&
                    ShouldSkipOverlayQuad(
                        vertices[bottomLeft],
                        vertices[bottomRight],
                        vertices[topLeft],
                        vertices[topRight],
                        sinkY - mapOrigin.y))
                {
                    continue;
                }

                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = bottomRight;

                triangles[triangleIndex++] = bottomRight;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;
            }
        }

        if (triangleIndex == 0)
            return null;

        if (triangleIndex < triangles.Length)
        {
            int[] trimmed = new int[triangleIndex];
            System.Array.Copy(triangles, trimmed, triangleIndex);
            triangles = trimmed;
        }

        Mesh mesh = new Mesh
        {
            name = "FogOfWarSurfaceMesh"
        };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    bool TrySampleOverlaySurfaceHeight(
        float worldX,
        float worldZ,
        out float surfaceY)
    {
        if (FogGroundUtility.TrySampleSurfaceHeight(worldX, worldZ, out surfaceY))
            return true;

        if (!IsInsidePlayableMap(worldX, worldZ))
        {
            float edgeX = Mathf.Clamp(worldX, mapOrigin.x, mapOrigin.x + mapSize.x);
            float edgeZ = Mathf.Clamp(worldZ, mapOrigin.z, mapOrigin.z + mapSize.y);

            if (FogGroundUtility.TrySampleSurfaceHeight(edgeX, edgeZ, out surfaceY))
                return true;

            surfaceY = mapOrigin.y;
            return true;
        }

        surfaceY = mapOrigin.y;
        return false;
    }

    bool IsInsidePlayableMap(float worldX, float worldZ)
    {
        return worldX >= mapOrigin.x &&
               worldX <= mapOrigin.x + mapSize.x &&
               worldZ >= mapOrigin.z &&
               worldZ <= mapOrigin.z + mapSize.y;
    }

    static bool ShouldSkipOverlayQuad(
        Vector3 bottomLeft,
        Vector3 bottomRight,
        Vector3 topLeft,
        Vector3 topRight,
        float sinkLocalY)
    {
        return IsSunkVertex(bottomLeft, sinkLocalY) ||
               IsSunkVertex(bottomRight, sinkLocalY) ||
               IsSunkVertex(topLeft, sinkLocalY) ||
               IsSunkVertex(topRight, sinkLocalY);
    }

    static bool IsSunkVertex(Vector3 localVertex, float sinkLocalY)
    {
        return localVertex.y <= sinkLocalY + 0.01f;
    }

    void BuildVisionBlockerGrid()
    {
        int cellCount = gridWidth * gridHeight;
        visionBlocked = new bool[cellCount];
        blockerIdByCell = new int[cellCount];

        for (int i = 0; i < cellCount; i++)
            blockerIdByCell[i] = -1;

        blockerCells.Clear();
        blockerTopHeight.Clear();

        if (!enableVisionBlocking || mapSize.x <= 0f || mapSize.y <= 0f)
            return;

        FogOfWarVisionBlocker[] blockers = FindObjectsOfType<FogOfWarVisionBlocker>();

        foreach (FogOfWarVisionBlocker blocker in blockers)
            MarkBlockerBounds(blocker.GetWorldBounds());
    }

    /// <summary>
    /// 이 오브젝트가 차지하는 칸들을 하나의 "블로커"로 등록한다.
    /// 같은 블로커에 속한 칸끼리는 서로를 가리지 않는다(오브젝트 자신은 항상 온전히 밝혀짐).
    /// 블로커의 꼭대기 높이(worldBounds.max.y)도 같이 기록해서, 눈높이가 그보다 높은
    /// 시야 소스는 이 블로커를 내려다보며 넘어갈 수 있게 한다(언덕 위에서 절벽 아래를
    /// 내려다보는 경우 등).
    /// </summary>
    void MarkBlockerBounds(Bounds worldBounds)
    {
        int minX = WorldToGridX(worldBounds.min.x);
        int maxX = WorldToGridX(worldBounds.max.x);
        int minZ = WorldToGridZ(worldBounds.min.z);
        int maxZ = WorldToGridZ(worldBounds.max.z);

        int blockerId = blockerCells.Count;
        List<int> cells = new List<int>();
        blockerTopHeight.Add(worldBounds.max.y);

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int index = z * gridWidth + x;
                visionBlocked[index] = true;
                blockerIdByCell[index] = blockerId;
                cells.Add(index);
            }
        }

        blockerCells.Add(cells);
    }

    /// <summary>
    /// 지형 표면 높이를 낮은 해상도 그리드로 한 번만 레이캐스트/NavMesh 샘플링해서 구운 뒤,
    /// 그 결과를 안개 그리드와 같은 해상도로 한 번에 확장해둔다(둘 다 Start()에서 1회만
    /// 실행). 이렇게 해두면 매 틱 레이마칭 중에는 칸마다 나눗셈·보간 없이 배열 조회
    /// 한 번(SampleTerrainHeightAtCell)으로 높이를 얻을 수 있다.
    /// </summary>
    void BuildElevationHeightGrid()
    {
        elevationHeightByCell = null;

        if (!enableElevationVision ||
            mapSize.x <= 0f || mapSize.y <= 0f ||
            gridWidth <= 0 || gridHeight <= 0)
        {
            return;
        }

        int coarseWidth = Mathf.Max(2, elevationGridResolution);
        int coarseHeight = Mathf.Max(
            2,
            Mathf.RoundToInt(elevationGridResolution * (mapSize.y / mapSize.x)));

        float[] coarseHeights = new float[coarseWidth * coarseHeight];

        for (int z = 0; z < coarseHeight; z++)
        {
            float v = z / (float)(coarseHeight - 1);
            float worldZ = mapOrigin.z + v * mapSize.y;

            for (int x = 0; x < coarseWidth; x++)
            {
                float u = x / (float)(coarseWidth - 1);
                float worldX = mapOrigin.x + u * mapSize.x;

                coarseHeights[z * coarseWidth + x] =
                    FogGroundUtility.TrySampleSurfaceHeight(worldX, worldZ, out float height)
                        ? height
                        : mapOrigin.y;
            }
        }

        elevationHeightByCell = new float[gridWidth * gridHeight];

        for (int z = 0; z < gridHeight; z++)
        {
            float fz = ((z + 0.5f) / gridHeight) * (coarseHeight - 1);
            int z0 = Mathf.FloorToInt(fz);
            int z1 = Mathf.Min(z0 + 1, coarseHeight - 1);
            float tz = fz - z0;

            for (int x = 0; x < gridWidth; x++)
            {
                float fx = ((x + 0.5f) / gridWidth) * (coarseWidth - 1);
                int x0 = Mathf.FloorToInt(fx);
                int x1 = Mathf.Min(x0 + 1, coarseWidth - 1);
                float tx = fx - x0;

                float h00 = coarseHeights[z0 * coarseWidth + x0];
                float h10 = coarseHeights[z0 * coarseWidth + x1];
                float h01 = coarseHeights[z1 * coarseWidth + x0];
                float h11 = coarseHeights[z1 * coarseWidth + x1];

                elevationHeightByCell[z * gridWidth + x] = Mathf.Lerp(
                    Mathf.Lerp(h00, h10, tx),
                    Mathf.Lerp(h01, h11, tx),
                    tz);
            }
        }
    }

    float SampleTerrainHeightAtCell(int x, int z)
    {
        return elevationHeightByCell != null
            ? elevationHeightByCell[z * gridWidth + x]
            : mapOrigin.y;
    }

    public void Register(FogOfWarVisionSource source)
    {
        if (source == null || visionSources.Contains(source))
            return;

        visionSources.Add(source);
    }

    public void Unregister(FogOfWarVisionSource source)
    {
        if (source == null)
            return;

        visionSources.Remove(source);
    }

    public void BindMinimap(RectTransform minimapRect)
    {
        if (minimapRect == null || uiFogMaterial == null)
            return;

        Transform existing = minimapRect.Find("FogOverlay");

        if (existing != null)
            minimapFogOverlay = existing.GetComponent<RawImage>();

        if (minimapFogOverlay == null)
        {
            GameObject overlayObject = new GameObject(
                "FogOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));

            overlayObject.transform.SetParent(minimapRect, false);

            RectTransform overlayRect =
                overlayObject.GetComponent<RectTransform>();

            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            minimapFogOverlay = overlayObject.GetComponent<RawImage>();
            minimapFogOverlay.raycastTarget = false;
        }

        minimapFogOverlay.material = uiFogMaterial;
        minimapFogOverlay.texture = fogTexture;
        minimapFogOverlay.color = Color.white;
    }

    void UpdateFogTexture()
    {
        for (int i = 0; i < fogPixels.Length; i++)
            fogPixels[i].g = 0;

        for (int i = visionSources.Count - 1; i >= 0; i--)
        {
            FogOfWarVisionSource source = visionSources[i];

            if (source == null)
            {
                visionSources.RemoveAt(i);
                continue;
            }

            if (source.OwnerId != localPlayerOwnerId)
                continue;

            StampVision(
                source.GroundPosition,
                source.VisionRange,
                ResolveEdgeSoftness(source),
                ResolveEyeHeight(source));
        }

        for (int i = 0; i < fogPixels.Length; i++)
        {
            if (fogPixels[i].g > fogPixels[i].r)
                fogPixels[i].r = fogPixels[i].g;
        }

        fogTexture.SetPixels32(fogPixels);
        fogTexture.Apply(false);
    }

    float ResolveEdgeSoftness(FogOfWarVisionSource source)
    {
        return source.EdgeSoftnessOverride >= 0f
            ? source.EdgeSoftnessOverride
            : visionEdgeSoftness;
    }

    float ResolveEyeHeight(FogOfWarVisionSource source)
    {
        return source.EyeHeightOverride >= 0f
            ? source.EyeHeightOverride
            : defaultEyeHeight;
    }

    /// <summary>
    /// 시야 반경(radius) 안을 원형으로 채우되, FogOfWarVisionBlocker가 있는 칸(벽 등)을 만나면
    /// 그 칸까지만 밝히고 뒤쪽으로는 더 이상 뻗어나가지 않습니다(레이마칭 기반 Line-of-Sight).
    /// 지형 높이 차도 광선을 따라 고도각으로 추적합니다: 눈높이보다 낮거나 같은 지형은
    /// 항상 보이므로 높은 곳에서는 아래가 전부 보이고, 눈높이보다 높은(오르막) 지형만
    /// 지평선 규칙을 적용해서 낮은 곳에서는 가까운 높은 지형 너머가 안 보이게 합니다.
    /// </summary>
    void StampVision(Vector3 worldPosition, float radius, float edgeSoftness, float eyeHeight)
    {
        int centerX = WorldToGridX(worldPosition.x);
        int centerZ = WorldToGridZ(worldPosition.z);

        int radiusCellsX = Mathf.Max(1, Mathf.CeilToInt(radius / cellSizeX));
        int radiusCellsZ = Mathf.Max(1, Mathf.CeilToInt(radius / cellSizeZ));

        int minX = Mathf.Max(0, centerX - radiusCellsX);
        int maxX = Mathf.Min(gridWidth - 1, centerX + radiusCellsX);
        int minZ = Mathf.Max(0, centerZ - radiusCellsZ);
        int maxZ = Mathf.Min(gridHeight - 1, centerZ + radiusCellsZ);

        // 소스가 서 있는 칸은 항상 밝힌다.
        float centerSlope = float.NegativeInfinity;
        StampVisionCell(centerX, centerZ, worldPosition, eyeHeight, radius, edgeSoftness, ref centerSlope);

        // 경계 상자의 테두리 칸으로만 광선을 쏘고, 각 광선은 시야를 막는 칸을 만나면 멈춘다.
        // (박스 전체를 채우는 것과 점근적으로 같은 비용이면서 차단 여부를 반영할 수 있다.)
        for (int x = minX; x <= maxX; x++)
        {
            CastVisionRay(centerX, centerZ, x, minZ, worldPosition, eyeHeight, radius, edgeSoftness);

            if (maxZ != minZ)
                CastVisionRay(centerX, centerZ, x, maxZ, worldPosition, eyeHeight, radius, edgeSoftness);
        }

        for (int z = minZ + 1; z <= maxZ - 1; z++)
        {
            CastVisionRay(centerX, centerZ, minX, z, worldPosition, eyeHeight, radius, edgeSoftness);

            if (maxX != minX)
                CastVisionRay(centerX, centerZ, maxX, z, worldPosition, eyeHeight, radius, edgeSoftness);
        }
    }

    void CastVisionRay(
        int x0,
        int z0,
        int x1,
        int z1,
        Vector3 sourceWorldPos,
        float eyeHeight,
        float radius,
        float edgeSoftness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dz = Mathf.Abs(z1 - z0);
        int sx = x0 < x1 ? 1 : -1;
        int sz = z0 < z1 ? 1 : -1;
        int err = dx - dz;

        int x = x0;
        int z = z0;
        bool isSourceCell = true;

        // 광선을 따라 지금까지 관측된 가장 높은 고도각(지평선). 눈높이보다 높은(오르막)
        // 칸 중 이 각도를 못 넘는 칸만 가려진 것으로 보고 밝히지 않는다(광선 자체는 계속
        // 진행). 눈높이보다 낮거나 같은 칸은 이 규칙과 무관하게 항상 보인다.
        float maxSlope = float.NegativeInfinity;

        while (true)
        {
            bool canContinue = StampVisionCell(x, z, sourceWorldPos, eyeHeight, radius, edgeSoftness, ref maxSlope);

            // 소스 자신의 칸(광선의 첫 걸음)이 차단 칸이어도 광선은 계속 나가야 한다.
            // 그래야 벽 옆·위에 선 유닛이 스스로의 시야까지 막아버리지 않는다.
            if (!canContinue && !isSourceCell)
                return;

            isSourceCell = false;

            if (x == x1 && z == z1)
                return;

            int e2 = 2 * err;

            if (e2 > -dz)
            {
                err -= dz;
                x += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                z += sz;
            }
        }
    }

    Vector2 GetCellWorldXZ(int x, int z)
    {
        return new Vector2(
            mapOrigin.x + (x + 0.5f) * cellSizeX,
            mapOrigin.z + (z + 0.5f) * cellSizeZ);
    }

    /// <summary>
    /// 이 칸을 밝히고(고도각·차단 여부에 따라 밝히지 않을 수도 있음) 광선을 계속 진행해도
    /// 되는지 돌려준다. maxSlope는 이 광선이 지금까지 지나온 지형 중 가장 높게 보인
    /// 고도각으로, 호출할 때마다 갱신된다(광선 하나당 하나씩 유지).
    /// </summary>
    /// <returns>이 칸을 지나 광선을 계속 진행해도 되면 true, 여기서 멈춰야 하면 false.</returns>
    bool StampVisionCell(
        int x,
        int z,
        Vector3 sourceWorldPos,
        float eyeHeight,
        float radius,
        float edgeSoftness,
        ref float maxSlope)
    {
        if (x < 0 || x >= gridWidth || z < 0 || z >= gridHeight)
            return false;

        Vector2 worldXZ = GetCellWorldXZ(x, z);

        float dx = worldXZ.x - sourceWorldPos.x;
        float dz = worldXZ.y - sourceWorldPos.z;
        float distSqr = dx * dx + dz * dz;

        if (distSqr > radius * radius)
            return false;

        float distance = Mathf.Sqrt(distSqr);
        float eyeY = sourceWorldPos.y + eyeHeight;
        bool elevationVisible = true;

        // 아주 가까운 칸(소스 자신의 칸 등)은 거리가 0에 가까워 고도각이 불안정해지므로
        // 최소 거리 밑에서는 고도각 판정을 건너뛰고 항상 보이는 것으로 취급한다.
        if (enableElevationVision && distance >= MinElevationSampleDistance)
        {
            float cellHeight = SampleTerrainHeightAtCell(x, z);
            float slope = (cellHeight - eyeY) / distance;

            // 눈높이보다 낮거나 같은 지형은 "지평선"에 걸리지 않고 항상 보인다 — 언덕
            // 바로 앞이 급하게 꺼지는 지형이어도, 그 아래로 이어지는 낮은 지형 전체가
            // 가려지면 안 되기 때문이다(고지대에서는 아래가 다 보여야 한다).
            // 눈높이보다 높은 지형(오르막)만 지평선 규칙을 적용해서, 저지대에서 가까운
            // 높은 지형 너머의 더 먼 봉우리는 안 보이게 한다.
            bool atOrBelowEyeLevel = cellHeight <= eyeY;

            elevationVisible = atOrBelowEyeLevel || slope >= maxSlope - ElevationSlopeEpsilon;
            maxSlope = Mathf.Max(maxSlope, slope);
        }

        int index = z * gridWidth + x;

        if (elevationVisible)
        {
            float strength = CalculateVisionStrength(distance, radius, edgeSoftness);
            byte value = (byte)(strength * 255f);

            if (value > fogPixels[index].g)
                fogPixels[index].g = value;
        }

        if (visionBlocked != null && visionBlocked[index])
        {
            int blockerId = blockerIdByCell[index];

            // 눈높이가 이 블로커(벽·절벽)의 꼭대기보다 높으면 그 위를 내려다보는 것으로
            // 보고 막지 않는다 — 언덕 위에서 절벽 너머 낮은 바닥이 보여야 하기 때문이다.
            bool seesOverBlocker =
                enableElevationVision &&
                blockerId >= 0 &&
                eyeY >= blockerTopHeight[blockerId] - BlockerLookOverMargin;

            if (!seesOverBlocker)
            {
                if (elevationVisible)
                    IlluminateBlockerFootprint(index, sourceWorldPos, radius, edgeSoftness);

                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 광선이 이 블로커 칸에서 멈췄을 때, 같은 블로커에 속한 나머지 칸도 함께 밝힌다.
    /// 즉 FogOfWarVisionBlocker가 붙은 오브젝트 자신은 시야 범위 안에서 항상 온전히 보이고,
    /// 그 오브젝트를 지나 뒤쪽으로 향하는 시야만 막힌다.
    /// </summary>
    void IlluminateBlockerFootprint(
        int hitCellIndex,
        Vector3 sourceWorldPos,
        float radius,
        float edgeSoftness)
    {
        if (blockerIdByCell == null)
            return;

        int blockerId = blockerIdByCell[hitCellIndex];

        if (blockerId < 0)
            return;

        List<int> cells = blockerCells[blockerId];

        for (int i = 0; i < cells.Count; i++)
        {
            int index = cells[i];

            if (index == hitCellIndex)
                continue;

            int x = index % gridWidth;
            int z = index / gridWidth;
            Vector2 worldXZ = GetCellWorldXZ(x, z);

            float dx = worldXZ.x - sourceWorldPos.x;
            float dz = worldXZ.y - sourceWorldPos.z;
            float distSqr = dx * dx + dz * dz;

            if (distSqr > radius * radius)
                continue;

            float strength = CalculateVisionStrength(
                Mathf.Sqrt(distSqr),
                radius,
                edgeSoftness);

            byte value = (byte)(strength * 255f);

            if (value > fogPixels[index].g)
                fogPixels[index].g = value;
        }
    }

    float CalculateVisionStrength(float distance, float radius, float edgeSoftness)
    {
        if (edgeSoftness <= 0f)
            return distance <= radius ? 1f : 0f;

        float innerRadius = Mathf.Max(0f, radius - edgeSoftness);

        if (distance <= innerRadius)
            return 1f;

        if (distance >= radius)
            return 0f;

        return 1f - (distance - innerRadius) / edgeSoftness;
    }

    public bool IsVisible(Vector3 worldPosition)
    {
        if (!TrySampleFog(worldPosition, out byte explored, out byte visible))
            return false;

        return visible > (byte)(visibilityThreshold * 255f);
    }

    /// <summary>
    /// 스폰 회피용: 적이 플레이어에게 그려질 수 있으면 true.
    /// entityShowThreshold를 써서 시야 가장자리 스폰을 막습니다.
    /// </summary>
    public bool IsVisibleForSpawnAvoidance(Vector3 worldPosition)
    {
        if (!TrySampleFog(worldPosition, out _, out byte visible))
            return false;

        return visible > (byte)(entityShowThreshold * 255f);
    }

    /// <summary>시야 텍스처를 즉시 갱신합니다. 스포너 배치 직전에 호출합니다.</summary>
    public void RefreshVisionNow()
    {
        if (fogTexture == null || fogPixels == null)
            return;

        UpdateFogTexture();
    }

    public bool EvaluateEntityGameplayVisibility(Bounds worldBounds, bool wasVisible)
    {
        if (wasVisible)
            return !IsEntityFullyOutsideVision(worldBounds);

        return IsEntityPartiallyVisible(worldBounds);
    }

    public bool IsEntityPartiallyVisible(Bounds worldBounds)
    {
        byte showCutoff = (byte)(entityShowThreshold * 255f);

        foreach (Vector3 sample in GetEntityGroundSamplePoints(worldBounds))
        {
            if (TrySampleFog(sample, out _, out byte visible) && visible > showCutoff)
                return true;
        }

        return false;
    }

    public bool IsEntityFullyOutsideVision(Bounds worldBounds)
    {
        byte hideCutoff = (byte)(entityHideThreshold * 255f);

        foreach (Vector3 sample in GetEntityGroundSamplePoints(worldBounds))
        {
            if (TrySampleFog(sample, out _, out byte visible) && visible > hideCutoff)
                return false;
        }

        return true;
    }

    public bool IsEntityExplored(Bounds worldBounds)
    {
        foreach (Vector3 sample in GetEntityGroundSamplePoints(worldBounds))
        {
            if (TrySampleFog(sample, out byte explored, out _) && explored > 127)
                return true;
        }

        return false;
    }

    // 참고: TrySampleFog는 X/Z만으로 안개 그리드를 조회하고 Y는 쓰지 않으므로,
    // 여기서 지면 높이를 다시 레이캐스트/NavMesh로 스냅할 필요가 없다(불필요한 비용 제거).
    IEnumerable<Vector3> GetEntityGroundSamplePoints(Bounds worldBounds)
    {
        float padding = entityVisibilityPadding;
        float minX = worldBounds.min.x - padding;
        float maxX = worldBounds.max.x + padding;
        float minZ = worldBounds.min.z - padding;
        float maxZ = worldBounds.max.z + padding;
        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;
        float referenceY = worldBounds.center.y;

        yield return new Vector3(centerX, referenceY, centerZ);
        yield return new Vector3(minX, referenceY, minZ);
        yield return new Vector3(maxX, referenceY, minZ);
        yield return new Vector3(minX, referenceY, maxZ);
        yield return new Vector3(maxX, referenceY, maxZ);
        yield return new Vector3(centerX, referenceY, minZ);
        yield return new Vector3(centerX, referenceY, maxZ);
        yield return new Vector3(minX, referenceY, centerZ);
        yield return new Vector3(maxX, referenceY, centerZ);
    }

    public bool IsExplored(Vector3 worldPosition)
    {
        if (!TrySampleFog(worldPosition, out byte explored, out byte visible))
            return false;

        return explored > 127;
    }

    bool TrySampleFog(
        Vector3 worldPosition,
        out byte explored,
        out byte visible)
    {
        explored = 0;
        visible = 0;

        if (fogTexture == null || fogPixels == null)
            return false;

        if (mapSize.x <= 0f || mapSize.y <= 0f)
            return false;

        float normalizedX = (worldPosition.x - mapOrigin.x) / mapSize.x;
        float normalizedZ = (worldPosition.z - mapOrigin.z) / mapSize.y;

        // 맵 밖은 샘플하지 않는다(가장자리로 Clamp하면 시야 판정이 왜곡된다).
        if (normalizedX < 0f || normalizedX > 1f ||
            normalizedZ < 0f || normalizedZ > 1f)
        {
            return false;
        }

        int x = Mathf.Clamp(
            Mathf.FloorToInt(normalizedX * gridWidth),
            0,
            gridWidth - 1);
        int z = Mathf.Clamp(
            Mathf.FloorToInt(normalizedZ * gridHeight),
            0,
            gridHeight - 1);

        Color32 pixel = fogPixels[z * gridWidth + x];
        explored = pixel.r;
        visible = pixel.g;
        return true;
    }

    int WorldToGridX(float worldX)
    {
        float normalized = (worldX - mapOrigin.x) / mapSize.x;
        return Mathf.Clamp(
            Mathf.FloorToInt(normalized * gridWidth),
            0,
            gridWidth - 1);
    }

    int WorldToGridZ(float worldZ)
    {
        float normalized = (worldZ - mapOrigin.z) / mapSize.y;
        return Mathf.Clamp(
            Mathf.FloorToInt(normalized * gridHeight),
            0,
            gridHeight - 1);
    }
}
