#ifndef BASE_SHADER_LIT_PASS_INCLUDED
#define BASE_SHADER_LIT_PASS_INCLUDED

// URP LitForwardPass 를 쓰되, Surface 초기화 직후 Dissolve를 적용한다.
#if defined(_DISSOLVE_ON)
    #ifndef REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
        #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
    #endif
#endif

#define LitPassFragment LitPassFragment_URP
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#undef LitPassFragment

#include "BaseShaderPackedMask.hlsl"
#include "BaseShaderAlbedoRecolor.hlsl"
#include "BaseShaderDissolve.hlsl"

void LitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);
    BaseShader_ApplyPackedMask(input.uv, surfaceData);
    BaseShader_ApplyAlbedoRecolor(surfaceData);

#if defined(_DISSOLVE_ON)
    half3 dissolveViewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    float3 positionOS = TransformWorldToObject(input.positionWS);
    BaseShader_ApplyDissolve(positionOS, input.uv, input.normalWS, dissolveViewDirWS, surfaceData);
#endif

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv, _BaseMap);

#ifdef _DBUFFER
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif
}

#endif
