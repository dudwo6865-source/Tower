using UnityEngine;

[DisallowMultipleComponent]
public class FogOfWarVisibility : MonoBehaviour
{
    [Tooltip("탐색됐지만 현재 시야 밖일 때도 숨깁니다. (적 유닛·건물에 권장)")]
    public bool hideWhenNotVisible = true;

    private SelectableEntity selectableEntity;
    private Renderer[] renderers;
    private WorldHealthBar healthBar;

    private bool lastVisible = true;

    void Awake()
    {
        selectableEntity = GetComponent<SelectableEntity>();
        renderers = GetComponentsInChildren<Renderer>(true);
        healthBar = GetComponent<WorldHealthBar>();
    }

    void Start()
    {
        FogOfWarManager manager = FogOfWarManager.Instance;

        if (manager == null || selectableEntity == null)
            return;

        if (selectableEntity.ownerId == manager.LocalPlayerOwnerId)
            return;

        lastVisible = manager.IsVisible(GetGroundPosition());
        ApplyVisibility(lastVisible);
    }

    Vector3 GetGroundPosition()
    {
        return FogGroundUtility.SnapToGround(transform.position);
    }

    void LateUpdate()
    {
        FogOfWarManager manager = FogOfWarManager.Instance;

        if (manager == null || selectableEntity == null)
            return;

        if (selectableEntity.ownerId == manager.LocalPlayerOwnerId)
            return;

        bool visible = manager.IsVisible(GetGroundPosition());

        if (visible == lastVisible)
            return;

        lastVisible = visible;
        ApplyVisibility(visible);
    }

    void ApplyVisibility(bool visible)
    {
        bool show = visible || !hideWhenNotVisible;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            renderer.enabled = show;
        }

        if (healthBar != null)
            healthBar.RefreshVisibility();
    }
}
