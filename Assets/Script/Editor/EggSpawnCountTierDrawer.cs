using UnityEditor;
using UnityEngine;

// 알 미션의 '파괴 수별 출현 수' 구간 하나를 한 줄로 그린다: "파괴 [N]개 이상 → 한 번에 [M]개".
[CustomPropertyDrawer(typeof(EggSpawnCountTier))]
public class EggSpawnCountTierDrawer : PropertyDrawer
{
    const float FieldWidth = 48f;
    const float Gap = 4f;

    static readonly GUIContent DestroyedPrefix = new GUIContent("파괴", "지금까지 파괴한 알이 이 수 이상이면 이 구간을 씁니다.");
    static readonly GUIContent DestroyedSuffix = new GUIContent("개 이상  →  한 번에");
    static readonly GUIContent CountSuffix = new GUIContent("개", "이 구간에서 출현 차례마다 놓는 알 수입니다.");

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty destroyed = property.FindPropertyRelative("destroyedAtLeast");
        SerializedProperty count = property.FindPropertyRelative("eggsPerSpawn");

        EditorGUI.BeginProperty(position, label, property);
        Rect rect = EditorGUI.IndentedRect(position);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float x = rect.x;
        x = DrawLabel(rect, x, DestroyedPrefix);
        x = DrawInt(rect, x, destroyed, EggSpawnCountTier.MinDestroyedAtLeast);
        x = DrawLabel(rect, x, DestroyedSuffix);
        x = DrawInt(rect, x, count, EggSpawnCountTier.MinEggsPerSpawn);
        DrawLabel(rect, x, CountSuffix);

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    static float DrawLabel(Rect row, float x, GUIContent content)
    {
        float width = EditorStyles.label.CalcSize(content).x;
        EditorGUI.LabelField(new Rect(x, row.y, width, row.height), content);
        return x + width + Gap;
    }

    // 필드에 [Label]이 붙어 있어 PropertyField로 그리면 라벨이 다시 붙으므로 값만 직접 그린다.
    static float DrawInt(Rect row, float x, SerializedProperty property, int min)
    {
        EditorGUI.BeginChangeCheck();
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        int value = EditorGUI.IntField(new Rect(x, row.y, FieldWidth, row.height), property.intValue);
        EditorGUI.showMixedValue = false;
        if (EditorGUI.EndChangeCheck()) property.intValue = Mathf.Max(min, value);
        return x + FieldWidth + Gap;
    }
}
