using UnityEngine;

[DisallowMultipleComponent]
public class FogOfWarVisibility : MonoBehaviour
{
    [Label("시야 밖이면 숨김")]
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

        FogOfWarManager manager = FogOfWarManager.Instance;

        if (manager == null)
            return true;

        // 안개가 화면에 안 그려지는 상태(셰이더 누락 등)에서 유닛만 숨기면 적이 이유 없이
        // 사라진 것처럼 보인다. 이럴 땐 숨기지 않고 그냥 보여준다.
        if (!manager.HasWorldFogMaterial)
            return true;

        if (!hasBeenRevealed)
            return false;

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
