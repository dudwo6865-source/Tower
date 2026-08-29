#ifndef BASE_SHADER_SHADOW_DEPTH_INCLUDED
#define BASE_SHADER_SHADOW_DEPTH_INCLUDED

// ShadowCaster / DepthOnly 용 Dissolve clip.
// LitInput(CommonMaterial/LerpWhiteTo)을 Shadows보다 먼저 포함해야 한다.

#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif
#include "BaseShaderDissolve.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct BaseShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct BaseShadowVaryings
{
    float2 uv : TEXCOORD0;
    float3 positionOS : TEXCOORD1;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 BaseGetShadowPositionHClip(BaseShadowAttributes input)
{
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    return positionCS;
}

BaseShadowVaryings BaseShadowPassVertex(BaseShadowAttributes input)
{
    BaseShadowVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionOS = input.positionOS.xyz;
    output.positionCS = BaseGetShadowPositionHClip(input);
    return output;
}

half4 BaseShadowPassFragment(BaseShadowVaryings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

#if defined(_ALPHATEST_ON)
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
#endif

    BaseShader_ClipDissolve(input.positionOS, input.uv);

#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    return 0;
}

struct BaseDepthAttributes
{
    float4 position : POSITION;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct BaseDepthVaryings
{
    float2 uv : TEXCOORD0;
    float3 positionOS : TEXCOORD1;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

BaseDepthVaryings BaseDepthOnlyVertex(BaseDepthAttributes input)
{
    BaseDepthVaryings output = (BaseDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionOS = input.position.xyz;
    output.positionCS = TransformObjectToHClip(input.position.xyz);
    return output;
}

half BaseDepthOnlyFragment(BaseDepthVaryings input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_ALPHATEST_ON)
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
#endif

    BaseShader_ClipDissolve(input.positionOS, input.uv);

#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    return input.positionCS.z;
}

#endif
