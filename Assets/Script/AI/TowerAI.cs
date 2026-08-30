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

        // 이미 사거리 안에서 유효한 표적을 쏘고 있으면 재탐색하지 않는다.
        // 사거리를 벗어나거나 죽어야만 다시 찾는다.
        if (!(HasValidTarget() && attacker.IsInRange(currentTarget)))
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

    protected override void HandleAttackedBy(SelectableEntity attackerEntity)
    {
        // 타워는 피격 반격을 하지 않는다. 맞았다고 공격한 상대를 표적으로
        // 삼지 않고, 항상 일반 표적 탐색(TickRetarget/우선순위)으로만 고른다.
    }

    protected override void LogTargetChange(
        SelectableEntity previous,
        SelectableEntity next)
    {
        if (!debugCommandLog || previous == next)
            return;

        UnitCommandDebugLog.Log(
            this,
            $"타겟 변경({DescribeDropReason(previous)}) " +
            $"{DescribeTarget(previous)} -> {DescribeTargetGap(next)}");
    }

    // 타워는 사거리 안의 유효한 표적을 쏘는 동안 재탐색하지 않는다.
    // 그래서 표적이 바뀌었다는 건 이전 표적을 놓쳤다는 뜻이고, 그 이유를 같이 남긴다.
    string DescribeDropReason(SelectableEntity previous)
    {
        if (ReferenceEquals(previous, null))
            return "신규";

        // Unity의 == 오버로드라 파괴된 오브젝트도 여기서 걸린다.
        if (previous == null)
            return "소멸";

        EntityHealth health = previous.CachedHealth;

        if (health == null || !health.IsAlive)
            return "처치";

        if (attacker == null)
            return "재탐색";

        return attacker.IsInRange(previous)
            ? $"우선순위({targetPriority})"
            : $"사거리 이탈 {attacker.GetRangeGap(previous):0.0}";
    }

    string DescribeTargetGap(SelectableEntity target)
    {
        if (target == null || attacker == null)
            return DescribeTarget(target);

        return $"{DescribeTarget(target)} " +
               $"간격 {attacker.GetRangeGap(target):0.0}/{attacker.AttackRange:0.0}";
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

    // 조준각 계산은 UnitAttacker가 전담합니다. 여기서 따로 재면 발사 판정(IsAimedAt)과
    // 어긋나서, 다 돌았는데도 공격이 안 나가는 상황이 생깁니다.
    void FaceTarget()
    {
        if (attacker == null ||
            !attacker.TryGetAimYawDelta(turretPivot, currentTarget, out float deltaYaw))
            return;

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
