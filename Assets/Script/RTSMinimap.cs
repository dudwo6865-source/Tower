using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class RTSMinimap : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [Tooltip("미니맵 클릭 시 카메라를 이동시킬 RTS 카메라 컨트롤러입니다. 비워두면 씬에서 자동으로 찾습니다.")]
    public RTSCameraPivotController cameraController;

    [Header("Auto Setup")]
    [Tooltip("켜면 Canvas·미니맵 UI가 없을 때 우하단에 기본 미니맵 패널을 자동 생성합니다.")]
    public bool createMinimapIfMissing = true;

    private RectTransform minimapRect;
    private Terrain terrain;
    private float terrainWidth;
    private float terrainLength;

    void Start()
    {
        minimapRect = GetComponent<RectTransform>();
        terrain = Terrain.activeTerrain;

        if (terrain == null)
        {
            Debug.LogError("RTSMinimap: Terrain not found");
            return;
        }

        terrainWidth = terrain.terrainData.size.x;
        terrainLength = terrain.terrainData.size.z;

        if (cameraController == null)
            cameraController =
                FindObjectOfType<RTSCameraPivotController>();

        if (createMinimapIfMissing)
            EnsureMinimapUI();
    }

    void EnsureMinimapUI()
    {
        if (GetComponent<Image>() == null)
            gameObject.AddComponent<Image>();

        Image image = GetComponent<Image>();
        image.color = new Color(0.15f, 0.35f, 0.15f, 0.85f);
        image.raycastTarget = true;

        if (transform.parent != null)
            return;

        Canvas canvas = FindOverlayCanvas();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("RTS Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        transform.SetParent(canvas.transform, false);

        minimapRect.anchorMin = new Vector2(1f, 0f);
        minimapRect.anchorMax = new Vector2(1f, 0f);
        minimapRect.pivot = new Vector2(1f, 0f);
        minimapRect.anchoredPosition = new Vector2(-16f, 16f);
        minimapRect.sizeDelta = new Vector2(220f, 220f);
    }

    static Canvas FindOverlayCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();

        foreach (Canvas candidate in canvases)
        {
            if (candidate.renderMode == RenderMode.ScreenSpaceOverlay)
                return candidate;
        }

        return null;
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

        Rect rect = minimapRect.rect;

        float normalizedX =
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);

        float normalizedZ =
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        Vector3 worldPosition = new Vector3(
            normalizedX * terrainWidth,
            0f,
            normalizedZ * terrainLength);

        cameraController.FocusOnPosition(worldPosition);
    }
}
