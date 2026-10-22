// 멀리서도 보이는 빛 표시(Request1009Plan.md §37 L1 — 열린 탈출구 빛기둥·문틀 빛 띠·바닥 빛 원). 더하기 섞기, 안개를 받지 않는다.
// _Radial = 0: 원기둥·띠 — 가운데가 밝고 가장자리가 옅다(시선과 면 방향), 위로 갈수록(uv.y) _FadePower만큼 옅어진다.
// _Radial = 1: 바닥 원 — uv 가운데에서 바깥으로 옅어진다.
Shader "TagOfChaos/Beacon"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.3, 1, 0.45, 1)
        _FadePower ("Height Fade Power", Range(0.1, 8)) = 1.5
        _EdgePower ("Edge Power", Range(0.1, 8)) = 1.5
        _Radial ("Radial (ground disc)", Range(0, 1)) = 0
        _Pulse ("Pulse", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            ZWrite Off
            Blend One One
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Color;
            half _FadePower;
            half _EdgePower;
            half _Radial;
            half _Pulse;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 normal : TEXCOORD1; float3 viewDir : TEXCOORD2; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half facing = pow(saturate(abs(dot(normalize(i.normal), normalize(i.viewDir)))), _EdgePower);
                half column = facing * pow(saturate(1.0 - i.uv.y), _FadePower);
                half disc = pow(saturate(1.0 - length(i.uv - 0.5) * 2.0), _FadePower);
                half a = lerp(column, disc, step(0.5, _Radial));
                a *= 1.0 - _Pulse * (0.5 + 0.5 * sin(_Time.y * 3.0));
                return half4(_Color.rgb * a, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
