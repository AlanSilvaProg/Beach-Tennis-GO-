// Unlit ad board: shows texture A and slides texture B in from the right (_Slide 0..1).
// One draw call per panel, no lighting, no transparency.
Shader "BeachTennisGO/AdPanel"
{
    Properties
    {
        _MainTex ("Current ad", 2D) = "white" {}
        _NextTex ("Next ad", 2D) = "white" {}
        _MainST ("Current ad rect (scale xy, offset zw)", Vector) = (1, 1, 0, 0)
        _NextST ("Next ad rect (scale xy, offset zw)", Vector) = (1, 1, 0, 0)
        _Slide ("Slide", Range(0, 1)) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NextTex;
            float4 _MainST;
            float4 _NextST;
            float _Slide;
            half4 _Tint;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float u = i.uv.x;
                half4 c;
                if (u < 1.0 - _Slide)
                    c = tex2D(_MainTex, float2(u + _Slide, i.uv.y) * _MainST.xy + _MainST.zw);
                else
                    c = tex2D(_NextTex, float2(u + _Slide - 1.0, i.uv.y) * _NextST.xy + _NextST.zw);
                return c * _Tint;
            }
            ENDCG
        }
    }
}
