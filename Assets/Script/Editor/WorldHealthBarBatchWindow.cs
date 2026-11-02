using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class WorldHealthBarBatchWindow : EditorWindow
{
    [SerializeField, Label("공용 체력바 프리팹")]
    private GameObject barPrefab;
    [SerializeField, Label("적 체력바 항상 표시 적용")]
    private bool setAlwaysVisible;
    [SerializeField, Label("적 체력바 항상 표시 값")]
    private bool alwaysVisible = true;
    [SerializeField, Label("적용 대상")]
    private List<GameObject> targets = new List<GameObject>();
    private Vector2 scroll;
    private string result;

    [MenuItem("Tools/UI/체력바 일괄 적용")]
    static void Open()
    {
        WorldHealthBarBatchWindow window = GetWindow<WorldHealthBarBatchWindow>("체력바 일괄 적용");
        window.FindHealthPrefabs();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("공용 체력바 프리팹 루트에 WorldHealthBarView를 붙이고 한 번만 연결하세요. 적용하면 대상 프리팹 에셋을 바로 저장합니다. 런타임에는 UI를 만들지 않습니다.", MessageType.Info);
        barPrefab = (GameObject)EditorGUILayout.ObjectField("공용 체력바 프리팹", barPrefab, typeof(GameObject), false);
        bool validSource = WorldHealthBarBatchUtility.ValidateSource(barPrefab, out string sourceReason);
        if (!validSource)
            EditorGUILayout.HelpBox(sourceReason, MessageType.Warning);

        setAlwaysVisible = EditorGUILayout.Toggle("항상 표시 설정도 적용", setAlwaysVisible);
        if (setAlwaysVisible)
            alwaysVisible = EditorGUILayout.Toggle("적 체력바 항상 표시", alwaysVisible);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling))
        {
            if (GUILayout.Button("EntityHealth 프리팹 자동 선택"))
                FindHealthPrefabs();
        }
        EditorGUILayout.HelpBox("Assets 전체에서 본체에 EntityHealth가 있는 프리팹을 찾습니다. 하위 유닛만 포함한 맵·스테이지 프리팹은 제외합니다. 적용하지 않을 대상은 목록의 제외 버튼으로 빼세요.", MessageType.Info);

        if (GUILayout.Button("Project에서 선택한 프리팹 가져오기"))
        {
            targets = Selection.objects.OfType<GameObject>()
                .Where(item => !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(item)))
                .Select(item => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(item)))
                .Where(item => item != null).Distinct().ToList();
            result = null;
        }

        EditorGUILayout.LabelField($"적용 대상: {targets.Count}개");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < targets.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(targets[i], typeof(GameObject), false);
                if (GUILayout.Button("제외", GUILayout.Width(45f)))
                    targets.RemoveAt(i--);
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.HelpBox("같은 공용 체력바가 있으면 다시 연결하며 위치·크기를 유지합니다. 다른 체력바나 기존 수동 연결이 있으면 건너뜁니다. 프리팹 모드에서 열어 둔 대상은 닫고 적용하세요. 에셋 저장은 Ctrl+Z로 되돌릴 수 없습니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!validSource || targets.Count == 0
            || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            if (GUILayout.Button("선택한 프리팹에 체력바 적용"))
                Apply();
        }
        EditorGUILayout.HelpBox("일괄 제거는 목록의 유닛 본체 아래에 있는 WorldHealthBarView 공용 체력바를 제거합니다. 공용 원본 에셋은 삭제하지 않습니다. WorldHealthBar는 연결을 비우고 꺼 두며, 다시 적용하면 켜집니다. 수동 연결이 섞인 대상은 건너뜁니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(targets.Count == 0
            || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            if (GUILayout.Button("목록의 공용 체력바 일괄 제거"))
                Apply(remove: true);
        }
        if (!string.IsNullOrEmpty(result))
            EditorGUILayout.HelpBox(result, MessageType.Info);
    }

    void FindHealthPrefabs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        targets = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(prefab => prefab != null && prefab != barPrefab
                && prefab.GetComponent<EntityHealth>() != null)
            .Distinct().ToList();
        result = $"EntityHealth가 있는 프리팹 {targets.Count}개를 찾았습니다. 대상 목록을 확인한 뒤 적용하세요.";
        Repaint();
    }

    void Apply(bool remove = false)
    {
        string operation = remove ? "제거" : "적용";
        int applied = 0;
        int skipped = 0;
        // 기반 프리팹을 먼저 저장하여 변형 프리팹에 체력바가 중복 추가되지 않게 한다.
        GameObject[] orderedTargets = targets.OrderBy(target => target == null ? int.MaxValue
            : AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(target), true).Length).ToArray();
        foreach (GameObject target in orderedTargets)
        {
            string path = AssetDatabase.GetAssetPath(target);
            GameObject contents = null;
            try
            {
                if (!WorldHealthBarBatchUtility.ValidateTargetAsset(target, barPrefab, out string reason, remove))
                {
                    skipped++;
                    Debug.LogWarning($"체력바 일괄 {operation}: {path} — {reason}", target);
                    continue;
                }

                contents = PrefabUtility.LoadPrefabContents(path);
                bool changed = remove
                    ? WorldHealthBarBatchUtility.TryRemove(contents, out reason)
                    : WorldHealthBarBatchUtility.TryApply(contents, barPrefab,
                        setAlwaysVisible, alwaysVisible, out reason);
                if (!changed)
                {
                    skipped++;
                    Debug.LogWarning($"체력바 일괄 {operation}: {path} — {reason}", target);
                    continue;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path, out bool success);
                if (!success)
                    throw new InvalidOperationException("프리팹 저장에 실패했습니다.");
                applied++;
            }
            catch (Exception exception)
            {
                skipped++;
                Debug.LogError($"체력바 일괄 {operation}: {path} — {exception.Message}", target);
            }
            finally
            {
                if (contents != null)
                    PrefabUtility.UnloadPrefabContents(contents);
            }
        }
        result = $"{operation} {applied}개 / 건너뜀·실패 {skipped}개. 상세 사유는 Console에서 확인하세요.";
    }
}

public static class WorldHealthBarBatchUtility
{
    public static bool ValidateSource(GameObject prefab, out string reason)
    {
        reason = null;
        string path = AssetDatabase.GetAssetPath(prefab);
        if (prefab == null || !path.StartsWith("Assets/", StringComparison.Ordinal)
            || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
            || AssetDatabase.LoadAssetAtPath<GameObject>(path) != prefab)
            reason = "Project 창의 공용 체력바 프리팹 루트를 지정해 주세요.";
        else if (prefab.GetComponentsInChildren<EntityHealth>(true).Length > 0
            || prefab.GetComponentsInChildren<WorldHealthBar>(true).Length > 0)
            reason = "공용 프리팹에는 UI와 WorldHealthBarView만 두세요. EntityHealth와 WorldHealthBar는 유닛 본체에 둡니다.";
        else if (prefab.GetComponentsInChildren<WorldHealthBarView>(true).Length != 1
            || prefab.GetComponent<WorldHealthBarView>() == null)
            reason = "공용 프리팹 루트에 WorldHealthBarView를 하나만 추가해 주세요.";
        else if (!prefab.activeSelf)
            reason = "공용 체력바 프리팹 루트를 활성화해 주세요.";
        else
            return prefab.GetComponent<WorldHealthBarView>().TryValidate(out reason);
        return false;
    }

    public static bool ValidateTargetAsset(GameObject target, GameObject source, out string reason, bool removing = false)
    {
        reason = null;
        string path = AssetDatabase.GetAssetPath(target);
        if (!removing && !ValidateSource(source, out reason))
            return false;
        if (target == null || !path.StartsWith("Assets/", StringComparison.Ordinal)
            || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
            || PrefabUtility.GetPrefabAssetType(target) == PrefabAssetType.Model)
            reason = "Assets 폴더의 유닛·건물 프리팹만 적용할 수 있습니다.";
        else if (PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == path)
            reason = "이 대상의 프리팹 모드를 닫은 뒤 다시 적용해 주세요.";
        else if (!AssetDatabase.IsOpenForEdit(path))
            reason = "이 프리팹은 현재 편집할 수 없습니다.";
        else if (target.GetComponent<EntityHealth>() == null
            || target.GetComponent<SelectableEntity>() == null)
            reason = "프리팹 본체에 EntityHealth와 SelectableEntity가 필요합니다. 유닛·건물 본체를 선택해 주세요.";
        else if (!removing && AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(source), true).Contains(path))
            reason = "자기 자신 또는 순환 참조가 생기는 프리팹에는 적용할 수 없습니다.";
        return reason == null;
    }

    public static bool TryApply(GameObject root, GameObject source,
        bool setAlwaysVisible, bool alwaysVisible, out string reason)
    {
        if (!ValidateSource(source, out reason))
            return false;
        EntityHealth owner = root.GetComponent<EntityHealth>();
        if (owner == null || root.GetComponent<SelectableEntity>() == null)
        {
            reason = "대상 본체에 EntityHealth와 SelectableEntity가 필요합니다.";
            return false;
        }

        WorldHealthBar bar = root.GetComponent<WorldHealthBar>();
        WorldHealthBarView[] views = root.GetComponentsInChildren<WorldHealthBarView>(true)
            .Where(view => view.GetComponentInParent<EntityHealth>(true) == owner).ToArray();
        if (views.Length > 1)
        {
            reason = "체력바가 여러 개 있습니다. 사용할 체력바를 하나만 남겨 주세요.";
            return false;
        }

        WorldHealthBarView view = views.FirstOrDefault();
        if (view != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.gameObject)
            != AssetDatabase.GetAssetPath(source))
        {
            reason = "다른 체력바가 이미 있습니다. 기존 체력바는 유지했습니다.";
            return false;
        }
        if (bar != null && (bar.fillImage != null || bar.displayGroup != null
            || bar.healthText != null || bar.followAnchor != null)
            && (view == null || !ReferencesBelongToView(bar, view)))
        {
            reason = "기존 수동 연결이 있습니다. 기존 연결과 디자인은 유지했습니다.";
            return false;
        }

        if (view == null)
        {
            // 중첩 프리팹 연결을 보존하여 이후 공용 디자인 변경이 전파되도록 한다.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            view = instance.GetComponent<WorldHealthBarView>();
        }
        if (!view.TryValidate(out reason))
            return false;

        if (bar == null)
            bar = root.AddComponent<WorldHealthBar>();
        bar.fillImage = view.fillImage;
        bar.displayGroup = view.displayGroup;
        bar.healthText = view.healthText;
        bar.followAnchor = view.followAnchor;
        bar.enabled = true;
        if (setAlwaysVisible)
            bar.alwaysShowEnemyBar = alwaysVisible;
        if (PrefabUtility.IsPartOfPrefabInstance(bar))
            PrefabUtility.RecordPrefabInstancePropertyModifications(bar);
        EditorUtility.SetDirty(bar);
        reason = null;
        return true;
    }

    public static bool TryRemove(GameObject root, out string reason)
    {
        reason = null;
        WorldHealthBarView[] views = root.GetComponentsInChildren<WorldHealthBarView>(true)
            .Where(view => view.transform.parent == root.transform).ToArray();
        if (views.Length == 0)
        {
            reason = "제거할 공용 체력바가 없습니다. 수동 체력바는 유지했습니다.";
            return false;
        }
        // 유닛이나 다른 프리팹의 일부를 잘못 지우지 않도록 제거 범위를 확인한다.
        if (views.Any(view => view.GetComponentsInChildren<EntityHealth>(true).Length > 0
            || PrefabUtility.GetNearestPrefabInstanceRoot(view.gameObject) != view.gameObject))
        {
            reason = "공용 체력바 프리팹 루트로 확인되지 않는 대상이 있어 제거하지 않았습니다.";
            return false;
        }

        WorldHealthBar bar = root.GetComponent<WorldHealthBar>();
        if (bar != null)
        {
            Transform[] references = { bar.fillImage != null ? bar.fillImage.transform : null,
                bar.displayGroup != null ? bar.displayGroup.transform : null,
                bar.healthText != null ? bar.healthText.transform : null, bar.followAnchor };
            if (references.Any(reference => reference != null
                && !views.Any(view => reference.IsChildOf(view.transform))))
            {
                reason = "수동 연결이 섞여 있어 기존 체력바와 연결을 유지했습니다.";
                return false;
            }
            // 컴포넌트를 남겨 자동 보충 기능이 다시 추가하지 않도록 한다.
            bar.fillImage = null;
            bar.displayGroup = null;
            bar.healthText = null;
            bar.followAnchor = null;
            bar.enabled = false;
            if (PrefabUtility.IsPartOfPrefabInstance(bar))
                PrefabUtility.RecordPrefabInstancePropertyModifications(bar);
            EditorUtility.SetDirty(bar);
        }
        foreach (WorldHealthBarView view in views)
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        return true;
    }

    static bool ReferencesBelongToView(WorldHealthBar bar, WorldHealthBarView view)
    {
        return (bar.fillImage == null || bar.fillImage.transform.IsChildOf(view.transform))
            && (bar.displayGroup == null || bar.displayGroup.transform.IsChildOf(view.transform))
            && (bar.healthText == null || bar.healthText.transform.IsChildOf(view.transform))
            && (bar.followAnchor == null || bar.followAnchor.IsChildOf(view.transform));
    }
}
