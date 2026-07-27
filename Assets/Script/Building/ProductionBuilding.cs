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

    public bool IsAtCapacity =>
        recipe != null &&
        recipe.maxAlivePerBuilding > 0 &&
        AliveCount >= recipe.maxAlivePerBuilding;

    SelectableEntity selectableEntity;
    EntityHealth buildingHealth;
    LineRenderer rallyMarker;
    DayNightCycle dayNightCycle;
    bool subscribedDayNight;
    bool builtAtRuntime;
    readonly List<ProducedUnitMarker> producedUnits = new List<ProducedUnitMarker>();

    // 버스트 방식은 진행도 개념이 없으므로 항상 0을 반환한다.
    public float ProductionProgress => 0f;

    public bool IsProductionActive => subscribedDayNight;

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

    // 건물이 죽는 즉시(파괴 연출 시작 시점) 낮/밤 이벤트 구독을 끊는다.
    // 파괴 대기 중 밤 전환 이벤트가 이미 죽은 건물에서 실행되는 것을 막는다.
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

    // 게임 도중(런타임)에 건설된 건물임을 표시한다. TowerPlacementController가 호출한다.
    // 이 건물은 생성된 낮에는 생산하지 않고, 다음 낮 전환부터 생산한다.
    public void MarkBuiltAtRuntime()
    {
        builtAtRuntime = true;
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
        if (recipe == null || recipe.unitPrefab == null)
            return;

        // 생산은 '낮 시작 버스트' 방식으로 동작한다.
        // 매 낮이 시작될 때(OnPhaseStarted(Day)) 한 번, 생산 한도까지 즉시 채운다.
        SubscribeDayNight();

        // 게임 시작 시 이미 존재하던(미리 배치된) 건물은 첫 낮에 즉시 생산한다.
        // 게임 도중 건설된 건물(builtAtRuntime)은 생성된 낮엔 생산하지 않고, 다음 낮 전환부터 생산한다.
        // (낮/밤 사이클이 없으면 이벤트를 받을 수 없으므로 예외적으로 즉시 생산.)
        if (!builtAtRuntime && (dayNightCycle == null || dayNightCycle.IsDay))
            SpawnBurstToLimit();
    }

    void SubscribeDayNight()
    {
        if (subscribedDayNight)
            return;

        if (dayNightCycle == null)
            dayNightCycle = FindObjectOfType<DayNightCycle>();

        if (dayNightCycle == null)
            return;

        dayNightCycle.OnPhaseStarted += HandlePhaseStarted;
        subscribedDayNight = true;
    }

    void UnsubscribeDayNight()
    {
        if (!subscribedDayNight || dayNightCycle == null)
            return;

        dayNightCycle.OnPhaseStarted -= HandlePhaseStarted;
        subscribedDayNight = false;
    }

    void HandlePhaseStarted(DayNightPhase phase)
    {
        // 이미 파괴된 건물에서 이벤트가 호출되면 무시한다(구독 해제 타이밍 방어).
        if (this == null)
            return;

        // 밤이 시작되면 남아 있는 생산 유닛을 건물로 불러들여 사라지게 한다.
        if (phase == DayNightPhase.Night)
        {
            RecallUnits();
            return;
        }

        if (buildingHealth != null && !buildingHealth.IsAlive)
            return;

        SpawnBurstToLimit();
    }

    // 밤 전환 시 호출: 살아 있는 생산 유닛을 생산 건물로 이동시킨 뒤,
    // 도착하면 사라지게 한다.
    void RecallUnits()
    {
        if (producedUnits.Count == 0)
            return;

        Vector3 target = spawnPoint != null
            ? spawnPoint.position
            : transform.position;

        float arriveDistance = 1.5f;

        if (selectableEntity != null)
        {
            Bounds bounds = selectableEntity.SelectionBounds;
            arriveDistance = Mathf.Max(
                1.5f,
                Mathf.Max(bounds.extents.x, bounds.extents.z) + 1f);
        }

        // Recall 도중 Destroy로 목록이 바뀔 수 있으므로 스냅샷을 순회한다.
        ProducedUnitMarker[] snapshot = producedUnits.ToArray();

        foreach (ProducedUnitMarker marker in snapshot)
        {
            if (marker != null)
                marker.Recall(target, arriveDistance);
        }
    }

    // 생산 한도(maxAlivePerBuilding)까지 생산시간 없이 즉시 유닛을 채운다.
    void SpawnBurstToLimit()
    {
        if (recipe == null || recipe.unitPrefab == null)
            return;

        // 한도가 없으면(<=0) 무한 루프를 막기 위해 한 마리만 생산한다.
        if (recipe.maxAlivePerBuilding <= 0)
        {
            TrySpawnUnit();
            return;
        }

        // 스폰 실패가 반복될 때의 무한 루프를 막는 안전장치.
        int safety = recipe.maxAlivePerBuilding + 4;

        while (!IsAtCapacity && safety-- > 0)
        {
            if (!TrySpawnUnit())
                break;
        }
    }

    public void StopProduction()
    {
        UnsubscribeDayNight();
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

        if (IsAtCapacity)
            return false;

        int ownerId = selectableEntity != null ? selectableEntity.ownerId : 1;
        Vector3 spawnPosition = spawnPoint != null
            ? spawnPoint.position
            : transform.position;

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
            localPlayerOwnerId);

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
