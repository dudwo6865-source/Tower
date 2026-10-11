using UnityEngine;

// 월드 공간의 선분을 원형 브러시로 확장해 찍는다. 드래그 이벤트 사이도 빈틈없이 칠한다.
public static class TerrainRGBMaskBrushMath
{
    public static Color32 ChannelColor(int channel)
    {
        return channel == 1 ? new Color32(0, 255, 0, 255) :
            channel == 2 ? new Color32(0, 0, 255, 255) : new Color32(255, 0, 0, 255);
    }

    public static bool PaintSegment(Color32[] pixels, Color32[] strokeStart, float[] coverage,
        int resolution, Vector2 origin, Vector2 size, Vector2 from, Vector2 to,
        float radius, float strength, float hardness, int channel)
    {
        if (radius <= 0f || strength <= 0f || size.x <= 0f || size.y <= 0f)
            return false;

        float pixelX = size.x / resolution;
        float pixelZ = size.y / resolution;
        int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.x, to.x) - radius - origin.x) / pixelX));
        int maxX = Mathf.Min(resolution - 1, Mathf.FloorToInt((Mathf.Max(from.x, to.x) + radius - origin.x) / pixelX));
        int minZ = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.y, to.y) - radius - origin.y) / pixelZ));
        int maxZ = Mathf.Min(resolution - 1, Mathf.FloorToInt((Mathf.Max(from.y, to.y) + radius - origin.y) / pixelZ));
        Vector2 segment = to - from;
        float lengthSquared = segment.sqrMagnitude;
        Color target = ChannelColor(channel);
        bool changed = false;
        hardness = Mathf.Clamp01(hardness);
        strength = Mathf.Clamp01(strength);

        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
        {
            Vector2 point = origin + new Vector2((x + 0.5f) * pixelX, (z + 0.5f) * pixelZ);
            float t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - from, segment) / lengthSquared) : 0f;
            float distance = Vector2.Distance(point, from + segment * t) / radius;
            if (distance >= 1f)
                continue;

            float falloff = distance <= hardness ? 1f :
                1f - Mathf.SmoothStep(0f, 1f, (distance - hardness) / (1f - hardness));
            int index = z * resolution + x;
            float amount = falloff * strength;
            if (amount <= coverage[index])
                continue;

            // 한 번의 드래그에서는 최대 적용량만 사용해 이벤트 빈도에 따라 진해지는 것을 막는다.
            coverage[index] = amount;
            Color original = strokeStart[index];
            float sum = original.r + original.g + original.b;
            original = sum > 0.00001f ? new Color(original.r / sum, original.g / sum, original.b / sum, 1f) : Color.red;
            Color32 result = Color.Lerp(original, target, amount);
            Color32 current = pixels[index];
            if (current.r == result.r && current.g == result.g && current.b == result.b && current.a == result.a)
                continue;
            pixels[index] = result;
            changed = true;
        }
        return changed;
    }
}
