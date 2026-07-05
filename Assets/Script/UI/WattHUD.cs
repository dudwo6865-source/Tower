using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WattHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("현재 / 최대 Watt 텍스트입니다.")]
    public TextMeshProUGUI wattText;

    [Tooltip("초당 충전량 텍스트입니다.")]
    public TextMeshProUGUI incomeRateText;

    [Tooltip("Watt 충전량을 표시할 슬라이더입니다.")]
    public Slider wattSlider;

    [Tooltip("비워두면 씬에서 WattManager를 자동으로 찾습니다.")]
    public WattManager wattManager;

    [Header("Display")]
    public string amountFormat = "{0:0} / {1:0} W";
    public string incomeFormat = "+{0:0.0} /s";

    void Start()
    {
        if (wattManager == null)
            wattManager = WattManager.Instance;

        if (wattManager == null)
            wattManager = FindObjectOfType<WattManager>();

        if (wattManager == null)
        {
            Debug.LogError("WattHUD: WattManager not found");
            return;
        }

        if (wattSlider != null)
        {
            wattSlider.minValue = 0f;
            wattSlider.maxValue = 1f;
            wattSlider.interactable = false;
        }

        wattManager.OnWattChanged += HandleWattChanged;
        RefreshDisplay();
    }

    void OnDestroy()
    {
        if (wattManager != null)
            wattManager.OnWattChanged -= HandleWattChanged;
    }

    void HandleWattChanged(float currentWatt)
    {
        RefreshDisplay();
    }

    void RefreshDisplay()
    {
        if (wattManager == null)
            return;

        if (wattText != null)
        {
            wattText.text = string.Format(
                amountFormat,
                wattManager.CurrentWatt,
                wattManager.MaxWatt);
        }

        if (incomeRateText != null)
            incomeRateText.text = string.Format(incomeFormat, wattManager.incomePerSecond);

        if (wattSlider != null)
            wattSlider.value = wattManager.FillRatio;
    }
}
