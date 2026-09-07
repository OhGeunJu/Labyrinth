#ifndef DUNGEONCOLONY_ROCK_DEPTH_ONLY_PASS_INCLUDED
#define DUNGEONCOLONY_ROCK_DEPTH_ONLY_PASS_INCLUDED

// URP의 `DepthOnlyPass.hlsl` 사본이다. **고친 것은 자르기 두 줄뿐이고 나머지는 복사다**
// (URP를 올릴 때 병합할 것을 최소로 — `RockLitForwardPass.hlsl`과 같은 규칙).
//
// **왜 이 패스까지 잘라야 하나**(2026-09-07): 자르기를 `ForwardLit`에만 걸어 두었더니
// **깊이 버퍼에는 안 잘린 24m 벽이 그대로 들어갔다.** 색은 잘린 것을 그리는데 깊이는 통짜라
// 두 벌이 어긋나고, 그 어긋남이 화면에서는 *"야를 돌리면 평평한 뚜껑 위에 세로 무늬가
// 뜬다"*로 보인다(사용자). 원인이 셰이더가 아니라 **패스 하나를 빠뜨린 것**이라, 자르는
// 규칙을 아무리 만져도 안 없어진다.
//
// **같은 물음에는 같은 답이어야 한다** — 무엇이 그려지는가를 패스마다 따로 답하면 반드시
// 어긋난다. 깎는 함수는 `RockCut.hlsl` 한 벌이고 여기서도 그것을 부른다.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif
#include "RockCut.hlsl"

struct Attributes
{
    float4 position     : POSITION;
    float3 normalOS     : NORMAL;   // ← 더한 것: 뚜껑인지 옆면인지는 법선이 답한다
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    #if defined(_ALPHATEST_ON)
        float2 uv       : TEXCOORD0;
    #endif
    float3 positionWS   : TEXCOORD1;   // ← 더한 것: 프래그먼트가 높이로 자른다
    float3 normalWS     : TEXCOORD2;
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthOnlyVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    #if defined(_ALPHATEST_ON)
        output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    #endif

    // ── 고친 곳 하나: 윗면을 내린다 (`RockCut.hlsl`) ────────────────────────
    // **자리를 잡기 전이어야** 화면 좌표와 월드 좌표가 한 자리를 가리킨다.
    input.position.xyz = RockCutOS(input.position.xyz, input.normalOS);

    output.positionWS = TransformObjectToWorld(input.position.xyz);
    output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
    output.positionCS = TransformObjectToHClip(input.position.xyz);
    return output;
}

half DepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    // ── 고친 곳 둘: 옆면을 잘라 내고 등진 면을 버린다 (`RockCut.hlsl`) ──────
    RockCutClip(input.positionWS, input.normalWS, input.positionCS);
    RockCutBackface(input.positionWS, input.normalWS);

    #if defined(_ALPHATEST_ON)
        Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    #endif

    #if defined(LOD_FADE_CROSSFADE)
        LODFadeCrossFade(input.positionCS);
    #endif

    return input.positionCS.z;
}
#endif
