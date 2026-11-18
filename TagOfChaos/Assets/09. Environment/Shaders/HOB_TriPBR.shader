// Heaven Of Bakery HQ 재질(§89 품질 개선): 이음새 없는 타일 텍스처를 월드 축 3방향 투영(UV 필요 없음)으로 입힌다.
// 정점 색 RGB = Blender에서 구운 접촉 그림자(AO). 위를 보는 면에는 이끼 층(_MossAmount)을 노이즈 경계로 섞는다. 표준 PBR 조명.
Shader "TagOfChaos/HOB_TriPBR"
{
    Properties
    {
        _MainTex ("Albedo (RGB) Smoothness (A)", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Tile ("Tile size (m)", Float) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
        _SmoothScale ("Smoothness scale", Range(0,2)) = 1
        _BumpScale ("Normal strength", Range(0,2)) = 1
        _AOStrength ("Vertex AO strength", Range(0,1)) = 0.8
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _MossTex ("Moss albedo", 2D) = "white" {}
        _MossBump ("Moss normal", 2D) = "bump" {}
        _MossTile ("Moss tile (m)", Float) = 2
        _MossAmount ("Moss amount (0 = off)", Range(0,1)) = 0
        _MossEdge ("Moss edge sharpness", Range(1,20)) = 8
        _Blend ("Projection blend sharpness", Range(1,16)) = 6
        _MacroStrength ("Large-scale variation", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows addshadow
        #pragma multi_compile_instancing
        #pragma shader_feature_local _MOSS_ON
        #pragma target 3.5
        sampler2D _MainTex, _BumpMap, _MossTex, _MossBump;
        half4 _Color, _EmissionColor;
        float _MacroStrength, _Tile, _Metallic, _SmoothScale, _BumpScale, _AOStrength, _MossTile, _MossAmount, _MossEdge, _Blend;

        struct Input { float3 objPos; float3 objNrm; float3 worldNormal; float4 color : COLOR; INTERNAL_DATA };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // world axes in metres (HOB FBX meshes keep Blender's Z-up inside a rotated child, so object space would lie on its side).
            // Props are placed once (sub-maps move once at Awake), so world-space projection never swims during play.
            o.objPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.objNrm = UnityObjectToWorldNormal(v.normal);
        }

        float3 weights (float3 n) { float3 w = pow(abs(n), _Blend); return w / (w.x + w.y + w.z + 1e-5); }

        // triplanar sample: albedo + whiteout-blended normal (returned in object space)
        void tri (sampler2D alb, sampler2D nrm, float3 p, float3 n, float tile, out float4 col, out float3 nObj)
        {
            float3 w = weights(n); float2 ux = p.zy / tile, uy = p.xz / tile, uz = p.xy / tile;
            col = tex2D(alb, ux) * w.x + tex2D(alb, uy) * w.y + tex2D(alb, uz) * w.z;
            float3 tx = UnpackScaleNormal(tex2D(nrm, ux), _BumpScale), ty = UnpackScaleNormal(tex2D(nrm, uy), _BumpScale), tz = UnpackScaleNormal(tex2D(nrm, uz), _BumpScale);
            tx = float3(tx.xy + n.zy, abs(tx.z) * n.x); ty = float3(ty.xy + n.xz, abs(ty.z) * n.y); tz = float3(tz.xy + n.xy, abs(tz.z) * n.z);
            nObj = normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(IN.objNrm);
            float4 c; float3 nObj;
            tri(_MainTex, _BumpMap, IN.objPos, n, _Tile, c, nObj);
            float smooth = c.a * _SmoothScale;
            float3 albedo = c.rgb * _Color.rgb;
            // large-scale variation: the same tile seen 11x bigger vs its average colour (breaks visible repetition on big surfaces)
            float2 macroUV = IN.objPos.xz / (_Tile * 11.3) + IN.objPos.y * 0.013;
            float lumLow = dot(tex2D(_MainTex, macroUV).rgb, float3(0.3, 0.59, 0.11));
            float lumAvg = max(dot(tex2Dlod(_MainTex, float4(0.5, 0.5, 0, 12)).rgb, float3(0.3, 0.59, 0.11)), 0.05);
            albedo *= lerp(1.0, clamp(lumLow / lumAvg, 0.6, 1.4), _MacroStrength);
            #if _MOSS_ON
            float4 mc; float3 mn;
            tri(_MossTex, _MossBump, IN.objPos, n, _MossTile, mc, mn);
            float3 wn = n;
            float edge = wn.y + (mc.g - 0.45) * 0.8 - (1.0 - _MossAmount);
            float m = saturate(edge * _MossEdge);
            albedo = lerp(albedo, mc.rgb, m); nObj = normalize(lerp(nObj, mn, m)); smooth = lerp(smooth, mc.a, m);
            #endif
            albedo *= lerp(1.0, IN.color.rgb, _AOStrength);
            // object-space normal → world → tangent space for the surface shader
            float3 nW = nObj;
            float3 t = WorldNormalVector(IN, float3(1, 0, 0)), b = WorldNormalVector(IN, float3(0, 1, 0)), z = WorldNormalVector(IN, float3(0, 0, 1));
            o.Normal = normalize(float3(dot(nW, t), dot(nW, b), dot(nW, z)));
            o.Albedo = albedo;
            o.Metallic = _Metallic;
            o.Smoothness = saturate(smooth);
            o.Occlusion = lerp(1.0, IN.color.r, _AOStrength * 0.5);
            o.Emission = _EmissionColor.rgb;
        }
        ENDCG
    }
    FallBack "Standard"
}
