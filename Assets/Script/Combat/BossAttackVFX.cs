using UnityEngine;

// Animator와 같은 오브젝트에서 바닥 폭발 애니메이션 이벤트를 받는다.
[DisallowMultipleComponent]
[AddComponentMenu("게임/전투/BossAttackVFX")]
public class BossAttackVFX : MonoBehaviour
{
    [Header("바닥 폭발")]
    [Label("폭발 이펙트 프리팹")]
    [Tooltip("한 번 재생되는 파티클 프리팹을 연결합니다. 생성 후 기존 전투 이펙트 시스템이 자동으로 정리합니다.")]
    public GameObject explosionPrefab;
    [Label("위치 기준")]
    [Tooltip("비워두면 부모 EntityHealth의 위치와 방향을 사용합니다. 손이나 별도 타격 지점을 연결할 수도 있습니다.")]
    public Transform impactOrigin;
    [Label("위치 오프셋")]
    [Tooltip("기준의 방향을 따르는 미터 단위 오프셋입니다. Z는 앞쪽이며 보스 크기 배율은 적용하지 않습니다.")]
    public Vector3 positionOffset = new Vector3(0f, 0f, 3f);
    [Label("이펙트 크기")]
    [Min(0.01f)] public float effectScale = 1f;
    [Label("회전 보정")]
    public Vector3 rotationOffset;

    [Header("지면 배치")]
    [Label("지면 레이어")]
    [Tooltip("지형 콜라이더의 레이어만 선택합니다. 유닛과 건물의 EntityHealth 콜라이더는 제외합니다.")]
    public LayerMask groundLayers = 1;
    [Label("지면 탐색 시작 높이")]
    [Min(0f)] public float rayStartHeight = 5f;
    [Label("지면 탐색 거리")]
    [Min(0.01f)] public float rayDistance = 20f;
    [Label("지면에서 띄울 높이")]
    public float groundOffset = 0.03f;

    EntityHealth ownerHealth;
    BossSkillController bossSkills;

    void Awake()
    {
        ownerHealth = GetComponentInParent<EntityHealth>();
        bossSkills = GetComponentInParent<BossSkillController>();
    }

    // 피해 판정은 기존 공격 이벤트가 담당하고 이 함수는 시각 효과만 재생한다.
    public void OnGroundExplosion()
    {
        if (bossSkills != null && bossSkills.IsActive)
            return;
        if (!isActiveAndEnabled || explosionPrefab == null ||
            (ownerHealth != null && !ownerHealth.IsAlive))
            return;

        Transform origin = impactOrigin != null ? impactOrigin :
            ownerHealth != null ? ownerHealth.transform : transform;
        Vector3 position = origin.position + origin.rotation * positionOffset;
        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * rayStartHeight,
            Vector3.down, rayDistance, groundLayers, QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<EntityHealth>() != null ||
                hit.distance >= closestDistance)
                continue;
            closestDistance = hit.distance;
            position = hit.point;
        }

        // 지면을 찾지 못하면 지정한 타격 위치를 그대로 사용한다.
        position += Vector3.up * groundOffset;
        Quaternion rotation = Quaternion.Euler(0f, origin.eulerAngles.y, 0f) *
            explosionPrefab.transform.rotation * Quaternion.Euler(rotationOffset);
        CombatEffectSpawner.Spawn(explosionPrefab, position, rotation, null,
            Mathf.Max(0.01f, effectScale));
    }

    public void 바닥폭발()
    {
        OnGroundExplosion();
    }
}
