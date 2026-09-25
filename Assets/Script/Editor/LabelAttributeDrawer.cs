using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// [Label("…")]이 붙은 필드를 인스펙터에서 한글 이름으로 그린다.
// 배열 필드에 붙이면 유니티가 드로어를 배열 요소마다 호출하므로,
// 요소는 원래 이름(Element 0 등)을 쓰고 배열 제목은 아래 LabelAttributeInspector가 바꾼다.
[CustomPropertyDrawer(typeof(LabelAttribute))]
public class LabelAttributeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.PropertyField(position, property, ResolveLabel(property, label), true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, ResolveLabel(property, label), true);
    }

    GUIContent ResolveLabel(SerializedProperty property, GUIContent label)
    {
        if (IsArrayElement(property))
            return label;

        LabelAttribute labelAttribute = (LabelAttribute)attribute;
        return new GUIContent(labelAttribute.text, label != null ? label.tooltip : property.tooltip);
    }

    static bool IsArrayElement(SerializedProperty property)
    {
        return property.propertyPath.EndsWith("]", StringComparison.Ordinal);
    }
}

// 커스텀 에디터가 없는 스크립트에 대신 쓰이는 기본 인스펙터다.
// [Label]이 붙은 배열/리스트 필드의 제목만 한글로 바꾸고, 나머지는 기본 인스펙터와 같다.
// [Label]을 하나도 쓰지 않는 스크립트는 기본 인스펙터를 그대로 그린다.
[CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
[CanEditMultipleObjects]
public class LabelAttributeInspector : Editor
{
    public override void OnInspectorGUI()
    {
        LabelAttributeInspectorGUI.Draw(this);
    }
}

[CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
[CanEditMultipleObjects]
public class LabelAttributeScriptableObjectInspector : Editor
{
    public override void OnInspectorGUI()
    {
        LabelAttributeInspectorGUI.Draw(this);
    }
}

static class LabelAttributeInspectorGUI
{
    const BindingFlags FieldFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    // 타입별로 [Label]이 붙은 배열 필드 이름 → 라벨을 캐시한다. 없으면 null.
    static readonly Dictionary<Type, Dictionary<string, string>> arrayLabelCache =
        new Dictionary<Type, Dictionary<string, string>>();

    public static void Draw(Editor editor)
    {
        Dictionary<string, string> arrayLabels = editor.target != null
            ? GetArrayLabels(editor.target.GetType())
            : null;

        if (arrayLabels == null)
        {
            editor.DrawDefaultInspector();
            return;
        }

        SerializedObject serializedObject = editor.serializedObject;
        serializedObject.Update();

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(property, true);

                continue;
            }

            if (arrayLabels.TryGetValue(property.propertyPath, out string labelText))
            {
                EditorGUILayout.PropertyField(
                    property,
                    new GUIContent(labelText, property.tooltip),
                    true);
            }
            else
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    static Dictionary<string, string> GetArrayLabels(Type type)
    {
        if (arrayLabelCache.TryGetValue(type, out Dictionary<string, string> cached))
            return cached;

        Dictionary<string, string> labels = null;

        for (Type current = type; current != null && current != typeof(MonoBehaviour) && current != typeof(ScriptableObject); current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(FieldFlags))
            {
                if (!IsArrayOrList(field.FieldType))
                    continue;

                LabelAttribute labelAttribute = field.GetCustomAttribute<LabelAttribute>(true);

                if (labelAttribute == null)
                    continue;

                labels ??= new Dictionary<string, string>();
                labels[field.Name] = labelAttribute.text;
            }
        }

        arrayLabelCache[type] = labels;
        return labels;
    }

    static bool IsArrayOrList(Type type)
    {
        return type.IsArray ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
    }
}
