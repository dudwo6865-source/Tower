#ifndef TANK_PLANAR_TERRAIN_RGB_INCLUDED
#define TANK_PLANAR_TERRAIN_RGB_INCLUDED

#include "TerrainRGBMaskWeights.hlsl"

// 평지의 월드 XZ 투영에 사용한다. 수직 절벽에는 사용하지 않는다.
float3 TankPlanarNormal(float4 packedNormal, float strength)
{
    float3 n = UnpackNormal(packedNormal);
    n.xy *= max(strength, 0.0);
    return normalize(float3(n.xy, max(n.z, 0.001)));
}

void PlanarTerrainRGB_float(
    float3 PositionWS, float3 SurfaceNormalWS, float2 UV0,
    UnityTexture2D Mask, bool MaskUseUV0, float2 MaskOrigin, float2 MaskSize,
    UnityTexture2D SoilAlbedo, UnityTexture2D SoilNormal,
    float SoilTiling, float SoilStrength, float SoilSmoothness,
    UnityTexture2D SandAlbedo, UnityTexture2D SandNormal,
    float SandTiling, float SandStrength, float SandSmoothness,
    UnityTexture2D SedimentAlbedo, UnityTexture2D SedimentNormal,
    float SedimentTiling, float SedimentStrength, float SedimentSmoothness,
    out float3 BaseColor, out float3 NormalWS, out float Smoothness)
{
    float3 weights = TerrainRGBMaskWeights(Mask, MaskUseUV0, UV0, PositionWS, MaskOrigin, MaskSize);

    // 색상과 노멀은 같은 좌표와 반복 수치를 공유한다.
    float2 soilUV = PositionWS.xz * max(abs(SoilTiling), 0.0001);
    float2 sandUV = PositionWS.xz * max(abs(SandTiling), 0.0001);
    float2 sedimentUV = PositionWS.xz * max(abs(SedimentTiling), 0.0001);
    float3 soilColor = SAMPLE_TEXTURE2D(SoilAlbedo.tex, sampler_LinearRepeat, soilUV).rgb;
    float3 sandColor = SAMPLE_TEXTURE2D(SandAlbedo.tex, sampler_LinearRepeat, sandUV).rgb;
    float3 sedimentColor = SAMPLE_TEXTURE2D(SedimentAlbedo.tex, sampler_LinearRepeat, sedimentUV).rgb;
    BaseColor = soilColor * weights.r + sandColor * weights.g + sedimentColor * weights.b;

    float3 soilN = TankPlanarNormal(SAMPLE_TEXTURE2D(SoilNormal.tex, sampler_LinearRepeat, soilUV), SoilStrength);
    float3 sandN = TankPlanarNormal(SAMPLE_TEXTURE2D(SandNormal.tex, sampler_LinearRepeat, sandUV), SandStrength);
    float3 sedimentN = TankPlanarNormal(SAMPLE_TEXTURE2D(SedimentNormal.tex, sampler_LinearRepeat, sedimentUV), SedimentStrength);
    float3 detail = normalize(soilN * weights.r + sandN * weights.g + sedimentN * weights.b);

    // 월드 투영 노멀을 메시 탄젠트로 해석하면 회전된 지형 조각의 조명이 틀어진다.
    // 요철 방향을 실제 표면의 접평면으로 투영하여 월드 노멀로 출력한다.
    // 강도가 0이거나 평평한 노멀맵이면 메시의 원래 노멀을 그대로 보존한다.
    float3 surface = normalize(SurfaceNormalWS);
    float3 perturbation = float3(detail.x, 0.0, detail.y) / max(detail.z, 0.001);
    perturbation -= surface * dot(surface, perturbation);
    NormalWS = normalize(surface + perturbation);
    Smoothness = saturate(dot(weights, float3(SoilSmoothness, SandSmoothness, SedimentSmoothness)));
}

#endif
