using System;
using System.Collections.Generic;
using UnityEngine;

// 생성 중에만 색을 캐시한다. 원본 텍스처의 읽기/쓰기 설정은 변경하지 않는다.
public sealed class MinimapTerrainColorSampler : IDisposable
{
    sealed class Surface
    {
        public Material[] materials;
        public Mesh mesh;
        public int[] triangleEnds;
    }

    sealed class Palette
    {
        public bool valid, rgb, uvMask;
        public Color main, second, third;
        public Texture2D mask;
        public Vector4 origin, size;
    }

    readonly int colorResolution, maskResolution;
    readonly Dictionary<Collider, Surface> surfaces = new Dictionary<Collider, Surface>();
    readonly Dictionary<Material, Palette> palettes = new Dictionary<Material, Palette>();
    readonly Dictionary<Texture, Color> averages = new Dictionary<Texture, Color>();
    readonly Dictionary<Texture, Texture2D> masks = new Dictionary<Texture, Texture2D>();

    public MinimapTerrainColorSampler(int colorResolution, int maskResolution)
    {
        this.colorResolution = Mathf.Clamp(colorResolution, 16, 256);
        this.maskResolution = Mathf.Clamp(maskResolution, 32, 2048);
    }

    public bool TrySample(RaycastHit hit, out Color color)
    {
        color = default;
        if (hit.collider == null) return false;
        if (!surfaces.TryGetValue(hit.collider, out Surface surface))
        {
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null) renderer = hit.collider.GetComponentInChildren<Renderer>();
            surface = new Surface { materials = renderer != null ? renderer.sharedMaterials : Array.Empty<Material>() };
            var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            if (hit.collider is MeshCollider collider && filter != null && collider.sharedMesh == filter.sharedMesh)
            {
                surface.mesh = collider.sharedMesh;
                if (surface.mesh != null)
                {
                    surface.triangleEnds = new int[surface.mesh.subMeshCount];
                    int triangles = 0;
                    for (int i = 0; i < surface.triangleEnds.Length; i++)
                    {
                        triangles += (int)surface.mesh.GetIndexCount(i) / 3;
                        surface.triangleEnds[i] = triangles;
                    }
                }
            }
            surfaces.Add(hit.collider, surface);
        }
        int slot = 0;
        if (surface.triangleEnds != null && hit.triangleIndex >= 0)
            while (slot < surface.triangleEnds.Length - 1 && hit.triangleIndex >= surface.triangleEnds[slot]) slot++;
        if (slot >= surface.materials.Length || surface.materials[slot] == null) return false;
        Material material = surface.materials[slot];
        if (!palettes.TryGetValue(material, out Palette palette))
        {
            palette = ReadPalette(material);
            palettes.Add(material, palette);
        }
        if (!palette.valid) return false;
        Color linear = palette.main;
        if (palette.rgb && palette.mask != null)
        {
            if (palette.uvMask && surface.mesh == null) return false;
            Vector2 uv = palette.uvMask ? hit.textureCoord : new Vector2(
                (hit.point.x - palette.origin.x) / Mathf.Max(Mathf.Abs(palette.size.x), .001f),
                (hit.point.z - palette.origin.y) / Mathf.Max(Mathf.Abs(palette.size.y), .001f));
            Color weights = palette.mask.GetPixelBilinear(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
            float sum = weights.r + weights.g + weights.b;
            if (sum > .00001f)
                linear = (palette.main * weights.r + palette.second * weights.g + palette.third * weights.b) / sum;
        }
        // 미니맵용 일반 색상 텍스처에 저장할 감마 색으로 되돌린다.
        color = QualitySettings.activeColorSpace == ColorSpace.Linear ? linear.gamma : linear;
        color.a = 1f;
        return true;
    }

    Palette ReadPalette(Material material)
    {
        var result = new Palette();
        bool planar = material.HasProperty("_SoilAlbedo") && material.HasProperty("_RGBMask");
        bool triplanar = material.HasProperty("_Main_Albedo") && material.HasProperty("_RGBMask");
        if (planar || triplanar)
        {
            result.rgb = true;
            result.valid = TryAverage(GetTexture(material, planar ? "_SoilAlbedo" : "_Main_Albedo"), out result.main);
            if (!TryAverage(GetTexture(material, planar ? "_SandAlbedo" : "_Grass_Albedo"), out result.second)) result.second = result.main;
            if (!TryAverage(GetTexture(material, planar ? "_SedimentAlbedo" : "_Hill_Albedo"), out result.third)) result.third = result.main;
            Texture mask = GetTexture(material, "_RGBMask");
            if (mask != null && !masks.TryGetValue(mask, out result.mask))
            {
                result.mask = ReadTexture(mask, maskResolution);
                masks.Add(mask, result.mask);
            }
            result.uvMask = material.HasProperty("_MaskUseUV0") && material.GetFloat("_MaskUseUV0") > .5f;
            result.origin = material.GetVector("_MaskOrigin");
            result.size = material.GetVector("_MaskSize");
            return result;
        }
        Texture texture = GetTexture(material, "_BaseMap") ?? GetTexture(material, "_MainTex") ?? GetTexture(material, "_Main_Albedo");
        result.valid = TryAverage(texture, out result.main);
        if (result.valid)
        {
            Color tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            result.main *= QualitySettings.activeColorSpace == ColorSpace.Linear ? tint.linear : tint;
        }
        return result;
    }

    static Texture GetTexture(Material material, string property) => material.HasProperty(property) ? material.GetTexture(property) : null;

    bool TryAverage(Texture source, out Color color)
    {
        color = default;
        if (source == null) return false;
        if (averages.TryGetValue(source, out color)) return true;
        var readable = ReadTexture(source, colorResolution);
        if (readable == null) return false;
        try
        {
            var pixels = readable.GetPixels();
            foreach (var pixel in pixels) color += pixel;
            color /= pixels.Length;
            averages.Add(source, color);
            return true;
        }
        finally { Release(readable); }
    }

    // GPU에서 축소 복사하므로 압축되거나 읽기 불가능한 텍스처도 처리한다.
    static Texture2D ReadTexture(Texture source, int resolution)
    {
        int width = Mathf.Min(source.width, resolution), height = Mathf.Min(source.height, resolution);
        var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        bool previousSRGB = GL.sRGBWrite;
        Texture2D readable = null;
        try
        {
            GL.sRGBWrite = false;
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp };
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply(false, false);
            return readable;
        }
        catch (UnityException)
        {
            Release(readable);
            return null;
        }
        finally
        {
            GL.sRGBWrite = previousSRGB;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    static void Release(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(target);
        else UnityEngine.Object.DestroyImmediate(target);
    }

    public void Dispose()
    {
        foreach (var mask in masks.Values) Release(mask);
        masks.Clear();
    }
}
