using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// **성장 배경·특성을 글로 그리고, 올리면 설명을 띄우는 일 — 한 벌**(2026-09-08).
//
// 원래 `UnitStatsUI` 안에 정적으로 갇혀 있었다. 살펴보기 창에서도 남의 이력을 읽게 해 달라는
// 요청이 오면서(사용자: *"지금은 내 폰의 것만 읽을 수 있잖아"*) **두 창이 같은 것을 그리게**
// 됐는데, 각자 적으면 갈린다 — 실제로 갈릴 자리가 셋이다: 줄 모양(`무능력:` 꼬리표) ·
// 링크 id 규약 · 툴팁 본문.
//
// **줄로 못 나누는 글이라 TMP `<link>`를 쓴다.** 배경·특성은 한 문단 안에 섞여 있어 줄마다
// 오브젝트를 둘 수가 없다(스탯 열은 줄이 오브젝트라 제 포인터 이벤트를 쓴다 — `StatRowUI`).
public static class PawnStoryText
{
    const string Q = "\"";

    // ── 그리기 ───────────────────────────────────────────────────────────────

    // **소환물에게는 유년기가 없다** — 칸이 둘인 것은 같지만 묻는 것이 다르다:
    // *어떻게 생겨났나* · *무엇에 쓰였나*. **라벨만 갈아 끼운다**(칸을 늘리면 사람과 소환물의
    // 창 모양이 달라져 견줄 수가 없다).
    public static void AppendBackstories(StringBuilder sb, UnitBrain brain, string header)
    {
        if (brain == null || (brain.Childhood == null && brain.Adulthood == null)) return;

        if (header != null) sb.AppendLine(header);

        bool made = brain.IsSummoned;
        AppendStory(sb, made ? "비롯됨"   : "유년기", brain.Childhood);
        AppendStory(sb, made ? "부려진 일" : "성년기", brain.Adulthood);
    }

    static void AppendStory(StringBuilder sb, string slot, BackstoryData b)
    {
        if (b == null) return;

        // 오프셋은 툴팁 몫이다(특성과 같은 규칙). **다만 금지 작업은 줄에 남긴다** —
        // 그건 스탯 보정이 아니라 *"이 폰은 이 일을 못 한다"*는 경고라, 올려 봐야 아는
        // 것이면 P패널에서 `×`를 보고 나서야 알게 된다.
        string row = $"<color=#888>{slot}</color>  <b>{b.DisplayName}</b>";

        string bans = BanSummary(b);
        if (!string.IsNullOrEmpty(bans)) row += $"  <color=#D66>{bans}</color>";

        sb.AppendLine($"  {StoryLink(b, row)}");
    }

    public static void AppendTraits(StringBuilder sb, UnitBrain brain, string header)
    {
        var traits = brain != null ? brain.Traits : null;
        if (traits == null || traits.Count == 0) return;

        if (header != null) sb.AppendLine(header);
        foreach (var t in traits)
        {
            if (t == null) continue;
            sb.AppendLine($"  {TraitLink(t, $"<b>{t.DisplayName}</b>")}");
        }
    }

    // `무능력: 제작 · 연구`
    public static string BanSummary(BackstoryData b)
    {
        if (b?.DisabledJobs == null || b.DisabledJobs.Count == 0) return "";

        var names = new List<string>();
        foreach (var c in b.DisabledJobs) names.Add(JobCategoryInfo.DisplayName(c));
        return $"무능력: {string.Join(" · ", names)}";
    }

    // id는 **에셋 이름**이라 표시명이 바뀌어도 안 깨진다.
    public static string StoryLink(BackstoryData b, string body) => $"<link={Q}story:{b.name}{Q}>{body}</link>";
    public static string TraitLink(TraitData t, string body)     => $"<link={Q}trait:{t.name}{Q}>{body}</link>";

    // ── 한 줄로 붙여 쓰기 ────────────────────────────────────────────────────
    //
    // 살펴보기 창은 좁아서 **이름만 한 줄**로 쓴다(`배경  광산 마을 출신 · 상단 호위`).
    // 오프셋까지 적으면 두 줄이 숫자로 가득 찬다.
    //
    // **이름마다 링크를 감는다** — 그래야 좁은 줄에서도 올려서 자세한 것을 읽을 수 있다.
    // `BackstoryPool.NameList`를 그대로 쓰면 글자만 남아 **올려도 아무것도 안 뜬다.**
    public static string StoryNames(UnitBrain brain)
    {
        if (brain == null) return "";
        var parts = new List<string>();
        if (brain.Childhood != null) parts.Add(StoryLink(brain.Childhood, brain.Childhood.DisplayName));
        if (brain.Adulthood != null) parts.Add(StoryLink(brain.Adulthood, brain.Adulthood.DisplayName));
        return string.Join(" · ", parts);
    }

    public static string TraitNames(UnitBrain brain)
    {
        var traits = brain != null ? brain.Traits : null;
        if (traits == null || traits.Count == 0) return "";

        var parts = new List<string>();
        foreach (var t in traits) if (t != null) parts.Add(TraitLink(t, t.DisplayName));
        return string.Join(" · ", parts);
    }

    // ── 올렸을 때 ────────────────────────────────────────────────────────────

    // 링크 id → 띄울 글. **`story:`·`trait:`만 안다** — 스탯 칸(`stat:`)은 스탯 창의 것이라
    // 그쪽이 먼저 보고 넘긴다(`UnitStatsUI.Resolve`).
    public static bool Resolve(string id, out string title, out string body)
    {
        title = body = null;
        if (string.IsNullOrEmpty(id)) return false;

        if (id.StartsWith("story:"))
        {
            var story = BackstoryPool.Get(id.Substring(6));
            if (story == null) return false;

            title = story.DisplayName;
            string effect = story.EffectSummary();
            string bans   = BanSummary(story);
            if (!string.IsNullOrEmpty(bans))
                effect = string.IsNullOrEmpty(effect) ? bans : effect + "\n" + bans;

            body = Compose(story.Description, effect);
            return true;
        }

        if (id.StartsWith("trait:"))
        {
            var trait = TraitPool.Get(id.Substring(6));
            if (trait == null) return false;

            title = trait.DisplayName;
            body  = Compose(trait.Description, trait.EffectSummary());
            return true;
        }

        return false;
    }

    static string Compose(string desc, string effect) =>
        string.IsNullOrWhiteSpace(desc)
            ? effect
            : (string.IsNullOrEmpty(effect) ? desc : $"{desc}\n<color=#8AC>{effect}</color>");

    // **글자 덩어리 안에서 영역을 찾는 일 — 한 벌.** 두 창이 각자 적으면 한쪽만 `ClearHover`를
    // 빠뜨려 **창을 닫아도 설명이 화면에 남는다**(이 저장소가 이미 겪은 자리다).
    //
    // `hoveredId`를 부르는 쪽이 들고 있는 이유: 창마다 제 상태다(둘이 같이 떠 있을 수 있다).
    // 돌려주는 값은 *지금 무엇에 올라와 있나*이고, `null`이면 아무것도 아니다.
    public static void Tick(TMP_Text text, ref string hoveredId,
                            System.Func<string, (string title, string body)?> extra = null)
    {
        if (text == null || Mouse.current == null || !text.gameObject.activeInHierarchy)
        { Clear(ref hoveredId); return; }

        var canvas = text.canvas;
        if (canvas == null) { Clear(ref hoveredId); return; }

        Vector2 mouse = Mouse.current.position.ReadValue();
        var     cam   = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        int idx = TMP_TextUtilities.FindIntersectingLink(text, mouse, cam);
        if (idx < 0) { Clear(ref hoveredId); return; }

        string id = text.textInfo.linkInfo[idx].GetLinkID();
        if (id == hoveredId) return;   // 같은 칸이면 다시 그리지 않는다(위치는 툴팁이 따라온다)

        string title, body;
        var got = extra?.Invoke(id);
        if (got.HasValue) { title = got.Value.title; body = got.Value.body; }
        else if (!Resolve(id, out title, out body)) { Clear(ref hoveredId); return; }

        hoveredId = id;
        HoverTooltipUI.Show(title, body);
    }

    public static void Clear(ref string hoveredId)
    {
        if (hoveredId == null) return;
        hoveredId = null;
        HoverTooltipUI.Hide();
    }
}
