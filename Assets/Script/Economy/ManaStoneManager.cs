using System;
using UnityEngine;

// 마석(魔石) 자원 매니저입니다.
// 적을 처치할 때 획득하며, 그 게임 안에서만 유효한 업그레이드에 소비됩니다.
// (Watt과 달리 시간에 따른 자동 충전은 없습니다.)
[DisallowMultipleComponent]
public class ManaStoneManager : MonoBehaviour
{
    public static ManaStoneManager Instance { get; private set; }

    [Header("Owner")]
    [Tooltip("이 마석을 보유하는 플레이어 ID입니다. 이 소속의 유닛/건물이 적을 처치하면 마석을 얻습니다.")]
    public int ownerId = 1;

    [Header("Mana Stone")]
    [Tooltip("보유 가능한 최대 마석입니다. 0 이하면 상한이 없습니다.")]
    public float maxManaStone = 0f;

    [Tooltip("배틀 시작 시 보유 마석입니다.")]
    public float startingManaStone = 0f;

    public float CurrentManaStone { get; private set; }

    public float MaxManaStone => Mathf.Max(0f, maxManaStone);

    public bool HasCap => MaxManaStone > 0f;

    public event Action<float> OnManaStoneChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentManaStone = ClampAmount(Mathf.Max(0f, startingManaStone));
        NotifyChanged();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool CanAfford(int cost)
    {
        return cost <= 0 || CurrentManaStone >= cost;
    }

    public bool TrySpend(int cost)
    {
        if (cost <= 0)
            return true;

        if (!CanAfford(cost))
            return false;

        CurrentManaStone = ClampAmount(CurrentManaStone - cost);
        NotifyChanged();
        return true;
    }

    public void Add(float amount)
    {
        if (amount <= 0f)
            return;

        float before = CurrentManaStone;
        CurrentManaStone = ClampAmount(CurrentManaStone + amount);

        if (Mathf.Approximately(before, CurrentManaStone))
            return;

        NotifyChanged();
    }

    float ClampAmount(float value)
    {
        if (!HasCap)
            return Mathf.Max(0f, value);

        return Mathf.Clamp(value, 0f, MaxManaStone);
    }

    void NotifyChanged()
    {
        OnManaStoneChanged?.Invoke(CurrentManaStone);
    }
}
