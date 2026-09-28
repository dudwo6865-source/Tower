using UnityEngine;

[DisallowMultipleComponent]
public class FogOfWarVisionSource : MonoBehaviour
{
    [Label("시야 반경")]
    [Tooltip("이 오브젝트가 밝히는 시야 반경(월드 단위)입니다.")]
    public float visionRange = 12f;

    [Label("가장자리 부드러움 덮어쓰기")]
    [Tooltip("0 이상이면 FogOfWarManager의 기본 Vision Edge Softness 대신 이 값을 사용합니다. 음수면 매니저 기본값을 따릅니다.")]
    public float edgeSoftnessOverride = -1f;

    [Label("눈높이 덮어쓰기")]
    [Tooltip("0 이상이면 FogOfWarManager의 기본 Default Eye Height 대신 이 값을 사용합니다. 음수면 매니저 기본값을 따릅니다. 고지대 시야 계산(높은 곳에서 아래가 보이는 정도)에 씁니다.")]
    public float eyeHeightOverride = -1f;

    [Label("소유자 ID (유닛·건물이 아닐 때)")]
    [Tooltip("SelectableEntity가 없는 오브젝트(예: 본부 배치 마커)에서 쓸 소유자 ID입니다. 1(플레이어)로 두면 플레이어 시야로 안개를 밝힙니다. 유닛·건물에서는 SelectableEntity의 소유자를 쓰므로 무시됩니다.")]
    public int fallbackOwnerId = 0;

    private SelectableEntity selectableEntity;

    public int OwnerId =>
        selectableEntity != null ? selectableEntity.ownerId : fallbackOwnerId;

    public Vector3 Position => transform.position;

    public Vector3 GroundPosition => FogGroundUtility.SnapToGround(transform.position);

    public float VisionRange => visionRange;

    public float EdgeSoftnessOverride => edgeSoftnessOverride;

    public float EyeHeightOverride => eyeHeightOverride;

    void Awake()
    {
        selectableEntity = GetComponent<SelectableEntity>();
    }

    void OnEnable()
    {
        RegisterToManager();
    }

    void Start()
    {
        RegisterToManager();
    }

    void OnDisable()
    {
        if (FogOfWarManager.Instance != null)
            FogOfWarManager.Instance.Unregister(this);
    }

    void RegisterToManager()
    {
        if (FogOfWarManager.Instance != null)
            FogOfWarManager.Instance.Register(this);
    }
}
