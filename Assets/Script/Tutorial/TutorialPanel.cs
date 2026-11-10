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

    public bool CanShow(TutorialSequence sequence) => GetValidationError(sequence) == null;

    public string GetValidationError(TutorialSequence sequence)
    {
        if (sequence == null) return "표시할 안내 에셋이 없습니다.";
        if (sequence.pages == null || sequence.pages.Length == 0)
            return "안내 에셋의 '페이지'가 비어 있습니다. 페이지를 최소 1개 추가하고 본문을 입력하세요.";
        if (!isActiveAndEnabled) return "TutorialPanel 컴포넌트 또는 부모 오브젝트가 비활성 상태입니다.";
        if (panelRoot == null) return "TutorialPanel의 '패널 루트'를 연결하세요.";
        if (bodyText == null) return "TutorialPanel의 '본문 텍스트'를 연결하세요.";
        if (nextButton == null) return "TutorialPanel의 '다음 버튼'을 연결하세요.";
        if (transform.IsChildOf(panelRoot.transform))
            return "TutorialPanel은 '패널 루트' 자신이나 자식에 둘 수 없습니다. 항상 활성인 별도 오브젝트로 옮기세요.";
        if (sequence.waitForEvent && (sequence.pauseGame || sequence.blockGameplay))
            return "실습 안내에서는 '게임 시간 정지'와 '게임 조작 차단'을 모두 끄세요.";
        if (sequence.blockGameplay || sequence.pauseGame)
        {
            if (inputBlocker == null) return "TutorialPanel의 '입력 차단 배경'을 연결하세요.";
            if (transform.IsChildOf(inputBlocker.transform))
                return "TutorialPanel을 '입력 차단 배경' 자신이나 자식 밖으로 옮기세요.";
            Graphic blockerGraphic = inputBlocker.GetComponent<Graphic>();
            if (blockerGraphic == null) return "'입력 차단 배경'으로 연결한 오브젝트 자체에 Image가 필요합니다.";
            if (!blockerGraphic.raycastTarget) return "'입력 차단 배경'의 Image에서 Raycast Target을 켜세요.";
        }
        for (int i = 0; i < sequence.pages.Length; i++)
        {
            TutorialPage page = sequence.pages[i];
            if (page == null) return $"안내 에셋의 {i + 1}번째 페이지가 비어 있습니다.";
            if (page.image != null && (imageSlots == null || page.imageSlot < 0 ||
                page.imageSlot >= imageSlots.Length || imageSlots[page.imageSlot] == null))
                return $"{i + 1}번째 페이지의 이미지 영역 번호({page.imageSlot})에 연결된 Image가 없습니다. TutorialPanel의 '이미지 영역'을 확인하세요. 번호는 0부터 시작합니다.";
        }
        return null;
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
