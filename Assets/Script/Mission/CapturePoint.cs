using UnityEngine;

// 단일 거점의 점령 진행만 관리한다. 승패는 결과 매니저에서 확정한다.
[DisallowMultipleComponent]
public class CapturePoint : MonoBehaviour
{
    public enum CaptureState { Waiting, Capturing, Contested, Complete }

    [Label("거점 이름")]
    public string displayName = "전초 지점";

    [Label("점령 반경")]
    [Tooltip("중심에서의 월드 거리입니다. 높이와 오브젝트 스케일은 판정에 영향을 주지 않습니다.")]
    public float radius = 8f;

    public CaptureState State { get; private set; }
    public float Elapsed { get; private set; }
    public bool WasReset { get; private set; }
    public bool HasValidRadius => radius > 0f && !float.IsNaN(radius) && !float.IsInfinity(radius);

    public void ResetProgress()
    {
        State = CaptureState.Waiting;
        Elapsed = 0f;
        WasReset = false;
    }

    public void Tick(int playerOwnerId, float duration, float deltaTime)
    {
        if (!isActiveAndEnabled || !HasValidRadius || deltaTime <= 0f || State == CaptureState.Complete)
            return;

        bool ally = false;
        bool enemy = false;
        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (entity == null || !entity.isActiveAndEnabled ||
                entity.CachedHealth == null || !entity.CachedHealth.IsAlive ||
                !Contains(entity.transform.position))
                continue;

            if (entity.ownerId != playerOwnerId)
                enemy = true;
            else if (entity.entityType == SelectableEntityType.Unit)
                ally = true;
        }

        Advance(ally, enemy, duration, deltaTime);
    }

    // 범위 안의 개체 검색과 상태 전이를 분리한다.
    public void Advance(bool hasAlly, bool hasEnemy, float duration, float deltaTime)
    {
        if (deltaTime <= 0f || duration <= 0f || float.IsNaN(duration) ||
            float.IsInfinity(duration) || State == CaptureState.Complete)
            return;

        if (!hasAlly)
        {
            WasReset |= Elapsed > 0f;
            Elapsed = 0f;
            State = CaptureState.Waiting;
        }
        else if (hasEnemy)
        {
            State = CaptureState.Contested;
        }
        else
        {
            WasReset = false;
            Elapsed = Mathf.Min(duration, Elapsed + deltaTime);
            State = Elapsed >= duration ? CaptureState.Complete : CaptureState.Capturing;
        }
    }

    public bool Contains(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        return HasValidRadius && offset.x * offset.x + offset.z * offset.z <= radius * radius;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!HasValidRadius) return;
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, radius);
        UnityEditor.Handles.Label(transform.position, displayName);
    }
#endif
}
