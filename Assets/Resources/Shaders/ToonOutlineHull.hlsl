// **외곽선 껍데기의 정점 — 한 벌이다**(`ToonOutline`·`ToonOutlineOuter`가 같이 쓴다).
// 갈라 적으면 굵기 규칙(월드 6cm · 화면 최소)을 한쪽만 고치는 날 몸통 선만 굵기가 달라진다.
// 부르는 셰이더가 `_OutlineWidth`·`_OutlineMin`을 제 CBUFFER에 둔다.
#ifndef TOON_OUTLINE_HULL_INCLUDED
#define TOON_OUTLINE_HULL_INCLUDED

struct OutlineIn  { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; };
struct OutlineOut { float4 positionCS : SV_POSITION; };

// **껍데기를 미는 방향은 선 법선이다**(2026-11-06, 사용자: *"가까이서 보면 뾰족뾰족 — 저작한 거의 모든 유닛이"*).
// 거리장으로 빚은 메시의 정점 법선은 그 자리의 기울기라 주름 · 이음매 · 줄인 큰 세모에서 이웃끼리 크게 어긋난다 — 살 음영에는
// 맞는 값이지만 껍데기를 6cm씩 밀면 어긋난 만큼 갈라져 실루엣에 가시가, 얼굴 앞에 검은 조각이 선다(가까울수록 월드 굵기가 이겨 커진다).
// 구울 때 둘레의 면 법선을 고르게 섞은 방향을 **탄젠트 칸**에 넣는다(`ChibiPawnCreator.BakeOutlineNormals` — 스킨 메시도 탄젠트는 관절을 따라 돈다).
// **표지는 `w = 2`** — 탄젠트가 없는 메시(0)나 들여온 모델의 진짜 탄젠트(±1)는 예전대로 정점 법선을 민다.
float3 OutlineDirOS(OutlineIn v) { return v.tangentOS.w > 1.5 ? v.tangentOS.xyz : v.normalOS; }

// `groundPush` — 0이면 깊이를 안 건드린다. 0보다 크면 **땅을 이길 만큼 카메라 쪽으로 깊이만 당긴다**
// (`ToonOutlineOuter` 머리말 — 발밑의 선이 땅에 먹히던 것).
// `pushFrom` — 당기기 시작하는 내려다보는 각(V.y). 0이면 수평 위로 다 당긴다.
OutlineOut OutlineHull(OutlineIn v, float groundPush, float pushFrom)
{
    OutlineOut o;
    float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
    float3 nrmWS = normalize(TransformObjectToWorldNormal(OutlineDirOS(v)));

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
        // **아래를 향한 선만 · 내려다볼 때만 당긴다**(2026-11-06, 사용자: *"위쪽에서 볼 때는 괜찮은데 밑에서 보면 계속 문제가 있어"*).
        // 땅 · 몸통 윗면에 먹히는 것은 껍데기가 **아래로** 밀려 그 면 밑으로 들어간 자리뿐이다. 옆으로 민 선까지 당기면 어깨 뒤로 내린
        // 머리카락의 옆선이 어깨 앞으로 끌려 나와 셔츠 위에 검게 덮였고, 올려다볼 때는(V.y ≤ 0) cot가 천장(3)에 붙어 가장 크게 당겼다 —
        // 그때는 선 밑의 면이 카메라와 선 사이에 오지 않는다. 그래서 머리카락 · 수염은 한 선(`ChibiPawnCreator.LineFor`)으로 묶인다.
        // **늘어진 털은 충분히 내려다볼 때만**(같은 날 · 사용자: *"애매한 각도에서 … 딱 저 각도에서만 보이네"*) — 수염 땋은 가닥 · 엘프 앞 가닥은
        // 가슴에 살짝 묻혀 있어 그 속 껍데기를 당기면 비스듬한 각에서 셔츠 위로 드러난다. 털이 이겨야 할 면(몸통 윗면)은 위에서 내려다볼 때만 보이므로
        // 털 선 재질은 `pushFrom`을 올려 그 각부터 당긴다(몸 · 발밑 선은 0 — 땅은 낮은 각에서도 이겨야 한다).
        float  down = saturate(-nrmWS.y * 2.0) * saturate((V.y - pushFrom) * 10.0);
        float  push = off * cot * groundPush * down + 0.01;
        // **깊이만** 옮긴다 — 시선 위로 당긴 자리의 z/w를 원래 w에 싣는다(화면 자리는 그대로).
        float4 near = TransformWorldToHClip(posWS + V * push);
        o.positionCS.z = near.z / near.w * clipBase.w;
    }
    return o;
}

#endif
