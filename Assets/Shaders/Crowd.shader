// Animated spectators. Every person is one quad (four vertices at the same spot: the person's feet); the vertex shader opens it up into a
// card that always faces the camera, so the crowd reads well from any angle, and bobs it up and down. The fragment shader draws the
// person (head, hair, shoulders and two waving arms, with a thin dark outline) from maths: no texture, nothing to download.
// Vertex data: position = where the person stands, colour.rgb = shirt, colour.a = personal phase (0..1), uv0 = corner of the card (0..1),
// uv1 = (skin tone 0..1, hair tone 0..1). _CrowdCheer (global, 0..1) makes everybody jump and raise their arms (see Bleachers.Cheer).
Shader "BeachTennisGO/Crowd"
{
    Properties
    {
        _Size ("Person size (width, height)", Vector) = (0.8, 1.2, 0, 0)
        _BobSpeed ("Bob speed", Float) = 3.2
        _BobHeight ("Idle bob height (m)", Float) = 0.05
        _CheerHeight ("Extra jump when cheering (m)", Float) = 0.22
        _Outline ("Outline thickness", Float) = 0.03
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 shirt : TEXCOORD1; float4 look : TEXCOORD2; };

            float4 _Size;
            float _BobSpeed, _BobHeight, _CheerHeight, _Outline;
            float _CrowdCheer; // global

            float hash(float x) { return frac(sin(x * 127.1 + 17.3) * 43758.5453); }

            v2f vert(appdata v)
            {
                v2f o;
                float3 foot = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                float3 toCam = _WorldSpaceCameraPos - foot;
                toCam.y = 0.0;
                toCam = normalize(toCam + float3(0.0001, 0.0, 0.0001));
                float3 right = cross(toCam, float3(0.0, 1.0, 0.0)); // across the screen, whatever the viewing angle

                float phase = v.color.a * 6.2831853;
                float speed = _BobSpeed * (0.75 + 0.6 * hash(v.color.a * 91.7));
                float cheer = saturate(_CrowdCheer);
                float bob = abs(sin(_Time.y * speed + phase)) * (_BobHeight + _CheerHeight * cheer * (0.5 + hash(v.color.a * 33.1)));
                foot.y += bob;
                float3 world = foot + right * ((v.uv.x - 0.5) * _Size.x) + float3(0.0, v.uv.y * _Size.y, 0.0);
                o.pos = UnityWorldToClipPos(float4(world, 1.0));
                o.uv = v.uv;
                o.shirt = float4(v.color.rgb, v.color.a);
                o.look = float4(v.uv1.x, v.uv1.y, cheer, 0.0);
                return o;
            }

            // Signed distances (negative inside).
            float sdCircle(float2 p, float2 c, float r) { return length(p - c) - r; }
            float sdCapsule(float2 p, float2 a, float2 b, float r)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h) - r;
            }
            float sdBox(float2 p, float2 c, float2 halfSize, float r)
            {
                float2 d = abs(p - c) - (halfSize - r);
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv;
                float phase = i.shirt.a * 6.2831853;
                float cheer = i.look.z;
                float wave = sin(_Time.y * (4.0 + 3.0 * hash(i.shirt.a * 7.7)) + phase);
                float lift = 0.12 + 0.12 * wave + cheer * 0.26;           // how high the hands are above the shoulders

                float3 skin = lerp(float3(0.98, 0.78, 0.62), float3(0.42, 0.26, 0.16), i.look.x);
                float3 hairColor = lerp(float3(0.10, 0.07, 0.05), float3(0.95, 0.78, 0.30), step(0.8, i.look.y));
                hairColor = lerp(hairColor, float3(0.45, 0.25, 0.12), step(0.5, i.look.y) * step(i.look.y, 0.8));
                float3 shirt = i.shirt.rgb;

                float2 shoulderL = float2(0.27, 0.40), shoulderR = float2(0.73, 0.40);
                float2 handL = shoulderL + float2(-0.10, 0.10 + lift), handR = shoulderR + float2(0.10, 0.10 + lift * (0.6 + 0.4 * sin(phase * 3.0 + _Time.y * 5.0)));

                float dBody = sdBox(p, float2(0.5, 0.18), float2(0.30, 0.22), 0.12);
                float dArmL = sdCapsule(p, shoulderL, handL, 0.055);
                float dArmR = sdCapsule(p, shoulderR, handR, 0.055);
                float dHandL = sdCircle(p, handL, 0.065);
                float dHandR = sdCircle(p, handR, 0.065);
                float dHead = sdCircle(p, float2(0.5, 0.64), 0.155);
                float dHair = sdCircle(p, float2(0.5, 0.68), 0.168);

                float dAll = min(min(min(dBody, dArmL), min(dArmR, min(dHandL, dHandR))), min(dHead, dHair));
                clip(_Outline - dAll);                                    // outside the person (and its outline): not drawn

                float3 col = shirt;
                float dSkin = min(dHead, min(dHandL, dHandR));
                float dTop = min(dBody, min(dArmL, dArmR));
                if (dHair < dSkin && dHair < dTop && p.y > 0.66) col = hairColor;
                if (dSkin < dTop && dSkin < dHair + 0.02) col = skin;
                if (dHead < 0.0) col = (p.y > 0.70 && dHair < 0.0 && dHead > -0.04) ? hairColor : skin;
                float edge = smoothstep(-_Outline * 0.2, 0.0, dAll);      // 0 inside, 1 on the outline
                col = lerp(col, float3(0.08, 0.10, 0.20), edge);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
