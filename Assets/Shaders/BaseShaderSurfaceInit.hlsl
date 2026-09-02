#ifndef BASE_SHADER_SURFACE_INIT_INCLUDED
#define BASE_SHADER_SURFACE_INIT_INCLUDED

#include "BaseShaderPackedMask.hlsl"
#include "BaseShaderAlbedoRecolor.hlsl"

void BaseShader_InitializeSurfaceURP(float2 uv, out SurfaceData outSurfaceData)
{
    InitializeStandardLitSurfaceData(uv, outSurfaceData);
}

void BaseShader_InitializeSurface(float2 uv, out SurfaceData outSurfaceData)
{
    BaseShader_InitializeSurfaceURP(uv, outSurfaceData);
    BaseShader_ApplyPackedMask(uv, outSurfaceData);
    BaseShader_ApplyAlbedoRecolor(outSurfaceData);
}

#define InitializeStandardLitSurfaceData(uv, surfaceData) BaseShader_InitializeSurface(uv, surfaceData)

#endif
