// **몸통의 바깥 선 — 제 몸 위에는 안 긋고, 땅에는 안 먹힌다**(2026-09-19).
//
// ### 1. 제 몸 위에는 안 긋는다 (사용자: *"이거 왜 몸 가운데에 선이 생기는 거야?"*)
// 뒤집힌 껍데기는 **실루엣이면 어디든** 선을 긋는다 — 바깥 둘레만이 아니라 **몸이 제 몸을 가리는 자리**
// 에도. 여자 체형은 엉덩이(0.37)가 가슴(0.27)보다 넓어서, 위에서 내려다보면 **가슴의 아랫단이 엉덩이
// 위에 걸친다** — 그 단이 실루엣이라 배 한가운데에 가로선이 섰다. 틀린 선이 아니라 *정직한* 선이라
// 옆선을 고쳐서는 못 없앤다(카메라가 85°까지 눕고, 그 각에서는 위로 넓어지는 면은 전부 단이 된다).
// 그래서 **살이 다 칠해진 뒤에** 긋고 **제 몸통이 칠한 자리(스텐실 = 그 폰의 번호)는 건너뛴다.**
//
// **번호는 폰마다다**(`PawnOutlineId`). 한 값을 모두가 쓰면 **앞 폰의 선이 뒤 폰의 몸 위에서 사라진다**
// (사용자: *"이건 해결해야해"*) — 묻는 것은 *"폰의 몸인가"*가 아니라 **"내 몸인가"**다.
//
// ### 2. 땅에 안 먹힌다 (사용자: *"바닥이랑 겹쳐서 외곽선이 안보이는 거"*)
// 선은 화면에서만 밀려나고 깊이는 **몸의 가장자리 그대로**다. 몸통 밑동은 땅에 닿아 있으니 그 아래로
// 밀려난 선이 덮는 땅은 **몸보다 가깝다** — 내려다보는 각이 누울수록 더. 그래서 깊이만 카메라 쪽으로
// 당긴다(`ToonOutlineHull`의 `groundPush`) — **당기는 양은 그 땅을 이길 만큼만** 계산한다(고정값이면
// 멀리서는 모자라고 가까이서는 선이 남의 몸 위로 올라선다). 몸을 띄우는 길은 안 된다 — 경사·줌에 따라
// 굵기가 달라져 또 잘린다. **몸통에만 건다** — 서 있을 때 땅에 닿는 것은 몸통뿐이고, 머리에 걸면
// 머리의 선이 **머리카락을 뚫고** 비친다(머리카락은 머리보다 2~3cm 밖이다).
//
// ## 어기면 조용히 깨지는 것
//
// - **같은 렌더러의 두 번째 재질로 단다 — 큐는 살보다 뒤다.** 같은 셰이더의 패스로 넣으면
//   URP가 한 렌더러의 패스를 **외곽선 → 살** 순으로 그려, 선을 긋는 순간 제 살의 스텐실이 아직 없다.
//   뒤에 그리는 것이 땅에 대해서도 맞다 — 땅이 먼저 깊이를 채워야 당긴 깊이가 그걸 이긴다.
// - **큐는 반투명보다도 뒤(3001)다** — 발밑의 선택 고리(`GroundRing`, 3000)는 땅에 눌린 반투명이라
//   2001이면 선을 그은 **뒤에** 그 위를 덮어 초록 고리가 외곽선을 뚫었다(사용자: *"밑에 뜨는 이 초록선에
//   외곽선이 뚫려"*). 고리는 깊이를 안 적으므로 뒤에 긋는 선이 그대로 이긴다.
// - **몸통 재질(`Chibi_Shirt`)의 제 외곽선은 굵기 0이다** — 안 끄면 그쪽이 가로선을 그대로 긋는다.
// - **번호가 없으면 재질 기본값(1)을 모두가 쓴다** — 에디터의 씬 뷰가 그렇다(겹칠 때만 틀리다).
Shader "DungeonColony/ToonOutlineOuter"
{
    Properties
    {
        _OutlineColor("외곽선 색", Color) = (0.04,0.04,0.05,1)
        _OutlineWidth("외곽선 굵기(m)", Range(0,0.12)) = 0.06
        _OutlineMin("외곽선 최소 굵기(화면 기준)", Range(0,0.03)) = 0.008
        _OutlineGroundPush("땅 이기기 여유(배)", Range(0,3)) = 1.3
        [IntRange] _StencilRef("스텐실 번호(폰마다)", Range(0,255)) = 1
        // 얼굴의 선인가(코) — 켜면 번호 + 128과 비교해 **제 얼굴 위에서만** 비킨다(`PawnOutlineId`가 읽는다).
        [ToggleUI] _StencilFace("얼굴(번호 + 128)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Transparent+1" }

        Pass
        {
            Name "OutlineOuter"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            Stencil { Ref [_StencilRef] Comp NotEqual }

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _OutlineMin;
                float  _OutlineGroundPush;
            CBUFFER_END

            #include "ToonOutlineHull.hlsl"

            OutlineOut OutlineVert(OutlineIn v) { return OutlineHull(v, _OutlineGroundPush); }
            half4 OutlineFrag(OutlineOut i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
    Fallback Off
}
