using UnityEditor;
using UnityEngine;

// GridFootprint 인스펙터. 기본 항목 아래에 '모양 직접 지정'용 칸 격자를 그려
// 클릭으로 칸을 켜고 끌 수 있게 한다. 격자의 위쪽이 월드 +Z, 오른쪽이 월드 +X이다.
[CustomEditor(typeof(GridFootprint))]
[CanEditMultipleObjects]
public class GridFootprintEditor : Editor
{
    const float CellButtonSize = 26f;
    const float CellGap = 2f;

    static readonly Color UsedColor = new Color(0.3f, 0.75f, 1f);
    static readonly Color UnusedColor = new Color(0.25f, 0.25f, 0.25f);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GridFootprint footprint = (GridFootprint)target;

        if (!footprint.useCustomShape)
            return;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("칸 모양", EditorStyles.boldLabel);

        if (targets.Length > 1)
        {
            EditorGUILayout.HelpBox("칸 모양은 건물을 하나만 선택했을 때 편집할 수 있습니다.", MessageType.Info);
            return;
        }

        footprint.EnsureCellMask();
        Vector2Int size = footprint.footprintCells;

        EditorGUILayout.HelpBox(
            "칸을 클릭해 켜고 끕니다. 파란 칸만 점유하고 NavMesh 구멍을 뚫습니다. " +
            "위쪽이 월드 +Z(북), 오른쪽이 월드 +X(동)입니다. Scene 뷰에서 건물을 선택하면 모양이 기즈모로 보입니다.",
            MessageType.None);

        DrawCellGrid(footprint, size);
        DrawShapeButtons(footprint);
        DrawShapeSummary(footprint, size);
    }

    void DrawCellGrid(GridFootprint footprint, Vector2Int size)
    {
        float gridWidth = size.x * (CellButtonSize + CellGap);

        GUIStyle centered = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };

        Rect northRect = GUILayoutUtility.GetRect(gridWidth, 14f, GUILayout.ExpandWidth(false));
        GUI.Label(northRect, "▲ +Z", centered);

        Rect gridRect = GUILayoutUtility.GetRect(
            gridWidth,
            size.y * (CellButtonSize + CellGap),
            GUILayout.ExpandWidth(false));

        Color previousColor = GUI.backgroundColor;

        // 위쪽 행이 +Z가 되도록 z를 큰 값부터 그린다.
        for (int row = 0; row < size.y; row++)
        {
            int z = size.y - 1 - row;

            for (int x = 0; x < size.x; x++)
            {
                Rect cellRect = new Rect(
                    gridRect.x + x * (CellButtonSize + CellGap),
                    gridRect.y + row * (CellButtonSize + CellGap),
                    CellButtonSize,
                    CellButtonSize);

                bool used = footprint.GetCell(x, z);
                GUI.backgroundColor = used ? UsedColor : UnusedColor;

                if (GUI.Button(cellRect, GUIContent.none))
                {
                    RecordChange(footprint, "칸 모양 변경");
                    footprint.SetCell(x, z, !used);
                    ApplyChange(footprint);
                }
            }
        }

        GUI.backgroundColor = previousColor;

        Rect eastRect = GUILayoutUtility.GetRect(gridWidth, 14f, GUILayout.ExpandWidth(false));
        GUI.Label(eastRect, "+X ▶", centered);
    }

    void DrawShapeButtons(GridFootprint footprint)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("모두 켜기"))
            {
                RecordChange(footprint, "칸 모양 모두 켜기");
                footprint.SetAllCells(true);
                ApplyChange(footprint);
            }

            if (GUILayout.Button("모두 끄기"))
            {
                RecordChange(footprint, "칸 모양 모두 끄기");
                footprint.SetAllCells(false);
                ApplyChange(footprint);
            }

            if (GUILayout.Button("반전"))
            {
                RecordChange(footprint, "칸 모양 반전");

                Vector2Int size = footprint.footprintCells;

                for (int z = 0; z < size.y; z++)
                {
                    for (int x = 0; x < size.x; x++)
                        footprint.SetCell(x, z, !footprint.GetCell(x, z));
                }

                ApplyChange(footprint);
            }
        }
    }

    static void DrawShapeSummary(GridFootprint footprint, Vector2Int size)
    {
        bool[] mask = footprint.ActiveMask;
        int count = FootprintShape.CountCells(mask, size);

        if (count == 0)
        {
            EditorGUILayout.HelpBox("쓰는 칸이 하나도 없습니다. 이 상태로는 건물을 지을 수 없습니다.", MessageType.Warning);
            return;
        }

        int boxes = FootprintShape.ToRects(mask, size).Count;
        EditorGUILayout.LabelField(
            $"쓰는 칸 {count} / {size.x * size.y}  ·  NavMesh 장애물 박스 {boxes}개",
            EditorStyles.miniLabel);
    }

    static void RecordChange(GridFootprint footprint, string undoName)
    {
        Undo.RecordObject(footprint, undoName);
    }

    static void ApplyChange(GridFootprint footprint)
    {
        EditorUtility.SetDirty(footprint);
        PrefabUtility.RecordPrefabInstancePropertyModifications(footprint);
        SceneView.RepaintAll();
    }
}
