using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Base Shader 머티리얼 인스펙터.
/// Outline / Dissolve / Surface 기본 옵션을 보여주고, 덜 쓰는 맵은 폴드아웃으로 감춥니다.
/// URP의 UnityEditor.BaseShaderGUI 와 이름이 겹치지 않도록 Tank 접두사를 씁니다.
/// </summary>
public class TankBaseShaderGUI : ShaderGUI
{
    static readonly GUIContent OutlineLabel = new GUIContent("Outline");
    static readonly GUIContent DissolveLabel = new GUIContent("Dissolve");
    static readonly GUIContent SurfaceLabel = new GUIContent("Surface");
    static readonly GUIContent NormalLabel = new GUIContent("Normal Map");
    static readonly GUIContent EmissionLabel = new GUIContent("Emission");
    static readonly GUIContent AdvancedLabel = new GUIContent("Advanced");
    static readonly string[] MaskPackModeLabels =
    {
        "Metallic (R) — Substance 메탈릭",
        "Metallic (R) + Roughness (G)",
        "ORM (G=Roughness, B=Metallic)"
    };

    static bool showDissolve = true;
    static bool showNormal = false;
    static bool showEmission = false;
    static bool showAdvanced = false;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        if (materialEditor == null || properties == null)
            return;

        Material material = materialEditor.target as Material;
        if (material == null)
            return;

        FindProps(properties, out Props p);

        EditorGUI.BeginChangeCheck();

        DrawOutline(materialEditor, p);
        EditorGUILayout.Space(6f);

        showDissolve = EditorGUILayout.BeginFoldoutHeaderGroup(showDissolve, DissolveLabel);
        if (showDissolve)
            DrawDissolve(materialEditor, material, p);
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(6f);
        DrawSurface(materialEditor, p);

        EditorGUILayout.Space(8f);
        showNormal = EditorGUILayout.BeginFoldoutHeaderGroup(showNormal, NormalLabel);
        if (showNormal)
            DrawNormal(materialEditor, material, p);
        EditorGUILayout.EndFoldoutHeaderGroup();

        showEmission = EditorGUILayout.BeginFoldoutHeaderGroup(showEmission, EmissionLabel);
        if (showEmission)
            DrawEmission(materialEditor, material, p);
        EditorGUILayout.EndFoldoutHeaderGroup();

        showAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(showAdvanced, AdvancedLabel);
        if (showAdvanced)
            DrawAdvanced(materialEditor, material, p);
        EditorGUILayout.EndFoldoutHeaderGroup();

        bool changed = EditorGUI.EndChangeCheck();
        foreach (Object obj in materialEditor.targets)
        {
            Material mat = obj as Material;
            if (mat != null)
                SetMaterialKeywords(mat);
        }

        if (changed)
            materialEditor.PropertiesChanged();
    }

    struct Props
    {
        public MaterialProperty outlineColor;
        public MaterialProperty outlineWidth;
        public MaterialProperty outlineViewOffset;
        public MaterialProperty dissolveEnabled;
        public MaterialProperty dissolveHeight;
        public MaterialProperty dissolveEdge;
        public MaterialProperty dissolveEdgeColor;
        public MaterialProperty dissolveNoiseScale;
        public MaterialProperty dissolveNoiseStrength;
        public MaterialProperty dissolveFresnelColor;
        public MaterialProperty baseMap;
        public MaterialProperty baseColor;
        public MaterialProperty smoothness;
        public MaterialProperty metallic;
        public MaterialProperty metallicMap;
        public MaterialProperty maskPackMode;
        public MaterialProperty normalToggle;
        public MaterialProperty bumpScale;
        public MaterialProperty bumpMap;
        public MaterialProperty emissionToggle;
        public MaterialProperty emissionColor;
        public MaterialProperty emissionMap;
        public MaterialProperty alphaClip;
        public MaterialProperty cutoff;
        public MaterialProperty receiveShadows;
        public MaterialProperty queueOffset;
        public MaterialProperty cull;
        public MaterialProperty specularHighlights;
        public MaterialProperty environmentReflections;
    }

    static void FindProps(MaterialProperty[] properties, out Props p)
    {
        p = new Props
        {
            outlineColor = FindProperty("_OutlineColor", properties, false),
            outlineWidth = FindProperty("_OutlineWidth", properties, false),
            outlineViewOffset = FindProperty("_OutlineViewOffset", properties, false),
            dissolveEnabled = FindProperty("_DissolveEnabled", properties, false),
            dissolveHeight = FindProperty("_DissolveHeight", properties, false),
            dissolveEdge = FindProperty("_DissolveEdge", properties, false),
            dissolveEdgeColor = FindProperty("_DissolveEdgeColor", properties, false),
            dissolveNoiseScale = FindProperty("_DissolveNoiseScale", properties, false),
            dissolveNoiseStrength = FindProperty("_DissolveNoiseStrength", properties, false),
            dissolveFresnelColor = FindProperty("_DissolveFresnelColor", properties, false),
            baseMap = FindProperty("_BaseMap", properties, false),
            baseColor = FindProperty("_BaseColor", properties, false),
            smoothness = FindProperty("_Smoothness", properties, false),
            metallic = FindProperty("_Metallic", properties, false),
            metallicMap = FindProperty("_MetallicGlossMap", properties, false),
            maskPackMode = FindProperty("_MaskPackMode", properties, false),
            normalToggle = FindProperty("_NormalMapToggle", properties, false),
            bumpScale = FindProperty("_BumpScale", properties, false),
            bumpMap = FindProperty("_BumpMap", properties, false),
            emissionToggle = FindProperty("_EmissionToggle", properties, false),
            emissionColor = FindProperty("_EmissionColor", properties, false),
            emissionMap = FindProperty("_EmissionMap", properties, false),
            alphaClip = FindProperty("_AlphaClip", properties, false),
            cutoff = FindProperty("_Cutoff", properties, false),
            receiveShadows = FindProperty("_ReceiveShadows", properties, false),
            queueOffset = FindProperty("_QueueOffset", properties, false),
            cull = FindProperty("_Cull", properties, false),
            specularHighlights = FindProperty("_SpecularHighlights", properties, false),
            environmentReflections = FindProperty("_EnvironmentReflections", properties, false),
        };
    }

    static void DrawOutline(MaterialEditor editor, Props p)
    {
        EditorGUILayout.LabelField(OutlineLabel, EditorStyles.boldLabel);
        if (p.outlineColor != null)
            editor.ShaderProperty(p.outlineColor, "Outline Color");
        if (p.outlineWidth != null)
            editor.ShaderProperty(p.outlineWidth, "Outline Width");
        if (p.outlineViewOffset != null)
        {
            editor.ShaderProperty(
                p.outlineViewOffset,
                new GUIContent(
                    "Outline View Offset",
                    "뷰 방향(View Dir)에 이 Vector3를 곱한 뒤 아웃라인 확장에 더합니다. " +
                    "축별로 외곽선이 치우치는 위치를 조절하세요. (0,0,0)=순수 노멀 확장"));
        }
    }

    static void DrawDissolve(MaterialEditor editor, Material material, Props p)
    {
        bool enabled = material.IsKeywordEnabled("_DISSOLVE_ON");
        EditorGUI.BeginChangeCheck();
        enabled = EditorGUILayout.Toggle(
            new GUIContent("Enable Dissolve", "오브젝트 로컬 Y 높이 + 노이즈로 메시를 잘라냅니다."),
            enabled);
        if (EditorGUI.EndChangeCheck())
        {
            SetKeyword(material, "_DISSOLVE_ON", enabled);
            if (p.dissolveEnabled != null)
                p.dissolveEnabled.floatValue = enabled ? 1f : 0f;
        }

        using (new EditorGUI.DisabledScope(!enabled))
        {
            if (p.dissolveHeight != null)
            {
                editor.ShaderProperty(
                    p.dissolveHeight,
                    new GUIContent("Height", "디졸브 진행(오브젝트 로컬 Y 임계값). 값을 올리면 더 많이 남습니다."));
            }
            if (p.dissolveEdge != null)
                editor.ShaderProperty(p.dissolveEdge, new GUIContent("Edge", "엣지 발광 밴드 폭"));
            if (p.dissolveEdgeColor != null)
                editor.ShaderProperty(p.dissolveEdgeColor, new GUIContent("Edge Color", "엣지 HDR 발광색"));
            if (p.dissolveNoiseScale != null)
                editor.ShaderProperty(p.dissolveNoiseScale, new GUIContent("Scale", "Simple Noise 스케일"));
            if (p.dissolveNoiseStrength != null)
                editor.ShaderProperty(p.dissolveNoiseStrength, new GUIContent("Strength", "노이즈가 높이에 더해지는 세기"));
            if (p.dissolveFresnelColor != null)
            {
                editor.ShaderProperty(
                    p.dissolveFresnelColor,
                    new GUIContent("Fresnel Color", "디졸브 활성 시 프레넬 오버레이(HDR)"));
            }
        }
    }

    static void DrawSurface(MaterialEditor editor, Props p)
    {
        EditorGUILayout.LabelField(SurfaceLabel, EditorStyles.boldLabel);
        if (p.baseMap != null && p.baseColor != null)
            editor.TexturePropertySingleLine(new GUIContent("Albedo"), p.baseMap, p.baseColor);
        else if (p.baseMap != null)
            editor.TexturePropertySingleLine(new GUIContent("Albedo"), p.baseMap);

        if (p.baseMap != null)
            editor.TextureScaleOffsetProperty(p.baseMap);

        bool hasMaskMap = p.metallicMap != null && p.metallicMap.textureValue != null;
        int packMode = p.maskPackMode != null ? (int)p.maskPackMode.floatValue : 0;
        packMode = Mathf.Clamp(packMode, 0, 2);

        if (p.metallicMap != null)
        {
            editor.TexturePropertySingleLine(
                new GUIContent(
                    "Mask Map",
                    "섭페 메탈릭 맵은 Metallic (R) 모드로 넣으세요.\n흰색=금속(반사 강함), 검정=비금속."),
                p.metallicMap,
                hasMaskMap ? null : p.metallic);
        }
        else if (p.metallic != null)
        {
            editor.ShaderProperty(p.metallic, "Metallic");
        }

        if (hasMaskMap && p.maskPackMode != null)
        {
            EditorGUI.BeginChangeCheck();
            packMode = EditorGUILayout.Popup(
                new GUIContent("Mask Packing", "맵 채널을 어떻게 읽을지 선택합니다."),
                packMode,
                MaskPackModeLabels);
            if (EditorGUI.EndChangeCheck())
                p.maskPackMode.floatValue = packMode;
        }

        if (p.smoothness != null)
        {
            string smoothnessTooltip = "표면 매끄러움 (0~1). 값이 클수록 반사가 또렷합니다.";
            if (hasMaskMap && packMode > 0)
                smoothnessTooltip = "Roughness(G)를 반전한 값에 곱해집니다. 러프니스 흰 부분=거침=반사 약함.";

            editor.ShaderProperty(
                p.smoothness,
                new GUIContent("Smoothness", smoothnessTooltip));
        }

        if (hasMaskMap)
        {
            string help = packMode == 2
                ? "ORM: G=Roughness(흰=거침), B=Metallic(흰=금속). R(AO)는 쓰지 않습니다."
                : packMode == 1
                    ? "R=Metallic(흰=금속=반사 강함), G=Roughness(흰=거침=반사 약함)."
                    : "섭페 Metallic 맵: 흰 부분만 금속입니다. 러프니스는 Smoothness 슬라이더로 조절하세요.";
            EditorGUILayout.HelpBox(help, MessageType.Info);
        }
    }

    static void DrawNormal(MaterialEditor editor, Material material, Props p)
    {
        if (p.bumpMap != null && p.bumpScale != null)
            editor.TexturePropertySingleLine(new GUIContent("Normal Map"), p.bumpMap, p.bumpScale);
        else if (p.bumpMap != null)
            editor.TexturePropertySingleLine(new GUIContent("Normal Map"), p.bumpMap);

        bool hasNormal = p.bumpMap != null && p.bumpMap.textureValue != null;
        if (p.normalToggle != null)
            p.normalToggle.floatValue = hasNormal ? 1f : 0f;
        SetKeyword(material, "_NORMALMAP", hasNormal);
    }

    static void DrawEmission(MaterialEditor editor, Material material, Props p)
    {
        bool useEmission = material.IsKeywordEnabled("_EMISSION");
        EditorGUI.BeginChangeCheck();
        useEmission = EditorGUILayout.Toggle("Use Emission", useEmission);
        if (EditorGUI.EndChangeCheck())
        {
            SetKeyword(material, "_EMISSION", useEmission);
            if (p.emissionToggle != null)
                p.emissionToggle.floatValue = useEmission ? 1f : 0f;

            if (useEmission)
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            else
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        using (new EditorGUI.DisabledScope(!useEmission))
        {
            if (p.emissionMap != null && p.emissionColor != null)
                editor.TexturePropertySingleLine(new GUIContent("Emission"), p.emissionMap, p.emissionColor);
            else if (p.emissionColor != null)
                editor.ShaderProperty(p.emissionColor, "Emission Color");
        }
    }

    static void DrawAdvanced(MaterialEditor editor, Material material, Props p)
    {
        if (p.alphaClip != null)
            editor.ShaderProperty(p.alphaClip, "Alpha Clipping");
        if (p.cutoff != null && material.IsKeywordEnabled("_ALPHATEST_ON"))
            editor.ShaderProperty(p.cutoff, "Alpha Cutoff");

        if (p.receiveShadows != null)
            editor.ShaderProperty(p.receiveShadows, "Receive Shadows");
        if (p.specularHighlights != null)
            editor.ShaderProperty(p.specularHighlights, "Specular Highlights");
        if (p.environmentReflections != null)
            editor.ShaderProperty(p.environmentReflections, "Environment Reflections");
        if (p.cull != null)
            editor.ShaderProperty(p.cull, "Render Face");
        if (p.queueOffset != null)
            editor.ShaderProperty(p.queueOffset, "Queue Offset");

        editor.EnableInstancingField();
        editor.RenderQueueField();
    }

    static void SetMaterialKeywords(Material material)
    {
        if (material.HasProperty("_DissolveEnabled"))
            SetKeyword(material, "_DISSOLVE_ON", material.GetFloat("_DissolveEnabled") > 0.5f);

        SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap") != null);
        SetKeyword(material, "_PARALLAXMAP", false);
        SetKeyword(material, "_OCCLUSIONMAP", false);
        SetKeyword(material, "_MASK_OCCLUSION", false);
        SetKeyword(material, "_DETAIL_MULX2", false);
        SetKeyword(material, "_DETAIL_SCALED", false);

        if (material.HasProperty("_MetallicGlossMap"))
            SetKeyword(material, "_METALLICSPECGLOSSMAP", material.GetTexture("_MetallicGlossMap") != null);

        int packMode = material.HasProperty("_MaskPackMode")
            ? Mathf.Clamp((int)material.GetFloat("_MaskPackMode"), 0, 2)
            : 0;
        SetKeyword(material, "_MASK_PACK_MR", packMode == 1);
        SetKeyword(material, "_MASK_PACK_ORM", packMode == 2);

        bool receiveShadows = !material.HasProperty("_ReceiveShadows") || material.GetFloat("_ReceiveShadows") > 0.5f;
        SetKeyword(material, "_RECEIVE_SHADOWS_OFF", !receiveShadows);

        bool alphaClip = material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f;
        SetKeyword(material, "_ALPHATEST_ON", alphaClip);

        bool specOff = material.HasProperty("_SpecularHighlights") && material.GetFloat("_SpecularHighlights") < 0.5f;
        SetKeyword(material, "_SPECULARHIGHLIGHTS_OFF", specOff);

        bool envOff = material.HasProperty("_EnvironmentReflections") && material.GetFloat("_EnvironmentReflections") < 0.5f;
        SetKeyword(material, "_ENVIRONMENTREFLECTIONS_OFF", envOff);
    }

    static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);
    }
}
