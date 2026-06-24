using TMPro;
using UnityEngine;

public class WattHUD : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI wattText;

    [Tooltip("비워두면 씬에서 WattManager를 자동으로 찾습니다.")]
    public WattManager wattManager;

    [Header("Display")]
    public string format = "{0:0} W";

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

        wattManager.OnWattChanged += HandleWattChanged;
        HandleWattChanged(wattManager.CurrentWatt);
    }

    void OnDestroy()
    {
        if (wattManager != null)
            wattManager.OnWattChanged -= HandleWattChanged;
    }

    void HandleWattChanged(float currentWatt)
    {
        if (wattText == null)
            return;

        wattText.text = string.Format(format, currentWatt);
    }
}
