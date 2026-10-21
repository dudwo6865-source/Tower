#ifndef BASE_SHADER_HIT_FLASH_INCLUDED
#define BASE_SHADER_HIT_FLASH_INCLUDED

// 피격 플래시(HitFlash.cs) 전용 발광색. 이미션 맵·_EMISSION 키워드와 무관하게
// 표면 전체에 더해지므로, 이미션 맵(생산 표시 등)과 서로 덮어쓰지 않는다.
half4 _HitFlashColor;

void BaseShader_ApplyHitFlash(inout SurfaceData surfaceData)
{
    surfaceData.emission += _HitFlashColor.rgb;
}

#endif
