#ifndef BASE_SHADER_PACKED_MASK_INCLUDED
#define BASE_SHADER_PACKED_MASK_INCLUDED

// Mask Pack Mode
//   (default) Metallic (R): 섭페 메탈릭 맵. 흰색=금속=반사 강함. Smoothness는 슬라이더.
//   _MASK_PACK_MR:  R=Metallic, G=Roughness(흰색=거침=반사 약함)
//   _MASK_PACK_ORM: G=Roughness, B=Metallic  (섭페/glTF 팩. R AO는 무시)
void BaseShader_ApplyPackedMask(float2 uv, inout SurfaceData surfaceData)
{
#if defined(_METALLICSPECGLOSSMAP)
    half4 packed = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);

#if defined(_MASK_PACK_ORM)
    surfaceData.metallic = packed.b;
    surfaceData.smoothness = (1.0h - saturate(packed.g)) * _Smoothness;

#elif defined(_MASK_PACK_MR)
    surfaceData.metallic = packed.r;
    surfaceData.smoothness = (1.0h - saturate(packed.g)) * _Smoothness;

#else
    // Metallic 맵만: 흰색=금속. Smoothness는 URP가 슬라이더(×A)로 이미 넣음.
    surfaceData.metallic = packed.r;
#endif
#endif
}

#endif
