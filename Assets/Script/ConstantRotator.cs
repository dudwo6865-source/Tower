using UnityEngine;

/// <summary>
/// 오브젝트를 일정한 속도로 계속 회전시킵니다.
/// 회전 축과 속도는 인스펙터에서 정합니다.
/// </summary>
[AddComponentMenu("Tower/일정 속도 회전 (Constant Rotator)")]
public class ConstantRotator : MonoBehaviour
{
    [Header("회전")]
    [Label("회전 축")]
    [Tooltip("회전 축입니다. (0,1,0)이면 Y축(수평) 회전입니다. 길이는 자동으로 정규화되므로 방향만 맞추면 됩니다.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Label("초당 회전 각도")]
    [Tooltip("초당 회전 각도(도)입니다. 음수면 반대 방향으로 돕니다.")]
    [SerializeField] private float degreesPerSecond = 90f;

    [Label("회전 기준 공간")]
    [Tooltip("Self는 오브젝트의 로컬 축, World는 월드 축을 기준으로 돕니다.")]
    [SerializeField] private Space rotationSpace = Space.Self;

    [Header("옵션")]
    [Label("회전 대상")]
    [Tooltip("회전시킬 대상입니다. 비워두면 이 오브젝트 자신을 돌립니다.")]
    [SerializeField] private Transform target;

    [Label("게임 속도 무시")]
    [Tooltip("켜면 게임 속도(Time.timeScale)의 영향을 받지 않습니다. 일시정지 중에도 도는 UI 연출용입니다.")]
    [SerializeField] private bool useUnscaledTime = false;

    [Label("시작 각도 무작위")]
    [Tooltip("켜면 시작할 때 회전 각도를 무작위로 잡습니다. 같은 프리팹을 여러 개 놓았을 때 전부 같은 각도로 도는 것을 막아줍니다.")]
    [SerializeField] private bool randomizeStartAngle = false;

    /// <summary>초당 회전 각도입니다. 런타임에 속도를 바꿀 때 씁니다.</summary>
    public float DegreesPerSecond
    {
        get => degreesPerSecond;
        set => degreesPerSecond = value;
    }

    /// <summary>회전 축입니다. 런타임에 축을 바꿀 때 씁니다.</summary>
    public Vector3 RotationAxis
    {
        get => rotationAxis;
        set => rotationAxis = value;
    }

    private Transform Target => target != null ? target : transform;

    void Start()
    {
        if (!randomizeStartAngle)
            return;

        Vector3 axis = GetNormalizedAxis();
        if (axis == Vector3.zero)
            return;

        Target.Rotate(axis, Random.Range(0f, 360f), rotationSpace);
    }

    void Update()
    {
        Vector3 axis = GetNormalizedAxis();
        if (axis == Vector3.zero || Mathf.Approximately(degreesPerSecond, 0f))
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        Target.Rotate(axis, degreesPerSecond * deltaTime, rotationSpace);
    }

    // 축 값이 (0,0,0)이면 회전이 정의되지 않으므로 0 벡터를 그대로 돌려주고 호출부에서 건너뛴다.
    Vector3 GetNormalizedAxis()
    {
        if (rotationAxis.sqrMagnitude < 0.000001f)
            return Vector3.zero;

        return rotationAxis.normalized;
    }
}
