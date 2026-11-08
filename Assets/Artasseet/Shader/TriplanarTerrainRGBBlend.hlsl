#ifndef TANK_TRIPLANAR_TERRAIN_RGB_BLEND_INCLUDED
#define TANK_TRIPLANAR_TERRAIN_RGB_BLEND_INCLUDED
#include "TerrainRGBMaskWeights.hlsl"

// 메시 탄젠트 대신 표면 노멀을 기준으로 요철 강도를 조절한다.
float3 TerrainNormalStrengthWS(float3 mapped, float3 surface, float strength)
{
    float3 result = surface + (mapped - surface) * max(strength, 0.0);
    return dot(result, result) > 0.000001 ? normalize(result) : surface;
}

void TriplanarTerrainRGBBlend_float(UnityTexture2D Mask, bool MaskUseUV0,
    float2 UV0, float3 PositionWS, float2 MaskOrigin, float2 MaskSize,
    float3 MainColor, float3 GrassColor, float3 HillColor,
    float3 MainNormal, float3 GrassNormal, float3 HillNormal,
    float SoilSmoothness, float SandSmoothness, float SedimentSmoothness,
    float3 SurfaceNormalWS, float MainStrength, float GrassStrength, float HillStrength,
    out float3 BaseColor, out float3 NormalWS, out float Smoothness)
{
    float3 weights = TerrainRGBMaskWeights(Mask, MaskUseUV0, UV0, PositionWS, MaskOrigin, MaskSize);
    BaseColor = MainColor * weights.r + GrassColor * weights.g + HillColor * weights.b;
    // 월드 공간에서 끝까지 혼합하여 절벽 메시의 UV와 탄젠트에 의존하지 않는다.
    float3 surface = normalize(SurfaceNormalWS);
    float3 mixed = TerrainNormalStrengthWS(MainNormal, surface, MainStrength) * weights.r
        + TerrainNormalStrengthWS(GrassNormal, surface, GrassStrength) * weights.g
        + TerrainNormalStrengthWS(HillNormal, surface, HillStrength) * weights.b;
    NormalWS = dot(mixed, mixed) > 0.000001 ? normalize(mixed) : surface;
    // 평지와 동일하게 재질별 매끄러움을 RGB 마스크 비율로 혼합한다.
    Smoothness = saturate(dot(weights, float3(SoilSmoothness, SandSmoothness, SedimentSmoothness)));
}
#endif
