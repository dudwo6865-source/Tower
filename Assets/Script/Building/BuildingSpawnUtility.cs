using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 건물 프리팹 생성 + 그리드 등록 + 건설 게이트 시작을 한곳에서 처리합니다.
/// 신규 배치(TowerPlacementController)와 업그레이드 교체(TowerUpgradeService)가 같이 씁니다.
/// </summary>
public static class BuildingSpawnUtility
{
    /// <summary>
    /// skipTerrainChecks는 방금 철거한 같은 크기 건물의 칸을 그대로 이어받을 때(업그레이드 교체) 켭니다.
    /// </summary>
    public static GameObject Spawn(
        IBuildablePlacementData data,
        Vector2Int originCell,
        Vector3 position,
        int localPlayerOwnerId,
        float fallbackFeatureLockDuration,
        string fallbackPlaceAnimationTrigger,
        bool skipTerrainChecks = false)
    {
        if (data == null || !IsSpawnablePrefab(data.Prefab, data.BuildAssetName))
            return null;

        Vector2Int footprintCells = data.GetFootprintCells();

        // 지형 검사는 반드시 Instantiate '전에' 끝낸다.
        // Instantiate 순간 EntityHealth.Awake가 BuildingRegistry에 등록되면서
        // MapGrid의 칸 높이 캐시가 통째로 비워지고, 프리팹의 NavMeshObstacle도 이미
        // carve를 시작한 상태가 된다. 그래서 등록 시점에 다시 계산된 높이에는 자기
        // 발밑에 뚫린 구멍이 반영돼, 바로 직전 미리보기에서 통과한 자리인데도
        // "NavMesh 위가 아니다"라며 등록이 실패했다.
        bool terrainAlreadyValid =
            skipTerrainChecks ||
            GridOccupancy.Instance == null ||
            GridOccupancy.Instance.CanOccupy(originCell, footprintCells, position.y);

        GameObject buildingObject = Object.Instantiate(
            data.Prefab,
            position,
            data.Prefab.transform.rotation);

        SelectableEntity selectable = buildingObject.GetComponent<SelectableEntity>();

        if (selectable != null)
        {
            selectable.ownerId = data.OwnerId > 0 ? data.OwnerId : localPlayerOwnerId;
            selectable.entityTypeId = data.GetEntityTypeId();
        }

        WorldHealthBar healthBar = buildingObject.GetComponent<WorldHealthBar>();

        if (healthBar != null)
            healthBar.localPlayerOwnerId = localPlayerOwnerId;

        BuildingSourceData.Assign(buildingObject, data);

        // Instantiate 직후 프리팹 NavMeshObstacle이 carve를 시작하면
        // 자기 발밑 NavMesh가 사라져 footprint 등록(IsFootprintOnNavMesh)이 실패한다.
        DisableNavMeshObstacles(buildingObject);

        GridFootprint footprint = GridFootprint.EnsureOnInstance(buildingObject);
        footprint.footprintCells = footprintCells;
        footprint.blockCells = true;
        footprint.snapTransformOnRegister = true;

        // 지형은 위에서(Instantiate 전에) 이미 확인했으므로 여기서는 칸 점유만 한다.
        if (!footprint.RegisterAtOriginCell(originCell, terrainAlreadyValid))
        {
            Debug.LogWarning(
                $"BuildingSpawnUtility: '{data.BuildAssetName}' footprint registration failed at {originCell}.",
                buildingObject);
        }

        ConfigureProductionBuilding(buildingObject, data);

        BeginConstructionPresentation(
            buildingObject,
            fallbackFeatureLockDuration,
            fallbackPlaceAnimationTrigger);

        return buildingObject;
    }

    public static bool IsSpawnablePrefab(GameObject prefab, string dataName)
    {
        if (prefab == null)
        {
            Debug.LogError($"BuildingSpawnUtility: '{dataName}' has no prefab assigned.");
            return false;
        }

        if (prefab.scene.IsValid())
        {
            Debug.LogError(
                $"BuildingSpawnUtility: '{dataName}' references a scene object. " +
                "Assign a Project prefab instead.");

            return false;
        }

        return true;
    }

    public static void DisableNavMeshObstacles(GameObject target)
    {
        if (target == null)
            return;

        foreach (NavMeshObstacle obstacle in target.GetComponentsInChildren<NavMeshObstacle>(true))
        {
            obstacle.carving = false;
            obstacle.enabled = false;
        }
    }

    static void BeginConstructionPresentation(
        GameObject buildingObject,
        float fallbackFeatureLockDuration,
        string fallbackPlaceAnimationTrigger)
    {
        if (buildingObject == null)
            return;

        BuildingConstructionGate gate =
            buildingObject.GetComponent<BuildingConstructionGate>();

        if (gate == null)
        {
            gate = buildingObject.AddComponent<BuildingConstructionGate>();
            gate.featureLockDuration = fallbackFeatureLockDuration;
            gate.placeAnimationTrigger = fallbackPlaceAnimationTrigger;
        }

        gate.BeginAfterPlacement();
    }

    static void ConfigureProductionBuilding(
        GameObject buildingObject,
        IBuildablePlacementData data)
    {
        BuildableProductionData productionData = data as BuildableProductionData;
        ProductionBuilding producer = buildingObject.GetComponent<ProductionBuilding>();

        if (productionData != null && productionData.recipe != null)
        {
            if (producer == null)
                producer = buildingObject.AddComponent<ProductionBuilding>();

            producer.SetRecipe(productionData.recipe);
            producer.BeginProduction();
            return;
        }

        if (producer == null)
            return;

        Building building = buildingObject.GetComponent<Building>();

        if (building == null || !building.isProductionBuilding)
            return;

        if (building.productionRecipe != null)
            producer.SetRecipe(building.productionRecipe);

        producer.BeginProduction();
    }
}
