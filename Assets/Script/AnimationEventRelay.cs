using UnityEngine;

/// <summary>
/// 자식 모델의 Animator에서 발생한 공격 애니메이션 이벤트를 부모의 UnitAnimator로 넘겨줍니다.
/// 유니티 애니메이션 이벤트는 Animator가 붙은 오브젝트의 스크립트만 호출하므로,
/// 모델이 자식에 있으면 이 중계 컴포넌트가 없을 때 OnAttackHit이 부모에 닿지 않습니다.
/// UnitAnimator가 자식 Animator를 쓰면 시작 시 자동으로 붙으므로 직접 붙일 필요는 없습니다.
/// </summary>
[DisallowMultipleComponent]
public class AnimationEventRelay : MonoBehaviour
{
    [Header("전달 대상")]
    [Label("유닛 애니메이터")]
    [Tooltip("이벤트를 넘겨줄 UnitAnimator입니다. 비워두면 부모에서 자동으로 찾습니다.")]
    public UnitAnimator target;

    void Awake()
    {
        ResolveTarget();
    }

    // 애니메이션 클립 이벤트: 타격 프레임에 피해/투사체를 적용한다.
    public void OnAttackHit()
    {
        ResolveTarget()?.OnAttackHit();
    }

    // 애니메이션 클립 이벤트: 원거리용 이름. 동작은 OnAttackHit과 같다.
    public void OnAttackFire()
    {
        ResolveTarget()?.OnAttackHit();
    }

    UnitAnimator ResolveTarget()
    {
        if (target == null)
            target = GetComponentInParent<UnitAnimator>();

        // 같은 오브젝트의 UnitAnimator는 이벤트를 직접 받으므로 중계하지 않는다(중복 호출 방지).
        if (target != null && target.gameObject == gameObject)
            return null;

        return target;
    }
}
