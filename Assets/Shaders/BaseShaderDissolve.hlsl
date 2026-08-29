#ifndef BASE_SHADER_DISSOLVE_INCLUDED
#define BASE_SHADER_DISSOLVE_INCLUDED

// Dissolve.shadergraph 포팅:
// Object-space Y 높이 + Simple Noise 로 디졸브, Edge 발광, Fresnel 오버레이.

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"

float _DissolveHeight;
float _DissolveEdge;
half4 _DissolveEdgeColor;
float _DissolveNoiseScale;
float _DissolveNoiseStrength;
half4 _DissolveFresnelColor;

float BaseShader_ValueNoise(float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);
    f = f * f * (3.0 - 2.0 * f);

    float r0, r1, r2, r3;
    Hash_Tchou_2_1_float(i + float2(0.0, 0.0), r0);
    Hash_Tchou_2_1_float(i + float2(1.0, 0.0), r1);
    Hash_Tchou_2_1_float(i + float2(0.0, 1.0), r2);
    Hash_Tchou_2_1_float(i + float2(1.0, 1.0), r3);

    float bottomOfGrid = lerp(r0, r1, f.x);
    float topOfGrid = lerp(r2, r3, f.x);
    return lerp(bottomOfGrid, topOfGrid, f.y);
}

float BaseShader_SimpleNoise(float2 uv, float scale)
{
    float result = 0.0;
    [unroll]
    for (int octave = 0; octave < 3; octave++)
    {
        float freq = pow(2.0, (float)octave);
        float amp = pow(0.5, (float)(3 - octave));
        result += BaseShader_ValueNoise(uv * (scale / freq)) * amp;
    }
    return result;
}

float BaseShader_DissolveThreshold(float2 uv)
{
    float n = BaseShader_SimpleNoise(uv, _DissolveNoiseScale);
    // Remap 0..1 -> [-Strength, Strength] then + Height
    float remapped = n * (2.0 * _DissolveNoiseStrength) - _DissolveNoiseStrength;
    return remapped + _DissolveHeight;
}

void BaseShader_EvalDissolve(float3 positionOS, float2 uv, out float keepAlpha, out float edgeMask)
{
    float heightY = positionOS.y;
    float threshold = BaseShader_DissolveThreshold(uv);
    keepAlpha = step(heightY, threshold);
    edgeMask = step(threshold, heightY + _DissolveEdge) * keepAlpha;
}

void BaseShader_ClipDissolve(float3 positionOS, float2 uv)
{
#if defined(_DISSOLVE_ON)
    float keepAlpha;
    float edgeMask;
    BaseShader_EvalDissolve(positionOS, uv, keepAlpha, edgeMask);
    clip(keepAlpha - 0.5);
#endif
}

void BaseShader_ApplyDissolve(
    float3 positionOS,
    float2 uv,
    half3 normalWS,
    half3 viewDirWS,
    inout SurfaceData surfaceData)
{
#if defined(_DISSOLVE_ON)
    float keepAlpha;
    float edgeMask;
    BaseShader_EvalDissolve(positionOS, uv, keepAlpha, edgeMask);
    clip(keepAlpha - 0.5);

    half ndotv = saturate(dot(normalize(normalWS), normalize(viewDirWS)));
    half fresnel = 1.0 - ndotv; // Power = 1 (Dissolve graph)
    surfaceData.albedo += fresnel * _DissolveFresnelColor.rgb;
    surfaceData.emission += edgeMask * _DissolveEdgeColor.rgb;
#endif
}

#endif
