// 물풍선 갇힘 물방울(Request1009Plan.md §45.7 WB3): 투명한 물 구체.
// 가운데는 밝은 하늘색이 아주 옅게(안에 갇힌 캐릭터가 선명하게 보임), 테두리로 갈수록 청록색 + 프레넬 빛.
// 테두리에서 뒤 배경이 살짝 굴절되고(GrabPass), 위쪽에 매끄러운 반사 하이라이트 두 개.
Shader "TagOfChaos/WaterBubble"
{
    Properties
    {
        _CenterColor ("Center Color", Color) = (0.62, 0.92, 1.0, 0.08)
        _RimColor ("Rim Color", Color) = (0.1, 0.85, 0.78, 0.75)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.6
        _Refraction ("Refraction", Range(0, 0.15)) = 0.05
        _Gloss ("Gloss", Range(8, 512)) = 160
        _Highlight ("Highlight", Range(0, 4)) = 1.6
        _Wobble ("Wobble", Range(0, 0.1)) = 0.025
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        GrabPass { "_BubbleGrab" }
        Pass
        {
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _CenterColor, _RimColor;
            float _RimPower, _Refraction, _Gloss, _Highlight, _Wobble;
            sampler2D _BubbleGrab;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 view : TEXCOORD1;
                float4 grab : TEXCOORD2;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                // 물이 살짝 출렁인다
                float w = sin(_Time.y * 3.1 + v.vertex.y * 7.0) * cos(_Time.y * 2.3 + v.vertex.x * 6.0);
                float4 p = v.vertex + float4(v.normal * w * _Wobble, 0);
                o.pos = UnityObjectToClipPos(p);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = normalize(WorldSpaceViewDir(p));
                o.grab = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 v = normalize(i.view);
                float facing = saturate(dot(n, v));
                float rim = pow(1.0 - facing, _RimPower);

                // 테두리 굴절: 법선 방향으로 배경을 조금 밀어 본다
                float2 offset = n.xy * _Refraction * rim;
                fixed3 behind = tex2Dproj(_BubbleGrab, UNITY_PROJ_COORD(i.grab + float4(offset * i.grab.w, 0, 0))).rgb;

                fixed4 tint = lerp(_CenterColor, _RimColor, rim);
                fixed3 col = lerp(behind, tint.rgb, tint.a * 0.85);
                // 반사 하이라이트(해 방향 + 위쪽 작은 창)
                float3 l1 = normalize(_WorldSpaceLightPos0.xyz + float3(0, 0.3, 0));
                float3 l2 = normalize(float3(-0.4, 1, -0.3));
                float s1 = pow(saturate(dot(reflect(-l1, n), v)), _Gloss);
                float s2 = pow(saturate(dot(reflect(-l2, n), v)), _Gloss * 0.5) * 0.6;
                col += (s1 + s2) * _Highlight;
                // 뒤 배경(GrabPass)을 이미 섞었으므로 불투명으로 그린다 — 굴절된 배경 + 물 색 + 빛
                return fixed4(col + rim * _RimColor.rgb * 0.35, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
