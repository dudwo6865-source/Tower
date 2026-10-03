using UnityEditor;
using UnityEngine;

// 웨이브 표의 '적 구성' 목록을 그립니다.
// WaveTuning은 밤 보정·이후 웨이브 증가율에도 쓰이지만, 적 구성은 웨이브 표에서만
// 의미가 있으므로 그 밖의 자리에서는 숨깁니다. 목록 제목도 여기서 한글로 넘깁니다.
[CustomPropertyDrawer(typeof(WaveEnemyComposition))]
public class WaveEnemyCompositionDrawer : PropertyDrawer
{
    const string ListTitle = "적 구성 (웨이브 총량)";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!IsInWaveTable(property))
            return;

        SerializedProperty entries = property.FindPropertyRelative("entries");
        EditorGUI.PropertyField(position, entries, MakeLabel(property, entries), true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 숨길 때는 줄 사이 간격까지 지워 빈 칸이 남지 않게 한다.
        if (!IsInWaveTable(property))
            return -EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty entries = property.FindPropertyRelative("entries");
        return EditorGUI.GetPropertyHeight(entries, MakeLabel(property, entries), true);
    }

    static GUIContent MakeLabel(SerializedProperty property, SerializedProperty entries)
    {
        int total = 0;

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);

            if (entry.FindPropertyRelative("prefab").objectReferenceValue == null)
                continue;

            total += Mathf.Max(0, entry.FindPropertyRelative("count").intValue);
        }

        string title = total > 0 ? $"{ListTitle} · 총 {total}마리" : $"{ListTitle} · 비어 있음";
        return new GUIContent(title, property.tooltip);
    }

    // 웨이브 표(WavePlan.waves)의 한 칸 안에 있을 때만 그린다.
    static bool IsInWaveTable(SerializedProperty property)
    {
        return property.propertyPath.Contains("waves.Array.data[");
    }
}

// 적 구성의 한 줄을 [적 프리팹][마리 수] 한 줄로 그립니다.
[CustomPropertyDrawer(typeof(WaveEnemyEntry))]
public class WaveEnemyEntryDrawer : PropertyDrawer
{
    const float CountWidth = 50f;
    const float UnitLabelWidth = 26f;
    const float Gap = 4f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty prefab = property.FindPropertyRelative("prefab");
        SerializedProperty count = property.FindPropertyRelative("count");

        EditorGUI.BeginProperty(position, label, property);

        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        position.height = EditorGUIUtility.singleLineHeight;

        Rect unitRect = new Rect(position.xMax - UnitLabelWidth, position.y, UnitLabelWidth, position.height);
        Rect countRect = new Rect(unitRect.x - Gap - CountWidth, position.y, CountWidth, position.height);
        Rect prefabRect = new Rect(position.x, position.y, countRect.x - Gap - position.x, position.height);

        EditorGUI.PropertyField(prefabRect, prefab, new GUIContent(string.Empty, prefab.tooltip));

        EditorGUI.BeginChangeCheck();
        int newCount = EditorGUI.IntField(countRect, new GUIContent(string.Empty, count.tooltip), count.intValue);
        if (EditorGUI.EndChangeCheck())
            count.intValue = Mathf.Max(0, newCount);

        EditorGUI.LabelField(unitRect, "마리");

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
