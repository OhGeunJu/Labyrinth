// 채광 구멍의 빛기둥 — 위에서 쏟아지는 볕(2026-10-11).
//
// **더하기로 섞는다** — 빛은 뒤의 것을 가리지 않고 밝히기만 한다(알파로 섞으면 뿌연 유리 기둥이 된다).
// 텍스처가 없다: 빛줄기는 기둥 둘레(uv.x)의 사인 몇 겹이고, 그것이 천천히 돌아서 볕이 일렁인다.
//
//   · 세로(uv.y)  — 메시의 정점 알파가 든다: 구멍(위)에서 밝고 바닥으로 갈수록 풀린다
//   · 둘레(uv.x)  — 빛줄기. `_Streak`가 0이면 고른 기둥이다
//   · 보는 각     — 옆면을 스치듯 보는 자리(윤곽)에서 옅어진다. 안 그러면 원뿔의 테두리가 선으로 선다
Shader "DungeonColony/LightShaft"
{
    Properties
    {
        [HDR] _BaseColor ("빛깔 (알파 = 짙기)", Color) = (1, 0.96, 0.84, 0.5)
        _Streak ("빛줄기 세기", Range(0, 1)) = 0.7
        _Drift  ("일렁이는 속도", Float) = 0.035
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Shaft"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _Streak;
                half  _Drift;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float3 normalWS   : TEXCOORD1;
                float3 viewWS     : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 ws    = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv         = v.uv;
                o.color      = v.color;
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS     = GetWorldSpaceViewDir(ws);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 빛줄기 — 주기가 서로 안 맞는 사인 셋. 둘레를 한 바퀴 돌면 이어져야 하므로 **정수 배**만 쓴다.
                float a = i.uv.x * 6.2831853;
                float t = _Time.y * _Drift * 6.2831853;
                half s = 0.5
                       + 0.27 * sin(a *  5.0 + t)
                       + 0.15 * sin(a * 11.0 - t * 1.7 + 1.3)
                       + 0.08 * sin(a * 23.0 + t * 2.3 + 4.1);
                s = saturate(s);
                s = lerp(1.0, s * s * 1.8, _Streak);

                half face = abs(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half edge = smoothstep(0.0, 0.45, face);

                half k = i.color.a * _BaseColor.a * s * edge;
                return half4(_BaseColor.rgb * i.color.rgb * k, 0);
            }
            ENDHLSL
        }
    }
}
