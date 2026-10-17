using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum UnitSoundAttenuation
{
    [Tooltip("거리·줌과 무관하게 항상 같은 볼륨입니다.")]
    None,

    [Tooltip("Unity 3D 공간음향(min/max Distance)을 사용합니다.")]
    Unity3D,

    [Tooltip("맵 XZ 거리 + 직교 줌에 따라 볼륨을 조절합니다. RTS 카메라 권장.")]
    RtsCamera
}

// 클립 하나와 그 클립만의 볼륨입니다.
[System.Serializable]
public class UnitSoundClip
{
    [Label("클립")]
    public AudioClip clip;

    [Label("볼륨")]
    [Tooltip("이 클립만의 볼륨 배율입니다. 전체 '볼륨'과 곱해집니다. 원본이 작은 클립은 1보다 크게 올릴 수 있습니다.")]
    [Range(0f, 2f)]
    public float volume = 1f;

    // 인스펙터에서 배열에 새로 추가한 칸은 볼륨이 0으로 들어오므로, 처음 한 번만 1로 맞춘다.
    [SerializeField, HideInInspector]
    internal bool initialized = true;

    public UnitSoundClip() { }

    public UnitSoundClip(AudioClip clip, float volume = 1f)
    {
        this.clip = clip;
        this.volume = volume;
    }
}

// 플레이어 조작에 반응하는 음성·알림 사운드 종류입니다.
public enum UnitSoundVoice
{
    Select,
    ProductionComplete,
    Command
}

[DisallowMultipleComponent]
public class UnitSound : MonoBehaviour, ISerializationCallbackReceiver
{
    // 클립 배열 제목(한글)과 끌어다 놓기 추가는 Editor/UnitSoundEditor.cs가 그린다.
    [Header("클립")]
    [Tooltip("공격 시 재생할 사운드입니다. 여러 개면 무작위로 선택합니다. 클립마다 볼륨을 따로 정할 수 있습니다.")]
    public UnitSoundClip[] attackSounds;

    [Tooltip("이 유닛의 공격이 대상에 맞았을 때 재생할 사운드입니다. 히트 이펙트와 같은 시점에 납니다. 클립마다 볼륨을 따로 정할 수 있습니다.")]
    public UnitSoundClip[] attackHitSounds;

    [Tooltip("사망·파괴 시 재생할 사운드입니다. 클립마다 볼륨을 따로 정할 수 있습니다.")]
    public UnitSoundClip[] deathSounds;

    [Header("공격 루프 (화염방사기 등)")]
    [Label("공격 루프 사운드")]
    [Tooltip("공격이 이어지는 동안 반복 재생할 사운드입니다(화염방사기 화염 소리 등). 클립의 Loop 설정과 상관없이 반복합니다. " +
             "클립을 넣으면 위 '공격 사운드'는 재생하지 않습니다(아래 '일반 공격 사운드도 재생'으로 함께 낼 수 있음). 비워두면 쓰지 않습니다.")]
    public UnitSoundClip attackLoopSound = new UnitSoundClip();

    [Label("공격 끊김 판정 여유(초)")]
    [Tooltip("마지막 공격 뒤 '공격 쿨다운 + 이 시간' 안에 다음 공격이 없으면 공격이 끝난 것으로 보고 루프를 멈춥니다. " +
             "공격 사이에 소리가 잠깐씩 끊기면 늘리세요. 추격하느라 공격이 끊기면 이 시간과 상관없이 바로 멈춥니다.")]
    [Min(0f)]
    public float attackLoopHoldTime = 0.15f;

    [Label("루프 페이드인(초)")]
    [Tooltip("루프가 시작될 때 볼륨이 0에서 원래 크기까지 커지는 시간입니다.")]
    [Min(0f)]
    public float attackLoopFadeIn = 0.05f;

    [Label("루프 페이드아웃(초)")]
    [Tooltip("공격이 끝났을 때 루프 볼륨이 0까지 줄어드는 시간입니다. 화염이 자연스럽게 잦아들게 합니다.")]
    [Min(0f)]
    public float attackLoopFadeOut = 0.25f;

    [Label("일반 공격 사운드도 재생")]
    [Tooltip("켜면 루프와 함께 공격할 때마다 위 '공격 사운드'도 재생합니다. 발사음·점화음을 루프 위에 얹을 때 씁니다.")]
    public bool attackLoopWithOneShots;

    [Header("음성·알림")]
    [Tooltip("플레이어가 이 유닛을 선택했을 때 재생할 사운드입니다. 여러 유닛을 함께 선택하면 그중 한 유닛의 소리만 납니다.")]
    public UnitSoundClip[] selectSounds;

    [Tooltip("이 유닛이 생산을 마치고 나왔을 때 재생할 사운드입니다. 쿨타임과 상관없이 생산될 때마다 항상 재생합니다.")]
    public UnitSoundClip[] productionCompleteSounds;

    [Tooltip("명령 버튼이나 단축키로 이 유닛에게 명령(이동·공격·공격 이동·정찰·정지·홀드)을 내렸을 때 재생할 사운드입니다. 우클릭으로 내리는 기본 이동·공격에서는 나지 않습니다. 여러 유닛에게 함께 내리면 그중 한 유닛의 소리만 납니다.")]
    [FormerlySerializedAs("attackOrderSounds")]
    public UnitSoundClip[] commandSounds;

    [Label("적 발견 시 선택 사운드")]
    [Tooltip("켜면 플레이어 유닛이 어그로 범위에서 적을 발견해 교전을 시작할 때 선택 사운드를 재생합니다. 이미 싸우던 중 다음 표적으로 넘어갈 때는 나지 않으며, 선택·명령 사운드 쿨타임을 함께 씁니다.\n" +
        "적 유닛에서 켜면, 그 적이 플레이어 쪽을 발견했을 때(발각) 선택 사운드를 재생합니다. 아래 '발각 사운드' 설정을 따릅니다.")]
    public bool playSelectSoundOnAggro = true;

    [Header("발각 사운드 (적 유닛)")]
    [Label("발각 사운드 쿨타임(초)")]
    [Tooltip("적 유닛의 발각 사운드가 한 번 나면, 이 시간(초) 동안은 다른 적의 발각 사운드도 나지 않습니다. 모든 적이 함께 쓰며, 플레이어의 선택·명령 사운드 쿨타임과는 따로입니다.")]
    [Min(0f)]
    public float detectedSoundCooldown = 2f;

    [Label("다시 발각으로 볼 시간(초)")]
    [Tooltip("이 적이 어그로 범위 안에 플레이어 쪽 표적 없이 이 시간(초) 이상 지낸 뒤 다시 잡아야 발각으로 칩니다. 싸우는 도중 표적이 바뀔 때마다 소리가 나지 않게 합니다.")]
    [Min(0f)]
    public float detectedRearmTime = 5f;

    [Label("시야 안에서만 재생")]
    [Tooltip("켜면 안개 시야 안에 보이는 적만 발각 사운드를 냅니다. 끄면 보이지 않는 곳의 적도 소리를 냅니다(거리 감쇠는 적용).")]
    public bool detectedRequiresVisibility = true;

    [Label("거리 감쇠 무시")]
    [Tooltip("켜면 음성·알림 사운드는 카메라와의 거리·줌과 상관없이 '볼륨' 그대로 재생합니다. 플레이어에게 바로 들려야 하는 소리라 켜 두기를 권장합니다.")]
    public bool voiceIgnoreAttenuation = true;

    [Label("선택·명령 사운드 쿨타임(초)")]
    [Tooltip("선택 사운드나 명령 사운드가 한 번 나면, 이 시간(초) 동안은 선택·명령 사운드가 둘 다 나지 않습니다. 모든 유닛이 함께 씁니다. 생산 완료 사운드는 이 쿨타임과 상관없이 항상 납니다.")]
    [Min(0f)]
    [FormerlySerializedAs("selectSoundCooldown")]
    public float voiceCooldown = 1f;

    // 예전 형식(클립만 있던 배열). 불러올 때 위 배열로 옮기고 비운다.
    [SerializeField, HideInInspector, FormerlySerializedAs("attackClips")]
    AudioClip[] legacyAttackClips;

    [SerializeField, HideInInspector, FormerlySerializedAs("attackHitClips")]
    AudioClip[] legacyAttackHitClips;

    [SerializeField, HideInInspector, FormerlySerializedAs("deathClips")]
    AudioClip[] legacyDeathClips;

    [Header("재생")]
    [Label("오디오 소스")]
    [Tooltip("비워두면 이 오브젝트에 AudioSource를 자동으로 추가합니다.")]
    public AudioSource audioSource;

    [Label("볼륨")]
    [Tooltip("전체 재생 볼륨입니다. 클립별 볼륨과 곱해집니다.")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Label("감쇠 방식")]
    [Tooltip("음량 감쇠 방식입니다.")]
    public UnitSoundAttenuation attenuation = UnitSoundAttenuation.RtsCamera;

    [Label("최소 거리")]
    [Tooltip("3D 공간음향(Unity3D) 또는 RTS 맵 거리 감쇠의 최대 볼륨 거리입니다.")]
    public float minDistance = 8f;

    [Label("최대 거리")]
    [Tooltip("3D 공간음향(Unity3D) 또는 RTS 맵 거리 감쇠의 무음 거리입니다.")]
    public float maxDistance = 120f;

    [Label("줌에 따른 볼륨")]
    [Tooltip("직교 줌에 따른 볼륨 조절을 사용합니다. RtsCamera 모드에서만 적용됩니다.")]
    public bool attenuateByZoom = true;

    [Label("확대 시 볼륨 배율")]
    [Tooltip("가장 확대(orthographicSize 최소)일 때의 볼륨 배율입니다.")]
    [Range(0f, 1f)]
    public float zoomedInVolume = 1f;

    [Label("축소 시 볼륨 배율")]
    [Tooltip("가장 축소(orthographicSize 최대)일 때의 볼륨 배율입니다.")]
    [Range(0f, 1f)]
    public float zoomedOutVolume = 0.35f;

    [Label("기준 최소 줌 크기")]
    [Tooltip("줌 계산에 쓰는 orthographicSize 최소값입니다. RTSCameraPivotController와 맞추세요.")]
    public float referenceMinOrthoSize = 12f;

    [Label("기준 최대 줌 크기")]
    [Tooltip("줌 계산에 쓰는 orthographicSize 최대값입니다. RTSCameraPivotController와 맞추세요.")]
    public float referenceMaxOrthoSize = 45f;

    [Label("피치 범위")]
    [Tooltip("재생 시 피치 무작위 범위입니다.")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Label("공격 사운드 최대 겹침 수")]
    [Tooltip("공격 사운드를 동시에 몇 개까지 겹쳐 재생할지입니다. 겹친 소리는 서로 끊지 않고 각자 끝까지 재생되므로, " +
             "화염방사기처럼 긴 소리를 연달아 틀면 이어지게 들립니다. 모두 재생 중일 때 들어온 공격은 소리를 생략합니다. " +
             "끊김 없이 이어지려면 '클립 길이 ÷ 공격 간격'보다 크게 잡으세요. 0이면 예전처럼 제한 없이 겹칩니다(동시 재생 한도를 넘으면 앞 소리가 끊길 수 있음).")]
    [Min(0)]
    public int attackSoundMaxOverlap = 3;

    [Label("공격 사운드 우선순위")]
    [Tooltip("동시 재생 한도(Project Settings > Audio > Max Real Voices)를 넘었을 때 어떤 소리를 먼저 끊을지 정합니다. " +
             "0이 가장 중요하고 256이 가장 덜 중요합니다. 낮출수록 공격 사운드가 다른 소리보다 늦게 끊깁니다. '최대 겹침 수'가 1 이상일 때만 적용됩니다.")]
    [Range(0, 256)]
    public int attackSoundPriority = 64;

    [Label("명중 사운드 간격(초)")]
    [Tooltip("명중 사운드 최소 간격(초)입니다. 한 번의 공격이 여러 대상을 맞히거나 " +
             "연사가 빠를 때 소리가 겹치는 것을 줄입니다.")]
    public float attackHitSoundCooldown = 0.12f;

    EntityHealth health;
    readonly List<AudioSource> attackAudioSources = new List<AudioSource>();

    // 공격 루프 상태. attackLoopFade는 페이드 진행도(0~1)로, 실제 볼륨에 곱한다.
    AudioSource attackLoopSource;
    UnitAttacker cachedAttacker;
    float attackLoopLastTime = float.NegativeInfinity;
    float attackLoopFade;
    bool attackLoopActive;
    bool attackLoopPaused;
    float lastAttackHitTime;
    bool isDead;

    // 임시 재생 오브젝트를 클립 길이보다 조금 더 남겨 끝부분이 잘리지 않게 한다.
    const float DetachedSoundTailTime = 0.1f;
    const float MinPitchForLength = 0.01f;

    static AudioListener cachedListener;

    // 음성·알림 사운드는 유닛이 달라도 서로 겹치지 않게 마지막 재생 시각을 함께 쓴다.
    static float lastVoiceTime = float.NegativeInfinity;

    // 적 발각 사운드는 플레이어 음성과 쿨타임을 따로 쓴다.
    static float lastDetectedTime = float.NegativeInfinity;

    void Awake()
    {
        health = GetComponent<EntityHealth>();
        EnsureAudioSource();
    }

    void Start()
    {
        ApplyAudioSourceSettings();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (maxDistance < minDistance)
            maxDistance = minDistance;

        InitializeAllNewEntries();
        ApplyAudioSourceSettings();
    }
#endif

    // 모든 사운드 배열의 새 칸(볼륨 0으로 들어온 칸)을 볼륨 1로 맞춘다.
    // 배열을 새로 추가하면 반드시 여기에도 넣는다.
    void InitializeAllNewEntries()
    {
        InitializeNewEntries(attackSounds);
        InitializeNewEntries(attackHitSounds);
        InitializeNewEntries(deathSounds);
        InitializeNewEntries(selectSounds);
        InitializeNewEntries(productionCompleteSounds);
        InitializeNewEntries(commandSounds);
    }

    static void InitializeNewEntries(UnitSoundClip[] sounds)
    {
        if (sounds == null)
            return;

        foreach (UnitSoundClip sound in sounds)
        {
            if (sound == null || sound.initialized)
                continue;

            sound.volume = 1f;
            sound.initialized = true;
        }
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        MigrateLegacy(ref legacyAttackClips, ref attackSounds);
        MigrateLegacy(ref legacyAttackHitClips, ref attackHitSounds);
        MigrateLegacy(ref legacyDeathClips, ref deathSounds);

        // 이미 볼륨 0으로 저장돼 버린 새 칸도 불러올 때 바로잡는다(에디터·빌드 공통).
        InitializeAllNewEntries();
    }

    static void MigrateLegacy(ref AudioClip[] legacy, ref UnitSoundClip[] sounds)
    {
        if (legacy == null || legacy.Length == 0)
            return;

        if (sounds == null || sounds.Length == 0)
            sounds = ToSoundClips(legacy, null);

        legacy = null;
    }

    // 같은 클립이 이미 있으면 그 볼륨을 이어받는다(UnitData가 클립을 다시 넣어도 볼륨 유지).
    static UnitSoundClip[] ToSoundClips(AudioClip[] clips, UnitSoundClip[] existing)
    {
        UnitSoundClip[] result = new UnitSoundClip[clips.Length];

        for (int i = 0; i < clips.Length; i++)
        {
            float clipVolume = 1f;

            if (existing != null)
            {
                foreach (UnitSoundClip sound in existing)
                {
                    if (sound != null && sound.clip == clips[i])
                    {
                        clipVolume = sound.volume;
                        break;
                    }
                }
            }

            result[i] = new UnitSoundClip(clips[i], clipVolume);
        }

        return result;
    }

    void OnEnable()
    {
        if (health == null)
            health = GetComponent<EntityHealth>();

        if (health == null)
            return;

        health.OnDied += HandleDied;
    }

    void OnDisable()
    {
        ResetAttackLoop();

        if (health == null)
            return;

        health.OnDied -= HandleDied;
    }

    void EnsureAudioSource()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        ApplyAudioSourceSettings();
    }

    void ApplyAudioSourceSettings()
    {
        ApplyAudioSourceSettings(audioSource);
        ApplyAudioSourceSettings(attackLoopSource);

        foreach (AudioSource source in attackAudioSources)
        {
            ApplyAudioSourceSettings(source);

            if (source != null)
                source.priority = attackSoundPriority;
        }
    }

    void ApplyAudioSourceSettings(AudioSource source)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.spatialBlend = attenuation == UnitSoundAttenuation.Unity3D ? 1f : 0f;
    }

    // 쉬고 있는 공격 사운드 전용 소스를 찾는다. 없으면 최대 겹침 수까지 새로 만들고,
    // 모두 재생 중이면 null을 돌려준다(재생 중인 소리를 끊지 않는다).
    // 출력 설정은 기본 오디오 소스를 따른다.
    AudioSource GetIdleAttackAudioSource()
    {
        foreach (AudioSource existing in attackAudioSources)
        {
            if (existing != null && !existing.isPlaying)
                return existing;
        }

        if (audioSource == null || attackAudioSources.Count >= attackSoundMaxOverlap)
            return null;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        source.dopplerLevel = audioSource.dopplerLevel;
        source.spread = audioSource.spread;
        source.priority = attackSoundPriority;
        ApplyAudioSourceSettings(source);
        attackAudioSources.Add(source);
        return source;
    }

    public void PlayAttack()
    {
        if (isDead)
            return;

        if (HasAttackLoop)
        {
            KeepAttackLoop();

            if (!attackLoopWithOneShots)
                return;
        }

        if (attackSoundMaxOverlap <= 0)
        {
            PlayRandom(attackSounds);
            return;
        }

        PlayAttackOverlapped();
    }

    // 쉬는 전용 소스에서 재생해, 앞서 틀던 공격 사운드와 겹치되 서로 끊지 않게 한다.
    void PlayAttackOverlapped()
    {
        AudioSource source = GetIdleAttackAudioSource();

        if (source == null)
            return;

        if (!TryPickSound(attackSounds, out UnitSoundClip sound, out float playVolume))
            return;

        source.clip = sound.clip;
        source.volume = playVolume;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.Play();
    }

    // ───────── 공격 루프 ─────────

    bool HasAttackLoop => attackLoopSound != null && attackLoopSound.clip != null;

    /// <summary>
    /// 공격이 끊겼을 때(추격 시작 등) 루프를 페이드아웃시킵니다. 루프를 쓰지 않으면 아무 일도 하지 않습니다.
    /// </summary>
    public void StopAttackLoop()
    {
        attackLoopActive = false;
    }

    // 공격할 때마다 불러 루프를 시작하거나 이어간다.
    void KeepAttackLoop()
    {
        AudioSource source = EnsureAttackLoopSource();

        if (source == null || !audioSource.enabled || !gameObject.activeInHierarchy)
            return;

        attackLoopLastTime = Time.time;
        attackLoopActive = true;

        if (source.isPlaying && source.clip == attackLoopSound.clip)
            return;

        // 페이드아웃 중에 다시 공격하면 그 볼륨에서 이어서 커지도록 진행도는 유지한다.
        source.clip = attackLoopSound.clip;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.volume = 0f;
        source.Play();
        attackLoopPaused = false;
    }

    AudioSource EnsureAttackLoopSource()
    {
        if (attackLoopSource != null)
            return attackLoopSource;

        if (audioSource == null)
            return null;

        attackLoopSource = gameObject.AddComponent<AudioSource>();
        attackLoopSource.loop = true;
        attackLoopSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        attackLoopSource.dopplerLevel = audioSource.dopplerLevel;
        attackLoopSource.spread = audioSource.spread;
        attackLoopSource.priority = attackSoundPriority;
        ApplyAudioSourceSettings(attackLoopSource);
        return attackLoopSource;
    }

    void Update()
    {
        if (attackLoopSource != null)
            UpdateAttackLoop();
    }

    void UpdateAttackLoop()
    {
        // 결과 창 등 일시정지(timeScale 0) 중에는 루프도 멈췄다가 재개하면 이어서 튼다.
        if (Time.timeScale <= 0f)
        {
            if (attackLoopSource.isPlaying)
            {
                attackLoopSource.Pause();
                attackLoopPaused = true;
            }

            return;
        }

        if (attackLoopPaused)
        {
            attackLoopSource.UnPause();
            attackLoopPaused = false;
        }

        if (!attackLoopSource.isPlaying)
            return;

        if (attackLoopActive && Time.time > attackLoopLastTime + GetAttackLoopHoldTime())
            attackLoopActive = false;

        float targetFade = attackLoopActive ? 1f : 0f;
        float fadeDuration = attackLoopActive ? attackLoopFadeIn : attackLoopFadeOut;

        attackLoopFade = fadeDuration <= 0f
            ? targetFade
            : Mathf.MoveTowards(attackLoopFade, targetFade, Time.deltaTime / fadeDuration);

        if (!attackLoopActive && attackLoopFade <= 0f)
        {
            attackLoopSource.Stop();
            return;
        }

        // 카메라가 움직이면 거리·줌 감쇠가 바뀌므로 매 프레임 볼륨을 다시 계산한다.
        float loopVolume = HasAttackLoop ? attackLoopSound.volume : 0f;
        attackLoopSource.volume = GetEffectiveVolume() * loopVolume * attackLoopFade;
    }

    // 공격 쿨다운보다 짧으면 공격 사이마다 루프가 끊기므로 쿨다운을 기준으로 여유를 더한다.
    float GetAttackLoopHoldTime()
    {
        if (cachedAttacker == null)
            cachedAttacker = GetComponent<UnitAttacker>();

        float cooldown = cachedAttacker != null ? cachedAttacker.EffectiveAttackCooldown : 0f;
        return cooldown + attackLoopHoldTime;
    }

    void ResetAttackLoop()
    {
        attackLoopActive = false;
        attackLoopPaused = false;
        attackLoopFade = 0f;

        if (attackLoopSource != null)
            attackLoopSource.Stop();
    }

    /// <summary>
    /// 이 유닛의 공격이 대상에 맞았을 때 재생합니다. 히트 이펙트를 띄우는 시점과 같습니다.
    /// 한 번의 공격이 여러 대상을 맞혀도 소리가 겹치지 않게 최소 간격을 둡니다.
    /// </summary>
    public void PlayAttackHit()
    {
        if (isDead)
            return;

        if (Time.time < lastAttackHitTime + attackHitSoundCooldown)
            return;

        lastAttackHitTime = Time.time;
        PlayRandom(attackHitSounds);
    }

    /// <summary>
    /// 공격자의 명중 사운드를 재생합니다.
    /// 투사체처럼 공격자에게서 떨어진 곳에서 착탄을 처리할 때 씁니다.
    /// </summary>
    public static void PlayAttackHit(SelectableEntity attacker)
    {
        if (attacker == null)
            return;

        UnitSound sound = attacker.GetComponent<UnitSound>();

        if (sound != null)
            sound.PlayAttackHit();
    }

    public void PlayDeath()
    {
        if (isDead)
            return;

        isDead = true;

        // 사망하면 EntityHealth가 같은 프레임에 오브젝트를 Destroy하므로, 이 오브젝트의
        // AudioSource로 틀면 소리가 바로 끊긴다. 따로 떨어진 임시 오브젝트에서 재생한다.
        PlayRandom(deathSounds, detached: true);
    }

    /// <summary>
    /// 음성·알림 사운드를 재생합니다. 클립이 없거나 최소 간격 안이면 재생하지 않습니다.
    /// </summary>
    /// <returns>실제로 재생했으면 true.</returns>
    public bool PlayVoice(UnitSoundVoice voice)
    {
        if (isDead || audioSource == null)
            return false;

        UnitSoundClip[] sounds = GetVoiceSounds(voice);

        if (sounds == null || sounds.Length == 0)
            return false;

        // 생산 완료는 알림이라 항상 낸다. 선택·명령만 쿨타임을 함께 쓴다.
        bool usesCooldown = voice != UnitSoundVoice.ProductionComplete;

        if (usesCooldown && Time.unscaledTime < lastVoiceTime + voiceCooldown)
            return false;

        UnitSoundClip sound = sounds[Random.Range(0, sounds.Length)];

        if (sound == null || sound.clip == null || sound.volume <= 0f)
            return false;

        if (!audioSource.enabled || !gameObject.activeInHierarchy)
            return false;

        float playVolume = (voiceIgnoreAttenuation ? volume : GetEffectiveVolume()) * sound.volume;

        if (playVolume <= 0.001f)
            return false;

        if (usesCooldown)
            lastVoiceTime = Time.unscaledTime;

        audioSource.PlayOneShot(sound.clip, playVolume);
        return true;
    }

    /// <summary>
    /// 적 유닛이 플레이어 쪽을 발견했을 때 선택 사운드를 재생합니다("발각").
    /// 플레이어에게 들려주는 알림이 아니라 그 자리에서 나는 소리라 거리 감쇠를 항상 적용합니다.
    /// </summary>
    public bool PlayDetectedVoice()
    {
        if (!playSelectSoundOnAggro || isDead || audioSource == null)
            return false;

        if (selectSounds == null || selectSounds.Length == 0)
            return false;

        if (Time.unscaledTime < lastDetectedTime + detectedSoundCooldown)
            return false;

        if (detectedRequiresVisibility &&
            FogOfWarManager.Instance != null &&
            !FogOfWarManager.Instance.IsVisible(transform.position))
        {
            return false;
        }

        UnitSoundClip sound = selectSounds[Random.Range(0, selectSounds.Length)];

        if (sound == null || sound.clip == null || sound.volume <= 0f)
            return false;

        if (!audioSource.enabled || !gameObject.activeInHierarchy)
            return false;

        float playVolume = GetEffectiveVolume() * sound.volume;

        if (playVolume <= 0.001f)
            return false;

        lastDetectedTime = Time.unscaledTime;
        audioSource.PlayOneShot(sound.clip, playVolume);
        return true;
    }

    public bool HasVoice(UnitSoundVoice voice)
    {
        UnitSoundClip[] sounds = GetVoiceSounds(voice);
        return sounds != null && sounds.Length > 0;
    }

    UnitSoundClip[] GetVoiceSounds(UnitSoundVoice voice)
    {
        switch (voice)
        {
            case UnitSoundVoice.Select:
                return selectSounds;
            case UnitSoundVoice.ProductionComplete:
                return productionCompleteSounds;
            case UnitSoundVoice.Command:
                return commandSounds;
            default:
                return null;
        }
    }

    /// <summary>
    /// 여러 유닛에게 한꺼번에 선택·명령이 갔을 때, 그 소리를 가진 첫 유닛 하나만 재생합니다.
    /// preferred가 있으면 그 유닛을 먼저 시도합니다(직접 클릭한 유닛 등).
    /// </summary>
    public static void PlayVoiceForGroup(
        IReadOnlyList<SelectableEntity> entities,
        UnitSoundVoice voice,
        SelectableEntity preferred = null)
    {
        if (TryPlayVoice(preferred, voice) || entities == null)
            return;

        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i] != preferred && TryPlayVoice(entities[i], voice))
                return;
        }
    }

    static bool TryPlayVoice(SelectableEntity entity, UnitSoundVoice voice)
    {
        if (entity == null)
            return false;

        UnitSound sound = entity.GetComponent<UnitSound>();
        return sound != null && sound.HasVoice(voice) && sound.PlayVoice(voice);
    }

    void HandleDied()
    {
        PlayDeath();
    }

    // 배열에서 무작위로 하나를 고르고 감쇠를 반영한 재생 볼륨을 구한다. 재생할 수 없으면 false.
    bool TryPickSound(UnitSoundClip[] sounds, out UnitSoundClip sound, out float playVolume)
    {
        sound = null;
        playVolume = 0f;

        if (sounds == null || sounds.Length == 0 || audioSource == null)
            return false;

        sound = sounds[Random.Range(0, sounds.Length)];

        if (sound == null || sound.clip == null || volume <= 0f || sound.volume <= 0f)
            return false;

        if (!audioSource.enabled || !gameObject.activeInHierarchy)
            return false;

        playVolume = GetEffectiveVolume() * sound.volume;
        return playVolume > 0.001f;
    }

    void PlayRandom(UnitSoundClip[] sounds, bool detached = false)
    {
        if (!TryPickSound(sounds, out UnitSoundClip sound, out float playVolume))
            return;

        float pitch = Random.Range(pitchRange.x, pitchRange.y);

        if (detached)
        {
            PlayDetached(sound.clip, playVolume, pitch);
            return;
        }

        float previousPitch = audioSource.pitch;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(sound.clip, playVolume);
        audioSource.pitch = previousPitch;
    }

    // 이 오브젝트가 사라져도 끝까지 재생되도록, 같은 설정의 AudioSource를 가진 임시
    // 오브젝트를 만들어 재생하고 클립 길이만큼 지난 뒤 지운다.
    void PlayDetached(AudioClip clip, float playVolume, float pitch)
    {
        GameObject soundObject = new GameObject("UnitSound_OneShot");
        soundObject.transform.position = transform.position;

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        source.spatialBlend = audioSource.spatialBlend;
        source.rolloffMode = audioSource.rolloffMode;
        source.minDistance = audioSource.minDistance;
        source.maxDistance = audioSource.maxDistance;
        source.dopplerLevel = audioSource.dopplerLevel;
        source.spread = audioSource.spread;
        source.priority = audioSource.priority;
        source.clip = clip;
        source.volume = playVolume;
        source.pitch = pitch;
        source.Play();

        Destroy(soundObject, clip.length / Mathf.Max(MinPitchForLength, Mathf.Abs(pitch)) + DetachedSoundTailTime);
    }

    float GetEffectiveVolume()
    {
        if (attenuation == UnitSoundAttenuation.None)
            return volume;

        if (attenuation == UnitSoundAttenuation.Unity3D)
            return volume;

        return volume * GetRtsDistanceScale() * GetZoomScale();
    }

    float GetRtsDistanceScale()
    {
        AudioListener listener = GetListener();

        if (listener == null)
            return 1f;

        Vector3 listenerPosition = listener.transform.position;
        Vector3 sourcePosition = transform.position;

        float distance = Vector2.Distance(
            new Vector2(sourcePosition.x, sourcePosition.z),
            new Vector2(listenerPosition.x, listenerPosition.z));

        if (distance <= minDistance)
            return 1f;

        if (distance >= maxDistance)
            return 0f;

        return 1f - Mathf.InverseLerp(minDistance, maxDistance, distance);
    }

    float GetZoomScale()
    {
        if (attenuation != UnitSoundAttenuation.RtsCamera || !attenuateByZoom)
            return 1f;

        Camera camera = Camera.main;

        if (camera == null || !camera.orthographic)
            return 1f;

        float zoomRatio = Mathf.InverseLerp(
            referenceMinOrthoSize,
            referenceMaxOrthoSize,
            camera.orthographicSize);

        return Mathf.Lerp(zoomedInVolume, zoomedOutVolume, zoomRatio);
    }

    static AudioListener GetListener()
    {
        if (cachedListener != null)
            return cachedListener;

        cachedListener = FindObjectOfType<AudioListener>();
        return cachedListener;
    }

    public void ApplyClips(
        AudioClip[] attack,
        AudioClip[] attackHit,
        AudioClip[] death,
        float newVolume = -1f)
    {
        if (attack != null && attack.Length > 0)
            attackSounds = ToSoundClips(attack, attackSounds);

        if (attackHit != null && attackHit.Length > 0)
            attackHitSounds = ToSoundClips(attackHit, attackHitSounds);

        if (death != null && death.Length > 0)
            deathSounds = ToSoundClips(death, deathSounds);

        if (newVolume >= 0f)
            volume = newVolume;
    }
}
