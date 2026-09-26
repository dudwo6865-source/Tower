using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WattHUD : MonoBehaviour
{
    [Header("참조")]
    [Label("Watt 텍스트")]
    [Tooltip("현재 / 최대 Watt 텍스트입니다.")]
    public TextMeshProUGUI wattText;

    [Label("초당 수입 텍스트")]
    [Tooltip("초당 순수입(수입 - 생산 소모량) 텍스트입니다.")]
    public TextMeshProUGUI incomeRateText;

    [Label("Watt 슬라이더")]
    [Tooltip("Watt 충전량을 표시할 슬라이더입니다.")]
    public Slider wattSlider;

    [Label("Watt 매니저")]
    [Tooltip("비워두면 씬에서 WattManager를 자동으로 찾습니다.")]
    public WattManager wattManager;

    [Header("표시")]
    [Label("Watt 형식")]
    [Tooltip("현재 / 최대 Watt 텍스트 형식입니다. {0}=현재 Watt, {1}=최대 Watt")]
    public string amountFormat = "{0:0} / {1:0} W";

    [Label("초당 수입 형식")]
    [Tooltip("초당 수입 텍스트 형식입니다. {0}=순수입(수입 - 소모), {1}=수입, {2}=생산 소모량\n" +
             "기본값의 {0:+0.0;-0.0;0.0}은 양수면 +, 음수면 - 부호를 붙입니다.")]
    public string netIncomeFormat = "{0:+0.0;-0.0;0.0} /s";

    [Label("적자 시 색 바꾸기")]
    [Tooltip("켜면 순수입이 음수일 때 초당 수입 텍스트를 아래 색으로 바꿉니다.")]
    public bool tintWhenNegative = true;

    [Label("적자 색")]
    [Tooltip("순수입이 음수일 때 초당 수입 텍스트 색입니다.")]
    public Color negativeIncomeColor = new Color(1f, 0.35f, 0.3f, 1f);

    // 순수입이 바뀌었을 때만 텍스트를 다시 쓰기 위한 마지막 표시값
    float lastNetIncome = float.NaN;
    Color defaultIncomeColor;

    void Start()
    {
        if (wattManager == null)
            wattManager = WattManager.Instance;

        if (wattManager == null)
            wattManager = FindObjectOfType<WattManager>();

        if (wattManager == null)
        {
            Debug.LogError("WattHUD: WattManager를 찾지 못했습니다.");
            return;
        }

        if (wattSlider != null)
        {
            wattSlider.minValue = 0f;
            wattSlider.maxValue = 1f;
            wattSlider.interactable = false;
        }

        if (incomeRateText != null)
            defaultIncomeColor = incomeRateText.color;

        wattManager.OnWattChanged += HandleWattChanged;
        RefreshDisplay();
    }

    void OnDestroy()
    {
        if (wattManager != null)
            wattManager.OnWattChanged -= HandleWattChanged;
    }

    // Watt가 가득 차 변화가 없어도 생산 시작/중지로 순수입은 바뀔 수 있으므로 매 프레임 확인한다.
    void Update()
    {
        RefreshIncome();
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

        RefreshIncome();

        if (wattSlider != null)
            wattSlider.value = wattManager.FillRatio;
    }

    void RefreshIncome()
    {
        if (wattManager == null || incomeRateText == null)
            return;

        float income = wattManager.EffectiveIncomePerSecond;
        float drain = wattManager.CurrentDrainPerSecond;
        float net = income - drain;

        if (Mathf.Approximately(net, lastNetIncome))
            return;

        lastNetIncome = net;
        incomeRateText.text = string.Format(netIncomeFormat, net, income, drain);

        if (tintWhenNegative)
            incomeRateText.color = net < 0f ? negativeIncomeColor : defaultIncomeColor;
    }
}
