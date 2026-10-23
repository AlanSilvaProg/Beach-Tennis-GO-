// Rotating "sunburst" UI background: a radial gradient (centre -> middle -> edge), soft alternating rays and white dashes that fly along the
// rays. Everything is drawn from maths (no texture), so it costs no download and stays sharp at any size. Use it on a RawImage.
// _Aspect = width / height of the rectangle (RaysBackground sets it); the vertex colour's alpha fades the whole thing.
Shader "BeachTennisGO/RaysBackground"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture (RawImage sets it; not used)", 2D) = "white" {}
        _CenterColor ("Centre colour", Color) = (1.0, 0.80, 0.30, 1)
        _MidColor ("Middle colour", Color) = (0.95, 0.38, 0.45, 1)
        _EdgeColor ("Edge colour", Color) = (0.12, 0.33, 0.85, 1)
        _MidRadius ("Where the middle colour sits (0..1)", Range(0.1, 0.9)) = 0.36
        _Spin ("Rotation speed (turns per second)", Float) = 0.02
        _Rays ("Number of soft rays", Float) = 14
        _RayStrength ("Soft ray contrast", Range(0, 0.3)) = 0.08
        _DashColor ("Dash colour", Color) = (1, 1, 1, 0.9)
        _Aspect ("Aspect (w/h)", Float) = 1.78
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            fixed4 _CenterColor, _MidColor, _EdgeColor, _DashColor;
            float _MidRadius, _Spin, _Rays, _RayStrength, _Aspect;

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // One layer of dashes: `count` sectors around the circle; a sector holds a dash on its ray with some probability.
            float dashes(float turns, float r, float count, float seed, float width, float shift)
            {
                float t = frac(turns + shift) * count;
                float sector = floor(t);
                float across = (frac(t) - 0.5) / count * 6.2831853 * r; // distance from the ray, in screen units
                float h1 = hash(float2(sector, seed));
                float h2 = hash(float2(sector, seed + 7.3));
                float h3 = hash(float2(sector, seed + 13.9));
                float start = 0.12 + 1.5 * h2;
                float len = 0.05 + 0.22 * h3;
                float onRay = step(abs(across), width) * step(start, r) * step(r, start + len) * step(h1, 0.5);
                return onRay;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * float2(_Aspect, 1.0) * 2.0;   // -1..1 on the short side
                float r = length(p) / 1.15;                              // ~0.87 at the short edges, ~1.8 at the corners of a 16:9 screen
                float g = saturate(r / (1.0 + _Aspect * 0.5));            // 0 at the centre, 1 at the corners
                float turns = atan2(p.y, p.x) / 6.2831853 + _Time.y * _Spin;

                float k = saturate(g / _MidRadius);
                float k2 = saturate((g - _MidRadius) / (1.0 - _MidRadius));
                fixed3 col = lerp(_CenterColor.rgb, _MidColor.rgb, k);
                col = lerp(col, _EdgeColor.rgb, k2);

                float ray = 0.5 + 0.5 * sin(turns * 6.2831853 * _Rays);
                col += (ray - 0.5) * _RayStrength;

                // Dashes: three layers turning at slightly different speeds, so they drift against each other.
                float d = dashes(turns, r, 36.0, 1.0, 0.0090, 0.0)
                        + dashes(turns, r, 24.0, 5.0, 0.0075, _Time.y * 0.006)
                        + dashes(turns, r, 52.0, 9.0, 0.0050, -_Time.y * 0.010);
                col = lerp(col, _DashColor.rgb, saturate(d) * _DashColor.a);

                col *= 1.0 - 0.18 * smoothstep(0.55, 1.0, g);              // soft vignette
                return fixed4(col, i.color.a);
            }
            ENDCG
        }
    }
}
