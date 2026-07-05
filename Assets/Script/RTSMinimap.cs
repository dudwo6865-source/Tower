using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(MinimapBlipManager))]
[DefaultExecutionOrder(200)]
public class RTSMinimap : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [Tooltip("미니맵 클릭 시 카메라를 이동시킬 RTS 카메라 컨트롤러입니다. 비워두면 씬에서 자동으로 찾습니다.")]
    public RTSCameraPivotController cameraController;

    [Header("Map Bounds")]
    [Tooltip("Auto: MapGrid(NavMesh) → Manual 순으로 맵 크기를 찾습니다.")]
    public MapPlayBoundsSource boundsSource = MapPlayBoundsSource.Auto;

    [Tooltip("Manual/Auto fallback용 맵 원점(왼쪽 아래)입니다.")]
    public Vector3 manualMapOrigin;

    [Tooltip("Manual/Auto fallback용 맵 크기(X=가로, Y=세로)입니다.")]
    public Vector2 manualMapSize = new Vector2(256f, 256f);

    [Header("Mesh Minimap Texture")]
    [Tooltip("메쉬 지형용 기본 바닥 색입니다.")]
    public Color baseMapColor = new Color(0.22f, 0.48f, 0.18f, 1f);

    [Tooltip("레이캐스트에 맞지 않은 영역 색입니다.")]
    public Color emptyMapColor = new Color(0.08f, 0.1f, 0.12f, 1f);

    [Tooltip("높이 차이에 따라 색을 약간 변화시킵니다.")]
    public bool tintByHeight = true;

    [Tooltip("높이 1m당 밝기 변화량입니다.")]
    public float heightTintStrength = 0.015f;

    [Tooltip("미니맵 텍스처 생성 시 지면 레이캐스트 마스크입니다.")]
    public LayerMask groundRaycastMask = ~0;

    [Tooltip("지면 레이캐스트 시작 높이 여유값입니다.")]
    public float raycastHeightPadding = 64f;

    [Tooltip("미니맵 텍스처 해상도입니다.")]
    public int textureResolution = 256;

    [Header("Camera View")]
    [Tooltip("미니맵에 표시할 현재 카메라 시야 테두리 색입니다.")]
    public Color cameraViewColor = new Color(1f, 1f, 1f, 0.9f);

    [Tooltip("카메라 시야 테두리 두께(픽셀)입니다.")]
    public float cameraViewBorderThickness = 2f;

    private RectTransform minimapRect;
    private MapPlayBoundsData mapBounds;
    private bool mapBoundsValid;

    private Texture2D minimapTexture;
    private Sprite minimapSprite;

    private RectTransform cameraViewRoot;
    private readonly RectTransform[] cameraViewBorders = new RectTransform[4];

    public RectTransform MinimapRect => minimapRect;

    public bool IsReady => minimapRect != null && mapBoundsValid;

    public Vector2 WorldToMinimapLocal(Vector3 worldPosition)
    {
        float normalizedX =
            (worldPosition.x - mapBounds.Origin.x) / mapBounds.Width;

        float normalizedZ =
            (worldPosition.z - mapBounds.Origin.z) / mapBounds.Length;

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
            mapBounds.Origin.x + normalizedX * mapBounds.Width,
            0f,
            mapBounds.Origin.z + normalizedZ * mapBounds.Length);
    }

    void Start()
    {
        minimapRect = GetComponent<RectTransform>();

        if (!ResolveMapBounds())
        {
            Debug.LogError(
                "RTSMinimap: Map bounds not found. MapGrid(NavMesh) 또는 Manual Map Size를 설정하세요.");
            return;
        }

        RebuildMeshMinimapTexture();

        if (cameraController == null)
            cameraController = FindObjectOfType<RTSCameraPivotController>();

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
    }
#endif

    bool ResolveMapBounds()
    {
        mapBoundsValid = MapPlayBounds.TryResolve(
            boundsSource,
            manualMapOrigin,
            manualMapSize,
            out mapBounds);

        return mapBoundsValid;
    }

    [ContextMenu("Rebuild Minimap Texture")]
    public void RebuildMinimapTexture()
    {
        if (!ResolveMapBounds())
            return;

        RebuildMeshMinimapTexture();
    }

    void RebuildMeshMinimapTexture()
    {
        int resolution = textureResolution;
        var pixels = new Color[resolution * resolution];
        float rayDistance = MapPlayBounds.GetRaycastDistance(raycastHeightPadding);
        float referenceHeight = mapBounds.Origin.y;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = (x + 0.5f) / resolution;
                float normalizedZ = (y + 0.5f) / resolution;

                float worldX = mapBounds.Origin.x + normalizedX * mapBounds.Width;
                float worldZ = mapBounds.Origin.z + normalizedZ * mapBounds.Length;

                Color pixelColor = emptyMapColor;
                Vector3 origin = MapPlayBounds.GetRaycastOrigin(
                    worldX,
                    worldZ,
                    raycastHeightPadding);

                if (Physics.Raycast(
                        origin,
                        Vector3.down,
                        out RaycastHit hit,
                        rayDistance,
                        groundRaycastMask,
                        QueryTriggerInteraction.Ignore))
                {
                    pixelColor = baseMapColor;

                    if (tintByHeight)
                    {
                        float heightDelta = hit.point.y - referenceHeight;
                        float tint = 1f + heightDelta * heightTintStrength;
                        pixelColor = new Color(
                            Mathf.Clamp01(pixelColor.r * tint),
                            Mathf.Clamp01(pixelColor.g * tint),
                            Mathf.Clamp01(pixelColor.b * tint),
                            1f);
                    }
                }

                pixelColor.a = 1f;
                pixels[y * resolution + x] = pixelColor;
            }
        }

        ApplyPixelsToMinimapTexture(pixels, resolution);
    }

    void ApplyPixelsToMinimapTexture(Color[] pixels, int resolution)
    {
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
        if (cameraController == null || minimapRect == null || !mapBoundsValid)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                minimapRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            return;

        Vector3 worldPoint = MinimapLocalToWorld(localPoint);
        worldPoint.y = MapPlayBounds.SampleGroundHeight(worldPoint);
        cameraController.FocusOnPosition(worldPoint);
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
        if (cameraController == null || minimapRect == null || !mapBoundsValid)
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
