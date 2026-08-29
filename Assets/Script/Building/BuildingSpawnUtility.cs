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
        footprint.footprintCells = data.GetFootprintCells();
        footprint.blockCells = true;
        footprint.snapTransformOnRegister = true;

        if (!footprint.RegisterAtOriginCell(originCell, skipTerrainChecks))
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
