using System;
using System.Collections.Generic;
using UnityEngine;

// 웨이브 진행을 관리합니다.
// 스포너를 새로 만들지 않고, 맵에 이미 배치된 EnemySpawner들의 수치
// (스폰량 / 스폰 간격 / 동시 생존 상한 / 스폰되는 적의 스탯 가중치)를 웨이브마다 조정합니다.
//
// 웨이브 = 낮과 밤 한 주기입니다. 첫 낮이 웨이브 1이고, 밤이 끝나 다음 낮이 시작되면 웨이브가 오릅니다.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    public DayNightCycle dayNightCycle;

    [Header("Wave Plan")]
    [Tooltip("웨이브별로 맵의 모든 EnemySpawner에 적용할 수치입니다. " +
             "표에 적은 웨이브 이후는 마지막 값에 증가율이 복리로 붙어 계속 이어집니다.")]
    public WavePlan wavePlan = new WavePlan();

    [Header("Night Bonus")]
    [Tooltip("켜면 밤 동안에만 아래 보정을 웨이브 수치에 한 번 더 곱합니다.")]
    public bool applyNightBonus = true;

    [Tooltip("밤 동안 추가로 곱할 보정입니다. 전부 1이면 낮과 같습니다.")]
    public WaveTuning nightBonus = new WaveTuning();

    [Header("Debug")]
    [Tooltip("웨이브가 바뀔 때 콘솔에 적용된 수치를 남깁니다.")]
    public bool logWaveChanges = true;

    /// <summary>1부터 시작합니다. 1 = 첫 번째 웨이브(첫 낮 + 첫 밤).</summary>
    public int CurrentWaveNumber { get; private set; } = 1;

    /// <summary>지금 스포너들에 적용 중인 최종 보정값입니다(밤 보정 포함).</summary>
    public WaveTuning CurrentTuning { get; private set; } = new WaveTuning();

    public bool IsNight => dayNightCycle != null && dayNightCycle.IsNight;

    public int ActiveSpawnerCount => EnemySpawner.Active.Count;

    public int TotalAliveEnemies
    {
        get
        {
            int total = 0;
            IReadOnlyList<EnemySpawner> spawners = EnemySpawner.Active;

            for (int i = 0; i < spawners.Count; i++)
            {
                if (spawners[i] != null)
                    total += spawners[i].AliveCount;
            }

            return total;
        }
    }

    /// <summary>웨이브 번호와 적용된 보정값을 함께 전달합니다. UI 표시 등에 씁니다.</summary>
    public event Action<int, WaveTuning> OnWaveChanged;

    public event Action<int> OnNightStarted;

    DayNightPhase? lastPhase;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 이 컴포넌트만 제거한다. gameObject째로 지우면 같은 오브젝트에 있는
            // 다른 매니저까지 함께 사라진다.
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveReferences();

        // 스포너(실행 순서 0)의 Awake보다 먼저 현재 웨이브 수치를 준비해 둔다.
        RefreshTuning(notify: false);
    }

    void OnEnable()
    {
        // Destroy(this)는 프레임 끝까지 지연되므로 중복 인스턴스도 OnEnable/Start가 돈다.
        // 그대로 두면 중복 쪽의 기본값이 스포너 전체에 덮어씌워진다.
        if (Instance != this)
            return;

        if (dayNightCycle != null)
            dayNightCycle.OnPhaseStarted += HandlePhaseStarted;
    }

    void OnDisable()
    {
        if (dayNightCycle != null)
            dayNightCycle.OnPhaseStarted -= HandlePhaseStarted;
    }

    void Start()
    {
        if (Instance != this)
            return;

        // 씬의 스포너들이 모두 등록된 뒤 한 번 더 적용한다.
        RefreshTuning(notify: true);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void ResolveReferences()
    {
        if (dayNightCycle == null)
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();
    }

    void HandlePhaseStarted(DayNightPhase phase)
    {
        // '밤 -> 낮'으로 실제로 넘어갔을 때만 웨이브를 올린다.
        // DayNightCycle은 시작할 때 현재 페이즈로 이벤트를 한 번 쏘는데,
        // 그건 웨이브가 넘어간 게 아니라 시작 상태를 알리는 것이다.
        bool nightEnded =
            lastPhase.HasValue &&
            lastPhase.Value == DayNightPhase.Night &&
            phase == DayNightPhase.Day;

        lastPhase = phase;

        if (nightEnded)
            CurrentWaveNumber++;

        RefreshTuning(notify: true);

        if (phase == DayNightPhase.Night)
            OnNightStarted?.Invoke(CurrentWaveNumber);
    }

    /// <summary>현재 웨이브(와 낮/밤)에 맞는 보정을 다시 계산해 모든 스포너에 적용합니다.</summary>
    public void RefreshTuning(bool notify)
    {
        CurrentTuning = BuildTuningForWave(CurrentWaveNumber, IsNight);
        ApplyCurrentWaveToAll();

        if (!notify)
            return;

        OnWaveChanged?.Invoke(CurrentWaveNumber, CurrentTuning);

        if (logWaveChanges)
        {
            Debug.Log(
                $"WaveManager: 웨이브 {CurrentWaveNumber}" +
                $"{(IsNight ? " (밤)" : " (낮)")} — 스포너 {ActiveSpawnerCount}개에 적용: " +
                $"{CurrentTuning.ToShortSummary()}");
        }
    }

    /// <summary>해당 웨이브의 보정값입니다. 에디터 미리보기와 UI가 같이 씁니다.</summary>
    public WaveTuning BuildTuningForWave(int waveNumber, bool night)
    {
        WaveTuning waveTuning = wavePlan != null
            ? wavePlan.Evaluate(waveNumber)
            : new WaveTuning();

        if (!night || !applyNightBonus)
            return waveTuning.Sanitized();

        return WaveTuning.Combine(waveTuning, nightBonus).Sanitized();
    }

    public void ApplyCurrentWaveToAll()
    {
        IReadOnlyList<EnemySpawner> spawners = EnemySpawner.Active;

        for (int i = spawners.Count - 1; i >= 0; i--)
            ApplyCurrentWaveTo(spawners[i]);
    }

    /// <summary>웨이브 도중에 새로 등록된 스포너가 현재 수치를 따라오게 합니다.</summary>
    public void ApplyCurrentWaveTo(EnemySpawner spawner)
    {
        if (spawner == null)
            return;

        spawner.ApplyWaveTuning(CurrentTuning, CurrentWaveNumber);
    }

    /// <summary>테스트용으로 웨이브를 직접 지정합니다.</summary>
    public void SetWave(int waveNumber)
    {
        CurrentWaveNumber = Mathf.Max(1, waveNumber);
        RefreshTuning(notify: true);
    }

    [ContextMenu("다음 웨이브로 진행 (테스트)")]
    void AdvanceWaveFromMenu()
    {
        SetWave(CurrentWaveNumber + 1);
    }
}
