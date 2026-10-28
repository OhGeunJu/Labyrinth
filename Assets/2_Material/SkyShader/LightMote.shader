// 볕 속에 뜬 먼지 — 채광 구멍(2026-10-11).
//
// 텍스처 없는 둥근 점을 더하기로 찍는다. 파티클의 색·알파(정점 색)를 그대로 탄다.
Shader "DungeonColony/LightMote"
{
    Properties
    {
        [HDR] _BaseColor ("빛깔", Color) = (1, 0.96, 0.84, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+11" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Mote"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv         = v.uv;
                o.color      = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half d = length(i.uv - 0.5) * 2.0;
                half k = saturate(1.0 - d);
                k = k * k * i.color.a * _BaseColor.a;
                return half4(_BaseColor.rgb * i.color.rgb * k, 0);
            }
            ENDHLSL
        }
    }
}
