using TMPro;
using UnityEngine;

// 로비의 크레딧 텍스트에 붙여 저장된 보유량을 표시한다.
[RequireComponent(typeof(TMP_Text))]
public class CreditHUD : MonoBehaviour
{
    [Label("크레딧 텍스트")]
    [SerializeField] TMP_Text balanceText;
    [Label("표시 형식")]
    [SerializeField] string displayFormat = "크레딧 {0:N0}";

    void OnEnable()
    {
        if (balanceText == null) balanceText = GetComponent<TMP_Text>();
        CreditWallet.Changed += Refresh;
        Refresh();
    }

    void OnDisable() => CreditWallet.Changed -= Refresh;

    public void Refresh()
    {
        if (balanceText != null)
            balanceText.text = CreditWallet.TryGetBalance(out long balance)
                ? string.Format(displayFormat, balance) : "크레딧 불러오기 실패";
    }
}
