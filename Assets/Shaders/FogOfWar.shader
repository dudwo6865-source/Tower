Shader "RTS/FogOfWar"
{
    Properties
    {
        _FogTex ("Fog Texture", 2D) = "black" {}
        _MapOrigin ("Map Origin XZ", Vector) = (0, 0, 0, 0)
        _MapSize ("Map Size XZ", Vector) = (256, 256, 0, 0)
        _UnexploredColor ("Unexplored Color", Color) = (0, 0, 0, 0.95)
        _ExploredColor ("Explored Color", Color) = (0, 0, 0, 0.55)
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
            ZTest LEqual
            Offset 0, -1
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FogTex;
            float4 _FogTex_ST;
            float4 _MapOrigin;
            float4 _MapSize;
            fixed4 _UnexploredColor;
            fixed4 _ExploredColor;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = (i.worldPos.xz - _MapOrigin.xy) / _MapSize.xy;
                uv = saturate(uv);

                fixed4 fog = tex2D(_FogTex, uv);

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
