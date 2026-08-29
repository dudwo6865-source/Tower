//Unity Toon Shader (URP only, Particle vertex-color alpha variant)
//Based on "Toon(Tessellation)" (com.unity.toonshader)
//Adds: always multiplies particle vertex color alpha (Color over Lifetime) into final alpha.
Shader "Toon(Tessellation)Particle" {
    Properties {
        [HideInInspector] _simpleUI ("SimpleUI", Int ) = 0
        [HideInInspector][Enum(OFF, 0, ON, 1)] _isUnityToonshader("Material is touched by Unity Toon Shader", Int) = 1
        [HideInInspector] _utsVersionX("VersionX", Float) = 0
        [HideInInspector] _utsVersionY("VersionY", Float) = 11
        [HideInInspector] _utsVersionZ("VersionZ", Float) = 0
        [HideInInspector] _utsTechnique ("Technique", int ) = 0 //DWF
        _AutoRenderQueue("Automatic Render Queue ", int) = 1

        [Enum(OFF, 0, StencilOut, 1, StencilMask, 2)] _StencilMode("StencilMode", int) = 0
        // these are set in UniversalToonGUI.cs in accordance with _StencilMode
        _StencilComp("Stencil Comparison", Float) = 8
        _StencilNo("Stencil No", Float) = 1
        _StencilOpPass("Stencil Operation", Float) = 0
        _StencilOpFail("Stencil Operation", Float) = 0
        [Enum(OFF, 0, ON, 1,] _TransparentEnabled("Transparent Mode", int) = 0

        // DoubleShadeWithFeather
        // 0:_IS_CLIPPING_OFF      1:_IS_CLIPPING_MODE    2:_IS_CLIPPING_TRANSMODE
        // ShadingGradeMap
        // 0:_IS_TRANSCLIPPING_OFF 1:_IS_TRANSCLIPPING_ON
        [Enum(OFF, 0, ON, 1, TRANSMODE, 2)] _ClippingMode("CliippingMode", int) = 0


        [Enum(OFF, 0, FRONT, 1, BACK, 2)] _CullMode("Cull Mode", int) = 2  //OFF/FRONT/BACK
        [Enum(OFF, 0, ONT, 1)]	_ZWriteMode("ZWrite Mode", int) = 1  //OFF/ON
        [Enum(OFF, 0, ONT, 1)]	_ZOverDrawMode("ZOver Draw Mode", Float) = 0  //OFF/ON
        _SPRDefaultUnlitColorMask("SPRDefaultUnlit Path Color Mask", int) = 15
        [Enum(OFF, 0, FRONT, 1, BACK, 2)] _SRPDefaultUnlitColMode("SPRDefaultUnlit  Cull Mode", int) = 1  //OFF/FRONT/BACK
        // ClippingMask paramaters from Here.
        _ClippingMask("ClippingMask", 2D) = "white" {}

        _IsBaseMapAlphaAsClippingMask("IsBaseMapAlphaAsClippingMask", Float) = 0
        //
        [Toggle(_)] _Inverse_Clipping("Inverse_Clipping", Float) = 0
        _Clipping_Level("Clipping_Level", Range(0, 1)) = 0
        _Tweak_transparency("Tweak_transparency", Range(-1, 1)) = 0
        // [Particle] 이 셰이더는 파티클 정점색(Color over Lifetime) 알파를 항상 최종 알파에 곱합니다.
        // ClippingMask paramaters to Here.


        [Enum(OFF,0,FRONT,1,BACK,2)] _CullMode("Cull Mode", int) = 2  //OFF/FRONT/BACK






        _MainTex ("BaseMap", 2D) = "white" {}
        _BaseMap ("BaseMap", 2D) = "white" {}
        _BaseColor ("BaseColor", Color) = (1,1,1,1)
        //v.2.0.5 : Clipping/TransClipping for SSAO Problems in PostProcessing Stack.
        //If you want to go back the former SSAO results, comment out the below line.
        _Color ("Color", Color) = (1,1,1,1)
        //
        [Toggle(_)] _Is_LightColor_Base ("Is_LightColor_Base", Float ) = 1
        _1st_ShadeMap ("1st_ShadeMap", 2D) = "white" {}
        //v.2.0.5
        [Toggle(_)] _Use_BaseAs1st ("Use BaseMap as 1st_ShadeMap", Float ) = 0
        _1st_ShadeColor ("1st_ShadeColor", Color) = (1,1,1,1)
        [Toggle(_)] _Is_LightColor_1st_Shade ("Is_LightColor_1st_Shade", Float ) = 1
        _2nd_ShadeMap ("2nd_ShadeMap", 2D) = "white" {}
        //v.2.0.5
        [Toggle(_)] _Use_1stAs2nd ("Use 1st_ShadeMap as 2nd_ShadeMap", Float ) = 0
        _2nd_ShadeColor ("2nd_ShadeColor", Color) = (1,1,1,1)
        [Toggle(_)] _Is_LightColor_2nd_Shade ("Is_LightColor_2nd_Shade", Float ) = 1
        _NormalMap ("NormalMap", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 1)) = 1
        [Toggle(_)] _Is_NormalMapToBase ("Is_NormalMapToBase", Float ) = 0
        //v.2.0.4.4
        [Toggle(_)] _Set_SystemShadowsToBase ("Set_SystemShadowsToBase", Float ) = 1
        _Tweak_SystemShadowsLevel ("Tweak_SystemShadowsLevel", Range(-0.5, 0.5)) = 0
        //v.2.0.6
        _BaseColor_Step ("BaseColor_Step", Range(0, 1)) = 0.5
        _BaseShade_Feather ("Base/Shade_Feather", Range(0.0001, 1)) = 0.0001
        _ShadeColor_Step ("ShadeColor_Step", Range(0, 1)) = 0
        _1st2nd_Shades_Feather ("1st/2nd_Shades_Feather", Range(0.0001, 1)) = 0.0001
        _1st_ShadeColor_Step ("1st_ShadeColor_Step", Range(0, 1)) = 0.5
        _1st_ShadeColor_Feather ("1st_ShadeColor_Feather", Range(0.0001, 1)) = 0.0001
        _2nd_ShadeColor_Step ("2nd_ShadeColor_Step", Range(0, 1)) = 0
        _2nd_ShadeColor_Feather ("2nd_ShadeColor_Feather", Range(0.0001, 1)) = 0.0001
        //v.2.0.5
        _StepOffset ("Step_Offset (ForwardAdd Only)", Range(-0.5, 0.5)) = 0
        [Toggle(_)] _Is_Filter_HiCutPointLightColor ("PointLights HiCut_Filter (ForwardAdd Only)", Float ) = 1
        //
        _Set_1st_ShadePosition ("Set_1st_ShadePosition", 2D) = "white" {}
        _Set_2nd_ShadePosition ("Set_2nd_ShadePosition", 2D) = "white" {}
        _ShadingGradeMap("ShadingGradeMap", 2D) = "white" {}
        //v.2.0.6
        _Tweak_ShadingGradeMapLevel("Tweak_ShadingGradeMapLevel", Range(-0.5, 0.5)) = 0
        _BlurLevelSGM("Blur Level of ShadingGradeMap", Range(0, 10)) = 0
        //
        _HighColor ("HighColor", Color) = (0,0,0,1)
//v.2.0.4 HighColor_Tex
        _HighColor_Tex ("HighColor_Tex", 2D) = "white" {}
        [Toggle(_)] _Is_LightColor_HighColor ("Is_LightColor_HighColor", Float ) = 1
        [Toggle(_)] _Is_NormalMapToHighColor ("Is_NormalMapToHighColor", Float ) = 0
        _HighColor_Power ("HighColor_Power", Range(0, 1)) = 0
        [Toggle(_)] _Is_SpecularToHighColor ("Is_SpecularToHighColor", Float ) = 0
        [Toggle(_)] _Is_BlendAddToHiColor ("Is_BlendAddToHiColor", Float ) = 0
        [Toggle(_)] _Is_UseTweakHighColorOnShadow ("Is_UseTweakHighColorOnShadow", Float ) = 0
        _TweakHighColorOnShadow ("TweakHighColorOnShadow", Range(0, 1)) = 0
//HiColorMask
        _Set_HighColorMask ("Set_HighColorMask", 2D) = "white" {}
        _Tweak_HighColorMaskLevel ("Tweak_HighColorMaskLevel", Range(-1, 1)) = 0
        [Toggle(_)] _RimLight ("RimLight", Float ) = 0
        _RimLightColor ("RimLightColor", Color) = (1,1,1,1)
        [Toggle(_)] _Is_LightColor_RimLight ("Is_LightColor_RimLight", Float ) = 1
        [Toggle(_)] _Is_NormalMapToRimLight ("Is_NormalMapToRimLight", Float ) = 0
        _RimLight_Power ("RimLight_Power", Range(0, 1)) = 0.1
        _RimLight_InsideMask ("RimLight_InsideMask", Range(0.0001, 1)) = 0.0001
        [Toggle(_)] _RimLight_FeatherOff ("RimLight_FeatherOff", Float ) = 0
//RimLight
        [Toggle(_)] _LightDirection_MaskOn ("LightDirection_MaskOn", Float ) = 0
        _Tweak_LightDirection_MaskLevel ("Tweak_LightDirection_MaskLevel", Range(0, 0.5)) = 0
        [Toggle(_)] _Add_Antipodean_RimLight ("Add_Antipodean_RimLight", Float ) = 0
        _Ap_RimLightColor ("Ap_RimLightColor", Color) = (1,1,1,1)
        [Toggle(_)] _Is_LightColor_Ap_RimLight ("Is_LightColor_Ap_RimLight", Float ) = 1
        _Ap_RimLight_Power ("Ap_RimLight_Power", Range(0, 1)) = 0.1
        [Toggle(_)] _Ap_RimLight_FeatherOff ("Ap_RimLight_FeatherOff", Float ) = 0
//RimLightMask
        _Set_RimLightMask ("Set_RimLightMask", 2D) = "white" {}
        _Tweak_RimLightMaskLevel ("Tweak_RimLightMaskLevel", Range(-1, 1)) = 0
//
        [Toggle(_)] _MatCap ("MatCap", Float ) = 0
        _MatCap_Sampler ("MatCap_Sampler", 2D) = "black" {}
        //v.2.0.6
        _BlurLevelMatcap ("Blur Level of MatCap_Sampler", Range(0, 10)) = 0
        _MatCapColor ("MatCapColor", Color) = (1,1,1,1)
        [Toggle(_)] _Is_LightColor_MatCap ("Is_LightColor_MatCap", Float ) = 1
        [Toggle(_)] _Is_BlendAddToMatCap ("Is_BlendAddToMatCap", Float ) = 1
        _Tweak_MatCapUV ("Tweak_MatCapUV", Range(-0.5, 0.5)) = 0
        _Rotate_MatCapUV ("Rotate_MatCapUV", Range(-1, 1)) = 0
        //v.2.0.6
        [Toggle(_)] _CameraRolling_Stabilizer ("Activate CameraRolling_Stabilizer", Float ) = 0
        [Toggle(_)] _Is_NormalMapForMatCap ("Is_NormalMapForMatCap", Float ) = 0
        _NormalMapForMatCap ("NormalMapForMatCap", 2D) = "bump" {}
        _BumpScaleMatcap ("Scale for NormalMapforMatCap", Range(0, 1)) = 1
        _Rotate_NormalMapForMatCapUV ("Rotate_NormalMapForMatCapUV", Range(-1, 1)) = 0
        [Toggle(_)] _Is_UseTweakMatCapOnShadow ("Is_UseTweakMatCapOnShadow", Float ) = 0
        _TweakMatCapOnShadow ("TweakMatCapOnShadow", Range(0, 1)) = 0
//MatcapMask
        _Set_MatcapMask ("Set_MatcapMask", 2D) = "white" {}
        _Tweak_MatcapMaskLevel ("Tweak_MatcapMaskLevel", Range(-1, 1)) = 0
        [Toggle(_)] _Inverse_MatcapMask ("Inverse_MatcapMask", Float ) = 0
        //v.2.0.5
        [Toggle(_)] _Is_Ortho ("Orthographic Projection for MatCap", Float ) = 0
        //// Angel Rings
        [Toggle(_)] _AngelRing("AngelRing", Float) = 0
        _AngelRing_Sampler("AngelRing_Sampler", 2D) = "black" {}
        _AngelRing_Color("AngelRing_Color", Color) = (1, 1, 1, 1)
        [Toggle(_)] _Is_LightColor_AR("Is_LightColor_AR", Float) = 1
        _AR_OffsetU("AR_OffsetU", Range(0, 0.5)) = 0
        _AR_OffsetV("AR_OffsetV", Range(0, 1)) = 0.3
        [Toggle(_)] _ARSampler_AlphaOn("ARSampler_AlphaOn", Float) = 0
        //
        //v.2.0.7 Emissive
        [KeywordEnum(SIMPLE,ANIMATION)] _EMISSIVE("EMISSIVE MODE", Float) = 0
        _Emissive_Tex ("Emissive_Tex", 2D) = "white" {}
        [HDR]_Emissive_Color ("Emissive_Color", Color) = (0,0,0,1)
        _Base_Speed ("Base_Speed", Float ) = 0
        _Scroll_EmissiveU ("Scroll_EmissiveU", Range(-1, 1)) = 0
        _Scroll_EmissiveV ("Scroll_EmissiveV", Range(-1, 1)) = 0
        _Rotate_EmissiveUV ("Rotate_EmissiveUV", Float ) = 0
        [Toggle(_)] _Is_PingPong_Base ("Is_PingPong_Base", Float ) = 0
        [Toggle(_)] _Is_ColorShift ("Activate ColorShift", Float ) = 0
        [HDR]_ColorShift ("ColorSift", Color) = (0,0,0,1)
        _ColorShift_Speed ("ColorShift_Speed", Float ) = 0
        [Toggle(_)] _Is_ViewShift ("Activate ViewShift", Float ) = 0
        [HDR]_ViewShift ("ViewSift", Color) = (0,0,0,1)
        [Toggle(_)] _Is_ViewCoord_Scroll ("Is_ViewCoord_Scroll", Float ) = 0
        //
//Outline
        [KeywordEnum(NML,POS)] _OUTLINE("OUTLINE MODE", Float) = 0
        _Outline_Width ("Outline_Width", Float ) = 0
        _Farthest_Distance ("Farthest_Distance", Float ) = 100
        _Nearest_Distance ("Nearest_Distance", Float ) = 0.5
        _Outline_Sampler ("Outline_Sampler", 2D) = "white" {}
        _Outline_Color ("Outline_Color", Color) = (0.5,0.5,0.5,1)
        [Toggle(_)] _Is_BlendBaseColor ("Is_BlendBaseColor", Float ) = 0
        [Toggle(_)] _Is_LightColor_Outline ("Is_LightColor_Outline", Float ) = 1
        //v.2.0.4
        [Toggle(_)] _Is_OutlineTex ("Is_OutlineTex", Float ) = 0
        _OutlineTex ("OutlineTex", 2D) = "white" {}
        //Offset parameter
        _Offset_Z ("Offset_Camera_Z", Float) = 0
        //v.2.0.4.3 Baked Normal Texture for Outline
        [Toggle(_)] _Is_BakedNormal ("Is_BakedNormal", Float ) = 0
        _BakedNormal ("Baked Normal for Outline", 2D) = "white" {}
        //GI Intensity
        _GI_Intensity ("GI_Intensity", Range(0, 1)) = 0
        //For VR Chat under No effective light objects
        _Unlit_Intensity ("Unlit_Intensity", Range(0, 4)) = 0
        //v.2.0.5 
        [Toggle(_)] _Is_Filter_LightColor ("VRChat : SceneLights HiCut_Filter", Float ) = 1
        //Built-in Light Direction
        [Toggle(_)] _Is_BLD ("Advanced : Activate Built-in Light Direction", Float ) = 0
        _Offset_X_Axis_BLD (" Offset X-Axis (Built-in Light Direction)", Range(-1, 1)) = -0.05
        _Offset_Y_Axis_BLD (" Offset Y-Axis (Built-in Light Direction)", Range(-1, 1)) = 0.09
        [Toggle(_)] _Inverse_Z_Axis_BLD (" Inverse Z-Axis (Built-in Light Direction)", Float ) = 1


	//////////////////////////////////////////////////////////////////////////////
	////////////////// Beginning of HDRP Matte. //////////////////////////////////
	//////////////////////////////////////////////////////////////////////////////
        [Toggle(_)] _BaseColorVisible("Channel mask", Float) = 1
        [Toggle(_)] _BaseColorOverridden("Channel mask", Float) = 0
        _BaseColorMaskColor("chennel mask color", Color) = (1, 1, 1, 1)

        [Toggle(_)] _FirstShadeVisible("Channel mask", Float) = 1
        [Toggle(_)] _FirstShadeOverridden("Channel mask", Float) = 0
        _FirstShadeMaskColor("chennel mask color", Color) = (0, 1, 1, 1)

        [Toggle(_)] _SecondShadeVisible("Channel mask", Float) = 1
        [Toggle(_)] _SecondShadeOverridden("Channel mask", Float) = 0
        _SecondShadeMaskColor("chennel mask color", Color) = (0, 0, 1, 1)

        [Toggle(_)] _HighlightVisible("Channel mask", Float) = 1
        [Toggle(_)] _HighlightOverridden("Channel mask", Float) = 0
        _HighlightMaskColor("Channel mask color", Color) = (1, 1, 0, 1)

        [Toggle(_)] _AngelRingVisible("Channel mask", Float) = 1
        [Toggle(_)] _AngelRingOverridden("Channel mask", Float) = 0
        _AngelRingMaskColor("Channel mask color", Color) = (0, 1, 0, 1)

        [Toggle(_)] _RimLightVisible("Channel mask", Float) = 1
        [Toggle(_)] _RimLightOverridden("Channel mask", Float) = 0
        _RimLightMaskColor("Channel mask color", Color) = (1, 0, 1, 1)

        [Toggle(_)] _OutlineVisible("Channel mask", Float) = 1
        [Toggle(_)] _OutlineOverridden("Channel mask", Float) = 0
        _OutlineMaskColor("Channel mask color", Color) = (0, 0, 0, 1)

        [Toggle(_)] _ComposerMaskMode("", Float) = 0
        [Enum(None, 0, BaseColor, 1, FirstShade, 2, SecondShade,3, Highlight, 4, AngelRing, 5, RimLight, 6)] _ClippingMatteMode("Clipping Matte Mode", int) = 0


	//////////////////////////////////////////////////////////////////////////////
	////////////////// End of HDRP Matte.       //////////////////////////////////
	//////////////////////////////////////////////////////////////////////////////

	////////////////// Avoid HDRP srp batcher error ///////////////////////////////
        [HideInInspector] emissive("to avoid srp batcher error", Color)= (0, 0, 0, 1) //
	////////////////// Avoid HDRP srp batcher error ///////////////////////////////


	//////////////////////////////////////////////////////////////////////////////
	////////////////// Beginning of HDRP material default values. ////////////////
	//////////////////////////////////////////////////////////////////////////////
	_BaseColorMap("BaseColorMap", 2D) = "white" {}
        [HideInInspector] _BaseColorMap_MipInfo("_BaseColorMap_MipInfo", Vector) = (0, 0, 0, 0)

        _Metallic("_Metallic", Range(0.0, 1.0)) = 0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _MaskMap("MaskMap", 2D) = "white" {}
        _SmoothnessRemapMin("SmoothnessRemapMin", Float) = 0.0
        _SmoothnessRemapMax("SmoothnessRemapMax", Float) = 1.0
        _AlphaRemapMin("AlphaRemapMin", Float) = 0.0   // HDRP 14
        _AlphaRemapMax("AlphaRemapMax", Float) = 1.0   // HDRP 14
        _AORemapMin("AORemapMin", Float) = 0.0
        _AORemapMax("AORemapMax", Float) = 1.0

        _NormalMapOS("NormalMapOS", 2D) = "white" {} // Object space normal map - no good default value
        _NormalScale("_NormalScale", Range(0.0, 8.0)) = 1

        _BentNormalMap("_BentNormalMap", 2D) = "bump" {}
        _BentNormalMapOS("_BentNormalMapOS", 2D) = "white" {}

        _HeightMap("HeightMap", 2D) = "black" {}
        [HideInInspector] _HeightAmplitude("Height Amplitude", Float) = 0.02 // In world units. This will be computed in the UI.
        [HideInInspector] _HeightCenter("Height Center", Range(0.0, 1.0)) = 0.5 // In texture space

        [Enum(MinMax, 0, Amplitude, 1)] _HeightMapParametrization("Heightmap Parametrization", Int) = 0
        _HeightOffset("Height Offset", Float) = 0
        _HeightMin("Heightmap Min", Float) = -1
        _HeightMax("Heightmap Max", Float) = 1
        _HeightTessAmplitude("Amplitude", Float) = 2.0 // in Centimeters
        _HeightTessCenter("Height Center", Range(0.0, 1.0)) = 0.5 // In texture space

        _HeightPoMAmplitude("Height Amplitude", Float) = 2.0 // In centimeters

        _DetailMap("DetailMap", 2D) = "linearGrey" {}
        _DetailAlbedoScale("_DetailAlbedoScale", Range(0.0, 2.0)) = 1
        _DetailNormalScale("_DetailNormalScale", Range(0.0, 2.0)) = 1
        _DetailSmoothnessScale("_DetailSmoothnessScale", Range(0.0, 2.0)) = 1

        _TangentMap("TangentMap", 2D) = "bump" {}
        _TangentMapOS("TangentMapOS", 2D) = "white" {}
        _Anisotropy("Anisotropy", Range(-1.0, 1.0)) = 0
        _AnisotropyMap("AnisotropyMap", 2D) = "white" {}

        [HideInInspector] _DiffusionProfile("Obsolete, kept for migration purpose", Int) = 0
        [HideInInspector] _DiffusionProfileAsset("Diffusion Profile Asset", Vector) = (0, 0, 0, 0)
        [HideInInspector] _DiffusionProfileHash("Diffusion Profile Hash", Float) = 0
        _SubsurfaceMask("Subsurface Radius", Range(0.0, 1.0)) = 1.0
        _SubsurfaceMaskMap("Subsurface Radius Map", 2D) = "white" {}
        _TransmissionMask("Transmission Mask", Range(0.0, 1.0)) = 1.0
        _Thickness("Thickness", Range(0.0, 1.0)) = 1.0
        _ThicknessMap("Thickness Map", 2D) = "white" {}
        _ThicknessRemap("Thickness Remap", Vector) = (0, 1, 0, 0)

        _IridescenceThickness("Iridescence Thickness", Range(0.0, 1.0)) = 1.0
        _IridescenceThicknessMap("Iridescence Thickness Map", 2D) = "white" {}
        _IridescenceThicknessRemap("Iridescence Thickness Remap", Vector) = (0, 1, 0, 0)
        _IridescenceMask("Iridescence Mask", Range(0.0, 1.0)) = 1.0
        _IridescenceMaskMap("Iridescence Mask Map", 2D) = "white" {}

        _CoatMask("Coat Mask", Range(0.0, 1.0)) = 0.0
        _CoatMaskMap("CoatMaskMap", 2D) = "white" {}

        [ToggleUI] _EnergyConservingSpecularColor("_EnergyConservingSpecularColor", Float) = 1.0
        _SpecularColor("SpecularColor", Color) = (1, 1, 1, 1)
        _SpecularColorMap("SpecularColorMap", 2D) = "white" {}

        [Enum(Off, 0, From Ambient Occlusion, 1, From Bent Normals, 2)]  _SpecularOcclusionMode("Specular Occlusion Mode", Int) = 1

        [HDR] _EmissiveColor("EmissiveColor", Color) = (0, 0, 0)
        [HideInInspector] _EmissiveColorLDR("EmissiveColor LDR", Color) = (0, 0, 0)
        _EmissiveColorMap("EmissiveColorMap", 2D) = "white" {}
        [ToggleUI] _AlbedoAffectEmissive("Albedo Affect Emissive", Float) = 0.0
        [HideInInspector] _EmissiveIntensityUnit("Emissive Mode", Int) = 0
        [ToggleUI] _UseEmissiveIntensity("Use Emissive Intensity", Int) = 0
        _EmissiveIntensity("Emissive Intensity", Float) = 1
        _EmissiveExposureWeight("Emissive Pre Exposure", Range(0.0, 1.0)) = 1.0

        _DistortionVectorMap("DistortionVectorMap", 2D) = "black" {}
        [ToggleUI] _DistortionEnable("Enable Distortion", Float) = 0.0
        [ToggleUI] _DistortionDepthTest("Distortion Depth Test Enable", Float) = 1.0
        [Enum(Add, 0, Multiply, 1, Replace, 2)] _DistortionBlendMode("Distortion Blend Mode", Int) = 0
        [HideInInspector] _DistortionSrcBlend("Distortion Blend Src", Int) = 0
        [HideInInspector] _DistortionDstBlend("Distortion Blend Dst", Int) = 0
        [HideInInspector] _DistortionBlurSrcBlend("Distortion Blur Blend Src", Int) = 0
        [HideInInspector] _DistortionBlurDstBlend("Distortion Blur Blend Dst", Int) = 0
        [HideInInspector] _DistortionBlurBlendMode("Distortion Blur Blend Mode", Int) = 0
        _DistortionScale("Distortion Scale", Float) = 1
        _DistortionVectorScale("Distortion Vector Scale", Float) = 2
        _DistortionVectorBias("Distortion Vector Bias", Float) = -1
        _DistortionBlurScale("Distortion Blur Scale", Float) = 1
        _DistortionBlurRemapMin("DistortionBlurRemapMin", Float) = 0.0
        _DistortionBlurRemapMax("DistortionBlurRemapMax", Float) = 1.0

        [ToggleUI]  _UseShadowThreshold("_UseShadowThreshold", Float) = 0.0
        [ToggleUI]  _AlphaCutoffEnable("Alpha Cutoff Enable", Float) = 0.0
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _AlphaCutoffShadow("_AlphaCutoffShadow", Range(0.0, 1.0)) = 0.5
        _AlphaCutoffPrepass("_AlphaCutoffPrepass", Range(0.0, 1.0)) = 0.5
        _AlphaCutoffPostpass("_AlphaCutoffPostpass", Range(0.0, 1.0)) = 0.5
        [ToggleUI] _TransparentDepthPrepassEnable("_TransparentDepthPrepassEnable", Float) = 0.0
        [ToggleUI] _TransparentBackfaceEnable("_TransparentBackfaceEnable", Float) = 0.0
        [ToggleUI] _TransparentDepthPostpassEnable("_TransparentDepthPostpassEnable", Float) = 0.0
        _TransparentSortPriority("_TransparentSortPriority", Float) = 0

        // Transparency
        [Enum(None, 0, Box, 1, Sphere, 2, Thin, 3)]_RefractionModel("Refraction Model", Int) = 0
        [Enum(Proxy, 1, HiZ, 2)]_SSRefractionProjectionModel("Refraction Projection Model", Int) = 0
        _Ior("Index Of Refraction", Range(1.0, 2.5)) = 1.0
        _ThicknessMultiplier("Thickness Multiplier", Float) = 1.0
        _TransmittanceColor("Transmittance Color", Color) = (1.0, 1.0, 1.0)
        _TransmittanceColorMap("TransmittanceColorMap", 2D) = "white" {}
        _ATDistance("Transmittance Absorption Distance", Float) = 1.0
        [ToggleUI] _TransparentWritingMotionVec("_TransparentWritingMotionVec", Float) = 0.0

        // Stencil state
        [HideInInspector] _StencilRef("_StencilRef", Int) = 2 // StencilLightingUsage.RegularLighting
        [HideInInspector] _StencilWriteMask("_StencilWriteMask", Int) = 3 // StencilMask.Lighting
        [HideInInspector] _StencilRefGBuffer("_StencilRefGBuffer", Int) = 2 // StencilLightingUsage.RegularLighting
        [HideInInspector] _StencilWriteMaskGBuffer("_StencilWriteMaskGBuffer", Int) = 3 // StencilMask.Lighting
        [HideInInspector] _StencilRefDepth("_StencilRefDepth", Int) = 0 // Nothing
        [HideInInspector] _StencilWriteMaskDepth("_StencilWriteMaskDepth", Int) = 32 // DoesntReceiveSSR
        [HideInInspector] _StencilRefMV("_StencilRefMV", Int) = 128 // StencilBitMask.ObjectMotionVectors
        [HideInInspector] _StencilWriteMaskMV("_StencilWriteMaskMV", Int) = 128 // StencilBitMask.ObjectMotionVectors
        [HideInInspector] _StencilRefDistortionVec("_StencilRefDistortionVec", Int) = 64 // StencilBitMask.DistortionVectors
        [HideInInspector] _StencilWriteMaskDistortionVec("_StencilWriteMaskDistortionVec", Int) = 64 // StencilBitMask.DistortionVectors

        // Blending state
        [HideInInspector] _SurfaceType("__surfacetype", Float) = 0.0
        [HideInInspector] _BlendMode("__blendmode", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _AlphaSrcBlend("__alphaSrc", Float) = 1.0
        [HideInInspector] _AlphaDstBlend("__alphaDst", Float) = 0.0
        [HideInInspector][ToggleUI] _ZWrite("__zw", Float) = 1.0
        [HideInInspector][ToggleUI] _TransparentZWrite("_TransparentZWrite", Float) = 0.0
        [HideInInspector] _CullMode("__cullmode", Float) = 2.0
        [HideInInspector] _CullModeForward("__cullmodeForward", Float) = 2.0
        [HideInInspector] _TransparentCullMode("_TransparentCullMode", Int) = 2 // Back culling by default
        [HideInInspector] _ZTestDepthEqualForOpaque("_ZTestDepthEqualForOpaque", Int) = 4 // Less equal
        [HideInInspector] _ZTestModeDistortion("_ZTestModeDistortion", Int) = 8
        [HideInInspector] _ZTestGBuffer("_ZTestGBuffer", Int) = 4
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTestTransparent("Transparent ZTest", Int) = 4 // Less equal

        [ToggleUI] _EnableFogOnTransparent("Enable Fog", Float) = 1.0
        [ToggleUI] _EnableBlendModePreserveSpecularLighting("Enable Blend Mode Preserve Specular Lighting", Float) = 1.0

        [ToggleUI] _DoubleSidedEnable("Double sided enable", Float) = 0.0
        [Enum(Flip, 0, Mirror, 1, None, 2)] _DoubleSidedNormalMode("Double sided normal mode", Float) = 1
        [HideInInspector] _DoubleSidedConstants("_DoubleSidedConstants", Vector) = (1, 1, -1, 0)

        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3, Planar, 4, Triplanar, 5)] _UVBase("UV Set for base", Float) = 0
        [Enum(WorldSpace, 0, ObjectSpace, 1)] _ObjectSpaceUVMapping("Mapping space", Float) = 0.0
        _TexWorldScale("Scale to apply on world coordinate", Float) = 1.0
        [HideInInspector] _InvTilingScale("Inverse tiling scale = 2 / (abs(_BaseColorMap_ST.x) + abs(_BaseColorMap_ST.y))", Float) = 1
        [HideInInspector] _UVMappingMask("_UVMappingMask", Color) = (1, 0, 0, 0)
        [Enum(TangentSpace, 0, ObjectSpace, 1)] _NormalMapSpace("NormalMap space", Float) = 0

        [Enum(Subsurface Scattering, 0, Standard, 1, Anisotropy, 2, Iridescence, 3, Specular Color, 4, Translucent, 5)] _MaterialID("MaterialId", Int) = 1 // MaterialId.Standard
        [ToggleUI] _TransmissionEnable("_TransmissionEnable", Float) = 1.0

        [Enum(None, 0, Vertex displacement, 1, Pixel displacement, 2)] _DisplacementMode("DisplacementMode", Int) = 3
        [ToggleUI] _DisplacementLockObjectScale("displacement lock object scale", Float) = 1.0
        [ToggleUI] _DisplacementLockTilingScale("displacement lock tiling scale", Float) = 1.0
        [ToggleUI] _DepthOffsetEnable("Depth Offset View space", Float) = 0.0

        [ToggleUI] _EnableGeometricSpecularAA("EnableGeometricSpecularAA", Float) = 0.0
        _SpecularAAScreenSpaceVariance("SpecularAAScreenSpaceVariance", Range(0.0, 1.0)) = 0.1
        _SpecularAAThreshold("SpecularAAThreshold", Range(0.0, 1.0)) = 0.2

        _PPDMinSamples("Min sample for POM", Range(1.0, 64.0)) = 5
        _PPDMaxSamples("Max sample for POM", Range(1.0, 64.0)) = 15
        _PPDLodThreshold("Start lod to fade out the POM effect", Range(0.0, 16.0)) = 5
        _PPDPrimitiveLength("Primitive length for POM", Float) = 1
        _PPDPrimitiveWidth("Primitive width for POM", Float) = 1
        [HideInInspector] _InvPrimScale("Inverse primitive scale for non-planar POM", Vector) = (1, 1, 0, 0)

        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _UVDetail("UV Set for detail", Float) = 0
        [HideInInspector] _UVDetailsMappingMask("_UVDetailsMappingMask", Color) = (1, 0, 0, 0)
        [ToggleUI] _LinkDetailsWithBase("LinkDetailsWithBase", Float) = 1.0

        [Enum(Use Emissive Color, 0, Use Emissive Mask, 1)] _EmissiveColorMode("Emissive color mode", Float) = 1
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3, Planar, 4, Triplanar, 5)] _UVEmissive("UV Set for emissive", Float) = 0
        _TexWorldScaleEmissive("Scale to apply on world coordinate", Float) = 1.0
        [HideInInspector] _UVMappingMaskEmissive("_UVMappingMaskEmissive", Color) = (1, 0, 0, 0)

        _EmissionColor("Color", Color) = (1, 1, 1)

        // Tessellation specific
        [Enum(None, 0, Phong, 1)] _TessellationMode("Tessellation mode", Float) = 0
        _TessellationFactor("Tessellation Factor", Range(0.0, 64.0)) = 4.0
        _TessellationFactorMinDistance("Tessellation start fading distance", Float) = 20.0
        _TessellationFactorMaxDistance("Tessellation end fading distance", Float) = 50.0
        _TessellationFactorTriangleSize("Tessellation triangle size", Float) = 100.0
        _TessellationShapeFactor("Tessellation shape factor", Range(0.0, 1.0)) = 0.75 // Only use with Phong
        _TessellationBackFaceCullEpsilon("Tessellation back face epsilon", Range(-1.0, 0.0)) = -0.25
        _Color("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [ToggleUI] _SupportDecals("Support Decals", Float) = 1.0
        [ToggleUI] _ReceivesSSR("Receives SSR", Float) = 1.0
        [ToggleUI] _AddPrecomputedVelocity("AddPrecomputedVelocity", Float) = 0.0

        //Tessellation (Legacy)
        _TessEdgeLength("DX11 Tess : Edge length", Range(2, 50)) = 5
        _TessPhongStrength("DX11 Tess : Phong Strength", Range(0, 1)) = 0.5
        _TessExtrusionAmount("DX11 Tess : Extrusion Amount", Range(-0.005, 0.005)) = 0.0

        // Additional render-state props referenced by URP passes / UTS3GUI
        [HideInInspector] _Cull("__cull", Float) = 2.0
    }

    HLSLINCLUDE
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
    #define DISABLE_RP_SHADERS

    // enable dithering LOD crossfade
    #pragma multi_compile _ LOD_FADE_CROSSFADE
    ENDHLSL

    // *************************** //
    // ****** URP Subshader  ***** //
    // *************************** //
    SubShader
    {
        PackageRequirements
        {
             "com.unity.render-pipelines.universal": "10.5.0"
        }
        Tags {
            "RenderType"="Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

//ToonCoreStart
        Pass {
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}
            ZWrite[_ZWriteMode]
            Cull[_CullMode]
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil {

                Ref[_StencilNo]

                Comp[_StencilComp]
                Pass[_StencilOpPass]
                Fail[_StencilOpFail]

            }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex vert
            #pragma fragment frag

#ifndef DISABLE_RP_SHADERS
            #pragma shader_feature_local _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _METALLICSPECGLOSSMAP
            #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local _SPECULAR_SETUP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
#endif
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS
            #if (!defined(UNITY_COMPILER_DXC) && (defined(UNITY_PLATFORM_OSX) || defined(UNITY_PLATFORM_IOS))) || defined(SHADER_API_PS5)
                #if defined(SHADER_API_PS5) || defined(SHADER_API_METAL)
                    #define SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER 1
                    #pragma warning (disable : 3568) // unknown pragma ignored
                    #pragma never_use_dxc metal
                    #pragma dynamic_branch _ _FOVEATED_RENDERING_NON_UNIFORM_RASTER
                    #pragma warning (default : 3568) // restore unknown pragma ignored
                #endif
            #endif
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS

            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
        #if UNITY_VERSION >= 202230 // Requires Universal RP 14.0.7
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #else
            #pragma multi_compile _ DOTS_INSTANCING_ON
        #endif

            #define _IS_PASS_FWDBASE
            #pragma shader_feature_local _ _SHADINGGRADEMAP

            #pragma shader_feature _IS_TRANSCLIPPING_OFF _IS_TRANSCLIPPING_ON
            #pragma shader_feature _IS_ANGELRING_OFF _IS_ANGELRING_ON

            #pragma shader_feature_local _ UTS_USE_RAYTRACING_SHADOW
            #pragma shader_feature _IS_CLIPPING_OFF _IS_CLIPPING_MODE _IS_CLIPPING_TRANSMODE

            #pragma shader_feature _EMISSIVE_SIMPLE _EMISSIVE_ANIMATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#ifdef UNIVERSAL_PIPELINE_CORE_INCLUDED
            #include "UniversalToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "UniversalToonHead.hlsl"
            #include "UniversalToonBody.hlsl"
#endif
            ENDHLSL

        }

        Pass {
            Name "Outline"
            Tags {
                "LightMode" = "SRPDefaultUnlit"
            }
            Cull [_SRPDefaultUnlitColMode]
            ColorMask [_SPRDefaultUnlitColorMask]
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref[_StencilNo]
                Comp[_StencilComp]
                Pass[_StencilOpPass]
                Fail[_StencilOpFail]

            }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _IS_OUTLINE_CLIPPING_NO _IS_OUTLINE_CLIPPING_YES
            #pragma multi_compile _OUTLINE_NML _OUTLINE_POS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #pragma multi_compile_instancing
        #if UNITY_VERSION >= 202230 // Requires Universal RP 14.0.7
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #else
            #pragma multi_compile _ DOTS_INSTANCING_ON
        #endif

#ifdef UNIVERSAL_PIPELINE_CORE_INCLUDED
            #include "UniversalToonInput.hlsl"
            #include "UniversalToonHead.hlsl"
            #include "UniversalToonOutline.hlsl"
#endif
            ENDHLSL
        }

        Pass
        {
            Name "Universal2D"
            Tags
            {
                "LightMode" = "Universal2D"
            }

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "UniversalBasic2D.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            Cull[_CullMode]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma multi_compile_instancing
        #if UNITY_VERSION >= 202230 // Requires Universal RP 14.0.7
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #else
            #pragma multi_compile _ DOTS_INSTANCING_ON
        #endif

            #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "UniversalToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull[_CullMode]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile_instancing
        #if UNITY_VERSION >= 202230 // Requires Universal RP 14.0.7
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #else
            #pragma multi_compile _ DOTS_INSTANCING_ON
        #endif

            #include "UniversalToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Version.hlsl"
#ifndef VERSION_GREATER_EQUAL
#define VERSION_GREATER_EQUAL(x, y) 1
#endif
#if (VERSION_GREATER_EQUAL(10, 0))
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile_instancing
        #if UNITY_VERSION >= 202230 // Requires Universal RP 14.0.7
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #else
            #pragma multi_compile _ DOTS_INSTANCING_ON
        #endif

            #include "UniversalToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
#endif
            ENDHLSL
        }
//ToonCoreEnd
    }

    CustomEditor "UnityEditor.Rendering.Toon.UTS3GUI"
}
