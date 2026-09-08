using UnityEditor;
using UnityEngine;

// 공격 타입(Melee/Ranged/Flamethrower/Cannon/Hitscan)에 따라 관련 없는 필드는 인스펙터에서 숨깁니다.
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

    SerializedProperty minAttackRange;
    SerializedProperty useBallisticArc;
    SerializedProperty ballisticGravity;
    SerializedProperty arcHeight;
    SerializedProperty arcHeightRatio;
    SerializedProperty minArcHeight;
    SerializedProperty arcPeakTime;
    SerializedProperty arcClimbPower;
    SerializedProperty arcDivePower;
    SerializedProperty impactOffsetRadius;
    SerializedProperty lateralWobbleRatio;
    SerializedProperty lateralWobbleAmount;
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
    SerializedProperty trailEffectPrefab;

    SerializedProperty hitscanTrailPrefab;
    SerializedProperty hitscanTrailDuration;
    SerializedProperty hitscanTrailWidth;

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

        minAttackRange = serializedObject.FindProperty("minAttackRange");
        useBallisticArc = serializedObject.FindProperty("useBallisticArc");
        ballisticGravity = serializedObject.FindProperty("ballisticGravity");
        arcHeight = serializedObject.FindProperty("arcHeight");
        arcHeightRatio = serializedObject.FindProperty("arcHeightRatio");
        minArcHeight = serializedObject.FindProperty("minArcHeight");
        arcPeakTime = serializedObject.FindProperty("arcPeakTime");
        arcClimbPower = serializedObject.FindProperty("arcClimbPower");
        arcDivePower = serializedObject.FindProperty("arcDivePower");
        impactOffsetRadius = serializedObject.FindProperty("impactOffsetRadius");
        lateralWobbleRatio = serializedObject.FindProperty("lateralWobbleRatio");
        lateralWobbleAmount = serializedObject.FindProperty("lateralWobbleAmount");
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
        trailEffectPrefab = serializedObject.FindProperty("trailEffectPrefab");

        hitscanTrailPrefab = serializedObject.FindProperty("hitscanTrailPrefab");
        hitscanTrailDuration = serializedObject.FindProperty("hitscanTrailDuration");
        hitscanTrailWidth = serializedObject.FindProperty("hitscanTrailWidth");
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

        bool showProjectile = mixedType || type == AttackType.Ranged || type == AttackType.Flamethrower || type == AttackType.Cannon;
        bool showFlamethrower = mixedType || type == AttackType.Flamethrower;
        bool showCannon = mixedType || type == AttackType.Cannon;
        bool showHitscan = mixedType || type == AttackType.Hitscan;

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
            EditorGUILayout.PropertyField(minAttackRange);
            EditorGUILayout.PropertyField(useBallisticArc);

            bool mixedBallistic = useBallisticArc.hasMultipleDifferentValues;
            bool ballistic = mixedBallistic || useBallisticArc.boolValue;

            if (ballistic)
            {
                EditorGUILayout.PropertyField(ballisticGravity);
            }

            if (mixedBallistic || !useBallisticArc.boolValue)
            {
                EditorGUILayout.PropertyField(arcHeight);
                EditorGUILayout.PropertyField(arcHeightRatio);
                EditorGUILayout.PropertyField(minArcHeight);
                EditorGUILayout.PropertyField(arcPeakTime);
                EditorGUILayout.PropertyField(arcClimbPower);
                EditorGUILayout.PropertyField(arcDivePower);
            }

            EditorGUILayout.PropertyField(impactOffsetRadius);
            EditorGUILayout.PropertyField(lateralWobbleRatio);
            EditorGUILayout.PropertyField(lateralWobbleAmount);
            EditorGUILayout.PropertyField(splashRadius);
            EditorGUILayout.PropertyField(splashMinDamageRatio);
            EditorGUILayout.PropertyField(hitEffectBaseRadius);
        }

        if (showHitscan)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hitscan", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hitscanTrailPrefab);
            EditorGUILayout.PropertyField(hitscanTrailDuration);
            EditorGUILayout.PropertyField(hitscanTrailWidth);
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
        {
            EditorGUILayout.PropertyField(projectilePrefab);
            EditorGUILayout.PropertyField(trailEffectPrefab);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
