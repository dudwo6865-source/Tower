using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 기존 UI 텍스트를 연결하거나, 연결이 없으면 첫 돌연변이 때 기본 표시를 만든다.
[DisallowMultipleComponent]
public class EnemyMutationHUD : MonoBehaviour
{
    static EnemyMutationHUD instance;
    [Label("누적 효과 텍스트")]
    public TMP_Text summaryText;
    [Label("결과 알림 텍스트")]
    public TMP_Text notificationText;
    [Label("알림 표시 시간(초)")]
    [Min(0.1f)] public float notificationDuration = 5f;
    [Label("자동 표시 위치")]
    public Vector2 panelPosition = new Vector2(-24f, -160f);
    [Label("자동 표시 크기")]
    public Vector2 panelSize = new Vector2(430f, 150f);
    [Label("자동 표시 글자 크기")]
    public float fontSize = 20f;

    EnemyMutationManager manager;
    readonly Queue<string> notifications = new Queue<string>();
    float notificationUntil;
    GameObject generatedPanel;

    public static void EnsureInstance()
    {
        if (instance != null) return;
        instance = FindObjectOfType<EnemyMutationHUD>();
        if (instance == null)
            instance = new GameObject("돌연변이 표시").AddComponent<EnemyMutationHUD>();
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

    void OnEnable()
    {
        Bind();
    }

    void OnDisable()
    {
        if (manager == null) return;
        manager.OnChanged -= Refresh;
        manager.OnMutationApplied -= ShowNotification;
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
        manager.OnChanged += Refresh;
        manager.OnMutationApplied += ShowNotification;
        Refresh();
    }

    void Update()
    {
        Bind();
        bool gameOver = GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver;
        if (gameOver)
        {
            notifications.Clear();
            if (notificationText != null) notificationText.text = string.Empty;
            if (generatedPanel != null) generatedPanel.SetActive(false);
            return;
        }
        if (notificationText == null || Time.unscaledTime < notificationUntil) return;
        notificationText.text = notifications.Count > 0 ? notifications.Dequeue() : string.Empty;
        if (notificationText.text.Length > 0)
            notificationUntil = Time.unscaledTime + notificationDuration;
    }

    void Refresh()
    {
        if (manager == null) return;
        if (manager.MutationCount > 0 && summaryText == null && notificationText == null)
            CreateDefaultPanel();
        if (summaryText != null)
            summaryText.text = manager.MutationCount > 0 ? manager.Summary : string.Empty;
        if (manager.MutationCount == 0)
        {
            notifications.Clear();
            notificationUntil = 0f;
            if (notificationText != null) notificationText.text = string.Empty;
        }
        if (generatedPanel != null) generatedPanel.SetActive(manager.MutationCount > 0);
    }

    void ShowNotification(string message)
    {
        notifications.Enqueue(message);
    }

    void CreateDefaultPanel()
    {
        generatedPanel = new GameObject("돌연변이 알림 캔버스", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        generatedPanel.transform.SetParent(transform, false);
        Canvas canvas = generatedPanel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = generatedPanel.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("돌연변이 패널", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(generatedPanel.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = panelPosition;
        rect.sizeDelta = panelSize;
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.08f, 0.07f, 0.1f, 0.88f);
        background.raycastTarget = false;
        summaryText = CreateText(panel.transform, "누적 효과", new Vector2(12f, -12f), new Vector2(panelSize.x - 24f, 85f));
        notificationText = CreateText(panel.transform, "결과 알림", new Vector2(12f, -100f), new Vector2(panelSize.x - 24f, 45f));
        notificationText.color = new Color(1f, 0.75f, 0.4f);
    }

    TMP_Text CreateText(Transform parent, string objectName, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansKR-Black SDF");
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.raycastTarget = false;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0f, 1f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        return text;
    }
}
