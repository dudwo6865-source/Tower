using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 누를 수 없는(Interactable 꺼짐) 상태에서도 마우스를 올리면 Highlighted 상태로 전환하는 버튼입니다.
/// 기본 Button은 Disabled일 때 호버를 무시하므로, 잠긴 레시피 버튼에 마우스를 올려 설명 라벨을 띄울 때 씁니다.
/// 클릭은 기본 Button과 같이 Interactable일 때만 동작합니다.
/// 마우스를 빼면 다시 Disabled로 돌아가므로, Animator에 Highlighted → Disabled 전환(Disabled 트리거)이 있어야 합니다.
/// </summary>
[AddComponentMenu("UI/Disabled Hover Button")]
public class DisabledHoverButton : Button
{
    [Label("잠겨도 호버 표시")]
    [Tooltip("켜면 누를 수 없는 상태에서도 마우스를 올렸을 때 Highlighted 상태로 전환합니다.")]
    public bool hoverWhenDisabled = true;

    bool pointerInside;

    public override void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        base.OnPointerEnter(eventData);
        TransitionWhileDisabled();
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        base.OnPointerExit(eventData);
        TransitionWhileDisabled();
    }

    // 기본 Selectable은 Interactable이 꺼져 있으면 호버 때 상태 전환을 호출하지 않으므로 직접 호출한다.
    void TransitionWhileDisabled()
    {
        if (hoverWhenDisabled && IsActive() && !IsInteractable())
            DoStateTransition(SelectionState.Disabled, false);
    }

    protected override void OnDisable()
    {
        pointerInside = false;
        base.OnDisable();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        if (hoverWhenDisabled && pointerInside && state == SelectionState.Disabled)
            state = SelectionState.Highlighted;

        base.DoStateTransition(state, instant);
    }
}
