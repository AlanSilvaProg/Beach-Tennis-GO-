// Flat fake ground shadow. _Shape 0: racket (handle + head with a lighter strings area) along uv.y; _Shape 1: net (soft strip,
// darker tape on top; uv.x < 0 marks the solid posts). Unlit, alpha blended, no depth write.
Shader "BeachTennisGO/FakeShadow"
{
    Properties
    {
        _Color ("Colour (alpha = darkness)", Color) = (0.22, 0.14, 0.04, 0.45)
        _Shape ("Shape (0 racket, 1 net)", Float) = 0
        _Aspect ("Length / width", Float) = 2
        _NetOpacity ("Net strip opacity", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "IgnoreProjector" = "True" }
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

            half4 _Color;
            float _Shape;
            float _Aspect;
            half _NetOpacity;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half a;
                if (_Shape < 0.5)
                {
                    // Work in units of the width so the head stays round whatever the length.
                    float2 p = float2(i.uv.x - 0.5, i.uv.y * _Aspect);
                    float handle = (1 - smoothstep(0.04, 0.08, abs(p.x))) * step(p.y, 0.45 * _Aspect + 0.1) * smoothstep(0.0, 0.05, p.y);
                    float2 e = (p - float2(0, 0.7 * _Aspect)) / float2(0.5, 0.3 * _Aspect);
                    float r = length(e);
                    float head = (1 - smoothstep(0.88, 1.0, r)) * lerp(0.55, 1, smoothstep(0.6, 0.85, r));
                    a = saturate(max(handle, head));
                }
                else if (i.uv.x < -0.5)
                {
                    a = 1; // post
                }
                else
                {
                    float edge = smoothstep(0.0, 0.03, i.uv.x) * smoothstep(1.0, 0.97, i.uv.x) * smoothstep(0.0, 0.2, i.uv.y);
                    a = edge * lerp(_NetOpacity, 1, smoothstep(0.82, 0.92, i.uv.y)); // tape on top is darker
                }
                return half4(_Color.rgb, _Color.a * a);
            }
            ENDCG
        }
    }
}
