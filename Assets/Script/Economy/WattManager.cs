using System;
using UnityEngine;

[DisallowMultipleComponent]
public class WattManager : MonoBehaviour
{
    public static WattManager Instance { get; private set; }

    [Header("Watt")]
    [Tooltip("Watt 최대 충전량입니다.")]
    public float maxWatt = 100f;

    [Tooltip("배틀 시작 시 보유 Watt입니다.")]
    public float startingWatt = 50f;

    [Tooltip("초당 자동으로 증가하는 Watt입니다. HQ·점령지 연동은 나중에 추가할 수 있습니다.")]
    public float incomePerSecond = 5f;

    public float CurrentWatt { get; private set; }

    public float MaxWatt => Mathf.Max(0f, maxWatt);

    public float FillRatio
    {
        get
        {
            if (MaxWatt <= 0f)
                return 0f;

            return Mathf.Clamp01(CurrentWatt / MaxWatt);
        }
    }

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

        AddWatt(incomePerSecond * Time.deltaTime);
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
