using UnityEngine;
using UnityEngine.AI;

public class RTSSceneBootstrap : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("아군 건물 프리팹입니다. 비워두면 기본 오브젝트를 생성합니다.")]
    public GameObject allyBuildingPrefab;

    [Tooltip("적 유닛 프리팹입니다. 비워두면 기본 오브젝트를 생성합니다.")]
    public GameObject enemyUnitPrefab;

    [Header("Spawn")]
    [Tooltip("아군 건물 스폰 위치입니다. 비워두면 맵 중앙에 생성합니다.")]
    public Transform allyBuildingSpawn;

    [Tooltip("적 유닛 스폰 위치입니다. 비워두면 맵 가장자리에 생성합니다.")]
    public Transform enemyUnitSpawn;

    [Tooltip("씬에 건물/적이 없을 때만 테스트용 오브젝트를 자동 생성합니다.")]
    public bool spawnDefaultsIfMissing = true;

    void Start()
    {
        if (!spawnDefaultsIfMissing)
            return;

        if (BuildingRegistry.Buildings.Count == 0)
            SpawnAllyBuilding();

        if (FindObjectOfType<EnemyUnitAI>() == null)
            SpawnEnemyUnit();
    }

    void SpawnAllyBuilding()
    {
        Vector3 spawnPosition = GetAllyBuildingSpawnPosition();

        if (allyBuildingPrefab != null)
        {
            Instantiate(allyBuildingPrefab, spawnPosition, Quaternion.identity);
            return;
        }

        CreateDefaultAllyBuilding(spawnPosition);
    }

    void SpawnEnemyUnit()
    {
        Vector3 spawnPosition = GetEnemyUnitSpawnPosition();

        if (enemyUnitPrefab != null)
        {
            GameObject enemy =
                Instantiate(enemyUnitPrefab, spawnPosition, Quaternion.identity);

            EnemyUnitAI ai = enemy.GetComponent<EnemyUnitAI>();
            if (ai != null)
                ai.Initialize(1);

            return;
        }

        CreateDefaultEnemyUnit(spawnPosition);
    }

    Vector3 GetAllyBuildingSpawnPosition()
    {
        if (allyBuildingSpawn != null)
            return allyBuildingSpawn.position;

        Terrain terrain = Terrain.activeTerrain;

        if (terrain == null)
            return Vector3.zero;

        Vector3 terrainSize = terrain.terrainData.size;
        float height =
            terrain.SampleHeight(
                new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.z * 0.5f));

        return new Vector3(
            terrainSize.x * 0.5f,
            height,
            terrainSize.z * 0.5f);
    }

    Vector3 GetEnemyUnitSpawnPosition()
    {
        if (enemyUnitSpawn != null)
            return enemyUnitSpawn.position;

        Terrain terrain = Terrain.activeTerrain;

        if (terrain == null)
            return new Vector3(10f, 0f, 10f);

        Vector3 terrainSize = terrain.terrainData.size;
        Vector3 rawPosition = new Vector3(15f, 0f, 15f);
        float height = terrain.SampleHeight(rawPosition);

        Vector3 spawnPosition = new Vector3(
            rawPosition.x,
            height,
            rawPosition.z);

        if (NavMesh.SamplePosition(
                spawnPosition,
                out NavMeshHit hit,
                10f,
                NavMesh.AllAreas))
            return hit.position;

        return spawnPosition;
    }

    static void CreateDefaultAllyBuilding(Vector3 position)
    {
        GameObject building =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        building.name = "AllyBuilding";
        building.transform.position = position + Vector3.up * 2f;
        building.transform.localScale = new Vector3(6f, 4f, 6f);

        SelectableEntity selectable = building.AddComponent<SelectableEntity>();
        selectable.entityType = SelectableEntityType.Building;
        selectable.ownerId = 1;
        selectable.entityTypeId = "hq";

        EntityHealth health = building.GetComponent<EntityHealth>();
        health.maxHealth = 200f;

        WorldHealthBar healthBar = building.GetComponent<WorldHealthBar>();
        healthBar.barWidth = 6f;
        healthBar.heightOffset = 0.5f;

        NavMeshObstacle obstacle = building.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = new Vector3(6f, 4f, 6f);
        obstacle.carving = true;
    }

    static void CreateDefaultEnemyUnit(Vector3 position)
    {
        GameObject enemy =
            GameObject.CreatePrimitive(PrimitiveType.Capsule);

        enemy.name = "EnemyUnit";
        enemy.transform.position = position + Vector3.up * 1f;

        SelectableEntity selectable = enemy.AddComponent<SelectableEntity>();
        selectable.entityType = SelectableEntityType.Unit;
        selectable.ownerId = 2;
        selectable.entityTypeId = "enemy";

        EntityHealth health = enemy.GetComponent<EntityHealth>();
        health.maxHealth = 80f;

        WorldHealthBar healthBar = enemy.GetComponent<WorldHealthBar>();
        healthBar.barWidth = 1.5f;

        NavMeshAgent agent = enemy.AddComponent<NavMeshAgent>();
        agent.height = 2f;
        agent.radius = 0.5f;
        agent.speed = 5f;
        agent.angularSpeed = 360f;
        agent.acceleration = 12f;

        EnemyUnitAI ai = enemy.AddComponent<EnemyUnitAI>();
        ai.Initialize(1);
    }
}
