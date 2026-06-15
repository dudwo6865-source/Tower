using UnityEngine;

public class TowerAI : CombatAIBase
{
    [Header("Turret")]
    [Tooltip("적을 향해 회전시킬 포탑 트랜스폼입니다. 비워두면 이 오브젝트 자신을 회전합니다. " +
             "모델 기울기(-90 X 등)는 이 트랜스폼의 '자식' 메쉬에 두어야 회전 시 똑바로 섭니다.")]
    public Transform turretPivot;

    [Tooltip("포탑이 대상을 바라보는 회전 속도입니다.")]
    public float facingSpeed = 8f;

    [Tooltip("포탑이 적을 정확히 겨누도록 보정하는 Y축 각도(도)입니다. 모델 정면이 어긋나면 값을 조정하세요.")]
    public float aimYawOffset;

    [Tooltip("대상이 없을 때 처음 바라보던 방향으로 천천히 돌아갑니다.")]
    public bool returnToIdleWhenNoTarget = true;

    private Quaternion idleRotation;

    protected override void Awake()
    {
        base.Awake();

        if (turretPivot == null)
            turretPivot = transform;

        idleRotation = turretPivot.rotation;
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

        // 초기 자세(모델 기울기 포함)를 유지한 채 월드 Y축으로만 회전시킨다.
        // 이렇게 하면 루트에 -90 X 기울기가 있어도 뒤집히지 않는다.
        float yaw =
            Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + aimYawOffset;

        Quaternion targetRotation =
            Quaternion.AngleAxis(yaw, Vector3.up) * idleRotation;

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
