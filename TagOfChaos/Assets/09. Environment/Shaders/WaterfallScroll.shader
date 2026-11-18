// 폭포 물줄기 판(Request1009Plan.md §89 HB2 F1): 흐름 노이즈 2장이 다른 속도로 아래로 흐른다. 가장자리(uv.x)·위아래(정점 색 a)는 투명.
// uv.y = 위(0) → 아래(1) 거리(m / _Tile). _PreviewTime은 에디터 미리보기 사진용(판에서는 0).
Shader "TagOfChaos/WaterfallScroll"
{
    Properties
    {
        _MainTex ("Flow noise (tiling)", 2D) = "gray" {}
        _Color ("Water", Color) = (0.42, 0.72, 1.0, 1)
        _DeepColor ("Water deep", Color) = (0.20, 0.48, 0.92, 1)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        _Speed1 ("Flow speed 1", Float) = 1.2
        _Speed2 ("Flow speed 2", Float) = 2.1
        _Stretch ("Vertical stretch", Float) = 0.35
        _EdgeFade ("Edge fade (u)", Range(0.01, 0.5)) = 0.18
        _Alpha ("Alpha", Range(0, 1)) = 0.85
        _PreviewTime ("Preview time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color, _DeepColor, _FoamColor;
            float _Speed1, _Speed2, _Stretch, _EdgeFade, _Alpha, _PreviewTime;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y + _PreviewTime;
                float2 uv1 = float2(i.uv.x * 1.0, i.uv.y * _Stretch - t * _Speed1 * _Stretch);
                float2 uv2 = float2(i.uv.x * 1.7 + 0.37, i.uv.y * _Stretch * 1.3 - t * _Speed2 * _Stretch);
                float n1 = tex2D(_MainTex, TRANSFORM_TEX(uv1, _MainTex)).r;
                float n2 = tex2D(_MainTex, TRANSFORM_TEX(uv2, _MainTex)).r;
                float foam = smoothstep(0.55, 0.9, n1 * n2 * 1.9);
                fixed3 water = lerp(_DeepColor.rgb, _Color.rgb, saturate(n1 * 0.8 + 0.2));
                fixed3 col = lerp(water, _FoamColor.rgb, foam);
                float edge = smoothstep(0, _EdgeFade, i.uv.x) * smoothstep(0, _EdgeFade, 1 - i.uv.x);
                float a = saturate(_Alpha * (0.65 + 0.35 * n2) + foam * 0.3) * edge * i.color.a;
                fixed4 c = fixed4(col, a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
