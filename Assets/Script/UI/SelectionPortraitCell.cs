using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 다중 선택 시 초상화 그리드 한 칸을 구성하는 컴포넌트입니다.
// 그리드 셀 프리팹의 루트에 붙이고, 아래 필드에 각 UI 요소를 연결하세요.
public class SelectionPortraitCell : MonoBehaviour, IPointerClickHandler
{
    [Header("참조")]
    [Label("초상화 이미지")]
    [Tooltip("이 칸에 표시할 초상화 이미지입니다. (필수)")]
    public Image portraitImage;

    [Label("체력 이미지")]
    [Tooltip("(선택) 대상 체력 비율을 표시할 Image입니다. Image Type을 Filled로 설정하면 fillAmount로 반영됩니다.")]
    public Image healthFillImage;

    [Label("라벨 텍스트")]
    [Tooltip("(선택) 대상 이름 등을 표시할 텍스트입니다.")]
    public TextMeshProUGUI labelText;

    [Label("클릭 버튼")]
    [Tooltip("(선택) 칸 클릭을 받을 버튼입니다. 비워두면 칸 안에서 Button을 찾습니다. 버튼이 없으면 칸의 Raycast Target이 켜진 이미지를 클릭해도 동작합니다.")]
    public Button clickButton;

    public SelectableEntity Entity { get; private set; }

    // 칸을 왼쪽 클릭했을 때 호출한다. 다중 선택 패널이 구독한다.
    public event Action<SelectionPortraitCell> OnClicked;

    void Awake()
    {
        if (clickButton == null)
            clickButton = GetComponentInChildren<Button>(true);

        if (clickButton != null)
            clickButton.onClick.AddListener(HandleClick);
    }

    void OnDestroy()
    {
        if (clickButton != null)
            clickButton.onClick.RemoveListener(HandleClick);
    }

    // 버튼이 없는 칸을 위한 클릭 처리. 버튼이 있으면 버튼 onClick만 쓴다(중복 호출 방지).
    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickButton != null || eventData.button != PointerEventData.InputButton.Left)
            return;

        HandleClick();
    }

    void HandleClick()
    {
        if (Entity != null)
            OnClicked?.Invoke(this);
    }

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
