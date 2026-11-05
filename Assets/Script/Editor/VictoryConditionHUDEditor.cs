using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VictoryConditionHUD))]
[CanEditMultipleObjects]
public class VictoryConditionHUDEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        bool showBossHealth = false;
        foreach (Object selected in targets)
            if (UsesBossObjective((VictoryConditionHUD)selected)) showBossHealth = true;

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (!showBossHealth && (property.name == "bossHealthFill" || property.name == "bossHealthText"))
                continue;
            using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                EditorGUILayout.PropertyField(property, true);
        }
        serializedObject.ApplyModifiedProperties();
    }

    static bool UsesBossObjective(VictoryConditionHUD hud)
    {
        GameResultManager result = hud.resultManager;
        if (result == null) result = FindInScene<GameResultManager>(hud);

        // 플레이 중에는 실제 적용된 조건을 따른다.
        if (Application.isPlaying) return result != null && result.winByDefeatingBoss;

        // 편집 중에는 씬 매니저보다 맵 로더의 스테이지 덮어쓰기가 우선한다.
        MapLoader loader = FindInScene<MapLoader>(hud);
        if (loader != null && loader.loadOnAwake)
        {
            MapConfig config = MapLoader.PendingConfig != null ? MapLoader.PendingConfig : loader.defaultConfig;
            if (config != null && config.overrideWinCondition) return config.winByDefeatingBoss;
        }
        return result != null && result.winByDefeatingBoss;
    }

    static T FindInScene<T>(VictoryConditionHUD hud) where T : Behaviour
    {
        if (!hud.gameObject.scene.IsValid() || !hud.gameObject.scene.isLoaded) return null;
        foreach (GameObject root in hud.gameObject.scene.GetRootGameObjects())
            foreach (T component in root.GetComponentsInChildren<T>())
                if (component.isActiveAndEnabled) return component;
        return null;
    }
}
