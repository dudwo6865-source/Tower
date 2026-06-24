using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LODGroup))]
[CanEditMultipleObjects]
public class LODGroupKoreanEditor : Editor
{
    static bool showUsageGuide = true;

    SerializedProperty enabledProp;
    SerializedProperty localReferencePointProp;
    SerializedProperty sizeProp;
    SerializedProperty fadeModeProp;
    SerializedProperty animateCrossFadingProp;
    SerializedProperty lodsProp;

    void OnEnable()
    {
        enabledProp = serializedObject.FindProperty("m_Enabled");
        localReferencePointProp = serializedObject.FindProperty("m_LocalReferencePoint");
        sizeProp = serializedObject.FindProperty("m_Size");
        fadeModeProp = serializedObject.FindProperty("m_FadeMode");
        animateCrossFadingProp = serializedObject.FindProperty("m_AnimateCrossFading");
        lodsProp = serializedObject.FindProperty("m_LODs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawUsageGuide();
        EditorGUILayout.Space();

        if (enabledProp != null)
        {
            EditorGUILayout.PropertyField(
                enabledProp,
                new GUIContent("활성화", "LOD 전환을 사용할지 여부입니다."));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("경계 설정", EditorStyles.boldLabel);

        if (localReferencePointProp != null)
        {
            EditorGUILayout.PropertyField(
                localReferencePointProp,
                new GUIContent(
                    "로컬 기준점",
                    "LOD 거리 계산의 중심점입니다. 보통 메쉬 중앙에 두며, Recalculate Bounds로 자동 갱신할 수 있습니다."));
        }

        if (sizeProp != null)
        {
            EditorGUILayout.PropertyField(
                sizeProp,
                new GUIContent(
                    "LOD 경계 크기",
                    "기준점을 중심으로 한 구(Bounding Sphere) 반지름입니다. 카메라 거리·화면 비율 계산에 사용됩니다."));
        }

        using (new EditorGUI.DisabledScope(serializedObject.isEditingMultipleObjects))
        {
            if (GUILayout.Button(new GUIContent(
                    "경계 다시 계산",
                    "자식 Renderer를 기준으로 로컬 기준점과 LOD 경계 크기를 자동으로 맞춥니다.")))
            {
                foreach (Object targetObject in targets)
                {
                    LODGroup lodGroup = (LODGroup)targetObject;
                    Undo.RecordObject(lodGroup, "Recalculate LOD Bounds");
                    lodGroup.RecalculateBounds();
                    EditorUtility.SetDirty(lodGroup);
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("전환 설정", EditorStyles.boldLabel);

        if (fadeModeProp != null)
        {
            EditorGUILayout.PropertyField(
                fadeModeProp,
                new GUIContent(
                    "페이드 모드",
                    "None: 즉시 전환. CrossFade: LOD 메쉬를 겹쳐 부드럽게 전환. SpeedTree: SpeedTree 전용."));
        }

        if (animateCrossFadingProp != null)
        {
            EditorGUILayout.PropertyField(
                animateCrossFadingProp,
                new GUIContent(
                    "크로스페이드 애니메이션",
                    "CrossFade 모드에서 LOD 전환 시 페이드를 애니메이션합니다."));
        }

        EditorGUILayout.Space();
        DrawLodLevels();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawUsageGuide()
    {
        showUsageGuide = EditorGUILayout.Foldout(showUsageGuide, "LOD Group 사용법 (한글)", true);

        if (!showUsageGuide)
            return;

        EditorGUILayout.HelpBox(
            "카메라와의 거리(화면에서 차지하는 크기)에 따라 자식 메쉬를 단계별로 바꿔 그리기 부하를 줄입니다.\n\n" +
            "1) 루트에 LOD Group 추가\n" +
            "2) LOD0(가까움) → LOD1 → LOD2(멀음) 순으로 자식 Renderer 배치\n" +
            "3) 각 LOD 슬롯에 해당 MeshRenderer/SkinnedMeshRenderer 연결\n" +
            "4) Screen Relative Height 값으로 전환 거리 조정\n" +
            "5) Scene 뷰에서 선택 시 LOD 미리보기 슬라이더로 확인",
            MessageType.Info);
    }

    void DrawLodLevels()
    {
        if (lodsProp == null)
        {
            EditorGUILayout.HelpBox("LOD 단계 데이터(m_LODs)를 찾을 수 없습니다.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("LOD 단계", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "LOD 0 = 최고 품질(가까울 때). 번호가 커질수록 단순한 메쉬.\n" +
            "Screen Relative Height: 화면 높이 대비 오브젝트 크기 비율(0~1). " +
            "이 값보다 작아지면 다음 LOD로 전환됩니다.",
            MessageType.None);

        for (int i = 0; i < lodsProp.arraySize; i++)
        {
            SerializedProperty lodElement = lodsProp.GetArrayElementAtIndex(i);
            SerializedProperty screenHeight = lodElement.FindPropertyRelative("screenRelativeHeight");
            SerializedProperty fadeWidth = lodElement.FindPropertyRelative("fadeTransitionWidth");
            SerializedProperty renderers = lodElement.FindPropertyRelative("renderers");

            string title = i == lodsProp.arraySize - 1
                ? $"LOD {i} (최저 / Culled 근처)"
                : $"LOD {i}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            if (screenHeight != null)
            {
                EditorGUILayout.PropertyField(
                    screenHeight,
                    new GUIContent(
                        "화면 상대 높이",
                        "카메라 화면에서 이 오브젝트가 차지하는 높이 비율(0~1). " +
                        "값이 클수록 가까이 있을 때만 이 LOD가 표시됩니다."));
            }

            if (fadeWidth != null)
            {
                EditorGUILayout.PropertyField(
                    fadeWidth,
                    new GUIContent(
                        "전환 페이드 폭",
                        "CrossFade 사용 시 LOD 전환이 일어나는 구간의 너비(0~1)입니다."));
            }

            if (renderers != null)
            {
                EditorGUILayout.PropertyField(
                    renderers,
                    new GUIContent(
                        "렌더러",
                        "이 LOD 단계에서 켜질 MeshRenderer 또는 SkinnedMeshRenderer 목록입니다."),
                    true);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }
    }
}
