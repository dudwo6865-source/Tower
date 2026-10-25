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

    [Header("미해금 상태")]
    [Label("미해금 배경")] public Sprite lockedBackground;
    [Label("미해금 번호 배지")] public Sprite lockedBadge;
    [Label("잠금 표시 오브젝트")]
    [Tooltip("미해금 상태에서만 표시할 자물쇠 등의 자식 오브젝트입니다.")]
    public GameObject lockedIndicator;
    [Label("잠금 안내 텍스트")] public TMP_Text lockedText;
    [Label("잠금 안내 문구")] public string lockedMessage = "이전 미션 클리어 필요";
    [Label("항목 캔버스 그룹")]
    [Tooltip("비워두면 항목 루트의 CanvasGroup을 사용하거나 자동 추가합니다.")]
    public CanvasGroup stateGroup;
    [Label("미해금 투명도"), Range(0f, 1f)] public float lockedAlpha = 0.45f;

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
    bool unlocked = true;
    bool capturedAlpha;
    float normalAlpha;

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

    public void Bind(string number, string missionName, int stars, Action selected, bool isUnlocked = true)
    {
        unlocked = isUnlocked;
        onSelected = selected;
        Button button = selectButton != null ? selectButton : GetComponent<Button>();
        if (button != null) button.interactable = unlocked;
        if (stateGroup == null) stateGroup = GetComponent<CanvasGroup>();
        if (stateGroup == null) stateGroup = gameObject.AddComponent<CanvasGroup>();
        if (!capturedAlpha) { normalAlpha = stateGroup.alpha; capturedAlpha = true; }
        stateGroup.alpha = unlocked ? normalAlpha : normalAlpha * lockedAlpha;
        stateGroup.interactable = unlocked;
        SetIndicator(lockedIndicator, !unlocked);
        if (lockedText != null) lockedText.text = unlocked ? "" : lockedMessage;
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
        selected &= unlocked;
        if (backgroundImage != null)
            backgroundImage.sprite = !unlocked && lockedBackground != null ? lockedBackground
                : selected && selectedBackground != null ? selectedBackground : normalBackground;
        if (badgeImage != null)
            badgeImage.sprite = !unlocked && lockedBadge != null ? lockedBadge
                : selected && selectedBadge != null ? selectedBadge : normalBadge;
        SetIndicator(selectionIndicator, selected);
    }

    void SetIndicator(GameObject indicator, bool visible)
    {
        // 항목 자체를 끄는 잘못된 연결은 무시한다.
        if (indicator != null && indicator != gameObject && !transform.IsChildOf(indicator.transform))
            indicator.SetActive(visible);
    }

    void Select() { if (unlocked) onSelected?.Invoke(); }
}
