using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UIHelper
{
    public static GameObject MakePanel(Transform parent, string name, Vector2 size,
                                       Vector2 pivot, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta      = size;
        rt.pivot          = pivot;
        rt.anchorMin      = pivot;
        rt.anchorMax      = pivot;
        rt.anchoredPosition = anchoredPos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.92f);

        return go;
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

        return btn;
    }
}
