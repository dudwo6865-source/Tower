using System;
using UnityEngine;

[DisallowMultipleComponent]
public class WattManager : MonoBehaviour
{
    public static WattManager Instance { get; private set; }

    [Header("Watt")]
    [Label("최대 Watt")]
    [Tooltip("Watt 최대 충전량입니다.")]
    public float maxWatt = 100f;

    [Label("시작 Watt")]
    [Tooltip("배틀 시작 시 보유 Watt입니다.")]
    public float startingWatt = 50f;

    [Label("초당 수입")]
    [Tooltip("초당 자동으로 증가하는 Watt입니다. HQ·점령지 연동은 나중에 추가할 수 있습니다.")]
    public float incomePerSecond = 5f;

    public float CurrentWatt { get; private set; }

    // W02 대용량 축전지: 최대 Watt 보유량 보너스를 반영합니다.
    public float MaxWatt
    {
        get
        {
            float value = maxWatt;

            if (RelicManager.Instance != null)
                value = RelicManager.Instance.ApplyBonus(RelicEffectType.WattMaxCapacity, value);

            return Mathf.Max(0f, value);
        }
    }

    public float FillRatio
    {
        get
        {
            if (MaxWatt <= 0f)
                return 0f;

            return Mathf.Clamp01(CurrentWatt / MaxWatt);
        }
    }

    // W01 고효율 발전기 보너스를 반영한 초당 수입입니다.
    public float EffectiveIncomePerSecond
    {
        get
        {
            float income = Mathf.Max(0f, incomePerSecond);

            if (RelicManager.Instance != null)
                income = RelicManager.Instance.ApplyBonus(RelicEffectType.WattIncome, income);

            return income;
        }
    }

    // 생산 건물들이 지금 소모 중인 초당 Watt 합계입니다.
    public float CurrentDrainPerSecond => ProductionBuilding.GetTotalWattDrainPerSecond();

    // 초당 수입에서 초당 소모량을 뺀 순수입입니다. 음수면 Watt가 줄어듭니다.
    public float NetIncomePerSecond => EffectiveIncomePerSecond - CurrentDrainPerSecond;

    public bool IsFull => MaxWatt > 0f && CurrentWatt >= MaxWatt - 0.001f;

    public event Action<float> OnWattChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentWatt = ClampWatt(Mathf.Max(0f, startingWatt));
        NotifyChanged();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (incomePerSecond <= 0f || IsFull)
            return;

        AddWatt(EffectiveIncomePerSecond * Time.deltaTime);
    }

    public bool CanAfford(int cost)
    {
        return cost <= 0 || CurrentWatt >= cost;
    }

    public bool TrySpend(int cost)
    {
        if (cost <= 0)
            return true;

        if (!CanAfford(cost))
            return false;

        CurrentWatt = ClampWatt(CurrentWatt - cost);
        NotifyChanged();
        return true;
    }

    // 초당 소모처럼 소수 단위로 조금씩 차감할 때 사용한다. 부족하면 차감하지 않는다.
    public bool TrySpendAmount(float amount)
    {
        if (amount <= 0f)
            return true;

        if (CurrentWatt < amount)
            return false;

        CurrentWatt = ClampWatt(CurrentWatt - amount);
        NotifyChanged();
        return true;
    }

    public void AddWatt(float amount)
    {
        if (amount <= 0f)
            return;

        float before = CurrentWatt;
        CurrentWatt = ClampWatt(CurrentWatt + amount);

        if (Mathf.Approximately(before, CurrentWatt))
            return;

        NotifyChanged();
    }

    float ClampWatt(float value)
    {
        if (MaxWatt <= 0f)
            return Mathf.Max(0f, value);

        return Mathf.Clamp(value, 0f, MaxWatt);
    }

    void NotifyChanged()
    {
        OnWattChanged?.Invoke(CurrentWatt);
    }
}
