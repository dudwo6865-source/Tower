using UnityEngine;
using UnityEngine.EventSystems;

public class RTSCameraPivotController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("화면 가장자리에서 이 픽셀 범위 안에 마우스가 들어오면 카메라가 자동으로 이동합니다.")]
    public float edgeSize = 20f;

    [Header("Middle Mouse Drag")]
    [Tooltip("마우스 휠(가운데 버튼)을 누른 채 드래그할 때 카메라가 이동하는 속도입니다. 값이 클수록 빠르게 팬됩니다.")]
    public float dragPanSpeed = 0.15f;

    [Header("Speed")]
    [Tooltip("줌이 최대로 가까울 때(최소 거리) 카메라 이동 속도입니다.")]
    public float minMoveSpeed = 10f;

    [Tooltip("줌이 최대로 멀 때(최대 거리) 카메라 이동 속도입니다.")]
    public float maxMoveSpeed = 50f;

    [Header("Smoothing")]
    [Tooltip("카메라 위치가 목표 지점에 도달할 때 부드럽게 따라가는 시간입니다. 값이 작을수록 반응이 빠릅니다.")]
    public float positionSmoothTime = 0.12f;

    [Tooltip("카메라 회전이 목표 각도에 도달할 때 부드럽게 따라가는 시간입니다.")]
    public float rotationSmoothTime = 0.1f;

    [Header("Zoom")]
    [Tooltip("줌 및 팬에 사용할 메인 카메라입니다. CameraRig의 자식 카메라를 연결하세요.")]
    public Camera mainCamera;

    [Tooltip("마우스 휠 스크롤 한 칸당 줌 이동량입니다. 값이 클수록 빠르게 확대/축소됩니다.")]
    public float zoomSpeed = 5f;

    [Tooltip("직교(Orthographic) 투영을 사용합니다. 거리와 무관하게 일정한 크기로 보여 일정한 뷰를 원할 때 적합합니다.")]
    public bool useOrthographic = true;

    [Tooltip("[직교 모드] 가장 확대했을 때의 크기(orthographicSize)입니다. 작을수록 확대됩니다.")]
    public float minOrthoSize = 12f;

    [Tooltip("[직교 모드] 가장 축소했을 때의 크기(orthographicSize)입니다. 클수록 더 넓게 보입니다.")]
    public float maxOrthoSize = 45f;

    [Tooltip("[원근 모드] 카메라가 가장 가까이 접근할 수 있는 최소 거리입니다.")]
    public float minCameraDistance = 15f;

    [Tooltip("[원근 모드] 카메라가 가장 멀리 떨어질 수 있는 최대 거리입니다.")]
    public float maxCameraDistance = 60f;

    [Header("Start Focus")]
    [Tooltip("게임 시작 시 카메라가 이 대상(예: 본진 건물)을 중심으로 시작합니다. 비워두면 맵 중앙에서 시작합니다.")]
    public Transform startFocusTarget;

    [Tooltip("켜면 Home 키를 눌렀을 때도 Start Focus Target 위치로 돌아갑니다. (Use Custom Home Position이 꺼져 있을 때)")]
    public bool homeReturnsToStartFocus = true;

    [Header("Home")]
    [Tooltip("켜면 Home 키 입력 시 아래 Custom Home Position으로 이동합니다. 끄면 게임 시작 시 맵 중앙으로 이동합니다.")]
    public bool useCustomHomePosition;

    [Tooltip("Home 키를 눌렀을 때 카메라가 이동할 월드 좌표입니다. Use Custom Home Position이 켜져 있을 때만 사용됩니다.")]
    public Vector3 customHomePosition;

    [Header("Terrain Bounds")]
    [Tooltip("화면에 보이는 영역이 맵을 벗어나지 않도록 카메라를 제한합니다. 이 값만큼 가장자리에서 추가 여백을 둡니다. 0이면 보이는 영역의 끝이 맵 끝에 딱 맞고, 음수면 맵 밖이 조금 보입니다.")]
    public float edgeMargin = 0f;

    private Terrain terrain;

    private float terrainWidth;
    private float terrainLength;

    private Vector3 targetPosition;
    private Vector3 moveVelocity;
    private float targetYaw;
    private float yawVelocity;
    private Vector3 homePosition;

    private Vector3 lastDragMousePosition;
    private bool isDragPanning;

    void Start()
    {
        EnsureEventSystem();
        EnsureGameSystems();

        terrain = Terrain.activeTerrain;

        if (terrain == null)
        {
            Debug.LogError("Terrain not found");
            return;
        }

        terrainWidth = terrain.terrainData.size.x;
        terrainLength = terrain.terrainData.size.z;

        Vector3 mapCenter = new Vector3(
            terrainWidth * 0.5f,
            0f,
            terrainLength * 0.5f);

        Vector3 startPosition = mapCenter;

        if (startFocusTarget != null)
        {
            startPosition.x = startFocusTarget.position.x;
            startPosition.z = startFocusTarget.position.z;
        }

        transform.position = startPosition;
        targetPosition = startPosition;
        targetYaw = transform.eulerAngles.y;

        if (useCustomHomePosition)
            homePosition = customHomePosition;
        else if (startFocusTarget != null && homeReturnsToStartFocus)
            homePosition = startPosition;
        else
            homePosition = mapCenter;

        SetupProjection();
    }

    void SetupProjection()
    {
        if (mainCamera == null)
            return;

        if (!useOrthographic)
        {
            mainCamera.orthographic = false;
            return;
        }

        mainCamera.orthographic = true;

        float initialSize = mainCamera.orthographicSize;

        if (initialSize < minOrthoSize || initialSize > maxOrthoSize)
            initialSize = (minOrthoSize + maxOrthoSize) * 0.5f;

        mainCamera.orthographicSize =
            Mathf.Clamp(initialSize, minOrthoSize, maxOrthoSize);

        float cameraDistance =
            Mathf.Abs(mainCamera.transform.localPosition.z);

        float requiredFar = cameraDistance + maxOrthoSize * 4f + 100f;

        if (mainCamera.farClipPlane < requiredFar)
            mainCamera.farClipPlane = requiredFar;
    }

    void Update()
    {
        if (terrain == null)
            return;

        bool pointerOverUI = IsPointerOverUI();

        if (!pointerOverUI)
        {
            KeyboardMove();
            DragPan();
            EdgeScrollMove();
            Zoom();
        }

        if (Input.GetKeyDown(KeyCode.Home))
            FocusOnPosition(homePosition);

        ApplySmoothMovement();
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    void EnsureGameSystems()
    {
        if (FindObjectOfType<UnitSelectionManager>() == null)
        {
            GameObject managerObject =
                new GameObject("UnitSelectionManager");

            UnitSelectionManager manager =
                managerObject.AddComponent<UnitSelectionManager>();

            manager.cameraController = this;
        }

        if (FindObjectOfType<SelectionBoxUI>() == null)
        {
            GameObject boxObject =
                new GameObject("SelectionBoxUI");

            boxObject.AddComponent<SelectionBoxUI>();
        }
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        return EventSystem.current.IsPointerOverGameObject();
    }

    public void FocusOnPosition(Vector3 worldPosition)
    {
        targetPosition = ClampToTerrain(
            new Vector3(
                worldPosition.x,
                targetPosition.y,
                worldPosition.z));
    }

    void KeyboardMove()
    {
        if (isDragPanning)
            return;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(horizontal) < 0.01f &&
            Mathf.Abs(vertical) < 0.01f)
            return;

        Vector3 right = transform.right;
        Vector3 forward = transform.forward;
        right.y = 0f;
        forward.y = 0f;
        right.Normalize();
        forward.Normalize();

        Vector3 move =
            (right * horizontal + forward * vertical).normalized;

        float moveSpeed =
            Mathf.Lerp(
                minMoveSpeed,
                maxMoveSpeed,
                GetZoomRatio());

        targetPosition +=
            move *
            moveSpeed *
            Time.deltaTime;

        targetPosition = ClampToTerrain(targetPosition);
    }

    void DragPan()
    {
        if (Input.GetMouseButtonDown(2))
        {
            lastDragMousePosition = Input.mousePosition;
            isDragPanning = true;
        }

        if (Input.GetMouseButtonUp(2))
            isDragPanning = false;

        if (!isDragPanning || !Input.GetMouseButton(2))
            return;

        Vector3 mouseDelta =
            Input.mousePosition - lastDragMousePosition;

        lastDragMousePosition = Input.mousePosition;

        if (mouseDelta.sqrMagnitude < 0.01f)
            return;

        Vector3 right = mainCamera.transform.right;
        Vector3 forward = mainCamera.transform.forward;
        right.y = 0f;
        forward.y = 0f;
        right.Normalize();
        forward.Normalize();

        float zoomRatio = GetZoomRatio();
        float panSpeed =
            dragPanSpeed *
            Mathf.Lerp(0.5f, 2f, zoomRatio);

        Vector3 pan =
            (-right * mouseDelta.x - forward * mouseDelta.y) *
            panSpeed;

        targetPosition += pan;
        targetPosition = ClampToTerrain(targetPosition);
    }

    void EdgeScrollMove()
    {
        if (isDragPanning)
            return;

        Vector3 move = Vector3.zero;

        if (Input.mousePosition.x <= edgeSize)
            move.x -= 1f;

        if (Input.mousePosition.x >= Screen.width - edgeSize)
            move.x += 1f;

        if (Input.mousePosition.y <= edgeSize)
            move.z -= 1f;

        if (Input.mousePosition.y >= Screen.height - edgeSize)
            move.z += 1f;

        if (move.sqrMagnitude < 0.01f)
            return;

        Vector3 right = transform.right;
        Vector3 forward = transform.forward;
        right.y = 0f;
        forward.y = 0f;
        right.Normalize();
        forward.Normalize();

        Vector3 worldMove =
            (right * move.x + forward * move.z).normalized;

        float moveSpeed =
            Mathf.Lerp(
                minMoveSpeed,
                maxMoveSpeed,
                GetZoomRatio());

        targetPosition +=
            worldMove *
            moveSpeed *
            Time.deltaTime;

        targetPosition = ClampToTerrain(targetPosition);
    }

    float GetZoomRatio()
    {
        if (useOrthographic && mainCamera.orthographic)
            return Mathf.InverseLerp(
                minOrthoSize,
                maxOrthoSize,
                mainCamera.orthographicSize);

        float currentDistance =
            Mathf.Abs(
                mainCamera.transform.localPosition.z);

        return Mathf.InverseLerp(
            minCameraDistance,
            maxCameraDistance,
            currentDistance);
    }

    void Zoom()
    {
        float scroll =
            Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        Vector3 worldPointBefore =
            GetGroundPointUnderMouse();

        if (useOrthographic && mainCamera.orthographic)
            ZoomOrthographic(scroll);
        else
            ZoomPerspective(scroll);

        Vector3 worldPointAfter =
            GetGroundPointUnderMouse();

        Vector3 offset =
            worldPointBefore - worldPointAfter;

        targetPosition +=
            new Vector3(offset.x, 0f, offset.z);

        targetPosition = ClampToTerrain(targetPosition);
    }

    void ZoomOrthographic(float scroll)
    {
        float size =
            mainCamera.orthographicSize - scroll * zoomSpeed;

        mainCamera.orthographicSize =
            Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
    }

    void ZoomPerspective(float scroll)
    {
        Vector3 localPos =
            mainCamera.transform.localPosition;

        localPos +=
            mainCamera.transform.forward *
            scroll *
            zoomSpeed;

        float distance =
            Vector3.Distance(
                Vector3.zero,
                localPos);

        if (distance < minCameraDistance)
        {
            localPos =
                localPos.normalized *
                minCameraDistance;
        }

        if (distance > maxCameraDistance)
        {
            localPos =
                localPos.normalized *
                maxCameraDistance;
        }

        mainCamera.transform.localPosition = localPos;
    }

    Vector3 GetGroundPointUnderMouse()
    {
        Ray ray =
            mainCamera.ScreenPointToRay(Input.mousePosition);

        float groundY = GetGroundHeightAt(targetPosition);

        Plane groundPlane =
            new Plane(Vector3.up, new Vector3(0f, groundY, 0f));

        if (groundPlane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);

        return targetPosition;
    }

    float GetGroundHeightAt(Vector3 position)
    {
        if (terrain == null)
            return 0f;

        return terrain.SampleHeight(
            new Vector3(position.x, 0f, position.z));
    }

    void ApplySmoothMovement()
    {
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref moveVelocity,
            positionSmoothTime);

        float currentYaw = transform.eulerAngles.y;
        float smoothedYaw = Mathf.SmoothDampAngle(
            currentYaw,
            targetYaw,
            ref yawVelocity,
            rotationSmoothTime);

        transform.rotation =
            Quaternion.Euler(0f, smoothedYaw, 0f);
    }

    Vector3 ClampToTerrain(Vector3 pos)
    {
        if (mainCamera == null)
            return pos;

        ComputeVisibleGroundOffsets(
            out float offMinX,
            out float offMaxX,
            out float offMinZ,
            out float offMaxZ);

        float xMin = edgeMargin - offMinX;
        float xMax = terrainWidth - edgeMargin - offMaxX;
        float zMin = edgeMargin - offMinZ;
        float zMax = terrainLength - edgeMargin - offMaxZ;

        pos.x = ClampOrCenter(pos.x, xMin, xMax, terrainWidth * 0.5f);
        pos.z = ClampOrCenter(pos.z, zMin, zMax, terrainLength * 0.5f);

        return pos;
    }

    float ClampOrCenter(float value, float min, float max, float fallbackCenter)
    {
        if (min > max)
            return fallbackCenter;

        return Mathf.Clamp(value, min, max);
    }

    public bool TryGetVisibleGroundBounds(
        out float minX,
        out float maxX,
        out float minZ,
        out float maxZ)
    {
        minX = 0f;
        maxX = 0f;
        minZ = 0f;
        maxZ = 0f;

        if (mainCamera == null || terrain == null)
            return false;

        float groundY = GetGroundHeightAt(transform.position);

        Plane groundPlane =
            new Plane(Vector3.up, new Vector3(0f, groundY, 0f));

        Vector2[] corners =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        minX = float.MaxValue;
        maxX = float.MinValue;
        minZ = float.MaxValue;
        maxZ = float.MinValue;

        bool anyHit = false;

        foreach (Vector2 corner in corners)
        {
            Ray ray = mainCamera.ViewportPointToRay(corner);

            if (!groundPlane.Raycast(ray, out float distance))
                continue;

            Vector3 point = ray.GetPoint(distance);

            float terrainHeight = GetGroundHeightAt(point);

            Plane localPlane =
                new Plane(Vector3.up, new Vector3(0f, terrainHeight, 0f));

            if (localPlane.Raycast(ray, out float localDistance))
                point = ray.GetPoint(localDistance);

            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minZ = Mathf.Min(minZ, point.z);
            maxZ = Mathf.Max(maxZ, point.z);
            anyHit = true;
        }

        return anyHit && minX <= maxX && minZ <= maxZ;
    }

    void ComputeVisibleGroundOffsets(
        out float offMinX,
        out float offMaxX,
        out float offMinZ,
        out float offMaxZ)
    {
        offMinX = 0f;
        offMaxX = 0f;
        offMinZ = 0f;
        offMaxZ = 0f;

        if (!TryGetVisibleGroundBounds(
                out float minX,
                out float maxX,
                out float minZ,
                out float maxZ))
            return;

        Vector3 pivot = transform.position;

        offMinX = minX - pivot.x;
        offMaxX = maxX - pivot.x;
        offMinZ = minZ - pivot.z;
        offMaxZ = maxZ - pivot.z;
    }
}
