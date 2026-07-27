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

    [Header("Audio")]
    [Tooltip("건물 배치가 확정될 때 재생할 사운드입니다.")]
    public AudioClip placementSound;

    [Tooltip("배치 사운드 볼륨입니다.")]
    [Range(0f, 1f)]
    public float placementSoundVolume = 1f;

    [Tooltip("배치에 실패했을 때 재생할 사운드입니다.")]
    public AudioClip failedPlacementSound;

    [Tooltip("배치 실패 사운드 볼륨입니다.")]
    [Range(0f, 1f)]
    public float failedPlacementSoundVolume = 1f;

    [Tooltip("켜면 맵 XZ 거리에 따라 볼륨을 줄입니다. 끄면 2D로 재생합니다.")]
    public bool placementSoundUseMapDistance;

    [Tooltip("맵 거리 감쇠 시작 거리입니다.")]
    public float placementSoundMinDistance = 8f;

    [Tooltip("맵 거리 감쇠 무음 거리입니다.")]
    public float placementSoundMaxDistance = 120f;

    public bool IsPlacing => pendingBuildData != null;

    public Vector2Int PreviewOriginCell => pendingOriginCell;

    public Vector2Int PendingFootprintCells => pendingFootprintCells;

    public bool PreviewPlacementValid =>
        pendingBuildData != null &&
        IsValidPlacement(pendingOriginCell, pendingBuildData);

    public bool HasPreviewPlacement =>
        ghostObject != null && ghostObject.activeSelf;

    public event Action<PlacementPreviewState> PreviewChanged;

    private IBuildablePlacementData pendingBuildData;
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
    private AudioSource placementAudioSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsurePlacementAudioSource();

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
        return BeginPlacement((IBuildablePlacementData)data);
    }

    public bool BeginPlacement(BuildableProductionData data)
    {
        return BeginPlacement((IBuildablePlacementData)data);
    }

    public bool BeginPlacement(IBuildablePlacementData data)
    {
        if (data == null || data.Prefab == null)
            return false;

        if (WattManager.Instance == null)
        {
            Debug.LogError("TowerPlacementController: WattManager not found");
            return false;
        }

        if (!WattManager.Instance.CanAfford(data.WattCost))
        {
            PlayFailedPlacementFeedback();
            return false;
        }

        CancelPlacement();

        if (!IsSpawnablePrefab(data.Prefab, data.BuildAssetName))
            return false;

        pendingBuildData = data;
        pendingFootprintCells = data.GetFootprintCells();

        if (!CreateGhost(data.Prefab))
        {
            pendingBuildData = null;
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
        pendingBuildData = null;
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

        if (!IsValidPlacement(originCell, pendingBuildData))
        {
            PlayFailedPlacementSound(placementPoint);
            return;
        }

        if (!WattManager.Instance.TrySpend(pendingBuildData.WattCost))
        {
            PlayFailedPlacementSound(placementPoint);
            CancelPlacement();
            return;
        }

        PlaceBuilding(pendingBuildData, originCell, placementPoint);
        CancelPlacement();
    }

    void PlaceBuilding(
        IBuildablePlacementData data,
        Vector2Int originCell,
        Vector3 position)
    {
        if (!IsSpawnablePrefab(data.Prefab, data.BuildAssetName))
            return;

        Quaternion rotation = data.Prefab.transform.rotation;

        GameObject buildingObject =
            (GameObject)Instantiate(data.Prefab, position, rotation);

        SelectableEntity selectable =
            buildingObject.GetComponent<SelectableEntity>();

        if (selectable != null)
        {
            selectable.ownerId = data.OwnerId > 0
                ? data.OwnerId
                : localPlayerOwnerId;
            selectable.entityTypeId = data.GetEntityTypeId();
        }

        WorldHealthBar healthBar =
            buildingObject.GetComponent<WorldHealthBar>();

        if (healthBar != null)
            healthBar.localPlayerOwnerId = localPlayerOwnerId;

        // Instantiate 직후 프리팹 NavMeshObstacle이 carve를 시작하면
        // 자기 발밑 NavMesh가 사라져 footprint 등록(IsFootprintOnNavMesh)이 실패한다.
        DisableNavMeshObstacles(buildingObject);

        GridFootprint footprint = GridFootprint.EnsureOnInstance(buildingObject);
        footprint.footprintCells = data.GetFootprintCells();
        footprint.blockCells = true;
        footprint.snapTransformOnRegister = true;

        if (!footprint.RegisterAtOriginCell(originCell))
        {
            Debug.LogWarning(
                $"TowerPlacementController: '{data.BuildAssetName}' footprint registration failed at {originCell}.",
                buildingObject);
        }

        ConfigureProductionBuilding(buildingObject, data);
        PlayPlacementSound(position);
    }

    static void DisableNavMeshObstacles(GameObject target)
    {
        if (target == null)
            return;

        foreach (NavMeshObstacle obstacle in target.GetComponentsInChildren<NavMeshObstacle>(true))
        {
            obstacle.carving = false;
            obstacle.enabled = false;
        }
    }

    public void PlayFailedPlacementFeedback()
    {
        PlayFailedPlacementSound(GetFeedbackSoundPosition());
    }

    void PlayPlacementSound(Vector3 position)
    {
        PlaySoundAtPoint(placementSound, placementSoundVolume, position);
    }

    void PlayFailedPlacementSound(Vector3 position)
    {
        PlaySoundAtPoint(failedPlacementSound, failedPlacementSoundVolume, position);
    }

    Vector3 GetFeedbackSoundPosition()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null)
            return mainCamera.transform.position;

        AudioListener listener = FindAudioListener();

        return listener != null
            ? listener.transform.position
            : transform.position;
    }

    void PlaySoundAtPoint(AudioClip clip, float volume, Vector3 position)
    {
        if (clip == null || volume <= 0f)
            return;

        AudioListener listener = FindAudioListener();
        EnsurePlacementAudioSource();

        float effectiveVolume = volume * GetPlacementSoundDistanceScale(position, listener);

        if (effectiveVolume <= 0.001f)
            return;

        placementAudioSource.PlayOneShot(clip, effectiveVolume);
    }

    void EnsurePlacementAudioSource()
    {
        if (placementAudioSource == null)
            placementAudioSource = GetComponent<AudioSource>();

        if (placementAudioSource == null)
            placementAudioSource = gameObject.AddComponent<AudioSource>();

        placementAudioSource.playOnAwake = false;
        placementAudioSource.loop = false;
        placementAudioSource.spatialBlend = 0f;
    }

    float GetPlacementSoundDistanceScale(Vector3 position, AudioListener listener)
    {
        if (!placementSoundUseMapDistance || listener == null)
            return 1f;

        Vector3 listenerPos = listener.transform.position;
        float distance = Vector2.Distance(
            new Vector2(listenerPos.x, listenerPos.z),
            new Vector2(position.x, position.z));

        if (distance <= placementSoundMinDistance)
            return 1f;

        if (distance >= placementSoundMaxDistance)
            return 0f;

        return 1f - Mathf.InverseLerp(
            placementSoundMinDistance,
            placementSoundMaxDistance,
            distance);
    }

    static AudioListener FindAudioListener()
    {
        if (Camera.main != null)
        {
            AudioListener onMain = Camera.main.GetComponent<AudioListener>();

            if (onMain != null)
                return onMain;
        }

        return FindObjectOfType<AudioListener>();
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
            producer.MarkBuiltAtRuntime();
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

        producer.MarkBuiltAtRuntime();
        producer.BeginProduction();
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

        bool isValid = IsValidPlacement(pendingOriginCell, pendingBuildData);

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
        if (pendingBuildData == null)
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

        if (MapGrid.Instance == null || pendingBuildData == null)
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

        float groundY = MapPlayBounds.SampleGroundHeight(
            ray.origin + ray.direction * 50f);

        Plane groundPlane = new Plane(
            Vector3.up,
            new Vector3(0f, groundY, 0f));

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
        worldPoint.y = MapPlayBounds.SampleGroundHeight(worldPoint);
        return worldPoint;
    }

    bool IsValidPlacement(Vector2Int originCell, IBuildablePlacementData data)
    {
        if (data == null)
            return false;

        Vector2Int footprint = data == pendingBuildData
            ? pendingFootprintCells
            : data.GetFootprintCells();

        int ownerId = data.OwnerId > 0 ? data.OwnerId : localPlayerOwnerId;

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
        if (!IsSpawnablePrefab(prefab, pendingBuildData != null ? pendingBuildData.BuildAssetName : "Building"))
            return false;

        ghostObject = (GameObject)Instantiate(prefab);
        ghostObject.name = "TowerPlacementGhost";
        ConfigureGhostNonColliding(ghostObject);

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

    static void ConfigureGhostNonColliding(GameObject ghost)
    {
        const int IgnoreRaycastLayer = 2;

        foreach (Transform child in ghost.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = IgnoreRaycastLayer;

        foreach (Collider collider in ghost.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (NavMeshObstacle obstacle in ghost.GetComponentsInChildren<NavMeshObstacle>(true))
        {
            obstacle.carving = false;
            obstacle.enabled = false;
        }

        foreach (NavMeshAgent agent in ghost.GetComponentsInChildren<NavMeshAgent>(true))
            agent.enabled = false;

        foreach (Rigidbody body in ghost.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        foreach (CharacterController controller in ghost.GetComponentsInChildren<CharacterController>(true))
            controller.enabled = false;

        foreach (MonoBehaviour behaviour in ghost.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
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
