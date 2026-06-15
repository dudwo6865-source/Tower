using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[CustomEditor(typeof(Unit))]
[CanEditMultipleObjects]
public class UnitEditor : Editor
{
    private const string RawComponentsKey = "UnitEditor.showRawComponents";

    private bool showSelection = true;
    private bool showHealth = true;
    private bool showAttacker = true;
    private bool showAI = true;
    private bool showNavAgent = true;
    private bool showMovement = true;
    private bool showHealthBar = true;
    private bool showRawComponents = false;

    void OnEnable()
    {
        showRawComponents = SessionState.GetBool(RawComponentsKey, false);
        ApplyHideFlags(!showRawComponents);
    }

    void RebuildInspector()
    {
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("data"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawSetupButtons();
        EditorGUILayout.Space();

        bool dataDriven = AnyDataAssigned();

        if (dataDriven)
        {
            EditorGUILayout.HelpBox(
                "UnitData 에셋이 할당되어 있어 게임 시작 시 해당 값이 각 컴포넌트에 적용됩니다. 수치는 UnitData 에셋에서 편집하세요. (아래 값은 참고용)",
                MessageType.Info);

            EditorGUILayout.Space();
        }

        using (new EditorGUI.DisabledScope(dataDriven))
        {
            DrawComponentSection<SelectableEntity>("선택 (Selection)", ref showSelection);
            DrawComponentSection<EntityHealth>("체력 (Health)", ref showHealth);
            DrawComponentSection<UnitAttacker>("공격 (Attacker)", ref showAttacker);
            DrawComponentSection<UnitCombatAI>("전투 AI (Combat AI)", ref showAI);
            DrawNavAgentSection("이동 속도 (NavMesh Agent)", ref showNavAgent);
            DrawComponentSection<UnitMovement>("이동 입력 (Movement)", ref showMovement);
            DrawComponentSection<WorldHealthBar>("체력바 (Health Bar)", ref showHealthBar);
        }

        DrawRawToggle();
    }

    bool AnyDataAssigned()
    {
        foreach (Object targetObject in targets)
        {
            if (((Unit)targetObject).data != null)
                return true;
        }

        return false;
    }

    void DrawSetupButtons()
    {
        EditorGUILayout.LabelField("컴포넌트 구성", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("전투 유닛 프리셋"))
            RunSetup(true, true, true, "Setup Combat Unit");

        if (GUILayout.Button("건물/비전투 프리셋"))
            RunSetup(false, false, false, "Setup Building");

        EditorGUILayout.EndHorizontal();

        bool hasAttacker = AllHave<UnitAttacker>();
        bool hasCombatAI = AllHave<UnitCombatAI>();
        bool hasMovement = AllHave<UnitMovement>();

        EditorGUI.BeginChangeCheck();

        bool wantsCombatAI = EditorGUILayout.ToggleLeft(
            new GUIContent("전투 AI (UnitCombatAI + NavMeshAgent)", "자동 교전/추격 AI입니다."),
            hasCombatAI);

        bool wantsMovement = EditorGUILayout.ToggleLeft(
            new GUIContent("수동 이동 (UnitMovement)", "우클릭으로 이동 명령을 내릴 수 있습니다."),
            hasMovement);

        bool wantsAttacker = EditorGUILayout.ToggleLeft(
            new GUIContent("공격 (UnitAttacker)", "공격 능력입니다. 전투 AI를 켜면 자동 포함됩니다."),
            hasAttacker || wantsCombatAI);

        if (EditorGUI.EndChangeCheck())
            RunSetup(
                wantsAttacker || wantsCombatAI,
                wantsCombatAI,
                wantsMovement,
                "Edit Unit Components");

        DrawAdvanceToggle();
    }

    void DrawAdvanceToggle()
    {
        List<UnitCombatAI> ais = CollectCombatAIs();

        if (ais.Count == 0)
            return;

        bool reference = ais[0].advanceToEnemyBuildings;
        bool mixed = false;

        for (int i = 1; i < ais.Count; i++)
        {
            if (ais[i].advanceToEnemyBuildings != reference)
            {
                mixed = true;
                break;
            }
        }

        using (new EditorGUI.DisabledScope(AnyDataAssigned()))
        {
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();

            bool advance = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "전투 대상 없을 때 적 건물로 진군",
                    "교전 대상이 없으면 가장 가까운 적 건물로 이동합니다. (UnitCombatAI 필요)"),
                reference);

            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (changed)
            {
                Undo.RecordObjects(ais.ToArray(), "Toggle Advance To Enemy Buildings");

                foreach (UnitCombatAI ai in ais)
                {
                    ai.advanceToEnemyBuildings = advance;
                    EditorUtility.SetDirty(ai);
                }
            }
        }

        if (AnyDataAssigned())
            EditorGUILayout.HelpBox(
                "UnitData가 할당되어 있어 이 값은 게임 시작 시 UnitData의 설정으로 덮어써집니다. UnitData 에셋에서 변경하세요.",
                MessageType.None);
    }

    List<UnitCombatAI> CollectCombatAIs()
    {
        List<UnitCombatAI> result = new List<UnitCombatAI>();

        foreach (Object targetObject in targets)
        {
            UnitCombatAI ai = ((Unit)targetObject).GetComponent<UnitCombatAI>();

            if (ai != null)
                result.Add(ai);
        }

        return result;
    }

    bool AllHave<T>() where T : Component
    {
        foreach (Object targetObject in targets)
        {
            if (((Unit)targetObject).GetComponent<T>() == null)
                return false;
        }

        return true;
    }

    void RunSetup(bool attacker, bool combatAI, bool movement, string undoName)
    {
        foreach (Object targetObject in targets)
        {
            Unit unit = (Unit)targetObject;
            Undo.RegisterCompleteObjectUndo(unit.gameObject, undoName);
            unit.EnsureComponents(attacker, combatAI, movement);
        }

        ApplyHideFlags(!showRawComponents);
        RebuildInspector();
    }

    void DrawComponentSection<T>(string title, ref bool foldout) where T : Component
    {
        List<Object> components = CollectComponents<T>();

        if (components.Count == 0)
            return;

        foldout = EditorGUILayout.Foldout(foldout, title, true);

        if (!foldout)
            return;

        EditorGUI.indentLevel++;

        SerializedObject so = new SerializedObject(components.ToArray());
        so.Update();

        SerializedProperty property = so.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.name == "m_Script")
                continue;

            EditorGUILayout.PropertyField(property, true);
        }

        so.ApplyModifiedProperties();

        EditorGUI.indentLevel--;
    }

    void DrawNavAgentSection(string title, ref bool foldout)
    {
        List<NavMeshAgent> agents = CollectNavAgents();

        if (agents.Count == 0)
            return;

        foldout = EditorGUILayout.Foldout(foldout, title, true);

        if (!foldout)
            return;

        EditorGUI.indentLevel++;

        NavMeshAgent first = agents[0];

        float speed = DrawAgentFloat(
            "이동 속도", "유닛의 이동 속도입니다.", agents, a => a.speed,
            out bool speedChanged);

        float angularSpeed = DrawAgentFloat(
            "회전 속도", "회전 속도(도/초)입니다.", agents, a => a.angularSpeed,
            out bool angularChanged);

        float acceleration = DrawAgentFloat(
            "가속도", "목표 속도까지 가속하는 정도입니다.", agents, a => a.acceleration,
            out bool accelChanged);

        if (speedChanged || angularChanged || accelChanged)
        {
            Undo.RecordObjects(agents.ToArray(), "Edit NavMeshAgent Movement");

            foreach (NavMeshAgent agent in agents)
            {
                if (speedChanged)
                    agent.speed = speed;

                if (angularChanged)
                    agent.angularSpeed = angularSpeed;

                if (accelChanged)
                    agent.acceleration = acceleration;

                EditorUtility.SetDirty(agent);
            }
        }

        EditorGUI.indentLevel--;
    }

    float DrawAgentFloat(
        string label,
        string tooltip,
        List<NavMeshAgent> agents,
        System.Func<NavMeshAgent, float> getter,
        out bool changed)
    {
        float reference = getter(agents[0]);
        bool mixed = false;

        for (int i = 1; i < agents.Count; i++)
        {
            if (!Mathf.Approximately(getter(agents[i]), reference))
            {
                mixed = true;
                break;
            }
        }

        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();

        float value = EditorGUILayout.FloatField(
            new GUIContent(label, tooltip),
            reference);

        changed = EditorGUI.EndChangeCheck();
        EditorGUI.showMixedValue = false;

        return value;
    }

    List<Object> CollectComponents<T>() where T : Component
    {
        List<Object> result = new List<Object>();

        foreach (Object targetObject in targets)
        {
            T component = ((Unit)targetObject).GetComponent<T>();

            if (component != null)
                result.Add(component);
        }

        return result;
    }

    List<NavMeshAgent> CollectNavAgents()
    {
        List<NavMeshAgent> result = new List<NavMeshAgent>();

        foreach (Object targetObject in targets)
        {
            NavMeshAgent agent = ((Unit)targetObject).GetComponent<NavMeshAgent>();

            if (agent != null)
                result.Add(agent);
        }

        return result;
    }

    void DrawRawToggle()
    {
        EditorGUILayout.Space();

        bool newValue = EditorGUILayout.ToggleLeft(
            "개별 컴포넌트를 인스펙터에 그대로 표시 (고급)",
            showRawComponents);

        if (newValue != showRawComponents)
        {
            showRawComponents = newValue;
            SessionState.SetBool(RawComponentsKey, newValue);
            ApplyHideFlags(!showRawComponents);
            RebuildInspector();
        }
    }

    void ApplyHideFlags(bool hide)
    {
        HideFlags flag = hide ? HideFlags.HideInInspector : HideFlags.None;

        SetFlag<SelectableEntity>(flag);
        SetFlag<EntityHealth>(flag);
        SetFlag<WorldHealthBar>(flag);
        SetFlag<UnitAttacker>(flag);
        SetFlag<UnitCombatAI>(flag);
        SetFlag<UnitMovement>(flag);
    }

    void SetFlag<T>(HideFlags flag) where T : Component
    {
        foreach (Object targetObject in targets)
        {
            T component = ((Unit)targetObject).GetComponent<T>();

            if (component != null && component.hideFlags != flag)
            {
                component.hideFlags = flag;
                EditorUtility.SetDirty(component);
            }
        }
    }
}
