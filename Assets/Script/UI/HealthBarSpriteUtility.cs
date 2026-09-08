using UnityEngine;
using UnityEngine.Rendering;

public static class HealthBarSpriteUtility
{
    private static Sprite whiteSprite;
    private static Material overlayMaterial;

    public static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            100f);

        return whiteSprite;
    }

    public static Material GetOverlayMaterial()
    {
        if (overlayMaterial != null)
            return overlayMaterial;

        Shader shader = Shader.Find("UI/Default");

        if (shader == null)
            return null;

        overlayMaterial = new Material(shader);
        // 지형·언덕 등에 가려지지 않고 항상 위에 표시되도록 깊이 판정을 무시합니다.
        overlayMaterial.SetInt(
            "unity_GUIZTestMode",
            (int)CompareFunction.Always);

        return overlayMaterial;
    }
}
