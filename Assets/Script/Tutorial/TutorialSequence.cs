using System;
using UnityEngine;

public enum TutorialEvent
{
    [InspectorName("스테이지 시작")] StageStarted,
    [InspectorName("낮 시작")] DayStarted,
    [InspectorName("밤 시작")] NightStarted,
    [InspectorName("건물 건설 완료")] BuildingCompleted,
    [InspectorName("유닛 생산 완료")] UnitProduced,
    [InspectorName("사용자 지정 이벤트")] Custom
}

[Serializable]
public class TutorialPage
{
    [Label("본문"), TextArea(3, 12)] public string text;
    [Label("이미지")] public Sprite image;
    [Label("이미지 영역 번호"), Min(0)] public int imageSlot;
}

[Serializable]
public class TutorialBinding
{
    [Label("발생 이벤트")] public TutorialEvent trigger;
    [Label("이벤트 이름")]
    [Tooltip("사용자 지정 이벤트에서만 사용합니다. 구역 트리거의 이벤트 이름과 같게 입력합니다.")]
    public string eventId;
    [Label("웨이브 번호")]
    [Tooltip("낮·밤 이벤트에 적용합니다. 0이면 모든 웨이브에 반응합니다.")]
    [Min(0)] public int wave;
    [Label("선행 안내")]
    [Tooltip("연결하면 이 안내를 끝낸 뒤에 발생한 이벤트에만 반응합니다.")]
    public TutorialSequence prerequisite;
    [Label("표시할 안내")] public TutorialSequence sequence;
}

[CreateAssetMenu(menuName = "Tank/튜토리얼/안내", fileName = "TutorialSequence")]
public class TutorialSequence : ScriptableObject
{
    [Label("게임 시간 정지")] public bool pauseGame = true;
    [Label("게임 조작 차단")]
    [Tooltip("설명에서는 켜고, 직접 행동하는 안내에서는 시간 정지와 함께 끕니다.")]
    public bool blockGameplay = true;
    [Label("실행 중 한 번만 표시")] public bool oncePerStage = true;
    [Label("실습 이벤트까지 대기")]
    [Tooltip("켜면 마지막 페이지는 클릭으로 닫히지 않고 완료 이벤트를 기다립니다. 게임 시간 정지와 게임 조작 차단을 끄세요.")]
    public bool waitForEvent;
    [Label("완료 이벤트")] public TutorialEvent completionTrigger = TutorialEvent.BuildingCompleted;
    [Label("완료 이벤트 이름")]
    [Tooltip("완료 이벤트가 사용자 지정일 때만 사용합니다.")]
    public string completionEvent;
    [Label("페이지")] public TutorialPage[] pages = Array.Empty<TutorialPage>();
}
