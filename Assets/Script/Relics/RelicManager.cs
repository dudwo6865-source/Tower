using System;
using System.Collections.Generic;
using UnityEngine;

// 유물 시스템의 중심 매니저입니다.
// - relicPool에 담긴 유물들의 Available/Owned/Destroyed 상태를 관리합니다.
// - 스포너가 파괴되면 Available 유물 중 candidateCount개를 무작위로 제시합니다.
// - 장착된(Owned) 유물의 수치 효과를 다른 시스템(UnitAttacker, UpgradeManager,
//   WattManager, ManaStoneManager 등)이 조회할 수 있도록 제공합니다.
// UI는 이 컴포넌트가 노출하는 이벤트/메서드에 연결해서 직접 만듭니다.
//
// 스테이지가 끝나면(씬 재로드) 이 컴포넌트도 새로 생성되므로 별도의 초기화 로직
// 없이 유물 상태가 자연스럽게 초기화됩니다. (WattManager 등 다른 매니저와 동일)
[DisallowMultipleComponent]
public class RelicManager : MonoBehaviour
{
    public static RelicManager Instance { get; private set; }

    [Header("소유자")]
    [Label("플레이어 ID")]
    [Tooltip("유물 효과를 받는 플레이어 ID입니다.")]
    public int playerOwnerId = 1;

    [Header("풀")]
    [Label("유물 후보 풀")]
    [Tooltip("이 스테이지에서 후보로 등장할 수 있는 전체 유물 목록입니다. " +
        "만든 RelicDefinition 에셋을 여기에 모두 끌어다 놓으세요.")]
    public List<RelicDefinition> relicPool = new List<RelicDefinition>();

    [Header("슬롯")]
    [Label("슬롯 수")]
    [Tooltip("동시에 장착할 수 있는 유물 수입니다. (기획서 기준 5)")]
    [Min(1)]
    public int slotCount = 5;

    [Label("후보 수")]
    [Tooltip("스포너 파괴 시 한 번에 제시할 후보 수입니다. (기획서 기준 3)")]
    [Min(1)]
    public int candidateCount = 3;

    [Header("디버그")]
    [Label("유물 로그")]
    [Tooltip("유물 후보 제시/장착/교체 로그를 콘솔에 남깁니다.")]
    public bool logRelicEvents = true;

    // 장착된 유물 목록이 바뀌면 발생합니다. (슬롯 UI 갱신용)
    public event Action OnRelicsChanged;

    // 스포너 파괴로 새 후보가 제시되면 발생합니다. (후보 선택 화면을 여는 신호)
    public event Action<IReadOnlyList<RelicDefinition>> OnCandidatesOffered;

    // 슬롯이 가득 찬 상태에서 후보를 골라, 버릴 유물을 정해야 할 때 발생합니다. (교체 화면을 여는 신호)
    public event Action<RelicDefinition> OnReplaceRequired;

    readonly Dictionary<RelicDefinition, RelicState> states = new Dictionary<RelicDefinition, RelicState>();
    readonly List<RelicDefinition> owned = new List<RelicDefinition>();

    List<RelicDefinition> pendingCandidates;
    RelicDefinition pendingReplacement;

    public IReadOnlyList<RelicDefinition> OwnedRelics => owned;
    public IReadOnlyList<RelicDefinition> PendingCandidates => pendingCandidates;
    public RelicDefinition PendingReplacementChoice => pendingReplacement;
    public int SlotCount => Mathf.Max(1, slotCount);
    public bool HasFreeSlot => owned.Count < SlotCount;

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
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public RelicState GetState(RelicDefinition def)
    {
        if (def == null)
            return RelicState.Destroyed;

        return states.TryGetValue(def, out RelicState state) ? state : RelicState.Available;
    }

    // 스포너가 파괴됐을 때 호출합니다. (EnemySpawner.HandleDied에서 호출)
    // Available 유물 중 candidateCount개를 무작위로 뽑아 OnCandidatesOffered로 알립니다.
    public void NotifySpawnerDestroyed(EnemySpawner spawner)
    {
        List<RelicDefinition> available = new List<RelicDefinition>();

        for (int i = 0; i < relicPool.Count; i++)
        {
            RelicDefinition def = relicPool[i];

            if (def != null && GetState(def) == RelicState.Available)
                available.Add(def);
        }

        if (available.Count == 0)
        {
            if (logRelicEvents)
                Debug.Log("RelicManager: 제시할 수 있는 Available 유물이 없습니다.", this);

            return;
        }

        pendingCandidates = PickRandomUnique(available, candidateCount);

        if (logRelicEvents)
        {
            string spawnerName = spawner != null ? spawner.name : "(unknown)";
            Debug.Log($"RelicManager: 스포너 '{spawnerName}' 파괴 — 후보 {pendingCandidates.Count}종 제시: " +
                string.Join(", ", pendingCandidates.ConvertAll(r => r.displayName)), this);
        }

        OnCandidatesOffered?.Invoke(pendingCandidates);
    }

    // UI에서 후보 하나를 선택했을 때 호출합니다.
    // 빈 슬롯이 있으면 즉시 장착하고, 없으면 OnReplaceRequired를 발생시켜 버릴 유물을 물어봅니다.
    public bool ChooseRelic(RelicDefinition chosen)
    {
        if (chosen == null || GetState(chosen) != RelicState.Available)
            return false;

        pendingCandidates = null;

        if (HasFreeSlot)
        {
            Equip(chosen);
            return true;
        }

        pendingReplacement = chosen;

        if (logRelicEvents)
            Debug.Log($"RelicManager: 슬롯이 가득 차 교체 대상 선택 대기 — 선택한 유물 '{chosen.displayName}'", this);

        OnReplaceRequired?.Invoke(chosen);
        return true;
    }

    // 교체 화면에서 버릴 유물을 정했을 때 호출합니다.
    public bool ConfirmReplace(RelicDefinition toDiscard)
    {
        if (pendingReplacement == null || toDiscard == null || !owned.Contains(toDiscard))
            return false;

        RelicDefinition newRelic = pendingReplacement;
        pendingReplacement = null;

        Discard(toDiscard);
        Equip(newRelic);
        return true;
    }

    // 교체를 취소합니다. 골랐던 후보는 Available 상태 그대로 남아 다음에 다시 등장할 수 있습니다.
    public void CancelReplace()
    {
        pendingReplacement = null;
    }

    void Equip(RelicDefinition def)
    {
        states[def] = RelicState.Owned;
        owned.Add(def);

        if (logRelicEvents)
            Debug.Log($"RelicManager: '{def.displayName}' 장착 ({owned.Count}/{SlotCount})", this);

        NotifyChanged();
    }

    void Discard(RelicDefinition def)
    {
        states[def] = RelicState.Destroyed;
        owned.Remove(def);

        if (logRelicEvents)
            Debug.Log($"RelicManager: '{def.displayName}' 버림 (Destroyed)", this);
    }

    void NotifyChanged()
    {
        OnRelicsChanged?.Invoke();

        // 체력·이동속도·생산 상한처럼 UpgradeManager가 미리 계산해 밀어넣는(push) 값들은
        // 유물이 바뀔 때도 다시 계산해야 반영된다.
        UpgradeManager.Instance?.ApplyToAllEntities();
    }

    static List<RelicDefinition> PickRandomUnique(List<RelicDefinition> source, int count)
    {
        List<RelicDefinition> pool = new List<RelicDefinition>(source);
        List<RelicDefinition> picked = new List<RelicDefinition>();
        int take = Mathf.Min(count, pool.Count);

        for (int i = 0; i < take; i++)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return picked;
    }

    // ---- 효과 조회 (UpgradeManager.GetPercentBonus/GetFlatBonus/ApplyBonus와 같은 공식) ----

    public float GetPercentBonus(RelicEffectType type)
    {
        float total = 0f;

        for (int i = 0; i < owned.Count; i++)
        {
            RelicDefinition def = owned[i];

            if (def != null && def.effectType == type && def.valueMode == RelicValueMode.Percent)
                total += def.value;
        }

        return total / 100f;
    }

    public float GetFlatBonus(RelicEffectType type)
    {
        float total = 0f;

        for (int i = 0; i < owned.Count; i++)
        {
            RelicDefinition def = owned[i];

            if (def != null && def.effectType == type && def.valueMode == RelicValueMode.Flat)
                total += def.value;
        }

        return total;
    }

    public float ApplyBonus(RelicEffectType type, float baseValue)
    {
        return baseValue * (1f + GetPercentBonus(type)) + GetFlatBonus(type);
    }

    // ownerId가 이 유물 효과를 받는 플레이어와 다르면(적 등) baseValue를 그대로 돌려준다.
    public float GetModifiedValue(RelicEffectType type, int ownerId, float baseValue)
    {
        if (ownerId != playerOwnerId)
            return baseValue;

        return ApplyBonus(type, baseValue);
    }
}
