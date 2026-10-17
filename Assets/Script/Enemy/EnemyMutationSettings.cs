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
    [Label("방어력 증가량")]
    [Tooltip("방어력 돌연변이 한 번에 더할 방어력입니다(비율이 아니라 덧셈). 1이면 적이 맞을 때마다 피해를 1씩 덜 받습니다.")]
    [Min(0f)] public float armorIncrease = 1f;
    [Label("공격력 추첨 가중치")]
    [Min(0f)] public float damageWeight = 1f;
    [Label("체력 추첨 가중치")]
    [Min(0f)] public float healthWeight = 1f;
    [Label("이동 속도 추첨 가중치")]
    [Min(0f)] public float speedWeight = 1f;
    [Label("방어력 추첨 가중치")]
    [Tooltip("0이면 방어력 돌연변이가 나오지 않습니다. 기존 스테이지의 확률이 바뀌지 않도록 기본값은 0입니다.")]
    [Min(0f)] public float armorWeight = 0f;

    public EnemyMutationSettings Clone() => (EnemyMutationSettings)MemberwiseClone();
}

public enum EnemyMutationType
{
    [InspectorName("스폰 수 증가")] Spawn,
    [InspectorName("공격력 증가")] Damage,
    [InspectorName("체력 증가")] Health,
    [InspectorName("이동 속도 증가")] Speed,
    [InspectorName("방어력 증가")] Armor
}
