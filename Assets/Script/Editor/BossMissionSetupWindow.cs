using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

// 기존 에셋을 덮어쓰지 않고 보스 프리팹과 네 구역의 테스트 맵을 만든다.
public class BossMissionSetupWindow : EditorWindow
{
    MapConfig sourceStage;
    GameObject sourceEnemy;
    GameObject headquarters;
    string missionName = "보스 사냥";
    float bossHealth = 2000f;
    float bossDamage = 30f;
    float bossSpeed = 2f;
    float zoneWidth = 36f;
    float zoneSpacing = 25f;

    [MenuItem("Tools/맵/보스 미션 만들기")]
    static void Open() => GetWindow<BossMissionSetupWindow>("보스 미션 만들기");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("새 폴더에 보스 프리팹, 임시 지형 맵과 스테이지를 만듭니다. 기존 에셋은 수정하지 않습니다. 생성 후 맵의 지형과 둥지 위치를 편집하세요. 수치는 테스트 시작값입니다.", MessageType.Info);
        sourceStage = (MapConfig)EditorGUILayout.ObjectField("설정을 복사할 스테이지", sourceStage, typeof(MapConfig), false);
        sourceEnemy = (GameObject)EditorGUILayout.ObjectField("보스 외형 원본 적 프리팹", sourceEnemy, typeof(GameObject), false);
        headquarters = (GameObject)EditorGUILayout.ObjectField("플레이어 본부 프리팹", headquarters, typeof(GameObject), false);
        missionName = EditorGUILayout.TextField("미션 이름", missionName);
        bossHealth = EditorGUILayout.FloatField("보스 최대 체력", bossHealth);
        bossDamage = EditorGUILayout.FloatField("보스 공격력", bossDamage);
        bossSpeed = EditorGUILayout.FloatField("보스 이동 속도", bossSpeed);
        zoneWidth = EditorGUILayout.FloatField("교전 구역 너비", zoneWidth);
        zoneSpacing = EditorGUILayout.FloatField("구역 중심 사이 거리", zoneSpacing);
        EditorGUILayout.HelpBox("본부 → 중앙 방어 구역 → 추격 구간 → 둥지 순서로 배치합니다. 일반 적 스포너와 UI는 생성하지 않습니다. 보스 행동 확인 후 일반 공격대를 약하게 추가하세요.", MessageType.None);
        bool valid = sourceStage != null && sourceEnemy != null && headquarters != null &&
            sourceEnemy.GetComponent<EntityHealth>() != null && sourceEnemy.GetComponent<UnitAttacker>() != null &&
            sourceEnemy.GetComponent<NavMeshAgent>() != null && headquarters.GetComponent<Headquarters>() != null &&
            bossHealth > 0f && bossDamage > 0f && bossSpeed > 0f && zoneWidth >= 30f && zoneSpacing >= 20f;
        using (new EditorGUI.DisabledScope(!valid || EditorApplication.isPlaying))
            if (GUILayout.Button("새 보스 미션 에셋 생성")) CreateMission();
    }

    void CreateMission()
    {
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/Data/Stage/BossMission");
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        GameObject enemy = null;
        GameObject map = null;
        try
        {
            enemy = Instantiate(sourceEnemy);
            enemy.name = "보스";
            foreach (CombatAIBase old in enemy.GetComponents<CombatAIBase>()) DestroyImmediate(old);
            foreach (EnemySpawnerGuard guard in enemy.GetComponents<EnemySpawnerGuard>()) DestroyImmediate(guard);
            BossCombatAI ai = enemy.AddComponent<BossCombatAI>();
            ai.aggroRange = 15f;
            enemy.GetComponent<EntityHealth>().maxHealth = bossHealth;
            enemy.GetComponent<UnitAttacker>().attackDamage = bossDamage;
            enemy.GetComponent<NavMeshAgent>().speed = bossSpeed;
            enemy.GetComponent<SelectableEntity>().ownerId = 2;
            enemy.SetActive(true);
            GameObject savedBoss = PrefabUtility.SaveAsPrefabAsset(enemy, folder + "/Boss.prefab");

            map = new GameObject("보스 미션 맵");
            MapRoot root = map.AddComponent<MapRoot>();
            NavMeshSurface surface = map.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.agentTypeID = enemy.GetComponent<NavMeshAgent>().agentTypeID;
            root.navMeshSurface = surface;
            root.bakeNavMeshOnStart = true;
            string[] zones = { "본부 구역", "중앙 방어 구역", "추격 구간", "둥지 구역" };
            for (int i = 0; i < zones.Length; i++)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = zones[i];
                ground.transform.SetParent(map.transform, false);
                ground.transform.localPosition = new Vector3(0f, -0.5f, i * zoneSpacing);
                ground.transform.localScale = new Vector3(i == 2 ? zoneWidth * 0.75f : zoneWidth, 1f, zoneSpacing);
            }
            var hqObject = new GameObject("본부 생성 위치");
            hqObject.transform.SetParent(map.transform, false);
            StageBuildingSpawnPoint marker = hqObject.AddComponent<StageBuildingSpawnPoint>();
            marker.buildingPrefab = headquarters;
            marker.overrideOwner = true;
            marker.ownerId = 1;
            marker.playConstructionEffect = false;
            var nestObject = new GameObject("보스 둥지");
            nestObject.transform.SetParent(map.transform, false);
            nestObject.transform.localPosition = new Vector3(0f, 0f, zoneSpacing * 3f);
            BossNest nest = nestObject.AddComponent<BossNest>();
            nest.bossPrefab = savedBoss.GetComponent<BossCombatAI>();
            nest.displayName = missionName;
            nest.radius = Mathf.Min(zoneWidth, zoneSpacing) * 0.45f;
            GameObject savedMap = PrefabUtility.SaveAsPrefabAsset(map, folder + "/BossMap.prefab");
            MapConfig config = Instantiate(sourceStage);
            config.name = "BossStage";
            config.displayName = missionName;
            config.description = "밤에는 본부를 습격하고 피해를 받으면 둥지로 후퇴하는 보스를 처치하세요. 낮에 둥지를 공격할 수도 있습니다.";
            config.mapRootPrefab = savedMap;
            config.overrideWinCondition = true;
            config.winBySurvivingNights = false;
            config.winWhenAllSpawnersDestroyed = false;
            config.winByCapturingPoint = false;
            config.winByDefeatingBoss = true;
            config.overrideInitialEnemies = true;
            config.initialEnemies.Clear();
            config.mutations.enabled = false;
            config.overrideDayNight = true;
            config.startPhase = DayNightPhase.Day;
            AssetDatabase.CreateAsset(config, folder + "/BossStage.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }
        finally
        {
            if (enemy != null) DestroyImmediate(enemy);
            if (map != null) DestroyImmediate(map);
        }
    }
}
