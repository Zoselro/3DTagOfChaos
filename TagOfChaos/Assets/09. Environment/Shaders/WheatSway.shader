// 밀 흔들림(Request1009Plan.md §89 HB1): 정점 색 R = 흔들림 세기(뿌리 0 ~ 이삭 1, Blender sway 색), G = 포기 위상. Built-in 표면 셰이더 + GPU 인스턴싱.
Shader "TagOfChaos/WheatSway"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff (cards)", Range(0,1)) = 0.4
        _WindDir ("Wind dir (xz)", Vector) = (1,0,0.35,0)
        _Sway ("Sway (m)", Float) = 0.06
        _Gust ("Gust (m)", Float) = 0.10
        _Speed ("Speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow alphatest:_Cutoff
        #pragma multi_compile_instancing
        #pragma target 3.0
        sampler2D _MainTex; fixed4 _Color; float4 _WindDir; float _Sway, _Gust, _Speed;
        struct Input { float2 uv_MainTex; };
        void vert (inout appdata_full v)
        {
            float w = v.color.r;
            float ph = v.color.g * 6.2832;
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float t = _Time.y * _Speed;
            float gust = 0.5 + 0.5 * sin(t * 1.57 + wp.x * 0.6);
            float sway = sin(t * 3.14 + wp.x * 1.3 + wp.z * 0.9 + ph);
            float2 d = normalize(_WindDir.xz);
            float amp = w * (_Sway + _Gust * gust);
            float2 off = d * amp * (0.6 + 0.4 * sway) + float2(-d.y, d.x) * amp * 0.25 * sin(t * 6.28 + ph);
            wp.xz += off;
            wp.y -= 0.35 * w * dot(off, off);
            v.vertex.xyz = mul(unity_WorldToObject, float4(wp, 1)).xyz;
        }
        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb; o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
