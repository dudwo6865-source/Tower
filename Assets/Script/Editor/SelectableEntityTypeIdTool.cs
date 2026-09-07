using System.IO;
using UnityEditor;
using UnityEngine;

// SelectableEntity.autoAssignEntityTypeId는 프리팹을 열거나 수정할 때만 갱신됩니다.
// 이미 만들어진 프리팹들을 한 번에 정리하고 싶을 때 이 메뉴를 사용하세요.
public static class SelectableEntityTypeIdTool
{
    [MenuItem("Tools/RTS/Selectable Entity/프리팹 이름으로 EntityTypeId 일괄 설정")]
    static void AssignEntityTypeIdFromPrefabNames()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int updatedCount = 0;
        int skippedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            SelectableEntity selectable = prefab.GetComponent<SelectableEntity>();

            if (selectable == null)
                continue;

            if (!selectable.autoAssignEntityTypeId)
            {
                skippedCount++;
                continue;
            }

            string prefabName = Path.GetFileNameWithoutExtension(path);

            if (selectable.entityTypeId == prefabName)
                continue;

            selectable.entityTypeId = prefabName;
            EditorUtility.SetDirty(selectable);
            updatedCount++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[SelectableEntityTypeIdTool] entityTypeId {updatedCount}개 프리팹에 자동 적용 완료" +
            (skippedCount > 0 ? $" ({skippedCount}개는 autoAssignEntityTypeId 꺼져 있어 건너뜀)" : "") +
            ".");
    }
}
