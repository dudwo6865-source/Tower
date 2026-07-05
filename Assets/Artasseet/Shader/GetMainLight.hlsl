#ifndef TOON_GET_MAIN_LIGHT_INCLUDED
#define TOON_GET_MAIN_LIGHT_INCLUDED

#include "Lighting.cginc"

void GetMainLight_float(float3 In, out float3 Direction, out float3 Color)
{
#if defined(SHADERGRAPH_PREVIEW)
    Direction = normalize(float3(-0.5, 1.0, -0.3));
    Color = float3(1.0, 1.0, 1.0);
#elif defined(UNITY_PASS_FORWARDBASE)
    if (_WorldSpaceLightPos0.w == 0.0)
        Direction = normalize(_WorldSpaceLightPos0.xyz);
    else
        Direction = normalize(_WorldSpaceLightPos0.xyz - In);

    Color = _LightColor0.rgb;
#else
    Direction = normalize(float3(-0.5, 1.0, -0.3));
    Color = float3(0.0, 0.0, 0.0);
#endif
}

#endif
