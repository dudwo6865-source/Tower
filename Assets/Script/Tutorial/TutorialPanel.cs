using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[AddComponentMenu("게임/튜토리얼/TutorialPanel")]
public class TutorialPanel : MonoBehaviour
{
    [Label("패널 루트")] public GameObject panelRoot;
    [Label("본문 텍스트")] public TMP_Text bodyText;
    [Label("이미지 영역")] public Image[] imageSlots = Array.Empty<Image>();
    [Label("다음 버튼")]
    [Tooltip("패널 전체를 클릭하려면 패널에 만든 Button을 연결합니다. 별도의 이벤트 연결은 필요 없습니다.")]
    public Button nextButton;
    [Label("입력 차단 배경")]
    [Tooltip("설명 뒤의 게임 UI를 가리는 전체 화면 Image 오브젝트를 연결합니다. Raycast Target을 켜고 패널보다 뒤에 배치합니다.")]
    public GameObject inputBlocker;
    public event Action NextRequested;
    public bool IsConfigured => panelRoot != null && bodyText != null && nextButton != null &&
        !transform.IsChildOf(panelRoot.transform);

    void Awake()
    {
        if (nextButton != null) nextButton.onClick.AddListener(RequestNext);
        Hide();
    }

    void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(RequestNext);
    }

    void RequestNext() { NextRequested?.Invoke(); }

    public bool CanShow(TutorialSequence sequence)
    {
        if (!isActiveAndEnabled || !IsConfigured || sequence == null || sequence.pages == null || sequence.pages.Length == 0)
            return false;
        if (sequence.blockGameplay || sequence.pauseGame)
        {
            if (inputBlocker == null || transform.IsChildOf(inputBlocker.transform)) return false;
            Graphic blockerGraphic = inputBlocker.GetComponent<Graphic>();
            if (blockerGraphic == null || !blockerGraphic.raycastTarget) return false;
        }
        foreach (TutorialPage page in sequence.pages)
            if (page == null || (page.image != null && (imageSlots == null || page.imageSlot < 0 ||
                page.imageSlot >= imageSlots.Length || imageSlots[page.imageSlot] == null))) return false;
        return true;
    }

    public void Show(TutorialPage page, bool blockInput, bool canAdvance)
    {
        bodyText.text = page.text ?? "";
        if (imageSlots != null)
            for (int i = 0; i < imageSlots.Length; i++)
            {
                Image slot = imageSlots[i];
                if (slot == null) continue;
                bool visible = page.image != null && i == page.imageSlot;
                if (visible) slot.sprite = page.image;
                slot.gameObject.SetActive(visible);
            }
        nextButton.interactable = canAdvance;
        if (inputBlocker != null) inputBlocker.SetActive(blockInput);
        panelRoot.SetActive(true);
        if (blockInput && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
    }

    public void Hide()
    {
        // 표시 루트와 이 컴포넌트는 별도 오브젝트여야 한다.
        if (panelRoot != null && !transform.IsChildOf(panelRoot.transform)) panelRoot.SetActive(false);
        if (inputBlocker != null && !transform.IsChildOf(inputBlocker.transform)) inputBlocker.SetActive(false);
    }
}
