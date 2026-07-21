#!/usr/bin/env python3
"""Generate StylizedExplosion.shadergraph for Unity Built-in Shader Graph."""

import json
import uuid

OUT_PATH = r"d:\Game\Tank!\Assets\Artasseet\Shader\StylizedExplosion.shadergraph"
HLSL_GUID = "a3f8c2d91e4b4a6f8c1d2e3f4a5b6c7d"


def uid():
    return uuid.uuid4().hex


g_edges = []
g_chunks = []


def add(obj):
    g_chunks.append(obj)
    return obj.get("m_ObjectId")


def connect(out_node, out_slot, in_node, in_slot):
    g_edges.append(
        {
            "m_OutputSlot": {"m_Node": {"m_Id": out_node}, "m_SlotId": out_slot},
            "m_InputSlot": {"m_Node": {"m_Id": in_node}, "m_SlotId": in_slot},
        }
    )


prop_noise = uid()
prop_dark = uid()
prop_light = uid()
cat = uid()
node_uv0 = uid()
node_uv1 = uid()
node_split = uid()
node_normal = uid()
node_view = uid()
node_cf = uid()
node_p_noise = uid()
node_p_dark = uid()
node_p_light = uid()
block_pos = uid()
block_nrm = uid()
block_tan = uid()
block_base = uid()
block_alpha = uid()
target = uid()
subtarget = uid()

slot_pos_in = uid()
slot_nrm_in = uid()
slot_tan_in = uid()
slot_base_in = uid()
slot_alpha_in = uid()

s_noise_out = uid()
s_dark_out = uid()
s_light_out = uid()
s_uv0_out = uid()
s_uv1_out = uid()
s_split_in = uid()
s_split_r = uid()
s_split_g = uid()
s_split_b = uid()
s_split_a = uid()
s_normal_out = uid()
s_view_out = uid()

cf_names = [
    ("uv", 0),
    ("particleLightControl", 0),
    ("particleDarkControl", 0),
    ("noiseTex", 0),
    ("noiseSampler", 0),
    ("darkColor", 0),
    ("lightColor", 0),
    ("worldNormal", 0),
    ("viewDirection", 0),
    ("BaseColor", 1),
    ("Alpha", 1),
]
cf_slots = [uid() for _ in cf_names]

graph = {
    "m_SGVersion": 3,
    "m_Type": "UnityEditor.ShaderGraph.GraphData",
    "m_ObjectId": uid(),
    "m_Properties": [{"m_Id": prop_noise}, {"m_Id": prop_dark}, {"m_Id": prop_light}],
    "m_Keywords": [],
    "m_Dropdowns": [],
    "m_CategoryData": [{"m_Id": cat}],
    "m_Nodes": [{"m_Id": x} for x in [
        block_pos, block_nrm, block_tan, block_base, block_alpha,
        node_uv0, node_uv1, node_split, node_normal, node_view, node_cf,
        node_p_noise, node_p_dark, node_p_light,
    ]],
    "m_GroupDatas": [],
    "m_StickyNoteDatas": [],
    "m_Edges": [],
    "m_VertexContext": {
        "m_Position": {"x": 0.0, "y": 0.0},
        "m_Blocks": [{"m_Id": block_pos}, {"m_Id": block_nrm}, {"m_Id": block_tan}],
    },
    "m_FragmentContext": {
        "m_Position": {"x": 0.0, "y": 200.0},
        "m_Blocks": [{"m_Id": block_base}, {"m_Id": block_alpha}],
    },
    "m_PreviewData": {
        "serializedMesh": {"m_SerializedMesh": '{"mesh":{"instanceID":0}}', "m_Guid": ""},
        "preventRotation": False,
    },
    "m_Path": "Tank/VFX",
    "m_GraphPrecision": 1,
    "m_PreviewMode": 2,
    "m_OutputNode": {"m_Id": ""},
    "m_SubDatas": [],
    "m_ActiveTargets": [{"m_Id": target}],
}

add(graph)

add({
    "m_SGVersion": 0,
    "m_Type": "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty",
    "m_ObjectId": prop_noise,
    "m_Guid": {"m_GuidSerialized": str(uuid.uuid4())},
    "m_Name": "Noise Texture",
    "m_DefaultRefNameVersion": 1,
    "m_RefNameGeneratedByDisplayName": "Noise Texture",
    "m_DefaultReferenceName": "_Noise_Texture",
    "m_OverrideReferenceName": "",
    "m_GeneratePropertyBlock": True,
    "m_UseCustomSlotLabel": False,
    "m_CustomSlotLabel": "",
    "m_DismissedVersion": 0,
    "m_Precision": 0,
    "overrideHLSLDeclaration": False,
    "hlslDeclarationOverride": 0,
    "m_Hidden": False,
    "m_Value": {"m_SerializedTexture": '{"texture":{"instanceID":0}}', "m_Guid": ""},
    "isMainTexture": True,
    "useTilingAndOffset": False,
    "m_Modifiable": True,
    "m_DefaultType": 0,
})

add({
    "m_SGVersion": 3,
    "m_Type": "UnityEditor.ShaderGraph.Internal.ColorShaderProperty",
    "m_ObjectId": prop_dark,
    "m_Guid": {"m_GuidSerialized": str(uuid.uuid4())},
    "m_Name": "Dark Color",
    "m_DefaultRefNameVersion": 1,
    "m_RefNameGeneratedByDisplayName": "Dark Color",
    "m_DefaultReferenceName": "_Dark_Color",
    "m_OverrideReferenceName": "",
    "m_GeneratePropertyBlock": True,
    "m_UseCustomSlotLabel": False,
    "m_CustomSlotLabel": "",
    "m_DismissedVersion": 0,
    "m_Precision": 0,
    "overrideHLSLDeclaration": False,
    "hlslDeclarationOverride": 0,
    "m_Hidden": False,
    "m_Value": {"r": 0.05, "g": 0.02, "b": 0.08, "a": 1.0},
    "isMainColor": False,
    "m_ColorMode": 0,
})

add({
    "m_SGVersion": 3,
    "m_Type": "UnityEditor.ShaderGraph.Internal.ColorShaderProperty",
    "m_ObjectId": prop_light,
    "m_Guid": {"m_GuidSerialized": str(uuid.uuid4())},
    "m_Name": "Light Color",
    "m_DefaultRefNameVersion": 1,
    "m_RefNameGeneratedByDisplayName": "Light Color",
    "m_DefaultReferenceName": "_Light_Color",
    "m_OverrideReferenceName": "",
    "m_GeneratePropertyBlock": True,
    "m_UseCustomSlotLabel": False,
    "m_CustomSlotLabel": "",
    "m_DismissedVersion": 0,
    "m_Precision": 0,
    "overrideHLSLDeclaration": False,
    "hlslDeclarationOverride": 0,
    "m_Hidden": False,
    "m_Value": {"r": 4.0, "g": 1.2, "b": 0.2, "a": 1.0},
    "isMainColor": False,
    "m_ColorMode": 1,
})

add({
    "m_SGVersion": 0,
    "m_Type": "UnityEditor.ShaderGraph.CategoryData",
    "m_ObjectId": cat,
    "m_Name": "",
    "m_ChildObjectList": [{"m_Id": prop_noise}, {"m_Id": prop_dark}, {"m_Id": prop_light}],
})

add({"m_SGVersion": 0, "m_Type": "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInUnlitSubTarget", "m_ObjectId": subtarget})

add({
    "m_SGVersion": 2,
    "m_Type": "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInTarget",
    "m_ObjectId": target,
    "m_ActiveSubTarget": {"m_Id": subtarget},
    "m_AllowMaterialOverride": False,
    "m_SurfaceType": 1,
    "m_ZWriteControl": 2,
    "m_ZTestMode": 4,
    "m_AlphaMode": 0,
    "m_RenderFace": 2,
    "m_AlphaClip": False,
    "m_CustomEditorGUI": "",
})

for block_id, desc, slot_id in [
    (block_pos, "VertexDescription.Position", slot_pos_in),
    (block_nrm, "VertexDescription.Normal", slot_nrm_in),
    (block_tan, "VertexDescription.Tangent", slot_tan_in),
    (block_base, "SurfaceDescription.BaseColor", slot_base_in),
    (block_alpha, "SurfaceDescription.Alpha", slot_alpha_in),
]:
    add({
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.BlockNode",
        "m_ObjectId": block_id,
        "m_Group": {"m_Id": ""},
        "m_Name": desc,
        "m_DrawState": {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": 0.0, "y": 0.0, "width": 0.0, "height": 0.0}},
        "m_Slots": [{"m_Id": slot_id}],
        "synonyms": [],
        "m_Precision": 0,
        "m_PreviewExpanded": True,
        "m_DismissedVersion": 0,
        "m_PreviewMode": 0,
        "m_CustomColors": {"m_SerializableColors": []},
        "m_SerializedDescriptor": desc,
    })

add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.PositionMaterialSlot", "m_ObjectId": slot_pos_in, "m_Id": 0, "m_DisplayName": "Position", "m_SlotType": 0, "m_Hidden": False, "m_ShaderOutputName": "Position", "m_StageCapability": 1, "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_Labels": [], "m_Space": 0})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.NormalMaterialSlot", "m_ObjectId": slot_nrm_in, "m_Id": 0, "m_DisplayName": "Normal", "m_SlotType": 0, "m_Hidden": False, "m_ShaderOutputName": "Normal", "m_StageCapability": 1, "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_Labels": [], "m_Space": 0})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.TangentMaterialSlot", "m_ObjectId": slot_tan_in, "m_Id": 0, "m_DisplayName": "Tangent", "m_SlotType": 0, "m_Hidden": False, "m_ShaderOutputName": "Tangent", "m_StageCapability": 1, "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_Labels": [], "m_Space": 0})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.ColorRGBMaterialSlot", "m_ObjectId": slot_base_in, "m_Id": 0, "m_DisplayName": "Base Color", "m_SlotType": 0, "m_Hidden": False, "m_ShaderOutputName": "BaseColor", "m_StageCapability": 2, "m_Value": {"x": 0.5, "y": 0.5, "z": 0.5}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_Labels": [], "m_ColorMode": 0, "m_DefaultColor": {"r": 0.5, "g": 0.5, "b": 0.5, "a": 1.0}})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector1MaterialSlot", "m_ObjectId": slot_alpha_in, "m_Id": 0, "m_DisplayName": "Alpha", "m_SlotType": 0, "m_Hidden": False, "m_ShaderOutputName": "Alpha", "m_StageCapability": 2, "m_Value": 1.0, "m_DefaultValue": 1.0, "m_Labels": []})

add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.UVNode", "m_ObjectId": node_uv0, "m_Group": {"m_Id": ""}, "m_Name": "UV0", "m_DrawState": {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": -1200.0, "y": -200.0, "width": 145.0, "height": 130.0}}, "m_Slots": [{"m_Id": s_uv0_out}], "synonyms": [], "m_Precision": 0, "m_PreviewExpanded": True, "m_DismissedVersion": 0, "m_PreviewMode": 0, "m_CustomColors": {"m_SerializableColors": []}, "m_Channel": 0})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector4MaterialSlot", "m_ObjectId": s_uv0_out, "m_Id": 0, "m_DisplayName": "Out", "m_SlotType": 1, "m_Hidden": False, "m_ShaderOutputName": "Out", "m_StageCapability": 3, "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}, "m_Labels": []})

add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.UVNode", "m_ObjectId": node_uv1, "m_Group": {"m_Id": ""}, "m_Name": "UV1", "m_DrawState": {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": -1200.0, "y": 80.0, "width": 145.0, "height": 130.0}}, "m_Slots": [{"m_Id": s_uv1_out}], "synonyms": [], "m_Precision": 0, "m_PreviewExpanded": True, "m_DismissedVersion": 0, "m_PreviewMode": 0, "m_CustomColors": {"m_SerializableColors": []}, "m_Channel": 1})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector4MaterialSlot", "m_ObjectId": s_uv1_out, "m_Id": 0, "m_DisplayName": "Out", "m_SlotType": 1, "m_Hidden": False, "m_ShaderOutputName": "Out", "m_StageCapability": 3, "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}, "m_Labels": []})

add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.SplitNode", "m_ObjectId": node_split, "m_Group": {"m_Id": ""}, "m_Name": "Split", "m_DrawState": {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": -980.0, "y": 80.0, "width": 120.0, "height": 149.0}}, "m_Slots": [{"m_Id": s_split_in}, {"m_Id": s_split_r}, {"m_Id": s_split_g}, {"m_Id": s_split_b}, {"m_Id": s_split_a}], "synonyms": ["separate"], "m_Precision": 0, "m_PreviewExpanded": True, "m_DismissedVersion": 0, "m_PreviewMode": 0, "m_CustomColors": {"m_SerializableColors": []}})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.DynamicVectorMaterialSlot", "m_ObjectId": s_split_in, "m_Id": 0, "m_DisplayName": "In", "m_SlotType": 0, "m_Hidden": False, "m_ShaderOutputName": "In", "m_StageCapability": 3, "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector1MaterialSlot", "m_ObjectId": s_split_r, "m_Id": 1, "m_DisplayName": "R", "m_SlotType": 1, "m_Hidden": False, "m_ShaderOutputName": "R", "m_StageCapability": 3, "m_Value": 0.0, "m_DefaultValue": 0.0, "m_Labels": []})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector1MaterialSlot", "m_ObjectId": s_split_g, "m_Id": 2, "m_DisplayName": "G", "m_SlotType": 1, "m_Hidden": False, "m_ShaderOutputName": "G", "m_StageCapability": 3, "m_Value": 0.0, "m_DefaultValue": 0.0, "m_Labels": []})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector1MaterialSlot", "m_ObjectId": s_split_b, "m_Id": 3, "m_DisplayName": "B", "m_SlotType": 1, "m_Hidden": False, "m_ShaderOutputName": "B", "m_StageCapability": 3, "m_Value": 0.0, "m_DefaultValue": 0.0, "m_Labels": []})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector1MaterialSlot", "m_ObjectId": s_split_a, "m_Id": 4, "m_DisplayName": "A", "m_SlotType": 1, "m_Hidden": False, "m_ShaderOutputName": "A", "m_StageCapability": 3, "m_Value": 0.0, "m_DefaultValue": 0.0, "m_Labels": []})

add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.NormalVectorNode", "m_ObjectId": node_normal, "m_Group": {"m_Id": ""}, "m_Name": "Normal Vector", "m_DrawState": {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": -1200.0, "y": 320.0, "width": 208.0, "height": 315.0}}, "m_Slots": [{"m_Id": s_normal_out}], "synonyms": ["surface direction"], "m_Precision": 0, "m_PreviewExpanded": True, "m_DismissedVersion": 0, "m_PreviewMode": 2, "m_CustomColors": {"m_SerializableColors": []}, "m_Space": 2})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector3MaterialSlot", "m_ObjectId": s_normal_out, "m_Id": 0, "m_DisplayName": "Out", "m_SlotType": 1, "m_Hidden": False, "m_ShaderOutputName": "Out", "m_StageCapability": 3, "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_Labels": []})

add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.ViewDirectionNode", "m_ObjectId": node_view, "m_Group": {"m_Id": ""}, "m_Name": "View Direction", "m_DrawState": {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": -1200.0, "y": 680.0, "width": 208.0, "height": 315.0}}, "m_Slots": [{"m_Id": s_view_out}], "synonyms": [], "m_Precision": 0, "m_PreviewExpanded": True, "m_DismissedVersion": 0, "m_PreviewMode": 2, "m_CustomColors": {"m_SerializableColors": []}, "m_Space": 2})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector3MaterialSlot", "m_ObjectId": s_view_out, "m_Id": 0, "m_DisplayName": "Out", "m_SlotType": 1, "m_Hidden": False, "m_ShaderOutputName": "Out", "m_StageCapability": 3, "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_Labels": []})

for node_id, prop_id, slot_out, y, label in [
    (node_p_noise, prop_noise, s_noise_out, -500, "Noise Texture"),
    (node_p_dark, prop_dark, s_dark_out, -350, "Dark Color"),
    (node_p_light, prop_light, s_light_out, -200, "Light Color"),
]:
    add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.PropertyNode", "m_ObjectId": node_id, "m_Group": {"m_Id": ""}, "m_Name": "Property", "m_DrawState": {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": -980.0, "y": float(y), "width": 160.0, "height": 34.0}}, "m_Slots": [{"m_Id": slot_out}], "synonyms": [], "m_Precision": 0, "m_PreviewExpanded": True, "m_DismissedVersion": 0, "m_PreviewMode": 0, "m_CustomColors": {"m_SerializableColors": []}, "m_Property": {"m_Id": prop_id}})

add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Texture2DMaterialSlot", "m_ObjectId": s_noise_out, "m_Id": 0, "m_DisplayName": "Noise Texture", "m_SlotType": 1, "m_Hidden": False, "m_ShaderOutputName": "Out", "m_StageCapability": 3, "m_BareResource": False})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector4MaterialSlot", "m_ObjectId": s_dark_out, "m_Id": 0, "m_DisplayName": "Dark Color", "m_SlotType": 1, "m_Hidden": False, "m_ShaderOutputName": "Out", "m_StageCapability": 3, "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}, "m_Labels": []})
add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector4MaterialSlot", "m_ObjectId": s_light_out, "m_Id": 0, "m_DisplayName": "Light Color", "m_SlotType": 1, "m_Hidden": False, "m_ShaderOutputName": "Out", "m_StageCapability": 3, "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}, "m_Labels": []})

cf_slot_objs = []
for i, (name, stype) in enumerate(cf_names):
    sid = cf_slots[i]
    if name == "uv":
        obj = {"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector2MaterialSlot", "m_ObjectId": sid, "m_Id": i, "m_DisplayName": name, "m_SlotType": stype, "m_Hidden": False, "m_ShaderOutputName": name, "m_StageCapability": 3, "m_Value": {"x": 0.0, "y": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0}, "m_Labels": []}
    elif name in ("darkColor", "lightColor", "worldNormal", "viewDirection", "BaseColor"):
        obj = {"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector3MaterialSlot", "m_ObjectId": sid, "m_Id": i, "m_DisplayName": name, "m_SlotType": stype, "m_Hidden": False, "m_ShaderOutputName": name, "m_StageCapability": 3, "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_Labels": []}
    elif name == "noiseTex":
        obj = {"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Texture2DInputMaterialSlot", "m_ObjectId": sid, "m_Id": i, "m_DisplayName": name, "m_SlotType": stype, "m_Hidden": False, "m_ShaderOutputName": name, "m_StageCapability": 3, "m_BareResource": False, "m_Texture": {"m_SerializedTexture": '{"texture":{"instanceID":0}}', "m_Guid": ""}, "m_DefaultType": 0}
    elif name == "noiseSampler":
        obj = {"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.SamplerStateMaterialSlot", "m_ObjectId": sid, "m_Id": i, "m_DisplayName": name, "m_SlotType": stype, "m_Hidden": False, "m_ShaderOutputName": name, "m_StageCapability": 3, "m_BareResource": False}
    else:
        obj = {"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector1MaterialSlot", "m_ObjectId": sid, "m_Id": i, "m_DisplayName": name, "m_SlotType": stype, "m_Hidden": False, "m_ShaderOutputName": name, "m_StageCapability": 3, "m_Value": 0.0, "m_DefaultValue": 0.0, "m_Labels": []}
    cf_slot_objs.append(obj)
    add(obj)

add({
    "m_SGVersion": 1,
    "m_Type": "UnityEditor.ShaderGraph.CustomFunctionNode",
    "m_ObjectId": node_cf,
    "m_Group": {"m_Id": ""},
    "m_Name": "Stylized Explosion Dissolve",
    "m_DrawState": {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": -420.0, "y": 120.0, "width": 280.0, "height": 420.0}},
    "m_Slots": [{"m_Id": s["m_ObjectId"]} for s in cf_slot_objs],
    "synonyms": ["code", "HLSL"],
    "m_Precision": 0,
    "m_PreviewExpanded": True,
    "m_DismissedVersion": 0,
    "m_PreviewMode": 0,
    "m_CustomColors": {"m_SerializableColors": []},
    "m_SourceType": 0,
    "m_FunctionName": "StylizedExplosionDissolve",
    "m_FunctionSource": HLSL_GUID,
    "m_FunctionBody": "",
})

connect(node_uv1, 0, node_split, 0)
connect(node_uv0, 0, node_cf, 0)
connect(node_split, 1, node_cf, 1)
connect(node_split, 2, node_cf, 2)
connect(node_p_noise, 0, node_cf, 3)
connect(node_p_dark, 0, node_cf, 5)
connect(node_p_light, 0, node_cf, 6)
connect(node_normal, 0, node_cf, 7)
connect(node_view, 0, node_cf, 8)
connect(node_cf, 9, block_base, 0)
connect(node_cf, 10, block_alpha, 0)

graph["m_Edges"] = g_edges

with open(OUT_PATH, "w", encoding="utf-8") as f:
    f.write(json.dumps(graph, indent=4))
    for chunk in g_chunks[1:]:
        f.write("\n\n")
        f.write(json.dumps(chunk, indent=4))

print("Wrote", OUT_PATH)
