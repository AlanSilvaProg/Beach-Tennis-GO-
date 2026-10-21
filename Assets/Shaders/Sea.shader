// Stylised sea behind the court, fully procedural (no textures): shallow-to-deep colour, moving light wave crests, a foam line that
// slides up and down the shore and a band of wet sand in front of it. Meant for a flat plane lying just above the sand and starting a
// little before the shoreline (_ShoreZ is the world z of the average waterline). The far water fades into the horizon colour, which
// should match the camera background. Unlit, alpha blended (only the wet sand / foam edge is translucent), no depth write.
Shader "BeachTennisGO/Sea"
{
    Properties
    {
        _ShoreZ ("Shoreline world z", Float) = 17
        _ShallowColor ("Shallow water", Color) = (0.30, 0.85, 0.82, 1)
        _DeepColor ("Deep water", Color) = (0.04, 0.38, 0.72, 1)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        _WetColor ("Wet sand", Color) = (0.42, 0.30, 0.16, 1)
        _HorizonColor ("Horizon (camera background)", Color) = (0.55, 0.82, 0.95, 1)
        _WaveSpeed ("Wave speed (1 = lively, 0.3 = slow and calm)", Float) = 0.3
        _FadeStart ("Horizon fade starts at z", Float) = 35
        _FadeLength ("Horizon fade length (m)", Float) = 70
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _ShoreZ, _WaveSpeed, _FadeStart, _FadeLength;
            half4 _ShallowColor, _DeepColor, _FoamColor, _WetColor, _HorizonColor;

            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float x = i.world.x, z = i.world.z;

                // Waterline: slides back and forth, a different amount along the beach.
                float shore = _ShoreZ + 0.45 * sin(t * 0.8 + x * 0.17) + 0.2 * sin(t * 1.7 + x * 0.55);
                float d = z - shore;                       // metres into the water (negative = still sand)

                // Wet sand in front of the waterline, with a thin bright wash line right at the edge.
                if (d < 0.0)
                {
                    float wet = smoothstep(-1.8, 0.0, d);
                    float wash = smoothstep(-0.35, -0.05, d);
                    half3 c = lerp(_WetColor.rgb, _FoamColor.rgb, wash * 0.55);
                    return half4(c, wet * 0.55 + wash * 0.25);
                }

                // Water colour: shallow near the beach, deep further out.
                half3 c = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(d / 24.0));

                // Light wave crests travelling towards the beach.
                float w = sin(d * 1.5 - t * 1.6 + sin(x * 0.33 + t * 0.5) * 1.3);
                float crest = smoothstep(0.72, 1.0, w) * (1.0 - saturate(d / 70.0));
                c += crest * 0.22;

                // Foam: the breaking line at the shore plus a couple of fading streaks behind it.
                float breaker = 1.0 - smoothstep(0.25, 0.8 + 0.25 * sin(x * 0.9 + t * 2.0), d);
                float streak = smoothstep(0.7, 1.0, sin(d * 2.2 - t * 1.1 + x * 0.25)) * (1.0 - smoothstep(1.2, 5.0, d)) * 0.55;
                c = lerp(c, _FoamColor.rgb, saturate(breaker + streak));

                // Far water melts into the horizon / sky colour.
                float fade = saturate((z - _FadeStart) / _FadeLength);
                c = lerp(c, _HorizonColor.rgb, fade);
                return half4(c, 1);
            }
            ENDCG
        }
    }
}
