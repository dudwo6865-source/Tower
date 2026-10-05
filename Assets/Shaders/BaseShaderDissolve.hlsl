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
half4 _DissolveCapColor;
half4 _DissolveCapEmission;

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

// 잘린 단면 채우기.
// 디졸브로 앞면이 잘리면 그 구멍으로 메시 안쪽 뒷면이 보인다. 그 뒷면을 단면 색으로 칠하면
// 스텐실이나 별도 뚜껑 메시 없이도 속이 꽉 찬 것처럼 보인다.
// 조명 계산 위치는 뒷면 대신, 시선이 절단 높이(_DissolveHeight) 평면과 만나는 점으로 옮긴다.
float3 BaseShader_DissolveCapPositionOS(float3 positionOS, half3 viewDirWS)
{
    // 직교 카메라에서도 맞도록 위치 차이가 아니라 시선 방향으로 평면 교차를 구한다.
    float3 viewDirOS = TransformWorldToObjectDir(viewDirWS, false);
    if (viewDirOS.y <= 1e-4)
        return positionOS;

    float distanceToCap = (_DissolveHeight - positionOS.y) / viewDirOS.y;
    return positionOS + viewDirOS * max(distanceToCap, 0.0);
}

void BaseShader_ApplyDissolveCapSurface(inout SurfaceData surfaceData)
{
    surfaceData.albedo = _DissolveCapColor.rgb;
    surfaceData.specular = half3(0.0, 0.0, 0.0);
    surfaceData.metallic = 0.0;
    surfaceData.smoothness = 0.0;
    surfaceData.normalTS = half3(0.0, 0.0, 1.0);
    surfaceData.occlusion = 1.0;
    surfaceData.emission = _DissolveCapEmission.rgb;
    surfaceData.clearCoatMask = 0.0;
    surfaceData.clearCoatSmoothness = 0.0;
    surfaceData.alpha = 1.0;
}

#endif
