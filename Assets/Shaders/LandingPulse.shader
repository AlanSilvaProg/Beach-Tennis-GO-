// Soft radial waves expanding from the centre of a ground quad. Unlit, alpha blended, no depth write.
Shader "BeachTennisGO/LandingPulse"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 0.5)
        _Waves ("Waves across the radius", Float) = 2
        _Speed ("Wave speed (cycles/s)", Float) = 0.8
        _Sharpness ("Wave sharpness", Float) = 2
        _Intensity ("Intensity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Color;
            float _Waves;
            float _Speed;
            float _Sharpness;
            float _Intensity;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy * 2.0 - 1.0;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float d = length(i.uv);
                // Waves travel outwards: phase grows with time, shrinks with distance.
                float w = 0.5 + 0.5 * cos(6.2831853 * (d * _Waves - _Time.y * _Speed));
                w = pow(w, _Sharpness);
                float edge = saturate(1.0 - d);          // fade out at the rim
                float core = smoothstep(0.0, 0.15, d);   // and a soft hole in the very centre
                half a = w * edge * edge * core * _Color.a * _Intensity;
                return half4(_Color.rgb, a);
            }
            ENDCG
        }
    }
}
