// **외곽선 껍데기의 정점 — 한 벌이다**(`ToonOutline`·`ToonOutlineOuter`가 같이 쓴다).
// 갈라 적으면 굵기 규칙(월드 6cm · 화면 최소)을 한쪽만 고치는 날 몸통 선만 굵기가 달라진다.
// 부르는 셰이더가 `_OutlineWidth`·`_OutlineMin`을 제 CBUFFER에 둔다.
#ifndef TOON_OUTLINE_HULL_INCLUDED
#define TOON_OUTLINE_HULL_INCLUDED

struct OutlineIn  { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
struct OutlineOut { float4 positionCS : SV_POSITION; };

// `groundPush` — 0이면 깊이를 안 건드린다. 0보다 크면 **땅을 이길 만큼 카메라 쪽으로 깊이만 당긴다**
// (`ToonOutlineOuter` 머리말 — 발밑의 선이 땅에 먹히던 것).
OutlineOut OutlineHull(OutlineIn v, float groundPush)
{
    OutlineOut o;
    float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
    float3 nrmWS = normalize(TransformObjectToWorldNormal(v.normalOS));

    float4 clipBase  = TransformWorldToHClip(posWS);
    // 월드 굵기: 법선 쪽으로 `_OutlineWidth`m 민 자리를 클립으로 옮겨 **차이**만 쓴다.
    float4 clipThick = TransformWorldToHClip(posWS + nrmWS * _OutlineWidth);
    float2 dWorld    = clipThick.xy - clipBase.xy;

    // 화면 최소: `w`를 곱해 원근 나눗셈을 상쇄한 **픽셀 일정** 굵기. `_OutlineMin`은 **픽셀**이다
    // (2026-10-18 — 전에는 NDC 0.008, 1080p에서 약 4px이라 13m만 멀어져도 최소가 이겨 폰은 작아지는데
    // 선만 그대로였다 — 사용자: *"카메라가 멀어지면 뚱뚱해지고"*). NDC 한 픽셀 = 2 / 화면 높이.
    float3 nrmCS   = TransformWorldToHClipDir(nrmWS);
    float2 nrm2    = nrmCS.xy / max(length(nrmCS.xy), 1e-5);
    float2 dScreen = nrm2 * (_OutlineMin * 2.0 / _ScreenParams.y) * clipBase.w;

    // **화면에서 굵은 쪽** — 가까이서는 월드가, 멀리서는 최소가 이긴다.
    float2 d = dot(dWorld, dWorld) > dot(dScreen, dScreen) ? dWorld : dScreen;

    o.positionCS = clipBase;
    o.positionCS.xy += d;

    if (groundPush > 0)
    {
        // 선이 화면에서 밀려난 거리(m) — `w`가 그대로라 클립 xy ÷ 투영 배율이 곧 시야 공간 거리다.
        float  off = length(float2(d.x / UNITY_MATRIX_P[0][0], d.y / UNITY_MATRIX_P[1][1]));
        // 그 아래에 비치는 땅은 **off × cot(내려다보는 각)**만큼 더 가깝다. 각이 누울수록 커지므로
        // 천장을 둔다(18° 아래로는 조금 먹힌다 — 더 당기면 선이 제 손 공 위로 올라선다).
        float3 V   = GetWorldSpaceNormalizeViewDir(posWS);
        float  cot = min(sqrt(saturate(1 - V.y * V.y)) / max(V.y, 0.05), 3.0);
        float  push = off * cot * groundPush + 0.01;
        // **깊이만** 옮긴다 — 시선 위로 당긴 자리의 z/w를 원래 w에 싣는다(화면 자리는 그대로).
        float4 near = TransformWorldToHClip(posWS + V * push);
        o.positionCS.z = near.z / near.w * clipBase.w;
    }
    return o;
}

#endif
