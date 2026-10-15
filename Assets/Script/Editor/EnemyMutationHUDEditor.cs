using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(EnemyMutationHUD))]
public class EnemyMutationHUDEditor : Editor
{
    [MenuItem("Tools/게임/돌연변이 매니저 생성 또는 선택")]
    public static void CreateOrSelectManager()
    {
        if (EditorApplication.isPlaying)
        {
            EnemyMutationHUD.EnsureInstance();
            Selection.activeGameObject = Object.FindObjectOfType<EnemyMutationHUD>()?.gameObject;
            return;
        }

        EnemyMutationHUD hud = Object.FindObjectOfType<EnemyMutationHUD>(true);
        EnemyMutationManager manager = Object.FindObjectOfType<EnemyMutationManager>(true);
        GameObject targetObject = hud != null ? hud.gameObject : (manager != null ? manager.gameObject : null);
        bool changed = false;
        if (targetObject == null)
        {
            targetObject = new GameObject("돌연변이 매니저");
            Undo.RegisterCreatedObjectUndo(targetObject, "돌연변이 매니저 생성");
            changed = true;
        }
        if (manager == null)
        {
            Undo.AddComponent<EnemyMutationManager>(targetObject);
            changed = true;
        }
        if (hud == null)
        {
            Undo.AddComponent<EnemyMutationHUD>(targetObject);
            changed = true;
        }
        if (changed) EditorSceneManager.MarkSceneDirty(targetObject.scene);
        Selection.activeGameObject = targetObject;
        EditorGUIUtility.PingObject(targetObject);
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("돌연변이 알림 매니저입니다. 직접 만든 TMP 텍스트를 연결하거나 자동 UI를 사용할 수 있습니다. " +
            "알림 시작/종료 효과에는 파티클 등 원하는 동작을 연결합니다. 설정을 보존하려면 플레이 전에 배치하고 씬을 저장하세요.", MessageType.Info);
        DrawDefaultInspector();
        EnemyMutationHUD hud = (EnemyMutationHUD)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("문구 미리보기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(hud.FormatNotification(hud.previewType, hud.previewIncrease), MessageType.None);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !hud.isActiveAndEnabled ||
            (GameResultManager.Instance != null && GameResultManager.Instance.IsGameOver)))
        {
            if (GUILayout.Button("알림과 효과 미리 재생 (플레이 중)"))
                hud.PreviewNotification();
        }
        EditorGUILayout.HelpBox("미리보기는 실제 돌연변이·적 능력치·보상에 영향을 주지 않습니다. 연결한 효과음과 시작/종료 이벤트는 재생합니다.", MessageType.None);
    }
}

[CustomEditor(typeof(EnemyMutationManager))]
public class EnemyMutationManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("확률과 강화 수치를 관리합니다. MapLoader가 있는 씬은 스테이지 에디터의 돌연변이 설정이 실행 시 적용됩니다. " +
            "알림 문구·색상·효과는 돌연변이 알림 매니저에서 조절합니다.", MessageType.Info);
        DrawDefaultInspector();
        if (GUILayout.Button("알림 매니저 생성 또는 선택"))
            EnemyMutationHUDEditor.CreateOrSelectManager();
        if (Application.isPlaying)
            EditorGUILayout.HelpBox(((EnemyMutationManager)target).Summary, MessageType.None);
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
