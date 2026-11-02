Shader "ColorTag/PaintStamp"
{
    // 캔버스 전체를 읽어 다시 쓰던 방식(스탬프 1개당 512x512 Blit 2회) 대신, PlayerPaintCanvas가 스탬프 영역만 덮는
    // 사각형을 GL로 한 번에 여러 개 그리고 원 밖은 버린다(Bug-fix-plan.md §30.5 F5). 스탬프 색은 정점 색, 원 내부 좌표는
    // TEXCOORD0(-1~1)로 전달된다. 잠금 규칙은 캔버스를 읽는 대신 하드웨어 블렌딩으로 처리한다:
    //   일반 붓(_RespectLock=1): Blend OneMinusDstAlpha DstAlpha — 이미 칠해진(알파=1) 픽셀은 그대로, 미도색만 칠함
    //   강제 도포(_RespectLock=0): Blend One Zero — 잠금 무시하고 항상 덮어씀
    // 블렌드 값은 PlayerPaintCanvas가 _RespectLock을 보고 _SrcBlend/_DstBlend에 설정한다.
    Properties
    {
        // 1 = 일반 붓(브러시): 이미 칠해진(알파=1) 픽셀은 건드리지 않음
        // 0 = 라운드 확정 재도색: 잠금을 무시하고 항상 덮어씀
        _RespectLock ("Respect Lock", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 8 // OneMinusDstAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 7 // DstAlpha
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 local : TEXCOORD0; // 스탬프 중심 기준 -1~1(원 판정용), 전체 도포는 0
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 local : TEXCOORD0;
                float4 color : COLOR;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.local = v.local;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                clip(1 - dot(i.local, i.local)); // 스탬프 원 밖은 그리지 않음
                return fixed4(i.color.rgb, 1);   // 새로 칠하고 알파를 1로 고정(잠금)
            }
            ENDCG
        }
    }
}
