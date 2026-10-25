using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>명령 확정음 종류입니다. 종류별 클립이 비어 있으면 '기본 명령 확정음'을 씁니다.</summary>
public enum UICommandSound
{
    Move,
    Attack,
    Patrol,
    RallyPoint,
    Stop
}

/// <summary>
/// 버튼 클릭과 명령 입력(모드 진입·확정·취소) 효과음을 재생합니다. 씬이 바뀌어도 유지됩니다.
/// 버튼은 하나씩 연결하지 않고 마우스 아래의 Button/Toggle을 직접 감지하므로,
/// 런타임에 생성되는 버튼도 따로 할 일이 없습니다. 특정 버튼만 소리를 바꾸려면 UIButtonSound를 붙입니다.
///
/// 씬에 없으면 Resources/UISoundManager 프리팹을 불러오고, 그것도 없으면 빈 매니저(무음)를 만듭니다.
/// 클립을 지정하려면 이 컴포넌트를 붙인 프리팹을 Assets/Resources/UISoundManager.prefab으로 두세요.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-2000)]
public class UISoundManager : MonoBehaviour
{
    public static UISoundManager Instance { get; private set; }

    const string ResourcePath = "UISoundManager";

    [Header("재생")]
    [Label("전체 UI 볼륨")]
    [Tooltip("모든 UI 효과음에 곱해지는 볼륨입니다.")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;

    [Label("오디오 믹서 그룹")]
    [Tooltip("UI 효과음을 보낼 Audio Mixer 그룹입니다. 비워두면 기본 출력으로 재생합니다.")]
    public AudioMixerGroup outputMixerGroup;

    [Label("겹침 방지 시간(초)")]
    [Tooltip("UI 효과음이 이 시간 안에 연달아 나면 먼저 난 소리만 냅니다. 버튼을 눌러 명령 모드에 들어갈 때 클릭음과 모드 진입음이 겹치지 않게 합니다. 0이면 모두 냅니다.")]
    [Min(0f)]
    public float overlapBlockSeconds = 0.06f;

    [Label("피치 랜덤 폭")]
    [Tooltip("재생할 때마다 피치를 1 ± 이 값 사이에서 무작위로 바꿔 반복음이 덜 기계적으로 들리게 합니다. 0이면 항상 같은 피치입니다.")]
    [Range(0f, 0.3f)]
    public float pitchRandomRange = 0.03f;

    [Header("버튼")]
    [Label("버튼 클릭음")]
    [Tooltip("누를 수 있는 Button/Toggle을 클릭했을 때 재생합니다.")]
    public AudioClip buttonClickClip;

    [Label("버튼 클릭 볼륨")]
    [Range(0f, 1f)]
    public float buttonClickVolume = 1f;

    [Label("버튼 하이라이트음")]
    [Tooltip("누를 수 있는 Button/Toggle에 마우스를 올리거나 키보드로 선택하면 재생합니다. 비워두면 무음입니다.")]
    public AudioClip buttonHighlightClip;
    [Label("버튼 하이라이트 볼륨"), Range(0f, 1f)]
    public float buttonHighlightVolume = 0.5f;
    [Label("하이라이트 재생 간격(초)"), Min(0f)]
    public float highlightInterval = 0.06f;

    [Label("잠긴 버튼 클릭음")]
    [Tooltip("누를 수 없는(Interactable 꺼짐) 버튼을 클릭했을 때 재생합니다. 비워두면 소리를 내지 않습니다.")]
    public AudioClip buttonDisabledClip;

    [Label("잠긴 버튼 클릭 볼륨")]
    [Range(0f, 1f)]
    public float buttonDisabledVolume = 1f;

    [Header("명령")]
    [Label("명령 모드 진입음")]
    [Tooltip("이동/공격/정찰/집결지 명령 모드에 들어갈 때(단축키 포함) 재생합니다. 버튼으로 들어가면 겹침 방지로 클릭음만 납니다.")]
    public AudioClip commandModeEnterClip;

    [Label("명령 모드 진입 볼륨")]
    [Range(0f, 1f)]
    public float commandModeEnterVolume = 1f;

    [Label("기본 명령 확정음")]
    [Tooltip("명령을 확정했을 때 재생합니다. 아래 종류별 확정음이 비어 있으면 이 소리를 씁니다.")]
    public AudioClip commandConfirmClip;

    [Label("명령 확정 볼륨")]
    [Range(0f, 1f)]
    public float commandConfirmVolume = 1f;

    [Label("이동 확정음")]
    [Tooltip("이동 명령을 확정했을 때 재생합니다. 비워두면 기본 명령 확정음을 씁니다.")]
    public AudioClip moveConfirmClip;

    [Label("공격 확정음")]
    [Tooltip("공격·공격 이동 명령을 확정했을 때 재생합니다. 비워두면 기본 명령 확정음을 씁니다.")]
    public AudioClip attackConfirmClip;

    [Label("정찰 확정음")]
    [Tooltip("정찰 명령을 확정했을 때 재생합니다. 비워두면 기본 명령 확정음을 씁니다.")]
    public AudioClip patrolConfirmClip;

    [Label("집결지 확정음")]
    [Tooltip("집결지 지정을 확정했을 때 재생합니다. 비워두면 기본 명령 확정음을 씁니다.")]
    public AudioClip rallyConfirmClip;

    [Label("정지/홀드 확정음")]
    [Tooltip("정지·홀드 명령을 내렸을 때 재생합니다. 비워두면 기본 명령 확정음을 씁니다.")]
    public AudioClip stopConfirmClip;

    [Label("명령 취소음")]
    [Tooltip("명령 모드나 건물 배치를 우클릭/Esc로 취소했을 때 재생합니다. 비워두면 소리를 내지 않습니다.")]
    public AudioClip commandCancelClip;

    [Label("명령 취소 볼륨")]
    [Range(0f, 1f)]
    public float commandCancelVolume = 1f;

    AudioSource source;
    float lastPlayTime = float.NegativeInfinity;

    // 마우스를 누른 순간 아래에 있던 클릭 대상. 같은 대상 위에서 뗐을 때만 클릭으로 본다(Button과 같은 규칙).
    GameObject pressedClickHandler;
    PointerEventData pointerData;
    EventSystem pointerDataOwner;
    readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    Selectable hoveredButton;
    GameObject selectedObject;
    float lastHighlightTime = float.NegativeInfinity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        EnsureInstance();
    }

    public static UISoundManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        UISoundManager existing = FindObjectOfType<UISoundManager>();

        if (existing != null)
        {
            existing.Initialize();
            return existing;
        }

        UISoundManager prefab = Resources.Load<UISoundManager>(ResourcePath);

        if (prefab != null)
            return Instantiate(prefab);

        GameObject managerObject = new GameObject("UISoundManager");
        return managerObject.AddComponent<UISoundManager>();
    }

    void Awake()
    {
        Initialize();
    }

    void Initialize()
    {
        if (Instance == this)
            return;

        // 같은 오브젝트의 다른 컴포넌트가 날아가지 않도록 컴포넌트만 지운다.
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad는 루트 오브젝트에만 동작한다.
        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.outputAudioMixerGroup = outputMixerGroup;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        DetectButtonClick();
    }

    void LateUpdate()
    {
        if (Instance != this) return;
        GameObject handler = FindClickHandlerUnderPointer();
        Selectable hovered = handler != null ? handler.GetComponent<Selectable>() : null;
        if (!CanHighlight(hovered)) hovered = null;
        bool entered = hovered != hoveredButton;
        hoveredButton = hovered;
        if (entered && hovered != null && !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
            PlayButtonHighlight(hovered);

        GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (current == selectedObject) return;
        selectedObject = current;
        Selectable selected = current != null ? current.GetComponent<Selectable>() : null;
        // 마우스 클릭으로 선택된 버튼은 클릭음만 내고, 탐색으로 바뀐 선택에 하이라이트음을 낸다.
        if (selected != hovered && !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0) && CanHighlight(selected))
            PlayButtonHighlight(selected);
    }

    void OnDisable()
    {
        hoveredButton = null;
        selectedObject = null;
    }

    static bool CanHighlight(Selectable target)
    {
        return (target is Button || target is Toggle) && target.IsActive() && target.IsInteractable();
    }

    void PlayButtonHighlight(Selectable target)
    {
        UIButtonSound custom = target.GetComponent<UIButtonSound>();
        if (custom != null && (custom.mute || custom.muteHighlight)) return;
        AudioClip clip = custom != null && custom.highlightClip != null ? custom.highlightClip : buttonHighlightClip;
        float volume = buttonHighlightVolume * masterVolume * (custom != null ? custom.volumeScale : 1f);
        if (clip == null || source == null || volume <= 0f) return;
        if (Time.unscaledTime - lastHighlightTime < highlightInterval) return;
        lastHighlightTime = Time.unscaledTime;
        // 클릭·명령음의 겹침 방지 시각을 갱신하지 않아, 바로 클릭해도 클릭음이 누락되지 않는다.
        source.outputAudioMixerGroup = outputMixerGroup;
        source.pitch = 1f + Random.Range(-pitchRandomRange, pitchRandomRange);
        source.PlayOneShot(clip, volume);
    }

    // ───────── 버튼 클릭 감지 ─────────

    void DetectButtonClick()
    {
        if (Input.GetMouseButtonDown(0))
            pressedClickHandler = FindClickHandlerUnderPointer();

        if (!Input.GetMouseButtonUp(0))
            return;

        GameObject pressed = pressedClickHandler;
        pressedClickHandler = null;

        if (pressed == null || FindClickHandlerUnderPointer() != pressed)
            return;

        Selectable selectable = pressed.GetComponent<Selectable>();

        // 소리를 낼 대상은 버튼류(Button/Toggle)만이다.
        if (!(selectable is Button) && !(selectable is Toggle))
            return;

        PlayButtonClick(selectable);
    }

    GameObject FindClickHandlerUnderPointer()
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
            return null;

        // 씬이 바뀌면 EventSystem도 바뀌므로 그때 새로 만든다.
        if (pointerData == null || pointerDataOwner != eventSystem)
        {
            pointerData = new PointerEventData(eventSystem);
            pointerDataOwner = eventSystem;
        }

        pointerData.position = Input.mousePosition;
        raycastResults.Clear();
        eventSystem.RaycastAll(pointerData, raycastResults);

        if (raycastResults.Count == 0)
            return null;

        // EventSystem과 같이 가장 위의 UI를 기준으로, 클릭을 받는 부모까지 올라간다.
        return ExecuteEvents.GetEventHandler<IPointerClickHandler>(raycastResults[0].gameObject);
    }

    void PlayButtonClick(Selectable selectable)
    {
        bool interactable = selectable.IsInteractable();
        UIButtonSound custom = selectable.GetComponent<UIButtonSound>();

        AudioClip clip = interactable ? buttonClickClip : buttonDisabledClip;
        float volume = interactable ? buttonClickVolume : buttonDisabledVolume;

        if (custom != null)
        {
            if (custom.mute)
                return;

            AudioClip overrideClip = interactable ? custom.clickClip : custom.disabledClip;

            if (overrideClip != null)
                clip = overrideClip;

            volume *= custom.volumeScale;
        }

        PlayInternal(clip, volume);
    }

    // ───────── 명령 ─────────

    /// <summary>명령 모드(이동/공격/정찰/집결지)에 들어갈 때 호출합니다.</summary>
    public static void PlayCommandModeEnter()
    {
        UISoundManager manager = EnsureInstance();
        manager.PlayInternal(manager.commandModeEnterClip, manager.commandModeEnterVolume);
    }

    /// <summary>명령이 실제로 전달됐을 때 호출합니다.</summary>
    public static void PlayCommandConfirm(UICommandSound kind)
    {
        UISoundManager manager = EnsureInstance();
        AudioClip clip = manager.GetConfirmClip(kind);

        if (clip == null)
            clip = manager.commandConfirmClip;

        manager.PlayInternal(clip, manager.commandConfirmVolume);
    }

    public static void PlayCommandConfirm(UnitCommandMode mode)
    {
        switch (mode)
        {
            case UnitCommandMode.Attack:
                PlayCommandConfirm(UICommandSound.Attack);
                break;

            case UnitCommandMode.Patrol:
                PlayCommandConfirm(UICommandSound.Patrol);
                break;

            case UnitCommandMode.RallyPoint:
                PlayCommandConfirm(UICommandSound.RallyPoint);
                break;

            default:
                PlayCommandConfirm(UICommandSound.Move);
                break;
        }
    }

    /// <summary>명령 모드나 건물 배치를 플레이어가 직접 취소했을 때 호출합니다.</summary>
    public static void PlayCommandCancel()
    {
        UISoundManager manager = EnsureInstance();
        manager.PlayInternal(manager.commandCancelClip, manager.commandCancelVolume);
    }

    AudioClip GetConfirmClip(UICommandSound kind)
    {
        switch (kind)
        {
            case UICommandSound.Move: return moveConfirmClip;
            case UICommandSound.Attack: return attackConfirmClip;
            case UICommandSound.Patrol: return patrolConfirmClip;
            case UICommandSound.RallyPoint: return rallyConfirmClip;
            case UICommandSound.Stop: return stopConfirmClip;
            default: return null;
        }
    }

    // ───────── 재생 ─────────

    void PlayInternal(AudioClip clip, float volume)
    {
        if (clip == null || source == null)
            return;

        float effectiveVolume = volume * masterVolume;

        if (effectiveVolume <= 0f)
            return;

        // 결과 창 등 일시정지(timeScale 0) 중에도 동작하도록 unscaledTime을 쓴다.
        float now = Time.unscaledTime;

        if (now - lastPlayTime < overlapBlockSeconds)
            return;

        lastPlayTime = now;
        source.outputAudioMixerGroup = outputMixerGroup;
        source.pitch = 1f + Random.Range(-pitchRandomRange, pitchRandomRange);
        source.PlayOneShot(clip, effectiveVolume);
    }
}
