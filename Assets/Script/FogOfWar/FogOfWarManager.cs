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

    [Tooltip("Terrain/메쉬 Collider를 레이캐스트로 찾을 레이어입니다.")]
    public LayerMask groundRaycastMask = ~0;

    [Tooltip("표면 레이캐스트 시작 높이(지형 위 추가값)입니다.")]
    public float surfaceRaycastHeightPadding = 32f;

    [Tooltip("어떤 표면 샘플도 실패한 정점/삼각형을 오버레이에서 제외합니다.")]
    public bool hideOverlayWithoutSurface = true;

    [Tooltip("지형 표면을 따라가는 메쉬 세그먼트 수입니다. 높을수록 경사에 잘 맞지만 무거워집니다.")]
    public int overlayMeshSegments = 64;

    [Tooltip("지형 표면 위로 띄울 높이입니다. 유닛/지형이 안개를 뚫을 때 올리세요.")]
    public float overlayHeightOffset = 1.2f;

    [Header("Colors")]
    public Color unexploredColor = new Color(0f, 0f, 0f, 0.95f);
    public Color exploredColor = new Color(0f, 0f, 0f, 0.55f);

    public Texture2D FogTexture => fogTexture;
    public int LocalPlayerOwnerId => localPlayerOwnerId;

    private readonly List<FogOfWarVisionSource> visionSources =
        new List<FogOfWarVisionSource>();

    private Terrain terrain;
    private Vector3 mapOrigin;
    private Vector2 mapSize;

    private Texture2D fogTexture;
    private Color32[] fogPixels;

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
        ApplySurfaceSamplingSettings();
        ApplyMaterialSettings(worldFogMaterial);
        ApplyMaterialSettings(uiFogMaterial);

        if (createWorldOverlay)
            CreateWorldOverlay();

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

        terrain = Terrain.activeTerrain;

        if (terrain == null)
        {
            Debug.LogError(
                "FogOfWarManager: MapGrid bounds unavailable and Terrain not found.");
            mapOrigin = Vector3.zero;
            mapSize = new Vector2(256f, 256f);
            return;
        }

        mapOrigin = terrain.transform.position;
        mapSize = new Vector2(
            terrain.terrainData.size.x,
            terrain.terrainData.size.z);
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
        overlayObject.transform.position = mapOrigin;
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

        for (int z = 0; z < vertCountZ; z++)
        {
            float v = z / (float)segmentsZ;

            for (int x = 0; x < vertCountX; x++)
            {
                float u = x / (float)segmentsX;
                float worldX = mapOrigin.x + u * mapSize.x;
                float worldZ = mapOrigin.z + v * mapSize.y;
                int index = z * vertCountX + x;

                uvs[index] = new Vector2(u, v);

                if (FogGroundUtility.TrySampleSurfaceHeight(
                        worldX,
                        worldZ,
                        out float surfaceY))
                {
                    vertices[index] = new Vector3(
                        u * mapSize.x,
                        surfaceY - mapOrigin.y + overlayHeightOffset,
                        v * mapSize.y);
                }
                else
                {
                    vertices[index] = new Vector3(
                        u * mapSize.x,
                        hideOverlayWithoutSurface ? sinkY - mapOrigin.y : overlayHeightOffset,
                        v * mapSize.y);
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

            StampVision(source.GroundPosition, source.VisionRange);
        }

        for (int i = 0; i < fogPixels.Length; i++)
        {
            if (fogPixels[i].g > fogPixels[i].r)
                fogPixels[i].r = fogPixels[i].g;
        }

        fogTexture.SetPixels32(fogPixels);
        fogTexture.Apply(false);
    }

    void StampVision(Vector3 worldPosition, float radius)
    {
        float cellSizeX = mapSize.x / gridWidth;
        float cellSizeZ = mapSize.y / gridHeight;

        int centerX = WorldToGridX(worldPosition.x);
        int centerZ = WorldToGridZ(worldPosition.z);

        int radiusCellsX = Mathf.CeilToInt(radius / cellSizeX);
        int radiusCellsZ = Mathf.CeilToInt(radius / cellSizeZ);

        float radiusSqr = radius * radius;

        int minX = Mathf.Max(0, centerX - radiusCellsX);
        int maxX = Mathf.Min(gridWidth - 1, centerX + radiusCellsX);
        int minZ = Mathf.Max(0, centerZ - radiusCellsZ);
        int maxZ = Mathf.Min(gridHeight - 1, centerZ + radiusCellsZ);

        for (int z = minZ; z <= maxZ; z++)
        {
            float worldZ = mapOrigin.z + (z + 0.5f) * cellSizeZ;

            for (int x = minX; x <= maxX; x++)
            {
                float worldX = mapOrigin.x + (x + 0.5f) * cellSizeX;

                float dx = worldX - worldPosition.x;
                float dz = worldZ - worldPosition.z;
                float distSqr = dx * dx + dz * dz;

                if (distSqr > radiusSqr)
                    continue;

                float strength = CalculateVisionStrength(
                    Mathf.Sqrt(distSqr),
                    radius);

                byte value = (byte)(strength * 255f);
                int index = z * gridWidth + x;

                if (value > fogPixels[index].g)
                    fogPixels[index].g = value;
            }
        }
    }

    float CalculateVisionStrength(float distance, float radius)
    {
        if (visionEdgeSoftness <= 0f)
            return distance <= radius ? 1f : 0f;

        float innerRadius = Mathf.Max(0f, radius - visionEdgeSoftness);

        if (distance <= innerRadius)
            return 1f;

        if (distance >= radius)
            return 0f;

        return 1f - (distance - innerRadius) / visionEdgeSoftness;
    }

    public bool IsVisible(Vector3 worldPosition)
    {
        if (!TrySampleFog(worldPosition, out byte explored, out byte visible))
            return false;

        return visible > (byte)(visibilityThreshold * 255f);
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

        if (fogTexture == null)
            return false;

        int x = WorldToGridX(worldPosition.x);
        int z = WorldToGridZ(worldPosition.z);

        if (x < 0 || x >= gridWidth || z < 0 || z >= gridHeight)
            return false;

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
