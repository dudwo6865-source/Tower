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

    [Tooltip("카메라 회전(Q/E)이 목표 각도에 도달할 때 부드럽게 따라가는 시간입니다.")]
    public float rotationSmoothTime = 0.1f;

    [Header("Rotation")]
    [Tooltip("Q/E 키로 카메라를 회전할 때 초당 회전 각도(도)입니다.")]
    public float rotationSpeed = 120f;

    [Header("Zoom")]
    [Tooltip("줌 및 팬에 사용할 메인 카메라입니다. CameraRig의 자식 카메라를 연결하세요.")]
    public Camera mainCamera;

    [Tooltip("마우스 휠 스크롤 한 칸당 줌 이동량입니다. 값이 클수록 빠르게 확대/축소됩니다.")]
    public float zoomSpeed = 5f;

    [Tooltip("카메라가 가장 가까이 접근할 수 있는 최소 거리입니다.")]
    public float minCameraDistance = 15f;

    [Tooltip("카메라가 가장 멀리 떨어질 수 있는 최대 거리입니다.")]
    public float maxCameraDistance = 60f;

    [Header("Home")]
    [Tooltip("켜면 Home 키 입력 시 아래 Custom Home Position으로 이동합니다. 끄면 게임 시작 시 맵 중앙으로 이동합니다.")]
    public bool useCustomHomePosition;

    [Tooltip("Home 키를 눌렀을 때 카메라가 이동할 월드 좌표입니다. Use Custom Home Position이 켜져 있을 때만 사용됩니다.")]
    public Vector3 customHomePosition;

    [Header("Terrain Bounds")]
    [Tooltip("지형 가장자리에서 카메라가 멈추는 여백 거리입니다. 이 값만큼 안쪽까지만 이동할 수 있습니다.")]
    public float borderPadding = 10f;

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

        Vector3 startPosition = new Vector3(
            terrainWidth * 0.5f,
            0f,
            terrainLength * 0.5f);

        transform.position = startPosition;
        targetPosition = startPosition;
        targetYaw = transform.eulerAngles.y;

        homePosition = useCustomHomePosition
            ? customHomePosition
            : startPosition;
    }

    void Update()
    {
        if (terrain == null)
            return;

        bool pointerOverUI = IsPointerOverUI();

        if (!pointerOverUI)
        {
            KeyboardMove();
            HandleRotation();
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

        if (FindObjectOfType<RTSMinimap>() == null)
        {
            GameObject minimapObject =
                new GameObject("RTSMinimap");

            RTSMinimap minimap =
                minimapObject.AddComponent<RTSMinimap>();

            minimap.cameraController = this;
        }

        if (FindObjectOfType<SelectionBoxUI>() == null)
        {
            GameObject boxObject =
                new GameObject("SelectionBoxUI");

            boxObject.AddComponent<SelectionBoxUI>();
        }

        if (FindObjectOfType<RTSSceneBootstrap>() == null)
        {
            GameObject bootstrapObject =
                new GameObject("RTSSceneBootstrap");

            bootstrapObject.AddComponent<RTSSceneBootstrap>();
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

    void HandleRotation()
    {
        float rotateInput = 0f;

        if (Input.GetKey(KeyCode.Q))
            rotateInput -= 1f;

        if (Input.GetKey(KeyCode.E))
            rotateInput += 1f;

        if (Mathf.Abs(rotateInput) < 0.01f)
            return;

        targetYaw += rotateInput * rotationSpeed * Time.deltaTime;
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

        Vector3 worldPointAfter =
            GetGroundPointUnderMouse();

        Vector3 offset =
            worldPointBefore - worldPointAfter;

        targetPosition +=
            new Vector3(offset.x, 0f, offset.z);

        targetPosition = ClampToTerrain(targetPosition);
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
        pos.x = Mathf.Clamp(
            pos.x,
            borderPadding,
            terrainWidth - borderPadding);

        pos.z = Mathf.Clamp(
            pos.z,
            borderPadding,
            terrainLength - borderPadding);

        return pos;
    }
}
