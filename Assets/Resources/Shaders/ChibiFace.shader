// **치비 이목구비 — 채널을 쪼갠 마스크 한 장으로 눈·눈썹·입을 칠한다**
// (2026-09-18, 사용자가 가져온 글: *"눈 색깔이나 에미션, 눈 흰자나 혀 색깔같이 스킨 컬러의
//  블렌딩 해야하는 처리 등등 커스텀 쉐이더에서 활용할 채널 스플릿 맵 형식 … 이목구비를 그릴
//  영역 메쉬만 따로 뜯어서 얼굴보다 살짝 위에 … UV맵 해상도를 줄이고 알파 클리핑"*).
//
// ## 그림은 색이 아니라 **마스크 넷**이다
//
//     R  잉크   — 눈동자·눈썹·입선. `_InkColor`로 칠한다
//     G  흰자   — 눈 흰자·이·하이라이트. `_WhiteColor`
//     B  강조   — 홍채·혀. **재질마다 색이 다르다**(눈 재질은 눈 색, 입 재질은 혀 색) · 빛날 수 있다
//     A  자르기 — 0.5 아래는 버린다(반투명 없음)
//
//   셋 다 0이고 A만 1이면 **살색**이 칠해진다(볼 터치·입 안쪽처럼 피부와 섞일 자리).
//
// 색을 그림에 굽지 않는 이유가 이 형식의 전부다 — **눈 색을 사람마다 바꾸는 데 그림이 한 장도
// 안 는다.** 그림에 색을 구우면 눈 색 다섯 × 표정 N만큼 그림이 불어난다.
//
// ## 칸을 골라 표정을 바꾼다
//
// 아틀라스는 `_Cells` × `_Cells` 칸이고 판의 UV는 **제 기본 칸**을 가리킨다. 표정은 판마다
// `_CellOffset`(칸 단위)을 얹는 일이다 — `PawnLook.SetCell`. 왼눈·오른눈이 **칸이 따로**라
// 한쪽만 옮기면 윙크다(좌우 반전으로 칸을 아끼는 길은 그래서 안 썼다).
//
// ## 빛은 살과 같은 계단을 탄다
//
// 이목구비만 빛을 안 타면 **어두운 굴에서 흰자가 얼굴보다 밝게 떠 보인다.** 그래서
// `ToonOutline`과 같은 계단·같은 주변광 바닥을 쓴다. 발광(`_AccentGlow`)은 그 위에 더한다.
//
// ## 깊이 패스를 뺄 수 없다 — 자르기까지 같이
//
// 빼면 판이 깊이 텍스처에서 사라져 안개가 그 자리를 뒤의 것으로 읽는다. 깊이 패스에서도
// **같이 잘라야** 한다 — 안 자르면 판의 투명한 네모가 깊이에 찍혀 **눈 둘레에 안개 네모**가 뜬다.
Shader "DungeonColony/ChibiFace"
{
    Properties
    {
        [MainTexture] _BaseMap("아틀라스(RGBA 마스크)", 2D) = "black" {}
        _Cells("한 변의 칸 수", Float) = 4
        _CellOffset("칸 옮기기(칸 단위)", Vector) = (0,0,0,0)

        _InkColor("잉크 (R)", Color) = (0.08,0.07,0.08,1)
        _WhiteColor("흰자 (G)", Color) = (0.97,0.96,0.94,1)
        _AccentColor("강조 — 홍채·혀 (B)", Color) = (0.36,0.24,0.16,1)
        _AccentGlow("강조 발광", Range(0,4)) = 0
        _SkinColor("살 (셋 다 0)", Color) = (0.95,0.82,0.66,1)

        _ShadeColor("그늘 색조", Color) = (0.70,0.72,0.82,1)
        _ShadeThreshold("그늘 문턱", Range(0,1)) = 0.42
        _ShadeSoftness("그늘 경계 부드럽기", Range(0.001,0.3)) = 0.03
        _Cutoff("자르기", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float  _Cells;
            float4 _CellOffset;
            float4 _InkColor, _WhiteColor, _AccentColor, _SkinColor, _ShadeColor;
            float  _AccentGlow, _ShadeThreshold, _ShadeSoftness, _Cutoff;
        CBUFFER_END

        // 폰의 최저 밝기 — **재질이 아니라 세계가 든다**(`UnderworldLighting`). 상수(0.35)였을 때는 굴을
        // 아무리 어둡게 내려도 폰만 대낮처럼 떠서 밤으로 안 읽혔다(2026-10-11).
        half _PawnFloor;

        // 제 기본 칸 + 옮긴 칸. 판의 UV는 이미 기본 칸 안에 있다.
        float2 CellUV(float2 uv) { return uv + _CellOffset.xy / max(_Cells, 1.0); }

        half4 Mask(float2 uv)
        {
            half4 m = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, CellUV(uv));
            clip(m.a - _Cutoff);
            return m;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            V Vert(A v)
            {
                V o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                return o;
            }

            half ToonStep(half3 n, half3 dir, half atten)
            {
                half ndl = saturate(dot(n, dir));
                return smoothstep(_ShadeThreshold, _ShadeThreshold + _ShadeSoftness, ndl) * saturate(atten);
            }

            half4 Frag(V i) : SV_Target
            {
                half4 m = Mask(i.uv);

                // 살 → 강조 → 흰자 → 잉크 순으로 덮는다(나중 것이 이긴다).
                half3 col = _SkinColor.rgb;
                col = lerp(col, _AccentColor.rgb, m.b);
                col = lerp(col, _WhiteColor.rgb,  m.g);
                col = lerp(col, _InkColor.rgb,    m.r);

                // 살과 **같은 계단**을 탄다 — 안 타면 굴에서 흰자가 얼굴보다 밝게 뜬다.
                half3 n = normalize(i.normalWS);
                Light mainLight = GetMainLight();
                // 꺼진 해는 밝은 면을 못 고른다 — 근거는 `ToonOutline`의 같은 자리.
                half sunOn = saturate(max(mainLight.color.r, max(mainLight.color.g, mainLight.color.b)));
                half lit = ToonStep(n, mainLight.direction, mainLight.distanceAttenuation * sunOn);
                // **등을 도는 문은 URP의 매크로다**(2026-10-12). PC 렌더러는 Forward+라 등이 물체가 아니라
                // 화면 칸(cluster)에 매달려 있다 — 예전처럼 개수로 `for`를 돌면 한 개도 안 돈다.
                //
                // **밝기도 여기서 잰다**(`bright`). 전에는 등이 *밝은 면이냐 그늘이냐*만 골랐고 실제 밝기는
                // 주변광 바닥값이 다 냈다 — 굴을 어둡게 내리자 횃불 곁에 서도 폰이 검었다.
                // 법선은 안 본다: 불빛 안에 섰으면 몸 전체가 밝고, 면을 가르는 것은 계단(`lit`)이 한다.
                half bright = 0;
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light extra = GetAdditionalLight(lightIndex, i.positionWS);
                    half reach = extra.distanceAttenuation * extra.shadowAttenuation;
                    lit    = max(lit, ToonStep(n, extra.direction, reach));
                    bright = max(bright, reach * max(extra.color.r, max(extra.color.g, extra.color.b)));
                LIGHT_LOOP_END
                col = lerp(col * _ShadeColor.rgb, col, lit);
                // **불빛 안에서도 0.6까지다** — 폰의 색은 주변광 0.35~0.45 아래에서 맞춘 것이라, 1까지 올리면
                // 횃불 곁에서 색이 날아간다.
                col *= max(max(SampleSH(n), _PawnFloor), saturate(bright) * 0.6);

                // 발광은 빛과 무관하게 더한다 — 어둠 속에서 눈만 빛나는 자리.
                col += _AccentColor.rgb * _AccentGlow * m.b;
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex DVert
            #pragma fragment DFrag
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V DVert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }
            half4 DFrag(V i) : SV_Target { Mask(i.uv); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex NVert
            #pragma fragment NFrag
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };
            V NVert(A v)
            {
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }
            half4 NFrag(V i) : SV_Target { Mask(i.uv); return half4(normalize(i.normalWS) * 0.5 + 0.5, 0); }
            ENDHLSL
        }
    }
    Fallback Off
}
