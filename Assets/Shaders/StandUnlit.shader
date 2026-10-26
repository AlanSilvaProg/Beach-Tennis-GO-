// Flat vertex-coloured surfaces for the stands: no lights are needed (the scene has none), the shading of each face is baked into the vertex
// colours by Bleachers (tops lighter, fronts and sides darker).
Shader "BeachTennisGO/StandUnlit"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; return o; }
            fixed4 frag(v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
