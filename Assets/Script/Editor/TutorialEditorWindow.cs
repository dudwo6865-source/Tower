using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에셋·스테이지·씬 연결을 편집하며 게임 UI의 외형은 변경하지 않는다.
public class TutorialEditorWindow : EditorWindow
{
    [SerializeField, Label("선택 스테이지")] MapConfig stage;
    [SerializeField, Label("선택 안내")] TutorialSequence sequence;
    [SerializeField, Label("씬 진행 컴포넌트")] TutorialController controller;
    [SerializeField, Label("씬 로더")] MapLoader loader;
    [SerializeField, Label("구역 이벤트")] TutorialEventTrigger trigger;
    Vector2 stageScroll, contentScroll, sceneScroll;
    int previewPage;
    string search = "";
    TutorialSequence[] assets = Array.Empty<TutorialSequence>();
    MapConfig[] stages = Array.Empty<MapConfig>();

    [MenuItem("Tools/튜토리얼/튜토리얼 에디터")]
    public static void Open() { Open(null); }

    public static void Open(MapConfig config)
    {
        var window = GetWindow<TutorialEditorWindow>("튜토리얼 에디터");
        window.minSize = new Vector2(1040, 620);
        if (config != null) window.stage = config;
        window.Show();
    }

    void OnEnable()
    {
        Refresh();
        Undo.undoRedoPerformed += Repaint;
        EditorApplication.projectChanged += Refresh;
        EditorApplication.hierarchyChanged += Repaint;
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed -= Repaint;
        EditorApplication.projectChanged -= Refresh;
        EditorApplication.hierarchyChanged -= Repaint;
    }

    void Refresh()
    {
        assets = FindAssets<TutorialSequence>();
        stages = FindAssets<MapConfig>();
        Repaint();
    }

    static T[] FindAssets<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name)
        .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
        .Where(a => a != null).OrderBy(a => a.name).ToArray();

    static T[] SceneComponents<T>() where T : Component => Resources.FindObjectsOfTypeAll<T>()
        .Where(c => !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded &&
            (c.hideFlags & HideFlags.DontSave) == 0).ToArray();

    void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("목록 새로고침", EditorStyles.toolbarButton)) Refresh();
            if (GUILayout.Button("씬 연결 찾기", EditorStyles.toolbarButton)) FindSceneLinks();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("선택 에셋 저장", EditorStyles.toolbarButton))
                {
                    if (stage != null) AssetDatabase.SaveAssetIfDirty(stage);
                    if (sequence != null) AssetDatabase.SaveAssetIfDirty(sequence);
                }
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("플레이 중에는 읽기 전용입니다. 설정 변경은 플레이를 종료한 뒤 진행하세요.", MessageType.Info);
        float oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 125;
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            {
                stageScroll = EditorGUILayout.BeginScrollView(stageScroll);
                DrawStage();
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                contentScroll = EditorGUILayout.BeginScrollView(contentScroll);
                DrawSequence();
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(310)))
            {
                sceneScroll = EditorGUILayout.BeginScrollView(sceneScroll);
                DrawScene();
                EditorGUILayout.EndScrollView();
            }
        }
        EditorGUIUtility.labelWidth = oldLabelWidth;
    }

    void DrawStage()
    {
        EditorGUILayout.LabelField("1. 스테이지와 발생 이벤트", EditorStyles.boldLabel);
        stage = (MapConfig)EditorGUILayout.ObjectField("스테이지", stage, typeof(MapConfig), false);
        if (GUILayout.Button("스테이지 목록에서 선택"))
        {
            var menu = new GenericMenu();
            foreach (MapConfig item in stages)
            {
                MapConfig selected = item;
                menu.AddItem(new GUIContent(AssetDatabase.GetAssetPath(item)), stage == item, () => { stage = selected; Repaint(); });
            }
            if (stages.Length == 0) menu.AddDisabledItem(new GUIContent("스테이지 에셋이 없습니다"));
            menu.ShowAsContext();
        }
        if (stage == null)
        {
            EditorGUILayout.HelpBox("스테이지를 선택하세요. MapLoader가 없는 씬의 기본 이벤트는 오른쪽에서 편집합니다.", MessageType.Info);
            return;
        }
        var so = new SerializedObject(stage);
        so.Update();
        DrawBindings(so.FindProperty("tutorials"));
        so.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("동일 이벤트는 목록 순서대로 표시됩니다. 웨이브 0은 모든 웨이브입니다.", MessageType.Info);
    }

    void DrawBindings(SerializedProperty list)
    {
        int remove = -1, moveFrom = -1, moveTo = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            var entry = list.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawOrder(i, list.arraySize, "이벤트", ref remove, ref moveFrom, ref moveTo);
                Field(entry.FindPropertyRelative("trigger"), "발생 이벤트");
                var kind = (TutorialEvent)entry.FindPropertyRelative("trigger").enumValueIndex;
                if (kind == TutorialEvent.Custom) Field(entry.FindPropertyRelative("eventId"), "이벤트 이름");
                if (kind == TutorialEvent.DayStarted || kind == TutorialEvent.NightStarted)
                    Field(entry.FindPropertyRelative("wave"), "웨이브 번호");
                if (kind == TutorialEvent.EggSpawnCountChanged)
                    Field(entry.FindPropertyRelative("eggSpawnCount"), "변경 후 알 리젠 수");
                Field(entry.FindPropertyRelative("sequence"), "표시할 안내");
                Field(entry.FindPropertyRelative("prerequisite"), "선행 안내");
                var linked = entry.FindPropertyRelative("sequence").objectReferenceValue as TutorialSequence;
                if (linked == null) EditorGUILayout.HelpBox("표시할 안내를 연결하세요.", MessageType.Warning);
                else if (GUILayout.Button("이 안내 편집")) { sequence = linked; previewPage = 0; }
                if (linked != null && entry.FindPropertyRelative("prerequisite").objectReferenceValue == linked)
                    EditorGUILayout.HelpBox("자기 자신을 선행 안내로 지정하면 시작할 수 없습니다.", MessageType.Warning);
                if (kind == TutorialEvent.Custom && string.IsNullOrWhiteSpace(entry.FindPropertyRelative("eventId").stringValue))
                    EditorGUILayout.HelpBox("사용자 지정 이벤트 이름을 입력하세요.", MessageType.Warning);
            }
        }
        ApplyOrder(list, remove, moveFrom, moveTo);
        using (new EditorGUI.DisabledScope(sequence == null))
            if (GUILayout.Button("선택 안내를 이벤트로 추가"))
            {
                int index = list.arraySize++;
                var entry = list.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("trigger").enumValueIndex = (int)TutorialEvent.StageStarted;
                entry.FindPropertyRelative("eventId").stringValue = "";
                entry.FindPropertyRelative("wave").intValue = 0;
                entry.FindPropertyRelative("eggSpawnCount").intValue = 0;
                entry.FindPropertyRelative("prerequisite").objectReferenceValue = null;
                entry.FindPropertyRelative("sequence").objectReferenceValue = sequence;
            }
    }

    void DrawSequence()
    {
        EditorGUILayout.LabelField("2. 안내 에셋과 페이지", EditorStyles.boldLabel);
        sequence = (TutorialSequence)EditorGUILayout.ObjectField("안내 에셋", sequence, typeof(TutorialSequence), false);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("새 안내 만들기")) CreateSequence();
            if (GUILayout.Button("에셋 찾기")) EditorGUIUtility.PingObject(sequence);
        }
        search = EditorGUILayout.TextField("에셋 검색", search);
        if (GUILayout.Button("안내 목록에서 선택"))
        {
            var menu = new GenericMenu();
            var matches = assets.Where(a => a.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            foreach (var item in matches)
            {
                var selected = item;
                menu.AddItem(new GUIContent(AssetDatabase.GetAssetPath(item)), sequence == item,
                    () => { sequence = selected; previewPage = 0; Repaint(); });
            }
            if (matches.Length == 0) menu.AddDisabledItem(new GUIContent("검색 결과가 없습니다"));
            menu.ShowAsContext();
        }
        if (sequence == null) return;
        var so = new SerializedObject(sequence);
        so.Update();
        Field(so.FindProperty("pauseGame"), "게임 시간 정지");
        Field(so.FindProperty("blockGameplay"), "게임 조작 차단");
        Field(so.FindProperty("oncePerStage"), "한 번만 표시");
        Field(so.FindProperty("waitForEvent"), "실습 이벤트 대기");
        if (so.FindProperty("waitForEvent").boolValue)
        {
            Field(so.FindProperty("completionTrigger"), "완료 이벤트");
            if (so.FindProperty("completionTrigger").enumValueIndex == (int)TutorialEvent.Custom)
                Field(so.FindProperty("completionEvent"), "완료 이벤트 이름");
            if (so.FindProperty("pauseGame").boolValue || so.FindProperty("blockGameplay").boolValue)
                EditorGUILayout.HelpBox("실습에서는 시간 정지와 조작 차단을 모두 끄세요.", MessageType.Warning);
        }
        var pages = so.FindProperty("pages");
        int remove = -1, moveFrom = -1, moveTo = -1;
        for (int i = 0; i < pages.arraySize; i++)
        {
            var page = pages.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawOrder(i, pages.arraySize, "페이지", ref remove, ref moveFrom, ref moveTo);
                EditorGUILayout.LabelField("본문");
                var body = page.FindPropertyRelative("text");
                body.stringValue = EditorGUILayout.TextArea(body.stringValue, GUILayout.MinHeight(80));
                Field(page.FindPropertyRelative("image"), "이미지");
                Field(page.FindPropertyRelative("imageSlot"), "이미지 영역 번호");
            }
        }
        ApplyOrder(pages, remove, moveFrom, moveTo);
        if (pages.arraySize == 0) EditorGUILayout.HelpBox("페이지를 최소 1개 추가하세요.", MessageType.Warning);
        if (GUILayout.Button("페이지 추가"))
        {
            var page = pages.GetArrayElementAtIndex(pages.arraySize++);
            page.FindPropertyRelative("text").stringValue = "";
            page.FindPropertyRelative("image").objectReferenceValue = null;
            page.FindPropertyRelative("imageSlot").intValue = 0;
        }
        so.ApplyModifiedProperties();
        DrawPreview();
    }

    void DrawPreview()
    {
        if (sequence.pages == null || sequence.pages.Length == 0) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("내용 미리보기", EditorStyles.boldLabel);
        previewPage = Mathf.Clamp(EditorGUILayout.IntSlider("페이지", previewPage + 1, 1, sequence.pages.Length) - 1, 0, sequence.pages.Length - 1);
        var page = sequence.pages[previewPage];
        if (page == null) return;
        if (page.image != null)
        {
            Texture texture = AssetPreview.GetAssetPreview(page.image);
            if (texture != null)
                GUI.DrawTexture(GUILayoutUtility.GetRect(100, 180, GUILayout.ExpandWidth(true)), texture, ScaleMode.ScaleToFit);
            else if (AssetPreview.IsLoadingAssetPreview(page.image.GetInstanceID())) Repaint();
        }
        EditorGUILayout.HelpBox(page.text ?? "", MessageType.None);
        EditorGUILayout.LabelField("실제 외형과 배치는 씬 UI 설정을 따릅니다.", EditorStyles.wordWrappedMiniLabel);
    }

    void DrawScene()
    {
        EditorGUILayout.LabelField("3. 씬 컴포넌트 연결", EditorStyles.boldLabel);
        controller = (TutorialController)EditorGUILayout.ObjectField("TutorialController", controller, typeof(TutorialController), true);
        if (controller != null && EditorUtility.IsPersistent(controller))
        {
            EditorGUILayout.HelpBox("프로젝트 프리팹 대신 열린 씬의 컴포넌트를 선택하세요.", MessageType.Warning);
            return;
        }
        if (SceneComponents<TutorialController>().Length > 1)
            EditorGUILayout.HelpBox("열린 씬에 TutorialController가 여러 개 있습니다. 실행할 씬에는 하나만 활성화하세요.", MessageType.Warning);
        if (controller == null)
        {
            EditorGUILayout.HelpBox("씬 연결 찾기를 누르거나 Hierarchy에서 컴포넌트를 끌어오세요.", MessageType.Info);
            var go = Selection.activeGameObject;
            using (new EditorGUI.DisabledScope(go == null || EditorUtility.IsPersistent(go)))
                if (GUILayout.Button("선택 오브젝트에 관리 컴포넌트 추가"))
                {
                    controller = go.GetComponent<TutorialController>() ?? Undo.AddComponent<TutorialController>(go);
                    var panel = go.GetComponent<TutorialPanel>() ?? Undo.AddComponent<TutorialPanel>(go);
                    var setup = new SerializedObject(controller);
                    setup.FindProperty("panel").objectReferenceValue = panel;
                    setup.ApplyModifiedProperties();
                }
            EditorGUILayout.HelpBox("관리 컴포넌트만 추가합니다. UI는 에디터에서 만든 오브젝트를 연결하세요.", MessageType.None);
        }
        else
        {
            var so = new SerializedObject(controller);
            so.Update();
            Field(so.FindProperty("panel"), "표시 패널");
            Field(so.FindProperty("clickGuardSeconds"), "클릭 보호 시간(초)");
            so.ApplyModifiedProperties();
            var panel = controller.panel;
            if (panel != null && !EditorUtility.IsPersistent(panel))
            {
                var panelSo = new SerializedObject(panel);
                panelSo.Update();
                Field(panelSo.FindProperty("panelRoot"), "패널 루트");
                Field(panelSo.FindProperty("bodyText"), "본문 텍스트");
                Field(panelSo.FindProperty("nextButton"), "다음 버튼");
                Field(panelSo.FindProperty("inputBlocker"), "입력 차단 배경");
                Field(panelSo.FindProperty("imageSlots"), "이미지 영역");
                panelSo.ApplyModifiedProperties();
                if (sequence != null)
                {
                    string error = panel.GetValidationError(sequence);
                    if (!controller.isActiveAndEnabled) error = "TutorialController 또는 부모 오브젝트를 활성화하세요.";
                    if (panel.panelRoot != null && controller.transform.IsChildOf(panel.panelRoot.transform))
                        error = "TutorialController를 패널 루트 밖으로 옮기세요.";
                    EditorGUILayout.HelpBox(error ?? "선택 안내의 필수 연결이 정상입니다.", error == null ? MessageType.Info : MessageType.Warning);
                }
            }
            else EditorGUILayout.HelpBox("열린 씬의 TutorialPanel을 표시 패널로 연결하세요.", MessageType.Warning);
            if (SceneComponents<MapLoader>().Length == 0)
            {
                EditorGUILayout.LabelField("씬 기본 이벤트", EditorStyles.boldLabel);
                so.Update();
                DrawBindings(so.FindProperty("defaultBindings"));
                so.ApplyModifiedProperties();
            }
        }
        EditorGUILayout.Space();
        loader = (MapLoader)EditorGUILayout.ObjectField("MapLoader", loader, typeof(MapLoader), true);
        if (loader != null && !EditorUtility.IsPersistent(loader))
        {
            EditorGUILayout.ObjectField("현재 기본 스테이지", loader.defaultConfig, typeof(MapConfig), false);
            using (new EditorGUI.DisabledScope(stage == null))
                if (GUILayout.Button("선택 스테이지를 기본 스테이지로 연결"))
                {
                    var so = new SerializedObject(loader);
                    so.FindProperty("defaultConfig").objectReferenceValue = stage;
                    so.ApplyModifiedProperties();
                }
            if (stage != null && loader.defaultConfig != stage)
                EditorGUILayout.HelpBox("편집 중인 스테이지와 씬 기본 스테이지가 다릅니다.", MessageType.Warning);
            EditorGUILayout.HelpBox("미션 선택 화면에서 전달한 스테이지가 기본 스테이지보다 우선합니다.", MessageType.None);
        }
        trigger = (TutorialEventTrigger)EditorGUILayout.ObjectField("구역 이벤트", trigger, typeof(TutorialEventTrigger), true);
        if (trigger != null && !EditorUtility.IsPersistent(trigger))
        {
            var so = new SerializedObject(trigger);
            so.Update();
            Field(so.FindProperty("eventId"), "이벤트 이름");
            Field(so.FindProperty("onUnitEnter"), "아군 진입 시 실행");
            so.ApplyModifiedProperties();
        }
        if (GUILayout.Button("연결 컴포넌트의 씬 저장"))
        {
            var scenes = new Component[] { controller, controller != null ? controller.panel : null, loader, trigger }
                .Where(c => c != null && !EditorUtility.IsPersistent(c)).Select(c => c.gameObject.scene).Distinct();
            foreach (var scene in scenes)
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.SaveScene(scene);
        }
        EditorGUILayout.LabelField("씬 저장은 해당 씬의 다른 미저장 변경도 함께 저장합니다.", EditorStyles.wordWrappedMiniLabel);
    }

    void FindSceneLinks()
    {
        var controllers = SceneComponents<TutorialController>();
        var loaders = SceneComponents<MapLoader>();
        if (controllers.Length == 1) controller = controllers[0];
        if (loaders.Length == 1) loader = loaders[0];
        if (stage == null && loader != null) stage = loader.defaultConfig;
        Repaint();
    }

    void CreateSequence()
    {
        string path = EditorUtility.SaveFilePanelInProject("튜토리얼 안내 만들기", "TutorialSequence", "asset", "저장할 위치를 선택하세요.");
        if (string.IsNullOrEmpty(path)) return;
        var created = CreateInstance<TutorialSequence>();
        created.pages = new[] { new TutorialPage { text = "함장님, 임무를 안내하겠습니다." } };
        AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
        Undo.RegisterCreatedObjectUndo(created, "튜토리얼 안내 만들기");
        sequence = created;
        previewPage = 0;
        Refresh();
    }

    static void Field(SerializedProperty property, string label) =>
        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);

    static void DrawOrder(int index, int count, string title, ref int remove, ref int from, ref int to)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"{title} {index + 1}", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(index == 0))
                if (GUILayout.Button("↑", GUILayout.Width(25))) { from = index; to = index - 1; }
            using (new EditorGUI.DisabledScope(index == count - 1))
                if (GUILayout.Button("↓", GUILayout.Width(25))) { from = index; to = index + 1; }
            if (GUILayout.Button("삭제", GUILayout.Width(38))) remove = index;
        }
    }

    static void ApplyOrder(SerializedProperty list, int remove, int from, int to)
    {
        // 목록을 그린 뒤 변경하여 반복 중 직렬화 요소 참조가 깨지지 않도록 한다.
        if (remove >= 0) list.DeleteArrayElementAtIndex(remove);
        else if (from >= 0) list.MoveArrayElement(from, to);
    }
}
