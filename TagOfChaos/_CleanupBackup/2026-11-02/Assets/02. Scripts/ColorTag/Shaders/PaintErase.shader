Shader "ColorTag/PaintErase"
{
    // PaintStamp.shader와 같은 방식(스탬프 영역 사각형만 그리고 원 밖은 버림, Bug-fix-plan.md §30.5 F5)으로
    // 원 안을 잠금과 무관하게 투명(미도색)으로 되돌린다.
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 local : TEXCOORD0; // 스탬프 중심 기준 -1~1(원 판정용)
            };

            struct v2f
            {
                float2 local : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.local = v.local;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                clip(1 - dot(i.local, i.local)); // 스탬프 원 밖은 그리지 않음
                return fixed4(0, 0, 0, 0);       // 잠금 무시하고 항상 투명(미도색)으로 되돌림
            }
            ENDCG
        }
    }
}
