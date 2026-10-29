// The match has no real lights, so the character is lit by a fixed "sun" written in the shader: the texture, brightened, with soft shading
// (a high floor so no side goes dark), a little rim light and the same warm brightness as the racket. Works with skinned meshes.
Shader "BeachTennisGO/CharacterToon"
{
    Properties
    {
        _MainTex ("Base colour", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Range(0.5, 2)) = 1.25
        _Shade ("Shade depth (0 = flat)", Range(0, 1)) = 0.3
        _Rim ("Rim light", Range(0, 1)) = 0.18
        _SunDir ("Sun direction (world)", Vector) = (-0.35, 0.8, -0.5, 0)
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
            half _Brightness, _Shade, _Rim;
            float4 _SunDir;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half3 n : TEXCOORD1; half3 view : TEXCOORD2; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.view = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 n = normalize(i.n);
                half ndl = dot(n, normalize(_SunDir.xyz)) * 0.5 + 0.5;          // 0..1, wrapped so the dark side is only a little darker
                half light = lerp(1.0, 0.55 + 0.6 * ndl, _Shade);
                half rim = pow(1.0 - saturate(dot(n, normalize(i.view))), 2.5) * _Rim;
                half3 c = tex2D(_MainTex, i.uv).rgb * _Tint.rgb * _Brightness * light + rim;
                return half4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
