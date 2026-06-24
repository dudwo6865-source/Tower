using System;
using UnityEngine;

[DisallowMultipleComponent]
public class WattManager : MonoBehaviour
{
    public static WattManager Instance { get; private set; }

    [Header("Watt")]
    [Tooltip("배틀 시작 시 보유 Watt입니다.")]
    public float startingWatt = 50f;

    [Tooltip("초당 자동으로 증가하는 Watt입니다. HQ·점령지 연동은 나중에 추가할 수 있습니다.")]
    public float incomePerSecond = 5f;

    public float CurrentWatt { get; private set; }

    public event Action<float> OnWattChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentWatt = Mathf.Max(0f, startingWatt);
        NotifyChanged();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (incomePerSecond <= 0f)
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

        CurrentWatt -= cost;
        NotifyChanged();
        return true;
    }

    public void AddWatt(float amount)
    {
        if (amount <= 0f)
            return;

        CurrentWatt += amount;
        NotifyChanged();
    }

    void NotifyChanged()
    {
        OnWattChanged?.Invoke(CurrentWatt);
    }
}
