using UnityEditor;
using UnityEngine;

// UnitSoundClip을 "번호 | 클립 | 볼륨 슬라이더" 한 줄로 그린다.
// 클립 필드는 ObjectField로 직접 그린다. PropertyField로 그리면 필드의 [Label] 드로어가
// 빈 라벨 대신 "클립"을 끼워 넣어 필드 자리가 좁아지고 선택 버튼이 제대로 눌리지 않는다.
[CustomPropertyDrawer(typeof(UnitSoundClip))]
public class UnitSoundClipDrawer : PropertyDrawer
{
    const float IndexWidth = 22f;
    const float ClipWidthRatio = 0.5f;
    const float Spacing = 4f;
    const float MinVolume = 0f;
    const float MaxVolume = 2f; // UnitSoundClip.volume의 [Range]와 맞춘다.

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty clip = property.FindPropertyRelative("clip");
        SerializedProperty volume = property.FindPropertyRelative("volume");
        SerializedProperty initialized = property.FindPropertyRelative("initialized");

        // + 버튼으로 막 추가된 칸은 볼륨이 0이다. 그리는 시점에 1로 맞춰 저장되게 한다.
        if (initialized != null && !initialized.boolValue)
        {
            volume.floatValue = 1f;
            initialized.boolValue = true;
        }

        EditorGUI.BeginProperty(position, label, property);

        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float lineHeight = EditorGUIUtility.singleLineHeight;
        Rect line = new Rect(position.x, position.y, position.width, lineHeight);

        Rect indexRect = new Rect(line.x, line.y, IndexWidth, lineHeight);
        float rest = line.width - IndexWidth - Spacing;
        float clipWidth = rest * ClipWidthRatio;

        Rect clipRect = new Rect(indexRect.xMax + Spacing, line.y, clipWidth, lineHeight);
        Rect volumeRect = new Rect(
            clipRect.xMax + Spacing,
            line.y,
            line.xMax - clipRect.xMax - Spacing,
            lineHeight);

        EditorGUI.LabelField(indexRect, (GetArrayIndex(property) + 1).ToString(), EditorStyles.miniLabel);

        EditorGUI.ObjectField(clipRect, clip, typeof(AudioClip), GUIContent.none);

        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 34f;
        EditorGUI.Slider(
            volumeRect,
            volume,
            MinVolume,
            MaxVolume,
            new GUIContent("볼륨", volume.tooltip));
        EditorGUIUtility.labelWidth = previousLabelWidth;

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    static int GetArrayIndex(SerializedProperty property)
    {
        string path = property.propertyPath;
        int open = path.LastIndexOf('[');
        int close = path.LastIndexOf(']');

        if (open < 0 || close <= open)
            return 0;

        return int.TryParse(path.Substring(open + 1, close - open - 1), out int index) ? index : 0;
    }
}
