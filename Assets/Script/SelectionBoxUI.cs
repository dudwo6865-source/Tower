using UnityEngine;
using UnityEngine.UI;

public class SelectionBoxUI : MonoBehaviour
{
    private static readonly Color FillColor =
        new Color(0.2f, 0.85f, 0.3f, 0.18f);

    private static readonly Color BorderColor =
        new Color(0.2f, 0.95f, 0.35f, 0.95f);

    private RectTransform boxRect;
    private Image fillImage;
    private Image borderImage;
    private Canvas canvas;

    void Awake()
    {
        EnsureUI();
        Hide();
    }

    void EnsureUI()
    {
        if (canvas == null)
            canvas = FindOverlayCanvas();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("SelectionBoxCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (boxRect != null)
            return;

        GameObject boxObject = new GameObject("SelectionBox");
        boxObject.transform.SetParent(canvas.transform, false);

        boxRect = boxObject.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(boxRect, false);

        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        fillImage = fillObject.AddComponent<Image>();
        fillImage.color = FillColor;
        fillImage.raycastTarget = false;

        borderImage = boxObject.AddComponent<Image>();
        borderImage.color = BorderColor;
        borderImage.raycastTarget = false;

        boxObject.transform.SetAsLastSibling();
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

    public void UpdateBox(Vector2 screenStart, Vector2 screenEnd)
    {
        EnsureUI();

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenStart,
            null,
            out Vector2 localStart);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenEnd,
            null,
            out Vector2 localEnd);

        Vector2 center = (localStart + localEnd) * 0.5f;
        Vector2 size = new Vector2(
            Mathf.Abs(localEnd.x - localStart.x),
            Mathf.Abs(localEnd.y - localStart.y));

        boxRect.anchoredPosition = center;
        boxRect.sizeDelta = size;
        boxRect.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (boxRect != null)
            boxRect.gameObject.SetActive(false);
    }
}
