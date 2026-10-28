using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **Built-in 재질을 URP로 옮긴다 — 안 옮기면 통째로 분홍이다**
// (2026-09-17, 사용자: *"돌은 잘보이는데 버섯은 핑크색으로 떠 urp 깨진건가?"* — 맞았다).
//
// ## 왜 어떤 팩만 분홍인가
//
// 분홍은 **셰이더를 못 찾았다**는 뜻이다. `Fantastic ~ Pack`은 재질이 처음부터
// **URP/Lit**을 가리키는데(`guid: 933532a4…`), `3DForge/Cave Adventure kit`은
// **Built-in 내장 셰이더**를 가리킨다(`guid: 0000…f000`, fileID 7 = Legacy/Diffuse ·
// 46 = Standard). 그 셰이더는 URP 프로젝트에 **없다.**
//
// 그래서 **돌(던전 팩)은 멀쩡한데 버섯(동굴 팩)만 분홍**이었다 — 같은 화면에서 갈린 이유가
// 모델이 아니라 **어느 팩에서 왔느냐**였다.
//
// ## 프로퍼티를 이어 주지 않으면 셰이더만 바뀌고 텍스처가 사라진다
//
// URP/Lit은 이름이 다르다: `_MainTex` → **`_BaseMap`**, `_Color` → **`_BaseColor`**.
// 셰이더만 갈아 끼우면 **분홍이 흰색이 될 뿐**이다(그게 더 나쁘다 — 틀린 줄도 모른다).
//
// ## 폴더를 좁혀서 돈다
//
// 유니티의 `Render Pipeline Converter`는 **프로젝트 전체**를 훑어 되돌리기가 어렵다.
// 여기는 **그 팩 폴더만** 돈다 — 무엇이 바뀌었는지가 로그에 남고, 잘못되면 팩을 다시
// 임포트하면 그만이다.
//
// **다시 돌려도 된다** — 이미 URP인 재질은 건너뛴다.
public static class BuiltinToUrp
{
    // 손볼 폴더. **팩 전체가 아니라 실제로 쓰는 것부터** — 여기 없는 팩은 안 건드린다.
    static readonly string[] Folders =
    {
        "Assets/assetsss/3DForge",
        "Assets/assetsss/polyperfect",   // 2026-09-30, 사용자: *"polyperfect 여기 핑크색으로 보여"*
    };

    const string UrpLit      = "Universal Render Pipeline/Lit";
    const string UrpParticle = "Universal Render Pipeline/Particles/Unlit";

    // ── 2026-09-30에 넓혔다 — `polyperfect`에는 Standard 말고도 섞여 있었다 ─────────────
    //
    //   · **스카이박스는 건너뛴다** — Lit으로 바꾸면 하늘이 회색 판이 된다(내장 `Skybox/*`는 URP에서도 돈다).
    //   · **파티클은 URP 파티클로** — Lit으로 바꾸면 불꽃이 불투명한 네모가 된다.
    //   · **투명(Standard `_Mode` 2·3 = Fade·Transparent)은 투명으로** — 유리가 불투명한 벽이 된다.
    //   · **잎(`Nature/*`·`Leaves`)은 알파 컷을 켠다** — 키워드가 없어도 잎은 잎이다.

    [MenuItem("DungeonColony/Building/5. Convert Built-in Materials To URP")]
    static void Convert()
    {
        var lit = Shader.Find(UrpLit);
        if (lit == null)
        {
            Debug.LogError($"[재질] `{UrpLit}` 셰이더를 못 찾았습니다 — URP 프로젝트가 맞는지 확인하세요.");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Material", Folders);
        int moved = 0, already = 0;
        var notes = new List<string>();

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null) continue;

            // 이미 URP면 건너뛴다. **이름으로 본다** — `Shader` 참조를 비교하면 URP의
            // 다른 변형(Simple Lit·Unlit)까지 옮겨 버린다.
            if (m.shader.name.StartsWith("Universal Render Pipeline/")) { already++; continue; }
            string oldName = m.shader.name;
            if (oldName.StartsWith("Skybox/")) { already++; continue; }

            if (oldName.StartsWith("Particles/") || oldName.StartsWith("Legacy Shaders/Particles/"))
            {
                if (ToParticle(m)) { notes.Add(System.IO.Path.GetFileNameWithoutExtension(path) + "(파티클)"); moved++; }
                continue;
            }

            // **옮기기 전에 읽어 둔다** — 셰이더를 바꾸는 순간 옛 프로퍼티는 사라진다.
            var tex    = Get(m, "_MainTex");
            var color  = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            var bump   = Get(m, "_BumpMap");
            var meta   = Get(m, "_MetallicGlossMap");
            var occl   = Get(m, "_OcclusionMap");
            var emis   = Get(m, "_EmissionMap");
            var emisC  = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            float metal = m.HasProperty("_Metallic")   ? m.GetFloat("_Metallic")   : 0f;
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f;
            float cut   = m.HasProperty("_Cutoff")     ? m.GetFloat("_Cutoff")     : 0.5f;
            bool  alpha = m.IsKeywordEnabled("_ALPHATEST_ON")
                       || oldName.StartsWith("Nature/") || oldName.Contains("Leaves");
            float mode  = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f;   // Standard: 0 불투명 · 1 컷 · 2 페이드 · 3 투명
            if (mode == 1f) alpha = true;
            bool  clear = mode >= 2f;
            var   scale = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex")  : Vector2.one;
            var   off   = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;

            m.shader = lit;

            if (tex  != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetTextureScale("_BaseMap", scale);
            m.SetTextureOffset("_BaseMap", off);

            if (bump != null) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
            if (meta != null) { m.SetTexture("_MetallicGlossMap", meta); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (occl != null) { m.SetTexture("_OcclusionMap", occl); m.EnableKeyword("_OCCLUSIONMAP"); }

            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", gloss);

            // **발광은 색이 검지 않을 때만 켠다** — 검은 발광을 켜 두면 키워드만 늘고
            // 배칭이 갈린다(URP는 `_EMISSION` 키워드를 같이 켜야 실제로 빛난다).
            if (emis != null || emisC.maxColorComponent > 0.001f)
            {
                if (emis != null) m.SetTexture("_EmissionMap", emis);
                m.SetColor("_EmissionColor", emisC);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            // **잎사귀는 알파 컷이다** — 고사리·버섯 갓이 그렇고, 안 켜면 **네모난 판**으로 보인다.
            if (alpha)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", cut);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            if (clear)
            {
                m.SetFloat("_Surface", 1f);   // 투명
                m.SetFloat("_Blend", 0f);     // 알파
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            EditorUtility.SetDirty(m);
            notes.Add(System.IO.Path.GetFileNameWithoutExtension(path) + (clear ? "(투명)" : alpha ? "(컷)" : ""));
            moved++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[재질] URP로 옮긴 것 {moved}개 · 이미 URP였던 것 {already}개.\n  "
                + string.Join(", ", notes));
    }

    // 파티클 — 텍스처와 색만 이어 주고 **더하기로 섞는다**(불꽃·촛불은 빛이라 겹칠수록 밝아야 한다).
    static bool ToParticle(Material m)
    {
        var sh = Shader.Find(UrpParticle);
        if (sh == null) return false;

        var tex   = Get(m, "_MainTex");
        var color = m.HasProperty("_TintColor") ? m.GetColor("_TintColor")
                  : m.HasProperty("_Color")     ? m.GetColor("_Color") : Color.white;

        m.shader = sh;
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);   // 더하기
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return true;
    }

    static Texture Get(Material m, string name) =>
        m.HasProperty(name) ? m.GetTexture(name) : null;
}
