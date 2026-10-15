using System;
using UnityEngine;

[Serializable]
public class EnemyMutationSettings
{
    [Label("돌연변이 사용")]
    public bool enabled = true;
    [Label("적 소속 ID")]
    public int enemyOwnerId = 2;
    [Label("스폰 수 계열 확률")]
    [Range(0f, 1f)] public float spawnChance = 0.5f;
    [Label("스폰 수 증가율")]
    [Min(0f)] public float spawnIncrease = 0.2f;
    [Label("공격력 증가율")]
    [Min(0f)] public float damageIncrease = 0.1f;
    [Label("체력 증가율")]
    [Min(0f)] public float healthIncrease = 0.15f;
    [Label("이동 속도 증가율")]
    [Min(0f)] public float speedIncrease = 0.05f;
    [Label("공격력 추첨 가중치")]
    [Min(0f)] public float damageWeight = 1f;
    [Label("체력 추첨 가중치")]
    [Min(0f)] public float healthWeight = 1f;
    [Label("이동 속도 추첨 가중치")]
    [Min(0f)] public float speedWeight = 1f;

    public EnemyMutationSettings Clone() => (EnemyMutationSettings)MemberwiseClone();
}

public enum EnemyMutationType
{
    [InspectorName("스폰 수 증가")] Spawn,
    [InspectorName("공격력 증가")] Damage,
    [InspectorName("체력 증가")] Health,
    [InspectorName("이동 속도 증가")] Speed
}
