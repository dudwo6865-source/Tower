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
    AttackOrder
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

    [Header("음성·알림")]
    [Tooltip("플레이어가 이 유닛을 선택했을 때 재생할 사운드입니다. 여러 유닛을 함께 선택하면 그중 한 유닛의 소리만 납니다.")]
    public UnitSoundClip[] selectSounds;

    [Tooltip("이 유닛이 생산을 마치고 나왔을 때 재생할 사운드입니다.")]
    public UnitSoundClip[] productionCompleteSounds;

    [Tooltip("이 유닛에게 공격 명령(대상 공격·공격 이동)을 내렸을 때 재생할 사운드입니다. 여러 유닛에게 함께 내리면 그중 한 유닛의 소리만 납니다.")]
    public UnitSoundClip[] attackOrderSounds;

    [Label("거리 감쇠 무시")]
    [Tooltip("켜면 음성·알림 사운드는 카메라와의 거리·줌과 상관없이 '볼륨' 그대로 재생합니다. 플레이어에게 바로 들려야 하는 소리라 켜 두기를 권장합니다.")]
    public bool voiceIgnoreAttenuation = true;

    [Label("최소 간격(초)")]
    [Tooltip("음성·알림 사운드끼리의 최소 간격(초)입니다. 모든 유닛이 함께 씁니다. 선택·명령을 빠르게 반복할 때 소리가 겹쳐 쌓이는 것을 막습니다.")]
    [Min(0f)]
    public float voiceMinInterval = 0.15f;

    [Label("선택 사운드 쿨타임(초)")]
    [Tooltip("선택 사운드를 한 번 낸 뒤 이 시간(초) 동안은 다시 선택해도 선택 사운드를 내지 않습니다. 모든 유닛이 함께 씁니다. 0이면 '최소 간격'만 적용됩니다.")]
    [Min(0f)]
    public float selectSoundCooldown = 1f;

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

    [Label("명중 사운드 간격(초)")]
    [Tooltip("명중 사운드 최소 간격(초)입니다. 한 번의 공격이 여러 대상을 맞히거나 " +
             "연사가 빠를 때 소리가 겹치는 것을 줄입니다.")]
    public float attackHitSoundCooldown = 0.12f;

    EntityHealth health;
    float lastAttackHitTime;
    bool isDead;

    // 임시 재생 오브젝트를 클립 길이보다 조금 더 남겨 끝부분이 잘리지 않게 한다.
    const float DetachedSoundTailTime = 0.1f;
    const float MinPitchForLength = 0.01f;

    static AudioListener cachedListener;

    // 음성·알림 사운드는 유닛이 달라도 서로 겹치지 않게 마지막 재생 시각을 함께 쓴다.
    static float lastVoiceTime = float.NegativeInfinity;
    static float lastSelectVoiceTime = float.NegativeInfinity;

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
        InitializeNewEntries(attackOrderSounds);
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
        if (audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;

        if (attenuation == UnitSoundAttenuation.Unity3D)
        {
            audioSource.spatialBlend = 1f;
            return;
        }

        audioSource.spatialBlend = 0f;
    }

    public void PlayAttack()
    {
        if (isDead)
            return;

        PlayRandom(attackSounds);
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

        if (Time.unscaledTime < lastVoiceTime + voiceMinInterval)
            return false;

        if (voice == UnitSoundVoice.Select &&
            Time.unscaledTime < lastSelectVoiceTime + selectSoundCooldown)
            return false;

        UnitSoundClip sound = sounds[Random.Range(0, sounds.Length)];

        if (sound == null || sound.clip == null || sound.volume <= 0f)
            return false;

        if (!audioSource.enabled || !gameObject.activeInHierarchy)
            return false;

        float playVolume = (voiceIgnoreAttenuation ? volume : GetEffectiveVolume()) * sound.volume;

        if (playVolume <= 0.001f)
            return false;

        lastVoiceTime = Time.unscaledTime;

        if (voice == UnitSoundVoice.Select)
            lastSelectVoiceTime = Time.unscaledTime;

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
            case UnitSoundVoice.AttackOrder:
                return attackOrderSounds;
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

    void PlayRandom(UnitSoundClip[] sounds, bool detached = false)
    {
        if (sounds == null || sounds.Length == 0 || audioSource == null)
            return;

        UnitSoundClip sound = sounds[Random.Range(0, sounds.Length)];

        if (sound == null || sound.clip == null || volume <= 0f || sound.volume <= 0f)
            return;

        if (!audioSource.enabled || !gameObject.activeInHierarchy)
            return;

        float playVolume = GetEffectiveVolume() * sound.volume;

        if (playVolume <= 0.001f)
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
