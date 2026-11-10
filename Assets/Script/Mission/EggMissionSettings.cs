using System;
using System.Collections.Generic;
using UnityEngine;

// 파괴한 알 수 구간 하나: 이 수 이상 파괴했으면 한 번에 나오는 알 수를 이 값으로 바꾼다.
[Serializable]
public class EggSpawnCountTier
{
    public const int MinDestroyedAtLeast = 0;
    public const int MinEggsPerSpawn = 1;

    [Label("파괴 수 기준(이상)")]
    [Tooltip("지금까지 파괴한 알이 이 수 이상이면 이 구간을 씁니다.")]
    [Min(MinDestroyedAtLeast)] public int destroyedAtLeast = MinDestroyedAtLeast;
    [Label("한 번에 나오는 수")]
    [Tooltip("이 구간에서 출현 차례마다 놓는 알 수입니다.")]
    [Min(MinEggsPerSpawn)] public int eggsPerSpawn = MinEggsPerSpawn;

    public EggSpawnCountTier Clone() => (EggSpawnCountTier)MemberwiseClone();
}

// 알 파괴 미션 설정: 시간 경과에 따라 맵에 무작위로 알을 놓고, 정해진 수를 파괴하면 승리한다.
[Serializable]
public class EggMissionSettings
{
    [Header("승리 조건")]
    [Label("목표 파괴 수")]
    [Tooltip("이 수만큼 알을 파괴하면 승리합니다. 맵에 나온 알 수와는 상관없습니다. 부화한 알은 세지 않습니다.")]
    [Min(1)] public int targetDestroyCount = 5;

    [Header("부화")]
    [Label("알 부화 시간(초)")]
    [Tooltip("알마다 따로 흐르는 부화 시간입니다. 알이 맵에 나온 순간부터 세며, 시간이 다 되면 그 알만 부화합니다.")]
    [Min(0.01f)] public float hatchSeconds = 60f;

    [Header("알 출현")]
    [Label("알 프리팹")]
    [Tooltip("MonsterEgg가 붙은 알 프리팹입니다. 비워두면 맵에 미리 놓은 알만 사용합니다.")]
    public MonsterEgg eggPrefab;
    [Label("첫 출현 대기(초)")]
    [Tooltip("게임 시작(인트로 종료) 후 첫 알이 나오기까지의 시간입니다.")]
    [Min(0f)] public float firstSpawnDelay = 10f;
    [Label("출현 간격(초)")]
    [Tooltip("첫 출현 이후 이 간격마다 알이 나옵니다.")]
    [Min(0.1f)] public float spawnInterval = 30f;
    [Label("한 번에 나오는 수")]
    [Tooltip("출현 차례마다 놓는 알 수입니다. '파괴 수별 출현 수'가 비어 있거나 맞는 구간이 없을 때 이 값을 씁니다.")]
    [Min(EggSpawnCountTier.MinEggsPerSpawn)] public int eggsPerSpawn = EggSpawnCountTier.MinEggsPerSpawn;
    [Label("파괴 수별 출현 수")]
    [Tooltip("지금까지 파괴한 알 수에 따라 한 번에 나오는 알 수를 바꿉니다. 파괴 수 이하인 구간 중 기준이 가장 큰 구간을 씁니다(순서는 상관없음). 비워두면 '한 번에 나오는 수'를 그대로 씁니다.")]
    public List<EggSpawnCountTier> eggsPerSpawnByDestroyed = new List<EggSpawnCountTier>();
    [Label("동시 최대 알 수")]
    [Tooltip("맵에 남아 있는(부화 대기 중인) 알이 이 수 이상이면 새 알을 놓지 않고 다음 간격을 기다립니다. 0이면 제한하지 않습니다.")]
    [Min(0)] public int maxActiveEggs = 6;
    [Label("총 출현 한도")]
    [Tooltip("미션 동안 새로 놓을 알의 최대 수입니다. 0이면 제한하지 않습니다. 미리 놓은 알과 합쳐 목표 파괴 수 이상이어야 합니다.")]
    [Min(0)] public int maxTotalSpawns;

    [Header("배치 위치")]
    [Label("본부와 최소 거리")]
    [Min(0f)] public float minDistanceFromHq = 25f;
    [Label("알 사이 최소 거리")]
    [Min(0f)] public float minDistanceBetweenEggs = 10f;
    [Label("건물과 최소 거리")]
    [Tooltip("모든 건물(아군·적)의 중심에서 이 거리 안에는 놓지 않습니다.")]
    [Min(0f)] public float minDistanceFromBuildings = 6f;
    [Label("맵 가장자리 여백")]
    [Min(0f)] public float mapEdgeMargin = 6f;
    [Label("플레이어 시야 밖 우선")]
    [Tooltip("켜면 지금 보이지 않는 곳을 먼저 고릅니다. 마땅한 자리가 없으면 시야 안에도 놓습니다.")]
    public bool preferOutsideVision = true;
    [Label("위치 탐색 시도 수")]
    [Min(1)] public int placementAttempts = 40;

    // 구간 목록은 참조형이라 따로 복사한다(원본 에셋과 목록을 공유하지 않게).
    public EggMissionSettings Clone()
    {
        EggMissionSettings clone = (EggMissionSettings)MemberwiseClone();
        clone.eggsPerSpawnByDestroyed = new List<EggSpawnCountTier>();
        if (eggsPerSpawnByDestroyed != null)
            foreach (EggSpawnCountTier tier in eggsPerSpawnByDestroyed)
                if (tier != null) clone.eggsPerSpawnByDestroyed.Add(tier.Clone());
        return clone;
    }

    // 지금까지 파괴한 알 수에 맞는 '한 번에 나오는 수'를 돌려준다.
    // 정렬이 어긋나 있어도 파괴 수 이하인 구간 중 기준이 가장 큰 것을 고르고, 맞는 구간이 없으면 기본값을 쓴다.
    public int GetEggsPerSpawn(int destroyedEggs)
    {
        EggSpawnCountTier best = null;
        if (eggsPerSpawnByDestroyed != null)
            foreach (EggSpawnCountTier tier in eggsPerSpawnByDestroyed)
                if (tier != null && tier.destroyedAtLeast <= destroyedEggs &&
                    (best == null || tier.destroyedAtLeast >= best.destroyedAtLeast))
                    best = tier;
        return best != null ? best.eggsPerSpawn : eggsPerSpawn;
    }

    // 미리 놓은 알 수를 함께 받아 목표를 채울 수 있는 설정인지 검사한다.
    public string Validate(int preplacedEggs)
    {
        if (targetDestroyCount < 1) return "목표 파괴 수는 1 이상이어야 합니다.";
        if (!Positive(hatchSeconds)) return "알 부화 시간은 0보다 큰 유한한 값이어야 합니다.";
        if (!Finite(firstSpawnDelay) || firstSpawnDelay < 0f || !Positive(spawnInterval))
            return "첫 출현 대기는 0 이상, 출현 간격은 0보다 커야 합니다.";
        if (eggsPerSpawn < 1 || maxActiveEggs < 0 || maxTotalSpawns < 0 || placementAttempts < 1)
            return "출현 수·최대 알 수·시도 수 설정을 확인하세요.";
        if (eggsPerSpawnByDestroyed != null)
            foreach (EggSpawnCountTier tier in eggsPerSpawnByDestroyed)
                if (tier == null || tier.destroyedAtLeast < EggSpawnCountTier.MinDestroyedAtLeast ||
                    tier.eggsPerSpawn < EggSpawnCountTier.MinEggsPerSpawn)
                    return "파괴 수별 출현 수: 파괴 수 기준은 0 이상, 한 번에 나오는 수는 1 이상이어야 합니다.";
        if (eggPrefab == null)
            return preplacedEggs >= targetDestroyCount ? null
                : "알 프리팹을 연결하세요. 미리 놓은 알만으로는 목표 파괴 수를 채울 수 없습니다.";
        if (maxTotalSpawns > 0 && maxTotalSpawns + preplacedEggs < targetDestroyCount)
            return "총 출현 한도와 미리 놓은 알의 합이 목표 파괴 수보다 적습니다.";
        string error = eggPrefab.ValidateSetup();
        return error != null ? "알 프리팹: " + error : null;
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Positive(float value) => value > 0f && Finite(value);
}
