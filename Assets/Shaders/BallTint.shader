// Unlit ball texture where only the grey panel is recoloured by _Tint. The yellow felt and the white seam
// keep their original colours. The grey is found by colour: low saturation AND not bright (white is bright).
// The tint keeps the fibre detail of the grey (it is multiplied by the texel brightness).
Shader "BeachTennisGO/BallTint"
{
    Properties
    {
        _MainTex ("Ball texture", 2D) = "white" {}
        _Tint ("Tint (replaces the grey)", Color) = (0.95, 0.3, 0.45, 1)
        _GraySat ("Max saturation of the grey", Range(0, 1)) = 0.2
        _GrayMaxValue ("Brightness where grey turns into white", Range(0, 1)) = 0.78
        _Softness ("Mask softness", Range(0.01, 0.5)) = 0.1
        _GrayRef ("Grey brightness mapped to full tint", Range(0.2, 1)) = 0.6
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
            float4 _MainTex_ST;
            half4 _Tint;
            half _GraySat;
            half _GrayMaxValue;
            half _Softness;
            half _GrayRef;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 c = tex2D(_MainTex, i.uv).rgb;
                half mx = max(c.r, max(c.g, c.b));
                half mn = min(c.r, min(c.g, c.b));
                half sat = mx > 0.0001 ? (mx - mn) / mx : 0.0;

                // 1 for grey (unsaturated and not bright), 0 for yellow felt (saturated) and white seam (bright).
                half lowSat = 1.0 - smoothstep(_GraySat, _GraySat + _Softness, sat);
                half notWhite = 1.0 - smoothstep(_GrayMaxValue, _GrayMaxValue + _Softness, mx);
                half mask = lowSat * notWhite;

                half3 tinted = _Tint.rgb * saturate(mx / _GrayRef);
                return half4(lerp(c, tinted, mask), 1.0);
            }
            ENDCG
        }
    }
}
