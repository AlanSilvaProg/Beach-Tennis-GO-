// Used only to draw a racket into the small pictures of the Rackets window: the texture with a soft fake light, no scene lights needed.
Shader "BeachTennisGO/RacketPreview"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Ambient ("Ambient", Range(0, 1)) = 0.72
        _LightDir ("Light direction (view space)", Vector) = (-0.4, 0.6, 0.7, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Ambient;
            float4 _LightDir;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.n = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.n) * (facing > 0 ? 1 : -1);
                float l = saturate(dot(n, normalize(_LightDir.xyz)));
                fixed4 c = tex2D(_MainTex, i.uv);
                c.rgb *= _Ambient + (1 - _Ambient) * l * 1.6;
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
}
