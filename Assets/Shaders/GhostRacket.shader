// See-through "ghost" of a model: the model's texture with a tint and a see-through amount (_Alpha), no lighting. Used for the racket that
// shows the player how to serve (ServeHint).
Shader "BeachTennisGO/GhostRacket"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (0.75, 0.92, 1, 1)
        _Alpha ("See-through amount (0 = invisible, 1 = solid)", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Tint; float _Alpha;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex); return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float glow = 0.7 + 0.3 * dot(c.rgb, float3(0.299, 0.587, 0.114)); // light, but the racket's drawing stays readable
                return fixed4(saturate(_Tint.rgb * glow * 1.25), _Alpha);
            }
            ENDCG
        }
    }
}
