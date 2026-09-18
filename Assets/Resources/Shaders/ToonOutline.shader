// **림월드의 얼굴을 3D로 — 검은 외곽선 + 평면 음영**
// (2026-09-18, 사용자가 레퍼런스를 가져왔다: *"이 캐디가 참 좋은거 같아 림월드에 3d잖아"*).
//
// 저 인상을 만드는 것은 셋이고 **비례 말고 둘이 여기 있다**:
//
//   1. **두꺼운 검은 외곽선** — 뒤집힌 껍데기(inverted hull). 면을 법선 쪽으로 부풀리고
//      **앞면을 버리면** 실루엣 둘레에만 남는다.
//   2. **평면 음영** — 빛을 계단 하나로 접는다. 굴곡이 색으로 안 읽히고 **면이 통째로 한 색**
//      이라 만화 그림이 된다.
//
// ## 어기면 조용히 깨지는 것
//
// - **선 굵기는 월드 기준이고 화면 최소를 둔다**(2026-09-18, 사용자: *"가까울 때도 외곽선이
//   사진처럼 좀 더 두꺼우면 좋겠어 … 가까이로 가면 외곽선이 너무 얇아져서 그냥 선이야"*).
//   처음엔 화면 기준(`w`를 곱한다)으로 뒀는데, 그건 **픽셀 수가 일정**하다는 뜻이라 당겨서
//   폰이 커지면 **선만 그대로라 상대적으로 가늘어진다.** 저 그림의 선은 **몸에 비례**한다 —
//   그래서 미터로 두되(6cm — 처음 3cm에서 사용자가 두 배로), 멀어져 사라지지 않게 **화면 최소**를 밑에 깐다. 둘 중
//   **화면에서 굵은 쪽**을 고른다: 가까이서는 월드가, 멀리서는 최소가 이긴다.
// - **외곽선 패스가 먼저다.** 뒤에 두면 깊이가 이미 찬 뒤라 선이 제 몸에 가려 안 보인다.
// - **`DepthOnly`·`DepthNormals`를 뺄 수 없다** — 빼면 폰이 **깊이 텍스처에서 사라져**
//   전장의 안개가 폰 자리를 *그 뒤에 있는 것*으로 읽는다(`FogOfWar`가 깊이로 월드 자리를
//   되짚는다). 바위 셰이더가 자르기를 **그리는 패스 전부**에 걸어야 했던 것과 같은 자리다.
// - **추가 광원을 돌아야 한다** — 지하에는 해가 없고 **횃불이 광원**이다(`LightSource`).
//   메인 라이트만 보면 굴에서 폰이 주변광 색으로만 뜬다.
// - **그늘은 어둡게 하는 것이 아니라 색조를 얹는 것이다** — 굴이 이미 어두워서(주변광
//   0.30~0.62) 곱해 내리면 폰이 통째로 검게 눌린다.
// - **`Resources`에 둔다** — 코드가 이름으로 찾는 셰이더는 빌드에서 잘려 나간다
//   (`RuntimeShaders`). 재질이 가리키면 안 잘리지만, 여기 두면 그 둘이 다 지켜진다.
Shader "DungeonColony/ToonOutline"
{
    Properties
    {
        [MainColor] _BaseColor("색", Color) = (1,1,1,1)
        _ShadeColor("그늘 색조", Color) = (0.62,0.66,0.78,1)
        _ShadeThreshold("그늘 문턱", Range(0,1)) = 0.42
        _ShadeSoftness("그늘 경계 부드럽기", Range(0.001,0.3)) = 0.03
        _OutlineColor("외곽선 색", Color) = (0.04,0.04,0.05,1)
        _OutlineWidth("외곽선 굵기(m)", Range(0,0.12)) = 0.06
        _OutlineMin("외곽선 최소 굵기(화면 기준)", Range(0,0.03)) = 0.008

        // **면 버리기** — 기본은 뒷면을 버린다(Back). 머리카락처럼 **한 겹짜리 열린 껍질**은 Off로 둔다:
        // 안 그러면 트인 자리로 **안쪽 면이 보여야 할 곳이 비어** 뒤가 비친다(2026-09-18, 사용자:
        // *"약간씩 뚫리는게 기분나빠"*). 닫힌 몸(몸통·머리)은 Back이 맞다 — 안쪽이 안 보인다.
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("면 버리기", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _ShadeColor;
            float  _ShadeThreshold;
            float  _ShadeSoftness;
            float4 _OutlineColor;
            float  _OutlineWidth;
            float  _OutlineMin;
        CBUFFER_END
        ENDHLSL

        // ── 1. 외곽선 — 뒤집힌 껍데기 ───────────────────────────────────────
        Pass
        {
            Name "Outline"
            // **`UniversalForward`를 쓰면 안 된다**(2026-09-18). URP는 한 태그에 **패스 하나만**
            // 그린다 — 둘 다 그 태그를 달면 **먼저 있는 외곽선만 그려져 폰이 통째로 검은
            // 실루엣**이 된다(화면에는 *"까만 덩어리"* 하나로만 보인다).
            //
            // `SRPDefaultUnlit`은 URP가 불투명을 그릴 때 **같이 도는 태그**이고, 태그 목록에서
            // `UniversalForward`보다 **앞**이라 외곽선이 먼저 그려진다(그래야 살이 그 위를 덮는다).
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag

            struct OutlineIn  { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct OutlineOut { float4 positionCS : SV_POSITION; };

            OutlineOut OutlineVert(OutlineIn v)
            {
                OutlineOut o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(v.normalOS);

                nrmWS = normalize(nrmWS);

                float4 clipBase  = TransformWorldToHClip(posWS);
                // 월드 굵기: 법선 쪽으로 `_OutlineWidth`m 민 자리를 클립으로 옮겨 **차이**만 쓴다.
                float4 clipThick = TransformWorldToHClip(posWS + nrmWS * _OutlineWidth);
                float2 dWorld    = clipThick.xy - clipBase.xy;

                // 화면 최소: `w`를 곱해 원근 나눗셈을 상쇄한 **픽셀 일정** 굵기.
                float3 nrmCS   = TransformWorldToHClipDir(nrmWS);
                float2 nrm2    = nrmCS.xy / max(length(nrmCS.xy), 1e-5);
                float2 dScreen = nrm2 * _OutlineMin * clipBase.w;

                // **화면에서 굵은 쪽** — 가까이서는 월드가, 멀리서는 최소가 이긴다.
                float2 d = dot(dWorld, dWorld) > dot(dScreen, dScreen) ? dWorld : dScreen;

                o.positionCS = clipBase;
                o.positionCS.xy += d;
                return o;
            }

            half4 OutlineFrag(OutlineOut i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        // ── 2. 살 — 계단 하나로 접은 빛 ────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex LitVert
            #pragma fragment LitFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct LitIn  { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct LitOut
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            LitOut LitVert(LitIn v)
            {
                LitOut o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            // 빛 하나를 **계단 하나**로 접는다. 세기를 안 쓰는 것이 요점이다 — 묻는 것은
            // *얼마나 밝나*가 아니라 **비추나 마나**다.
            half ToonStep(half3 n, half3 dir, half atten)
            {
                half ndl = saturate(dot(n, dir));
                return smoothstep(_ShadeThreshold, _ShadeThreshold + _ShadeSoftness, ndl) * saturate(atten);
            }

            half4 LitFrag(LitOut i, bool front : SV_IsFrontFace) : SV_Target
            {
                // 양면일 때 **안쪽 면은 법선을 뒤집는다** — 안 뒤집으면 커튼 안쪽이 바깥 면의 빛으로
                // 칠해져 트인 자리에서 얼룩진다.
                half3 n = normalize(i.normalWS) * (front ? 1 : -1);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half lit = ToonStep(n, mainLight.direction,
                                    mainLight.shadowAttenuation * mainLight.distanceAttenuation);

                // **횃불을 돈다** — 지하에는 해가 없다.
                uint count = GetAdditionalLightsCount();
                for (uint k = 0u; k < count; k++)
                {
                    Light extra = GetAdditionalLight(k, i.positionWS);
                    lit = max(lit, ToonStep(n, extra.direction,
                                            extra.distanceAttenuation * extra.shadowAttenuation));
                }

                half3 shade = _BaseColor.rgb * _ShadeColor.rgb;
                half3 col   = lerp(shade, _BaseColor.rgb, lit);

                // 주변광이 **바닥을 든다** — 굴의 주변광이 폰의 최저 밝기다.
                col *= max(SampleSH(n), 0.35);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ── 깊이 — 안개와 화면 효과가 폰을 볼 수 있게 ──────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthIn  { float4 positionOS : POSITION; };
            struct DepthOut { float4 positionCS : SV_POSITION; };

            DepthOut DepthVert(DepthIn v)
            {
                DepthOut o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }
            half4 DepthFrag(DepthOut i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag

            struct NormalsIn  { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct NormalsOut { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            NormalsOut NormalsVert(NormalsIn v)
            {
                NormalsOut o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 NormalsFrag(NormalsOut i) : SV_Target
            {
                return half4(normalize(i.normalWS) * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowIn  { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowOut { float4 positionCS : SV_POSITION; };

            ShadowOut ShadowVert(ShadowIn v)
            {
                ShadowOut o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(v.normalOS);

                // **`ApplyShadowBias`는 클립 좌표를 안 돌려준다** — 그림자 여드름을 피하려고
                // **법선 쪽으로 밀어 낸 월드 좌표**다. 클립으로 옮기는 것은 여기서 해야 한다.
                float3 biasedWS = ApplyShadowBias(posWS, nrmWS, _LightDirection);
                o.positionCS = TransformWorldToHClip(biasedWS);

                // 근평면 너머로 밀린 정점을 평면에 눌러 둔다 — 안 누르면 그림자 맵에서
                // **잘려 나가 그 자리에 구멍**이 뚫린다.
            #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #else
                o.positionCS.z = max(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif
                return o;
            }
            half4 ShadowFrag(ShadowOut i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
