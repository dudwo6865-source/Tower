using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 다중 선택 시 초상화 그리드 한 칸을 구성하는 컴포넌트입니다.
// 그리드 셀 프리팹의 루트에 붙이고, 아래 필드에 각 UI 요소를 연결하세요.
public class SelectionPortraitCell : MonoBehaviour
{
    [Header("References")]
    [Tooltip("이 칸에 표시할 초상화 이미지입니다. (필수)")]
    public Image portraitImage;

    [Tooltip("(선택) 대상 체력 비율을 표시할 Image입니다. Image Type을 Filled로 설정하면 fillAmount로 반영됩니다.")]
    public Image healthFillImage;

    [Tooltip("(선택) 대상 이름 등을 표시할 텍스트입니다.")]
    public TextMeshProUGUI labelText;

    public SelectableEntity Entity { get; private set; }

    public void SetEntity(SelectableEntity entity, Sprite portrait, string label)
    {
        Entity = entity;

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        if (labelText != null)
            labelText.text = label ?? string.Empty;

        RefreshHealth();
    }

    public void RefreshHealth()
    {
        if (healthFillImage == null)
            return;

        if (Entity == null)
        {
            healthFillImage.fillAmount = 0f;
            return;
        }

        EntityHealth health = Entity.GetComponent<EntityHealth>();

        if (health != null && health.MaxHealth > 0f)
            healthFillImage.fillAmount =
                Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
    }
}
