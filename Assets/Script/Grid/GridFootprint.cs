using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-320)]
public class GridFootprint : MonoBehaviour
{
    public static Vector2Int DefaultBuildingFootprint = new Vector2Int(2, 2);

    [Label("점유 칸 수")]
    [Tooltip("배치·겹침 검사에 사용할 칸 수입니다. 배치 미리보기 격자 크기도 이 값을 따릅니다.")]
    public Vector2Int footprintCells = new Vector2Int(2, 2);

    [Label("칸 점유")]
    [Tooltip("켜면 footprint 칸을 격자에 점유하고 NavMeshObstacle 크기를 footprint에 맞춥니다.")]
    public bool blockCells = true;

    [Label("NavMesh 구멍 뚫기")]
    [Tooltip("켜면 NavMesh에 구멍을 뚫습니다(카빙). 적 스포너처럼 유닛이 위에서 스폰돼 나와야 하는 건물은 끄세요.")]
    public bool carveNavMesh = true;

    [Label("등록 시 격자에 맞추기")]
    [Tooltip("등록 시 transform.position을 footprint 중심 격자에 맞춥니다.")]
    public bool snapTransformOnRegister;

    [Label("모양 직접 지정")]
    [Tooltip("켜면 점유 칸 직사각형(바깥 틀) 안에서 실제로 쓸 칸만 골라 십자·ㄱ자 같은 모양을 만듭니다. " +
        "칸은 인스펙터 아래 격자를 클릭해 켜고 끕니다. 모양은 월드 격자 기준(위 = +Z)이라 건물을 회전해도 함께 돌지 않습니다. " +
        "점유·지형 검사·NavMesh 구멍·배치 미리보기가 이 모양을 따르고, 건설 구역·미니맵 아이콘은 바깥 틀 기준입니다.")]
    public bool useCustomShape;

    // 칸 마스크. 인덱스는 x + z * footprintCells.x이고, 그리기는 Editor/GridFootprintEditor.cs가 한다.
    // cellMaskSize는 마스크를 만들 때의 칸 수로, footprintCells가 바뀌면 겹치는 부분만 옮겨 담는다.
    [SerializeField, HideInInspector] bool[] cellMask;
    [SerializeField, HideInInspector] Vector2Int cellMaskSize;

    // 모양 지정 시 쓰는 장애물들. 박스 하나로는 ㄱ자를 뚫을 수 없어 칸을 직사각형 몇 개로 묶어 자식에 둔다.
    readonly List<NavMeshObstacle> shapedObstacles = new List<NavMeshObstacle>();
    const string ShapedObstacleName = "FootprintObstacle";

    public bool IsRegistered { get; private set; }

    public Vector2Int RegisteredOrigin { get; private set; }

    /// <summary>모양 지정이 켜져 있고 마스크가 칸 수와 맞을 때만 마스크를, 아니면 null(직사각형 전체)을 돌려줍니다.</summary>
    public bool[] ActiveMask =>
        useCustomShape &&
        cellMaskSize == footprintCells &&
        FootprintShape.IsValidMask(cellMask, footprintCells)
            ? cellMask
            : null;

    /// <summary>프리팹/인스턴스의 칸 모양입니다. 직사각형이면 null입니다.</summary>
    public static bool[] ResolveFootprintMask(GameObject source)
    {
        if (source == null)
            return null;

        GridFootprint footprint = source.GetComponent<GridFootprint>();
        return footprint != null ? footprint.ActiveMask : null;
    }

    /// <summary>마스크를 현재 칸 수에 맞춘다. 칸 수가 바뀌면 겹치는 칸 값은 유지하고 새 칸은 켠다.</summary>
    public void EnsureCellMask()
    {
        Vector2Int size = NormalizeFootprint(footprintCells);

        if (cellMaskSize == size && FootprintShape.IsValidMask(cellMask, size))
            return;

        bool oldValid = FootprintShape.IsValidMask(cellMask, cellMaskSize);
        bool[] resized = new bool[size.x * size.y];

        for (int z = 0; z < size.y; z++)
        {
            for (int x = 0; x < size.x; x++)
            {
                bool keep = oldValid && x < cellMaskSize.x && z < cellMaskSize.y;
                resized[x + z * size.x] = !keep || cellMask[x + z * cellMaskSize.x];
            }
        }

        cellMask = resized;
        cellMaskSize = size;
    }

    public bool GetCell(int x, int z)
    {
        EnsureCellMask();
        return FootprintShape.Contains(cellMask, cellMaskSize, x, z);
    }

    public void SetCell(int x, int z, bool used)
    {
        EnsureCellMask();

        if (x < 0 || z < 0 || x >= cellMaskSize.x || z >= cellMaskSize.y)
            return;

        cellMask[x + z * cellMaskSize.x] = used;
    }

    public void SetAllCells(bool used)
    {
        EnsureCellMask();

        for (int i = 0; i < cellMask.Length; i++)
            cellMask[i] = used;
    }

    public static Vector2Int ResolveFootprintCells(GameObject source)
    {
        if (source == null)
            return DefaultBuildingFootprint;

        GridFootprint footprint = source.GetComponent<GridFootprint>();

        if (footprint != null)
            return NormalizeFootprint(footprint.footprintCells);

        return DefaultBuildingFootprint;
    }

    public static GridFootprint EnsureOnInstance(GameObject instance)
    {
        GridFootprint footprint = instance.GetComponent<GridFootprint>();

        if (footprint != null)
            return footprint;

        return instance.AddComponent<GridFootprint>();
    }

    static Vector2Int NormalizeFootprint(Vector2Int cells)
    {
        if (cells.x <= 0 || cells.y <= 0)
            return Vector2Int.one;

        return cells;
    }

    void Awake()
    {
        if (blockCells)
            ConfigureStationaryBuilding(gameObject);
    }

    void Start()
    {
        if (!blockCells)
            return;

        if (!IsRegistered)
            RegisterAtCurrentPosition();
        else
            ApplyNavMeshObstacleSize();
    }

    void OnValidate()
    {
        footprintCells = NormalizeFootprint(footprintCells);
        EnsureCellMask();

        // OnValidate 안에서는 AddComponent를 부를 수 없다.
        // ("SendMessage cannot be called during Awake, CheckConsistency, or OnValidate")
        // 그래서 여기서는 이미 붙어 있는 장애물의 크기만 맞추고, 없으면 만들지 않는다.
        // 장애물이 없는 건물은 플레이 시작 시 Start()/RegisterAtOriginCell()에서 만들어진다.
        // 프리팹에 미리 넣어두고 싶으면 인스펙터 컨텍스트 메뉴를 쓴다.
        if (blockCells)
            ApplyNavMeshObstacleSize(allowCreate: false);
    }

    // 프리팹에 장애물을 미리 넣어두고 높이를 직접 잡고 싶을 때 씁니다.
    // (컴포넌트 우클릭 > NavMesh 장애물 만들기 / 갱신)
    [ContextMenu("NavMesh 장애물 만들기 / 갱신")]
    void CreateNavMeshObstacleFromMenu()
    {
        ApplyNavMeshObstacleSize(allowCreate: true);
    }

    void OnDisable()
    {
        Release();
    }

    public bool RegisterAtCurrentPosition()
    {
        return RegisterAtWorldPosition(transform.position);
    }

    public bool RegisterAtWorldPosition(Vector3 worldPosition)
    {
        if (!blockCells)
            return true;

        Release();

        if (MapGrid.Instance == null || GridOccupancy.Instance == null)
            return false;

        Vector2Int origin =
            MapGrid.Instance.GetFootprintOriginFromCenterWorld(
                worldPosition,
                footprintCells);

        return RegisterAtOriginCell(origin);
    }

    public bool RegisterAtOriginCell(Vector2Int originCell)
    {
        return RegisterAtOriginCell(originCell, false);
    }

    /// <summary>
    /// skipTerrainChecks는 방금 철거한 같은 크기 건물의 칸을 그대로 이어받을 때만 켭니다.
    /// NavMesh carve 구멍이 아직 복구되지 않아 지형 검사가 실패하기 때문입니다.
    /// </summary>
    public bool RegisterAtOriginCell(Vector2Int originCell, bool skipTerrainChecks)
    {
        if (!blockCells)
            return true;

        Release();

        if (MapGrid.Instance == null || GridOccupancy.Instance == null)
            return false;

        if (!GridOccupancy.Instance.TryOccupy(
                originCell,
                footprintCells,
                this,
                float.NaN,
                skipTerrainChecks))
            return false;

        IsRegistered = true;
        RegisteredOrigin = originCell;

        ConfigureStationaryBuilding(gameObject);

        if (snapTransformOnRegister)
        {
            Vector3 center =
                MapGrid.Instance.GetFootprintCenterWorld(originCell, footprintCells);

            // 다층 맵: 이미 배치된 높이를 선호해 같은 XZ의 아래층 NavMesh로 내려가지 않게 합니다.
            center.y = MapGrid.Instance.SampleGroundHeight(center, transform.position.y);
            transform.position = center;
        }

        ApplyNavMeshObstacleSize();
        return true;
    }

    public void Release()
    {
        if (!IsRegistered)
            return;

        if (GridOccupancy.Instance != null)
            GridOccupancy.Instance.Release(this);

        IsRegistered = false;
    }

    public static void ConfigureStationaryBuilding(GameObject target)
    {
        if (target == null)
            return;

        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();

        if (agent != null)
            agent.enabled = false;

        Rigidbody body = target.GetComponent<Rigidbody>();

        if (body != null)
        {
            body.isKinematic = true;
            body.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    void ApplyNavMeshObstacleSize()
    {
        ApplyNavMeshObstacleSize(allowCreate: true);
    }

    /// <summary>
    /// 생성 직후 꺼 둔 NavMesh 장애물을 칸 등록 결과와 상관없이 다시 켭니다.
    /// 칸 등록에 실패해도 건물 발밑 NavMesh는 뚫려 있어야 유닛이 건물을 통과하지 않습니다.
    /// </summary>
    public void RestoreNavMeshObstacle()
    {
        ApplyNavMeshObstacleSize(allowCreate: true);
    }

    /// <summary>
    /// NavMesh 장애물 크기를 footprint에 맞춥니다.
    /// allowCreate가 false면 장애물이 없어도 새로 만들지 않습니다.
    /// OnValidate처럼 컴포넌트를 추가할 수 없는 시점에서 부를 때 씁니다.
    /// </summary>
    void ApplyNavMeshObstacleSize(bool allowCreate)
    {
        if (!blockCells)
            return;

        NavMeshObstacle existing = GetComponent<NavMeshObstacle>();
        bool[] mask = ActiveMask;

        if (mask != null && carveNavMesh)
        {
            // 자식 오브젝트를 만드는 일이라 플레이 중에만 한다. (에디터에서는 기즈모로만 모양을 보여준다)
            if (Application.isPlaying)
                ApplyShapedObstacles(existing, mask);

            return;
        }

        ClearShapedObstacles();

        // 카빙을 끈 건물(예: 적 스포너)은 NavMesh 구멍을 뚫지 않는다.
        // 유닛이 건물 위에서 스폰돼 나올 수 있어야 하므로 장애물을 비활성화한다.
        if (!carveNavMesh)
        {
            if (existing != null)
            {
                existing.carving = false;
                existing.enabled = false;
            }

            return;
        }

        float cellSize = MapGrid.Instance != null
            ? MapGrid.Instance.cellSize
            : 2f;

        if (existing == null && !allowCreate)
            return;

        NavMeshObstacle obstacle = existing;

        if (obstacle == null)
            obstacle = AddNavMeshObstacle();

        float width = footprintCells.x * cellSize;
        float depth = footprintCells.y * cellSize;
        float height = obstacle.size.y > 0f ? obstacle.size.y : 4f;
        float sizeScale = MapGrid.Instance != null
            ? MapGrid.Instance.navObstacleSizeScale
            : 0.85f;

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(
            width * sizeScale,
            height,
            depth * sizeScale);
        obstacle.center = new Vector3(0f, height * 0.5f, 0f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.enabled = true;
    }

    // 칸 모양을 직사각형 몇 개로 묶어 자식 장애물 박스를 그만큼 만든다. 높이는 본체 장애물 값을 따르고,
    // 본체 장애물은 직사각형 전체를 뚫어 버리므로 끈다. 박스는 월드 축에 맞춰 둔다(모양이 월드 격자 기준).
    void ApplyShapedObstacles(NavMeshObstacle rootObstacle, bool[] mask)
    {
        float worldHeight = rootObstacle != null && rootObstacle.size.y > 0f
            ? rootObstacle.size.y * Mathf.Abs(transform.lossyScale.y)
            : 4f;

        if (rootObstacle != null)
        {
            rootObstacle.carving = false;
            rootObstacle.enabled = false;
        }

        float cellSize = MapGrid.Instance != null ? MapGrid.Instance.cellSize : 2f;
        float sizeScale = MapGrid.Instance != null ? MapGrid.Instance.navObstacleSizeScale : 0.85f;

        // 바깥 틀과 같은 비율로 건물 가장자리만 안쪽으로 줄인다. 모양 안쪽 이음매는 줄이지 않아야
        // 박스 사이에 틈이 생기지 않는다.
        float inset = cellSize * (1f - sizeScale) * 0.5f;

        List<RectInt> rects = FootprintShape.ToRects(mask, footprintCells);

        for (int i = 0; i < rects.Count; i++)
        {
            RectInt rect = rects[i];
            NavMeshObstacle obstacle = GetOrCreateShapedObstacle(i);

            float left = IsOuterEdge(mask, rect, Vector2Int.left) ? inset : 0f;
            float right = IsOuterEdge(mask, rect, Vector2Int.right) ? inset : 0f;
            float bottom = IsOuterEdge(mask, rect, Vector2Int.down) ? inset : 0f;
            float top = IsOuterEdge(mask, rect, Vector2Int.up) ? inset : 0f;

            float minX = (rect.xMin - footprintCells.x * 0.5f) * cellSize + left;
            float maxX = (rect.xMax - footprintCells.x * 0.5f) * cellSize - right;
            float minZ = (rect.yMin - footprintCells.y * 0.5f) * cellSize + bottom;
            float maxZ = (rect.yMax - footprintCells.y * 0.5f) * cellSize - top;

            Transform child = obstacle.transform;
            child.SetPositionAndRotation(
                transform.position + new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f),
                Quaternion.identity);

            Vector3 lossy = child.lossyScale;
            float sx = Mathf.Max(0.0001f, Mathf.Abs(lossy.x));
            float sy = Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
            float sz = Mathf.Max(0.0001f, Mathf.Abs(lossy.z));

            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(
                Mathf.Max(0.01f, maxX - minX) / sx,
                worldHeight / sy,
                Mathf.Max(0.01f, maxZ - minZ) / sz);
            obstacle.center = new Vector3(0f, worldHeight * 0.5f / sy, 0f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.enabled = true;
        }

        // 모양이 줄어 남는 박스는 지운다.
        for (int i = shapedObstacles.Count - 1; i >= rects.Count; i--)
        {
            if (shapedObstacles[i] != null)
                Destroy(shapedObstacles[i].gameObject);

            shapedObstacles.RemoveAt(i);
        }
    }

    // 직사각형의 한 변 바깥에 붙은 칸이 하나도 쓰이지 않으면 건물 가장자리로 본다.
    bool IsOuterEdge(bool[] mask, RectInt rect, Vector2Int direction)
    {
        if (direction.x != 0)
        {
            int x = direction.x < 0 ? rect.xMin - 1 : rect.xMax;

            for (int z = rect.yMin; z < rect.yMax; z++)
            {
                if (FootprintShape.Contains(mask, footprintCells, x, z))
                    return false;
            }

            return true;
        }

        int edgeZ = direction.y < 0 ? rect.yMin - 1 : rect.yMax;

        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            if (FootprintShape.Contains(mask, footprintCells, x, edgeZ))
                return false;
        }

        return true;
    }

    NavMeshObstacle GetOrCreateShapedObstacle(int index)
    {
        while (shapedObstacles.Count <= index)
            shapedObstacles.Add(null);

        if (shapedObstacles[index] != null)
            return shapedObstacles[index];

        GameObject child = new GameObject($"{ShapedObstacleName}_{index}");
        child.transform.SetParent(transform, false);
        child.layer = gameObject.layer;

        NavMeshObstacle obstacle = child.AddComponent<NavMeshObstacle>();
        shapedObstacles[index] = obstacle;
        return obstacle;
    }

    void ClearShapedObstacles()
    {
        if (shapedObstacles.Count == 0)
            return;

        foreach (NavMeshObstacle obstacle in shapedObstacles)
        {
            if (obstacle != null)
                Destroy(obstacle.gameObject);
        }

        shapedObstacles.Clear();
    }

    // 선택했을 때 Scene 뷰에 실제로 쓰는 칸을 그린다. 바깥 틀은 흐리게, 쓰는 칸은 진하게.
    void OnDrawGizmosSelected()
    {
        MapGrid grid = MapGrid.Instance != null ? MapGrid.Instance : FindFirstObjectByType<MapGrid>();
        float cellSize = grid != null ? grid.cellSize : 2f;
        Vector2Int size = NormalizeFootprint(footprintCells);
        bool[] mask = ActiveMask;
        Vector3 basePosition = transform.position + Vector3.up * 0.05f;

        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        Gizmos.DrawWireCube(basePosition, new Vector3(size.x * cellSize, 0.02f, size.y * cellSize));

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);

        for (int z = 0; z < size.y; z++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (!FootprintShape.Contains(mask, size, x, z))
                    continue;

                Vector3 offset = new Vector3(
                    (x + 0.5f - size.x * 0.5f) * cellSize,
                    0f,
                    (z + 0.5f - size.y * 0.5f) * cellSize);

                Gizmos.DrawWireCube(basePosition + offset, new Vector3(cellSize * 0.92f, 0.05f, cellSize * 0.92f));
            }
        }
    }

    // 에디터에서 붙일 때는 되돌리기가 되고 씬/프리팹이 변경으로 표시되게 한다.
    NavMeshObstacle AddNavMeshObstacle()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return UnityEditor.Undo.AddComponent<NavMeshObstacle>(gameObject);
#endif

        return gameObject.AddComponent<NavMeshObstacle>();
    }
}
