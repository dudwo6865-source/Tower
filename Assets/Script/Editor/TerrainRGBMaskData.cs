using UnityEngine;
using System.Collections.Generic;

// 픽셀 원본을 보관하여 PNG를 다시 읽지 않고 실행 취소와 편집 재개를 지원한다.
[PreferBinarySerialization]
public sealed class TerrainRGBMaskData : ScriptableObject
{
    [HideInInspector] public int resolution = 1024;
    [HideInInspector] public Vector2 origin;
    [HideInInspector] public Vector2 size = new Vector2(100f, 100f);
    [HideInInspector] public Material material;
    [HideInInspector] public List<Material> additionalMaterials = new List<Material>();
    [HideInInspector] public Texture2D exportedMask;
    [HideInInspector] public Color32[] pixels;

    public bool IsValid => resolution > 0 && resolution <= 2048 &&
        size.x > 0f && size.y > 0f && pixels != null &&
        pixels.Length == resolution * resolution;

    public void Initialize(int newResolution, Vector2 newOrigin, Vector2 newSize, Material target)
    {
        resolution = newResolution;
        origin = newOrigin;
        size = newSize;
        material = target;
        pixels = new Color32[resolution * resolution];
        Fill(0);
    }

    public void Fill(int channel)
    {
        Color32 color = TerrainRGBMaskBrushMath.ChannelColor(channel);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
    }

    // 기존 편집 원본의 단일 머티리얼 필드는 유지한다.
    public List<Material> GetMaterials()
    {
        var targets = new List<Material>();
        if (material != null) targets.Add(material);
        if (additionalMaterials != null)
            foreach (var target in additionalMaterials)
                if (target != null && !targets.Contains(target)) targets.Add(target);
        return targets;
    }
}
