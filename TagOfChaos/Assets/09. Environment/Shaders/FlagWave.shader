// 펄럭이는 깃발(Request1009Plan.md §89.22): 정점 색 R = 흔들림 세기(깃대 0 ~ 끝 1), UV x = 깃대에서 끝까지(물결 위상).
// 법선 방향으로 물결을 밀고, 끝으로 갈수록 크게. 양면(Cull Off). Built-in 표면 셰이더 + GPU 인스턴싱.
Shader "TagOfChaos/FlagWave"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Amp ("Wave (m)", Float) = 0.18
        _Waves ("Waves along flag", Float) = 1.6
        _Speed ("Speed", Float) = 4.5
        _Glossiness ("Smoothness", Range(0,1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma multi_compile_instancing
        #pragma target 3.0
        sampler2D _MainTex; fixed4 _Color; float _Amp, _Waves, _Speed, _Glossiness;
        struct Input { float2 uv_MainTex; float facing : VFACE; };
        void vert (inout appdata_full v)
        {
            float w = v.color.r;
            float u = v.texcoord.x;
            float t = _Time.y * _Speed;
            // 세계 좌표로 민다(FBX 가져오기 배율 100이면 물체 좌표 0.18 = 18 m가 되어 깃발이 길게 늘어났다)
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 wn = normalize(UnityObjectToWorldNormal(v.normal));
            float ph = dot(unity_ObjectToWorld._m03_m13_m23, float3(0.37, 0, 0.29));      // 깃발마다 위상이 다르게
            float wave = sin(u * _Waves * 6.2832 - t + ph) + 0.35 * sin(u * _Waves * 13.1 - t * 1.7 + ph);
            wp += wn * wave * _Amp * w;
            wp.y -= 0.04 * w * (1 + sin(t * 0.7 + ph));                                  // 끝이 살짝 처짐
            v.vertex.xyz = mul(unity_WorldToObject, float4(wp, 1)).xyz;
        }
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb; o.Smoothness = _Glossiness; o.Metallic = 0;
            if (IN.facing < 0) o.Normal = float3(0, 0, -1);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
