using UnityEngine;
using UnityEngine.Rendering;

// 실제 승리 조건에 연결된 거점만 표시한다. 시야나 탐색 정보는 변경하지 않는다.
[DefaultExecutionOrder(200)]
public class CaptureMissionIndicator : MonoBehaviour
{
    [Label("미니맵")]
    public RTSMinimap minimap;
    [Label("미니맵 거점 표시")]
    [Tooltip("에디터에서 만든 표시를 연결합니다. 크기와 색은 해당 UI에서 조절합니다.")]
    public RectTransform minimapMarker;
    [Label("월드 표시 머티리얼")]
    public Material worldMaterial;
    [Label("월드 표시 카메라")]
    [Tooltip("비워두면 메인 카메라를 사용합니다.")]
    public Camera worldCamera;
    [Label("월드 표시 레이어 번호")]
    [Tooltip("카메라가 표시하는 레이어를 지정합니다. 기본값 0은 Default입니다.")]
    [Range(0, 31)] public int worldLayer;
    [Label("테두리 두께")]
    [Min(0.01f)] public float ringWidth = 0.25f;
    [Label("테두리 분할 수")]
    [Range(16, 256)] public int segments = 96;
    [Label("지면 높이 여유")]
    public float groundOffset = 0.15f;

    Mesh ring;
    CapturePoint lastPoint;
    Vector3 lastPosition;
    float lastRadius, lastWidth, lastOffset;
    int lastSegments;

    void LateUpdate()
    {
        GameResultManager result = GameResultManager.Instance;
        CapturePoint point = result != null ? result.capturePoint : null;
        bool visible = result != null && result.winByCapturingPoint && !result.IsGameOver &&
            string.IsNullOrEmpty(result.CaptureError) && point != null && point.isActiveAndEnabled && point.HasValidRadius;

        if (minimapMarker != null)
        {
            bool showMinimap = visible && minimap != null && minimap.IsReady;
            minimapMarker.gameObject.SetActive(showMinimap);
            if (showMinimap)
            {
                RectTransform area = minimap.MinimapRect;
                if (minimapMarker.parent != area) minimapMarker.SetParent(area, false);
                Vector2 local = minimap.WorldToMinimapLocal(point.transform.position);
                minimapMarker.position = area.TransformPoint(new Vector3(local.x, local.y, 0f));
                // 안개와 일반 유닛 표시 위에서 목표 위치를 안내한다.
                minimapMarker.SetAsLastSibling();
            }
        }
        if (!visible || worldMaterial == null) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        if (ring == null || point != lastPoint || !point.transform.position.Equals(lastPosition) ||
            point.radius != lastRadius || ringWidth != lastWidth || segments != lastSegments || groundOffset != lastOffset)
            RebuildRing(point);

        Graphics.DrawMesh(ring, Matrix4x4.identity, worldMaterial, worldLayer,
            worldCamera, 0, null, ShadowCastingMode.Off, false);
    }

    void RebuildRing(CapturePoint point)
    {
        if (ring == null) ring = new Mesh { name = "점령 거점 테두리" };
        int count = Mathf.Clamp(segments, 16, 256);
        Vector3[] vertices = new Vector3[count * 2];
        int[] triangles = new int[count * 6];
        float halfWidth = Mathf.Min(Mathf.Max(0.01f, ringWidth) * 0.5f, point.radius);
        Vector3 center = point.transform.position;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            for (int edge = 0; edge < 2; edge++)
            {
                Vector3 position = center + direction * (point.radius + (edge == 0 ? -halfWidth : halfWidth));
                if (FogGroundUtility.TrySampleSurfaceHeight(position.x, position.z, out float height)) position.y = height;
                position.y += groundOffset;
                vertices[i * 2 + edge] = position;
            }
            int a = i * 2, b = ((i + 1) % count) * 2, t = i * 6;
            triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = a + 1;
            triangles[t + 3] = a + 1; triangles[t + 4] = b; triangles[t + 5] = b + 1;
        }
        ring.Clear();
        ring.vertices = vertices;
        ring.triangles = triangles;
        ring.RecalculateBounds();
        lastPoint = point;
        lastPosition = center;
        lastRadius = point.radius;
        lastWidth = ringWidth;
        lastOffset = groundOffset;
        lastSegments = segments;
    }

    void OnDisable()
    {
        if (minimapMarker != null) minimapMarker.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (ring != null) Destroy(ring);
    }
}
