using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ProductionBuilding : MonoBehaviour
{
    [Header("생산")]
    [Label("최대 인구수")]
    [Tooltip("이 건물이 생산한 유닛들이 동시에 차지할 수 있는 인구수 합계입니다. 배치 데이터로 지은 건물은 데이터 값으로 덮어씁니다. 0이면 무제한입니다.")]
    [Min(0)]
    public int maxPopulation = 20;

    [Label("현재 생산 레시피")]
    [Tooltip("지금 생산 중인 레시피입니다. 배치 데이터로 지은 건물은 데이터의 기본 레시피로 채워집니다.")]
    public ProductionRecipe recipe;

    [Label("생산 가능 레시피 목록")]
    [Tooltip("생산 패널에서 고를 수 있는 레시피입니다. 배치 데이터로 지은 건물은 데이터의 목록으로 덮어씁니다. 비워두면 현재 레시피 하나만 생산합니다.")]
    public List<ProductionRecipe> recipeOptions = new List<ProductionRecipe>();

    [Label("교체 시 진행도 유지")]
    [Tooltip("생산 중에 레시피를 바꿨을 때 진행 비율을 유지합니다. 끄면 진행도가 0부터 다시 시작합니다(이미 쓴 Watt는 돌려받지 않음).")]
    public bool keepProgressOnRecipeChange;

    [Label("생산 일시정지")]
    [Tooltip("켜져 있으면 생산 진행과 Watt 소모를 멈춥니다. 진행도는 그대로 유지됩니다. 생산 패널의 일시정지 버튼으로도 바꿀 수 있습니다.")]
    public bool isPaused;

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

    // 이 건물이 생산해 살아 있는 유닛들이 차지한 인구수 합계입니다.
    public int UsedPopulation { get; private set; }

    // 현재 레시피 유닛 한 마리가 차지하는 인구수입니다.
    public int CurrentRecipePopulation => recipe != null ? Mathf.Max(0, recipe.population) : 0;

    // 현재 레시피 유닛을 한 마리 더 만들면 최대 인구수를 넘는지 여부입니다.
    public bool IsAtCapacity
    {
        get
        {
            int cap = EffectiveMaxPopulation();
            return cap > 0 && UsedPopulation + CurrentRecipePopulation > cap;
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

    // 업그레이드(건물 유닛 스폰 수)·유물 보너스를 반영한 최대 인구수입니다.
    // 0 이하면 무제한을 의미하므로 0을 반환합니다.
    int EffectiveMaxPopulation()
    {
        if (maxPopulation <= 0)
            return 0;

        if (UpgradeManager.Instance == null || selectableEntity == null)
            return maxPopulation;

        return UpgradeManager.Instance.GetModifiedSpawnCount(
            selectableEntity.ownerId,
            maxPopulation);
    }

    SelectableEntity selectableEntity;
    EntityHealth buildingHealth;
    LineRenderer rallyMarker;
    Coroutine productionRoutine;
    float cycleElapsed;
    float cycleDuration;

    // 설치 직후 첫 생산 대기(레시피의 initialSpawnDelay) 중인지. 이 동안은 생산 진행도를 0으로 보여준다.
    bool inInitialDelay;
    readonly List<ProducedUnitMarker> producedUnits = new List<ProducedUnitMarker>();

    public int MaxPopulation => EffectiveMaxPopulation();

    // 레시피가 바뀌면 진행 중인 생산 주기를 끊고 새 레시피로 다시 시작한다.
    bool recipeChangePending;
    float pendingStartProgress;

    /// <summary>현재 레시피가 바뀌면 호출됩니다.</summary>
    public event System.Action<ProductionBuilding> OnRecipeChanged;

    // 목록이 비어 있으면 현재 레시피 하나만 고를 수 있다.
    readonly List<ProductionRecipe> fallbackOptions = new List<ProductionRecipe>(1);

    /// <summary>생산 패널에 표시할 레시피 목록입니다.</summary>
    public IReadOnlyList<ProductionRecipe> RecipeOptions
    {
        get
        {
            if (recipeOptions != null && recipeOptions.Count > 0)
                return recipeOptions;

            fallbackOptions.Clear();

            if (recipe != null)
                fallbackOptions.Add(recipe);

            return fallbackOptions;
        }
    }

    public bool HasRecipeOption(ProductionRecipe candidate)
    {
        if (candidate == null)
            return false;

        IReadOnlyList<ProductionRecipe> options = RecipeOptions;

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == candidate)
                return true;
        }

        return false;
    }

    public bool IsProducing =>
        productionRoutine != null &&
        !isPaused &&
        !IsAtCapacity &&
        !IsRecipeLocked &&
        !BuildingConstructionGate.IsFeatureLockedOn(this);

    public bool IsPaused => isPaused;

    // 현재 레시피의 해금 조건을 채우지 못해 생산이 멈춰 있는지 여부입니다.
    public bool IsRecipeLocked => recipe != null && !IsRecipeUnlocked(recipe);

    // 이 건물에서 해당 레시피의 해금 조건을 모두 채웠는지 여부입니다. '생산 건물 자신' 범위 조건은 이 건물로 판정합니다.
    public bool IsRecipeUnlocked(ProductionRecipe target) =>
        target != null && target.IsUnlocked(OwnerId, selectableEntity);

    // 이 건물 기준으로 채우지 못한 해금 조건 문구입니다.
    public string DescribeMissingRequirements(ProductionRecipe target) =>
        target != null ? target.DescribeMissingRequirements(OwnerId, selectableEntity) : string.Empty;

    public int OwnerId => selectableEntity != null ? selectableEntity.ownerId : 1;

    /// <summary>설치 직후 첫 생산 대기 중이면 true입니다. 남은 시간은 ProductionRemainingTime입니다.</summary>
    public bool IsInInitialDelay => inInitialDelay && productionRoutine != null;

    // 일시정지·한도 도달 중에도 쌓아 둔 진행도를 그대로 보여준다.
    // 첫 생산 대기는 유닛을 만드는 과정이 아니라서 슬라이더를 채우지 않는다.
    public float ProductionProgress
    {
        get
        {
            if (productionRoutine == null || cycleDuration <= 0f || inInitialDelay)
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

    // 생산하려 하지만 보유 유닛 한도에 막혔거나 Watt가 부족해 진행하지 못하는지 여부입니다.
    // 일시정지·건설 잠금·해금 조건 미충족은 포함하지 않습니다.
    public bool IsProductionBlocked =>
        productionRoutine != null &&
        !isPaused &&
        !IsRecipeLocked &&
        !BuildingConstructionGate.IsFeatureLockedOn(this) &&
        (IsAtCapacity || waitingForWatt);

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
        if (recipe == newRecipe)
            return;

        float previousProgress = ProductionProgress;
        recipe = newRecipe;

        if (productionRoutine != null)
        {
            recipeChangePending = true;
            pendingStartProgress = keepProgressOnRecipeChange ? previousProgress : 0f;
        }

        OnRecipeChanged?.Invoke(this);
    }

    /// <summary>
    /// 고를 수 있는 레시피 목록을 바꾸고 현재 레시피를 기본값으로 맞춥니다.
    /// 현재 레시피가 새 목록에 있으면 그대로 둡니다.
    /// </summary>
    public void SetRecipeOptions(IReadOnlyList<ProductionRecipe> options, ProductionRecipe defaultRecipe)
    {
        recipeOptions.Clear();

        if (options != null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] != null && !recipeOptions.Contains(options[i]))
                    recipeOptions.Add(options[i]);
            }
        }

        if (recipe != null && recipeOptions.Contains(recipe))
            return;

        SetRecipe(defaultRecipe != null ? defaultRecipe : (recipeOptions.Count > 0 ? recipeOptions[0] : null));
    }

    /// <summary>
    /// 플레이어가 생산 패널에서 레시피를 고를 때 씁니다. 목록에 없는 레시피는 무시합니다.
    /// 생산이 멈춰 있었다면(레시피가 없던 경우 등) 다시 시작합니다.
    /// </summary>
    public bool SelectRecipe(ProductionRecipe newRecipe)
    {
        if (!HasRecipeOption(newRecipe) || !IsRecipeUnlocked(newRecipe))
            return false;

        SetRecipe(newRecipe);

        if (productionRoutine == null && isActiveAndEnabled &&
            (buildingHealth == null || buildingHealth.IsAlive))
            BeginProduction();

        return true;
    }

    /// <summary>
    /// 건물 업그레이드로 교체될 때 생산 상태를 새 건물로 넘깁니다.
    /// 집결지·생산한 유닛(인구)·일시정지를 옮기고, 새 건물이 같은 레시피를 생산할 수 있으면 이어서 생산합니다.
    /// </summary>
    public void TransferStateTo(ProductionBuilding target)
    {
        if (target == null || target == this)
            return;

        if (hasRallyPoint)
            target.SetRallyPoint(rallyPointWorld);

        target.SetPaused(isPaused);

        if (recipe != null && target.HasRecipeOption(recipe) && target.IsRecipeUnlocked(recipe))
            target.SetRecipe(recipe);

        for (int i = 0; i < producedUnits.Count; i++)
        {
            ProducedUnitMarker marker = producedUnits[i];

            if (marker == null)
                continue;

            marker.Initialize(target, marker.Population);
            target.producedUnits.Add(marker);
            target.AliveCount++;
            target.UsedPopulation += marker.Population;
        }

        producedUnits.Clear();
        AliveCount = 0;
        UsedPopulation = 0;
    }

    public void SetMaxPopulation(int value)
    {
        maxPopulation = Mathf.Max(0, value);
    }

    public void SetPaused(bool paused)
    {
        if (isPaused == paused)
            return;

        isPaused = paused;

        if (paused)
            waitingForWatt = false;
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
        recipeChangePending = false;
        pendingStartProgress = 0f;
        inInitialDelay = false;
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
            // 첫 생산 대기는 건물 단위라 도중에 레시피를 바꿔도 끊지 않는다.
            inInitialDelay = true;
            yield return RunProductionCycle(recipe.initialSpawnDelay, 0f, false);
            inInitialDelay = false;

            if (buildingHealth != null && !buildingHealth.IsAlive)
                yield break;

            recipeChangePending = false;
            pendingStartProgress = 0f;
        }

        while (enabled)
        {
            if (buildingHealth != null && !buildingHealth.IsAlive)
                yield break;

            if (IsProductionHeld())
            {
                cycleElapsed = 0f;
                cycleDuration = 0f;
                yield return null;
                continue;
            }

            if (recipe == null || recipe.unitPrefab == null)
            {
                cycleElapsed = 0f;
                cycleDuration = 0f;
                yield return null;
                continue;
            }

            float startProgress = pendingStartProgress;
            pendingStartProgress = 0f;
            recipeChangePending = false;

            float interval = Mathf.Max(0f, GetEffectiveSpawnInterval());
            yield return RunProductionCycle(interval, startProgress);

            if (buildingHealth != null && !buildingHealth.IsAlive)
                yield break;

            // 주기 도중 레시피가 바뀌었으면 스폰하지 않고 새 레시피로 다시 시작한다.
            if (recipeChangePending)
                continue;

            TrySpawnUnit();
        }
    }

    // 진행을 멈추고 기다려야 하는 상태(건설 잠금·생산 한도·일시정지·해금 조건 미충족)인지 여부.
    bool IsProductionHeld()
    {
        return isPaused ||
            BuildingConstructionGate.IsFeatureLockedOn(this) ||
            IsAtCapacity ||
            IsRecipeLocked;
    }

    IEnumerator RunProductionCycle(
        float duration,
        float startProgress = 0f,
        bool interruptOnRecipeChange = true)
    {
        cycleDuration = Mathf.Max(0f, duration);
        cycleElapsed = cycleDuration * Mathf.Clamp01(startProgress);

        if (cycleDuration <= 0f)
            yield break;

        while (cycleElapsed < cycleDuration)
        {
            if (buildingHealth != null && !buildingHealth.IsAlive)
                yield break;

            if (interruptOnRecipeChange && recipeChangePending)
            {
                waitingForWatt = false;
                yield break;
            }

            if (IsProductionHeld())
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
        UsedPopulation = Mathf.Max(0, UsedPopulation - marker.Population);
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

        marker.Initialize(this, CurrentRecipePopulation, recipe);
        producedUnits.Add(marker);
        AliveCount++;
        UsedPopulation += marker.Population;
        SendSpawnedUnitToRally(unitObject);

        if (ownerId == localPlayerOwnerId)
        {
            UnitSound unitSound = unitObject.GetComponent<UnitSound>();

            if (unitSound != null)
                unitSound.PlayVoice(UnitSoundVoice.ProductionComplete);
        }

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
