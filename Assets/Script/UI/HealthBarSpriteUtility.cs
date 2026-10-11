using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class HealthBarSpriteUtility
{
    private static Sprite whiteSprite;
    private static readonly Dictionary<int, Material> overlayMaterials =
        new Dictionary<int, Material>();

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

    // 유닛·건물 셰이더(BaseShader)가 그린 픽셀에 남기는 스텐실 비트와 같은 값입니다.
    public const int DefaultUnitStencilBit = 128;

    // 깊이 판정은 끄고(지형·언덕에 묻히지 않게) 대신 스텐실로 유닛 모델을 피한다.
    // unitStencilBit가 0 이하이면 스텐실을 보지 않고 모든 모델 위에 그린다.
    // 값마다 머티리얼을 하나씩만 만들어 모든 체력바가 공유한다.
    public static Material GetOverlayMaterial(int unitStencilBit)
    {
        if (overlayMaterials.TryGetValue(unitStencilBit, out Material cached) &&
            cached != null)
            return cached;

        Shader shader = Shader.Find("UI/Default");

        if (shader == null)
            return null;

        Material material = new Material(shader);
        // 지형·언덕 등에 가려지지 않고 항상 위에 표시되도록 깊이 판정을 무시합니다.
        material.SetInt(
            "unity_GUIZTestMode",
            (int)CompareFunction.Always);

        // 모델이 이 비트를 남긴 픽셀에는 그리지 않는다.
        if (unitStencilBit > 0)
        {
            material.SetInt("_Stencil", unitStencilBit);
            material.SetInt("_StencilReadMask", unitStencilBit);
            material.SetInt("_StencilWriteMask", 0);
            material.SetInt("_StencilComp", (int)CompareFunction.NotEqual);
            material.SetInt("_StencilOp", (int)StencilOp.Keep);
        }

        overlayMaterials[unitStencilBit] = material;
        return material;
    }
}
