using UnityEngine;
using UnityEngine.Rendering;

// **불투명한 재질을 알파가 먹히는 것으로 바꾼다 — 답은 여기 하나다**(2026-09-02).
//
// 런타임에 투명하게 만드는 자리가 **둘**이었고 서로 달랐다:
//
//   · `BuildingRoof.MakeFadeable`      — 지붕·앞 벽을 흐린다(원본 재질의 사본)
//   · `BuildingBlueprint.CreateBlueprintMaterial` — 청사진의 파란 유령(셰이더에서 새로)
//
// 같은 물음에 두 곳이 답하면 반드시 갈린다. 실제로 갈려 있었다 — 한쪽은 `_AlphaClip`을
// 끄고 다른 쪽은 안 껐고, 둘 다 URP가 실제로 요구하는 것 몇 가지를 빠뜨렸다.
//
// ── URP는 "투명"을 색이 아니라 상태로 든다 ─────────────────────────────────
//
// 알파만 낮추면 아무 일도 안 일어난다. URP Lit은 **표면 종류(`_Surface`) · 블렌드 인자 ·
// 깊이 쓰기 · 키워드 · 렌더 큐**를 같이 봐야 투명해진다. 여기 적힌 순서는 짐작이 아니라
// URP 패키지의 `BaseShaderGUI.SetupMaterialBlendModeInternal`이 하는 그대로다(인스펙터에서
// Surface Type을 Transparent로 바꿀 때 도는 코드). **하나라도 빠지면 조용히 불투명이다.**
public static class FadeMaterial
{
    static readonly int SurfaceId   = Shader.PropertyToID("_Surface");
    static readonly int BlendId     = Shader.PropertyToID("_Blend");
    static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
    static readonly int SrcId       = Shader.PropertyToID("_SrcBlend");
    static readonly int DstId       = Shader.PropertyToID("_DstBlend");
    static readonly int SrcAlphaId  = Shader.PropertyToID("_SrcBlendAlpha");
    static readonly int DstAlphaId  = Shader.PropertyToID("_DstBlendAlpha");
    static readonly int ZWriteId    = Shader.PropertyToID("_ZWrite");
    static readonly int AlphaToMaskId = Shader.PropertyToID("_AlphaToMask");
    static readonly int ModeId      = Shader.PropertyToID("_Mode");        // 빌트인 Standard
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    // 원본을 복제해 투명으로. **공유 재질을 건드리지 않는다** — 그러면 세상의 모든 오두막이
    // 같이 흐려진다.
    public static Material CloneOf(Material src)
    {
        var m = src != null ? new Material(src) : Fresh();
        if (m != null) Setup(m);
        return m;
    }

    // 셰이더에서 새로 만들어 투명으로 + 색까지.
    public static Material Create(Color color)
    {
        var m = Fresh();
        if (m == null) return null;
        Setup(m);
        SetColor(m, color);
        return m;
    }

    static Material Fresh()
    {
        var sh = RuntimeShaders.Find(RuntimeShaders.Lit, RuntimeShaders.Standard);
        return sh != null ? new Material(sh) : null;
    }

    // **색을 넣는 문도 하나다.**
    //
    // `new Material(sh) { color = ... }`가 청사진이 안 비치던 원인이다 — `Material.color`는
    // `_Color`를 짚는데 **URP Lit이 실제로 읽는 것은 `_BaseColor`**다. 그래서 알파 0.4를
    // 넣어 놓고도 셰이더는 기본값(흰색, 알파 1)을 읽어 **불투명한 흰 덩어리**로 떴다.
    // 둘 다 넣으면 어느 셰이더로 떨어지든 맞는다.
    public static void SetColor(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        if (m.HasProperty(ColorId))     m.SetColor(ColorId, c);
    }

    // 지금 색은 두고 알파만 바꾼다(페이드가 매 프레임 부른다).
    public static void SetAlpha(Material m, float a)
    {
        if (m == null) return;

        if (m.HasProperty(BaseColorId))
        {
            var c = m.GetColor(BaseColorId);
            m.SetColor(BaseColorId, new Color(c.r, c.g, c.b, a));
        }
        if (m.HasProperty(ColorId))
        {
            var c = m.GetColor(ColorId);
            m.SetColor(ColorId, new Color(c.r, c.g, c.b, a));
        }
    }

    // 재질 하나를 투명 상태로 바꾼다. **URP GUI가 하는 것과 같은 순서다.**
    public static void Setup(Material m)
    {
        if (m == null) return;

        // ── 표면 종류 ──
        if (m.HasProperty(SurfaceId))   m.SetFloat(SurfaceId,   1f);   // 0 불투명 · 1 투명
        if (m.HasProperty(BlendId))     m.SetFloat(BlendId,     0f);   // 0 = 알파 블렌드
        if (m.HasProperty(AlphaClipId)) m.SetFloat(AlphaClipId, 0f);   // 컷아웃이 아니다
        if (m.HasProperty(ModeId))      m.SetFloat(ModeId,      3f);   // 빌트인 Standard: Transparent

        // URP Lit의 `ForwardLit` 패스는 `AlphaToMask[_AlphaToMask]`를 든다 — 켜져 있으면
        // MSAA에서 알파가 커버리지로 양자화돼 **반투명이 그물처럼 찍힌다.** 컷아웃 전용이다.
        if (m.HasProperty(AlphaToMaskId)) m.SetFloat(AlphaToMaskId, 0f);

        // ── 블렌드 인자 ──
        //
        // **알파 채널 쪽도 같이 적는다**(`_SrcBlendAlpha`/`_DstBlendAlpha`) — URP의
        // ShaderLab은 `Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]`라
        // 뒤쪽을 안 적으면 **불투명 시절의 One/Zero가 그대로 남는다.**
        m.SetFloat(SrcId, (float)BlendMode.SrcAlpha);
        m.SetFloat(DstId, (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty(SrcAlphaId)) m.SetFloat(SrcAlphaId, (float)BlendMode.One);
        if (m.HasProperty(DstAlphaId)) m.SetFloat(DstAlphaId, (float)BlendMode.OneMinusSrcAlpha);

        m.SetFloat(ZWriteId, 0f);

        // ── 키워드 ──
        //
        // **`_ALPHAPREMULTIPLY_ON`을 반드시 끈다.** 불투명 재질에도 `_BlendModePreserveSpecular`가
        // 1로 저작돼 있는데(유니티 기본값), 그 모드는 `_SrcBlend`를 **One**으로 쓴다 —
        // 우리는 `SrcAlpha`를 넣으므로 키워드가 켜져 있으면 셋이 어긋나 색이 튄다.
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHABLEND_ON");            // 빌트인 Standard용
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHAMODULATE_ON");

        // ── 언제 그리나 ──
        //
        // **태그도 같이 바꾼다** — 재질 에셋에 `RenderType: Opaque`가 저작돼 남아 있으면
        // 교체 셰이더·렌더러 기능이 이걸 보고 불투명 취급을 한다.
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
    }
}
