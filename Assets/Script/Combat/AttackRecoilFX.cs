using System.Collections;
using UnityEngine;

/// <summary>
/// 공격(발사) 순간 조준 트랜스폼(UnitAttacker.aimTransform, 타워는 포탑 피벗)을
/// 짧게 뒤로 밀었다가 원위치로 되돌리는 반동 연출입니다. 포탑이 현재 바라보는
/// 방향 기준으로 뒤로 미는 것이라, 타겟이 바뀌어 포탑이 돌아가 있어도 항상
/// 자연스럽게 뒤로 빠집니다. Animator나 리깅 없이 모든 유닛/타워 프리팹에
/// 재사용 가능합니다.
/// </summary>
[DisallowMultipleComponent]
public class AttackRecoilFX : MonoBehaviour
{
    [Tooltip("비워두면 UnitAttacker.aimTransform(타워는 포탑 피벗)을 자동으로 사용합니다.")]
    public Transform recoilTarget;

    [Tooltip("뒤로 밀리는 거리(로컬 단위)입니다.")]
    public float distance = 0.12f;

    [Tooltip("뒤로 튕기는(킥아웃) 시간(초)입니다.")]
    public float kickTime = 0.04f;

    [Tooltip("원위치로 돌아오는 시간(초)입니다.")]
    public float returnTime = 0.18f;

    [Tooltip("recoilTarget의 로컬 공간 기준 반동 축입니다. 기본은 뒤쪽(-Z)입니다.")]
    public Vector3 recoilAxisLocal = Vector3.back;

    Vector3 currentOffset;
    Coroutine playRoutine;

    void Start()
    {
        // UnitAttacker.aimTransform은 TowerAI 등이 자신의 Awake에서 채워주므로,
        // 실행 순서에 상관없이 값이 확정된 뒤인 Start에서 읽는다.
        if (recoilTarget == null)
        {
            UnitAttacker attacker = GetComponent<UnitAttacker>();
            recoilTarget = attacker != null && attacker.aimTransform != null
                ? attacker.aimTransform
                : transform;
        }
    }

    public void Play()
    {
        if (recoilTarget == null)
            return;

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        Vector3 axis = recoilAxisLocal.sqrMagnitude > 0.0001f
            ? recoilAxisLocal.normalized
            : Vector3.back;

        float t = 0f;
        while (t < kickTime)
        {
            t += Time.deltaTime;
            Apply(axis, Mathf.Clamp01(t / kickTime));
            yield return null;
        }

        Apply(axis, 1f);

        t = 0f;
        while (t < returnTime)
        {
            t += Time.deltaTime;
            Apply(axis, 1f - Mathf.Clamp01(t / returnTime));
            yield return null;
        }

        Apply(axis, 0f);
        playRoutine = null;
    }

    void Apply(Vector3 axisLocal, float amount)
    {
        // recoilTarget의 절대 위치를 캐시된 값으로 되돌리는 대신, 매 프레임
        // 목표 오프셋과 직전 오프셋의 '차이'만큼만 이동시킨다. 이동 유닛처럼
        // recoilTarget(자기 자신)이 NavMeshAgent 등으로 계속 움직이는 경우에도
        // 그 움직임 위에 반동만 얹히므로, 반동이 끝난 뒤 스폰 시점 위치로
        // 스냅되는 문제가 생기지 않는다.
        Vector3 targetOffset = axisLocal * (distance * amount);
        Vector3 delta = targetOffset - currentOffset;
        recoilTarget.Translate(delta, Space.Self);
        currentOffset = targetOffset;
    }
}
