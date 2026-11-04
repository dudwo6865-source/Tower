using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnitAttacker), typeof(EntityHealth))]
[AddComponentMenu("게임/전투/BossSkillController")]
public class BossSkillController : MonoBehaviour
{
    [Header("강력 내려찍기")]
    [Label("스킬 사용")] public bool skillEnabled = true;
    [Label("쿨타임")][Min(0.1f)] public float cooldown = 10f;
    [Label("첫 사용 대기 시간")][Min(0f)] public float initialDelay = 5f;
    [Label("발동 거리")]
    [Tooltip("일반 공격과 동일하게 보스와 표적의 몸체 사이 수평 거리로 판단합니다. 중심 사이 거리가 아닙니다.")]
    [Min(0.1f)] public float castRange = 7f;
    [Label("일반 공격력 배율")][Min(0f)] public float damageMultiplier = 3f;
    [Label("폭발 반경")][Min(0.1f)] public float radius = 3f;
    [Label("타격 높이 허용 거리")][Min(0f)] public float heightTolerance = 2f;
    [Label("최소 준비 시간")][Min(0f)] public float preparationTime = 1f;
    [Label("이벤트 대기 제한")]
    [Tooltip("타격 이벤트가 누락되면 이 시간이 지난 뒤 스킬을 취소합니다. 최소 준비 시간보다 길게 설정합니다.")]
    [Min(0.1f)] public float eventTimeout = 4f;
    [Label("타격 후 대기 시간")][Min(0f)] public float recoveryTime = 0.8f;

    [Header("애니메이션과 이펙트")]
    [Label("애니메이터")] public Animator skillAnimator;
    [Label("스킬 상태 경로")] public string skillState = "Base Layer.BossSlam";
    [Label("일반 공격 상태 경로")]
    [Tooltip("이 상태의 일반 공격과 복귀 전환이 끝난 뒤 스킬을 시작합니다.")]
    public string basicAttackState = "Base Layer.Attack";
    [Label("복귀 상태 경로")] public string idleState = "Base Layer.Idle";
    [Label("폭발 이펙트 프리팹")] public GameObject explosionPrefab;
    [Label("폭발 크기 배율")][Min(0.01f)] public float explosionScale = 1.5f;

    [Header("스킬 타격 사운드")]
    [Label("타격 사운드")]
    [Tooltip("스킬 타격 이벤트에서 한 번 재생합니다. 같은 오브젝트의 UnitSound에 설정한 볼륨과 거리 감쇠를 사용합니다.")]
    public AudioClip hitSound;
    [Label("타격 사운드 볼륨")]
    [Tooltip("UnitSound의 전체 볼륨에 곱하는 배율입니다.")]
    [Range(0f, 2f)] public float hitSoundVolume = 1f;

    [Header("바닥 예고 표시")]
    [Label("예고 표시 머티리얼")] public Material warningMaterial;
    [Label("예고 선 굵기")][Min(0.01f)] public float warningWidth = 0.12f;
    [Label("예고 원 분할 수")][Range(16, 128)] public int warningSegments = 64;
    [Label("예고 원 채우기")]
    [Tooltip("스킬 클립 시작부터 타격 이벤트까지 중심에서 바깥쪽으로 원을 채웁니다.")]
    public bool fillWarningCircle = true;
    [Label("채우기 머티리얼")]
    [Tooltip("비워두면 예고 표시 머티리얼을 사용합니다. 투명 머티리얼을 연결하면 바닥이 비쳐 보입니다.")]
    public Material warningFillMaterial;
    [Label("채우기 불투명도")]
    [Tooltip("머티리얼 색상의 알파에 곱합니다. 투명 머티리얼에서 적용됩니다.")]
    [Range(0f, 1f)] public float warningFillOpacity = 0.3f;
    [Label("지면 레이어")] public LayerMask groundLayers = 1;
    [Label("지면 탐색 시작 높이")][Min(0f)] public float rayStartHeight = 5f;
    [Label("지면 탐색 거리")][Min(0.01f)] public float rayDistance = 20f;
    [Label("지면에서 띄울 높이")] public float groundOffset = 0.05f;

    public bool IsActive => timing.IsActive;
    public bool WaitingForBasicAttack { get; private set; }
    public float CooldownRemaining => Mathf.Max(0f, timing.ReadyAt - Time.time);
    public Vector3 ImpactPosition { get; private set; }
    readonly BossSkillTiming timing = new BossSkillTiming();
    readonly List<EntityHealth> victims = new List<EntityHealth>();
    UnitAttacker attacker;
    EntityHealth health;
    SelectableEntity owner;
    BossCombatAI boss;
    UnitCombatAI unit;
    UnitSound unitSound;
    UnitAnimator[] unitAnimators;
    GameObject warning;
    Mesh warningFillMesh;
    Vector3[] warningFillEdge;
    Vector3[] warningFillVertices;
    readonly List<AnimatorClipInfo> warningClips = new List<AnimatorClipInfo>();
    AnimationClip warningClip;
    float warningHitNormalizedTime;
    float castRadius;
    float castDamage;

    void Awake()
    {
        attacker = GetComponent<UnitAttacker>();
        health = GetComponent<EntityHealth>();
        owner = GetComponent<SelectableEntity>();
        boss = GetComponent<BossCombatAI>();
        unit = GetComponent<UnitCombatAI>();
        unitSound = GetComponent<UnitSound>();
        unitAnimators = GetComponentsInChildren<UnitAnimator>(true);
        if (skillAnimator == null) skillAnimator = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        timing.Reset(Time.time, initialDelay);
        health.OnDied += HandleDied;
    }

    void OnDisable()
    {
        health.OnDied -= HandleDied;
        CancelSkill();
    }

    void HandleDied() { CancelSkill(); }

    bool CanContinue()
    {
        if (!isActiveAndEnabled || !skillEnabled || health == null || !health.IsAlive ||
            (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver))
            return false;

        // 보스 AI는 기존 둥지·후퇴 제한을 유지하고, 일반 유닛 AI는 둥지 없이 사용한다.
        if (boss != null && boss.isActiveAndEnabled)
            return boss.Nest != null && boss.Nest.isActiveAndEnabled &&
                boss.State != BossSortieRules.Phase.Retreat;
        return unit != null && unit.isActiveAndEnabled;
    }

    void Update()
    {
        if (WaitingForBasicAttack && !CanContinue()) WaitingForBasicAttack = false;
        if (IsActive && (!CanContinue() || timing.HasExpired(Time.time))) CancelSkill();
    }

    void LateUpdate()
    {
        if (warningFillMesh == null || timing.State != BossSkillTiming.Phase.Preparing ||
            skillAnimator == null || !skillAnimator.isActiveAndEnabled)
            return;

        // 실제 애니메이션 진행도를 읽으므로 피치가 아닌 Animator 재생 속도와 일시정지를 따른다.
        int stateHash = Animator.StringToHash(skillState);
        AnimatorStateInfo state = skillAnimator.GetCurrentAnimatorStateInfo(0);
        if (skillAnimator.IsInTransition(0) && skillAnimator.GetNextAnimatorStateInfo(0).fullPathHash == stateHash)
        {
            state = skillAnimator.GetNextAnimatorStateInfo(0);
            skillAnimator.GetNextAnimatorClipInfo(0, warningClips);
        }
        else if (state.fullPathHash == stateHash)
            skillAnimator.GetCurrentAnimatorClipInfo(0, warningClips);
        else
            return;

        AnimationClip clip = null;
        float weight = -1f;
        foreach (AnimatorClipInfo info in warningClips)
        {
            if (info.weight <= weight) continue;
            clip = info.clip;
            weight = info.weight;
        }
        if (clip == null) return;
        if (warningClip != clip)
        {
            warningClip = clip;
            warningHitNormalizedTime = -1f;
            foreach (AnimationEvent clipEvent in clip.events)
            {
                if (clipEvent.functionName != "보스스킬타격" && clipEvent.functionName != "OnBossSkillHit" &&
                    clipEvent.functionName != "OnSkillHit") continue;
                float hitTime = clip.length > 0f ? clipEvent.time / clip.length : 0f;
                if (warningHitNormalizedTime < 0f || hitTime < warningHitNormalizedTime)
                    warningHitNormalizedTime = hitTime;
            }
            if (warningHitNormalizedTime < 0f)
                Debug.LogWarning("BossSkillController: 스킬 클립에 보스스킬타격 이벤트가 없어 예고 원을 채울 수 없습니다.", this);
        }
        if (warningHitNormalizedTime < 0f) return;
        float progress = warningHitNormalizedTime > 0f
            ? Mathf.Clamp01(state.normalizedTime / warningHitNormalizedTime) : 1f;
        UpdateWarningFill(progress);
    }

    public bool TryStartSkill(SelectableEntity target)
    {
        WaitingForBasicAttack = false;
        if (Time.deltaTime <= 0f || !CanContinue() || IsActive || Time.time < timing.ReadyAt || owner == null ||
            target == null || !target.isActiveAndEnabled || target.ownerId == owner.ownerId ||
            target.CachedHealth == null || !target.CachedHealth.IsAlive || !attacker.CanEngage(target))
            return false;
        Vector3 center = target.SelectionBounds.center;
        Vector3 delta = center - transform.position;
        delta.y = 0f;
        if (attacker.GetRangeGap(target) > Mathf.Max(0f, castRange) || skillAnimator == null ||
            !skillAnimator.isActiveAndEnabled || !skillAnimator.HasState(0, Animator.StringToHash(skillState)) ||
            !skillAnimator.HasState(0, Animator.StringToHash(idleState))) return false;
        // 타격 전에는 예약 피해를 유지하고, 타격 후에도 공격 모션과 복귀 전환을 기다린다.
        // 기다리는 동안 AI는 다음 일반 공격을 시작하지 않는다. 쿨타임도 아직 소비하지 않는다.
        if (IsBasicAttackPlaying())
        {
            WaitingForBasicAttack = true;
            return false;
        }
        if (!timing.TryBegin(Time.time, cooldown, preparationTime, eventTimeout)) return false;

        // 예고 시작 시 좌표와 위력을 고정한다. 이후 표적을 따라가지 않는다.
        ImpactPosition = GroundPoint(new Vector3(center.x, target.transform.position.y, center.z));
        castRadius = Mathf.Max(0.1f, radius);
        castDamage = attacker.EffectiveAttackDamage * Mathf.Max(0f, damageMultiplier);
        attacker.CancelPendingAttack();
        attacker.NotifyAttackInterrupted();
        foreach (UnitAnimator unitAnimator in unitAnimators) unitAnimator.PrepareForSkill();
        if (delta.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(delta);
        ShowWarning();
        skillAnimator.Play(Animator.StringToHash(skillState), 0, 0f);
        return true;
    }

    bool IsBasicAttackPlaying()
    {
        if (attacker.HasPendingAttack) return true;
        int attackHash = Animator.StringToHash(basicAttackState);
        AnimatorStateInfo current = skillAnimator.GetCurrentAnimatorStateInfo(0);
        if (skillAnimator.IsInTransition(0))
        {
            AnimatorStateInfo next = skillAnimator.GetNextAnimatorStateInfo(0);
            if (current.fullPathHash == attackHash || next.fullPathHash == attackHash) return true;
        }
        // 종료 상태에 머무르는 비반복 클립은 마지막 프레임 이후 스킬로 넘어갈 수 있다.
        return current.fullPathHash == attackHash && (current.loop || current.normalizedTime < 1f);
    }

    public void OnSkillHit(AnimationEvent animationEvent)
    {
        // 일반 공격이나 전환 중 이전 클립의 이벤트는 스킬 타격으로 처리하지 않는다.
        if (!CanContinue() || Time.timeScale <= 0f || animationEvent == null ||
            animationEvent.animatorStateInfo.fullPathHash != Animator.StringToHash(skillState) ||
            !timing.TryHit(Time.time, recoveryTime)) return;
        ClearWarning();
        if (unitSound != null) unitSound.PlaySkillHit(hitSound, hitSoundVolume);
        if (explosionPrefab != null)
            CombatEffectSpawner.Spawn(explosionPrefab, ImpactPosition,
                explosionPrefab.transform.rotation, null, Mathf.Max(0.01f, explosionScale));

        // 사망 처리로 레지스트리가 바뀌어도 누락되지 않도록 먼저 타격 대상을 모은다.
        victims.Clear();
        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (entity == null || !entity.isActiveAndEnabled || entity == owner ||
                entity.ownerId == owner.ownerId || entity.CachedHealth == null || !entity.CachedHealth.IsAlive) continue;
            Vector3 nearest = entity.SelectionBounds.ClosestPoint(ImpactPosition);
            Vector3 gap = nearest - ImpactPosition;
            if (Mathf.Abs(gap.y) > heightTolerance) continue;
            gap.y = 0f;
            if (gap.sqrMagnitude <= castRadius * castRadius && !victims.Contains(entity.CachedHealth))
                victims.Add(entity.CachedHealth);
        }
        foreach (EntityHealth victim in victims)
            if (victim != null && victim.IsAlive && castDamage > 0f) victim.TakeDamage(castDamage, owner);
        victims.Clear();
    }

    public void CancelSkill()
    {
        WaitingForBasicAttack = false;
        bool wasActive = IsActive;
        timing.Cancel();
        ClearWarning();
        if (wasActive && health != null && health.IsAlive && skillAnimator != null &&
            skillAnimator.isActiveAndEnabled && skillAnimator.HasState(0, Animator.StringToHash(idleState)))
            skillAnimator.Play(Animator.StringToHash(idleState), 0, 0f);
    }

    Vector3 GroundPoint(Vector3 point)
    {
        float distance = float.PositiveInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * rayStartHeight,
            Vector3.down, rayDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance >= distance || hit.collider.GetComponentInParent<EntityHealth>() != null) continue;
            distance = hit.distance;
            point = hit.point;
        }
        return point + Vector3.up * groundOffset;
    }

    void ShowWarning()
    {
        ClearWarning();
        if (warningMaterial == null) return;
        warning = new GameObject("보스 내려찍기 예고");
        warning.transform.position = ImpactPosition;
        LineRenderer line = warning.AddComponent<LineRenderer>();
        line.sharedMaterial = warningMaterial;
        line.useWorldSpace = true;
        line.loop = true;
        line.widthMultiplier = warningWidth;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = Mathf.Clamp(warningSegments, 16, 128);
        warningFillEdge = new Vector3[line.positionCount + 2];
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            Vector3 point = ImpactPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * castRadius;
            Vector3 edge = GroundPoint(point - Vector3.up * groundOffset);
            line.SetPosition(i, edge);
            warningFillEdge[i + 1] = edge - ImpactPosition;
        }
        warningFillEdge[warningFillEdge.Length - 1] = warningFillEdge[1];
        if (fillWarningCircle) CreateWarningFill();
    }

    void CreateWarningFill()
    {
        GameObject fill = new GameObject("예고 원 채우기");
        fill.transform.SetParent(warning.transform, false);
        MeshFilter filter = fill.AddComponent<MeshFilter>();
        MeshRenderer renderer = fill.AddComponent<MeshRenderer>();
        Material material = warningFillMaterial != null ? warningFillMaterial : warningMaterial;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        foreach (string colorName in new[] { "_BaseColor", "_Color" })
        {
            if (!material.HasProperty(colorName)) continue;
            Color color = material.GetColor(colorName);
            color.a *= warningFillOpacity;
            properties.SetColor(colorName, color);
        }
        renderer.SetPropertyBlock(properties);

        warningFillVertices = new Vector3[warningFillEdge.Length];
        Vector3[] normals = new Vector3[warningFillEdge.Length];
        Vector2[] uv = new Vector2[warningFillEdge.Length];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Vector3.up;
            uv[i] = new Vector2(0.5f + warningFillEdge[i].x / (castRadius * 2f),
                0.5f + warningFillEdge[i].z / (castRadius * 2f));
        }
        int[] triangles = new int[(warningFillEdge.Length - 2) * 3];
        for (int i = 0; i < triangles.Length / 3; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 2;
            triangles[i * 3 + 2] = i + 1;
        }
        warningFillMesh = new Mesh { name = "보스 스킬 예고 채움" };
        warningFillMesh.MarkDynamic();
        warningFillMesh.vertices = warningFillVertices;
        warningFillMesh.triangles = triangles;
        warningFillMesh.normals = normals;
        warningFillMesh.uv = uv;
        filter.sharedMesh = warningFillMesh;
        UpdateWarningFill(0f);
    }

    void UpdateWarningFill(float progress)
    {
        // 지면 탐색은 생성 때만 하고, 재생 중에는 저장한 가장자리 좌표로 원을 키운다.
        for (int i = 1; i < warningFillVertices.Length; i++)
            warningFillVertices[i] = warningFillEdge[i] * progress;
        warningFillMesh.vertices = warningFillVertices;
        warningFillMesh.RecalculateBounds();
    }

    void ClearWarning()
    {
        if (warning != null) { warning.SetActive(false); Destroy(warning); }
        warning = null;
        if (warningFillMesh != null) Destroy(warningFillMesh);
        warningFillMesh = null;
        warningFillEdge = null;
        warningFillVertices = null;
        warningClip = null;
        warningHitNormalizedTime = -1f;
        warningClips.Clear();
    }
}
