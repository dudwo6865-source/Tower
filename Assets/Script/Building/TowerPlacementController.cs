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

    [Header("플레이어")]
    [Label("로컬 플레이어 ID")]
    [Tooltip("비워두면 UnitSelectionManager의 로컬 플레이어 ID를 사용합니다.")]
    public int localPlayerOwnerId = 1;

    [Header("배치")]
    [Label("지면 레이어")]
    [Tooltip("배치 위치를 찾을 때 사용할 지면 레이어입니다. Everything이면 모든 Collider를 검사합니다.")]
    public LayerMask groundMask = ~0;

    [Label("지면 탐색 간격(m)")]
    [Tooltip("지면 레이어에 맞는 Collider가 없을 때, 마우스 광선을 따라가며 NavMesh 표면을 찾는 간격입니다. 작을수록 정확하지만 무거워집니다.")]
    [Min(0.05f)]
    public float navMeshRayMarchStep = 0.5f;

    [Label("지면 탐색 높이 여유(m)")]
    [Tooltip("NavMesh 최고/최저 높이보다 이만큼 위아래까지 광선을 따라가며 찾습니다.")]
    [Min(0f)]
    public float navMeshRayMarchHeightPadding = 2f;

    [Label("지면 탐색 최대 횟수")]
    [Tooltip("한 프레임에 NavMesh 표면을 찾으려고 샘플링하는 최대 횟수입니다.")]
    [Min(1)]
    public int navMeshRayMarchMaxSteps = 400;

    [Label("배치 가능 색")]
    [Tooltip("배치 가능할 때 고스트 색입니다.")]
    public Color validGhostColor = new Color(0.2f, 0.95f, 0.35f, 0.55f);

    [Label("배치 불가 색")]
    [Tooltip("배치 불가일 때 고스트 색입니다.")]
    public Color invalidGhostColor = new Color(0.95f, 0.25f, 0.25f, 0.55f);

    [Header("오디오")]
    [Label("배치 사운드")]
    [Tooltip("건물 배치가 확정될 때 재생할 사운드입니다.")]
    public AudioClip placementSound;

    [Label("배치 사운드 볼륨")]
    [Tooltip("배치 사운드 볼륨입니다.")]
    [Range(0f, 1f)]
    public float placementSoundVolume = 1f;

    [Label("배치 실패 사운드")]
    [Tooltip("배치에 실패했을 때 재생할 사운드입니다.")]
    public AudioClip failedPlacementSound;

    [Label("배치 실패 사운드 볼륨")]
    [Tooltip("배치 실패 사운드 볼륨입니다.")]
    [Range(0f, 1f)]
    public float failedPlacementSoundVolume = 1f;

    [Label("맵 거리 기준 감쇠")]
    [Tooltip("켜면 맵 XZ 거리에 따라 볼륨을 줄입니다. 끄면 2D로 재생합니다.")]
    public bool placementSoundUseMapDistance;

    [Label("사운드 최소 거리")]
    [Tooltip("맵 거리 감쇠 시작 거리입니다.")]
    public float placementSoundMinDistance = 8f;

    [Label("사운드 최대 거리")]
    [Tooltip("맵 거리 감쇠 무음 거리입니다.")]
    public float placementSoundMaxDistance = 120f;

    [Header("건설")]
    [Label("기본 기능 잠금 시간(초)")]
    [Tooltip("프리팹에 BuildingConstructionGate가 없을 때 추가하며 쓰는 기본 기능 잠금 시간(초)입니다.")]
    public float defaultFeatureLockDuration = 2f;

    [Label("기본 배치 애니메이션 트리거")]
    [Tooltip("프리팹에 BuildingConstructionGate가 없을 때 추가하며 쓰는 Place 애니 트리거 이름입니다.")]
    public string defaultPlaceAnimationTrigger = "Place";

    public bool IsPlacing => pendingBuildData != null;

    public Vector2Int PreviewOriginCell => pendingOriginCell;

    public Vector2Int PendingFootprintCells => pendingFootprintCells;

    public bool PreviewPlacementValid =>
        pendingBuildData != null &&
        IsValidPlacement(
            pendingOriginCell,
            pendingBuildData,
            lastNotifiedCenterWorld.y);

    public bool HasPreviewPlacement =>
        ghostObject != null && ghostObject.activeSelf;

    public Vector3 PreviewCenterWorld =>
        HasPreviewPlacement
            ? ghostObject.transform.position
            : lastNotifiedCenterWorld;

    public IBuildablePlacementData PendingBuildData => pendingBuildData;

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

        // GetComponent는 이 오브젝트 자신만 보므로, GridVisualizer/BuildZoneManager를
        // 별도 오브젝트에서 직접 관리하는 경우 여기서 "없다"고 오판해 새로 하나 더
        // 만들어버린다. 그 새 인스턴스가 먼저 Instance를 차지하면, 원래 씬에 있던
        // (설정값이 들어있는) 진짜 인스턴스가 중복으로 판정돼 제거되는 문제가 있었다.
        // 씬 전체에서 찾아 이미 있으면 그대로 두도록 고친다.
        if (FindFirstObjectByType<GridVisualizer>() == null)
            gameObject.AddComponent<GridVisualizer>();

        if (FindFirstObjectByType<BuildZoneManager>() == null)
            gameObject.AddComponent<BuildZoneManager>();
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

        if (!BuildingSpawnUtility.IsSpawnablePrefab(data.Prefab, data.BuildAssetName))
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

        if (!IsValidPlacement(originCell, pendingBuildData, placementPoint.y))
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
        GameObject buildingObject = BuildingSpawnUtility.Spawn(
            data,
            originCell,
            position,
            localPlayerOwnerId,
            defaultFeatureLockDuration,
            defaultPlaceAnimationTrigger);

        if (buildingObject == null)
            return;

        PlayPlacementSound(position);
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

        bool isValid = IsValidPlacement(
            pendingOriginCell,
            pendingBuildData,
            placementPoint.y);

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
        bool usesNavMesh = grid != null && grid.UsesNavMesh;

        // 1순위: 지면 레이어 Collider. 맞은 지점 바로 아래 NavMesh로 보정한다.
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundMask))
        {
            if (!usesNavMesh)
                return TryFinalizePlacementPoint(SnapToGround(hit.point), out placementPoint);

            if (grid.TrySampleNavMeshAtXZ(hit.point, out NavMeshHit navHit))
            {
                placementPoint = navHit.position;
                return true;
            }

            // 유닛·건물 위를 맞혔거나 지면 레이어 설정이 틀렸을 수 있으므로
            // 실패로 끝내지 않고 아래 NavMesh 탐색으로 넘어간다.
        }

        // 2순위: 마우스 광선을 따라가며 NavMesh 표면을 직접 찾는다.
        // Collider나 레이어 설정과 무관하게 동작하고, 위에서부터 찾으므로 다층 지형에서도 윗면을 고른다.
        if (usesNavMesh)
            return TryMarchRayToNavMesh(ray, grid, out placementPoint);

        // NavMesh를 쓰지 않는 맵: 화면 중앙 지면 높이의 평면에 광선을 맞춘다.
        float groundY = MapPlayBounds.SampleGroundHeight(
            ray.origin + ray.direction * 50f);

        Plane groundPlane = new Plane(
            Vector3.up,
            new Vector3(0f, groundY, 0f));

        if (!groundPlane.Raycast(ray, out float distance))
            return false;

        placementPoint = SnapToGround(ray.GetPoint(distance));
        return true;
    }

    // 광선이 NavMesh 높이 범위에 들어오는 지점부터 일정 간격으로 내려가며,
    // 광선이 NavMesh 표면에 닿거나 그 아래로 내려간 첫 지점을 찾는다.
    bool TryMarchRayToNavMesh(Ray ray, MapGrid grid, out Vector3 placementPoint)
    {
        placementPoint = Vector3.zero;

        // 위에서 내려다보는 광선만 처리한다. 수평에 가까우면 지면을 만나지 않는다.
        if (ray.direction.y > -0.0001f)
            return false;

        float topY = grid.NavMeshMaxY + navMeshRayMarchHeightPadding;
        float bottomY = grid.NavMeshMinY - navMeshRayMarchHeightPadding;

        if (topY <= bottomY)
        {
            // NavMesh 높이 범위를 모르면(수동 바운즈 등) 넉넉한 범위로 찾는다.
            topY = ray.origin.y;
            bottomY = ray.origin.y - 1000f;
        }

        float startT = Mathf.Max(0f, (topY - ray.origin.y) / ray.direction.y);
        float endT = (bottomY - ray.origin.y) / ray.direction.y;

        if (endT <= startT)
            return false;

        float step = Mathf.Max(0.05f, navMeshRayMarchStep);
        int maxSteps = Mathf.Max(1, navMeshRayMarchMaxSteps);
        float sampleRadius = Mathf.Max(step, grid.CellSize * 0.5f);

        for (int i = 0; i <= maxSteps; i++)
        {
            float t = startT + step * i;

            if (t > endT)
                break;

            Vector3 point = ray.GetPoint(t);

            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, sampleRadius, grid.navMeshAreaMask))
                continue;

            // 광선이 아직 표면보다 한참 위면 계속 내려간다. (옆 칸의 표면이 잡힌 경우)
            if (point.y - hit.position.y > step)
                continue;

            // 광선이 지나는 XZ에서 다시 표면을 잡아 커서 바로 아래 지점으로 맞춘다.
            // 절벽 옆처럼 잡힌 표면이 커서 아래가 아니면 계속 내려간다.
            if (!grid.TrySampleNavMeshAtXZ(
                    new Vector3(point.x, hit.position.y, point.z),
                    out NavMeshHit surfaceHit))
                continue;

            placementPoint = surfaceHit.position;
            return true;
        }

        return false;
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

    bool IsValidPlacement(
        Vector2Int originCell,
        IBuildablePlacementData data,
        float preferredY)
    {
        if (data == null)
            return false;

        Vector2Int footprint = data == pendingBuildData
            ? pendingFootprintCells
            : data.GetFootprintCells();

        int ownerId = data.OwnerId > 0 ? data.OwnerId : localPlayerOwnerId;

        if (GridOccupancy.Instance != null &&
            MapGrid.Instance != null &&
            !GridOccupancy.Instance.CanOccupy(originCell, footprint, preferredY))
        {
            return false;
        }

        if (BuildZoneManager.Instance != null &&
            !BuildZoneProvider.PrefabCanPlaceOutsideBuildZones(data.Prefab) &&
            !BuildZoneManager.Instance.CanBuildFootprint(
                originCell,
                footprint,
                ownerId))
        {
            return false;
        }

        if (BuildZoneProvider.PrefabRequiresVisibleVision(data.Prefab) &&
            !BuildZoneProvider.IsFootprintCurrentlyVisible(originCell, footprint))
        {
            return false;
        }

        return true;
    }

    bool CreateGhost(GameObject prefab)
    {
        if (!BuildingSpawnUtility.IsSpawnablePrefab(
                prefab,
                pendingBuildData != null ? pendingBuildData.BuildAssetName : "Building"))
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
