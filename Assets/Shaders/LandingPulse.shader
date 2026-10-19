// Small radial waves on a ground quad: each ring starts at the full size, shrinks to the centre while fading
// out, then a new one appears at the full size again. Unlit, alpha blended, no depth write.
Shader "BeachTennisGO/LandingPulse"
{
    Properties
    {
        _CoreColor ("Ring colour", Color) = (1, 1, 0.85, 1)
        _OutlineColor ("Outline colour (contrast with the sand)", Color) = (0.2, 0.08, 0, 0.9)
        _Rings ("Rings at once", Range(1, 3)) = 2
        _Speed ("Cycles per second", Float) = 0.9
        _Width ("Ring thickness (outline included)", Range(0.04, 0.4)) = 0.16
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

            half4 _CoreColor;
            half4 _OutlineColor;
            float _Rings;
            float _Speed;
            float _Width;
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
                float outline = 0.0, core = 0.0;
                for (int k = 0; k < 3; k++)
                {
                    float on = step((float)k + 0.5, _Rings);
                    float phase = frac(_Time.y * _Speed + (float)k / max(_Rings, 1.0));
                    float r = 1.0 - phase;                                   // full size -> centre
                    float dist = abs(d - r);
                    float life = smoothstep(0.0, 0.06, phase) * (1.0 - smoothstep(0.7, 1.0, phase));
                    // Wide dark ring underneath, narrower bright ring on top: readable on light and dark ground.
                    outline = max(outline, (1.0 - smoothstep(_Width * 0.6, _Width, dist)) * life * on);
                    core = max(core, (1.0 - smoothstep(_Width * 0.2, _Width * 0.5, dist)) * life * on);
                }
                float clip = saturate((1.0 - d) / 0.05); // nothing outside the quad's circle
                half3 rgb = lerp(_OutlineColor.rgb, _CoreColor.rgb, core);
                half a = saturate(max(outline * _OutlineColor.a, core * _CoreColor.a)) * clip * _Intensity;
                return half4(rgb, a);
            }
            ENDCG
        }
    }
}
