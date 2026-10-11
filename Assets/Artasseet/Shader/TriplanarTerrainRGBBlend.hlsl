#ifndef TANK_TRIPLANAR_TERRAIN_RGB_BLEND_INCLUDED
#define TANK_TRIPLANAR_TERRAIN_RGB_BLEND_INCLUDED
#include "TerrainRGBMaskWeights.hlsl"

void TriplanarTerrainRGBBlend_float(UnityTexture2D Mask, bool MaskUseUV0,
    float2 UV0, float3 PositionWS, float2 MaskOrigin, float2 MaskSize,
    float3 MainColor, float3 GrassColor, float3 HillColor,
    float3 MainNormal, float3 GrassNormal, float3 HillNormal,
    out float3 BaseColor, out float3 NormalTS)
{
    float3 weights = TerrainRGBMaskWeights(Mask, MaskUseUV0, UV0, PositionWS, MaskOrigin, MaskSize);
    BaseColor = MainColor * weights.r + GrassColor * weights.g + HillColor * weights.b;
    // 각 투영 노드에서 같은 탄젠트 공간으로 변환한 노멀을 혼합한다.
    float3 mixed = MainNormal * weights.r + GrassNormal * weights.g + HillNormal * weights.b;
    NormalTS = dot(mixed, mixed) > 0.000001 ? normalize(mixed) : float3(0.0, 0.0, 1.0);
}
#endif
