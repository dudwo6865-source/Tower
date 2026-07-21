using System.Collections;
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
    Coroutine productionRoutine;
    float productionTimer;
    LineRenderer rallyMarker;

    public float ProductionProgress
    {
        get
        {
            if (recipe == null || recipe.spawnInterval <= 0f)
                return 0f;

            return Mathf.Clamp01(productionTimer / recipe.spawnInterval);
        }
    }

    public bool IsProductionActive => productionRoutine != null;

    void Awake()
    {
        selectableEntity = GetComponent<SelectableEntity>();
        buildingHealth = GetComponent<EntityHealth>();
    }

    void OnEnable()
    {
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
        if (UnitSelectionManager.Instance != null)
            UnitSelectionManager.Instance.OnSelectionChanged -= HandleSelectionChanged;

        StopProduction();

        if (rallyMarker != null)
            rallyMarker.enabled = false;
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
    }

    public void NotifyUnitReleased(ProducedUnitMarker marker)
    {
        if (marker == null)
            return;

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

    IEnumerator ProductionLoop()
    {
        if (recipe.initialSpawnDelay > 0f)
            yield return new WaitForSeconds(recipe.initialSpawnDelay);

        float timer = 0f;
        productionTimer = 0f;

        while (enabled)
        {
            if (buildingHealth != null && !buildingHealth.IsAlive)
                yield break;

            if (IsAtCapacity)
            {
                timer = 0f;
                productionTimer = 0f;
                yield return null;
                continue;
            }

            timer += Time.deltaTime;
            productionTimer = timer;

            if (timer < recipe.spawnInterval)
            {
                yield return null;
                continue;
            }

            timer = 0f;
            productionTimer = 0f;

            if (TrySpawnUnit())
                continue;

            yield return null;
        }
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
