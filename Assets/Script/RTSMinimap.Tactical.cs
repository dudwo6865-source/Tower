using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class RTSMinimap
{
    [Header("지형 동선 표시")]
    [Label("절벽 윤곽 표시")]
    public bool showCliffOutlines = true;
    [Label("절벽 윤곽 색")]
    public Color cliffOutlineColor = new Color(.12f, .1f, .07f, .8f);
    [Label("절벽 판정 높이 차")]
    [Tooltip("인접 픽셀의 높이 차가 이 값 이상이면 절벽 윤곽을 표시합니다.")]
    [Min(.1f)] public float cliffHeightThreshold = 2f;
    [Label("언덕 진입 표시")]
    public bool showHillRoutes = true;
    [Label("언덕 표시 색")]
    public Color hillRouteColor = new Color(1f, .8f, .25f, .9f);
    [Label("언덕 최소 경사")]
    [Range(1f, 70f)] public float hillMinSlope = 15f;
    [Label("언덕 최대 경사")]
    [Range(1f, 80f)] public float hillMaxSlope = 55f;
    [Label("언덕 화살표 크기")]
    [Tooltip("미니맵 텍스처에서 오르막 방향을 표시하는 화살표 크기입니다.")]
    [Range(2, 12)] public int hillArrowSize = 4;
    [Header("미니맵 명령")]
    [Label("우클릭 유닛 이동")]
    public bool rightClickMovesUnits = true;

    // 누른 순간 처리하여 건물 배치 취소와 같은 클릭이 이동으로 재사용되지 않게 한다.
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right || !rightClickMovesUnits || !IsReady) return;
        var placement = TowerPlacementController.Instance;
        if (placement != null && placement.IsPlacing)
        {
            placement.CancelPlacement();
            UISoundManager.PlayCommandCancel();
            return;
        }
        // 배치 컨트롤러가 같은 프레임에 먼저 취소했어도 그 클릭은 소비한다.
        if (placement != null && placement.LastCancelFrame == Time.frameCount) return;
        var commands = UnitCommandController.Instance;
        if (commands != null && commands.HasPendingMode)
        {
            commands.CancelMode();
            UISoundManager.PlayCommandCancel();
            return;
        }
        if (Time.timeScale <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
            MinimapRect, eventData.position, eventData.pressEventCamera, out Vector2 local) || !MinimapRect.rect.Contains(local)) return;
        Vector3 point = MinimapLocalToWorld(local);
        point.y = MapPlayBounds.SampleGroundHeight(point);
        if (UnitCommandHandler.IssueMoveToSelection(point))
            UnitCommandController.PlayRightClickConfirmPulse(point, UnitCommandMode.Move);
    }

    void DrawTerrainRoutes(Color[] pixels, RaycastHit[] hits, int width, int height)
    {
        if (!showCliffOutlines && !showHillRoutes) return;
        var slopes = new bool[hits.Length];
        float minNormal = Mathf.Cos(Mathf.Max(hillMinSlope, hillMaxSlope) * Mathf.Deg2Rad);
        float maxNormal = Mathf.Cos(Mathf.Min(hillMinSlope, hillMaxSlope) * Mathf.Deg2Rad);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (hits[index].collider == null) continue;
                slopes[index] = showHillRoutes && hits[index].normal.y >= minNormal && hits[index].normal.y <= maxNormal;
                if (slopes[index]) pixels[index] = Color.Lerp(pixels[index], hillRouteColor, .2f * hillRouteColor.a);
                if (!showCliffOutlines || slopes[index]) continue;
                bool edge = x > 0 && IsCliffEdge(hits[index], hits[index - 1])
                    || x + 1 < width && IsCliffEdge(hits[index], hits[index + 1])
                    || y > 0 && IsCliffEdge(hits[index], hits[index - width])
                    || y + 1 < height && IsCliffEdge(hits[index], hits[index + width]);
                if (edge) pixels[index] = Color.Lerp(pixels[index], cliffOutlineColor, cliffOutlineColor.a);
            }
        // 이어진 경사면마다 한 개만 표시하여 작은 미니맵에 화살표가 겹치지 않게 한다.
        var queue = new Queue<int>();
        var region = new List<int>();
        for (int seed = 0; seed < slopes.Length; seed++)
        {
            if (!slopes[seed]) continue;
            queue.Enqueue(seed);
            slopes[seed] = false;
            region.Clear();
            Vector2 center = Vector2.zero, direction = Vector2.zero;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(), x = current % width, y = current / width;
                region.Add(current);
                center += new Vector2(x, y);
                direction += new Vector2(-hits[current].normal.x, -hits[current].normal.z);
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? -1 : d == 1 ? 1 : 0);
                    int ny = y + (d == 2 ? -1 : d == 3 ? 1 : 0);
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int next = ny * width + nx;
                    if (!slopes[next]) continue;
                    slopes[next] = false;
                    queue.Enqueue(next);
                }
            }
            if (region.Count < 2 || direction.sqrMagnitude < .01f) continue;
            center /= region.Count;
            int closest = seed;
            float distance = float.MaxValue;
            foreach (int pixel in region)
            {
                float nextDistance = (new Vector2(pixel % width, pixel / width) - center).sqrMagnitude;
                if (nextDistance < distance) { closest = pixel; distance = nextDistance; }
            }
            center = new Vector2(closest % width, closest / width);
            direction = new Vector2(direction.x * width / mapBounds.Width, direction.y * height / mapBounds.Length).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x);
            float size = Mathf.Clamp(hillArrowSize, 2, 12);
            Vector2 tip = center + direction * size;
            DrawMapLine(pixels, width, height, center - direction * size, tip, hillRouteColor);
            DrawMapLine(pixels, width, height, tip, center + side * size * .65f, hillRouteColor);
            DrawMapLine(pixels, width, height, tip, center - side * size * .65f, hillRouteColor);
        }
    }

    bool IsCliffEdge(RaycastHit a, RaycastHit b) => b.collider == null || Mathf.Abs(a.point.y - b.point.y) >= Mathf.Max(.1f, cliffHeightThreshold);

    static void DrawMapLine(Color[] pixels, int width, int height, Vector2 from, Vector2 to, Color color)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) * 2));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(from, to, i / (float)steps);
            int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y);
            if (x >= 0 && x < width && y >= 0 && y < height)
                pixels[y * width + x] = Color.Lerp(pixels[y * width + x], color, color.a);
        }
    }
}
