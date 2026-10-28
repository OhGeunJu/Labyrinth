// **전장의 안개 — 화면 전체에 한 번 덮는다**(2026-09-15).
//
// 지형·바위·바닥·건물·폰·물건이 **셰이더가 전부 다르다** — 셰이더마다 안개를 넣으면 하나는
// 반드시 빠지고, 그 물건만 안개 속에서 멀쩡히 보인다. 그래서 **불투명을 다 그린 뒤 화면을
// 한 번 덮는다**: 깊이에서 월드 자리를 되짚고, 그 칸이 보이는지를 `FogOfWar`가 구운 텍스처에서
// 읽는다.
//
// **곱한다** — 안 가 본 곳은 검게(사용자: *"안 가 본 길 = 검은색"*), 가 본 곳은 원래대로.
// 가 본 곳에서 **안 보여야 하는 것(남의 폰)은 `FogOfWar`가 렌더러를 끈다** — 셰이더가 할
// 일이 아니다(땅은 보이는데 폰만 안 보이는 것을 화면 한 장으로는 못 가른다).
//
// 텍스처 두 채널: **R = 가 봤나 · G = 지금 보나**.
//
// **`Resources`에 둔다** — 코드가 이름으로 찾는 셰이더는 빌드에서 잘려 나간다(`RuntimeShaders`).
// `Resources.Load`는 참조라서 잘리지 않는다.
Shader "DungeonColony/FogOfWar"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        // 곱하기 — 결과 = 화면 × 출력색.
        Blend DstColor Zero

        Pass
        {
            Name "FogOfWar"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_FogVisTex);
            SAMPLER(sampler_FogVisTex);

            float4 _FogVisRect;   // xy = 창의 월드 원점(XZ) · zw = 1 / 창 크기(m)
            float4 _FogDim;       // x = 안 가 본 곳 밝기 · y = 가 봤지만 지금 안 보는 곳 밝기

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;

            #if UNITY_REVERSED_Z
                float depth = SampleSceneDepth(uv);
            #else
                float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
            #endif
                float3 ws = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                float2 fuv = (ws.xz - _FogVisRect.xy) * _FogVisRect.zw;

                // 창 밖은 안 보이는 곳이다 — 샘플러의 가장자리 늘이기에 기대면 창 끝의 한 줄이
                // 바깥으로 끝없이 번진다.
                float inside = step(0.0, fuv.x) * step(0.0, fuv.y) * step(fuv.x, 1.0) * step(fuv.y, 1.0);
                float2 s = SAMPLE_TEXTURE2D(_FogVisTex, sampler_FogVisTex, fuv).rg * inside;
                float explored = s.r, visible = s.g;

                float seen = lerp(_FogDim.y, 1.0, visible);     // 가 본 곳: 지금 보면 1
                float b    = lerp(_FogDim.x, seen, explored);   // 안 가 본 곳은 검다
                return half4(b, b, b, 1.0);
            }
            ENDHLSL
        }
    }
}
