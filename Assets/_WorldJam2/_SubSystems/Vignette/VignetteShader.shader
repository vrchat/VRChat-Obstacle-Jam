Shader "Unlit/VignetteShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Color", Color) = (1,0,0,1)
        _Size ("Size", Float) = 5
        _Strength ("Strength", Float) = 0.5
    }
    SubShader
    {

        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        ZTest Always
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma fragmentoption ARB_precision_hint_fastest 
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Tint;
            float _Strength;
            float _Size;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                
                fixed4 color = tex2D(_MainTex, i.uv) * _Tint;
                float d = distance(i.uv, float2(0.5,0.5)) * _Strength;
                d = pow(d, _Size);
                color.a = d;
                return color;
            }
            ENDCG
        }
    }
}
