using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 스테이지 시작 시 이 위치에 건물(본부, 적 스포너 등)을 건설하는 배치 마커입니다.
// 맵 루트 프리팹 안에 빈 오브젝트로 두고 이 컴포넌트를 붙입니다.
// 건물을 맵에 미리 놓아두면 NavMesh를 구울 때 건물 메쉬가 섞여 NavMesh가 깨지므로,
// 마커만 두고 게임이 시작되면 건설 연출과 함께 건물을 만듭니다.
// Scene 뷰에는 지어질 건물의 모양과 점유 칸이 미리보기로 그려집니다.
[DisallowMultipleComponent]
public class StageBuildingSpawnPoint : MonoBehaviour
{
    [Header("건물")]
    [Label("건물 프리팹")]
    [Tooltip("이 위치에 지을 건물 프리팹입니다. (예: HQ, Spawner) Project 창의 프리팹을 넣으세요.")]
    public GameObject buildingPrefab;

    [Label("마커 회전 사용")]
    [Tooltip("켜면 마커의 Y 회전으로 건물을 돌려서 짓습니다. 끄면 프리팹의 회전을 그대로 씁니다.")]
    public bool useMarkerRotation = false;

    [Header("소유자")]
    [Label("소유자 덮어쓰기")]
    [Tooltip("켜면 아래 소유자 ID로 건물의 소유자를 바꿉니다. 끄면 프리팹에 설정된 소유자를 그대로 씁니다.")]
    public bool overrideOwner = false;

    [Label("소유자 ID")]
    [Tooltip("'소유자 덮어쓰기'가 켜져 있을 때 쓸 ownerId입니다. (보통 1 = 플레이어, 2 = 적)")]
    public int ownerId = 1;

    [Header("건설")]
    [Label("건설 대기(초)")]
    [Tooltip("스테이지 시작 후 이 시간이 지나면 건설합니다. 본부를 먼저 짓고 스포너를 조금 뒤에 짓는 식으로 연출 순서를 정할 수 있습니다.")]
    public float spawnDelay = 0f;

    [Label("건설 연출 재생")]
    [Tooltip("켜면 타워를 지을 때와 같은 설치 연출(디졸브, 설치 애니메이션)을 재생합니다.")]
    public bool playConstructionEffect = true;

    [Label("기능 잠금 시간 지정")]
    [Tooltip("켜면 건설 연출 동안 건물 기능(생산 등)을 잠그는 시간을 아래 값으로 바꿉니다. 끄면 프리팹의 설정을 씁니다.")]
    public bool overrideFeatureLock = false;

    [Label("기능 잠금 시간(초)")]
    [Tooltip("'기능 잠금 시간 지정'이 켜져 있을 때 쓸 시간입니다. 0이면 잠그지 않습니다.")]
    public float featureLockDuration = 0f;

    [Header("에디터 미리보기")]
    [Label("미리보기 표시")]
    [Tooltip("Scene 뷰에 지어질 건물의 모양과 점유 칸을 그립니다.")]
    public bool drawPreview = true;

    [Label("미리보기 색")]
    [Tooltip("Scene 뷰 미리보기의 색입니다.")]
    public Color previewColor = new Color(0.3f, 0.8f, 1f, 0.35f);

    // 아직 건설되지 않은 본부/스포너 마커 수입니다.
    // 초기 적 배치와 스포너 전멸 판정이 이 값을 보고 건설이 끝날 때까지 기다립니다.
    public static int PendingHeadquartersCount { get; private set; }
    public static int PendingSpawnerCount { get; private set; }

    // 마커가 건물을 지은 직후(소유자 설정까지 끝난 뒤) 발생합니다.
    public static event Action<GameObject> OnAnyBuildingSpawned;

    public GameObject SpawnedBuilding { get; private set; }

    bool counted;
    bool countedAsHeadquarters;
    bool countedAsSpawner;
    bool spawned;

    void OnEnable()
    {
        if (!Application.isPlaying || spawned || counted || buildingPrefab == null)
            return;

        countedAsHeadquarters = buildingPrefab.GetComponent<Headquarters>() != null;
        countedAsSpawner = buildingPrefab.GetComponent<EnemySpawner>() != null;
        counted = true;

        if (countedAsHeadquarters)
            PendingHeadquartersCount++;

        if (countedAsSpawner)
            PendingSpawnerCount++;
    }

    void OnDisable()
    {
        // 건설 전에 꺼지면(맵 교체로 제거되는 등) 대기 수에서 뺀다.
        ReleasePendingCount();
    }

    void Start()
    {
        if (buildingPrefab == null)
        {
            Debug.LogWarning("StageBuildingSpawnPoint: '건물 프리팹'이 비어 있습니다.", this);
            return;
        }

        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        // 맵 인스턴스화, NavMesh 등록, 격자 갱신이 끝난 다음 프레임에 짓는다.
        yield return null;

        if (spawnDelay > 0f)
            yield return new WaitForSeconds(spawnDelay);

        Spawn();
    }

    [ContextMenu("지금 건설 (테스트)")]
    public void Spawn()
    {
        if (spawned || buildingPrefab == null || !Application.isPlaying)
            return;

        spawned = true;

        Quaternion rotation = useMarkerRotation
            ? Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * buildingPrefab.transform.rotation
            : buildingPrefab.transform.rotation;

        Vector3 position = ResolveGroundPosition(transform.position);
        GridFootprint prefabFootprint = buildingPrefab.GetComponent<GridFootprint>();
        bool blockCells = prefabFootprint != null && prefabFootprint.blockCells;

        // 지형 검사는 Instantiate 전에 끝낸다. (BuildingSpawnUtility와 같은 이유:
        // 생성 순간 건물이 등록되며 격자 높이 캐시가 비워지고 장애물 카빙이 시작된다.)
        Vector2Int originCell = Vector2Int.zero;
        bool terrainValid = true;

        if (blockCells && MapGrid.Instance != null)
        {
            Vector2Int cells = GridFootprint.ResolveFootprintCells(buildingPrefab);
            originCell = MapGrid.Instance.GetFootprintOriginFromCenterWorld(position, cells);

            // 점유 칸 한가운데에 서도록 격자에 맞춘다. (높이는 방금 구한 지면 높이를 유지)
            Vector3 snapped = MapGrid.Instance.GetFootprintCenterWorld(originCell, cells);
            snapped.y = MapGrid.Instance.SampleGroundHeight(snapped, position.y);
            position = snapped;

            if (GridOccupancy.Instance != null)
                terrainValid = GridOccupancy.Instance.CanOccupy(originCell, cells, position.y);
        }

        GameObject building = Instantiate(buildingPrefab, position, rotation);
        SpawnedBuilding = building;

        if (overrideOwner)
        {
            SelectableEntity selectable = building.GetComponent<SelectableEntity>();

            if (selectable != null)
                selectable.ownerId = ownerId;
        }

        if (blockCells)
        {
            // 생성 직후 카빙이 시작되면 자기 발밑 NavMesh가 사라져 칸 등록이 실패한다.
            BuildingSpawnUtility.DisableNavMeshObstacles(building);

            GridFootprint footprint = building.GetComponent<GridFootprint>();

            if (footprint != null && MapGrid.Instance != null &&
                !footprint.RegisterAtOriginCell(originCell, terrainValid))
            {
                Debug.LogWarning(
                    $"StageBuildingSpawnPoint: '{buildingPrefab.name}'의 칸 등록에 실패했습니다. " +
                    "마커 위치가 NavMesh 위인지, 다른 건물과 겹치지 않는지 확인하세요.",
                    this);
            }
        }

        if (playConstructionEffect)
            BeginConstructionPresentation(building);

        ReleasePendingCount();
        OnAnyBuildingSpawned?.Invoke(building);
    }

    void BeginConstructionPresentation(GameObject building)
    {
        BuildingConstructionGate gate = building.GetComponent<BuildingConstructionGate>();

        if (gate == null)
            gate = building.AddComponent<BuildingConstructionGate>();

        if (overrideFeatureLock)
            gate.featureLockDuration = Mathf.Max(0f, featureLockDuration);

        gate.BeginAfterPlacement();
    }

    void ReleasePendingCount()
    {
        if (!counted)
            return;

        counted = false;

        if (countedAsHeadquarters)
            PendingHeadquartersCount = Mathf.Max(0, PendingHeadquartersCount - 1);

        if (countedAsSpawner)
            PendingSpawnerCount = Mathf.Max(0, PendingSpawnerCount - 1);
    }

    static Vector3 ResolveGroundPosition(Vector3 position)
    {
        if (MapGrid.Instance != null)
        {
            position.y = MapGrid.Instance.SampleGroundHeight(position, position.y);
            return position;
        }

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            position.y = hit.position.y;

        return position;
    }

#if UNITY_EDITOR
    // Enter Play Mode에서 도메인 리로드를 끈 경우에도 대기 수가 이전 플레이 값으로 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        PendingHeadquartersCount = 0;
        PendingSpawnerCount = 0;
        OnAnyBuildingSpawned = null;
    }

    void OnDrawGizmos()
    {
        if (!drawPreview || (Application.isPlaying && spawned))
            return;

        Quaternion rotation = buildingPrefab == null
            ? Quaternion.identity
            : useMarkerRotation
                ? Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * buildingPrefab.transform.rotation
                : buildingPrefab.transform.rotation;

        Gizmos.color = previewColor;
        DrawFootprint();

        if (buildingPrefab != null)
            DrawPrefabMeshes(rotation);

        Gizmos.color = new Color(previewColor.r, previewColor.g, previewColor.b, 1f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
        Gizmos.DrawSphere(transform.position + Vector3.up * 3f, 0.25f);
    }

    void DrawFootprint()
    {
        Vector2Int cells = buildingPrefab != null
            ? GridFootprint.ResolveFootprintCells(buildingPrefab)
            : GridFootprint.DefaultBuildingFootprint;

        MapGrid grid = MapGrid.Instance != null ? MapGrid.Instance : FindObjectOfType<MapGrid>();
        float cellSize = grid != null ? grid.cellSize : 2f;

        Vector3 size = new Vector3(cells.x * cellSize, 0.05f, cells.y * cellSize);
        Gizmos.DrawCube(transform.position, size);
        Gizmos.DrawWireCube(transform.position, size);
    }

    void DrawPrefabMeshes(Quaternion rotation)
    {
        // 프리팹 루트 기준 상대 행렬로 각 메쉬를 마커 위치에 그린다.
        Matrix4x4 rootInverse = buildingPrefab.transform.worldToLocalMatrix;
        Matrix4x4 place = Matrix4x4.TRS(transform.position, rotation, buildingPrefab.transform.lossyScale);

        foreach (MeshFilter filter in buildingPrefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            Gizmos.matrix = place * rootInverse * filter.transform.localToWorldMatrix;
            Gizmos.DrawMesh(filter.sharedMesh);
        }

        foreach (SkinnedMeshRenderer skinned in buildingPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned.sharedMesh == null)
                continue;

            Gizmos.matrix = place * rootInverse * skinned.transform.localToWorldMatrix;
            Gizmos.DrawMesh(skinned.sharedMesh);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
