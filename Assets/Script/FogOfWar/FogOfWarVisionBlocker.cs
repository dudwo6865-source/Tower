using UnityEngine;

[DisallowMultipleComponent]
public class FogOfWarVisionBlocker : MonoBehaviour
{
    [Tooltip("켜면 아래 Manual Bounds 값을 그대로 사용합니다. 끄면 자식의 Collider(우선) 또는 Renderer Bounds를 합쳐 자동 계산합니다.")]
    public bool useManualBounds;

    [Tooltip("차단 영역 중심(로컬 오프셋)입니다. Use Manual Bounds가 켜져 있을 때만 사용됩니다.")]
    public Vector3 manualBoundsCenter;

    [Tooltip("차단 영역 크기(월드 단위)입니다. Use Manual Bounds가 켜져 있을 때만 사용됩니다.")]
    public Vector3 manualBoundsSize = new Vector3(2f, 4f, 2f);

    public Bounds GetWorldBounds()
    {
        if (useManualBounds)
            return new Bounds(transform.TransformPoint(manualBoundsCenter), manualBoundsSize);

        Collider[] colliders = GetComponentsInChildren<Collider>();

        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;

            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);

            return bounds;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        return new Bounds(transform.position, Vector3.one * 2f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Bounds bounds = GetWorldBounds();
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
#endif
}
