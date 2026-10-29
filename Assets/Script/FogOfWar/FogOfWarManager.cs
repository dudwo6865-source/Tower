using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    [Header("플레이어")]
    [Label("로컬 플레이어 ID")]
    [Tooltip("이 ownerId와 같은 유닛·건물의 시야로 안개를 밝힙니다.")]
    public int localPlayerOwnerId = 1;

    [Header("그리드")]
    [Label("그리드 가로")]
    [Tooltip("안개 그리드 가로 해상도입니다.")]
    public int gridWidth = 256;

    [Label("그리드 세로")]
    [Tooltip("안개 그리드 세로 해상도입니다.")]
    public int gridHeight = 256;

    [Header("시야")]
    [Label("시야 가장자리 부드러움")]
    [Tooltip("시야 원 가장자리를 부드럽게 fade-out하는 거리(월드 단위)입니다.")]
    public float visionEdgeSoftness = 4f;

    [Label("시야 판정 기준")]
    [Tooltip("게임플레이 시야 판정에 필요한 최소 visible 값(0~1)입니다.")]
    [Range(0f, 1f)]
    public float visibilityThreshold = 0.35f;

    [Header("유닛 가시성")]
    [Label("가시성 판정 여유")]
    [Tooltip("유닛/건물 가시성 판정 시 bounds를 XZ로 확장하는 여유(미터)입니다.")]
    public float entityVisibilityPadding = 0.75f;

    [Label("표시 기준")]
    [Tooltip("숨겨진 유닛이 다시 보일 때: 샘플 중 하나라도 이 값 이상이면 표시합니다.")]
    [Range(0f, 1f)]
    public float entityShowThreshold = 0.08f;

    [Label("숨김 기준")]
    [Tooltip("보이는 유닛을 숨길 때: 모든 샘플이 이 값 이하면 완전히 시야 밖으로 판정합니다.")]
    [Range(0f, 1f)]
    public float entityHideThreshold = 0.02f;

    [Header("자동 그리드 해상도")]
    [Label("자동 해상도")]
    [Tooltip("켜면 gridWidth/gridHeight 대신 맵 크기와 Auto Grid Cell Size로 해상도를 자동 계산합니다.")]
    public bool autoGridResolution;

    [Label("자동 해상도 칸 크기")]
    [Tooltip("자동 해상도 사용 시, 안개 그리드 한 칸이 덮는 월드 크기(미터)입니다. 작을수록 정밀하지만 무거워집니다.")]
    public float autoGridCellSize = 1f;

    [Label("자동 해상도 범위")]
    [Tooltip("자동 해상도의 최소/최대 한 변 칸 수입니다.")]
    public Vector2Int autoGridResolutionRange = new Vector2Int(64, 512);

    [Header("고저 시야")]
    [Label("고저 시야 사용")]
    [Tooltip("켜면 지형 높이 차이로 시야가 가려집니다(고지대 유리): 높은 곳에서는 낮은 곳이 보이고, 낮은 곳에서는 앞을 가로막는 높은 지형 너머가 안 보입니다.")]
    public bool enableElevationVision = true;

    [Label("높이맵 해상도")]
    [Tooltip("지형 높이맵 해상도(가로 기준 칸 수)입니다. Start() 시점에 한 번만 굽습니다. 낮을수록 굽는 속도가 빠르고 가볍습니다.")]
    public int elevationGridResolution = 64;

    [Label("기본 눈높이")]
    [Tooltip("시야 소스의 기본 눈높이(지면 기준, 미터)입니다. 유닛별로 다르게 하려면 FogOfWarVisionSource의 Eye Height Override를 쓰세요.")]
    public float defaultEyeHeight = 1.5f;

    [Label("AI 지형 시야 가림")]
    [Tooltip("켜면 적 AI도 언덕·절벽에 가려진 대상은 어그로로 잡지 않습니다. 안개는 플레이어 한쪽 시야라 적 AI가 쓸 수 없으므로, 여기서는 구워둔 지형 높이만 보고 판정합니다(배열 조회뿐이라 거의 공짜입니다).")]
    public bool aiTerrainLineOfSight = true;

    [Label("아래에서 고지대 가림")]
    [Tooltip("켜면 시야 주인이 서 있는 지면보다 '가림 높이 차' 이상 높은 지형은, 거리·각도와 상관없이 아래에서 보이지 않습니다. 절벽 벽에 붙었을 때 절벽 위가 보이는 현상을 막습니다.")]
    public bool blockHigherGround = true;

    [Label("가림 높이 차")]
    [Tooltip("이 높이(미터) 이상 높은 지형은 아래에서 보이지 않습니다. 절벽 한 단 높이보다 작게, 완만한 경사의 높이 차보다는 크게 잡으세요.")]
    public float highGroundBlockHeight = 1f;

    [Label("절벽 경계 여유 거리")]
    [Tooltip("높은 지형에서 이 거리(미터) 안에 있는 절벽 가장자리·경사 칸도 높은 지형으로 봅니다. 벽에 붙었을 때 절벽 위 가장자리가 띠처럼 보이면 늘리세요. 0이면 끕니다.")]
    public float highGroundEdgeMargin = 1.5f;

    [Label("경계로 볼 최소 높이")]
    [Tooltip("'절벽 경계 여유 거리'를 적용할 칸의 최소 높이 차(미터)입니다. 이보다 낮은 칸은 절벽 바로 밑이어도 평지로 보고 그대로 보여줍니다.")]
    public float highGroundEdgeMinRise = 0.3f;

    [Label("아래에서 언덕 지형 안내")]
    [Tooltip("보이는 낮은 입구에서 이어지는 언덕·램프를 시야 반경 안에서 어둡게 드러냅니다. 고지대 평지와 적·건물의 발견 판정은 바뀌지 않습니다. 지형 정보는 시작할 때 굽습니다.")]
    public bool revealHillTerrain = true;

    [Label("고지대 가장자리 지형 안내")]
    [Tooltip("아래에서 보이는 고지대 가장자리 안쪽 지형만 어둡게 보여줍니다. 적과 건물의 발견 판정은 바꾸지 않습니다.")]
    public bool revealHighGroundTerrain = true;
    [Label("고지대 지형 안내 거리")]
    [Tooltip("시야 방향을 따라 고지대 가장자리에서 안쪽으로 보여줄 거리입니다. 0이면 표시하지 않습니다.")]
    [Min(0f)] public float highGroundTerrainRevealDistance = 4f;

    [Label("시야 주인 지면 탐색 거리")]
    [Tooltip("유닛·건물이 서 있는 지면 높이를 찾을 때, 실제 위치에서 가장 가까운 NavMesh를 이 거리(미터) 안에서 찾습니다. 못 찾으면 그 자리의 가장 높은 표면을 씁니다. 너무 크게 잡을 필요는 없습니다.")]
    public float sourceGroundSnapDistance = 2f;

    [Header("갱신")]
    [Label("갱신 간격(초)")]
    [Tooltip("안개 텍스처를 갱신하는 간격(초)입니다. 시야 계산을 여러 프레임에 나누면 그 프레임 수만큼 실제 간격이 조금 늘어납니다.")]
    public float updateInterval = 0.1f;

    [Label("시야 계산 분산 프레임 수")]
    [Tooltip("한 번의 안개 갱신에 필요한 시야 계산을 이 프레임 수에 나눠 처리합니다. 시야 소스가 많을 때 한 프레임만 크게 튀는 것을 막습니다. 1이면 한 프레임에 모두 계산합니다(예전 방식).")]
    [Min(1)]
    public int visionSpreadFrames = 4;

    [Label("제자리 시야 재사용")]
    [Tooltip("켜면 지난 계산 이후 움직이지 않은 시야 소스(타워·건물·멈춘 유닛)는 다시 계산하지 않고 이전 결과를 그대로 씁니다.")]
    public bool reuseStationaryVision = true;

    [Label("제자리 판정 이동 거리")]
    [Tooltip("지난 계산 위치에서 이 거리(미터) 이하로만 움직였으면 제자리로 보고 이전 시야를 재사용합니다. 너무 크면 이동한 유닛의 시야가 늦게 따라옵니다.")]
    [Min(0f)]
    public float stationaryMoveTolerance = 0.05f;

    [Header("오버레이")]
    [Label("월드 오버레이 생성")]
    [Tooltip("월드 위 안개 오버레이를 자동 생성합니다.")]
    public bool createWorldOverlay = true;

    [Label("MapGrid 기준 사용")]
    [Tooltip("MapGrid가 있으면 bounds/표면 높이를 MapGrid(NavMesh) 기준으로 사용합니다.")]
    public bool useMapGridWhenAvailable = true;

    [Header("깊이 기반 안개")]
    [Label("깊이 기반 안개 사용")]
    [Tooltip("켜면 지형을 따라가는 메쉬 대신, 카메라 깊이로 화면에 그려진 지형·절벽의 실제 위치를 찾아 안개를 칠합니다. 절벽에서 안개가 뚫리거나 겹쳐 보이는 문제가 없습니다. 끄면 예전 방식(지형을 따라가는 메쉬)을 씁니다.")]
    public bool useDepthProjection = true;

    [Label("카메라 깊이 텍스처 자동 켜기")]
    [Tooltip("깊이 기반 안개에 필요한 카메라 깊이 텍스처를 시작 시 메인 카메라에 켭니다. 끄면 URP 파이프라인 에셋의 Depth Texture를 직접 켜야 합니다.")]
    public bool autoEnableCameraDepthTexture = true;

    [Label("안개 상자 위아래 범위")]
    [Tooltip("깊이 기반 안개 상자가 맵 기준 높이에서 위아래로 덮는 거리(미터)입니다. 이 범위 밖에 있는 지형에는 안개가 칠해지지 않습니다.")]
    public float depthVolumeVerticalExtent = 100f;

    [Header("부드럽게 표시")]
    [Label("표시 블러 횟수")]
    [Tooltip("화면·미니맵에 그리는 안개 경계를 흐리게 하는 횟수입니다. 높을수록 계단 모양 경계가 부드러워집니다. 유닛 표시·스폰 같은 게임플레이 판정은 흐리기 전 값을 씁니다.")]
    [Range(0, 4)]
    public int displayBlurIterations = 1;

    [Label("부드러운 전환")]
    [Tooltip("안개를 갱신할 때 직전 모습에서 새 모습으로 갱신 간격 동안 서서히 넘어갑니다. 유닛이 움직일 때 경계가 툭툭 끊기는 것을 줄입니다.")]
    public bool smoothTransition = true;

    [Header("표면 샘플링")]
    [Label("오버레이 샘플 방식")]
    [Tooltip("오버레이 메쉬 높이 샘플 방식입니다. 경사면이 있으면 Visual Geometry를 권장합니다.")]
    public FogSurfaceSampleMode overlaySampleMode =
        FogSurfaceSampleMode.VisualGeometry;

    [Label("지면 레이캐스트 마스크")]
    [Tooltip("메쉬 Collider를 레이캐스트로 찾을 레이어입니다.")]
    public LayerMask groundRaycastMask = ~0;

    [Label("레이캐스트 높이 여유")]
    [Tooltip("표면 레이캐스트 시작 높이(지형 위 추가값)입니다.")]
    public float surfaceRaycastHeightPadding = 32f;

    [Label("표면 없는 곳 숨김")]
    [Tooltip("어떤 표면 샘플도 실패한 정점/삼각형을 오버레이에서 제외합니다.")]
    public bool hideOverlayWithoutSurface = true;

    [Label("오버레이 메쉬 분할 수")]
    [Tooltip("지형 표면을 따라가는 메쉬 세그먼트 수입니다. 높을수록 경사에 잘 맞지만 무거워집니다. 깊이 기반 안개를 끈 경우에만 쓰입니다.")]
    public int overlayMeshSegments = 64;

    [Label("오버레이 높이")]
    [Tooltip("지형 표면 위로 띄울 높이입니다. 유닛/지형이 안개를 뚫을 때 올리세요. 깊이 기반 안개를 끈 경우에만 쓰입니다.")]
    public float overlayHeightOffset = 1.2f;

    [Label("오버레이 확장 거리")]
    [Tooltip("플레이 맵 바깥으로 오버레이 메쉬를 확장하는 거리(미터)입니다. 카메라 각도로 가장자리가 비칠 때 늘리세요.")]
    public float overlayMeshPadding = 48f;

    [Header("색상")]
    [Label("미탐색 색")]
    public Color unexploredColor = new Color(0f, 0f, 0f, 0.95f);
    [Label("탐색됨 색")]
    public Color exploredColor = new Color(0f, 0f, 0f, 0.55f);

    [Header("셰이더")]
    [Label("월드 안개 셰이더")]
    [Tooltip("월드 안개 오버레이에 쓸 셰이더입니다. Assets/Shaders/FogOfWar.shader를 넣어두세요. 비워두면 이름으로 찾지만, 그 셰이더를 쓰는 머티리얼이 없으면 빌드에서 잘려나가 안개가 깨집니다.")]
    public Shader worldFogShader;

    [Label("미니맵 안개 셰이더")]
    [Tooltip("미니맵 안개에 쓸 셰이더입니다. Assets/Shaders/FogOfWarUI.shader를 넣어두세요. 비워두면 이름으로 찾지만, 그 셰이더를 쓰는 머티리얼이 없으면 빌드에서 잘려나가 안개가 깨집니다.")]
    public Shader uiFogShader;

    public Texture2D FogTexture => fogTexture;
    public int LocalPlayerOwnerId => localPlayerOwnerId;

    // 월드 안개 머티리얼이 실제로 만들어졌는지입니다. 셰이더가 없어 안개가 화면에
    // 안 그려지는데 유닛만 계속 숨기면 "적이 이유 없이 사라진 것"처럼 보이므로,
    // FogOfWarVisibility가 이 값을 보고 숨김을 포기합니다.
    public bool HasWorldFogMaterial => worldFogMaterial != null;

    private readonly List<FogOfWarVisionSource> visionSources =
        new List<FogOfWarVisionSource>();

    private Vector3 mapOrigin;
    private Vector2 mapSize;

    private Texture2D fogTexture;
    private Color32[] fogPixels;

    // 지형 안내는 화면에만 합성해 적·건물의 탐색 판정과 분리한다.
    private bool[] hillTerrainByCell;
    private byte[] hillTerrainExplored;
    private byte[] highGroundTerrainScratch;
    private byte[] highGroundTerrainAccum;
    private byte[] highGroundTerrainDisplay;

    // 화면 표시용 버퍼. fogPixels(게임플레이 판정용 원본)를 흐려서 담고, 부드러운 전환을
    // 위해 직전 표시 결과를 fogPrevTexture에 남겨둔다.
    private Texture2D fogPrevTexture;
    private Color32[] displayPixels;
    private Color32[] prevDisplayPixels;
    private Color32[] blurScratchPixels;

    static readonly int FogTexId = Shader.PropertyToID("_FogTex");
    static readonly int FogPrevTexId = Shader.PropertyToID("_FogPrevTex");
    static readonly int FogBlendId = Shader.PropertyToID("_FogBlend");
    static readonly int CullId = Shader.PropertyToID("_Cull");
    static readonly int ZTestId = Shader.PropertyToID("_ZTest");
    const string DepthProjectionKeyword = "_FOG_DEPTH_PROJECTION";

    // 안개 그리드와 같은 해상도로 미리 펼쳐둔 지형 높이(칸당 값 1개). 레이마칭 중에는
    // 나눗셈·보간 없이 배열 조회 한 번으로 끝나도록 Start()에서 한 번만 굽는다.
    private float[] elevationHeightByCell;

    // 각 칸 주변 '절벽 경계 여유 거리' 안의 가장 높은 지형 높이. 절벽 위 가장자리가
    // 높이맵 보간으로 낮게 잡혀 아래에서 보이는 것을 막는 데 쓴다.
    private float[] elevationEdgeHeightByCell;

    private float cellSizeX;
    private float cellSizeZ;

    const float MinElevationSampleDistance = 0.5f;
    const float ElevationSlopeEpsilon = 0.01f;

    private Material worldFogMaterial;
    private Material uiFogMaterial;
    private Renderer overlayRenderer;
    private Mesh overlayMesh;

    private RawImage minimapFogOverlay;
    private float updateTimer;

    /// <summary>시야 소스 하나가 밝힌 칸과 밝기입니다. 소스가 제자리에 있으면 재사용합니다.</summary>
    [Header("원거리 피격 시 시야")]
    [Label("원거리 공격자 주변 공개")]
    [Tooltip("아군이 적 투사체에 맞으면 공격자 주변을 고저차와 관계없이 잠시 밝힙니다.")]
    public bool revealRangedAttacker = true;
    [Label("공격자 공개 반경")]
    [Min(0.1f)] public float attackerRevealRadius = 4f;
    [Label("공격자 공개 시간")]
    [Min(0.1f)] public float attackerRevealDuration = 2f;

    struct AttackReveal
    {
        public int attackerId;
        public Vector3 position;
        public float expires;
    }
    readonly List<AttackReveal> attackReveals = new List<AttackReveal>();

    public void RevealRangedAttacker(SelectableEntity attacker, SelectableEntity victim)
    {
        if (!revealRangedAttacker || attacker == null || victim == null ||
            victim.ownerId != localPlayerOwnerId || attacker.ownerId == victim.ownerId)
            return;
        var reveal = new AttackReveal { attackerId = attacker.GetInstanceID(),
            position = attacker.transform.position, expires = Time.time + attackerRevealDuration };
        for (int i = attackReveals.Count - 1; i >= 0; i--)
        {
            if (attackReveals[i].expires <= Time.time) { attackReveals.RemoveAt(i); continue; }
            if (attackReveals[i].attackerId == reveal.attackerId)
            {
                attackReveals[i] = reveal;
                return;
            }
        }
        attackReveals.Add(reveal);
    }

    void StampAttackReveals()
    {
        float radius = Mathf.Max(0.1f, attackerRevealRadius);
        for (int i = attackReveals.Count - 1; i >= 0; i--)
        {
            AttackReveal reveal = attackReveals[i];
            if (!revealRangedAttacker || reveal.expires <= Time.time)
            {
                attackReveals.RemoveAt(i);
                continue;
            }
            GetStampBounds(reveal.position, radius, out int minX, out int maxX, out int minZ, out int maxZ);
            // 고저차에 의한 가림과 별개로 공격자 주변의 작은 범위를 공개한다.
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 delta = GetCellWorldXZ(x, z) - new Vector2(reveal.position.x, reveal.position.z);
                    if (delta.sqrMagnitude <= radius * radius)
                        visionAccum[z * gridWidth + x] = 255;
                }
        }
    }

    public class VisionStamp
    {
        public Vector3 position;
        public float radius;
        public float edgeSoftness;
        public float eyeHeight;
        public int generation;

        // (칸 번호 << 8) | 밝기
        public readonly List<int> cells = new List<int>();
        public readonly List<int> terrainCells = new List<int>();
    }

    // 시야 계산을 여러 프레임에 나눠 처리하는 동안의 상태. 계산 중에는 fogPixels(게임플레이
    // 판정용)를 건드리지 않고 visionAccum에 모았다가, 다 끝나면 한 번에 반영한다.
    private readonly List<FogOfWarVisionSource> passSources = new List<FogOfWarVisionSource>();
    private bool visionPassActive;
    private int passNextIndex;
    private int passSourcesPerFrame;
    private byte[] visionAccum;

    // 소스 하나를 계산할 때 쓰는 임시 칸 버퍼. 계산 후 해당 범위만 0으로 되돌린다.
    private byte[] stampScratch;

    // 격자·지형 데이터가 바뀌면 올려서 저장된 시야 결과를 모두 무효로 만든다.
    private int stampGeneration;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeTexture();
        CreateMaterials();
    }

    void Start()
    {
        InitializeMap();

        if (autoGridResolution)
            ApplyAutoGridResolution();

        RecomputeCellSize();
        ApplySurfaceSamplingSettings();
        ApplyMaterialSettings(worldFogMaterial);
        ApplyMaterialSettings(uiFogMaterial);

        if (createWorldOverlay)
        {
            if (useDepthProjection && autoEnableCameraDepthTexture)
                EnsureCameraDepthTexture();

            CreateWorldOverlay();
        }

        BuildElevationHeightGrid();
        BuildElevationEdgeHeightGrid();
        BuildHillTerrainGrid();
        stampGeneration++;

        FogOfWarVisionSource[] existingSources =
            FindObjectsOfType<FogOfWarVisionSource>();

        foreach (FogOfWarVisionSource source in existingSources)
            Register(source);

        UpdateFogTexture();

        RTSMinimap minimap = FindObjectOfType<RTSMinimap>();

        // 루트가 아니라 맵이 실제로 그려지는 영역에 붙인다.
        // 루트에 붙이면 비율을 맞추고 남은 여백까지 안개가 덮는다.
        if (minimap != null)
            BindMinimap(minimap.MinimapRect);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (fogTexture != null)
            Destroy(fogTexture);

        if (fogPrevTexture != null)
            Destroy(fogPrevTexture);

        if (worldFogMaterial != null)
            Destroy(worldFogMaterial);

        if (uiFogMaterial != null)
            Destroy(uiFogMaterial);

        if (overlayMesh != null)
            Destroy(overlayMesh);
    }

    // 인스펙터에서 시야 관련 값을 바꾸면 저장된 시야 결과를 다시 계산하게 한다.
    void OnValidate()
    {
        stampGeneration++;
    }

    void Update()
    {
        updateTimer -= Time.deltaTime;

        if (!visionPassActive && updateTimer <= 0f)
            BeginVisionPass();

        if (visionPassActive)
            StepVisionPass(passSourcesPerFrame);

        UpdateFogBlend();
    }

    // 직전 갱신에서 지난 시간 비율만큼 이전 안개 → 새 안개로 섞는다.
    void UpdateFogBlend()
    {
        if (worldFogMaterial == null)
            return;

        float blend = smoothTransition && updateInterval > 0f
            ? Mathf.Clamp01(1f - updateTimer / updateInterval)
            : 1f;

        worldFogMaterial.SetFloat(FogBlendId, blend);
    }

    void InitializeMap()
    {
        if (useMapGridWhenAvailable && MapGrid.Instance != null)
        {
            MapGrid grid = MapGrid.Instance;

            if (grid.UsesNavMesh)
                grid.Refresh();

            mapOrigin = grid.MapOrigin;
            mapSize = grid.MapSize;

            if (mapSize.x > 0f && mapSize.y > 0f)
                return;
        }

        if (MapPlayBounds.TryResolve(
                MapPlayBoundsSource.Auto,
                Vector3.zero,
                new Vector2(256f, 256f),
                out MapPlayBoundsData bounds))
        {
            mapOrigin = bounds.Origin;
            mapSize = new Vector2(bounds.Width, bounds.Length);
            return;
        }

        Debug.LogWarning(
            "FogOfWarManager: Map bounds not found. Using default 256x256 at origin.");

        mapOrigin = Vector3.zero;
        mapSize = new Vector2(256f, 256f);
    }

    void ApplyAutoGridResolution()
    {
        if (mapSize.x <= 0f || mapSize.y <= 0f || autoGridCellSize <= 0f)
            return;

        int newWidth = Mathf.Clamp(
            Mathf.RoundToInt(mapSize.x / autoGridCellSize),
            autoGridResolutionRange.x,
            autoGridResolutionRange.y);

        int newHeight = Mathf.Clamp(
            Mathf.RoundToInt(mapSize.y / autoGridCellSize),
            autoGridResolutionRange.x,
            autoGridResolutionRange.y);

        if (newWidth == gridWidth && newHeight == gridHeight)
            return;

        gridWidth = newWidth;
        gridHeight = newHeight;

        if (fogTexture != null)
            Destroy(fogTexture);

        if (fogPrevTexture != null)
            Destroy(fogPrevTexture);

        InitializeTexture();
    }

    void RecomputeCellSize()
    {
        cellSizeX = gridWidth > 0 ? mapSize.x / gridWidth : 0f;
        cellSizeZ = gridHeight > 0 ? mapSize.y / gridHeight : 0f;
    }

    void ApplySurfaceSamplingSettings()
    {
        FogGroundUtility.overlaySampleMode = overlaySampleMode;
        FogGroundUtility.groundRaycastMask = groundRaycastMask;
        FogGroundUtility.raycastHeightPadding = surfaceRaycastHeightPadding;
        FogGroundUtility.sourceGroundSnapDistance = sourceGroundSnapDistance;
    }

    void InitializeTexture()
    {
        fogTexture = CreateFogTexture();
        fogPrevTexture = CreateFogTexture();

        int pixelCount = gridWidth * gridHeight;
        fogPixels = new Color32[pixelCount];
        displayPixels = new Color32[pixelCount];
        prevDisplayPixels = new Color32[pixelCount];
        blurScratchPixels = new Color32[pixelCount];
        visionAccum = new byte[pixelCount];
        stampScratch = new byte[pixelCount];
        visionPassActive = false;
        stampGeneration++;

        for (int i = 0; i < fogPixels.Length; i++)
            fogPixels[i] = new Color32(0, 0, 0, 255);

        fogTexture.SetPixels32(fogPixels);
        fogTexture.Apply(false);
        fogPrevTexture.SetPixels32(fogPixels);
        fogPrevTexture.Apply(false);

        // 자동 해상도로 텍스처를 다시 만들어도 머티리얼이 새 텍스처를 보게 한다.
        ApplyMaterialSettings(worldFogMaterial);
        ApplyMaterialSettings(uiFogMaterial);
    }

    Texture2D CreateFogTexture()
    {
        Texture2D texture = new Texture2D(
            gridWidth,
            gridHeight,
            TextureFormat.RG16,
            false,
            true);

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    void CreateMaterials()
    {
        worldFogMaterial = CreateFogMaterial(
            worldFogShader,
            "RTS/FogOfWar",
            nameof(worldFogShader));

        uiFogMaterial = CreateFogMaterial(
            uiFogShader,
            "RTS/FogOfWarUI",
            nameof(uiFogShader));

        ApplyMaterialSettings(worldFogMaterial);
        ApplyMaterialSettings(uiFogMaterial);
    }

    // 인스펙터에 꽂아둔 셰이더를 우선 쓴다. 이름으로만 찾으면(Shader.Find) 그 셰이더를
    // 참조하는 머티리얼이 프로젝트에 하나도 없을 때 빌드에서 셰이더가 통째로 잘려나가,
    // 에디터에서는 멀쩡하다가 빌드에서만 안개가 깨진다.
    Material CreateFogMaterial(Shader assignedShader, string shaderName, string fieldName)
    {
        Shader shader = assignedShader != null
            ? assignedShader
            : Shader.Find(shaderName);

        if (shader == null)
        {
            Debug.LogError(
                $"FogOfWarManager: {shaderName} 셰이더를 찾지 못했습니다. " +
                $"인스펙터의 {fieldName}에 셰이더를 직접 넣어주세요.",
                this);

            return null;
        }

        if (assignedShader == null)
        {
            Debug.LogWarning(
                $"FogOfWarManager: {shaderName} 셰이더를 이름으로 찾았습니다. " +
                $"빌드에서 잘려나갈 수 있으니 인스펙터의 {fieldName}에 넣어두세요.",
                this);
        }

        return new Material(shader);
    }

    void ApplyMaterialSettings(Material material)
    {
        if (material == null)
            return;

        material.SetTexture(FogTexId, fogTexture);

        if (material.HasProperty(FogPrevTexId))
            material.SetTexture(FogPrevTexId, fogPrevTexture);

        if (material == worldFogMaterial)
        {
            if (useDepthProjection)
            {
                // 상자 안쪽 면(뒷면)만, 가려짐과 무관하게 그린다. 실제 위치는 깊이로 복원한다.
                material.EnableKeyword(DepthProjectionKeyword);
                material.SetFloat(CullId, (float)CullMode.Front);
                material.SetFloat(ZTestId, (float)CompareFunction.Always);
            }
            else
            {
                material.DisableKeyword(DepthProjectionKeyword);
                material.SetFloat(CullId, (float)CullMode.Off);
                material.SetFloat(ZTestId, (float)CompareFunction.LessEqual);
            }
        }

        material.SetVector(
            "_MapOrigin",
            new Vector4(mapOrigin.x, mapOrigin.z, 0f, 0f));
        material.SetVector(
            "_MapSize",
            new Vector4(mapSize.x, mapSize.y, 0f, 0f));
        material.SetColor("_UnexploredColor", unexploredColor);
        material.SetColor("_ExploredColor", exploredColor);
    }

    void CreateWorldOverlay()
    {
        if (mapSize.x <= 0f || mapSize.y <= 0f)
            return;

        if (worldFogMaterial == null)
        {
            Debug.LogError(
                "FogOfWarManager: 월드 안개 머티리얼이 없어 오버레이를 만들지 않습니다.",
                this);

            return;
        }

        GameObject overlayObject = new GameObject("FogOfWarOverlay");
        overlayObject.transform.SetParent(transform, false);

        float padding = Mathf.Max(0f, overlayMeshPadding);
        float verticalExtent = useDepthProjection
            ? Mathf.Max(1f, depthVolumeVerticalExtent)
            : 0f;

        overlayObject.transform.position = new Vector3(
            mapOrigin.x - padding,
            mapOrigin.y - verticalExtent,
            mapOrigin.z - padding);
        overlayObject.transform.rotation = Quaternion.identity;

        overlayMesh = useDepthProjection
            ? BuildDepthVolumeMesh(
                new Vector3(
                    mapSize.x + padding * 2f,
                    verticalExtent * 2f,
                    mapSize.y + padding * 2f))
            : BuildSurfaceFollowingMesh(
                overlayMeshSegments,
                overlayMeshSegments);

        if (overlayMesh == null)
            return;

        MeshFilter meshFilter = overlayObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = overlayMesh;

        overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
        overlayRenderer.sharedMaterial = worldFogMaterial;
        overlayRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
    }

    // 깊이 기반 안개용 상자. 면을 바깥으로 감아두면 셰이더의 Cull Front로 안쪽 면만 그려져,
    // 카메라가 상자 밖이든 안이든 화면의 각 픽셀을 정확히 한 번 덮는다.
    static Mesh BuildDepthVolumeMesh(Vector3 size)
    {
        Vector3[] vertices = new Vector3[8];

        for (int i = 0; i < 8; i++)
        {
            vertices[i] = new Vector3(
                (i & 1) != 0 ? size.x : 0f,
                (i & 2) != 0 ? size.y : 0f,
                (i & 4) != 0 ? size.z : 0f);
        }

        int[][] faces =
        {
            new[] { 0, 2, 6, 4 }, // -X
            new[] { 1, 3, 7, 5 }, // +X
            new[] { 0, 1, 5, 4 }, // -Y
            new[] { 2, 3, 7, 6 }, // +Y
            new[] { 0, 1, 3, 2 }, // -Z
            new[] { 4, 5, 7, 6 }, // +Z
        };

        Vector3 center = size * 0.5f;
        List<int> triangles = new List<int>(36);

        foreach (int[] face in faces)
        {
            Vector3 faceCenter =
                (vertices[face[0]] + vertices[face[1]] +
                 vertices[face[2]] + vertices[face[3]]) * 0.25f;
            Vector3 outward = faceCenter - center;

            AddOutwardTriangle(triangles, vertices, face[0], face[1], face[2], outward);
            AddOutwardTriangle(triangles, vertices, face[0], face[2], face[3], outward);
        }

        Mesh mesh = new Mesh
        {
            name = "FogOfWarDepthVolume"
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddOutwardTriangle(
        List<int> triangles,
        Vector3[] vertices,
        int a,
        int b,
        int c,
        Vector3 outward)
    {
        // 유니티는 cross(b-a, c-a) 방향을 앞면으로 본다.
        Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);

        triangles.Add(a);

        if (Vector3.Dot(normal, outward) >= 0f)
        {
            triangles.Add(b);
            triangles.Add(c);
        }
        else
        {
            triangles.Add(c);
            triangles.Add(b);
        }
    }

    // 깊이 기반 안개는 카메라 깊이 텍스처가 있어야 동작한다. 파이프라인 에셋에서 꺼져
    // 있어도 메인 카메라만은 깊이 텍스처를 만들게 한다.
    void EnsureCameraDepthTexture()
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            Debug.LogWarning(
                "FogOfWarManager: 메인 카메라가 없어 깊이 텍스처를 켜지 못했습니다. " +
                "URP 파이프라인 에셋의 Depth Texture를 직접 켜주세요.",
                this);

            return;
        }

        camera.depthTextureMode |= DepthTextureMode.Depth;

        UniversalAdditionalCameraData cameraData =
            camera.GetUniversalAdditionalCameraData();

        if (cameraData != null)
            cameraData.requiresDepthOption = CameraOverrideOption.On;
    }

    Mesh BuildSurfaceFollowingMesh(int segmentsX, int segmentsZ)
    {
        int vertCountX = segmentsX + 1;
        int vertCountZ = segmentsZ + 1;
        int vertCount = vertCountX * vertCountZ;

        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[segmentsX * segmentsZ * 6];
        float sinkY = mapOrigin.y - 100f;
        float padding = Mathf.Max(0f, overlayMeshPadding);
        float meshWidth = mapSize.x + padding * 2f;
        float meshLength = mapSize.y + padding * 2f;
        float worldMinX = mapOrigin.x - padding;
        float worldMinZ = mapOrigin.z - padding;

        for (int z = 0; z < vertCountZ; z++)
        {
            float v = z / (float)segmentsZ;

            for (int x = 0; x < vertCountX; x++)
            {
                float u = x / (float)segmentsX;
                float worldX = worldMinX + u * meshWidth;
                float worldZ = worldMinZ + v * meshLength;
                int index = z * vertCountX + x;

                uvs[index] = new Vector2(u, v);

                if (TrySampleOverlaySurfaceHeight(
                        worldX,
                        worldZ,
                        out float surfaceY))
                {
                    vertices[index] = new Vector3(
                        u * meshWidth,
                        surfaceY - mapOrigin.y + overlayHeightOffset,
                        v * meshLength);
                }
                else
                {
                    vertices[index] = new Vector3(
                        u * meshWidth,
                        hideOverlayWithoutSurface ? sinkY - mapOrigin.y : overlayHeightOffset,
                        v * meshLength);
                }
            }
        }

        int triangleIndex = 0;

        for (int z = 0; z < segmentsZ; z++)
        {
            for (int x = 0; x < segmentsX; x++)
            {
                int bottomLeft = z * vertCountX + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + vertCountX;
                int topRight = topLeft + 1;

                if (hideOverlayWithoutSurface &&
                    ShouldSkipOverlayQuad(
                        vertices[bottomLeft],
                        vertices[bottomRight],
                        vertices[topLeft],
                        vertices[topRight],
                        sinkY - mapOrigin.y))
                {
                    continue;
                }

                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = bottomRight;

                triangles[triangleIndex++] = bottomRight;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;
            }
        }

        if (triangleIndex == 0)
            return null;

        if (triangleIndex < triangles.Length)
        {
            int[] trimmed = new int[triangleIndex];
            System.Array.Copy(triangles, trimmed, triangleIndex);
            triangles = trimmed;
        }

        Mesh mesh = new Mesh
        {
            name = "FogOfWarSurfaceMesh"
        };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    bool TrySampleOverlaySurfaceHeight(
        float worldX,
        float worldZ,
        out float surfaceY)
    {
        if (FogGroundUtility.TrySampleSurfaceHeight(worldX, worldZ, out surfaceY))
            return true;

        if (!IsInsidePlayableMap(worldX, worldZ))
        {
            float edgeX = Mathf.Clamp(worldX, mapOrigin.x, mapOrigin.x + mapSize.x);
            float edgeZ = Mathf.Clamp(worldZ, mapOrigin.z, mapOrigin.z + mapSize.y);

            if (FogGroundUtility.TrySampleSurfaceHeight(edgeX, edgeZ, out surfaceY))
                return true;

            surfaceY = mapOrigin.y;
            return true;
        }

        surfaceY = mapOrigin.y;
        return false;
    }

    bool IsInsidePlayableMap(float worldX, float worldZ)
    {
        return worldX >= mapOrigin.x &&
               worldX <= mapOrigin.x + mapSize.x &&
               worldZ >= mapOrigin.z &&
               worldZ <= mapOrigin.z + mapSize.y;
    }

    static bool ShouldSkipOverlayQuad(
        Vector3 bottomLeft,
        Vector3 bottomRight,
        Vector3 topLeft,
        Vector3 topRight,
        float sinkLocalY)
    {
        return IsSunkVertex(bottomLeft, sinkLocalY) ||
               IsSunkVertex(bottomRight, sinkLocalY) ||
               IsSunkVertex(topLeft, sinkLocalY) ||
               IsSunkVertex(topRight, sinkLocalY);
    }

    static bool IsSunkVertex(Vector3 localVertex, float sinkLocalY)
    {
        return localVertex.y <= sinkLocalY + 0.01f;
    }

    /// <summary>
    /// 지형 표면 높이를 낮은 해상도 그리드로 한 번만 레이캐스트/NavMesh 샘플링해서 구운 뒤,
    /// 그 결과를 안개 그리드와 같은 해상도로 한 번에 확장해둔다(둘 다 Start()에서 1회만
    /// 실행). 이렇게 해두면 매 틱 레이마칭 중에는 칸마다 나눗셈·보간 없이 배열 조회
    /// 한 번(SampleTerrainHeightAtCell)으로 높이를 얻을 수 있다.
    /// </summary>
    void BuildElevationHeightGrid()
    {
        elevationHeightByCell = null;

        if (!enableElevationVision ||
            mapSize.x <= 0f || mapSize.y <= 0f ||
            gridWidth <= 0 || gridHeight <= 0)
        {
            return;
        }

        int coarseWidth = Mathf.Max(2, elevationGridResolution);
        int coarseHeight = Mathf.Max(
            2,
            Mathf.RoundToInt(elevationGridResolution * (mapSize.y / mapSize.x)));

        float[] coarseHeights = new float[coarseWidth * coarseHeight];

        for (int z = 0; z < coarseHeight; z++)
        {
            float v = z / (float)(coarseHeight - 1);
            float worldZ = mapOrigin.z + v * mapSize.y;

            for (int x = 0; x < coarseWidth; x++)
            {
                float u = x / (float)(coarseWidth - 1);
                float worldX = mapOrigin.x + u * mapSize.x;

                coarseHeights[z * coarseWidth + x] =
                    FogGroundUtility.TrySampleSurfaceHeight(worldX, worldZ, out float height)
                        ? height
                        : mapOrigin.y;
            }
        }

        elevationHeightByCell = new float[gridWidth * gridHeight];

        for (int z = 0; z < gridHeight; z++)
        {
            float fz = ((z + 0.5f) / gridHeight) * (coarseHeight - 1);
            int z0 = Mathf.FloorToInt(fz);
            int z1 = Mathf.Min(z0 + 1, coarseHeight - 1);
            float tz = fz - z0;

            for (int x = 0; x < gridWidth; x++)
            {
                float fx = ((x + 0.5f) / gridWidth) * (coarseWidth - 1);
                int x0 = Mathf.FloorToInt(fx);
                int x1 = Mathf.Min(x0 + 1, coarseWidth - 1);
                float tx = fx - x0;

                float h00 = coarseHeights[z0 * coarseWidth + x0];
                float h10 = coarseHeights[z0 * coarseWidth + x1];
                float h01 = coarseHeights[z1 * coarseWidth + x0];
                float h11 = coarseHeights[z1 * coarseWidth + x1];

                elevationHeightByCell[z * gridWidth + x] = Mathf.Lerp(
                    Mathf.Lerp(h00, h10, tx),
                    Mathf.Lerp(h01, h11, tx),
                    tz);
            }
        }
    }

    // 각 칸 주변 '절벽 경계 여유 거리' 안의 최대 높이를 가로 → 세로 두 번에 나눠 구한다.
    void BuildElevationEdgeHeightGrid()
    {
        elevationEdgeHeightByCell = null;

        if (elevationHeightByCell == null || highGroundEdgeMargin <= 0f)
            return;

        int radiusX = Mathf.CeilToInt(highGroundEdgeMargin / Mathf.Max(0.0001f, cellSizeX));
        int radiusZ = Mathf.CeilToInt(highGroundEdgeMargin / Mathf.Max(0.0001f, cellSizeZ));
        float[] horizontal = new float[elevationHeightByCell.Length];

        for (int z = 0; z < gridHeight; z++)
        {
            int row = z * gridWidth;

            for (int x = 0; x < gridWidth; x++)
            {
                float max = float.NegativeInfinity;
                int minX = Mathf.Max(0, x - radiusX);
                int maxX = Mathf.Min(gridWidth - 1, x + radiusX);

                for (int nx = minX; nx <= maxX; nx++)
                    max = Mathf.Max(max, elevationHeightByCell[row + nx]);

                horizontal[row + x] = max;
            }
        }

        elevationEdgeHeightByCell = new float[elevationHeightByCell.Length];

        for (int z = 0; z < gridHeight; z++)
        {
            int minZ = Mathf.Max(0, z - radiusZ);
            int maxZ = Mathf.Min(gridHeight - 1, z + radiusZ);

            for (int x = 0; x < gridWidth; x++)
            {
                float max = float.NegativeInfinity;

                for (int nz = minZ; nz <= maxZ; nz++)
                    max = Mathf.Max(max, horizontal[nz * gridWidth + x]);

                elevationEdgeHeightByCell[z * gridWidth + x] = max;
            }
        }
    }

    /// <summary>
    /// groundY(시야 주인이 선 지면)에서 볼 때 이 칸이 '고지대'라 안 보여야 하는지입니다.
    /// 가림 높이 차 이상 높거나, 조금이라도 솟아 있으면서 경계 여유 거리 안에 고지대가 있으면
    /// 가립니다(높이맵 보간으로 절벽 위 가장자리가 낮게 잡혀도 띠처럼 보이지 않도록).
    /// </summary>
    bool IsHigherGroundBlocked(int x, int z, float groundY)
    {
        if (!blockHigherGround || elevationHeightByCell == null)
            return false;

        int index = z * gridWidth + x;
        float rise = elevationHeightByCell[index] - groundY;

        if (rise >= highGroundBlockHeight)
            return true;

        return elevationEdgeHeightByCell != null &&
               rise >= highGroundEdgeMinRise &&
               elevationEdgeHeightByCell[index] - groundY >= highGroundBlockHeight;
    }

    float SampleTerrainHeightAtCell(int x, int z)
    {
        return elevationHeightByCell != null
            ? elevationHeightByCell[z * gridWidth + x]
            : mapOrigin.y;
    }

    public void Register(FogOfWarVisionSource source)
    {
        if (source == null || visionSources.Contains(source))
            return;

        visionSources.Add(source);
    }

    public void Unregister(FogOfWarVisionSource source)
    {
        if (source == null)
            return;

        visionSources.Remove(source);
    }

    public void BindMinimap(RectTransform minimapRect)
    {
        if (minimapRect == null || uiFogMaterial == null)
            return;

        Transform existing = minimapRect.Find("FogOverlay");

        if (existing != null)
            minimapFogOverlay = existing.GetComponent<RawImage>();

        if (minimapFogOverlay == null)
        {
            GameObject overlayObject = new GameObject(
                "FogOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));

            overlayObject.transform.SetParent(minimapRect, false);

            RectTransform overlayRect =
                overlayObject.GetComponent<RectTransform>();

            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            minimapFogOverlay = overlayObject.GetComponent<RawImage>();
            minimapFogOverlay.raycastTarget = false;
        }

        minimapFogOverlay.material = uiFogMaterial;
        minimapFogOverlay.texture = fogTexture;
        minimapFogOverlay.color = Color.white;
    }

    void BuildHillTerrainGrid()
    {
        hillTerrainByCell = new bool[gridWidth * gridHeight];
        hillTerrainExplored = new byte[hillTerrainByCell.Length];
        highGroundTerrainScratch = new byte[hillTerrainByCell.Length];
        highGroundTerrainAccum = new byte[hillTerrainByCell.Length];
        highGroundTerrainDisplay = new byte[hillTerrainByCell.Length];
        CliffPainter[] painters = FindObjectsOfType<CliffPainter>();
        foreach (CliffPainter painter in painters)
        {
            painter.EnsureLookup();
            var tileCache = new Dictionary<CliffPainter.CellCoord, bool>();
            for (int z = 0; z < gridHeight; z++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    Vector2 world = GetCellWorldXZ(x, z);
                    var cell = painter.WorldToCell(new Vector3(world.x, 0f, world.y));
                    if (!tileCache.TryGetValue(cell, out bool isHill))
                    {
                        isHill = painter.IsVisionHillCell(cell);
                        tileCache.Add(cell, isHill);
                    }
                    if (isHill)
                        hillTerrainByCell[z * gridWidth + x] = true;
                }
            }
        }
    }

    // 시야를 한 프레임에 모두 계산해 바로 반영한다(시작 시·RefreshVisionNow용).
    void UpdateFogTexture()
    {
        BeginVisionPass();
        StepVisionPass(int.MaxValue);
    }

    void BeginVisionPass()
    {
        System.Array.Clear(visionAccum, 0, visionAccum.Length);
        System.Array.Clear(highGroundTerrainAccum, 0, highGroundTerrainAccum.Length);
        passSources.Clear();

        for (int i = visionSources.Count - 1; i >= 0; i--)
        {
            FogOfWarVisionSource source = visionSources[i];

            if (source == null)
            {
                visionSources.RemoveAt(i);
                continue;
            }

            if (source.OwnerId == localPlayerOwnerId)
                passSources.Add(source);
        }

        passNextIndex = 0;
        passSourcesPerFrame = Mathf.Max(
            1,
            Mathf.CeilToInt(passSources.Count / (float)Mathf.Max(1, visionSpreadFrames)));
        visionPassActive = true;
    }

    // 소스를 최대 count개 계산하고, 모두 끝났으면 결과를 반영한다.
    void StepVisionPass(int count)
    {
        using (PerfProbe.Measure(PerfCategory.FogVision))
        {
            int end = count >= passSources.Count - passNextIndex
                ? passSources.Count
                : passNextIndex + count;

            for (; passNextIndex < end; passNextIndex++)
            {
                // 계산 도중 파괴된 소스는 건너뛴다.
                FogOfWarVisionSource source = passSources[passNextIndex];
                if (source != null)
                    StampSource(source);
            }
        }

        if (passNextIndex < passSources.Count)
            return;

        using (PerfProbe.Measure(PerfCategory.FogUpload))
            CommitVisionPass();
    }

    void CommitVisionPass()
    {
        StampAttackReveals();
        System.Array.Copy(highGroundTerrainAccum, highGroundTerrainDisplay, highGroundTerrainAccum.Length);
        for (int i = 0; i < fogPixels.Length; i++)
        {
            byte visible = visionAccum[i];
            fogPixels[i].g = visible;

            if (visible > fogPixels[i].r)
                fogPixels[i].r = visible;
        }

        passSources.Clear();
        visionPassActive = false;

        // 부드러운 전환이 새 결과를 반영한 시점부터 시작되도록 여기서 타이머를 돌린다.
        updateTimer = updateInterval;
        UploadDisplayTexture();
    }

    void StampSource(FogOfWarVisionSource source)
    {
        Vector3 position = source.Position;
        float radius = source.VisionRange;
        float edgeSoftness = ResolveEdgeSoftness(source);
        float eyeHeight = ResolveEyeHeight(source);

        VisionStamp stamp = source.CachedStamp;

        if (reuseStationaryVision && stamp != null &&
            stamp.generation == stampGeneration &&
            stamp.radius == radius &&
            stamp.edgeSoftness == edgeSoftness &&
            stamp.eyeHeight == eyeHeight &&
            (stamp.position - position).sqrMagnitude <=
                stationaryMoveTolerance * stationaryMoveTolerance)
        {
            ApplyStamp(stamp);
            return;
        }

        if (stamp == null)
            source.CachedStamp = stamp = new VisionStamp();

        stamp.position = position;
        stamp.radius = radius;
        stamp.edgeSoftness = edgeSoftness;
        stamp.eyeHeight = eyeHeight;
        stamp.generation = stampGeneration;
        stamp.cells.Clear();
        stamp.terrainCells.Clear();

        Vector3 groundPosition = source.GroundPosition;
        StampVision(groundPosition, radius, edgeSoftness, eyeHeight);

        // 임시 버퍼에 찍힌 칸을 저장하고 버퍼는 다시 0으로 비운다.
        GetStampBounds(groundPosition, radius, out int minX, out int maxX, out int minZ, out int maxZ);

        for (int z = minZ; z <= maxZ; z++)
        {
            int row = z * gridWidth;

            for (int x = minX; x <= maxX; x++)
            {
                int index = row + x;
                byte terrainValue = highGroundTerrainScratch[index];
                if (terrainValue > 0)
                {
                    stamp.terrainCells.Add((index << 8) | terrainValue);
                    highGroundTerrainScratch[index] = 0;
                }
                byte value = stampScratch[index];

                if (value == 0)
                    continue;

                stampScratch[index] = 0;
                stamp.cells.Add((index << 8) | value);
            }
        }

        ApplyStamp(stamp);
    }

    void ApplyStamp(VisionStamp stamp)
    {
        foreach (int packed in stamp.terrainCells)
        {
            int index = packed >> 8;
            byte value = (byte)(packed & 0xFF);
            if (value > highGroundTerrainAccum[index]) highGroundTerrainAccum[index] = value;
        }
        List<int> cells = stamp.cells;

        for (int i = 0; i < cells.Count; i++)
        {
            int packed = cells[i];
            int index = packed >> 8;
            byte value = (byte)(packed & 0xFF);

            if (value > visionAccum[index])
                visionAccum[index] = value;
        }
    }

    void GetStampBounds(Vector3 worldPosition, float radius, out int minX, out int maxX, out int minZ, out int maxZ)
    {
        int centerX = WorldToGridX(worldPosition.x);
        int centerZ = WorldToGridZ(worldPosition.z);

        int radiusCellsX = Mathf.Max(1, Mathf.CeilToInt(radius / cellSizeX));
        int radiusCellsZ = Mathf.Max(1, Mathf.CeilToInt(radius / cellSizeZ));

        minX = Mathf.Max(0, centerX - radiusCellsX);
        maxX = Mathf.Min(gridWidth - 1, centerX + radiusCellsX);
        minZ = Mathf.Max(0, centerZ - radiusCellsZ);
        maxZ = Mathf.Min(gridHeight - 1, centerZ + radiusCellsZ);
    }

    // 게임플레이 판정용 원본(fogPixels)은 그대로 두고, 화면에 그릴 사본만 흐려서 올린다.
    void UploadDisplayTexture()
    {
        if (smoothTransition)
        {
            Color32[] previous = prevDisplayPixels;
            prevDisplayPixels = displayPixels;
            displayPixels = previous;

            fogPrevTexture.SetPixels32(prevDisplayPixels);
            fogPrevTexture.Apply(false);
        }

        System.Array.Copy(fogPixels, displayPixels, fogPixels.Length);

        if (revealHillTerrain && hillTerrainExplored != null)
        {
            for (int i = 0; i < displayPixels.Length; i++)
                if (hillTerrainExplored[i] > displayPixels[i].r)
                    displayPixels[i].r = hillTerrainExplored[i];
        }

        if (revealHighGroundTerrain && highGroundTerrainRevealDistance > 0f && highGroundTerrainDisplay != null)
            for (int i = 0; i < displayPixels.Length; i++)
                if (highGroundTerrainDisplay[i] > displayPixels[i].r)
                    displayPixels[i].r = highGroundTerrainDisplay[i];

        for (int i = 0; i < displayBlurIterations; i++)
            BlurDisplayPixels();

        fogTexture.SetPixels32(displayPixels);
        fogTexture.Apply(false);
    }

    // 1-2-1 가중치로 가로 → 세로 한 번씩 흐린다(가장자리는 끝 칸을 반복).
    void BlurDisplayPixels()
    {
        int width = gridWidth;
        int height = gridHeight;

        for (int z = 0; z < height; z++)
        {
            int row = z * width;

            for (int x = 0; x < width; x++)
            {
                Color32 left = displayPixels[row + Mathf.Max(0, x - 1)];
                Color32 center = displayPixels[row + x];
                Color32 right = displayPixels[row + Mathf.Min(width - 1, x + 1)];

                blurScratchPixels[row + x] = new Color32(
                    (byte)((left.r + center.r * 2 + right.r + 2) >> 2),
                    (byte)((left.g + center.g * 2 + right.g + 2) >> 2),
                    0,
                    255);
            }
        }

        for (int z = 0; z < height; z++)
        {
            int below = Mathf.Max(0, z - 1) * width;
            int row = z * width;
            int above = Mathf.Min(height - 1, z + 1) * width;

            for (int x = 0; x < width; x++)
            {
                Color32 down = blurScratchPixels[below + x];
                Color32 center = blurScratchPixels[row + x];
                Color32 up = blurScratchPixels[above + x];

                displayPixels[row + x] = new Color32(
                    (byte)((down.r + center.r * 2 + up.r + 2) >> 2),
                    (byte)((down.g + center.g * 2 + up.g + 2) >> 2),
                    0,
                    255);
            }
        }
    }

    float ResolveEdgeSoftness(FogOfWarVisionSource source)
    {
        return source.EdgeSoftnessOverride >= 0f
            ? source.EdgeSoftnessOverride
            : visionEdgeSoftness;
    }

    float ResolveEyeHeight(FogOfWarVisionSource source)
    {
        return source.EyeHeightOverride >= 0f
            ? source.EyeHeightOverride
            : defaultEyeHeight;
    }

    /// <summary>
    /// 시야 반경(radius) 안을 원형으로 채우되, 지형 높이 차를 광선을 따라 고도각으로
    /// 추적합니다(레이마칭 기반): 눈높이보다 낮거나 같은 지형은 항상 보이므로 높은 곳에서는
    /// 아래가 전부 보이고, 눈높이보다 높은(오르막) 지형만 지평선 규칙을 적용해서 낮은
    /// 곳에서는 가까운 높은 지형 너머가 안 보이게 합니다.
    /// </summary>
    void StampVision(Vector3 worldPosition, float radius, float edgeSoftness, float eyeHeight)
    {
        int centerX = WorldToGridX(worldPosition.x);
        int centerZ = WorldToGridZ(worldPosition.z);
        GetStampBounds(worldPosition, radius, out int minX, out int maxX, out int minZ, out int maxZ);

        // 소스가 서 있는 칸은 항상 밝힌다.
        float centerSlope = float.NegativeInfinity;
        bool centerHill = false;
        float centerEdgeDistance = -1f;
        float centerEdgeHeight = 0f;
        StampVisionCell(centerX, centerZ, worldPosition, eyeHeight, radius, edgeSoftness, ref centerSlope, ref centerHill, ref centerEdgeDistance, ref centerEdgeHeight);

        // 경계 상자의 테두리 칸으로만 광선을 쏜다. 박스 전체를 채우는 것과 점근적으로
        // 같은 비용이면서, 광선을 따라 고도각(지평선)을 추적할 수 있다.
        for (int x = minX; x <= maxX; x++)
        {
            CastVisionRay(centerX, centerZ, x, minZ, worldPosition, eyeHeight, radius, edgeSoftness);

            if (maxZ != minZ)
                CastVisionRay(centerX, centerZ, x, maxZ, worldPosition, eyeHeight, radius, edgeSoftness);
        }

        for (int z = minZ + 1; z <= maxZ - 1; z++)
        {
            CastVisionRay(centerX, centerZ, minX, z, worldPosition, eyeHeight, radius, edgeSoftness);

            if (maxX != minX)
                CastVisionRay(centerX, centerZ, maxX, z, worldPosition, eyeHeight, radius, edgeSoftness);
        }
    }

    void CastVisionRay(
        int x0,
        int z0,
        int x1,
        int z1,
        Vector3 sourceWorldPos,
        float eyeHeight,
        float radius,
        float edgeSoftness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dz = Mathf.Abs(z1 - z0);
        int sx = x0 < x1 ? 1 : -1;
        int sz = z0 < z1 ? 1 : -1;
        int err = dx - dz;

        int x = x0;
        int z = z0;

        // 광선을 따라 지금까지 관측된 가장 높은 고도각(지평선). 눈높이보다 높은(오르막)
        // 칸 중 이 각도를 못 넘는 칸만 가려진 것으로 보고 밝히지 않는다(광선 자체는 계속
        // 진행). 눈높이보다 낮거나 같은 칸은 이 규칙과 무관하게 항상 보인다.
        float maxSlope = float.NegativeInfinity;
        bool connectedHill = false;
        float edgeDistance = -1f;
        float edgeHeight = 0f;

        while (true)
        {
            if (!StampVisionCell(x, z, sourceWorldPos, eyeHeight, radius, edgeSoftness, ref maxSlope, ref connectedHill, ref edgeDistance, ref edgeHeight))
                return;

            if (x == x1 && z == z1)
                return;

            int e2 = 2 * err;

            if (e2 > -dz)
            {
                err -= dz;
                x += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                z += sz;
            }
        }
    }

    Vector2 GetCellWorldXZ(int x, int z)
    {
        return new Vector2(
            mapOrigin.x + (x + 0.5f) * cellSizeX,
            mapOrigin.z + (z + 0.5f) * cellSizeZ);
    }

    /// <summary>
    /// 이 칸을 밝히고(고도각에 따라 밝히지 않을 수도 있음) 광선을 계속 진행해도 되는지
    /// 돌려준다. maxSlope는 이 광선이 지금까지 지나온 지형 중 가장 높게 보인 고도각으로,
    /// 호출할 때마다 갱신된다(광선 하나당 하나씩 유지).
    /// </summary>
    /// <returns>이 칸을 지나 광선을 계속 진행해도 되면 true, 여기서 멈춰야 하면 false.</returns>
    bool StampVisionCell(
        int x,
        int z,
        Vector3 sourceWorldPos,
        float eyeHeight,
        float radius,
        float edgeSoftness,
        ref float maxSlope,
        ref bool connectedHill,
        ref float edgeDistance,
        ref float edgeHeight)
    {
        if (x < 0 || x >= gridWidth || z < 0 || z >= gridHeight)
            return false;

        Vector2 worldXZ = GetCellWorldXZ(x, z);

        float dx = worldXZ.x - sourceWorldPos.x;
        float dz = worldXZ.y - sourceWorldPos.z;
        float distSqr = dx * dx + dz * dz;

        if (distSqr > radius * radius)
            return false;

        float distance = Mathf.Sqrt(distSqr);
        float eyeY = sourceWorldPos.y + eyeHeight;
        bool elevationVisible = true;

        // 낮은 입구부터 같은 광선을 따라 이어지는 오르막만 안내한다.
        // 눈높이 아래도 지평선 검사를 생략하지 않아 다른 절벽 뒤로 새지 않게 한다.
        int cellIndex = z * gridWidth + x;
        float terrainHeight = SampleTerrainHeightAtCell(x, z);
        bool terrainLineVisible = distance < MinElevationSampleDistance ||
            (terrainHeight - eyeY) / distance >= maxSlope - ElevationSlopeEpsilon;
        if (revealHighGroundTerrain && enableElevationVision && blockHigherGround && highGroundTerrainRevealDistance > 0f)
        {
            float rise = terrainHeight - sourceWorldPos.y;
            if (terrainLineVisible && rise >= highGroundBlockHeight &&
                (edgeDistance < 0f || (distance - edgeDistance <= highGroundTerrainRevealDistance &&
                    terrainHeight > edgeHeight && terrainHeight - edgeHeight < highGroundBlockHeight)))
            {
                edgeDistance = distance;
                edgeHeight = terrainHeight;
            }
            // 최초로 보인 고지대의 좁은 띠만 표시한다. 다른 고지대나 뒤쪽 저지대로 확장하지 않는다.
            if (edgeDistance >= 0f && distance - edgeDistance <= highGroundTerrainRevealDistance &&
                rise >= highGroundBlockHeight && Mathf.Abs(terrainHeight - edgeHeight) < highGroundBlockHeight)
            {
                byte strength = (byte)(CalculateVisionStrength(distance, radius, edgeSoftness) * 255f);
                if (strength > highGroundTerrainScratch[cellIndex]) highGroundTerrainScratch[cellIndex] = strength;
            }
        }
        connectedHill = revealHillTerrain && enableElevationVision &&
            hillTerrainByCell != null && hillTerrainByCell[cellIndex] &&
            terrainLineVisible &&
            (connectedHill || terrainHeight - sourceWorldPos.y < highGroundBlockHeight);
        if (connectedHill)
        {
            byte strength = (byte)(CalculateVisionStrength(distance, radius, edgeSoftness) * 255f);
            if (strength > hillTerrainExplored[cellIndex])
                hillTerrainExplored[cellIndex] = strength;
        }

        // 아주 가까운 칸(소스 자신의 칸 등)은 거리가 0에 가까워 고도각이 불안정해지므로
        // 최소 거리 밑에서는 고도각 판정을 건너뛰고 항상 보이는 것으로 취급한다.
        if (enableElevationVision && distance >= MinElevationSampleDistance)
        {
            float cellHeight = SampleTerrainHeightAtCell(x, z);
            float slope = (cellHeight - eyeY) / distance;

            // 눈높이보다 낮거나 같은 지형은 "지평선"에 걸리지 않고 항상 보인다 — 언덕
            // 바로 앞이 급하게 꺼지는 지형이어도, 그 아래로 이어지는 낮은 지형 전체가
            // 가려지면 안 되기 때문이다(고지대에서는 아래가 다 보여야 한다).
            // 눈높이보다 높은 지형(오르막)만 지평선 규칙을 적용해서, 저지대에서 가까운
            // 높은 지형 너머의 더 먼 봉우리는 안 보이게 한다.
            bool atOrBelowEyeLevel = cellHeight <= eyeY;

            elevationVisible = atOrBelowEyeLevel || slope >= maxSlope - ElevationSlopeEpsilon;
            maxSlope = Mathf.Max(maxSlope, slope);

            // 고지대 가림은 이 칸을 밝히지 않을 뿐 광선은 계속 진행한다(너머의 낮은 지형).
            if (IsHigherGroundBlocked(x, z, sourceWorldPos.y))
                elevationVisible = false;
        }

        if (elevationVisible)
        {
            int index = z * gridWidth + x;
            float strength = CalculateVisionStrength(distance, radius, edgeSoftness);
            byte value = (byte)(strength * 255f);

            if (value > stampScratch[index])
                stampScratch[index] = value;
        }

        return true;
    }

    float CalculateVisionStrength(float distance, float radius, float edgeSoftness)
    {
        if (edgeSoftness <= 0f)
            return distance <= radius ? 1f : 0f;

        float innerRadius = Mathf.Max(0f, radius - edgeSoftness);

        if (distance <= innerRadius)
            return 1f;

        if (distance >= radius)
            return 0f;

        return 1f - (distance - innerRadius) / edgeSoftness;
    }

    public bool IsVisible(Vector3 worldPosition)
    {
        if (!TrySampleFog(worldPosition, out byte explored, out byte visible))
            return false;

        return visible > (byte)(visibilityThreshold * 255f);
    }

    /// <summary>
    /// 스폰 회피용: 적이 플레이어에게 그려질 수 있으면 true.
    /// entityShowThreshold를 써서 시야 가장자리 스폰을 막습니다.
    /// </summary>
    public bool IsVisibleForSpawnAvoidance(Vector3 worldPosition)
    {
        if (!TrySampleFog(worldPosition, out _, out byte visible))
            return false;

        return visible > (byte)(entityShowThreshold * 255f);
    }

    /// <summary>시야 텍스처를 즉시 갱신합니다. 스포너 배치 직전에 호출합니다.</summary>
    public void RefreshVisionNow()
    {
        if (fogTexture == null || fogPixels == null)
            return;

        UpdateFogTexture();
    }

    public bool EvaluateEntityGameplayVisibility(Bounds worldBounds, bool wasVisible)
    {
        if (wasVisible)
            return !IsEntityFullyOutsideVision(worldBounds);

        return IsEntityPartiallyVisible(worldBounds);
    }

    public bool IsEntityPartiallyVisible(Bounds worldBounds)
    {
        byte showCutoff = (byte)(entityShowThreshold * 255f);

        foreach (Vector3 sample in GetEntityGroundSamplePoints(worldBounds))
        {
            if (TrySampleFog(sample, out _, out byte visible) && visible > showCutoff)
                return true;
        }

        return false;
    }

    public bool IsEntityFullyOutsideVision(Bounds worldBounds)
    {
        byte hideCutoff = (byte)(entityHideThreshold * 255f);

        foreach (Vector3 sample in GetEntityGroundSamplePoints(worldBounds))
        {
            if (TrySampleFog(sample, out _, out byte visible) && visible > hideCutoff)
                return false;
        }

        return true;
    }

    public bool IsEntityExplored(Bounds worldBounds)
    {
        foreach (Vector3 sample in GetEntityGroundSamplePoints(worldBounds))
        {
            if (TrySampleFog(sample, out byte explored, out _) && explored > 127)
                return true;
        }

        return false;
    }

    // 참고: TrySampleFog는 X/Z만으로 안개 그리드를 조회하고 Y는 쓰지 않으므로,
    // 여기서 지면 높이를 다시 레이캐스트/NavMesh로 스냅할 필요가 없다(불필요한 비용 제거).
    IEnumerable<Vector3> GetEntityGroundSamplePoints(Bounds worldBounds)
    {
        float padding = entityVisibilityPadding;
        float minX = worldBounds.min.x - padding;
        float maxX = worldBounds.max.x + padding;
        float minZ = worldBounds.min.z - padding;
        float maxZ = worldBounds.max.z + padding;
        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;
        float referenceY = worldBounds.center.y;

        yield return new Vector3(centerX, referenceY, centerZ);
        yield return new Vector3(minX, referenceY, minZ);
        yield return new Vector3(maxX, referenceY, minZ);
        yield return new Vector3(minX, referenceY, maxZ);
        yield return new Vector3(maxX, referenceY, maxZ);
        yield return new Vector3(centerX, referenceY, minZ);
        yield return new Vector3(centerX, referenceY, maxZ);
        yield return new Vector3(minX, referenceY, centerZ);
        yield return new Vector3(maxX, referenceY, centerZ);
    }

    /// <summary>
    /// 적 AI 등 안개를 쓸 수 없는 진영이 지형 가림만으로 시야를 판정할 수 있는지입니다.
    /// </summary>
    public bool AiUsesTerrainLineOfSight =>
        aiTerrainLineOfSight &&
        enableElevationVision &&
        elevationHeightByCell != null;

    /// <summary>
    /// from(눈높이 eyeHeight)에서 to가 지형에 가려지지 않고 보이는지 검사합니다.
    /// 안개 텍스처가 아니라 Start()에서 구워둔 지형 높이 격자만 쓰기 때문에 소속과
    /// 무관하게 쓸 수 있고, 칸을 따라가며 배열을 읽는 게 전부라 비용이 거의 없습니다.
    /// 가림 판정 규칙은 StampVisionCell(플레이어 안개)과 같습니다: 눈높이보다 낮거나
    /// 같은 지형은 항상 보이고, 눈높이보다 높은 지형만 지평선(고도각) 규칙을 받습니다.
    /// eyeHeight가 음수면 Default Eye Height를 씁니다.
    /// </summary>
    public bool HasTerrainLineOfSight(Vector3 from, Vector3 to, float eyeHeight = -1f)
    {
        if (!enableElevationVision || elevationHeightByCell == null)
            return true;

        if (gridWidth <= 0 || gridHeight <= 0)
            return true;

        int x0 = WorldToGridX(from.x);
        int z0 = WorldToGridZ(from.z);
        int x1 = WorldToGridX(to.x);
        int z1 = WorldToGridZ(to.z);

        float eyeY = from.y + (eyeHeight >= 0f ? eyeHeight : defaultEyeHeight);
        float maxSlope = float.NegativeInfinity;

        int dx = Mathf.Abs(x1 - x0);
        int dz = Mathf.Abs(z1 - z0);
        int sx = x0 < x1 ? 1 : -1;
        int sz = z0 < z1 ? 1 : -1;
        int err = dx - dz;

        int x = x0;
        int z = z0;

        while (true)
        {
            // 격자를 벗어나면 가렸다고 볼 근거가 없으니 보이는 것으로 둔다.
            if (x < 0 || x >= gridWidth || z < 0 || z >= gridHeight)
                return true;

            bool isTargetCell = x == x1 && z == z1;

            Vector2 worldXZ = GetCellWorldXZ(x, z);
            float deltaX = worldXZ.x - from.x;
            float deltaZ = worldXZ.y - from.z;
            float distance = Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);

            // 아주 가까운 칸은 고도각이 불안정하므로 판정하지 않는다.
            if (distance >= MinElevationSampleDistance)
            {
                float cellHeight = SampleTerrainHeightAtCell(x, z);
                float slope = (cellHeight - eyeY) / distance;
                bool atOrBelowEyeLevel = cellHeight <= eyeY;
                bool visible =
                    atOrBelowEyeLevel || slope >= maxSlope - ElevationSlopeEpsilon;

                if (isTargetCell)
                    return visible && !IsHigherGroundBlocked(x, z, from.y);

                maxSlope = Mathf.Max(maxSlope, slope);
            }
            else if (isTargetCell)
            {
                return true;
            }

            int e2 = 2 * err;

            if (e2 > -dz)
            {
                err -= dz;
                x += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                z += sz;
            }
        }
    }

    public bool IsExplored(Vector3 worldPosition)
    {
        if (!TrySampleFog(worldPosition, out byte explored, out byte visible))
            return false;

        return explored > 127;
    }

    bool TrySampleFog(
        Vector3 worldPosition,
        out byte explored,
        out byte visible)
    {
        explored = 0;
        visible = 0;

        if (fogTexture == null || fogPixels == null)
            return false;

        if (mapSize.x <= 0f || mapSize.y <= 0f)
            return false;

        float normalizedX = (worldPosition.x - mapOrigin.x) / mapSize.x;
        float normalizedZ = (worldPosition.z - mapOrigin.z) / mapSize.y;

        // 맵 밖은 샘플하지 않는다(가장자리로 Clamp하면 시야 판정이 왜곡된다).
        if (normalizedX < 0f || normalizedX > 1f ||
            normalizedZ < 0f || normalizedZ > 1f)
        {
            return false;
        }

        int x = Mathf.Clamp(
            Mathf.FloorToInt(normalizedX * gridWidth),
            0,
            gridWidth - 1);
        int z = Mathf.Clamp(
            Mathf.FloorToInt(normalizedZ * gridHeight),
            0,
            gridHeight - 1);

        Color32 pixel = fogPixels[z * gridWidth + x];
        explored = pixel.r;
        visible = pixel.g;
        return true;
    }

    int WorldToGridX(float worldX)
    {
        float normalized = (worldX - mapOrigin.x) / mapSize.x;
        return Mathf.Clamp(
            Mathf.FloorToInt(normalized * gridWidth),
            0,
            gridWidth - 1);
    }

    int WorldToGridZ(float worldZ)
    {
        float normalized = (worldZ - mapOrigin.z) / mapSize.y;
        return Mathf.Clamp(
            Mathf.FloorToInt(normalized * gridHeight),
            0,
            gridHeight - 1);
    }
}
