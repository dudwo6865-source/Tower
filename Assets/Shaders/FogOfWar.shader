Shader "RTS/FogOfWar"
{
    Properties
    {
        _FogTex ("Fog Texture", 2D) = "black" {}
        _FogPrevTex ("Previous Fog Texture", 2D) = "black" {}
        _FogBlend ("Fog Blend", Range(0, 1)) = 1
        _MapOrigin ("Map Origin XZ", Vector) = (0, 0, 0, 0)
        _MapSize ("Map Size XZ", Vector) = (256, 256, 0, 0)
        _UnexploredColor ("Unexplored Color", Color) = (0, 0, 0, 0.95)
        _ExploredColor ("Explored Color", Color) = (0, 0, 0, 0.55)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Offset 0, -1
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 깊이 기반 안개: 안개 상자의 뒷면을 그리면서, 카메라 깊이 텍스처로 그 픽셀에
            // 실제로 그려진 지형·절벽의 월드 좌표를 복원해 그 XZ의 안개 값을 칠한다.
            #pragma multi_compile_local __ _FOG_DEPTH_PROJECTION
            #include "UnityCG.cginc"

            sampler2D _FogTex;
            sampler2D _FogPrevTex;
            float _FogBlend;
            float4 _MapOrigin;
            float4 _MapSize;
            fixed4 _UnexploredColor;
            fixed4 _ExploredColor;

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.screenPos = ComputeScreenPos(o.vertex);
                o.eyeDepth = -UnityObjectToViewPos(v.vertex.xyz).z;
                return o;
            }

            float3 ResolveFogWorldPos(v2f i)
            {
            #if defined(_FOG_DEPTH_PROJECTION)
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV);

                if (unity_OrthoParams.w > 0.5)
                {
                    // 직교 카메라: 깊이가 near~far 사이에서 선형이고 시선 방향이 일정하다.
                #if defined(UNITY_REVERSED_Z)
                    float depth01 = 1.0 - rawDepth;
                #else
                    float depth01 = rawDepth;
                #endif
                    float sceneEyeDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, depth01);
                    float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
                    return i.worldPos + cameraForward * (sceneEyeDepth - i.eyeDepth);
                }

                // 원근 카메라: 카메라에서 이 픽셀로 가는 광선을 장면 깊이만큼 늘린다.
                float perspectiveEyeDepth = LinearEyeDepth(rawDepth);
                float3 ray = i.worldPos - _WorldSpaceCameraPos;
                return _WorldSpaceCameraPos + ray * (perspectiveEyeDepth / max(i.eyeDepth, 1e-4));
            #else
                return i.worldPos;
            #endif
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 fogWorldPos = ResolveFogWorldPos(i);

                float2 uv = (fogWorldPos.xz - _MapOrigin.xy) / _MapSize.xy;
                uv = saturate(uv);

                // 직전 갱신 결과에서 이번 결과로 서서히 넘어가 경계가 툭툭 끊기지 않게 한다.
                fixed4 fog = lerp(tex2D(_FogPrevTex, uv), tex2D(_FogTex, uv), _FogBlend);

                float visibility = fog.g;
                float explored = fog.r;
                float fogAmount = 1.0 - visibility;

                if (fogAmount <= 0.001)
                    discard;

                fixed4 fogColor = lerp(_UnexploredColor, _ExploredColor, explored);
                fogColor.a *= fogAmount;

                return fogColor;
            }
            ENDCG
        }
    }

    FallBack Off
}
