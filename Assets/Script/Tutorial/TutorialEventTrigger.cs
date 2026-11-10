using UnityEngine;

[AddComponentMenu("게임/튜토리얼/TutorialEventTrigger")]
public class TutorialEventTrigger : MonoBehaviour
{
    [Label("이벤트 이름")] public string eventId;
    [Label("아군 유닛 진입 시 실행")] public bool onUnitEnter;

    public void RaiseEvent()
    {
        TutorialController.Instance?.RaiseCustomEvent(eventId);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!onUnitEnter) return;
        SelectableEntity entity = other.GetComponentInParent<SelectableEntity>();
        UnitSelectionManager selection = UnitSelectionManager.Instance;
        if (entity == null || selection == null || entity.entityType != SelectableEntityType.Unit ||
            entity.ownerId != selection.localPlayerOwnerId) return;
        RaiseEvent();
    }
}
