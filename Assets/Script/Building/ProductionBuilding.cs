using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ProductionBuilding : MonoBehaviour
{
    [Header("생산")]
    [Label("생산 레시피")]
    [Tooltip("생산 규칙입니다. 비워두면 Building 또는 배치 데이터의 recipe를 사용합니다.")]
    public ProductionRecipe recipe;

    [Label("스폰 위치")]
    [Tooltip("유닛이 생성될 위치입니다. 비워두면 건물 위치에서 스폰합니다.")]
    public Transform spawnPoint;

    [Label("스폰 링 간격")]
    [Tooltip("건물 외곽에서 얼마나 떨어져 링을 배치할지(미터)입니다. 0이면 모두 스폰 포인트에 겹칩니다.")]
    public float spawnScatterSpacing = 1.2f;

    [Label("링당 슬롯 수")]
    [Tooltip("건물 둘레 한 링에 배치할 슬롯 수입니다. 슬롯이 가득 차면 바깥 링으로 확장합니다.")]
    [Min(3)]
    public int spawnRingSlots = 8;

    [Label("자동 생산 시작")]
    [Tooltip("건물이 준비되면 자동으로 생산을 시작합니다.")]
    public bool autoStart = true;

    [Header("집결지")]
    [Label("집결지 사용")]
    [Tooltip("생산된 유닛이 이동할 렐리 포인트를 표시합니다.")]
    public bool hasRallyPoint;

    [Label("집결지 좌표")]
    [Tooltip("렐리 포인트 월드 좌표입니다.")]
    public Vector3 rallyPointWorld;

    [Label("집결지 표시 색")]
    [Tooltip("렐리 포인트 링 색상입니다.")]
    public Color rallyMarkerColor = new Color(1f, 0.65f, 0.15f, 0.95f);

    [Label("집결지 표시 반지름")]
    [Tooltip("렐리 포인트 링 반지름입니다.")]
    public float rallyMarkerRadius = 1.1f;

    public bool HasRallyPoint => hasRallyPoint;

    public int AliveCount { get; private set; }

    public bool IsAtCapacity
    {
        get
        {
            int cap = EffectiveMaxAlive();
            return cap > 0 && AliveCount >= cap;
        }
    }

    // U04 자동 생산 모듈: 생산 간격(시간)이 아니라 초당 생산 횟수(rate) 기준으로 적용한다.
    float GetEffectiveSpawnInterval()
    {
        float interval = recipe.spawnInterval;

        if (RelicManager.Instance == null || selectableEntity == null || interval <= 0f)
            return interval;

        float rate = 1f / interval;
        float modifiedRate = RelicManager.Instance.GetModifiedValue(
            RelicEffectType.ProductionSpeed,
            selectableEntity.ownerId,
            rate);

        return modifiedRate > 0.0001f ? 1f / modifiedRate : interval;
    }

    // 업그레이드(건물 유닛 스폰 수) 보너스를 반영한 최대 생존 수입니다.
    // recipe 값이 0 이하면 무제한을 의미하므로 0을 반환합니다.
    int EffectiveMaxAlive()
    {
        if (recipe == null || recipe.maxAlivePerBuilding <= 0)
            return 0;

        if (UpgradeManager.Instance == null || selectableEntity == null)
            return recipe.maxAlivePerBuilding;

        return UpgradeManager.Instance.GetModifiedSpawnCount(
            selectableEntity.ownerId,
            recipe.maxAlivePerBuilding);
    }

    SelectableEntity selectableEntity;
    EntityHealth buildingHealth;
    LineRenderer rallyMarker;
    Coroutine productionRoutine;
    float cycleElapsed;
    float cycleDuration;
    readonly List<ProducedUnitMarker> producedUnits = new List<ProducedUnitMarker>();

    public int MaxAliveCount => EffectiveMaxAlive();

    public bool IsProducing =>
        productionRoutine != null &&
        !IsAtCapacity &&
        !BuildingConstructionGate.IsFeatureLockedOn(this);

    public float ProductionProgress
    {
        get
        {
            if (!IsProducing || cycleDuration <= 0f)
                return 0f;

            return Mathf.Clamp01(cycleElapsed / cycleDuration);
        }
    }

    public float ProductionRemainingTime
    {
        get
        {
            if (!IsProducing)
                return 0f;

            return Mathf.Max(0f, cycleDuration - cycleElapsed);
        }
    }

    public bool IsProductionActive => IsProducing;

    // Watt가 부족해 생산 진행이 멈춰 있는지 여부입니다.
    public bool IsWaitingForWatt => IsProducing && waitingForWatt;

    // 생산 중일 때 초당 소모하는 Watt입니다. 소모 대상이 아니면 0입니다.
    public float WattCostPerSecond =>
        recipe != null && ConsumesWatt() ? Mathf.Max(0f, recipe.wattCostPerSecond) : 0f;

    // 지금 실제로 소모 중인 초당 Watt입니다. 생산 중이 아니면 0입니다.
    public float CurrentWattDrainPerSecond => IsProducing ? WattCostPerSecond : 0f;

    bool waitingForWatt;

    // 활성화된 생산 건물 목록. WattManager가 전체 초당 소모량을 합산할 때 쓴다.
    static readonly List<ProductionBuilding> activeBuildings = new List<ProductionBuilding>();

    // 지금 생산 중인 모든 건물의 초당 Watt 소모량 합계를 구한다.
    public static float GetTotalWattDrainPerSecond()
    {
        float total = 0f;

        for (int i = 0; i < activeBuildings.Count; i++)
        {
            ProductionBuilding building = activeBuildings[i];

            if (building != null)
                total += building.CurrentWattDrainPerSecond;
        }

        return total;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        activeBuildings.Clear();
    }

    void Awake()
    {
        selectableEntity = GetComponent<SelectableEntity>();
        buildingHealth = GetComponent<EntityHealth>();
    }

    void OnEnable()
    {
        if (!activeBuildings.Contains(this))
            activeBuildings.Add(this);

        if (buildingHealth != null)
            buildingHealth.OnDied += HandleBuildingDied;

        if (autoStart)
            StartCoroutine(BeginWhenReady());
    }

    void Start()
    {
        if (UnitSelectionManager.Instance != null)
            UnitSelectionManager.Instance.OnSelectionChanged += HandleSelectionChanged;

        UpdateRallyMarker();
    }

    void OnDisable()
    {
        activeBuildings.Remove(this);

        if (buildingHealth != null)
            buildingHealth.OnDied -= HandleBuildingDied;

        if (UnitSelectionManager.Instance != null)
            UnitSelectionManager.Instance.OnSelectionChanged -= HandleSelectionChanged;

        StopProduction();

        if (rallyMarker != null)
            rallyMarker.enabled = false;
    }

    void HandleBuildingDied()
    {
        StopProduction();
    }

    void HandleSelectionChanged()
    {
        UpdateRallyMarker();
    }

    void OnDestroy()
    {
        if (rallyMarker != null)
            Destroy(rallyMarker.gameObject);
    }

    public void SetRecipe(ProductionRecipe newRecipe)
    {
        recipe = newRecipe;
    }

    public void SetRallyPoint(Vector3 worldPoint)
    {
        rallyPointWorld = UnitSpawnUtility.SampleNavMeshPosition(worldPoint);
        hasRallyPoint = true;
        UpdateRallyMarker();
    }

    public void ClearRallyPoint()
    {
        hasRallyPoint = false;
        UpdateRallyMarker();
    }

    public void BeginProduction()
    {
        if (productionRoutine != null)
            return;

        if (recipe == null || recipe.unitPrefab == null)
            return;

        productionRoutine = StartCoroutine(ProductionLoop());
    }

    public void StopProduction()
    {
        if (productionRoutine == null)
            return;

        StopCoroutine(productionRoutine);
        productionRoutine = null;
        cycleElapsed = 0f;
        cycleDuration = 0f;
        waitingForWatt = false;
    }

    // WattManager는 로컬 플레이어의 자원이므로, 로컬 플레이어 소속 건물만 Watt를 소모한다.
    bool ConsumesWatt()
    {
        if (WattManager.Instance == null)
            return false;

        if (selectableEntity == null || UnitSelectionManager.Instance == null)
            return true;

        return selectableEntity.ownerId == UnitSelectionManager.Instance.localPlayerOwnerId;
    }

    // 이번 프레임의 생산 진행분만큼 Watt를 차감한다. 부족하면 false를 반환한다.
    bool TryConsumeWatt(float deltaTime)
    {
        float cost = WattCostPerSecond * deltaTime;

        if (cost <= 0f)
            return true;

        return WattManager.Instance.TrySpendAmount(cost);
    }

    IEnumerator ProductionLoop()
    {
        while (BuildingConstructionGate.IsFeatureLockedOn(this))
            yield return null;

        if (recipe != null && recipe.initialSpawnDelay > 0f)
        {
            yield return RunProductionCycle(recipe.initialSpawnDelay);

            if (buildingHealth != null && !buildingHealth.IsAlive)
                yield break;
        }

        while (enabled)
        {
            if (buildingHealth != null && !buildingHealth.IsAlive)
                yield break;

            if (BuildingConstructionGate.IsFeatureLockedOn(this) || IsAtCapacity)
            {
                cycleElapsed = 0f;
                cycleDuration = 0f;
                yield return null;
                continue;
            }

            float interval = recipe != null ? Mathf.Max(0f, GetEffectiveSpawnInterval()) : 0f;
            yield return RunProductionCycle(interval);

            if (buildingHealth != null && !buildingHealth.IsAlive)
                yield break;

            TrySpawnUnit();
        }
    }

    IEnumerator RunProductionCycle(float duration)
    {
        cycleDuration = Mathf.Max(0f, duration);
        cycleElapsed = 0f;

        if (cycleDuration <= 0f)
            yield break;

        while (cycleElapsed < cycleDuration)
        {
            if (buildingHealth != null && !buildingHealth.IsAlive)
                yield break;

            if (BuildingConstructionGate.IsFeatureLockedOn(this) || IsAtCapacity)
            {
                waitingForWatt = false;
                yield return null;
                continue;
            }

            // Watt가 부족하면 진행률을 올리지 않고 다음 프레임에 다시 시도한다.
            waitingForWatt = !TryConsumeWatt(Time.deltaTime);

            if (!waitingForWatt)
                cycleElapsed += Time.deltaTime;

            yield return null;
        }

        waitingForWatt = false;
        cycleElapsed = cycleDuration;
    }

    // 건물 둘레를 각도별로 나눠 배치한다. 슬롯이 차면 바깥 링으로 한 칸씩 확장한다.
    Vector3 GetRingOffset(int index)
    {
        if (spawnScatterSpacing <= 0f)
            return Vector3.zero;

        int slots = Mathf.Max(3, spawnRingSlots);
        int ring = index / slots;
        int slot = index % slots;
        float angleStep = 360f / slots;
        float angle = slot * angleStep + (ring % 2) * (angleStep * 0.5f);
        float radius = GetSpawnRingRadius() + ring * spawnScatterSpacing;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        return Quaternion.Euler(0f, angle, 0f) * forward * radius;
    }

    float GetSpawnRingRadius()
    {
        float buildingRadius = 1f;

        if (selectableEntity != null)
        {
            Bounds bounds = selectableEntity.SelectionBounds;
            buildingRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        }
        else if (MapGrid.Instance != null)
        {
            Vector2Int footprint = GridFootprint.ResolveFootprintCells(gameObject);
            buildingRadius = Mathf.Max(footprint.x, footprint.y) * MapGrid.Instance.cellSize * 0.5f;
        }

        return buildingRadius + spawnScatterSpacing;
    }

    public void NotifyUnitReleased(ProducedUnitMarker marker)
    {
        if (marker == null)
            return;

        producedUnits.Remove(marker);
        AliveCount = Mathf.Max(0, AliveCount - 1);
    }

    IEnumerator BeginWhenReady()
    {
        yield return null;

        if (buildingHealth != null && !buildingHealth.IsAlive)
            yield break;

        if (recipe == null || recipe.unitPrefab == null)
            yield break;

        BeginProduction();
    }

    bool TrySpawnUnit()
    {
        if (recipe == null || recipe.unitPrefab == null)
            return false;

        if (IsAtCapacity || BuildingConstructionGate.IsFeatureLockedOn(this))
            return false;

        int ownerId = selectableEntity != null ? selectableEntity.ownerId : 1;
        Vector3 basePosition = spawnPoint != null
            ? spawnPoint.position
            : transform.position;

        // 건물 둘레 각도 슬롯에 배치. 층 밖이면 건물 쪽으로 되돌린다.
        Vector3 scatterHint = basePosition + GetRingOffset(producedUnits.Count);

        if (!UnitSpawnUtility.TryResolveSpawnPosition(
                scatterHint,
                basePosition,
                out Vector3 spawnPosition))
        {
            spawnPosition = basePosition;
        }

        Quaternion spawnRotation = spawnPoint != null
            ? spawnPoint.rotation
            : transform.rotation;

        int localPlayerOwnerId = UnitSelectionManager.Instance != null
            ? UnitSelectionManager.Instance.localPlayerOwnerId
            : ownerId;

        GameObject unitObject = UnitSpawnUtility.SpawnUnit(
            recipe.unitPrefab,
            spawnPosition,
            spawnRotation,
            ownerId,
            localPlayerOwnerId,
            resampleNavMesh: false);

        if (unitObject == null)
            return false;

        ProducedUnitMarker marker = unitObject.GetComponent<ProducedUnitMarker>();

        if (marker == null)
            marker = unitObject.AddComponent<ProducedUnitMarker>();

        marker.Initialize(this);
        producedUnits.Add(marker);
        AliveCount++;
        SendSpawnedUnitToRally(unitObject);
        return true;
    }

    void SendSpawnedUnitToRally(GameObject unitObject)
    {
        if (!hasRallyPoint || unitObject == null)
            return;

        UnityEngine.AI.NavMeshAgent agent = unitObject.GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (agent == null || !agent.isActiveAndEnabled)
            return;

        Vector3 destination = UnitSpawnUtility.SampleNavMeshPosition(rallyPointWorld);

        UnitCombatAI combatAI = unitObject.GetComponent<UnitCombatAI>();

        if (combatAI != null)
            combatAI.BeginManualMove(destination);

        if (!GridMovement.TrySetAgentDestination(agent, destination))
            return;

        agent.isStopped = false;
    }

    void UpdateRallyMarker()
    {
        if (!hasRallyPoint || !IsSelected())
        {
            if (rallyMarker != null)
                rallyMarker.enabled = false;

            return;
        }

        EnsureRallyMarker();
        rallyMarker.enabled = true;
        rallyMarker.transform.position = rallyPointWorld + Vector3.up * 0.12f;
    }

    bool IsSelected()
    {
        return selectableEntity != null && selectableEntity.IsSelected;
    }

    void EnsureRallyMarker()
    {
        if (rallyMarker != null)
            return;

        GameObject markerObject = new GameObject("RallyPointMarker");
        markerObject.transform.SetParent(null, false);
        rallyMarker = markerObject.AddComponent<LineRenderer>();
        rallyMarker.material = new Material(Shader.Find("Sprites/Default"));
        rallyMarker.startColor = rallyMarkerColor;
        rallyMarker.endColor = rallyMarkerColor;
        rallyMarker.startWidth = 0.1f;
        rallyMarker.endWidth = 0.1f;
        rallyMarker.loop = true;
        rallyMarker.useWorldSpace = false;
        rallyMarker.positionCount = 32;

        for (int i = 0; i < rallyMarker.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / rallyMarker.positionCount;

            rallyMarker.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * rallyMarkerRadius,
                    0f,
                    Mathf.Sin(angle) * rallyMarkerRadius));
        }
    }
}
