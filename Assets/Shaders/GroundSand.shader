// Ground (sand and the court area): the sand texture mapped in world space on the XZ plane, so every object using it shares the same
// grain and the pattern does not stretch with the size of the mesh. Two layers at different scales and angles hide the tiling.
// Unlit, but the normal map is lit by a fixed "sun" (_SunDir) so the sand gets a soft relief: it only brightens / darkens the colour.
Shader "BeachTennisGO/GroundSand"
{
    Properties
    {
        _MainTex ("Sand texture (seamless)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _TileSize ("Tile size (m)", Float) = 5
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _NormalStrength ("Relief strength", Range(0, 3)) = 1
        _SunDir ("Direction towards the sun (xyz)", Vector) = (-0.4, 0.75, -0.5, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BumpMap;
            half4 _Color;
            float _TileSize;
            half _NormalStrength;
            float4 _SunDir;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float2 w = mul(unity_ObjectToWorld, v.vertex).xz;
                o.uv = w / _TileSize;
                // second layer: bigger, rotated ~35 degrees (cos, sin = 0.82, 0.57)
                o.uv2 = float2(w.x * 0.82 - w.y * 0.57, w.x * 0.57 + w.y * 0.82) / (_TileSize * 1.37);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 c = (tex2D(_MainTex, i.uv).rgb + tex2D(_MainTex, i.uv2).rgb) * 0.5;

                // Tangent-space normals of both layers brought to the world (x right, y up, z forward); layer 2 is rotated like its uv.
                half3 n1 = UnpackNormal(tex2D(_BumpMap, i.uv));
                half3 n2 = UnpackNormal(tex2D(_BumpMap, i.uv2));
                float2 slope1 = n1.xy;
                float2 slope2 = n2.x * float2(0.82, -0.57) + n2.y * float2(0.57, 0.82);
                float2 slope = (slope1 + slope2) * 0.5 * _NormalStrength;
                float3 n = normalize(float3(slope.x, 1.0, slope.y));

                float3 sun = normalize(_SunDir.xyz);
                half shade = saturate(dot(n, sun)) / sun.y;   // 1 on flat ground
                return half4(c * _Color.rgb * shade, 1);
            }
            ENDCG
        }
    }
}
