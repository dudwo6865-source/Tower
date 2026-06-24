using UnityEngine;

[DisallowMultipleComponent]
public class FogOfWarVisionSource : MonoBehaviour
{
    [Tooltip("이 오브젝트가 밝히는 시야 반경(월드 단위)입니다.")]
    public float visionRange = 12f;

    private SelectableEntity selectableEntity;

    public int OwnerId =>
        selectableEntity != null ? selectableEntity.ownerId : 0;

    public Vector3 Position => transform.position;

    public Vector3 GroundPosition => FogGroundUtility.SnapToGround(transform.position);

    public float VisionRange => visionRange;

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
