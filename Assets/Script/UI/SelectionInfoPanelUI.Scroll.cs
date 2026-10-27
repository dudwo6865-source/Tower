using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class SelectionInfoPanelUI
{
    [Header("다중 선택 - 스크롤 연결")]
    [Label("초상화 스크롤 뷰")]
    [Tooltip("직접 만든 세로 Scroll Rect를 연결합니다. Content에는 초상화 그리드 부모와 같은 오브젝트를 지정합니다. Content는 상단 기준 피벗으로 설정하고 Grid Layout Group(고정 열 수)과 Content Size Fitter(세로 Preferred Size)를 붙입니다. 비워두면 UI를 자동 생성하지 않습니다.")]
    public ScrollRect portraitScroll;
    [Label("세로 스크롤바")]
    [Tooltip("직접 만든 Scrollbar를 연결합니다. 비워두면 Scroll Rect에 설정된 스크롤바를 그대로 사용합니다. 표시 방식·속도·방향은 해당 컴포넌트에서 설정합니다.")]
    public Scrollbar portraitScrollbar;
    [Label("다중 선택 제목 텍스트")]
    [Tooltip("전체 선택 수를 표시할 TMP 텍스트입니다. 스크롤 내용 밖에 배치하면 항상 보입니다. 글꼴·크기·색상·위치는 연결한 오브젝트의 설정을 그대로 사용합니다.")]
    public TMP_Text portraitCountText;
    [Label("다중 선택 제목 형식")]
    [Tooltip("{0}에 전체 선택 수를 표시합니다. 예: 전체 선택 {0}")]
    public string portraitCountFormat = "전체 선택 {0}";

    bool warnedPortraitScroll;

    bool HasPortraitScroll => portraitScroll != null && portraitScroll.content != null &&
        portraitScroll.content == portraitGridParent;

    void ConfigurePortraitScroll()
    {
        if (portraitScroll == null) return;
        if (portraitScrollbar != null)
            portraitScroll.verticalScrollbar = portraitScrollbar;

        if (!HasPortraitScroll && !warnedPortraitScroll)
        {
            warnedPortraitScroll = true;
            Debug.LogWarning("선택 정보 패널: 스크롤 뷰의 Content와 초상화 그리드 부모에 같은 오브젝트를 연결하세요.", this);
        }
    }

    void RefreshPortraitScroll(int count)
    {
        if (portraitCountText != null)
            portraitCountText.text = string.Format(portraitCountFormat, count);
        if (!HasPortraitScroll) return;

        RectTransform content = portraitScroll.content;
        RectTransform viewport = portraitScroll.viewport != null
            ? portraitScroll.viewport : (RectTransform)portraitScroll.transform;
        Vector2 previousPosition = content.anchoredPosition;

        // 셀 크기·열 수는 그리드 설정을, 콘텐츠 높이는 사용자가 연결한 레이아웃 설정을 따른다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float maxOffset = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        previousPosition.y = Mathf.Clamp(previousPosition.y, 0f, maxOffset);
        portraitScroll.StopMovement();
        // 선택 수가 줄어도 맨 위로 돌아가지 않고 유효한 범위로만 보정한다.
        content.anchoredPosition = previousPosition;
    }

    void ResetPortraitScroll()
    {
        if (portraitCountText != null) portraitCountText.text = string.Empty;
        if (!HasPortraitScroll) return;
        portraitScroll.StopMovement();
        Vector2 position = portraitScroll.content.anchoredPosition;
        position.y = 0f;
        portraitScroll.content.anchoredPosition = position;
    }
}
