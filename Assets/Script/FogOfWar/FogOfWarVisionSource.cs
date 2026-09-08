using UnityEngine;

[DisallowMultipleComponent]
public class FogOfWarVisionSource : MonoBehaviour
{
    [Tooltip("이 오브젝트가 밝히는 시야 반경(월드 단위)입니다.")]
    public float visionRange = 12f;

    [Tooltip("0 이상이면 FogOfWarManager의 기본 Vision Edge Softness 대신 이 값을 사용합니다. 음수면 매니저 기본값을 따릅니다.")]
    public float edgeSoftnessOverride = -1f;

    private SelectableEntity selectableEntity;

    public int OwnerId =>
        selectableEntity != null ? selectableEntity.ownerId : 0;

    public Vector3 Position => transform.position;

    public Vector3 GroundPosition => FogGroundUtility.SnapToGround(transform.position);

    public float VisionRange => visionRange;

    public float EdgeSoftnessOverride => edgeSoftnessOverride;

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
