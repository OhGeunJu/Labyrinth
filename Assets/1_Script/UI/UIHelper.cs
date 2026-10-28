using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UIHelper
{
    // 런타임 생성 TMP는 기본 폰트(LiberationSans SDF)를 쓰는데 한글 글리프가 없어 □로 나온다.
    // TMP Settings의 기본 폰트 → 씬에 이미 있는 TMP 텍스트의 폰트 순으로 한글 가능한 폰트를 물려받는다.
    // (NanumGothic SDF는 Resources 밖이라 Resources.Load 불가 — 씬에서 상속하는 게 유일한 런타임 경로.)
    public static void ApplyKoreanFont(TextMeshProUGUI tmp, Transform context = null)
    {
        if (tmp == null) return;
        var font = ResolveKoreanFont(context != null ? context : tmp.transform);
        if (font != null) tmp.font = font;
    }

    static TMP_FontAsset _cachedFont;

    static TMP_FontAsset ResolveKoreanFont(Transform context)
    {
        if (_cachedFont != null) return _cachedFont;

        var def = TMP_Settings.defaultFontAsset;
        if (def != null && def.HasCharacter('한')) return _cachedFont = def;

        // 같은 캔버스(계층 루트) 안의 기존 텍스트 우선 — 패널과 같은 폰트를 쓰게 된다.
        var root = context != null ? context.root : null;
        if (root != null)
            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (t.font != null && t.font.HasCharacter('한')) return _cachedFont = t.font;

        foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include,
                                                                   FindObjectsSortMode.None))
            if (t.font != null && t.font.HasCharacter('한')) return _cachedFont = t.font;

        return null;   // 못 찾으면 기본 폰트 유지(□) — TMP Settings에 한글 폰트를 지정하면 해결
    }

    public static TextMeshProUGUI MakeLabel(Transform parent, string name, string text,
                                             Vector2 anchoredPos, Vector2 size,
                                             float fontSize = 13f, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta        = size;
        rt.anchoredPosition = anchoredPos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        ApplyKoreanFont(tmp, parent);

        return tmp;
    }

    public static Button MakeButton(Transform parent, string name, string label,
                                     Vector2 anchoredPos, Vector2 size, float fontSize = 12f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta        = size;
        rt.anchoredPosition = anchoredPos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        var btn = go.AddComponent<Button>();
        var colors      = btn.colors;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        colors.pressedColor     = new Color(0.15f, 0.15f, 0.15f, 1f);
        btn.colors = colors;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        ApplyKoreanFont(tmp, parent);

        return btn;
    }
}
