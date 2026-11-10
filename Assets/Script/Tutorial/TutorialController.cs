using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-400)]
[AddComponentMenu("게임/튜토리얼/TutorialController")]
public class TutorialController : MonoBehaviour
{
    public static TutorialController Instance { get; private set; }
    public static bool BlocksGameplay => (Instance != null && Instance.current != null &&
        (Instance.current.blockGameplay || Instance.current.pauseGame)) || Time.frameCount <= blockedThroughFrame;

    [Label("표시 패널")] public TutorialPanel panel;
    [Label("기본 이벤트 목록")]
    [Tooltip("MapLoader가 없을 때 사용합니다. MapLoader가 있으면 스테이지의 튜토리얼 목록을 사용합니다.")]
    public TutorialBinding[] defaultBindings = Array.Empty<TutorialBinding>();
    [Label("페이지 클릭 보호 시간(초)")]
    [Tooltip("패널을 연 클릭이나 빠른 중복 클릭으로 페이지가 넘어가는 것을 막습니다. 실제 시간 기준입니다.")]
    [Min(0f)] public float clickGuardSeconds = 0.2f;

    readonly Queue<TutorialSequence> pending = new Queue<TutorialSequence>();
    readonly HashSet<TutorialSequence> queued = new HashSet<TutorialSequence>();
    readonly HashSet<TutorialSequence> completed = new HashSet<TutorialSequence>();
    TutorialBinding[] bindings;
    TutorialSequence current;
    int pageIndex;
    int pageFrame;
    float pageShownAt;
    bool ready;
    bool configured;
    int lastCycle = -1;
    DayNightPhase lastPhase;
    static int blockedThroughFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; blockedThroughFrame = -1; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnEnable()
    {
        if (Instance != this) return;
        if (panel != null) panel.NextRequested += NextPage;
    }

    void Start()
    {
        if (Instance != this || configured) return;
        MapLoader loader = MapLoader.Instance;
        Configure(loader != null && loader.LoadedConfig != null ? loader.LoadedConfig.tutorials : defaultBindings);
    }

    public void Configure(TutorialBinding[] stageBindings)
    {
        CancelAll();
        completed.Clear();
        bindings = stageBindings ?? Array.Empty<TutorialBinding>();
        configured = true;
        ready = false;
        lastCycle = -1;
    }

    void Update()
    {
        if (Instance != this) return;
        if (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver)
        {
            CancelAll();
            return;
        }
        if (current != null && (panel == null || !panel.isActiveAndEnabled ||
            panel.panelRoot == null || !panel.panelRoot.activeInHierarchy)) CancelAll();
        MapLoader loader = MapLoader.Instance;
        if (!configured || StageIntroController.IsPlaying || (loader != null && !loader.IsMapReady)) return;
        if (!ready)
        {
            ready = true;
            Notify(TutorialEvent.StageStarted);
        }
        DayNightCycle cycle = DayNightCycle.Instance;
        if (cycle != null && cycle.autoStart && (lastCycle != cycle.CycleCount || lastPhase != cycle.CurrentPhase))
        {
            lastCycle = cycle.CycleCount;
            lastPhase = cycle.CurrentPhase;
            Notify(cycle.IsDay ? TutorialEvent.DayStarted : TutorialEvent.NightStarted, null, cycle.CycleCount + 1);
        }
        // 다른 정지 화면이 닫힌 뒤 안내를 시작한다.
        if (current == null && pending.Count > 0 && !GamePause.IsPaused && Time.timeScale > 0f &&
            Time.frameCount > blockedThroughFrame) OpenNext();
    }

    public void Notify(TutorialEvent trigger, string eventId = null, int wave = 0)
    {
        if (!isActiveAndEnabled || !configured || (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver)) return;
        if (current != null && current.waitForEvent && pageIndex == current.pages.Length - 1 &&
            current.completionTrigger == trigger && (trigger != TutorialEvent.Custom || current.completionEvent == eventId)) FinishCurrent();
        // 실습을 완료한 이벤트는 그 실습을 선행 조건으로 둔 다음 안내에도 전달한다.
        foreach (TutorialBinding binding in bindings)
        {
            if (binding == null || binding.trigger != trigger ||
                (trigger == TutorialEvent.Custom && binding.eventId != eventId) ||
                ((trigger == TutorialEvent.DayStarted || trigger == TutorialEvent.NightStarted) && binding.wave > 0 && binding.wave != wave) ||
                (binding.prerequisite != null && !completed.Contains(binding.prerequisite))) continue;
            Play(binding.sequence);
        }
    }

    public void RaiseCustomEvent(string eventId)
    {
        if (!string.IsNullOrWhiteSpace(eventId)) Notify(TutorialEvent.Custom, eventId);
    }

    public void Play(TutorialSequence sequence)
    {
        if (!isActiveAndEnabled || sequence == null || queued.Contains(sequence) || current == sequence ||
            (sequence.oncePerStage && completed.Contains(sequence))) return;
        if (panel == null || !panel.CanShow(sequence) ||
            (sequence.waitForEvent && (sequence.pauseGame || sequence.blockGameplay)))
        {
            Debug.LogWarning("TutorialController: 패널·본문·다음 버튼·입력 차단 배경·이미지 영역과 페이지를 확인하세요. 실습 안내는 시간 정지와 조작 차단을 꺼야 합니다.", this);
            return;
        }
        pending.Enqueue(sequence);
        queued.Add(sequence);
    }

    void OpenNext()
    {
        TutorialSequence next = pending.Dequeue();
        queued.Remove(next);
        if (next == null || panel == null || !panel.CanShow(next)) return;
        current = next;
        pageIndex = 0;
        if (current.pauseGame) GamePause.Acquire(this);
        ShowPage();
    }

    void ShowPage()
    {
        pageFrame = Time.frameCount;
        pageShownAt = Time.unscaledTime;
        panel.Show(current.pages[pageIndex], current.blockGameplay || current.pauseGame,
            !current.waitForEvent || pageIndex < current.pages.Length - 1);
    }

    public void NextPage()
    {
        if (current == null || Time.frameCount <= pageFrame || Time.unscaledTime - pageShownAt < clickGuardSeconds) return;
        if (pageIndex + 1 < current.pages.Length) { pageIndex++; ShowPage(); }
        else if (!current.waitForEvent) FinishCurrent();
    }

    void FinishCurrent()
    {
        completed.Add(current);
        CloseCurrent();
    }

    void CloseCurrent()
    {
        if (current != null && (current.pauseGame || current.blockGameplay)) blockedThroughFrame = Time.frameCount + 1;
        current = null;
        if (panel != null) panel.Hide();
        GamePause.Release(this);
    }

    public void CancelAll()
    {
        StopAllCoroutines();
        pending.Clear();
        queued.Clear();
        CloseCurrent();
    }

    public void WatchConstruction(GameObject building)
    {
        if (isActiveAndEnabled && building != null) StartCoroutine(WaitForConstruction(building));
    }

    IEnumerator WaitForConstruction(GameObject building)
    {
        while (building != null && BuildingConstructionGate.IsFeatureLockedOn(building)) yield return null;
        if (building != null) Notify(TutorialEvent.BuildingCompleted);
    }

    void OnDisable()
    {
        if (Instance != this) return;
        if (panel != null) panel.NextRequested -= NextPage;
        CancelAll();
    }

    void OnDestroy()
    {
        if (Instance == this) { CancelAll(); Instance = null; }
    }
}
