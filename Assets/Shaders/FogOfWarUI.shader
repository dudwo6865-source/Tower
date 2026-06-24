Shader "RTS/FogOfWarUI"
{
    Properties
    {
        _FogTex ("Fog Texture", 2D) = "black" {}
        _UnexploredColor ("Unexplored Color", Color) = (0, 0, 0, 0.95)
        _ExploredColor ("Explored Color", Color) = (0, 0, 0, 0.55)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FogTex;
            fixed4 _UnexploredColor;
            fixed4 _ExploredColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 fog = tex2D(_FogTex, i.uv);

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
