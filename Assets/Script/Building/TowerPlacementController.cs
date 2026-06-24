using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public readonly struct PlacementPreviewState
{
    public readonly bool hasPreview;
    public readonly Vector2Int originCell;
    public readonly Vector2Int footprintCells;
    public readonly Vector3 centerWorld;
    public readonly bool isValid;

    public PlacementPreviewState(
        bool hasPreview,
        Vector2Int originCell,
        Vector2Int footprintCells,
        Vector3 centerWorld,
        bool isValid)
    {
        this.hasPreview = hasPreview;
        this.originCell = originCell;
        this.footprintCells = footprintCells;
        this.centerWorld = centerWorld;
        this.isValid = isValid;
    }
}

[DisallowMultipleComponent]
public class TowerPlacementController : MonoBehaviour
{
    public static TowerPlacementController Instance { get; private set; }

    [Header("Player")]
    [Tooltip("비워두면 UnitSelectionManager의 로컬 플레이어 ID를 사용합니다.")]
    public int localPlayerOwnerId = 1;

    [Header("Placement")]
    [Tooltip("배치 위치를 찾을 때 사용할 지면 레이어입니다. Everything이면 모든 Collider를 검사합니다.")]
    public LayerMask groundMask = ~0;

    [Tooltip("배치 가능할 때 고스트 색입니다.")]
    public Color validGhostColor = new Color(0.2f, 0.95f, 0.35f, 0.55f);

    [Tooltip("배치 불가일 때 고스트 색입니다.")]
    public Color invalidGhostColor = new Color(0.95f, 0.25f, 0.25f, 0.55f);

    public bool IsPlacing => pendingData != null;

    public Vector2Int PreviewOriginCell => pendingOriginCell;

    public Vector2Int PendingFootprintCells => pendingFootprintCells;

    public bool PreviewPlacementValid =>
        pendingData != null &&
        IsValidPlacement(pendingOriginCell, pendingData);

    public bool HasPreviewPlacement =>
        ghostObject != null && ghostObject.activeSelf;

    public event Action<PlacementPreviewState> PreviewChanged;

    private BuildableTowerData pendingData;
    private Vector2Int pendingFootprintCells = Vector2Int.one;
    private Vector2Int pendingOriginCell;
    private Vector2Int lastNotifiedOriginCell = new Vector2Int(int.MinValue, int.MinValue);
    private Vector3 lastNotifiedCenterWorld = new Vector3(float.NaN, float.NaN, float.NaN);
    private bool lastNotifiedHasPreview;
    private bool lastNotifiedValid;
    private GameObject ghostObject;
    private readonly List<Renderer> ghostRenderers = new List<Renderer>();
    private readonly List<Material> ghostMaterials = new List<Material>();
    private Camera mainCamera;
    private Terrain terrain;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (GetComponent<PlacementGridVisualizer>() == null)
            gameObject.AddComponent<PlacementGridVisualizer>();

        if (GetComponent<BuildZoneManager>() == null)
            gameObject.AddComponent<BuildZoneManager>();

        if (GetComponent<BuildZoneVisualizer>() == null)
            gameObject.AddComponent<BuildZoneVisualizer>();
    }

    void Start()
    {
        mainCamera = Camera.main;
        terrain = Terrain.activeTerrain;

        if (UnitSelectionManager.Instance != null)
            localPlayerOwnerId = UnitSelectionManager.Instance.localPlayerOwnerId;
    }

    void OnDestroy()
    {
        DestroyGhost();

        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (!IsPlacing)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        UpdateGhostTransform();
        HandlePlacementInput();
    }

    public bool BeginPlacement(BuildableTowerData data)
    {
        if (data == null || data.prefab == null)
            return false;

        if (WattManager.Instance == null)
        {
            Debug.LogError("TowerPlacementController: WattManager not found");
            return false;
        }

        if (!WattManager.Instance.CanAfford(data.wattCost))
            return false;

        CancelPlacement();

        if (!IsSpawnablePrefab(data.prefab, data.name))
            return false;

        pendingData = data;
        pendingFootprintCells = data.GetFootprintCells();

        if (!CreateGhost(data.prefab))
        {
            pendingData = null;
            pendingFootprintCells = Vector2Int.one;
            return false;
        }

        ResetPreviewNotificationCache();
        UpdateGhostTransform();

        return true;
    }

    public void CancelPlacement()
    {
        NotifyPreviewEnded();
        pendingData = null;
        pendingFootprintCells = Vector2Int.one;
        DestroyGhost();
    }

    void HandlePlacementInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
            return;
        }

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
            return;
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        if (!TryGetSnappedPlacement(
                out Vector2Int originCell,
                out Vector3 placementPoint))
            return;

        if (!IsValidPlacement(originCell, pendingData))
            return;

        if (!WattManager.Instance.TrySpend(pendingData.wattCost))
        {
            CancelPlacement();
            return;
        }

        PlaceTower(pendingData, originCell, placementPoint);
        CancelPlacement();
    }

    void PlaceTower(
        BuildableTowerData data,
        Vector2Int originCell,
        Vector3 position)
    {
        if (!IsSpawnablePrefab(data.prefab, data.name))
            return;

        Quaternion rotation = data.prefab.transform.rotation;

        GameObject towerObject =
            (GameObject)Instantiate(data.prefab, position, rotation);

        SelectableEntity selectable =
            towerObject.GetComponent<SelectableEntity>();

        if (selectable != null)
        {
            selectable.ownerId = data.ownerId > 0
                ? data.ownerId
                : localPlayerOwnerId;
        }

        WorldHealthBar healthBar =
            towerObject.GetComponent<WorldHealthBar>();

        if (healthBar != null)
            healthBar.localPlayerOwnerId = localPlayerOwnerId;

        GridFootprint footprint = GridFootprint.EnsureOnInstance(towerObject);
        footprint.snapTransformOnRegister = true;
        footprint.RegisterAtOriginCell(originCell);
    }

    void UpdateGhostTransform()
    {
        if (ghostObject == null)
            return;

        if (!TryGetSnappedPlacement(out pendingOriginCell, out Vector3 placementPoint))
        {
            ghostObject.SetActive(false);
            NotifyPreviewChangedIfNeeded(false, false);
            return;
        }

        ghostObject.SetActive(true);
        ghostObject.transform.position = placementPoint;

        bool isValid = IsValidPlacement(pendingOriginCell, pendingData);

        for (int i = 0; i < ghostMaterials.Count; i++)
            ghostMaterials[i].color = isValid ? validGhostColor : invalidGhostColor;

        NotifyPreviewChangedIfNeeded(true, isValid);
    }

    void ResetPreviewNotificationCache()
    {
        lastNotifiedOriginCell = new Vector2Int(int.MinValue, int.MinValue);
        lastNotifiedCenterWorld = new Vector3(float.NaN, float.NaN, float.NaN);
        lastNotifiedHasPreview = false;
        lastNotifiedValid = false;
    }

    void NotifyPreviewEnded()
    {
        if (!lastNotifiedHasPreview &&
            lastNotifiedOriginCell.x == int.MinValue)
        {
            return;
        }

        ResetPreviewNotificationCache();
        PreviewChanged?.Invoke(
            new PlacementPreviewState(
                false,
                default,
                Vector2Int.one,
                Vector3.zero,
                false));
    }

    void NotifyPreviewChangedIfNeeded(bool hasPreview, bool isValid)
    {
        if (pendingData == null)
            return;

        Vector3 centerWorld = Vector3.zero;

        if (hasPreview)
        {
            if (ghostObject != null && ghostObject.activeSelf)
            {
                centerWorld = ghostObject.transform.position;
            }
            else if (MapGrid.Instance != null)
            {
                centerWorld = MapGrid.Instance.GetFootprintCenterWorld(
                    pendingOriginCell,
                    pendingFootprintCells);
                centerWorld.y = MapGrid.Instance.SampleGroundHeight(centerWorld);
            }
        }

        bool cellChanged =
            hasPreview && pendingOriginCell != lastNotifiedOriginCell;
        bool centerChanged = hasPreview &&
            (centerWorld - lastNotifiedCenterWorld).sqrMagnitude > 0.0001f;
        bool validChanged = isValid != lastNotifiedValid;
        bool previewChanged = hasPreview != lastNotifiedHasPreview;

        if (!cellChanged && !centerChanged && !validChanged && !previewChanged)
            return;

        lastNotifiedOriginCell = hasPreview
            ? pendingOriginCell
            : new Vector2Int(int.MinValue, int.MinValue);
        lastNotifiedCenterWorld = hasPreview
            ? centerWorld
            : new Vector3(float.NaN, float.NaN, float.NaN);
        lastNotifiedHasPreview = hasPreview;
        lastNotifiedValid = isValid;

        PreviewChanged?.Invoke(
            new PlacementPreviewState(
                hasPreview,
                pendingOriginCell,
                pendingFootprintCells,
                centerWorld,
                isValid));
    }

    bool TryGetSnappedPlacement(
        out Vector2Int originCell,
        out Vector3 centerWorld)
    {
        originCell = default;
        centerWorld = Vector3.zero;

        if (!TryGetRawPlacementPoint(out Vector3 rawPoint))
            return false;

        if (MapGrid.Instance == null || pendingData == null)
        {
            centerWorld = SnapToGround(rawPoint);
            originCell = Vector2Int.zero;
            return true;
        }

        return MapGrid.Instance.TryGetSnappedFootprintPlacement(
            rawPoint,
            pendingFootprintCells,
            out originCell,
            out centerWorld);
    }

    bool TryGetRawPlacementPoint(out Vector3 placementPoint)
    {
        placementPoint = Vector3.zero;

        if (mainCamera == null)
            return false;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        MapGrid grid = MapGrid.Instance;

        if (grid != null &&
            grid.UsesNavMesh &&
            NavMesh.Raycast(
                ray.origin,
                ray.origin + ray.direction * 1000f,
                out NavMeshHit navRayHit,
                grid.navMeshAreaMask))
        {
            placementPoint = navRayHit.position;
            return true;
        }

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundMask))
        {
            Vector3 candidate = hit.point;

            if (grid != null && grid.UsesNavMesh)
            {
                if (grid.TrySampleNavMeshAtXZ(candidate, out NavMeshHit navHit))
                {
                    placementPoint = navHit.position;
                    return true;
                }

                return false;
            }

            return TryFinalizePlacementPoint(SnapToGround(candidate), out placementPoint);
        }

        if (terrain == null)
            terrain = Terrain.activeTerrain;

        if (terrain == null)
            return false;

        Plane groundPlane = new Plane(
            Vector3.up,
            terrain.transform.position);

        if (!groundPlane.Raycast(ray, out float distance))
            return false;

        placementPoint = SnapToGround(ray.GetPoint(distance));
        return TryFinalizePlacementPoint(placementPoint, out placementPoint);
    }

    bool TryFinalizePlacementPoint(Vector3 candidate, out Vector3 placementPoint)
    {
        placementPoint = candidate;

        MapGrid grid = MapGrid.Instance;

        if (grid == null || !grid.UsesNavMesh)
            return true;

        if (grid.TrySampleNavMeshAtXZ(placementPoint, out NavMeshHit navHit))
        {
            placementPoint = navHit.position;
            return true;
        }

        return false;
    }

    Vector3 SnapToGround(Vector3 worldPoint)
    {
        MapGrid grid = MapGrid.Instance;

        if (grid != null)
        {
            worldPoint.y = grid.SampleGroundHeight(worldPoint);
            return worldPoint;
        }

        if (terrain == null)
            terrain = Terrain.activeTerrain;

        if (terrain == null)
            return worldPoint;

        worldPoint.y = terrain.SampleHeight(worldPoint) +
                       terrain.transform.position.y;

        return worldPoint;
    }

    bool IsValidPlacement(Vector2Int originCell, BuildableTowerData data)
    {
        if (data == null)
            return false;

        Vector2Int footprint = data == pendingData
            ? pendingFootprintCells
            : data.GetFootprintCells();

        int ownerId = data.ownerId > 0 ? data.ownerId : localPlayerOwnerId;

        if (GridOccupancy.Instance != null &&
            MapGrid.Instance != null &&
            !GridOccupancy.Instance.CanOccupy(originCell, footprint))
        {
            return false;
        }

        if (BuildZoneManager.Instance != null &&
            !BuildZoneManager.Instance.CanBuildFootprint(
                originCell,
                footprint,
                ownerId))
        {
            return false;
        }

        return true;
    }

    bool CreateGhost(GameObject prefab)
    {
        if (!IsSpawnablePrefab(prefab, pendingData != null ? pendingData.name : "Tower"))
            return false;

        ghostObject = (GameObject)Instantiate(prefab);
        ghostObject.name = "TowerPlacementGhost";

        foreach (Collider collider in ghostObject.GetComponentsInChildren<Collider>())
            collider.enabled = false;

        foreach (MonoBehaviour behaviour in ghostObject.GetComponentsInChildren<MonoBehaviour>())
            behaviour.enabled = false;

        ghostRenderers.Clear();
        ghostMaterials.Clear();

        foreach (Renderer renderer in ghostObject.GetComponentsInChildren<Renderer>())
        {
            ghostRenderers.Add(renderer);

            Material[] sourceMaterials = renderer.materials;
            var tintedMaterials = new Material[sourceMaterials.Length];

            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material material = new Material(sourceMaterials[i]);
                material.color = validGhostColor;
                tintedMaterials[i] = material;
                ghostMaterials.Add(material);
            }

            renderer.materials = tintedMaterials;
        }

        return true;
    }

    static bool IsSpawnablePrefab(GameObject prefab, string dataName)
    {
        if (prefab == null)
        {
            Debug.LogError(
                $"TowerPlacementController: '{dataName}' has no prefab assigned.");

            return false;
        }

        if (prefab.scene.IsValid())
        {
            Debug.LogError(
                $"TowerPlacementController: '{dataName}' references a scene object. " +
                "Assign a Project prefab such as Assets/Artasseet/LowPolyCliffPack/Prefabs/Tower/Tower.prefab.");

            return false;
        }

        return true;
    }

    void DestroyGhost()
    {
        if (ghostObject != null)
            Destroy(ghostObject);

        ghostObject = null;
        ghostRenderers.Clear();

        foreach (Material material in ghostMaterials)
        {
            if (material != null)
                Destroy(material);
        }

        ghostMaterials.Clear();
    }
}
