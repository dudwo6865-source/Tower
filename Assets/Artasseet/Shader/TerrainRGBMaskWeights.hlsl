#ifndef TANK_TERRAIN_RGB_MASK_WEIGHTS_INCLUDED
#define TANK_TERRAIN_RGB_MASK_WEIGHTS_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

// 평지와 절벽이 같은 좌표, 검정 대체값, 정규화 규칙을 사용한다.
float3 TerrainRGBMaskWeights(UnityTexture2D Mask, bool MaskUseUV0,
    float2 UV0, float3 PositionWS, float2 MaskOrigin, float2 MaskSize)
{
    float2 uv = MaskUseUV0 ? UV0 :
        (PositionWS.xz - MaskOrigin) / max(abs(MaskSize), float2(0.001, 0.001));
    float3 weights = saturate(SAMPLE_TEXTURE2D(Mask.tex, sampler_LinearClamp, uv).rgb);
    float total = dot(weights, float3(1.0, 1.0, 1.0));
    return total > 0.00001 ? weights / max(total, 0.00001) : float3(1.0, 0.0, 0.0);
}
#endif
