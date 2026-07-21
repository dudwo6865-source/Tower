#ifndef TOON_GET_MAIN_LIGHT_INCLUDED
#define TOON_GET_MAIN_LIGHT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

void GetMainLight_float(float3 WorldPos, out float3 Direction, out float3 Color)
{
#if defined(SHADERGRAPH_PREVIEW)
    Direction = normalize(float3(-0.5, 1.0, -0.3));
    Color = float3(1.0, 1.0, 1.0);
#else
    float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
    Light mainLight = GetMainLight(shadowCoord);
    Direction = mainLight.direction;
    Color = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
#endif
}

#endif
