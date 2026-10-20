// Net model: the texture's alpha makes the mesh holes see-through (only the cords, tape and posts stay visible).
// Unlit, alpha blended, no depth write. _AlphaBoost keeps the cords visible when mip maps average them with the holes at a distance.
Shader "BeachTennisGO/NetSheer"
{
    Properties
    {
        _MainTex ("Texture (alpha = cords)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _AlphaBoost ("Alpha boost", Range(1, 4)) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;
            half _AlphaBoost;

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
                half4 c = tex2D(_MainTex, i.uv) * _Color;
                c.a = saturate(c.a * _AlphaBoost);
                return c;
            }
            ENDCG
        }
    }
}
