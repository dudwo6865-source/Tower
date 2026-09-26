using System;
using UnityEngine;

[Serializable]
public struct CombatEffectPrefabs
{
    [Label("총구 화염 프리팹")]
    [Tooltip("공격 시 발사 위치에 재생할 머즐 플래시 프리팹입니다.")]
    public GameObject muzzleFlashPrefab;

    [Label("피격 이펙트 프리팹")]
    [Tooltip("피격·근접 타격 시 대상 위치에 재생할 히트 이펙트 프리팹입니다.")]
    public GameObject hitEffectPrefab;

    [Label("투사체 프리팹")]
    [Tooltip("원거리 공격 투사체 프리팹입니다. Projectile 컴포넌트가 없으면 자동으로 추가됩니다.")]
    public GameObject projectilePrefab;

    [Label("사망 이펙트 프리팹")]
    [Tooltip("사망·파괴 시 재생할 이펙트 프리팹입니다.")]
    public GameObject deathEffectPrefab;

    public bool HasAnyPrefab =>
        muzzleFlashPrefab != null ||
        hitEffectPrefab != null ||
        projectilePrefab != null ||
        deathEffectPrefab != null;
}
