Shader "ColorTag/PlayerPaintedSkin"
{
    Properties
    {
        _MainTex ("Base Skin", 2D) = "white" {}
        _PaintTex ("Paint Canvas", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            sampler2D _PaintTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            // 원본 피부 텍스처 위에, 캐릭터가 라운드 중 칠한 부위(_PaintTex의 알파로 판별)만 페인트 색으로 덧입힌다 (5.2)
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, i.uv);
                fixed4 paint = tex2D(_PaintTex, i.uv);

                fixed3 albedo = lerp(baseColor.rgb, paint.rgb, paint.a);

                float ndotl = saturate(dot(normalize(i.worldNormal), normalize(_WorldSpaceLightPos0.xyz)));
                fixed3 lit = albedo * (ndotl * _LightColor0.rgb + unity_AmbientSky.rgb);

                return fixed4(lit, baseColor.a);
            }
            ENDCG
        }
    }
    Fallback "Standard"
}
