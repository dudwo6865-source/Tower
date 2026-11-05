using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 유닛·건물에 붙은 월드 체력바(WorldHealthBarView와 그 자식)의 레이어를 HealthBar로 바꿉니다.
/// 렌더러 설정이나 다른 오브젝트의 레이어는 건드리지 않습니다.
/// </summary>
public static class UnitRenderLayerSetup
{
    public const string HealthBarLayerName = "HealthBar";

    [MenuItem("Tools/유닛/체력바 레이어 설정")]
    static void Run()
    {
        int layer = LayerMask.NameToLayer(HealthBarLayerName);
        if (layer < 0)
        {
            EditorUtility.DisplayDialog("체력바 레이어 설정",
                $"'{HealthBarLayerName}' 레이어가 없습니다. Project Settings > Tags and Layers에서 먼저 만드세요.", "확인");
            return;
        }

        int prefabCount = ApplyToPrefabs(layer);
        int sceneCount = ApplyToOpenScenes(layer);
        AssetDatabase.SaveAssets();

        string message = $"바꾼 프리팹: {prefabCount}개\n바꾼 씬 오브젝트: {sceneCount}개";
        Debug.Log($"UnitRenderLayerSetup: {message.Replace("\n", " / ")}");
        EditorUtility.DisplayDialog("체력바 레이어 설정", message, "확인");
    }

    static int ApplyToPrefabs(int layer)
    {
        int count = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("체력바 레이어 설정", path, (float)i / guids.Length);

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || CountChanges(asset.transform, layer) == 0) continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (Apply(root.transform, layer, record: false) > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        count++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        return count;
    }

    static int ApplyToOpenScenes(int layer)
    {
        int count = 0;
        var scenes = new HashSet<UnityEngine.SceneManagement.Scene>();
        foreach (GameObject go in Object.FindObjectsOfType<GameObject>(true))
        {
            if (EditorUtility.IsPersistent(go) || go.transform.parent != null) continue;

            int changed = Apply(go.transform, layer, record: true);
            if (changed == 0) continue;
            count += changed;
            scenes.Add(go.scene);
        }
        foreach (var scene in scenes) EditorSceneManager.MarkSceneDirty(scene);
        return count;
    }

    // 체력바 루트(WorldHealthBarView)와 그 아래 오브젝트 중 레이어가 다른 것을 센다.
    static int CountChanges(Transform root, int layer)
    {
        int count = 0;
        foreach (WorldHealthBarView view in root.GetComponentsInChildren<WorldHealthBarView>(true))
        {
            foreach (Transform child in view.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.layer != layer) count++;
            }
        }
        return count;
    }

    static int Apply(Transform root, int layer, bool record)
    {
        int count = 0;
        foreach (WorldHealthBarView view in root.GetComponentsInChildren<WorldHealthBarView>(true))
        {
            foreach (Transform child in view.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = child.gameObject;
                if (go.layer == layer) continue;
                if (record) Undo.RecordObject(go, "체력바 레이어 설정");
                go.layer = layer;
                if (record) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                count++;
            }
        }
        return count;
    }
}
