using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 알림 표현은 여기서 관리하고 실제 추첨과 능력치 계산은 EnemyMutationManager가 담당한다.
[AddComponentMenu("게임/돌연변이 알림 매니저")]
[DisallowMultipleComponent]
public class EnemyMutationHUD : MonoBehaviour
{
    static EnemyMutationHUD instance;

    [Header("UI 연결")]
    [Label("누적 효과 텍스트")]
    public TMP_Text summaryText;
    [Label("결과 알림 텍스트")]
    public TMP_Text notificationText;
    [Label("알림 패널")]
    [Tooltip("알림이 표시되는 동안 켜고 끝나면 끕니다. 알림 텍스트를 이 패널 안에 넣어 둡니다. 패널에 Canvas Group이 있으면 패널 전체를 페이드합니다. 이 매니저가 붙은 오브젝트나 그 부모는 연결하지 않습니다.")]
    public GameObject notificationPanel;
    [Label("누적 효과가 있으면 패널 유지")]
    [Tooltip("켜면 돌연변이가 한 번이라도 생긴 뒤에는 알림이 끝나도 패널을 열어 둡니다. 누적 효과 텍스트를 패널 안에 넣었을 때 사용합니다.")]
    public bool keepPanelOpenWithMutations;
    [Label("자동 UI 생성")]
    [Tooltip("텍스트와 패널을 모두 비워두면 기본 UI를 생성합니다. 직접 만든 UI를 연결하면 자동 생성하지 않습니다.")]
    public bool createDefaultUI = true;

    [Header("알림 문구")]
    [Label("알림 형식")]
    [Tooltip("{효과명}, {증가율}을 실제 값으로 바꿉니다. 증가율에는 %가 포함됩니다. 방어력 돌연변이는 %가 아니라 더한 수치(예: 1)로 바뀝니다.")]
    [TextArea] public string notificationFormat = "돌연변이 발생: {효과명} +{증가율}";
    [Label("스폰 수 효과 이름")]
    public string spawnName = "밤 공격대 수";
    [Label("공격력 효과 이름")]
    public string damageName = "적 공격력";
    [Label("체력 효과 이름")]
    public string healthName = "적 체력";
    [Label("이동 속도 효과 이름")]
    public string speedName = "적 이동 속도";
    [Label("방어력 효과 이름")]
    public string armorName = "적 방어력";
    [Label("누적 효과 형식")]
    [Tooltip("{횟수}, {스폰}, {공격력}, {체력}, {이동속도}, {방어력}을 사용할 수 있습니다. 증가율에는 %가 포함되고, {방어력}은 더한 수치입니다.")]
    [TextArea(3, 6)] public string summaryFormat = "돌연변이 {횟수}회\n밤 공격대 +{스폰} · 공격력 +{공격력}\n체력 +{체력} · 이동 속도 +{이동속도} · 방어력 +{방어력}";

    [Header("표시")]
    [Label("알림 표시 시간(초)")]
    [Tooltip("등장·퇴장 효과를 포함한 한 알림의 전체 시간입니다. 일시정지 중에도 흐릅니다.")]
    [Min(0.1f)] public float notificationDuration = 5f;
    [Label("유물 선택 후 대기 시간(초)")]
    [Tooltip("유물 선택·교체와 대기 중인 보상이 모두 끝난 뒤 알림을 표시하기까지의 시간입니다. 일시정지와 무관하게 흐릅니다.")]
    [Min(0f)] public float delayAfterRelicSelection = 0.35f;
    [Label("텍스트 스타일 덮어쓰기")]
    [Tooltip("끄면 직접 연결한 텍스트의 폰트·크기를 유지합니다. 자동 생성 UI에는 아래 설정을 사용합니다. 글자 색상은 항상 텍스트에 설정한 색을 씁니다.")]
    public bool overrideTextStyle;
    [Label("글꼴")]
    [Tooltip("자동 UI에서 비워두면 프로젝트의 한글 글꼴을 사용합니다.")]
    public TMP_FontAsset font;
    [Label("글자 크기")]
    [Min(1f)] public float fontSize = 20f;

    [Header("알림 연출")]
    [Label("등장 시간(초)")]
    [Min(0f)] public float fadeInDuration = 0.15f;
    [Label("퇴장 시간(초)")]
    [Min(0f)] public float fadeOutDuration = 0.4f;
    [Label("크기 강조 대상")]
    [Tooltip("비워두면 알림 패널을, 패널도 없으면 알림 텍스트를 강조합니다. 다른 UI까지 움직이지 않도록 전용 오브젝트를 연결합니다.")]
    public RectTransform emphasisTarget;
    [Label("등장 크기 배율")]
    [Min(0.01f)] public float startScale = 1.15f;
    [Label("크기 복원 시간(초)")]
    [Min(0f)] public float emphasisDuration = 0.25f;
    [Label("알림 오디오 소스")]
    [Tooltip("비워두면 필요한 경우 2D 오디오 소스를 생성합니다.")]
    public AudioSource audioSource;
    [Label("알림 효과음")]
    public AudioClip notificationSound;
    [Label("알림 음량")]
    [Tooltip("1이 원래 크기입니다. 1보다 크면 효과음을 증폭하며, 너무 크게 올리면 소리가 찢어질 수 있습니다.")]
    [Range(0f, 3f)] public float volume = 1f;
    [Label("알림 시작 효과")]
    [Tooltip("알림이 실제로 표시될 때 실행합니다. 파티클 Play 등 원하는 효과를 연결합니다. 미리보기에서도 실행됩니다.")]
    public UnityEvent onNotificationStarted = new UnityEvent();
    [Label("알림 종료 효과")]
    [Tooltip("표시 종료·게임 종료·매니저 비활성화 시 실행합니다. 시작한 효과의 Stop 등을 연결합니다.")]
    public UnityEvent onNotificationFinished = new UnityEvent();

    [Header("자동 UI 배치")]
    [Label("자동 표시 위치")]
    public Vector2 panelPosition = new Vector2(-24f, -160f);
    [Label("자동 표시 크기")]
    public Vector2 panelSize = new Vector2(430f, 150f);
    [Label("배경 색상")]
    public Color backgroundColor = new Color(0.08f, 0.07f, 0.1f, 0.88f);
    [Label("기준 해상도")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Label("표시 순서")]
    public int sortingOrder = 50;

    [Header("플레이 중 미리보기")]
    [Label("미리보기 효과")]
    public EnemyMutationType previewType = EnemyMutationType.Health;
    [Label("미리보기 증가율")]
    [Min(0f)] public float previewIncrease = 0.15f;

    struct Notice
    {
        public EnemyMutationType type;
        public float increase;
    }

    EnemyMutationManager manager;
    readonly Queue<Notice> notifications = new Queue<Notice>();
    float notificationStartedAt;
    bool showingNotification;
    bool suspendedNotification;
    bool waitingForRelicSelection;
    float notificationResumeAt;
    Notice currentNotice;
    Color originalNotificationColor;
    CanvasGroup panelGroup;
    float originalPanelAlpha = 1f;
    Vector3 originalScale;
    RectTransform animatedTarget;
    GameObject generatedPanel;
    RectTransform generatedRect;
    Image generatedBackground;
    bool appearanceDirty;

    public static void EnsureInstance()
    {
        if (instance != null) return;
        instance = FindObjectOfType<EnemyMutationHUD>();
        if (instance == null)
            instance = new GameObject("돌연변이 알림 매니저").AddComponent<EnemyMutationHUD>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
    }

    void OnEnable() => Bind();
    void OnValidate() => appearanceDirty = true;

    void OnDisable()
    {
        StopPresentation();
        if (manager == null) return;
        manager.OnChanged -= HandleStateChanged;
        manager.OnMutationRolled -= EnqueueNotification;
        manager = null;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Bind()
    {
        if (manager != null || EnemyMutationManager.Instance == null) return;
        manager = EnemyMutationManager.Instance;
        manager.OnChanged += HandleStateChanged;
        manager.OnMutationRolled += EnqueueNotification;
        Refresh();
    }

    // 스포너 파괴와 유물 선택 처리가 끝난 뒤 표시 여부를 판단한다.
    void LateUpdate()
    {
        Bind();
        if (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver)
        {
            StopPresentation();
            return;
        }
        if (appearanceDirty)
        {
            appearanceDirty = false;
            Refresh();
            if (showingNotification && notificationText != null)
                notificationText.text = FormatNotification(currentNotice.type, currentNotice.increase);
        }
        if (RelicManager.Instance != null && RelicManager.Instance.HasPendingSelection)
        {
            waitingForRelicSelection = true;
            if (showingNotification)
            {
                // 가려진 알림은 선택 완료 후 처음부터 다시 보여준다.
                suspendedNotification = true;
                FinishNotification();
            }
            return;
        }
        if (waitingForRelicSelection)
        {
            waitingForRelicSelection = false;
            notificationResumeAt = Time.unscaledTime + Mathf.Max(0f, delayAfterRelicSelection);
        }
        if (Time.unscaledTime < notificationResumeAt) return;
        if (suspendedNotification)
        {
            suspendedNotification = false;
            BeginNotification(currentNotice);
        }
        if (showingNotification)
        {
            float elapsed = Time.unscaledTime - notificationStartedAt;
            float duration = Mathf.Max(0.1f, notificationDuration);
            if (elapsed >= duration)
                FinishNotification();
            else
            {
                float alphaIn = fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f;
                float alphaOut = fadeOutDuration > 0f ? Mathf.Clamp01((duration - elapsed) / fadeOutDuration) : 1f;
                SetFadeAlpha(Mathf.Min(alphaIn, alphaOut));
                if (animatedTarget != null)
                {
                    float progress = emphasisDuration > 0f ? Mathf.Clamp01(elapsed / emphasisDuration) : 1f;
                    animatedTarget.localScale = originalScale * Mathf.Lerp(Mathf.Max(0.01f, startScale), 1f, progress);
                }
            }
        }
        if (!showingNotification && notifications.Count > 0)
            BeginNotification(notifications.Dequeue());
    }

    bool ShouldStyle => overrideTextStyle || generatedPanel != null;

    public string FormatNotification(EnemyMutationType type, float increase)
    {
        string amount = type == EnemyMutationType.Armor ? increase.ToString("0.#") : increase.ToString("P0");
        return (notificationFormat ?? string.Empty).Replace("{효과명}", EffectName(type))
            .Replace("{증가율}", amount);
    }

    string EffectName(EnemyMutationType type)
    {
        switch (type)
        {
            case EnemyMutationType.Spawn: return spawnName ?? string.Empty;
            case EnemyMutationType.Damage: return damageName ?? string.Empty;
            case EnemyMutationType.Health: return healthName ?? string.Empty;
            case EnemyMutationType.Armor: return armorName ?? string.Empty;
            default: return speedName ?? string.Empty;
        }
    }

    // 패널에 Canvas Group이 있으면 패널 전체를, 없으면 알림 텍스트만 페이드한다.
    void SetFadeAlpha(float alpha)
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = originalPanelAlpha * alpha;
            return;
        }
        if (notificationText == null) return;
        Color color = originalNotificationColor;
        color.a *= alpha;
        notificationText.color = color;
    }

    // 매니저 자신이 패널 안에 있으면 패널을 끄는 순간 매니저도 멈추므로 토글하지 않는다.
    bool CanTogglePanel => notificationPanel != null && !transform.IsChildOf(notificationPanel.transform);

    void UpdatePanelActive()
    {
        if (!CanTogglePanel) return;
        bool open = showingNotification
            || (keepPanelOpenWithMutations && manager != null && manager.MutationCount > 0);
        if (notificationPanel.activeSelf != open) notificationPanel.SetActive(open);
    }

    void HandleStateChanged()
    {
        if (manager != null && manager.MutationCount == 0) StopPresentation();
        Refresh();
    }

    void Refresh()
    {
        if (manager != null && manager.MutationCount > 0) EnsureUI();
        ApplyAppearance();
        if (summaryText != null)
            summaryText.text = manager != null && manager.MutationCount > 0
                ? (summaryFormat ?? string.Empty).Replace("{횟수}", manager.MutationCount.ToString())
                    .Replace("{스폰}", manager.SpawnBonus.ToString("P0"))
                    .Replace("{공격력}", manager.DamageBonus.ToString("P0"))
                    .Replace("{체력}", manager.HealthBonus.ToString("P0"))
                    .Replace("{이동속도}", manager.SpeedBonus.ToString("P0"))
                    .Replace("{방어력}", manager.ArmorBonus.ToString("0.#"))
                : string.Empty;
        if (!showingNotification && notifications.Count == 0 && notificationText != null)
            notificationText.text = string.Empty;
        if (generatedPanel != null)
            generatedPanel.SetActive(showingNotification || (manager != null && manager.MutationCount > 0));
        UpdatePanelActive();
    }

    void EnqueueNotification(EnemyMutationType type, float increase)
    {
        notifications.Enqueue(new Notice { type = type, increase = increase });
    }

    public void PreviewNotification()
    {
        if (!Application.isPlaying || (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver)) return;
        EnqueueNotification(previewType, previewIncrease);
    }

    void BeginNotification(Notice notice)
    {
        EnsureUI();
        if (generatedPanel != null) generatedPanel.SetActive(true);
        ApplyAppearance();
        currentNotice = notice;
        showingNotification = true;
        notificationStartedAt = Time.unscaledTime;
        UpdatePanelActive();
        panelGroup = notificationPanel != null ? notificationPanel.GetComponent<CanvasGroup>() : null;
        if (panelGroup != null) originalPanelAlpha = panelGroup.alpha;
        if (notificationText != null)
        {
            originalNotificationColor = notificationText.color;
            notificationText.text = FormatNotification(notice.type, notice.increase);
        }
        SetFadeAlpha(fadeInDuration > 0f ? 0f : 1f);
        animatedTarget = emphasisTarget != null ? emphasisTarget
            : notificationPanel != null ? notificationPanel.transform as RectTransform
            : notificationText != null ? notificationText.rectTransform : null;
        if (animatedTarget != null)
        {
            originalScale = animatedTarget.localScale;
            animatedTarget.localScale = originalScale * (emphasisDuration > 0f ? Mathf.Max(0.01f, startScale) : 1f);
        }
        if (notificationSound != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            audioSource.PlayOneShot(notificationSound, Mathf.Max(0f, volume));
        }
        onNotificationStarted?.Invoke();
    }

    void FinishNotification()
    {
        if (!showingNotification) return;
        showingNotification = false;
        if (animatedTarget != null) animatedTarget.localScale = originalScale;
        animatedTarget = null;
        if (notificationText != null)
        {
            notificationText.text = string.Empty;
            notificationText.color = originalNotificationColor;
        }
        if (panelGroup != null) panelGroup.alpha = originalPanelAlpha;
        panelGroup = null;
        if (generatedPanel != null && (manager == null || manager.MutationCount == 0))
            generatedPanel.SetActive(false);
        UpdatePanelActive();
        onNotificationFinished?.Invoke();
    }

    void StopPresentation()
    {
        notifications.Clear();
        suspendedNotification = false;
        waitingForRelicSelection = false;
        notificationResumeAt = 0f;
        FinishNotification();
        if (summaryText != null) summaryText.text = string.Empty;
        if (notificationText != null) notificationText.text = string.Empty;
        if (generatedPanel != null) generatedPanel.SetActive(false);
        if (CanTogglePanel) notificationPanel.SetActive(false);
    }

    void EnsureUI()
    {
        if (createDefaultUI && summaryText == null && notificationText == null && notificationPanel == null)
            CreateDefaultPanel();
    }

    void ApplyAppearance()
    {
        if (ShouldStyle)
        {
            ApplyTextStyle(summaryText);
            ApplyTextStyle(notificationText);
        }
        if (generatedPanel == null) return;
        generatedPanel.GetComponent<Canvas>().sortingOrder = sortingOrder;
        generatedPanel.GetComponent<CanvasScaler>().referenceResolution =
            new Vector2(Mathf.Max(1f, referenceResolution.x), Mathf.Max(1f, referenceResolution.y));
        generatedRect.anchoredPosition = panelPosition;
        generatedRect.sizeDelta = new Vector2(Mathf.Max(1f, panelSize.x), Mathf.Max(1f, panelSize.y));
        generatedBackground.color = backgroundColor;
    }

    void ApplyTextStyle(TMP_Text text)
    {
        if (text == null) return;
        if (font != null) text.font = font;
        text.fontSize = Mathf.Max(1f, fontSize);
    }

    void CreateDefaultPanel()
    {
        generatedPanel = new GameObject("돌연변이 알림 캔버스", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        generatedPanel.transform.SetParent(transform, false);
        generatedPanel.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        generatedPanel.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        GameObject panel = new GameObject("돌연변이 패널", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(generatedPanel.transform, false);
        generatedRect = panel.GetComponent<RectTransform>();
        generatedRect.anchorMin = generatedRect.anchorMax = generatedRect.pivot = new Vector2(1f, 1f);
        generatedBackground = panel.GetComponent<Image>();
        generatedBackground.raycastTarget = false;
        summaryText = CreateText(panel.transform, "누적 효과", new Vector2(0f, 0.35f), Vector2.one);
        notificationText = CreateText(panel.transform, "결과 알림", Vector2.zero, new Vector2(1f, 0.35f));
        ApplyAppearance();
    }

    TMP_Text CreateText(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        TMP_FontAsset defaultFont = font != null ? font : Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansKR-Black SDF");
        if (defaultFont != null) text.font = defaultFont;
        text.raycastTarget = false;
        text.rectTransform.anchorMin = anchorMin;
        text.rectTransform.anchorMax = anchorMax;
        text.rectTransform.offsetMin = new Vector2(12f, 6f);
        text.rectTransform.offsetMax = new Vector2(-12f, -6f);
        return text;
    }
}
