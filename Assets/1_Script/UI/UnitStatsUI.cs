using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 선택 유닛의 스탯 패널 — C키 토글.
public class UnitStatsUI : MonoBehaviour
{
    [SerializeField] GameObject       panel;
    [Tooltip("왼쪽 상자 — 속성·성장 배경·특성")]
    [SerializeField] TextMeshProUGUI  infoText;
    [Tooltip("스탯 열들 — 속성·전투·무기·마법·작업·제작. `DungeonColony → Setup Stat Panel`이 만든다")]
    [SerializeField] StatColumnUI[] columns;

    [Header("이름표")]
    [Tooltip("`던컨  인간` — 창 맨 위")]
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] Image           portrait;
    [Tooltip("초상화가 없을 때의 색")]
    [SerializeField] Color           emptyPortrait = new(1f, 1f, 1f, 0.12f);
    [SerializeField] SelectionManager selectionManager;
    [SerializeField] Key              toggleKey = Key.C;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        selectionManager.OnSelectionChanged += OnSelectionChanged;
        BuildModeManager.OnActiveChanged    += OnBuildModeChanged;
    }

    void OnDestroy()
    {
        selectionManager.OnSelectionChanged -= OnSelectionChanged;
        BuildModeManager.OnActiveChanged    -= OnBuildModeChanged;
    }

    void OnSelectionChanged(PawnMover pawn, bool selected)
    {
        if (selectionManager.Selected.Count == 0 && panel != null)
            panel.SetActive(false);
    }

    void OnBuildModeChanged(bool active) { if (active && panel != null) panel.SetActive(false); }

    void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            if (selectionManager.Selected.Count == 0) return;
            bool next = panel != null && !panel.activeSelf;
            panel?.SetActive(next);
            if (next) Refresh();
        }

        if (panel != null && panel.activeSelf) { Refresh(); UpdateHover(); }
        else                                    ClearHover();
    }

    // ── 마우스 올린 칸의 설명 ───────────────────────────────────────────────
    // **TMP `<link>`로 글자 안에서 영역을 잡는다.** 스탯 51개마다 오브젝트를 두면 창이 무거워지고,
    // 스탯이 늘 때마다 씬 작업이 따라붙는다. 링크는 텍스트 한 덩어리를 유지한 채로 칸마다
    // 다른 설명을 띄운다.
    void UpdateHover()
    {
        if (infoText == null || Mouse.current == null) { ClearHover(); return; }

        var canvas = infoText.canvas;
        if (canvas == null) { ClearHover(); return; }

        Vector2 mouse = Mouse.current.position.ReadValue();
        var     cam   = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        // **링크가 남은 곳은 왼쪽 상자뿐이다** — 기술 열은 줄이 오브젝트가 되면서 제
        // 포인터 이벤트로 설명을 띄운다(`StatRowUI`). 글자 안에서 영역을 찾아 헤매는 것은
        // **줄로 못 나누는 글**(성장 배경·특성)에만 남는다.
        int idx = TMP_TextUtilities.FindIntersectingLink(infoText, mouse, cam);
        if (idx < 0) { ClearHover(); return; }

        string id = infoText.textInfo.linkInfo[idx].GetLinkID();
        if (id == _hoveredId) return;   // 같은 칸이면 다시 그리지 않는다(위치는 툴팁이 따라온다)

        if (!Resolve(id, out string title, out string body)) { ClearHover(); return; }

        _hoveredId = id;
        HoverTooltipUI.Show(title, body);
    }

    void ClearHover()
    {
        if (_hoveredId == null) return;
        _hoveredId = null;
        HoverTooltipUI.Hide();
    }

    string _hoveredId;

    // `stat:12` / `trait:Trait_Tough` — 링크 id 하나로 두 종류를 가른다.
    static bool Resolve(string id, out string title, out string body)
    {
        title = body = null;
        if (string.IsNullOrEmpty(id)) return false;

        if (id.StartsWith("stat:"))
        {
            if (!int.TryParse(id.Substring(5), out int raw)) return false;
            var stat = (StatName)raw;
            title = stat.ToDisplay();
            body  = stat.Describe();
            return true;
        }

        if (id.StartsWith("story:"))
        {
            var story = BackstoryPool.Get(id.Substring(6));
            if (story == null) return false;

            title = story.DisplayName;
            string effect = story.EffectSummary();
            string bans   = BanSummary(story);
            if (!string.IsNullOrEmpty(bans))
                effect = string.IsNullOrEmpty(effect) ? bans : effect + "\n" + bans;

            body = string.IsNullOrWhiteSpace(story.Description)
                 ? effect
                 : (string.IsNullOrEmpty(effect) ? story.Description
                                                 : story.Description + "\n" + "<color=#8AC>" + effect + "</color>");
            return true;
        }

        if (id.StartsWith("trait:"))
        {
            var trait = TraitPool.Get(id.Substring(6));
            if (trait == null) return false;

            title = trait.DisplayName;
            string effect = trait.EffectSummary();
            body = string.IsNullOrWhiteSpace(trait.Description)
                 ? effect
                 : (string.IsNullOrEmpty(effect) ? trait.Description
                                                 : $"{trait.Description}\n<color=#8AC>{effect}</color>");
            return true;
        }

        return false;
    }

    void Refresh()
    {
        if (selectionManager.Selected.Count == 0) { panel?.SetActive(false); return; }

        var pawn  = selectionManager.Selected[0];
        var brain = pawn.GetComponent<UnitBrain>();
        if (brain == null) return;

        var stats = brain.Stats;

        // **숫자는 줄 오브젝트가 갖는다** — 여기서 하는 일은 어느 줄에 어느 스탯인지 알려
        // 주는 것뿐이다. 표(`StatColumns`)는 씬을 지은 도구와 **같은 것**을 읽는다.
        var held = HeldStats(brain.Equipment);
        if (columns != null)
            foreach (var col in columns) if (col != null) col.Bind(stats, held);

        // 왼쪽 상자는 **줄로 못 나누는 것들**이다(제목·든 것·성장 배경·특성 — 길이가 저마다
        // 다르고 한 줄에 설명이 붙는다). 이런 글까지 줄 오브젝트로 만들면 칸마다 규격을
        // 정해야 하는데, 정작 그 글들은 규격이 있을 수 없는 글이다.
        AppendIdentity(brain, pawn, stats);

        var sb = new StringBuilder();
        AppendHeld(sb, brain.Equipment);
        AppendBackstories(sb, brain);
        AppendTraits(sb, brain);

        Assign(infoText, sb.ToString());
    }

    // **누구인가는 맨 위다** — 초상화와 이름. 전에는 이 줄이 `성장 배경` 위에 붙어 **속성
    // 아래**로 밀려 있었다(속성 열이 왼쪽 상자의 첫 자식이라서). 이름표는 창을 열자마자
    // 읽히는 자리에 있어야 한다.
    void AppendIdentity(UnitBrain brain, PawnMover pawn, UnitStats stats)
    {
        if (nameText != null)
        {
            string race = brain.Data != null ? brain.Data.RaceName : pawn.name;
            string tag  = stats.IsLocked ? "  <color=#888>[고정]</color>" : "";
            nameText.text = $"<b>{brain.UnitName}</b>  <size=85%><color=#E8C566>{race}</color></size>{tag}";
        }

        if (portrait == null) return;

        // 초상화가 없으면 종족 아이콘 — 그것도 없으면 빈 칸 색(로스터와 같은 규칙).
        var sprite = brain.EnsurePortrait();
        if (sprite == null && brain.Data != null) sprite = brain.Data.Icon;
        portrait.sprite = sprite;
        portrait.color  = sprite != null ? Color.white : emptyPortrait;
    }

    // **바뀌었을 때만 넣는다.** 매 프레임 같은 글자를 다시 넣으면 TMP가 그때마다 레이아웃을
    // 다시 잡고, 크기를 다시 재면서 **스크롤이 맨 위로 튕겨 올라간다** — 내려서 읽으려 하면
    // 첫 줄로 돌아오는 그 증상이다. 글자는 초당 몇 번 바뀔 값이 아니라 비교 하나로 끝난다.
    static void Assign(TextMeshProUGUI tmp, string next)
    {
        if (tmp != null && tmp.text != next) tmp.text = next;
    }

    // **무엇을 들었나** — 기술 열에서 파랗게 뜬 스탯이 어느 물건 때문인지를 여기서 잇는다.
    // 열에 이름을 붙이면 폭이 모자라 등급과 겹친다.
    static void AppendHeld(StringBuilder sb, Equipment equip)
    {
        if (equip == null) return;

        bool main   = equip.MainHand != null;
        bool shield = equip.OffHand != null && equip.OffHand.Type == WeaponType.Shield;
        if (!main && !shield) return;

        sb.AppendLine(H("든 것"));
        if (main)
            sb.AppendLine($"  <color=#AAF>{equip.MainHand.ItemName}</color> " +
                          $"<size=85%><color=#888>{UnitStats.StatFor(equip.MainHand).ToDisplay()}</color></size>");
        if (shield)
            sb.AppendLine($"  <color=#AAF>{equip.OffHand.ItemName}</color> " +
                          $"<size=85%><color=#888>{StatName.Shield.ToDisplay()}</color></size>");
        sb.AppendLine();
    }

    // 지금 손에 든 것의 스탯. **어느 물건인지는 왼쪽 상자가 적는다** — 열에서는 색만 바뀐다.
    static HashSet<StatName> HeldStats(Equipment equip)
    {
        var held = new HashSet<StatName>();
        if (equip?.MainHand != null) held.Add(UnitStats.StatFor(equip.MainHand));
        if (equip?.OffHand != null && equip.OffHand.Type == WeaponType.Shield)
            held.Add(StatName.Shield);
        return held;
    }

    // **특성 줄.** 이름 옆에 무엇을 얼마나 얹는지 같이 적는다 — 이름만 있으면 `곰손`이 좋은
    // 건지 나쁜 건지 알 수 없고, 툴팁을 따로 만들면 그걸 띄우려고 마우스를 올려야 한다.
    // 좋고 나쁨은 **색이 아니라 부호**가 말한다(특성 하나가 올리고 내리기를 같이 한다).
    static void AppendTraits(StringBuilder sb, UnitBrain brain)
    {
        if (brain.Traits == null || brain.Traits.Count == 0) return;

        // **이름만 적는다.** 오프셋(`강인함 +25 · 회피 +5`)을 줄에 붙이면 특성 셋에 성장 배경
        // 둘이면 **글이 창 밖으로 삐져나간다**(장비를 차서 `든 것` 절이 생기면 바로 그렇게 됐다).
        // 그리고 그 숫자는 **툴팁이 이미 다 갖고 있다** — 같은 것을 두 곳에 적고 있었던 셈이다.
        sb.AppendLine(H("■ 특성"));
        foreach (var t in brain.Traits)
        {
            if (t == null) continue;
            sb.AppendLine($"  {TraitLink(t, $"<b>{t.DisplayName}</b>")}");
        }
        sb.AppendLine();
    }

    // **성장 배경 줄.** 특성 바로 위다 — 특성이 *어떤 기질인가*라면 배경은 *어떻게 살아왔나*라,
    // 사람을 읽는 순서가 그쪽이 먼저다. 못 하는 일이 있으면 **그 줄에 같이 적는다**(붉게) —
    // 무능력은 스탯 몇 점보다 훨씬 크게 이 폰을 정하는데, 툴팁을 열어야만 보이면 P패널에서
    // "왜 이건 못 켜지"를 그때 처음 만나게 된다.
    static void AppendBackstories(StringBuilder sb, UnitBrain brain)
    {
        if (brain.Childhood == null && brain.Adulthood == null) return;

        sb.AppendLine(H("■ 성장 배경"));
        AppendStory(sb, "유년기", brain.Childhood);
        AppendStory(sb, "성년기", brain.Adulthood);
        sb.AppendLine();
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

    // `무능력: 제작 · 연구`
    static string BanSummary(BackstoryData b)
    {
        if (b?.DisabledJobs == null || b.DisabledJobs.Count == 0) return "";

        var names = new List<string>();
        foreach (var c in b.DisabledJobs) names.Add(JobCategoryInfo.DisplayName(c));
        return $"무능력: {string.Join(" · ", names)}";
    }

    static string StoryLink(BackstoryData b, string body) => $"<link={Q}story:{b.name}{Q}>{body}</link>";

    static string H(string t) => $"<color=#E8C566>{t}</color>";

    // TMP `<link>` — 글자 덩어리 **안에서** 영역을 잡는다. 스탯마다 오브젝트를 두지 않고도
    // 칸별로 다른 설명을 띄울 수 있는 이유가 이것이다. id는 숫자라 표시명이 바뀌어도 안 깨진다.
    static string TraitLink(TraitData t, string body) => $"<link={Q}trait:{t.name}{Q}>{body}</link>";

    const string Q = "\"";
}
