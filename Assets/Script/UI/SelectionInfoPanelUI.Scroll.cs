using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class SelectionInfoPanelUI
{
    [Header("다중 선택 - 스크롤")]
    [Label("고정 셀과 세로 스크롤 사용")]
    [Tooltip("기본 셀 크기를 유지하고 모든 선택 대상을 스크롤로 표시합니다. 셀 자동 축소와 최대 칸 수 제한보다 우선합니다. 실행 전에 설정합니다.")]
    public bool scrollPortraits = true;
    [Label("최대 표시 줄 수")]
    [Tooltip("기존 영역 높이 안에서 표시할 최대 줄 수입니다. 공간이 부족하면 더 적은 줄을 표시합니다.")]
    [Min(1)] public int portraitVisibleRows = 2;
    [Label("선택 수 제목 높이")]
    [Min(1f)] public float portraitHeaderHeight = 24f;
    [Label("선택 수 글자 크기")]
    [Min(1f)] public float portraitCountFontSize = 18f;
    [Label("스크롤바 너비")]
    [Min(1f)] public float portraitScrollbarWidth = 14f;
    [Label("스크롤바 간격")]
    [Min(0f)] public float portraitScrollbarGap = 4f;
    [Label("스크롤 속도")]
    [Min(1f)] public float portraitScrollSensitivity = 40f;

    ScrollRect portraitScroll;
    RectTransform portraitViewport;
    TextMeshProUGUI portraitCountText;
    int visiblePortraitCount;
    float lastPortraitWidth = -1f;
    float lastPortraitHeight = -1f;

    void EnsurePortraitScroll()
    {
        if (!scrollPortraits || portraitScroll != null || portraitGridParent == null) return;
        RectTransform root = portraitGridParent as RectTransform;
        if (root == null) return;
        if (portraitGrid == null) portraitGrid = root.GetComponent<GridLayoutGroup>();
        if (portraitGrid == null) return;

        // 기존 영역은 고정된 틀로 남기고 초상화만 내부 콘텐츠로 옮긴다.
        GridLayoutGroup originalGrid = portraitGrid;
        originalGrid.enabled = false;
        ContentSizeFitter rootFitter = root.GetComponent<ContentSizeFitter>();
        if (rootFitter != null) rootFitter.enabled = false;
        portraitScroll = root.GetComponent<ScrollRect>();
        if (portraitScroll == null) portraitScroll = root.gameObject.AddComponent<ScrollRect>();
        portraitScroll.horizontal = false;
        portraitScroll.vertical = true;
        portraitScroll.movementType = ScrollRect.MovementType.Clamped;
        portraitScroll.inertia = false;
        portraitScroll.scrollSensitivity = portraitScrollSensitivity;

        RectTransform title = CreatePortraitRect("전체 선택 수", root);
        title.anchorMin = new Vector2(0f, 1f);
        title.anchorMax = Vector2.one;
        title.pivot = new Vector2(0.5f, 1f);
        title.sizeDelta = new Vector2(0f, portraitHeaderHeight);
        portraitCountText = title.gameObject.AddComponent<TextMeshProUGUI>();
        if (nameText != null) portraitCountText.font = nameText.font;
        portraitCountText.fontSize = portraitCountFontSize;
        portraitCountText.alignment = TextAlignmentOptions.MidlineLeft;
        portraitCountText.raycastTarget = false;

        portraitViewport = CreatePortraitRect("초상화 표시 영역", root);
        portraitViewport.gameObject.AddComponent<RectMask2D>();
        Image hitArea = portraitViewport.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        RectTransform content = CreatePortraitRect("초상화 스크롤 내용", portraitViewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        portraitGrid = content.gameObject.AddComponent<GridLayoutGroup>();
        portraitGrid.padding = originalGrid.padding;
        portraitGrid.spacing = originalGrid.spacing;
        portraitGrid.childAlignment = TextAnchor.UpperLeft;
        portraitGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        portraitGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        portraitGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        foreach (SelectionPortraitCell cell in spawnedPortraitCells)
            if (cell != null) cell.transform.SetParent(content, false);
        portraitGridParent = content;
        portraitScroll.content = content;
        portraitScroll.viewport = portraitViewport;

        RectTransform bar = CreatePortraitRect("초상화 세로 스크롤바", root);
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = Vector2.one;
        bar.pivot = Vector2.one;
        bar.offsetMin = new Vector2(-portraitScrollbarWidth, 0f);
        bar.offsetMax = new Vector2(0f, -portraitHeaderHeight);
        Image track = bar.gameObject.AddComponent<Image>();
        track.color = new Color(0f, 0f, 0f, 0.45f);
        Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        RectTransform handle = CreatePortraitRect("스크롤 손잡이", bar);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.75f, 0.8f, 0.85f, 1f);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        portraitScroll.verticalScrollbar = scrollbar;
        portraitScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    void LateUpdate()
    {
        if (!multiSelectionActive || portraitScroll == null || !portraitScroll.gameObject.activeInHierarchy) return;
        RectTransform root = (RectTransform)portraitScroll.transform;
        if (!Mathf.Approximately(root.rect.width, lastPortraitWidth) ||
            !Mathf.Approximately(root.rect.height, lastPortraitHeight))
            RefreshPortraitScroll(visiblePortraitCount);
    }

    void RefreshPortraitScroll(int count)
    {
        if (portraitScroll == null) return;
        visiblePortraitCount = count;
        portraitCountText.text = $"전체 선택 {count}";
        RectTransform root = (RectTransform)portraitScroll.transform;
        lastPortraitWidth = root.rect.width;
        lastPortraitHeight = root.rect.height;
        Vector2 cell = portraitGrid.cellSize;
        Vector2 spacing = portraitGrid.spacing;
        RectOffset padding = portraitGrid.padding;
        float height = Mathf.Min(Mathf.Max(1f, root.rect.height - portraitHeaderHeight),
            Mathf.Max(1, portraitVisibleRows) * (cell.y + spacing.y) - spacing.y + padding.vertical);
        // 스크롤바 자리를 항상 확보해 나타날 때 열 수가 바뀌지 않게 한다.
        portraitViewport.anchorMin = new Vector2(0f, 1f);
        portraitViewport.anchorMax = Vector2.one;
        portraitViewport.pivot = new Vector2(0.5f, 1f);
        portraitViewport.offsetMin = new Vector2(0f, -portraitHeaderHeight - height);
        portraitViewport.offsetMax = new Vector2(-portraitScrollbarWidth - portraitScrollbarGap, -portraitHeaderHeight);
        RectTransform bar = (RectTransform)portraitScroll.verticalScrollbar.transform;
        bar.offsetMin = new Vector2(-portraitScrollbarWidth, root.rect.height - portraitHeaderHeight - height);
        int columns = Mathf.Max(1, Mathf.FloorToInt(
            (portraitViewport.rect.width - padding.horizontal + spacing.x) / Mathf.Max(1f, cell.x + spacing.x)));
        portraitGrid.constraintCount = columns;
        int rows = Mathf.CeilToInt((float)count / columns);
        float contentHeight = padding.vertical + rows * cell.y + Mathf.Max(0, rows - 1) * spacing.y;
        RectTransform content = portraitScroll.content;
        float previousOffset = content.anchoredPosition.y;
        content.sizeDelta = new Vector2(0f, Mathf.Max(height, contentHeight));
        // 선택 수가 줄어도 맨 위로 돌아가지 않고 유효한 범위로만 보정한다.
        content.anchoredPosition = new Vector2(0f, Mathf.Clamp(previousOffset, 0f, Mathf.Max(0f, contentHeight - height)));
        portraitScroll.StopMovement();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    void ResetPortraitScroll()
    {
        if (portraitScroll == null) return;
        portraitScroll.StopMovement();
        portraitScroll.content.anchoredPosition = Vector2.zero;
    }

    static RectTransform CreatePortraitRect(string objectName, Transform parent)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }
}
