using UnityEngine;

public class TowerAI : CombatAIBase
{
    [Header("Turret")]
    [Tooltip("적을 향해 회전시킬 포탑 트랜스폼입니다. 비워두면 이 오브젝트 자신을 회전합니다. " +
             "모델 기울기(-90 X 등)는 이 트랜스폼의 '자식' 메쉬에 두어야 회전 시 똑바로 섭니다.")]
    public Transform turretPivot;

    [Tooltip("포탑이 대상을 바라보는 회전 속도입니다.")]
    public float facingSpeed = 8f;

    [Tooltip("포탑이 적을 정확히 겨누도록 추가 보정하는 Y축 각도(도)입니다. " +
             "Fire Point가 있으면 그 방향을 기준으로 자동 보정되며, 이 값은 추가로 더해집니다.")]
    public float aimYawOffset;

    [Tooltip("대상이 없을 때 처음 바라보던 방향으로 천천히 돌아갑니다.")]
    public bool returnToIdleWhenNoTarget = true;

    private Quaternion idleRotation;
    private float effectiveYawOffset;

    protected override void Awake()
    {
        base.Awake();

        if (turretPivot == null)
            turretPivot = transform;

        idleRotation = turretPivot.rotation;
        effectiveYawOffset = aimYawOffset + ComputeFirePointYawOffset();
    }

    float ComputeFirePointYawOffset()
    {
        if (attacker == null || attacker.firePoint == null)
            return 0f;

        Vector3 pivotForward = turretPivot.forward;
        Vector3 fireForward = attacker.firePoint.forward;
        pivotForward.y = 0f;
        fireForward.y = 0f;

        if (pivotForward.sqrMagnitude < 0.0001f ||
            fireForward.sqrMagnitude < 0.0001f)
            return 0f;

        return Vector3.SignedAngle(
            pivotForward.normalized,
            fireForward.normalized,
            Vector3.up);
    }

    void Update()
    {
        TickRetarget();

        if (!HasValidTarget())
        {
            if (returnToIdleWhenNoTarget)
                RotateTowards(idleRotation);

            return;
        }

        FaceTarget();

        if (attacker.IsInRange(currentTarget))
            attacker.TryAttack(currentTarget, currentTargetHealth);
    }

    void FaceTarget()
    {
        Vector3 direction =
            currentTarget.transform.position - turretPivot.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        // Fire Point 방향을 기준으로 Y축만 돌려 모델 기울기(X/Z)는 idle 자세를 유지한다.
        float yaw =
            Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg +
            effectiveYawOffset;

        Vector3 idleEuler = idleRotation.eulerAngles;
        Quaternion targetRotation =
            Quaternion.Euler(idleEuler.x, yaw, idleEuler.z);

        RotateTowards(targetRotation);
    }

    void RotateTowards(Quaternion targetRotation)
    {
        turretPivot.rotation = Quaternion.Slerp(
            turretPivot.rotation,
            targetRotation,
            Time.deltaTime * facingSpeed);
    }
}
