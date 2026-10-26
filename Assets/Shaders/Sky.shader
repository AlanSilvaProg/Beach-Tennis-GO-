// The sky behind the sea: a painted picture (clouds fading to a pale horizon) wrapped around the camera as a band just above the horizon.
//  - the picture's bottom edge sits on the horizon and its top edge at _TopElevation degrees above it (above that the top row goes on);
//  - horizontally it is _WidthDegrees wide and repeats; the picture's sides melt into plain sky, so the copies never touch each other
//    (the clouds of one copy never run into the clouds of the next);
//  - below the horizon it keeps the bottom row's colour, which is the colour the far sea fades into.
Shader "BeachTennisGO/Sky"
{
    Properties
    {
        _MainTex ("Sky picture", 2D) = "white" {}
        _TopElevation ("Elevation of the picture's top edge (degrees above the horizon)", Range(2, 40)) = 5.8
        _WidthDegrees ("Width of the picture (degrees of the horizon)", Range(10, 180)) = 30
        _Yaw ("Turn the picture around (degrees)", Range(-180, 180)) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _TopElevation, _WidthDegrees, _Yaw;
            fixed4 _Tint;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float elevation = degrees(asin(clamp(d.y, -1.0, 1.0)));
                float azimuth = degrees(atan2(d.x, d.z)) + _Yaw;
                float v = saturate(elevation / _TopElevation);
                float2 uv = float2(azimuth / _WidthDegrees + 0.5, v);   // a copy of the picture is centred straight ahead
                // frac() makes the picture repeat; the derivatives are taken before it so the mip level does not jump at the repeats
                return tex2Dgrad(_MainTex, float2(frac(uv.x), uv.y), ddx(uv), ddy(uv)) * _Tint;
            }
            ENDCG
        }
    }
}
