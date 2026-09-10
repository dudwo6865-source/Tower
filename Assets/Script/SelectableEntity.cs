using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

public enum SelectableEntityType
{
    Unit,
    Building
}

public class SelectableEntity : MonoBehaviour
{
    [Header("Selection")]
    [Tooltip("이 오브젝트의 종류입니다. 유닛 또는 건물.")]
    public SelectableEntityType entityType = SelectableEntityType.Unit;

    [Tooltip("소유 플레이어 ID입니다. 로컬 플레이어와 같아야 선택할 수 있습니다.")]
    public int ownerId = 1;

    [Tooltip("같은 타입 전체 선택(더블클릭)에 사용되는 타입 ID입니다. 예: tank, barracks")]
    public string entityTypeId = "unit";

    [Tooltip("체크하면 entityTypeId를 이 프리팹의 이름으로 자동 설정합니다. " +
        "직접 다른 값을 쓰고 싶다면(예: 여러 프리팹을 같은 그룹으로 묶기) 체크를 해제하세요. " +
        "UnitData의 entityTypeId가 채워져 있으면 런타임에는 그 값이 우선 적용됩니다.")]
    public bool autoAssignEntityTypeId = true;

    [Tooltip("선택/체력바 기준이 되는 콜라이더입니다. 비워두면 자식에서 자동으로 찾습니다. (Root 본의 콜라이더 등)")]
    public Collider selectionCollider;

    [Header("UI")]
    [Tooltip("선택 정보 패널 등에 표시할 초상화입니다. 비워두면 UnitData.portrait를 사용합니다.")]
    public Sprite portrait;

    [Header("Health")]
    [Tooltip("EntityHealth와 WorldHealthBar가 없으면 자동으로 추가합니다.")]
    public bool autoSetupHealth = true;

    public bool IsSelected { get; private set; }

    static readonly Color AttackRangeRingColor = new Color(1f, 1f, 1f, 0.35f);
    static readonly Color MinAttackRangeRingColor = new Color(0.05f, 0.05f, 0.05f, 0.55f);

    private SelectionRingIndicator ringIndicator;
    private SelectionRingIndicator attackRangeRingIndicator;
    private SelectionRingIndicator minAttackRangeRingIndicator;
    private EntityHealth cachedHealth;
    private CombatAIBase cachedCombatAI;
    private UnitAttacker cachedAttacker;
    private bool healthCached;
    private bool combatAICached;
    private bool attackerCached;

    public EntityHealth CachedHealth
    {
        get
        {
            if (!healthCached)
            {
                cachedHealth = GetComponent<EntityHealth>();
                healthCached = true;
            }

            return cachedHealth;
        }
    }

    public CombatAIBase CachedCombatAI
    {
        get
        {
            if (!combatAICached)
            {
                cachedCombatAI = GetComponent<CombatAIBase>();
                combatAICached = true;
            }

            return cachedCombatAI;
        }
    }

    public UnitAttacker CachedAttacker
    {
        get
        {
            if (!attackerCached)
            {
                cachedAttacker = GetComponent<UnitAttacker>();
                attackerCached = true;
            }

            return cachedAttacker;
        }
    }

    public Collider SelectionCollider
    {
        get
        {
            if (selectionCollider == null)
                selectionCollider = GetComponentInChildren<Collider>();

            return selectionCollider;
        }
    }

    public Bounds SelectionBounds
    {
        get
        {
            Collider collider = SelectionCollider;

            if (collider != null)
                return collider.bounds;

            return new Bounds(transform.position, Vector3.one);
        }
    }

    void Awake()
    {
        if (autoSetupHealth)
            EnsureHealthComponents();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!autoAssignEntityTypeId)
            return;

        // PrefabUtility 조회를 OnValidate 안에서 바로 하면, 에디터가 씬을 복원하는 시점 등에
        // 내부적으로 SendMessage를 유발해 콘솔에 경고가 뜬다("SendMessage cannot be called
        // during Awake, CheckConsistency, or OnValidate"). 다음 에디터 틱으로 미뤄서 피한다.
        EditorApplication.delayCall += DeferredApplyEntityTypeId;
    }

    void DeferredApplyEntityTypeId()
    {
        if (this == null || !autoAssignEntityTypeId)
            return;

        string prefabName = ResolvePrefabAssetName(gameObject);

        if (!string.IsNullOrEmpty(prefabName))
            entityTypeId = prefabName;
    }

    static string ResolvePrefabAssetName(GameObject target)
    {
        // 프리팹 에셋을 직접 편집 중이거나(프리팹 모드/프로젝트 창),
        // 씬에 배치된 프리팹 인스턴스인 경우 모두 원본 프리팹 에셋 경로를 찾는다.
        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);

        if (string.IsNullOrEmpty(assetPath))
            assetPath = AssetDatabase.GetAssetPath(target);

        return string.IsNullOrEmpty(assetPath)
            ? null
            : Path.GetFileNameWithoutExtension(assetPath);
    }
#endif

    void OnEnable()
    {
        SelectableRegistry.Register(this);

        // 스폰/활성화 시 현재 업그레이드 보너스를 반영한다(플레이어 소속만 적용됨).
        UpgradeManager.NotifySpawned(this);
    }

    void OnDisable()
    {
        IsSelected = false;

        if (ringIndicator != null)
            ringIndicator.SetVisible(false);

        if (attackRangeRingIndicator != null)
            attackRangeRingIndicator.SetVisible(false);

        if (minAttackRangeRingIndicator != null)
            minAttackRangeRingIndicator.SetVisible(false);

        if (UnitSelectionManager.Instance != null)
            UnitSelectionManager.Instance.NotifyEntityRemoved(this);

        SelectableRegistry.Unregister(this);
    }

    public bool CanBeSelectedBy(int localPlayerOwnerId)
    {
        return ownerId == localPlayerOwnerId;
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        SelectionRingIndicator ring = GetOrCreateRingIndicator();
        ring.SetColor(IsEnemyOfLocalPlayer()
            ? SelectionRingIndicator.EnemyRingColor
            : SelectionRingIndicator.AllyRingColor);
        ring.SetVisible(selected);

        UpdateAttackRangeRings(selected);

        WorldHealthBar healthBar = GetComponent<WorldHealthBar>();

        if (healthBar != null)
            healthBar.RefreshVisibility();
    }

    // 선택 시 이 대상의 공격 사거리(그리고 대포의 최소 사격 거리)를 바닥에 원으로 표시합니다.
    void UpdateAttackRangeRings(bool selected)
    {
        UnitAttacker attacker = CachedAttacker;

        if (attacker == null)
            return;

        bool showRange = selected && attacker.AttackRange > 0f;

        if (showRange || attackRangeRingIndicator != null)
            GetOrCreateAttackRangeRing().SetVisible(showRange);

        bool showMinRange = selected &&
            attacker.attackType == AttackType.Cannon &&
            attacker.minAttackRange > 0f;

        if (showMinRange || minAttackRangeRingIndicator != null)
            GetOrCreateMinAttackRangeRing().SetVisible(showMinRange);
    }

    SelectionRingIndicator GetOrCreateAttackRangeRing()
    {
        if (attackRangeRingIndicator != null)
            return attackRangeRingIndicator;

        GameObject ringObject = new GameObject("AttackRangeRing");
        ringObject.transform.SetParent(transform, false);

        attackRangeRingIndicator = ringObject.AddComponent<SelectionRingIndicator>();
        attackRangeRingIndicator.Initialize(CachedAttacker.AttackRange);
        attackRangeRingIndicator.SetColor(AttackRangeRingColor);

        return attackRangeRingIndicator;
    }

    SelectionRingIndicator GetOrCreateMinAttackRangeRing()
    {
        if (minAttackRangeRingIndicator != null)
            return minAttackRangeRingIndicator;

        GameObject ringObject = new GameObject("MinAttackRangeRing");
        ringObject.transform.SetParent(transform, false);

        minAttackRangeRingIndicator = ringObject.AddComponent<SelectionRingIndicator>();
        minAttackRangeRingIndicator.Initialize(CachedAttacker.minAttackRange);
        minAttackRangeRingIndicator.SetColor(MinAttackRangeRingColor);

        return minAttackRangeRingIndicator;
    }

    bool IsEnemyOfLocalPlayer()
    {
        UnitSelectionManager manager = UnitSelectionManager.Instance;
        return manager != null && ownerId != manager.localPlayerOwnerId;
    }

    void EnsureHealthComponents()
    {
        EntityHealth health = GetComponent<EntityHealth>();

        if (health == null)
        {
            health = gameObject.AddComponent<EntityHealth>();
            health.maxHealth =
                entityType == SelectableEntityType.Building ? 200f : 80f;
        }

        WorldHealthBar healthBar = GetComponent<WorldHealthBar>();

        if (healthBar == null)
            gameObject.AddComponent<WorldHealthBar>();

        if (GetComponent<HitFlash>() == null)
            gameObject.AddComponent<HitFlash>();
    }

    float GetRingRadius()
    {
        Collider collider = SelectionCollider;

        if (collider == null)
            return 1.5f;

        Vector3 extents = collider.bounds.extents;
        return Mathf.Max(extents.x, extents.z) * 1.1f;
    }

    SelectionRingIndicator GetOrCreateRingIndicator()
    {
        if (ringIndicator != null)
            return ringIndicator;

        GameObject ringObject = new GameObject("SelectionRing");
        ringObject.transform.SetParent(transform, false);

        ringIndicator = ringObject.AddComponent<SelectionRingIndicator>();
        ringIndicator.Initialize(GetRingRadius());

        return ringIndicator;
    }
}
