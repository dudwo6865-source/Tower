Shader "Tank/Toon (Mesh Outline)"
{
    Properties
    {
        [MainTexture] _Base_Map("Base Map", 2D) = "white" {}
        _Shadow_Color("Shadow Color", Color) = (0, 0, 0, 0)
        _Outline_Color("Outline Color", Color) = (0, 0, 0, 1)
        _Outline_Width("Outline Width", Range(0.0001, 1)) = 0.015
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        // Pass 1: inverted hull outline
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "Always" }

            Cull Front
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            float _Outline_Width;
            fixed4 _Outline_Color;

            struct OutlineAppdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct OutlineV2f
            {
                float4 pos : SV_POSITION;
                UNITY_FOG_COORDS(0)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            OutlineV2f OutlineVert(OutlineAppdata v)
            {
                OutlineV2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 normalOS = normalize(v.normal);
                float3 extruded = v.vertex.xyz + normalOS * _Outline_Width;
                o.pos = UnityObjectToClipPos(float4(extruded, 1.0));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 OutlineFrag(OutlineV2f i) : SV_Target
            {
                fixed4 col = _Outline_Color;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        // Pass 2: toon shading (matches Toon.shadergraph logic)
        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode" = "ForwardBase" }

            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex ToonVert
            #pragma fragment ToonFrag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _Base_Map;
            float4 _Base_Map_ST;
            fixed4 _Shadow_Color;

            struct ToonAppdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ToonV2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ToonV2f ToonVert(ToonAppdata v)
            {
                ToonV2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _Base_Map);
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 ToonFrag(ToonV2f i) : SV_Target
            {
                fixed4 baseTex = tex2D(_Base_Map, i.uv);
                float3 normalWS = normalize(i.normalWS);

                float3 lightDir;
                if (_WorldSpaceLightPos0.w == 0.0)
                    lightDir = normalize(_WorldSpaceLightPos0.xyz);
                else
                    lightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);

                float ndotl = dot(normalWS, lightDir);
                float remapped = ndotl * 0.5 + 0.5;
                float shade = smoothstep(0.5, 0.6, remapped);

                fixed3 shadowTint = baseTex.rgb * _Shadow_Color.rgb;
                fixed3 litTint = baseTex.rgb;
                fixed3 toonColor = lerp(shadowTint, litTint, shade);
                toonColor *= _LightColor0.rgb;

                fixed4 col = fixed4(toonColor, baseTex.a);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    FallBack Off
}
