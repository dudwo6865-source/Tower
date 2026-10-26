using UnityEngine;
using UnityEngine.UI;

// 별도 아트 에셋 없이 기본 본부·스포너 아이콘과 경고 링을 그린다.
public class MinimapTacticalGlyph : MaskableGraphic
{
    public enum Shape { Headquarters, Spawner, Ring }
    Shape shape;
    public void SetShape(Shape value)
    {
        if (shape == value) return;
        shape = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(rect.width, rect.height) * .43f;
        float thickness = Mathf.Max(1.2f, radius * .22f);
        Vector2 center = rect.center;
        if (shape == Shape.Ring)
        {
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Line(vh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, Mathf.Max(1.2f, radius * .1f));
            }
            return;
        }
        if (shape == Shape.Headquarters)
        {
            Vector2 left = center + new Vector2(-radius, 0), right = center + new Vector2(radius, 0);
            Line(vh, left, center + Vector2.up * radius, thickness);
            Line(vh, center + Vector2.up * radius, right, thickness);
            Line(vh, left, left + Vector2.down * radius, thickness);
            Line(vh, right, right + Vector2.down * radius, thickness);
            Line(vh, left + Vector2.down * radius, right + Vector2.down * radius, thickness);
            Line(vh, center + Vector2.down * radius, center + Vector2.down * radius * .25f, thickness);
        }
        else
        {
            var points = new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            for (int i = 0; i < 4; i++)
                Line(vh, center + points[i] * radius, center + points[(i + 1) % 4] * radius, thickness);
            Line(vh, center - Vector2.up * radius * .35f, center + Vector2.up * radius * .35f, thickness);
            Line(vh, center - Vector2.right * radius * .35f, center + Vector2.right * radius * .35f, thickness);
        }
    }

    void Line(VertexHelper vh, Vector2 from, Vector2 to, float thickness)
    {
        Vector2 delta = (to - from).normalized;
        Vector2 side = new Vector2(-delta.y, delta.x) * thickness * .5f;
        int start = vh.currentVertCount;
        vh.AddVert(from - side, color, Vector2.zero);
        vh.AddVert(from + side, color, Vector2.zero);
        vh.AddVert(to + side, color, Vector2.zero);
        vh.AddVert(to - side, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
