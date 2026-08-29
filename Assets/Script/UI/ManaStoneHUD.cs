using TMPro;
using UnityEngine;

// 보유 마석을 표시하는 HUD입니다.
public class ManaStoneHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("현재 마석 수량 텍스트입니다.")]
    public TextMeshProUGUI manaStoneText;

    [Tooltip("비워두면 씬에서 ManaStoneManager를 자동으로 찾습니다.")]
    public ManaStoneManager manaStoneManager;

    [Header("Display")]
    [Tooltip("상한이 있을 때 표시 형식입니다. {0}=현재, {1}=최대")]
    public string cappedFormat = "{0:0} / {1:0} 마석";

    [Tooltip("상한이 없을 때 표시 형식입니다. {0}=현재")]
    public string uncappedFormat = "{0:0} 마석";

    void Start()
    {
        if (manaStoneManager == null)
            manaStoneManager = ManaStoneManager.Instance;

        if (manaStoneManager == null)
            manaStoneManager = FindObjectOfType<ManaStoneManager>();

        if (manaStoneManager == null)
        {
            Debug.LogError("ManaStoneHUD: ManaStoneManager not found");
            return;
        }

        manaStoneManager.OnManaStoneChanged += HandleChanged;
        RefreshDisplay();
    }

    void OnDestroy()
    {
        if (manaStoneManager != null)
            manaStoneManager.OnManaStoneChanged -= HandleChanged;
    }

    void HandleChanged(float current)
    {
        RefreshDisplay();
    }

    void RefreshDisplay()
    {
        if (manaStoneManager == null || manaStoneText == null)
            return;

        manaStoneText.text = manaStoneManager.HasCap
            ? string.Format(cappedFormat, manaStoneManager.CurrentManaStone, manaStoneManager.MaxManaStone)
            : string.Format(uncappedFormat, manaStoneManager.CurrentManaStone);
    }
}
