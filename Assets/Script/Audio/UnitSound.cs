using UnityEngine;

public enum UnitSoundAttenuation
{
    [Tooltip("거리·줌과 무관하게 항상 같은 볼륨입니다.")]
    None,

    [Tooltip("Unity 3D 공간음향(min/max Distance)을 사용합니다.")]
    Unity3D,

    [Tooltip("맵 XZ 거리 + 직교 줌에 따라 볼륨을 조절합니다. RTS 카메라 권장.")]
    RtsCamera
}

[DisallowMultipleComponent]
public class UnitSound : MonoBehaviour
{
    [Header("클립")]
    [Label("공격 사운드")]
    [Tooltip("공격 시 재생할 사운드입니다. 여러 개면 무작위로 선택합니다.")]
    public AudioClip[] attackClips;

    [Label("명중 사운드")]
    [Tooltip("이 유닛의 공격이 대상에 맞았을 때 재생할 사운드입니다. 히트 이펙트와 같은 시점에 납니다.")]
    public AudioClip[] attackHitClips;

    [Label("사망 사운드")]
    [Tooltip("사망·파괴 시 재생할 사운드입니다.")]
    public AudioClip[] deathClips;

    [Header("재생")]
    [Label("오디오 소스")]
    [Tooltip("비워두면 이 오브젝트에 AudioSource를 자동으로 추가합니다.")]
    public AudioSource audioSource;

    [Label("볼륨")]
    [Tooltip("재생 볼륨입니다.")]
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

    static AudioListener cachedListener;

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

        ApplyAudioSourceSettings();
    }
#endif

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

        PlayRandom(attackClips);
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
        PlayRandom(attackHitClips);
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
        PlayRandom(deathClips);
    }

    void HandleDied()
    {
        PlayDeath();
    }

    void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || audioSource == null)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clip == null || volume <= 0f)
            return;

        if (!audioSource.enabled || !gameObject.activeInHierarchy)
            return;

        float playVolume = GetEffectiveVolume();

        if (playVolume <= 0.001f)
            return;

        float previousPitch = audioSource.pitch;
        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip, playVolume);
        audioSource.pitch = previousPitch;
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
            attackClips = attack;

        if (attackHit != null && attackHit.Length > 0)
            attackHitClips = attackHit;

        if (death != null && death.Length > 0)
            deathClips = death;

        if (newVolume >= 0f)
            volume = newVolume;
    }
}
