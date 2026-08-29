using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ProductionBuilding : MonoBehaviour
{
    [Header("Production")]
    [Tooltip("생산 규칙입니다. 비워두면 Building 또는 배치 데이터의 recipe를 사용합니다.")]
    public ProductionRecipe recipe;

    [Tooltip("유닛이 생성될 위치입니다. 비워두면 건물 위치에서 스폰합니다.")]
    public Transform spawnPoint;

    [Tooltip("건물 외곽에서 얼마나 떨어져 링을 배치할지(미터)입니다. 0이면 모두 스폰 포인트에 겹칩니다.")]
    public float spawnScatterSpacing = 1.2f;

    [Tooltip("건물 둘레 한 링에 배치할 슬롯 수입니다. 슬롯이 가득 차면 바깥 링으로 확장합니다.")]
    [Min(3)]
    public int spawnRingSlots = 8;

    [Tooltip("건물이 준비되면 자동으로 생산을 시작합니다.")]
    public bool autoStart = true;

    [Header("Rally")]
    [Tooltip("생산된 유닛이 이동할 렐리 포인트를 표시합니다.")]
    public bool hasRallyPoint;

    [Tooltip("렐리 포인트 월드 좌표입니다.")]
    public Vector3 rallyPointWorld;

    [Tooltip("렐리 포인트 링 색상입니다.")]
    public Color rallyMarkerColor = new Color(1f, 0.65f, 0.15f, 0.95f);

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

    void Awake()
    {
        selectableEntity = GetComponent<SelectableEntity>();
        buildingHealth = GetComponent<EntityHealth>();
    }

    void OnEnable()
    {
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

            float interval = recipe != null ? Mathf.Max(0f, recipe.spawnInterval) : 0f;
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
                yield return null;
                continue;
            }

            cycleElapsed += Time.deltaTime;
            yield return null;
        }

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
            combatAI.BeginManualMove();

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
