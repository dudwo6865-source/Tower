using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TmpUnicodeCollector
{
    public sealed class Settings
    {
        public bool scanScenes = true;
        public bool scanPrefabs = true;
        public bool scanScriptableObjects = true;
        public bool scanCSharpScripts = true;
        public bool includeAsciiPrintable = true;
        public bool excludeTmpExamples = true;
        public string[] excludePathContains =
        {
            "/TextMesh Pro/Examples",
            "/PluginMaster/",
            "/Plugins/",
            "/vHierarchy/",
        };
    }

    public sealed class Result
    {
        public string characters = string.Empty;
        public int characterCount;
        public int sourceCount;
        public List<string> sources = new List<string>();
    }

    static readonly Regex CSharpStringRegex = new Regex(
        @"(?<literal>@""(?:""""|[^""])*"")|(?<literal>""(?:\\.|[^""\\])*"")",
        RegexOptions.Compiled);

    static readonly string[] SearchFolders = { "Assets" };

    public static Result Collect(Settings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        HashSet<char> characters = new HashSet<char>();
        HashSet<string> sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (settings.includeAsciiPrintable)
        {
            for (int i = 32; i <= 126; i++)
                characters.Add((char)i);
        }

        if (settings.scanPrefabs)
            CollectFromPrefabs(settings, characters, sources);

        if (settings.scanScenes)
            CollectFromScenes(settings, characters, sources);

        if (settings.scanScriptableObjects)
            CollectFromScriptableObjects(settings, characters, sources);

        if (settings.scanCSharpScripts)
            CollectFromCSharpScripts(settings, characters, sources);

        char[] sorted = characters
            .Where(c => !char.IsControl(c))
            .OrderBy(c => c)
            .ToArray();

        return new Result
        {
            characters = new string(sorted),
            characterCount = sorted.Length,
            sourceCount = sources.Count,
            sources = sources.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    static void CollectFromPrefabs(
        Settings settings,
        HashSet<char> characters,
        HashSet<string> sources)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", SearchFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (ShouldSkipPath(path, settings))
                continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            if (CollectFromGameObject(prefab, path, characters))
                sources.Add(path);
        }
    }

    static void CollectFromScenes(
        Settings settings,
        HashSet<char> characters,
        HashSet<string> sources)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", SearchFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (ShouldSkipPath(path, settings))
                continue;

            if (!IsScannableScenePath(path))
                continue;

            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedByCollector = false;

            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    openedByCollector = true;
                }

                bool found = false;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (CollectFromGameObject(root, path, characters))
                        found = true;
                }

                if (found)
                    sources.Add(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"TMP Unicode Collector: 씬 스캔을 건너뜁니다. {path}\n{exception.Message}");
            }
            finally
            {
                if (openedByCollector && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    static bool IsScannableScenePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string normalized = path.Replace('\\', '/');

        return normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
    }

    static void CollectFromScriptableObjects(
        Settings settings,
        HashSet<char> characters,
        HashSet<string> sources)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", SearchFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (ShouldSkipPath(path, settings))
                continue;

            ScriptableObject asset =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

            if (asset == null)
                continue;

            if (CollectFromObject(asset, path, characters))
                sources.Add(path);
        }
    }

    static void CollectFromCSharpScripts(
        Settings settings,
        HashSet<char> characters,
        HashSet<string> sources)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", SearchFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ShouldSkipPath(path, settings))
                continue;

            string content = File.ReadAllText(path, Encoding.UTF8);
            bool found = false;

            foreach (Match match in CSharpStringRegex.Matches(content))
            {
                string literal = match.Groups["literal"].Value;

                if (string.IsNullOrEmpty(literal))
                    continue;

                string decoded = DecodeCSharpStringLiteral(literal);

                if (AddChars(decoded, characters))
                    found = true;
            }

            if (found)
                sources.Add(path);
        }
    }

    static bool CollectFromGameObject(
        GameObject gameObject,
        string sourcePath,
        HashSet<char> characters)
    {
        bool found = false;

        foreach (TMP_Text tmpText in gameObject.GetComponentsInChildren<TMP_Text>(true))
        {
            if (AddChars(tmpText.text, characters))
                found = true;
        }

        Component[] components = gameObject.GetComponentsInChildren<Component>(true);

        foreach (Component component in components)
        {
            if (component == null)
                continue;

            if (component is TMP_Text)
                continue;

            if (CollectFromObject(component, sourcePath, characters))
                found = true;
        }

        return found;
    }

    static bool CollectFromObject(
        UnityEngine.Object target,
        string sourcePath,
        HashSet<char> characters)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        bool found = false;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = true;

            if (property.propertyType != SerializedPropertyType.String)
                continue;

            if (AddChars(property.stringValue, characters))
                found = true;
        }

        return found;
    }

    static bool AddChars(string value, HashSet<char> characters)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        bool added = false;

        foreach (char character in value)
        {
            if (char.IsControl(character))
                continue;

            if (characters.Add(character))
                added = true;
        }

        return added;
    }

    static bool ShouldSkipPath(string assetPath, Settings settings)
    {
        if (string.IsNullOrEmpty(assetPath))
            return true;

        string normalized = assetPath.Replace('\\', '/');

        if (settings.excludeTmpExamples &&
            normalized.IndexOf("/TextMesh Pro/Examples", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (settings.excludePathContains == null)
            return false;

        foreach (string exclude in settings.excludePathContains)
        {
            if (string.IsNullOrWhiteSpace(exclude))
                continue;

            if (normalized.IndexOf(exclude, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static string DecodeCSharpStringLiteral(string literal)
    {
        if (literal.StartsWith("@\"", StringComparison.Ordinal))
        {
            string inner = literal.Substring(2, literal.Length - 3);
            return inner.Replace("\"\"", "\"");
        }

        string body = literal.Substring(1, literal.Length - 2);
        StringBuilder builder = new StringBuilder(body.Length);

        for (int i = 0; i < body.Length; i++)
        {
            char current = body[i];

            if (current != '\\' || i + 1 >= body.Length)
            {
                builder.Append(current);
                continue;
            }

            char escape = body[++i];

            switch (escape)
            {
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case '\\':
                    builder.Append('\\');
                    break;
                case '"':
                    builder.Append('"');
                    break;
                case 'u':
                    if (i + 4 < body.Length &&
                        int.TryParse(
                            body.Substring(i + 1, 4),
                            System.Globalization.NumberStyles.HexNumber,
                            null,
                            out int unicodeValue))
                    {
                        builder.Append((char)unicodeValue);
                        i += 4;
                    }
                    else
                    {
                        builder.Append('\\');
                        builder.Append(escape);
                    }

                    break;
                default:
                    builder.Append('\\');
                    builder.Append(escape);
                    break;
            }
        }

        return builder.ToString();
    }
}
