using System.Collections;
using TMPro;
using UnityEngine;

public class DayNightHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("비워두면 씬에서 DayNightCycle을 자동으로 찾습니다.")]
    public DayNightCycle dayNightCycle;

    [Tooltip("남은 낮/밤 시간을 표시할 텍스트입니다.")]
    public TextMeshProUGUI timerText;

    [Tooltip("페이즈 변경 알림을 표시할 텍스트입니다.")]
    public TextMeshProUGUI notificationText;

    [Header("Notification Messages")]
    [Tooltip("낮이 시작될 때 표시할 문구입니다.")]
    public string dayStartedMessage = "낮이 되었습니다";

    [Tooltip("밤이 시작될 때 표시할 문구입니다. {0} = 밤 번호")]
    public string nightStartedMessageFormat = "{0}번째 밤이 되었습니다";

    [Tooltip("알림 텍스트가 화면에 유지되는 시간(초)입니다.")]
    public float notificationDuration = 3f;

    [Header("Timer")]
    [Tooltip("낮 타이머 표시 형식입니다. {0} = 남은 초")]
    public string dayTimerFormat = "낮 {0:0}s";

    [Tooltip("밤 타이머 표시 형식입니다. {0} = 남은 초")]
    public string nightTimerFormat = "밤 {0:0}s";

    Coroutine notificationRoutine;

    void Start()
    {
        if (dayNightCycle == null)
            dayNightCycle = DayNightCycle.Instance;

        if (dayNightCycle == null)
            dayNightCycle = FindObjectOfType<DayNightCycle>();

        if (dayNightCycle == null)
        {
            Debug.LogError("DayNightHUD: DayNightCycle not found");
            return;
        }

        dayNightCycle.OnPhaseStarted += HandlePhaseStarted;

        if (notificationText != null)
            notificationText.text = string.Empty;

        HandlePhaseStarted(dayNightCycle.CurrentPhase);
    }

    void OnDestroy()
    {
        if (dayNightCycle != null)
            dayNightCycle.OnPhaseStarted -= HandlePhaseStarted;
    }

    void Update()
    {
        RefreshTimer();
    }

    void HandlePhaseStarted(DayNightPhase phase)
    {
        if (!(phase == DayNightPhase.Day && dayNightCycle.CycleCount == 0))
            ShowPhaseNotification(phase);

        RefreshTimer();
    }

    void ShowPhaseNotification(DayNightPhase phase)
    {
        if (notificationText == null)
            return;

        string message = phase == DayNightPhase.Day
            ? dayStartedMessage
            : string.Format(
                nightStartedMessageFormat,
                dayNightCycle.CurrentNightNumber);

        notificationText.text = message;

        if (notificationRoutine != null)
            StopCoroutine(notificationRoutine);

        if (notificationDuration > 0f)
            notificationRoutine = StartCoroutine(ClearNotificationAfterDelay());
    }

    IEnumerator ClearNotificationAfterDelay()
    {
        yield return new WaitForSeconds(notificationDuration);
        notificationText.text = string.Empty;
        notificationRoutine = null;
    }

    void RefreshTimer()
    {
        if (timerText == null || dayNightCycle == null)
            return;

        string format = dayNightCycle.IsDay ? dayTimerFormat : nightTimerFormat;
        timerText.text = string.Format(format, dayNightCycle.RemainingPhaseTime);
    }
}
