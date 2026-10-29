Shader "RTS/CaptureObjective"
{
    Properties { _Color ("표시 색", Color) = (1, 0.8, 0.1, 1) }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct Input { float4 vertex : POSITION; };
            struct Output { float4 vertex : SV_POSITION; };
            Output vert(Input input) { Output o; o.vertex = UnityObjectToClipPos(input.vertex); return o; }
            fixed4 frag(Output input) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
