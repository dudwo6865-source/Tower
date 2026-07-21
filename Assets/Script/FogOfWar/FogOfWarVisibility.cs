using UnityEngine;

[DisallowMultipleComponent]
public class FogOfWarVisibility : MonoBehaviour
{
    [Tooltip("탐색된 지역에서 현재 시야 밖일 때 숨깁니다. 한 번 시야에 들어온 적은 탐색 지역 안에서는 시야 밖에서도 계속 표시됩니다.")]
    public bool hideWhenNotVisible = true;

    private SelectableEntity selectableEntity;
    private Renderer[] renderers;
    private WorldHealthBar healthBar;

    private bool lastVisible = true;
    private bool lastShown = true;
    private bool hasBeenRevealed;

    public bool IsCurrentlyVisible => lastVisible;

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

        lastVisible = manager.EvaluateEntityGameplayVisibility(
            GetVisibilityBounds(),
            lastVisible);

        if (lastVisible)
            hasBeenRevealed = true;

        lastShown = ShouldShow(lastVisible);
        ApplyVisibility(lastShown);
    }

    Bounds GetVisibilityBounds()
    {
        if (selectableEntity != null)
            return selectableEntity.SelectionBounds;

        if (renderers != null && renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        return new Bounds(transform.position, Vector3.one);
    }

    void LateUpdate()
    {
        FogOfWarManager manager = FogOfWarManager.Instance;

        if (manager == null || selectableEntity == null)
            return;

        if (selectableEntity.ownerId == manager.LocalPlayerOwnerId)
            return;

        bool inVision = manager.EvaluateEntityGameplayVisibility(
            GetVisibilityBounds(),
            lastVisible);

        if (inVision)
            hasBeenRevealed = true;

        lastVisible = inVision;

        bool shouldShow = ShouldShow(inVision);

        if (shouldShow == lastShown)
            return;

        lastShown = shouldShow;
        ApplyVisibility(shouldShow);
    }

    bool ShouldShow(bool inVision)
    {
        if (!hideWhenNotVisible)
            return true;

        if (inVision)
            return true;

        if (!hasBeenRevealed)
            return false;

        FogOfWarManager manager = FogOfWarManager.Instance;

        if (manager == null)
            return true;

        return manager.IsEntityExplored(GetVisibilityBounds());
    }

    void ApplyVisibility(bool show)
    {
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
