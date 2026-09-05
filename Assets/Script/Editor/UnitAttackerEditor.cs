using UnityEditor;
using UnityEngine;

// 공격 타입(Melee/Ranged/Flamethrower/Cannon)에 따라 관련 없는 필드는 인스펙터에서 숨깁니다.
[CustomEditor(typeof(UnitAttacker))]
[CanEditMultipleObjects]
public class UnitAttackerEditor : Editor
{
    SerializedProperty attackType;
    SerializedProperty attackDamage;
    SerializedProperty attackRange;
    SerializedProperty attackCooldown;

    SerializedProperty useAttackAnimationEvent;

    SerializedProperty projectileSpeed;
    SerializedProperty firePoint;

    SerializedProperty pierceHitRadius;

    SerializedProperty arcHeight;
    SerializedProperty arcHeightRatio;
    SerializedProperty minArcHeight;
    SerializedProperty splashRadius;
    SerializedProperty splashMinDamageRatio;
    SerializedProperty hitEffectBaseRadius;

    SerializedProperty requireFacingToAttack;
    SerializedProperty aimAngleTolerance;
    SerializedProperty aimTransform;
    SerializedProperty aimYawOffset;

    SerializedProperty spawnVisualEffects;
    SerializedProperty projectileColor;
    SerializedProperty hitColor;

    SerializedProperty muzzleFlashPrefab;
    SerializedProperty hitEffectPrefab;
    SerializedProperty projectilePrefab;

    void OnEnable()
    {
        attackType = serializedObject.FindProperty("attackType");
        attackDamage = serializedObject.FindProperty("attackDamage");
        attackRange = serializedObject.FindProperty("attackRange");
        attackCooldown = serializedObject.FindProperty("attackCooldown");

        useAttackAnimationEvent = serializedObject.FindProperty("useAttackAnimationEvent");

        projectileSpeed = serializedObject.FindProperty("projectileSpeed");
        firePoint = serializedObject.FindProperty("firePoint");

        pierceHitRadius = serializedObject.FindProperty("pierceHitRadius");

        arcHeight = serializedObject.FindProperty("arcHeight");
        arcHeightRatio = serializedObject.FindProperty("arcHeightRatio");
        minArcHeight = serializedObject.FindProperty("minArcHeight");
        splashRadius = serializedObject.FindProperty("splashRadius");
        splashMinDamageRatio = serializedObject.FindProperty("splashMinDamageRatio");
        hitEffectBaseRadius = serializedObject.FindProperty("hitEffectBaseRadius");

        requireFacingToAttack = serializedObject.FindProperty("requireFacingToAttack");
        aimAngleTolerance = serializedObject.FindProperty("aimAngleTolerance");
        aimTransform = serializedObject.FindProperty("aimTransform");
        aimYawOffset = serializedObject.FindProperty("aimYawOffset");

        spawnVisualEffects = serializedObject.FindProperty("spawnVisualEffects");
        projectileColor = serializedObject.FindProperty("projectileColor");
        hitColor = serializedObject.FindProperty("hitColor");

        muzzleFlashPrefab = serializedObject.FindProperty("muzzleFlashPrefab");
        hitEffectPrefab = serializedObject.FindProperty("hitEffectPrefab");
        projectilePrefab = serializedObject.FindProperty("projectilePrefab");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Attack", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(attackType);
        EditorGUILayout.PropertyField(attackDamage);
        EditorGUILayout.PropertyField(attackRange);
        EditorGUILayout.PropertyField(attackCooldown);

        // 여러 오브젝트를 함께 선택했는데 공격 타입이 서로 다르면(혼합 값) 안전하게 전부 보여줍니다.
        bool mixedType = attackType.hasMultipleDifferentValues;
        AttackType type = (AttackType)attackType.enumValueIndex;

        bool showProjectile = mixedType || type != AttackType.Melee;
        bool showFlamethrower = mixedType || type == AttackType.Flamethrower;
        bool showCannon = mixedType || type == AttackType.Cannon;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useAttackAnimationEvent);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fire Point", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(firePoint);

        if (showProjectile)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ranged / Cannon", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(projectileSpeed);
        }

        if (showFlamethrower)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Flamethrower", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(pierceHitRadius);
        }

        if (showCannon)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cannon", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(arcHeight);
            EditorGUILayout.PropertyField(arcHeightRatio);
            EditorGUILayout.PropertyField(minArcHeight);
            EditorGUILayout.PropertyField(splashRadius);
            EditorGUILayout.PropertyField(splashMinDamageRatio);
            EditorGUILayout.PropertyField(hitEffectBaseRadius);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Aim", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(requireFacingToAttack);
        EditorGUILayout.PropertyField(aimAngleTolerance);
        EditorGUILayout.PropertyField(aimTransform);
        EditorGUILayout.PropertyField(aimYawOffset);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Visuals", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(spawnVisualEffects);
        EditorGUILayout.PropertyField(projectileColor);
        EditorGUILayout.PropertyField(hitColor);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Effect Prefabs", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(muzzleFlashPrefab);
        EditorGUILayout.PropertyField(hitEffectPrefab);

        if (showProjectile)
            EditorGUILayout.PropertyField(projectilePrefab);

        serializedObject.ApplyModifiedProperties();
    }
}
