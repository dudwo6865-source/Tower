using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 에디터 목록 순서를 프로젝트에 저장한다. 런타임 미션 해금 순서와는 별개다.
[FilePath("ProjectSettings/StageEditorOrder.asset", FilePathAttribute.Location.ProjectFolder)]
public class StageEditorOrderSettings : ScriptableSingleton<StageEditorOrderSettings>
{
    [SerializeField, Label("스테이지 정렬 순서")]
    List<string> stageGuids = new List<string>();

    public void Sort(List<MapConfig> stages)
    {
        var ranks = new Dictionary<string, int>();
        for (int i = 0; i < stageGuids.Count; i++)
            if (!ranks.ContainsKey(stageGuids[i])) ranks.Add(stageGuids[i], i);
        stages.Sort((a, b) =>
        {
            string pathA = AssetDatabase.GetAssetPath(a);
            string pathB = AssetDatabase.GetAssetPath(b);
            int rankA = ranks.TryGetValue(AssetDatabase.AssetPathToGUID(pathA), out int aIndex) ? aIndex : int.MaxValue;
            int rankB = ranks.TryGetValue(AssetDatabase.AssetPathToGUID(pathB), out int bIndex) ? bIndex : int.MaxValue;
            int comparison = rankA.CompareTo(rankB);
            if (comparison != 0) return comparison;
            comparison = string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
            return comparison != 0 ? comparison : string.Compare(pathA, pathB, StringComparison.Ordinal);
        });
    }

    public void Store(List<MapConfig> stages)
    {
        Undo.RecordObject(this, "스테이지 목록 순서 변경");
        stageGuids.Clear();
        foreach (var stage in stages)
            stageGuids.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(stage)));
        Persist();
    }

    public void Persist() => Save(true);
}

public partial class StageEditorWindow
{
    void OnDisable() => Undo.undoRedoPerformed -= OnStageOrderUndoRedo;

    void OnStageOrderUndoRedo()
    {
        StageEditorOrderSettings.instance.Persist();
        RefreshStageList();
        Repaint();
    }

    void DrawStageOrderControls()
    {
        int index = stages.IndexOf(selected);
        bool searching = !string.IsNullOrEmpty(searchFilter);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(searching || index <= 0))
            if (GUILayout.Button(new GUIContent("▲ 위로", "선택한 스테이지를 한 칸 위로 옮기고 순서를 저장합니다.")))
                MoveSelectedStage(index, index - 1);
        using (new EditorGUI.DisabledScope(searching || index < 0 || index >= stages.Count - 1))
            if (GUILayout.Button(new GUIContent("▼ 아래로", "선택한 스테이지를 한 칸 아래로 옮기고 순서를 저장합니다.")))
                MoveSelectedStage(index, index + 1);
        EditorGUILayout.EndHorizontal();
        if (searching)
            EditorGUILayout.HelpBox("검색을 지우면 순서를 조정할 수 있습니다.", MessageType.Info);
    }

    void MoveSelectedStage(int from, int to)
    {
        var stage = stages[from];
        stages.RemoveAt(from);
        stages.Insert(to, stage);
        StageEditorOrderSettings.instance.Store(stages);
        Repaint();
    }
}
