using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(MinimapBlipManager))]
[DefaultExecutionOrder(200)]
public class RTSMinimap : MonoBehaviour, IPointerClickHandler
{
    [System.Serializable]
    public class LayerColor
    {
        [Tooltip("이 Terrain Layer에 대응하는 미니맵 표시 색입니다.")]
        public Color color = new Color(0.25f, 0.45f, 0.2f, 1f);
    }

    [Header("References")]
    [Tooltip("미니맵에 사용할 Terrain입니다. 비워두면 씬의 activeTerrain을 사용합니다.")]
    public Terrain terrain;

    [Tooltip("미니맵 클릭 시 카메라를 이동시킬 RTS 카메라 컨트롤러입니다. 비워두면 씬에서 자동으로 찾습니다.")]
    public RTSCameraPivotController cameraController;

    [Header("Terrain Layer Colors")]
    [Tooltip("Terrain 텍스처 레이어를 미니맵 색으로 표시합니다.")]
    public bool useTerrainLayerColors = true;

    [Tooltip("미니맵 베이스 텍스처 해상도입니다.")]
    public int textureResolution = 256;

    [Tooltip("Terrain Layer 순서와 동일한 인덱스의 미니맵 색 목록입니다.")]
    public LayerColor[] layerColors =
    {
        new LayerColor { color = new Color(0.22f, 0.48f, 0.18f, 1f) },
        new LayerColor { color = new Color(0.45f, 0.34f, 0.18f, 1f) },
        new LayerColor { color = new Color(0.38f, 0.38f, 0.38f, 1f) },
        new LayerColor { color = new Color(0.18f, 0.36f, 0.52f, 1f) },
    };

    [Header("Camera View")]
    [Tooltip("미니맵에 표시할 현재 카메라 시야 테두리 색입니다.")]
    public Color cameraViewColor = new Color(1f, 1f, 1f, 0.9f);

    [Tooltip("카메라 시야 테두리 두께(픽셀)입니다.")]
    public float cameraViewBorderThickness = 2f;

    private RectTransform minimapRect;
    private float terrainWidth;
    private float terrainLength;
    private Vector3 terrainOrigin;

    private Texture2D minimapTexture;
    private Sprite minimapSprite;

    private RectTransform cameraViewRoot;
    private readonly RectTransform[] cameraViewBorders = new RectTransform[4];

    public RectTransform MinimapRect => minimapRect;

    public bool IsReady =>
        minimapRect != null && terrain != null && terrainWidth > 0f;

    public Vector2 WorldToMinimapLocal(Vector3 worldPosition)
    {
        float normalizedX =
            (worldPosition.x - terrainOrigin.x) / terrainWidth;

        float normalizedZ =
            (worldPosition.z - terrainOrigin.z) / terrainLength;

        Rect rect = minimapRect.rect;

        return new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedX),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedZ));
    }

    public Vector3 MinimapLocalToWorld(Vector2 localPoint)
    {
        Rect rect = minimapRect.rect;

        float normalizedX =
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);

        float normalizedZ =
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        return new Vector3(
            terrainOrigin.x + normalizedX * terrainWidth,
            0f,
            terrainOrigin.z + normalizedZ * terrainLength);
    }

    void Start()
    {
        minimapRect = GetComponent<RectTransform>();

        if (terrain == null)
            terrain = Terrain.activeTerrain;

        if (terrain == null)
        {
            Debug.LogError("RTSMinimap: Terrain not found");
            return;
        }

        terrainWidth = terrain.terrainData.size.x;
        terrainLength = terrain.terrainData.size.z;
        terrainOrigin = terrain.transform.position;

        SyncLayerColorCount();

        if (useTerrainLayerColors)
            RebuildTerrainLayerTexture();

        if (cameraController == null)
            cameraController =
                FindObjectOfType<RTSCameraPivotController>();

        FogOfWarManager fogManager = FogOfWarManager.Instance;

        if (fogManager == null)
            fogManager = FindObjectOfType<FogOfWarManager>();

        if (fogManager != null)
            fogManager.BindMinimap(minimapRect);

        EnsureCameraViewIndicator();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        textureResolution = Mathf.Clamp(textureResolution, 32, 2048);
        SyncLayerColorCount();
    }
#endif

    [ContextMenu("Rebuild Minimap Texture")]
    public void RebuildTerrainLayerTexture()
    {
        if (terrain == null)
            return;

        TerrainData terrainData = terrain.terrainData;

        if (terrainData == null)
            return;

        SyncLayerColorCount();

        int layerCount = terrainData.alphamapLayers;

        if (layerCount <= 0)
            return;

        int alphamapWidth = terrainData.alphamapWidth;
        int alphamapHeight = terrainData.alphamapHeight;
        float[,,] alphamaps =
            terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

        int resolution = textureResolution;
        var pixels = new Color[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = (x + 0.5f) / resolution;
                float normalizedZ = (y + 0.5f) / resolution;

                int alphamapX = Mathf.Clamp(
                    Mathf.FloorToInt(normalizedX * alphamapWidth),
                    0,
                    alphamapWidth - 1);

                int alphamapZ = Mathf.Clamp(
                    Mathf.FloorToInt(normalizedZ * alphamapHeight),
                    0,
                    alphamapHeight - 1);

                Color blendedColor = Color.black;
                float totalWeight = 0f;
                int colorCount = Mathf.Min(layerCount, layerColors.Length);

                for (int layer = 0; layer < colorCount; layer++)
                {
                    float weight = alphamaps[alphamapZ, alphamapX, layer];

                    if (weight <= 0f)
                        continue;

                    blendedColor += layerColors[layer].color * weight;
                    totalWeight += weight;
                }

                if (totalWeight > 0f)
                    blendedColor /= totalWeight;
                else if (layerColors.Length > 0)
                    blendedColor = layerColors[0].color;

                blendedColor.a = 1f;
                pixels[y * resolution + x] = blendedColor;
            }
        }

        if (minimapTexture == null ||
            minimapTexture.width != resolution ||
            minimapTexture.height != resolution)
        {
            ReleaseMinimapTexture();

            minimapTexture = new Texture2D(
                resolution,
                resolution,
                TextureFormat.RGBA32,
                false);

            minimapTexture.wrapMode = TextureWrapMode.Clamp;
            minimapTexture.filterMode = FilterMode.Bilinear;
        }

        minimapTexture.SetPixels(pixels);
        minimapTexture.Apply();

        ApplyMinimapSprite();
    }

    void SyncLayerColorCount()
    {
        if (terrain == null)
            return;

        int layerCount = terrain.terrainData.terrainLayers.Length;

        if (layerCount <= 0)
            return;

        if (layerColors == null || layerColors.Length != layerCount)
        {
            var previous = layerColors;
            layerColors = new LayerColor[layerCount];

            for (int i = 0; i < layerCount; i++)
            {
                if (previous != null && i < previous.Length)
                    layerColors[i] = previous[i];
                else
                    layerColors[i] = new LayerColor
                    {
                        color = GetDefaultLayerColor(i)
                    };
            }
        }
    }

    static Color GetDefaultLayerColor(int index)
    {
        Color[] defaults =
        {
            new Color(0.22f, 0.48f, 0.18f, 1f),
            new Color(0.45f, 0.34f, 0.18f, 1f),
            new Color(0.38f, 0.38f, 0.38f, 1f),
            new Color(0.18f, 0.36f, 0.52f, 1f),
        };

        return defaults[index % defaults.Length];
    }

    void ApplyMinimapSprite()
    {
        if (minimapTexture == null)
            return;

        Image image = GetComponent<Image>();

        if (minimapSprite != null)
        {
            if (Application.isPlaying)
                Destroy(minimapSprite);
            else
                DestroyImmediate(minimapSprite);
        }

        minimapSprite = Sprite.Create(
            minimapTexture,
            new Rect(0f, 0f, minimapTexture.width, minimapTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        image.sprite = minimapSprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
    }

    void ReleaseMinimapTexture()
    {
        if (minimapSprite != null)
        {
            if (Application.isPlaying)
                Destroy(minimapSprite);
            else
                DestroyImmediate(minimapSprite);

            minimapSprite = null;
        }

        if (minimapTexture != null)
        {
            if (Application.isPlaying)
                Destroy(minimapTexture);
            else
                DestroyImmediate(minimapTexture);

            minimapTexture = null;
        }
    }

    void OnDestroy()
    {
        ReleaseMinimapTexture();
    }

    void LateUpdate()
    {
        UpdateCameraViewIndicator();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (cameraController == null || minimapRect == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                minimapRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            return;

        cameraController.FocusOnPosition(MinimapLocalToWorld(localPoint));
    }

    void EnsureCameraViewIndicator()
    {
        if (cameraViewRoot != null || minimapRect == null)
            return;

        GameObject rootObject = new GameObject(
            "CameraViewIndicator",
            typeof(RectTransform));

        rootObject.transform.SetParent(minimapRect, false);

        cameraViewRoot = rootObject.GetComponent<RectTransform>();
        cameraViewRoot.anchorMin = new Vector2(0.5f, 0.5f);
        cameraViewRoot.anchorMax = new Vector2(0.5f, 0.5f);
        cameraViewRoot.pivot = new Vector2(0.5f, 0.5f);

        string[] borderNames = { "Top", "Right", "Bottom", "Left" };

        for (int i = 0; i < cameraViewBorders.Length; i++)
        {
            GameObject borderObject = new GameObject(
                borderNames[i],
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            borderObject.transform.SetParent(cameraViewRoot, false);

            RectTransform borderRect =
                borderObject.GetComponent<RectTransform>();

            borderRect.anchorMin = new Vector2(0.5f, 0.5f);
            borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.pivot = new Vector2(0.5f, 0.5f);

            Image borderImage = borderObject.GetComponent<Image>();
            borderImage.color = cameraViewColor;
            borderImage.raycastTarget = false;

            cameraViewBorders[i] = borderRect;
        }

        cameraViewRoot.gameObject.SetActive(false);
    }

    void UpdateCameraViewIndicator()
    {
        if (cameraController == null || minimapRect == null)
            return;

        EnsureCameraViewIndicator();

        if (!cameraController.TryGetVisibleGroundBounds(
                out float minX,
                out float maxX,
                out float minZ,
                out float maxZ))
        {
            if (cameraViewRoot != null)
                cameraViewRoot.gameObject.SetActive(false);

            return;
        }

        cameraViewRoot.gameObject.SetActive(true);
        cameraViewRoot.SetAsLastSibling();

        Vector2 localMin = WorldToMinimapLocal(
            new Vector3(minX, 0f, minZ));

        Vector2 localMax = WorldToMinimapLocal(
            new Vector3(maxX, 0f, maxZ));

        float localMinX = localMin.x;
        float localMaxX = localMax.x;
        float localMinY = localMin.y;
        float localMaxY = localMax.y;

        float width = localMaxX - localMinX;
        float height = localMaxY - localMinY;
        float thickness = cameraViewBorderThickness;

        SetBorderRect(
            cameraViewBorders[0],
            new Vector2(
                (localMinX + localMaxX) * 0.5f,
                localMaxY - thickness * 0.5f),
            new Vector2(width, thickness));

        SetBorderRect(
            cameraViewBorders[1],
            new Vector2(
                localMaxX - thickness * 0.5f,
                (localMinY + localMaxY) * 0.5f),
            new Vector2(thickness, height));

        SetBorderRect(
            cameraViewBorders[2],
            new Vector2(
                (localMinX + localMaxX) * 0.5f,
                localMinY + thickness * 0.5f),
            new Vector2(width, thickness));

        SetBorderRect(
            cameraViewBorders[3],
            new Vector2(
                localMinX + thickness * 0.5f,
                (localMinY + localMaxY) * 0.5f),
            new Vector2(thickness, height));

        foreach (RectTransform border in cameraViewBorders)
        {
            if (border == null)
                continue;

            Image borderImage = border.GetComponent<Image>();

            if (borderImage != null)
                borderImage.color = cameraViewColor;
        }
    }

    static void SetBorderRect(
        RectTransform border,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        border.anchoredPosition = anchoredPosition;
        border.sizeDelta = sizeDelta;
    }
}
