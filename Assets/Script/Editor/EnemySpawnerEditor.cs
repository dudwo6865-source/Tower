using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemySpawner))]
[CanEditMultipleObjects]
public class EnemySpawnerEditor : Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            using (new EditorGUI.DisabledScope(property.name == "m_Script"))
            {
                if (property.name == "enemyPrefabs" || property.name == "guardPrefabs")
                    EditorGUILayout.PropertyField(property, new GUIContent(
                        property.name == "enemyPrefabs" ? "적 프리팹 목록" : "수비대 프리팹 목록", property.tooltip), true);
                else
                    EditorGUILayout.PropertyField(property, true);
            }
        }
        serializedObject.ApplyModifiedProperties();
        EnemySpawner spawner = (EnemySpawner)target;
        EditorGUILayout.HelpBox("밤 시작·밤 길이의 1/3·2/3에 공격대를 세 번 나눠 출격합니다. 나머지는 앞 출격부터 배분합니다. " +
            "수비대는 첫 배치 및 매일 아침에 부족분만 보충하며 밤에는 진군하지 않습니다.", MessageType.Info);
        if (spawner.enemyPrefabs.Count == 0 && spawner.guardPrefabs.Count == 0)
            EditorGUILayout.HelpBox("수비대 프리팹 또는 적 프리팹을 지정하세요. 웨이브 적 구성은 공격대에만 사용합니다.", MessageType.Warning);
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("밤 공격대 적용 현황", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("웨이브", spawner.AppliedWaveNumber.ToString());
            EditorGUILayout.LabelField("밤 기본 총량", spawner.WaveCompositionTotal.ToString());
            EditorGUILayout.LabelField("출격 차수", $"{spawner.CompletedBatches}/3");
            EditorGUILayout.LabelField("다음 출격까지", $"{spawner.SpawnCountdown:0.0}초");
            EditorGUILayout.LabelField("공격대 생존 / 생성 예약", $"{spawner.AliveCount} / {spawner.PendingSpawnCount}");
            EditorGUILayout.LabelField("상한으로 대기하는 물량", spawner.WaitingAttackCount.ToString());
            EditorGUILayout.LabelField("공격대 생존 상한", spawner.EffectiveMaxAliveEnemies == 0 ? "무제한" : spawner.EffectiveMaxAliveEnemies.ToString());
            EditorGUILayout.LabelField("수비대 생존 / 정원", $"{spawner.GuardAliveCount} / {spawner.guardCapacity}");
            EditorGUILayout.LabelField("수비대 보충 예약", spawner.PendingGuardCount.ToString());
            if (EnemyMutationManager.Instance != null)
                EditorGUILayout.HelpBox(EnemyMutationManager.Instance.Summary, MessageType.None);
        }
        else
        {
            EditorGUILayout.LabelField("밤 공격대 미리보기", EditorStyles.boldLabel);
            WaveManager waveManager = Object.FindObjectOfType<WaveManager>();
            for (int wave = 1; wave <= 5; wave++)
            {
                WaveTuning tuning = waveManager != null ? waveManager.BuildTuningForWave(wave, true) : new WaveTuning();
                int total = spawner.spawnPeriodically && wave >= spawner.activateFromWave ? EnemySpawner.GetNightTotal(spawner.enemiesPerSpawn, tuning) : 0;
                EditorGUILayout.LabelField($"웨이브 {wave}", $"총 {total} = {EnemySpawner.GetBatchSize(total, 0)} / {EnemySpawner.GetBatchSize(total, 1)} / {EnemySpawner.GetBatchSize(total, 2)}마리");
            }
            EditorGUILayout.HelpBox("씬 WaveManager 기준 미리보기입니다. MapLoader가 있는 경우 스테이지 에디터의 실제 스테이지 설정을 확인하세요. 돌연변이 추가 물량은 포함하지 않습니다.", MessageType.None);
        }
    }
}
