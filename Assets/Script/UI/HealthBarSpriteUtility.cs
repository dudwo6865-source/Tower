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
        overlayMaterial.SetInt(
            "unity_GUIZTestMode",
            (int)CompareFunction.Always);

        return overlayMaterial;
    }
}
