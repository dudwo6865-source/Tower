using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class TmpUnicodeCollectorWindow : EditorWindow
{
    const string DefaultOutputPath = "Assets/TextMesh Pro/Resources/TmpProjectCharacters.txt";

    bool scanScenes = true;
    bool scanPrefabs = true;
    bool scanScriptableObjects = true;
    bool scanCSharpScripts = true;
    bool includeAsciiPrintable = true;
    bool excludeTmpExamples = true;

    string outputPath = DefaultOutputPath;
    string collectedCharacters = string.Empty;
    string statusMessage = string.Empty;
    Vector2 sourceScroll;
    Vector2 previewScroll;

    TmpUnicodeCollector.Result lastResult;

    [MenuItem("Tools/TextMesh Pro/Collect Project Unicode")]
    static void OpenWindow()
    {
        TmpUnicodeCollectorWindow window = GetWindow<TmpUnicodeCollectorWindow>(
            false,
            "TMP Unicode Collector",
            true);

        window.minSize = new Vector2(520f, 420f);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("TMP 아틀라스용 프로젝트 유니코드 수집", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "프로젝트에서 사용 중인 문자열을 모아 Font Asset Creator의 Character Sequence에 넣을 수 있는 텍스트를 만듭니다.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("스캔 대상", EditorStyles.boldLabel);
        scanScenes = EditorGUILayout.ToggleLeft("씬 (.unity)", scanScenes);
        scanPrefabs = EditorGUILayout.ToggleLeft("프리팹", scanPrefabs);
        scanScriptableObjects = EditorGUILayout.ToggleLeft("ScriptableObject / .asset", scanScriptableObjects);
        scanCSharpScripts = EditorGUILayout.ToggleLeft("C# 문자열 리터럴", scanCSharpScripts);
        includeAsciiPrintable = EditorGUILayout.ToggleLeft("ASCII 출력 가능 문자(32~126) 항상 포함", includeAsciiPrintable);
        excludeTmpExamples = EditorGUILayout.ToggleLeft("TextMesh Pro Examples 제외", excludeTmpExamples);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("출력", EditorStyles.boldLabel);
        outputPath = EditorGUILayout.TextField("저장 경로", outputPath);

        EditorGUILayout.BeginHorizontal();

        try
        {
            if (GUILayout.Button("수집", GUILayout.Height(28f)))
                EditorApplication.delayCall += CollectCharacters;

            GUI.enabled = !string.IsNullOrEmpty(collectedCharacters);

            if (GUILayout.Button("클립보드 복사", GUILayout.Height(28f)))
            {
                EditorGUIUtility.systemCopyBuffer = collectedCharacters;
                statusMessage = "클립보드에 복사했습니다.";
            }

            if (GUILayout.Button("파일 저장", GUILayout.Height(28f)))
                SaveToFile();
        }
        finally
        {
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        if (!string.IsNullOrEmpty(statusMessage))
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        if (lastResult != null)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                $"수집 문자 수: {lastResult.characterCount}    출처: {lastResult.sourceCount}개",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);

        previewScroll = EditorGUILayout.BeginScrollView(
            previewScroll,
            GUILayout.MinHeight(120f));

        EditorGUILayout.TextArea(
            collectedCharacters,
            GUILayout.ExpandHeight(true));

        EditorGUILayout.EndScrollView();

        if (lastResult != null && lastResult.sources.Count > 0)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("출처 목록", EditorStyles.boldLabel);

            sourceScroll = EditorGUILayout.BeginScrollView(
                sourceScroll,
                GUILayout.MinHeight(120f));

            foreach (string source in lastResult.sources)
                EditorGUILayout.LabelField(source, EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }
    }

    void CollectCharacters()
    {
        try
        {
            TmpUnicodeCollector.Settings settings = new TmpUnicodeCollector.Settings
            {
                scanScenes = scanScenes,
                scanPrefabs = scanPrefabs,
                scanScriptableObjects = scanScriptableObjects,
                scanCSharpScripts = scanCSharpScripts,
                includeAsciiPrintable = includeAsciiPrintable,
                excludeTmpExamples = excludeTmpExamples,
            };

            lastResult = TmpUnicodeCollector.Collect(settings);
            collectedCharacters = lastResult.characters;
            statusMessage =
                $"수집 완료: {lastResult.characterCount}자, 출처 {lastResult.sourceCount}개";
        }
        catch (Exception exception)
        {
            statusMessage = $"수집 실패: {exception.Message}";
            Debug.LogException(exception);
        }

        Repaint();
    }

    void SaveToFile()
    {
        if (string.IsNullOrEmpty(collectedCharacters))
        {
            statusMessage = "먼저 수집을 실행해 주세요.";
            return;
        }

        string projectRelativePath = outputPath.Replace('\\', '/').Trim();
        string fullPath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath));

        string directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, collectedCharacters, System.Text.Encoding.UTF8);
        AssetDatabase.Refresh();

        statusMessage = $"저장 완료: {projectRelativePath}";
    }
}
