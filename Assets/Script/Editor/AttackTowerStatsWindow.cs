using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 모든 Attack Tower(공격 타워)의 UnitAttacker 수치를 한 화면에서 표로 보고 수정하는 에디터입니다.
// 수정한 값은 즉시 해당 타워 프리팹 에셋에 저장됩니다.
public class AttackTowerStatsWindow : EditorWindow
{
    class Entry
    {
        public BuildableTowerData data;
        public GameObject prefab;
        public UnitAttacker attacker;
        public SerializedObject serializedAttacker;

        public SerializedProperty attackType;
        public SerializedProperty attackDamage;
        public SerializedProperty attackRange;
        public SerializedProperty attackCooldown;
        public SerializedProperty projectileSpeed;
        public SerializedProperty minAttackRange;
        public SerializedProperty useBallisticArc;
        public SerializedProperty ballisticGravity;
        public SerializedProperty arcHeight;
        public SerializedProperty arcHeightRatio;
        public SerializedProperty minArcHeight;
        public SerializedProperty arcPeakTime;
        public SerializedProperty arcClimbPower;
        public SerializedProperty arcDivePower;
        public SerializedProperty impactOffsetRadius;
        public SerializedProperty lateralWobbleRatio;
        public SerializedProperty lateralWobbleAmount;
        public SerializedProperty splashRadius;
        public SerializedProperty splashMinDamageRatio;
        public SerializedProperty hitEffectBaseRadius;
        public SerializedProperty pierceHitRadius;
    }

    const float NameWidth = 130f;
    const float TypeWidth = 100f;
    const float NumWidth = 62f;
    const float ButtonWidth = 50f;

    readonly List<Entry> entries = new List<Entry>();
    Vector2 scroll;
    string search = "";

    [MenuItem("Tools/타워 공격 스탯 에디터")]
    public static void Open()
    {
        var window = GetWindow<AttackTowerStatsWindow>("타워 스탯");
        window.minSize = new Vector2(500, 300);
        window.LoadEntries();
    }

    void OnEnable()
    {
        LoadEntries();
    }

    void LoadEntries()
    {
        entries.Clear();

        string[] guids = AssetDatabase.FindAssets("t:BuildableTowerData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BuildableTowerData data = AssetDatabase.LoadAssetAtPath<BuildableTowerData>(path);
            if (data == null || data.prefab == null)
                continue;

            // UnitAttacker가 있는 프리팹만 "공격 타워"로 취급합니다.
            UnitAttacker attacker = data.prefab.GetComponent<UnitAttacker>();
            if (attacker == null)
                continue;

            var so = new SerializedObject(attacker);
            entries.Add(new Entry
            {
                data = data,
                prefab = data.prefab,
                attacker = attacker,
                serializedAttacker = so,
                attackType = so.FindProperty("attackType"),
                attackDamage = so.FindProperty("attackDamage"),
                attackRange = so.FindProperty("attackRange"),
                attackCooldown = so.FindProperty("attackCooldown"),
                projectileSpeed = so.FindProperty("projectileSpeed"),
                minAttackRange = so.FindProperty("minAttackRange"),
                useBallisticArc = so.FindProperty("useBallisticArc"),
                ballisticGravity = so.FindProperty("ballisticGravity"),
                arcHeight = so.FindProperty("arcHeight"),
                arcHeightRatio = so.FindProperty("arcHeightRatio"),
                minArcHeight = so.FindProperty("minArcHeight"),
                arcPeakTime = so.FindProperty("arcPeakTime"),
                arcClimbPower = so.FindProperty("arcClimbPower"),
                arcDivePower = so.FindProperty("arcDivePower"),
                impactOffsetRadius = so.FindProperty("impactOffsetRadius"),
                lateralWobbleRatio = so.FindProperty("lateralWobbleRatio"),
                lateralWobbleAmount = so.FindProperty("lateralWobbleAmount"),
                splashRadius = so.FindProperty("splashRadius"),
                splashMinDamageRatio = so.FindProperty("splashMinDamageRatio"),
                hitEffectBaseRadius = so.FindProperty("hitEffectBaseRadius"),
                pierceHitRadius = so.FindProperty("pierceHitRadius"),
            });
        }

        entries.Sort((a, b) => string.Compare(a.data.displayName, b.data.displayName));
    }

    void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(4);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawHeader();

        foreach (Entry entry in entries)
        {
            if (!string.IsNullOrEmpty(search) &&
                entry.data.displayName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            DrawRow(entry);
        }

        if (entries.Count == 0)
            EditorGUILayout.HelpBox("UnitAttacker가 붙은 BuildableTowerData를 찾지 못했습니다.", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70)))
                LoadEntries();

            GUILayout.Space(8);
            GUILayout.Label("검색", GUILayout.Width(30));
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(150));

            GUILayout.FlexibleSpace();
        }
    }

    void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            HeaderLabel("타워", NameWidth);
            HeaderLabel("타입", TypeWidth);
            HeaderLabel("공격력", NumWidth);
            HeaderLabel("사거리", NumWidth);
            HeaderLabel("최소사거리", NumWidth);
            HeaderLabel("쿨다운", NumWidth);
            HeaderLabel("투사체속도", NumWidth);
            HeaderLabel("탄도계산", NumWidth);
            HeaderLabel("중력", NumWidth);
            HeaderLabel("포물선높이", NumWidth);
            HeaderLabel("높이비율", NumWidth);
            HeaderLabel("최소높이", NumWidth);
            HeaderLabel("정점위치", NumWidth);
            HeaderLabel("상승지수", NumWidth);
            HeaderLabel("하강지수", NumWidth);
            HeaderLabel("착탄오차", NumWidth);
            HeaderLabel("흔들림비율", NumWidth);
            HeaderLabel("흔들림상한", NumWidth);
            HeaderLabel("범위반경", NumWidth);
            HeaderLabel("범위감쇠", NumWidth);
            HeaderLabel("이펙트기준반경", NumWidth);
            HeaderLabel("관통반경", NumWidth);
            HeaderLabel("", ButtonWidth);
        }

        EditorGUILayout.Space(2);
    }

    static void HeaderLabel(string text, float width)
    {
        EditorGUILayout.LabelField(text, EditorStyles.boldLabel, GUILayout.Width(width));
    }

    void DrawRow(Entry entry)
    {
        if (entry.attacker == null || entry.serializedAttacker == null ||
            entry.serializedAttacker.targetObject == null)
            return;

        entry.serializedAttacker.Update();

        AttackType type = (AttackType)entry.attackType.enumValueIndex;
        bool isCannon = type == AttackType.Cannon;
        bool isFlame = type == AttackType.Flamethrower;
        bool hasProjectile = type != AttackType.Melee;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(entry.data.displayName, GUILayout.Width(NameWidth));

            EditorGUILayout.PropertyField(entry.attackType, GUIContent.none, GUILayout.Width(TypeWidth));

            Field(entry.attackDamage);
            Field(entry.attackRange);

            using (new EditorGUI.DisabledScope(!isCannon))
                Field(entry.minAttackRange);

            Field(entry.attackCooldown);

            using (new EditorGUI.DisabledScope(!hasProjectile))
                Field(entry.projectileSpeed);

            using (new EditorGUI.DisabledScope(!isCannon))
                Field(entry.useBallisticArc);

            bool ballistic = isCannon && entry.useBallisticArc.boolValue;

            using (new EditorGUI.DisabledScope(!ballistic))
                Field(entry.ballisticGravity);

            using (new EditorGUI.DisabledScope(!isCannon || ballistic))
            {
                Field(entry.arcHeight);
                Field(entry.arcHeightRatio);
                Field(entry.minArcHeight);
                Field(entry.arcPeakTime);
                Field(entry.arcClimbPower);
                Field(entry.arcDivePower);
            }

            using (new EditorGUI.DisabledScope(!isCannon))
            {
                Field(entry.impactOffsetRadius);
                Field(entry.lateralWobbleRatio);
                Field(entry.lateralWobbleAmount);
                Field(entry.splashRadius);
                Field(entry.splashMinDamageRatio);
                Field(entry.hitEffectBaseRadius);
            }

            using (new EditorGUI.DisabledScope(!isFlame))
                Field(entry.pierceHitRadius);

            if (GUILayout.Button("선택", GUILayout.Width(ButtonWidth)))
            {
                Selection.activeObject = entry.prefab;
                EditorGUIUtility.PingObject(entry.prefab);
            }
        }

        if (entry.serializedAttacker.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(entry.attacker);
            PrefabUtility.SavePrefabAsset(entry.prefab);
        }
    }

    static void Field(SerializedProperty prop)
    {
        EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.Width(NumWidth));
    }
}
