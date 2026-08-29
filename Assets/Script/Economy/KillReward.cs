using UnityEngine;

// 이 오브젝트(적)가 죽으면 플레이어가 마석을 획득합니다.
// 싱글 게임이므로 막타 판정 없이, 죽는 즉시 무조건 지급합니다.
// 적 프리팹(예: Monster)에 붙여 사용합니다.
[RequireComponent(typeof(EntityHealth))]
[DisallowMultipleComponent]
public class KillReward : MonoBehaviour
{
    [Tooltip("이 대상이 죽을 때 플레이어에게 지급하는 마석 양입니다.")]
    public int manaStoneReward = 5;

    EntityHealth health;

    void Awake()
    {
        health = GetComponent<EntityHealth>();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDied += HandleDied;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    void HandleDied()
    {
        if (manaStoneReward <= 0)
            return;

        ManaStoneManager manager = ManaStoneManager.Instance;

        if (manager != null)
            manager.Add(manaStoneReward);
    }
}
