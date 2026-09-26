using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 미니맵입니다.
// 이 오브젝트(루트)는 여백을 채우는 배경이고, 실제 맵은 자식 "MapArea"에 그립니다.
// MapArea는 맵의 가로:세로 비율을 그대로 유지하므로 정사각형이 아닌 맵도 찌그러지지 않고,
// 남는 공간은 루트 배경색(기본 검정)으로 채워집니다.
// 블립·시야 안개·카메라 테두리는 모두 MapArea를 기준으로 배치합니다.
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(MinimapBlipManager))]
[DefaultExecutionOrder(200)]
public class RTSMinimap : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    const string MapAreaName = "MapArea";

    [Header("참조")]
    [Label("카메라 컨트롤러")]
    [Tooltip("미니맵 클릭 시 카메라를 이동시킬 RTS 카메라 컨트롤러입니다. 비워두면 씬에서 자동으로 찾습니다.")]
    public RTSCameraPivotController cameraController;

    [Header("맵 범위")]
    [Label("맵 범위 출처")]
    [Tooltip("Auto: MapGrid(NavMesh) → Manual 순으로 맵 크기를 찾습니다.")]
    public MapPlayBoundsSource boundsSource = MapPlayBoundsSource.Auto;

    [Label("수동 맵 원점")]
    [Tooltip("Manual/Auto fallback용 맵 원점(왼쪽 아래)입니다.")]
    public Vector3 manualMapOrigin;

    [Label("수동 맵 크기")]
    [Tooltip("Manual/Auto fallback용 맵 크기(X=가로, Y=세로)입니다.")]
    public Vector2 manualMapSize = new Vector2(256f, 256f);

    [Header("레이아웃")]
    [Label("여백 색")]
    [Tooltip("맵 비율을 맞추고 남는 여백을 채울 색입니다.")]
    public Color letterboxColor = Color.black;

    [Label("맵 영역 여백")]
    [Tooltip("미니맵 테두리에서 안쪽으로 둘 여백(픽셀)입니다.")]
    public float mapAreaPadding = 0f;

    [Header("미니맵 텍스처")]
    [Label("기본 바닥 색")]
    [Tooltip("메쉬 지형용 기본 바닥 색입니다.")]
    public Color baseMapColor = new Color(0.22f, 0.48f, 0.18f, 1f);

    [Label("빈 영역 색")]
    [Tooltip("레이캐스트에 맞지 않은 영역 색입니다.")]
    public Color emptyMapColor = new Color(0.08f, 0.1f, 0.12f, 1f);

    [Label("높이별 색 변화")]
    [Tooltip("높이 차이에 따라 색을 약간 변화시킵니다.")]
    public bool tintByHeight = true;

    [Label("높이 색 변화 강도")]
    [Tooltip("높이 1m당 밝기 변화량입니다.")]
    public float heightTintStrength = 0.015f;

    [Label("지면 레이캐스트 마스크")]
    [Tooltip("미니맵 텍스처 생성 시 지면 레이캐스트 마스크입니다.")]
    public LayerMask groundRaycastMask = ~0;

    [Label("레이캐스트 높이 여유")]
    [Tooltip("지면 레이캐스트 시작 높이 여유값입니다.")]
    public float raycastHeightPadding = 64f;

    [Label("텍스처 해상도")]
    [Tooltip("미니맵 텍스처의 긴 변 해상도입니다. 짧은 변은 맵 비율에 맞춰 줄어듭니다.")]
    public int textureResolution = 256;

    [Header("카메라 시야")]
    [Label("카메라 시야 색")]
    [Tooltip("미니맵에 표시할 현재 카메라 시야 테두리 색입니다.")]
    public Color cameraViewColor = new Color(1f, 1f, 1f, 0.9f);

    [Label("카메라 시야 테두리 두께")]
    [Tooltip("카메라 시야 테두리 두께(픽셀)입니다.")]
    public float cameraViewBorderThickness = 2f;

    [Header("입력")]
    [Label("드래그로 카메라 이동")]
    [Tooltip("켜면 미니맵을 누른 채 끌어서 카메라를 이동할 수 있습니다.")]
    public bool allowDragToPan = true;

    private RectTransform rootRect;
    private RectTransform mapAreaRect;
    private Image mapAreaImage;

    private MapPlayBoundsData mapBounds;
    private bool mapBoundsValid;

    private Texture2D minimapTexture;
    private Sprite minimapSprite;

    private RectTransform cameraViewRoot;
    private readonly RectTransform[] cameraViewBorders = new RectTransform[4];
    private readonly Image[] cameraViewBorderImages = new Image[4];

    private Vector2 lastRootSize = new Vector2(-1f, -1f);

    /// <summary>미니맵 전체 영역입니다. 맵 비율을 맞추고 남는 여백까지 포함합니다.</summary>
    public RectTransform RootRect
    {
        get
        {
            if (rootRect == null)
                rootRect = GetComponent<RectTransform>();

            return rootRect;
        }
    }

    /// <summary>
    /// 맵이 실제로 그려지는 영역입니다. 블립·시야 안개·카메라 테두리 모두 이 사각형을 기준으로 합니다.
    /// 다른 스크립트가 Start 순서상 먼저 물어볼 수 있으므로 여기서 만들어 둡니다.
    /// </summary>
    public RectTransform MinimapRect
    {
        get
        {
            EnsureMapArea();
            return mapAreaRect;
        }
    }

    public bool IsReady => mapAreaRect != null && mapBoundsValid;

    public Vector2 WorldToMinimapLocal(Vector3 worldPosition)
    {
        float normalizedX =
            (worldPosition.x - mapBounds.Origin.x) / mapBounds.Width;

        float normalizedZ =
            (worldPosition.z - mapBounds.Origin.z) / mapBounds.Length;

        Rect rect = MinimapRect.rect;

        return new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedX),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedZ));
    }

    public Vector3 MinimapLocalToWorld(Vector2 localPoint)
    {
        Rect rect = MinimapRect.rect;

        float normalizedX =
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);

        float normalizedZ =
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        return new Vector3(
            mapBounds.Origin.x + normalizedX * mapBounds.Width,
            0f,
            mapBounds.Origin.z + normalizedZ * mapBounds.Length);
    }

    void Awake()
    {
        EnsureMapArea();
    }

    void Start()
    {
        if (!ResolveMapBounds())
        {
            Debug.LogError(
                "RTSMinimap: Map bounds not found. MapGrid(NavMesh) 또는 Manual Map Size를 설정하세요.",
                this);
            return;
        }

        UpdateMapAreaSize();
        RebuildMeshMinimapTexture();

        if (cameraController == null)
            cameraController = FindObjectOfType<RTSCameraPivotController>();

        FogOfWarManager fogManager = FogOfWarManager.Instance;

        if (fogManager == null)
            fogManager = FindObjectOfType<FogOfWarManager>();

        if (fogManager != null)
            fogManager.BindMinimap(MinimapRect);

        EnsureCameraViewIndicator();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        textureResolution = Mathf.Clamp(textureResolution, 32, 2048);
        mapAreaPadding = Mathf.Max(0f, mapAreaPadding);
    }
#endif

    // 미니맵 UI 크기가 바뀌면 맵 영역도 비율을 유지한 채 다시 맞춘다.
    void OnRectTransformDimensionsChange()
    {
        UpdateMapAreaSize();
    }

    // ── 맵 영역(비율 유지) ────────────────────────────────────────

    void EnsureMapArea()
    {
        if (mapAreaRect != null)
            return;

        Transform existing = RootRect.Find(MapAreaName);

        if (existing != null)
        {
            mapAreaRect = existing as RectTransform;
            mapAreaImage = existing.GetComponent<Image>();
        }

        if (mapAreaRect == null)
        {
            GameObject areaObject = new GameObject(
                MapAreaName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            areaObject.transform.SetParent(RootRect, false);

            mapAreaRect = areaObject.GetComponent<RectTransform>();
            mapAreaImage = areaObject.GetComponent<Image>();
        }

        // 클릭 판정은 루트가 받는다. 맵 영역은 그리기만 한다.
        if (mapAreaImage != null)
            mapAreaImage.raycastTarget = false;

        mapAreaRect.anchorMin = new Vector2(0.5f, 0.5f);
        mapAreaRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapAreaRect.pivot = new Vector2(0.5f, 0.5f);
        mapAreaRect.anchoredPosition = Vector2.zero;

        // 블립·시야·카메라 테두리보다 뒤에 그린다.
        mapAreaRect.SetAsFirstSibling();

        ApplyLetterboxBackground();
        UpdateMapAreaSize();
    }

    // 루트는 여백을 채우는 배경이 된다. 맵 그림은 자식(MapArea)이 그린다.
    void ApplyLetterboxBackground()
    {
        Image rootImage = GetComponent<Image>();

        if (rootImage == null)
            return;

        rootImage.sprite = null;
        rootImage.color = letterboxColor;
        rootImage.type = Image.Type.Simple;
        rootImage.preserveAspect = false;
    }

    void UpdateMapAreaSize()
    {
        if (mapAreaRect == null)
            return;

        Rect outer = RootRect.rect;
        float padding = Mathf.Max(0f, mapAreaPadding) * 2f;
        float availableWidth = outer.width - padding;
        float availableHeight = outer.height - padding;

        if (availableWidth <= 0f || availableHeight <= 0f)
            return;

        float mapAspect = GetMapAspect();

        // 가로를 꽉 채워보고, 세로가 넘치면 세로 기준으로 다시 맞춘다.
        float width = availableWidth;
        float height = width / mapAspect;

        if (height > availableHeight)
        {
            height = availableHeight;
            width = height * mapAspect;
        }

        mapAreaRect.sizeDelta = new Vector2(width, height);
    }

    float GetMapAspect()
    {
        if (!mapBoundsValid || mapBounds.Width <= 0f || mapBounds.Length <= 0f)
            return 1f;

        return mapBounds.Width / mapBounds.Length;
    }

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
        EnsureMapArea();

        if (!ResolveMapBounds())
            return;

        UpdateMapAreaSize();
        RebuildMeshMinimapTexture();
    }

    // ── 미니맵 텍스처 ────────────────────────────────────────────

    void RebuildMeshMinimapTexture()
    {
        GetTextureSize(out int textureWidth, out int textureHeight);

        var pixels = new Color[textureWidth * textureHeight];
        float rayDistance = MapPlayBounds.GetRaycastDistance(raycastHeightPadding);
        float referenceHeight = mapBounds.Origin.y;

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                float normalizedX = (x + 0.5f) / textureWidth;
                float normalizedZ = (y + 0.5f) / textureHeight;

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
                pixels[y * textureWidth + x] = pixelColor;
            }
        }

        ApplyPixelsToMinimapTexture(pixels, textureWidth, textureHeight);
    }

    // 맵이 가로로 길면 세로 해상도를 줄인다. 정사각형 텍스처를 쓰면 픽셀 밀도가 축마다 달라진다.
    void GetTextureSize(out int textureWidth, out int textureHeight)
    {
        float mapAspect = GetMapAspect();
        int longSide = Mathf.Clamp(textureResolution, 32, 2048);

        if (mapAspect >= 1f)
        {
            textureWidth = longSide;
            textureHeight = Mathf.Max(8, Mathf.RoundToInt(longSide / mapAspect));
            return;
        }

        textureHeight = longSide;
        textureWidth = Mathf.Max(8, Mathf.RoundToInt(longSide * mapAspect));
    }

    void ApplyPixelsToMinimapTexture(Color[] pixels, int textureWidth, int textureHeight)
    {
        if (minimapTexture == null ||
            minimapTexture.width != textureWidth ||
            minimapTexture.height != textureHeight)
        {
            ReleaseMinimapTexture();

            minimapTexture = new Texture2D(
                textureWidth,
                textureHeight,
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

        EnsureMapArea();

        if (mapAreaImage == null)
            return;

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

        mapAreaImage.sprite = minimapSprite;
        mapAreaImage.color = Color.white;
        mapAreaImage.type = Image.Type.Simple;

        // 영역 자체가 이미 맵 비율이므로 여기서 또 맞출 필요가 없다.
        mapAreaImage.preserveAspect = false;
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
        // 캔버스 레이아웃은 Awake/Start 시점에 아직 확정되지 않을 수 있다.
        // 해상도나 캔버스 스케일이 바뀌는 경우까지 함께 받으려고 크기 변화를 확인한다.
        Vector2 rootSize = RootRect.rect.size;

        if (rootSize != lastRootSize)
        {
            lastRootSize = rootSize;
            UpdateMapAreaSize();
        }

        UpdateCameraViewIndicator();
    }

    // ── 입력 ────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        FocusFromPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!allowDragToPan)
            return;

        FocusFromPointer(eventData);
    }

    void FocusFromPointer(PointerEventData eventData)
    {
        if (cameraController == null || !IsReady)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                MinimapRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            return;

        // 비율을 맞추고 남은 여백(검은 부분)은 맵 밖이므로 무시한다.
        if (!MinimapRect.rect.Contains(localPoint))
            return;

        Vector3 worldPoint = MinimapLocalToWorld(localPoint);
        worldPoint.y = MapPlayBounds.SampleGroundHeight(worldPoint);
        cameraController.FocusOnPosition(worldPoint);
    }

    // ── 카메라 시야 테두리 ────────────────────────────────────────

    void EnsureCameraViewIndicator()
    {
        if (cameraViewRoot != null)
            return;

        EnsureMapArea();

        if (mapAreaRect == null)
            return;

        GameObject rootObject = new GameObject(
            "CameraViewIndicator",
            typeof(RectTransform));

        rootObject.transform.SetParent(mapAreaRect, false);

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
            cameraViewBorderImages[i] = borderImage;
        }

        cameraViewRoot.gameObject.SetActive(false);
    }

    void UpdateCameraViewIndicator()
    {
        if (cameraController == null || !IsReady)
            return;

        EnsureCameraViewIndicator();

        if (cameraViewRoot == null)
            return;

        if (!cameraController.TryGetVisibleGroundBounds(
                out float minX,
                out float maxX,
                out float minZ,
                out float maxZ))
        {
            cameraViewRoot.gameObject.SetActive(false);
            return;
        }

        cameraViewRoot.gameObject.SetActive(true);
        cameraViewRoot.SetAsLastSibling();

        Vector2 localMin = WorldToMinimapLocal(new Vector3(minX, 0f, minZ));
        Vector2 localMax = WorldToMinimapLocal(new Vector3(maxX, 0f, maxZ));

        // 카메라가 맵 밖을 보고 있어도 테두리가 미니맵 밖으로 삐져나가지 않게 자른다.
        Rect area = MinimapRect.rect;

        float localMinX = Mathf.Clamp(localMin.x, area.xMin, area.xMax);
        float localMaxX = Mathf.Clamp(localMax.x, area.xMin, area.xMax);
        float localMinY = Mathf.Clamp(localMin.y, area.yMin, area.yMax);
        float localMaxY = Mathf.Clamp(localMax.y, area.yMin, area.yMax);

        float width = localMaxX - localMinX;
        float height = localMaxY - localMinY;

        if (width <= 0f || height <= 0f)
        {
            cameraViewRoot.gameObject.SetActive(false);
            return;
        }

        float thickness = Mathf.Min(
            cameraViewBorderThickness,
            Mathf.Min(width, height));

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

        for (int i = 0; i < cameraViewBorderImages.Length; i++)
        {
            if (cameraViewBorderImages[i] != null)
                cameraViewBorderImages[i].color = cameraViewColor;
        }
    }

    static void SetBorderRect(
        RectTransform border,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        if (border == null)
            return;

        border.anchoredPosition = anchoredPosition;
        border.sizeDelta = sizeDelta;
    }
}
