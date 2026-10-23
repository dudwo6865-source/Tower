using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 확정된 결과를 연출할 뿐 크레딧이나 클리어 기록은 변경하지 않는다.
[RequireComponent(typeof(RectTransform))]
public class ResultScoreUI : MonoBehaviour
{
    [Header("화면 연결")]
    [Label("종합점수 텍스트")]
    public TextMeshProUGUI totalText;
    [Label("점수 슬라이더")]
    public Slider scoreSlider;
    [Label("게이지 채움 이미지")]
    public Image fillImage;
    [Label("별 이미지 목록")]
    [Tooltip("1성, 2성, 3성 순서입니다. 비어 있으면 자동 생성합니다.")]
    public Image[] starImages = new Image[3];
    [Label("별 스프라이트")]
    [Tooltip("비워두면 오각별 모양을 생성합니다.")]
    public Sprite starSprite;
    [Label("기준 점수 텍스트 목록")]
    public TextMeshProUGUI[] thresholdTexts = new TextMeshProUGUI[3];

    [Header("점수 연출")]
    [Label("점수 상승 시간(초)"), Min(0)]
    public float animationDuration = 2.2f;
    [Label("별 등장 시간(초)"), Min(0)]
    public float starPopDuration = 0.25f;
    [Label("별 등장 확대 배율"), Min(1)]
    public float starPopScale = 1.4f;
    [Label("종합점수 글자 크기"), Min(1)]
    public float scoreFontSize = 18f;
    [Label("기준점 글자 크기"), Min(1)]
    public float thresholdFontSize = 10f;
    [Label("별 크기"), Min(1)]
    public float starSize = 18f;

    [Header("단계별 색")]
    [Label("미달 게이지 색")]
    public Color zeroStarColor = new Color(0.35f, 0.45f, 0.55f);
    [Label("1성 동색")]
    public Color oneStarColor = new Color(0.82f, 0.49f, 0.25f);
    [Label("2성 은색")]
    public Color twoStarColor = new Color(0.73f, 0.85f, 0.95f);
    [Label("3성 금색")]
    public Color threeStarColor = new Color(1f, 0.8f, 0.23f);
    [Label("게이지 배경색")]
    public Color trackColor = new Color(0.07f, 0.1f, 0.15f, 0.9f);
    [Label("미획득 별 색")]
    public Color emptyStarColor = new Color(0.2f, 0.25f, 0.3f);

    StageClearRewards rules;
    double total;
    float elapsed;
    bool running;
    bool awardStars;
    int shownStars;
    TMP_Text optionalStarsText;
    readonly float[] revealedAt = new float[3];
    readonly Vector3[] restScales = new Vector3[3];
    Sprite generatedStar;

    public void Show(double score, StageClearRewards settings, bool victory, TMP_FontAsset font, TMP_Text starsText)
    {
        EnsureVisuals(font);
        rules = settings;
        total = score;
        awardStars = victory && settings != null;
        optionalStarsText = starsText;
        elapsed = 0;
        shownStars = 0;
        for (int i = 0; i < 3; i++)
        {
            revealedAt[i] = -1;
            if (starImages.Length > i && starImages[i] != null)
            {
                if (restScales[i] == Vector3.zero) restScales[i] = starImages[i].rectTransform.localScale;
                starImages[i].rectTransform.localScale = restScales[i];
                starImages[i].enabled = false;
            }
            if (thresholdTexts.Length > i && thresholdTexts[i] != null)
                thresholdTexts[i].text = settings == null ? "-" : Threshold(i).ToString("N0");
        }
        running = true;
        Refresh(animationDuration <= 0 ? total : 0);
    }

    void Update()
    {
        if (!running) return;
        elapsed += Time.unscaledDeltaTime;
        Refresh(ScoreRevealProgress.Sample(total, elapsed, animationDuration));
        if (elapsed >= Mathf.Max(0, animationDuration) + Mathf.Max(0, starPopDuration)) running = false;
    }

    void OnEnable()
    {
        if (rules != null && elapsed < Mathf.Max(0, animationDuration) + Mathf.Max(0, starPopDuration)) running = true;
    }
    void OnDisable() { running = false; }
    void OnDestroy() { if (generatedStar != null) Destroy(generatedStar); }

    double Threshold(int index) => index == 0 ? rules.OneStarScore : index == 1 ? rules.TwoStarScore : rules.TargetScore;
    Color TierColor(int stars) => stars >= 3 ? threeStarColor : stars == 2 ? twoStarColor : stars == 1 ? oneStarColor : zeroStarColor;

    void Refresh(double score)
    {
        totalText.text = rules == null ? "종합점수 -" : $"종합점수 {score:N0} / {rules.TargetScore:N0}";
        scoreSlider.SetValueWithoutNotify(rules == null ? 0 : Mathf.Clamp01((float)(score / rules.TargetScore)));
        int stars = awardStars ? rules.GetStarsForScore(score) : 0;
        fillImage.color = TierColor(stars);
        for (int i = 0; i < 3; i++)
        {
            if (starImages.Length <= i || starImages[i] == null) continue;
            if (stars > i && shownStars <= i) revealedAt[i] = elapsed;
            starImages[i].enabled = stars > i;
            starImages[i].color = TierColor(i + 1);
            float t = starPopDuration <= 0 ? 1 : Mathf.Clamp01((elapsed - revealedAt[i]) / starPopDuration);
            starImages[i].rectTransform.localScale = restScales[i] * Mathf.Lerp(starPopScale, 1, t);
        }
        shownStars = stars;
        if (optionalStarsText != null)
            optionalStarsText.text = awardStars ? new string('★', stars) + new string('☆', 3 - stars) : "";
    }

    [ContextMenu("점수 UI 생성")]
    public void BuildVisuals() => EnsureVisuals(TMP_Settings.defaultFontAsset);

    void EnsureVisuals(TMP_FontAsset font)
    {
        if (totalText == null)
            totalText = MakeText("종합점수", new Vector2(0, 0.7f), Vector2.one, scoreFontSize, font);
        if (scoreSlider == null)
        {
            var track = MakeImage("점수 게이지", new Vector2(0.04f, 0.25f), new Vector2(0.96f, 0.43f), trackColor);
            scoreSlider = track.gameObject.AddComponent<Slider>();
            var fill = new GameObject("채움", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            fillImage = fill.GetComponent<Image>();
            fillImage.raycastTarget = false;
            var rect = fillImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            scoreSlider.fillRect = rect;
        }
        scoreSlider.minValue = 0;
        scoreSlider.maxValue = 1;
        scoreSlider.wholeNumbers = false;
        scoreSlider.interactable = false;
        scoreSlider.transition = Selectable.Transition.None;
        scoreSlider.navigation = new Navigation { mode = Navigation.Mode.None };
        if (fillImage == null && scoreSlider.fillRect != null) fillImage = scoreSlider.fillRect.GetComponent<Image>();
        if (fillImage == null)
        {
            var fill = new GameObject("채움", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(scoreSlider.transform, false);
            fillImage = fill.GetComponent<Image>();
            fillImage.raycastTarget = false;
            fillImage.rectTransform.anchorMin = Vector2.zero;
            fillImage.rectTransform.anchorMax = Vector2.one;
            fillImage.rectTransform.offsetMin = fillImage.rectTransform.offsetMax = Vector2.zero;
            scoreSlider.fillRect = fillImage.rectTransform;
        }
        if (starImages == null || starImages.Length < 3) System.Array.Resize(ref starImages, 3);
        if (thresholdTexts == null || thresholdTexts.Length < 3) System.Array.Resize(ref thresholdTexts, 3);
        Sprite sprite = starSprite != null ? starSprite : GetGeneratedStar();
        for (int i = 0; i < 3; i++)
        {
            float fraction = i == 0 ? 0.5f : i == 1 ? 0.75f : 1f;
            float x = 0.04f + 0.92f * fraction;
            if (starImages[i] == null)
            {
                var ghost = MakeImage($"{i + 1}성 자리", new Vector2(x, 0.57f), new Vector2(x, 0.57f), emptyStarColor);
                ghost.rectTransform.sizeDelta = Vector2.one * starSize;
                ghost.sprite = sprite;
                ghost.useSpriteMesh = true;
                var star = MakeImage($"{i + 1}성 획득", new Vector2(x, 0.57f), new Vector2(x, 0.57f), TierColor(i + 1));
                star.rectTransform.sizeDelta = Vector2.one * starSize;
                starImages[i] = star;
            }
            starImages[i].sprite = sprite;
            starImages[i].useSpriteMesh = true;
            if (thresholdTexts[i] == null)
            {
                thresholdTexts[i] = MakeText($"{i + 1}성 기준", new Vector2(x, 0.1f), new Vector2(x, 0.1f), thresholdFontSize, font);
                thresholdTexts[i].rectTransform.sizeDelta = new Vector2(55, 14);
                if (i == 2) thresholdTexts[i].rectTransform.pivot = new Vector2(1, 0.5f);
                thresholdTexts[i].color = TierColor(i + 1);
            }
        }
    }

    RectTransform MakeRect(string objectName, Vector2 min, Vector2 max)
    {
        var root = new GameObject(objectName, typeof(RectTransform));
        root.transform.SetParent(transform, false);
        root.layer = gameObject.layer;
        var rect = (RectTransform)root.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    Image MakeImage(string objectName, Vector2 min, Vector2 max, Color color)
    {
        var image = MakeRect(objectName, min, max).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    TextMeshProUGUI MakeText(string objectName, Vector2 min, Vector2 max, float size, TMP_FontAsset font)
    {
        var text = MakeRect(objectName, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.65f;
        text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    Sprite GetGeneratedStar()
    {
        if (generatedStar != null) return generatedStar;
        generatedStar = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        generatedStar.name = "결과 오각별";
        var vertices = new Vector2[11];
        var triangles = new ushort[30];
        for (int i = 0; i < 10; i++)
        {
            float angle = (90 - i * 36) * Mathf.Deg2Rad;
            float radius = i % 2 == 0 ? 0.5f : 0.22f;
            vertices[i + 1] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (ushort)(i + 1);
            triangles[i * 3 + 2] = (ushort)((i + 1) % 10 + 1);
        }
        generatedStar.OverrideGeometry(vertices, triangles);
        return generatedStar;
    }
}
