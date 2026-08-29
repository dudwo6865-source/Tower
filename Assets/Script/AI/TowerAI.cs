using UnityEngine;

public class TowerAI : CombatAIBase
{
    [Header("Turret")]
    [Tooltip("적을 향해 회전시킬 포탑 트랜스폼입니다. 비워두면 이 오브젝트 자신을 회전합니다. " +
             "모델 기울기(-90 X 등)는 이 트랜스폼의 '자식' 메쉬에 두어야 회전 시 똑바로 섭니다.")]
    public Transform turretPivot;

    [Tooltip("포탑이 대상을 바라보는 회전 속도입니다.")]
    public float facingSpeed = 8f;

    [Tooltip("포신 방향에 더하는 Y축 보정(도)입니다. Fire Point를 포신 끝에 두면 0으로 두세요.")]
    public float aimYawOffset;

    [Tooltip("대상이 없을 때 처음 바라보던 방향으로 천천히 돌아갑니다.")]
    public bool returnToIdleWhenNoTarget = true;

    private Quaternion idleRotation;
    private Vector3 barrelLocal = Vector3.forward;

    protected override void Awake()
    {
        base.Awake();

        if (turretPivot == null)
            turretPivot = transform;

        idleRotation = turretPivot.rotation;
        barrelLocal = ResolveBarrelLocal();

        if (attacker != null)
        {
            attacker.aimTransform = turretPivot;
            attacker.aimAxisLocal = barrelLocal;
            attacker.aimYawOffset = 0f;
        }
    }

    void Update()
    {
        if (BuildingConstructionGate.IsFeatureLockedOn(this))
            return;

        TickRetarget();

        if (!HasValidTarget())
        {
            if (returnToIdleWhenNoTarget)
                RotateYawTowards(idleRotation);

            return;
        }

        FaceTarget();

        if (attacker.IsInRange(currentTarget))
            attacker.TryAttack(currentTarget, currentTargetHealth);
    }

    Vector3 ResolveBarrelLocal()
    {
        Vector3 local = Vector3.forward;

        if (attacker != null && attacker.firePoint != null)
        {
            Vector3 fromPivot = turretPivot.InverseTransformPoint(attacker.firePoint.position);
            fromPivot.y = 0f;
            if (fromPivot.sqrMagnitude > 0.01f)
                local = fromPivot.normalized;
            else
            {
                Vector3 fireForward = turretPivot.InverseTransformDirection(attacker.firePoint.forward);
                fireForward.y = 0f;
                if (fireForward.sqrMagnitude > 0.0001f)
                    local = fireForward.normalized;
            }
        }

        if (Mathf.Abs(aimYawOffset) > 0.01f)
            local = Quaternion.Euler(0f, aimYawOffset, 0f) * local;

        return local.sqrMagnitude > 0.0001f ? local.normalized : Vector3.forward;
    }

    void FaceTarget()
    {
        Vector3 origin = attacker != null && attacker.firePoint != null
            ? attacker.firePoint.position
            : turretPivot.position;
        Vector3 toTarget = currentTarget.SelectionBounds.center - origin;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.01f)
            return;

        Vector3 barrelWorld = turretPivot.TransformDirection(barrelLocal);
        barrelWorld.y = 0f;
        if (barrelWorld.sqrMagnitude < 0.0001f)
            return;

        float deltaYaw = Vector3.SignedAngle(barrelWorld, toTarget, Vector3.up);
        ApplyYaw(deltaYaw);
    }

    void RotateYawTowards(Quaternion targetRotation)
    {
        Vector3 currentYaw = turretPivot.forward;
        Vector3 targetYaw = targetRotation * Vector3.forward;
        currentYaw.y = 0f;
        targetYaw.y = 0f;
        if (currentYaw.sqrMagnitude < 0.0001f || targetYaw.sqrMagnitude < 0.0001f)
            return;

        float deltaYaw = Vector3.SignedAngle(currentYaw, targetYaw, Vector3.up);
        ApplyYaw(deltaYaw);
    }

    void ApplyYaw(float deltaYaw)
    {
        float maxDegrees = Mathf.Max(1f, facingSpeed) * 90f * Time.deltaTime;
        float applied = Mathf.Min(Mathf.Abs(deltaYaw), maxDegrees) * Mathf.Sign(deltaYaw);
        if (Mathf.Abs(applied) < 0.001f)
            return;

        turretPivot.rotation = Quaternion.AngleAxis(applied, Vector3.up) * turretPivot.rotation;
    }
}
