using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 미션 항목 하나의 표시와 선택 입력을 담당한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class MissionListItemUI : MonoBehaviour
{
    [Header("항목 연결")]
    [Label("선택 버튼")] public Button selectButton;
    [Label("미션 번호 텍스트")] public TMP_Text numberText;
    [Label("미션 이름 텍스트")] public TMP_Text nameText;
    [Label("항목 배경 이미지")] public Image backgroundImage;
    [Label("번호 배지 이미지")] public Image badgeImage;
    [Label("선택 표시 오브젝트")] public GameObject selectionIndicator;

    [Header("선택 상태 이미지")]
    [Label("일반 배경")] public Sprite normalBackground;
    [Label("선택 배경")] public Sprite selectedBackground;
    [Label("일반 번호 배지")] public Sprite normalBadge;
    [Label("선택 번호 배지")] public Sprite selectedBadge;

    [Header("클리어 별")]
    [Label("별 이미지 목록")]
    [Tooltip("왼쪽부터 별 이미지 3개를 순서대로 연결합니다.")]
    public Image[] starImages = new Image[3];
    [Label("획득한 별")] public Sprite earnedStar;
    [Label("미획득 별")] public Sprite emptyStar;
    [Label("획득한 별 색상")] public Color earnedStarColor = Color.white;
    [Label("미획득 별 색상")] public Color emptyStarColor = Color.gray;

    Action onSelected;
    Button boundButton;

    void Reset() { selectButton = GetComponent<Button>(); }

    void OnEnable()
    {
        boundButton = selectButton != null ? selectButton : GetComponent<Button>();
        if (boundButton != null) boundButton.onClick.AddListener(Select);
    }

    void OnDisable()
    {
        if (boundButton != null) boundButton.onClick.RemoveListener(Select);
        boundButton = null;
    }

    public void Bind(string number, string missionName, int stars, Action selected)
    {
        onSelected = selected;
        if (numberText != null) numberText.text = number;
        if (nameText != null) nameText.text = missionName;
        // 일반 이미지를 생략하면 제작한 항목에 들어 있던 이미지를 사용한다.
        if (normalBackground == null && backgroundImage != null) normalBackground = backgroundImage.sprite;
        if (normalBadge == null && badgeImage != null) normalBadge = badgeImage.sprite;
        stars = Mathf.Clamp(stars, 0, 3);
        if (starImages != null)
            for (int i = 0; i < starImages.Length; i++)
            {
                Image target = starImages[i];
                if (target == null) continue;
                bool earned = i < stars;
                Sprite sprite = earned ? earnedStar : emptyStar;
                if (sprite != null) target.sprite = sprite;
                target.color = earned ? earnedStarColor : emptyStarColor;
            }
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (backgroundImage != null)
            backgroundImage.sprite = selected && selectedBackground != null ? selectedBackground : normalBackground;
        if (badgeImage != null)
            badgeImage.sprite = selected && selectedBadge != null ? selectedBadge : normalBadge;
        // 항목 자체를 끄는 잘못된 연결은 무시한다.
        if (selectionIndicator != null && selectionIndicator != gameObject
            && !transform.IsChildOf(selectionIndicator.transform))
            selectionIndicator.SetActive(selected);
    }

    void Select() { onSelected?.Invoke(); }
}
