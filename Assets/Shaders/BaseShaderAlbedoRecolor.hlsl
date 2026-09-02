#ifndef BASE_SHADER_ALBEDO_RECOLOR_INCLUDED
#define BASE_SHADER_ALBEDO_RECOLOR_INCLUDED

// 알베도맵에서 특정 색상(기본값: 파란색) 영역을 다른 색으로 치환한다.
// Hue만 교체하고 원본의 채도/명도는 유지해서 텍스처의 음영/하이라이트를 보존한다.

half4 _AlbedoKeyColor;
half4 _AlbedoRecolorColor;
half _AlbedoRecolorRange;
half _AlbedoRecolorSmoothness;

half3 BaseShader_RGBtoHSV(half3 c)
{
    half4 K = half4(0.0h, -1.0h / 3.0h, 2.0h / 3.0h, -1.0h);
    half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
    half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
    half d = q.x - min(q.w, q.y);
    half e = 1.0e-6h;
    return half3(abs(q.z + (q.w - q.y) / (6.0h * d + e)), d / (q.x + e), q.x);
}

half3 BaseShader_HSVtoRGB(half3 c)
{
    half4 K = half4(1.0h, 2.0h / 3.0h, 1.0h / 3.0h, 3.0h);
    half3 p = abs(frac(c.xxx + K.xyz) * 6.0h - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

void BaseShader_ApplyAlbedoRecolor(inout SurfaceData surfaceData)
{
#if defined(_ALBEDO_RECOLOR)
    half3 srcHSV = BaseShader_RGBtoHSV(surfaceData.albedo);
    half3 keyHSV = BaseShader_RGBtoHSV(_AlbedoKeyColor.rgb);

    half hueDelta = abs(srcHSV.x - keyHSV.x);
    hueDelta = min(hueDelta, 1.0h - hueDelta); // 색상환에서의 최단 거리

    half mask = 1.0h - smoothstep(_AlbedoRecolorRange, _AlbedoRecolorRange + _AlbedoRecolorSmoothness, hueDelta);
    mask *= saturate(srcHSV.y * 4.0h); // 채도가 거의 없는(회색/흰색) 픽셀은 대상에서 제외

    half targetHue = BaseShader_RGBtoHSV(_AlbedoRecolorColor.rgb).x;
    half3 recolored = BaseShader_HSVtoRGB(half3(targetHue, srcHSV.y, srcHSV.z));

    surfaceData.albedo = lerp(surfaceData.albedo, recolored, mask);
#endif
}

#endif
