using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// 선택 유닛의 스탯 패널 — C키 토글.
public class UnitStatsUI : MonoBehaviour
{
    [SerializeField] GameObject       panel;
    [SerializeField] TextMeshProUGUI  infoText;
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
        var equip = brain.Equipment;
        var sb    = new StringBuilder();

        string raceName = brain.Data != null ? brain.Data.RaceName : pawn.name;
        string lockTag  = stats.IsLocked ? "  <color=#888>[고정]</color>" : "";
        sb.AppendLine($"<b><color=#E8C566>{raceName}</color></b>  <color=#888>스탯</color>{lockTag}");
        sb.AppendLine();

        // **특성이 맨 위다.** 숫자 51개를 훑기 전에 "이 폰이 어떤 사람인가"부터 읽혀야 한다 —
        // 특성은 그 숫자들의 **요약**이 아니라 왜 그런 숫자가 나왔는지의 이유다.
        AppendTraits(sb, brain);

        // 속성
        sb.AppendLine(H("■ 속성"));
        sb.AppendLine(Row2(stats, StatName.Strength,  StatName.Toughness));
        sb.AppendLine(Row2(stats, StatName.Dexterity, StatName.Cognition));
        sb.AppendLine();

        // 전투 기술
        sb.AppendLine(H("■ 전투"));
        sb.AppendLine(Row2(stats, StatName.MeleeAttack,   StatName.MeleeDefense));
        sb.AppendLine(Row2(stats, StatName.RangedDefense, StatName.PrecisionShooting));
        sb.AppendLine(Row2(stats, StatName.MartialArts,   StatName.Evasion));
        sb.AppendLine($"  {Link(StatName.Athletics, Cell(stats, StatName.Athletics))}");

        if (equip?.MainHand != null)
        {
            StatName wStat = WeaponToStat(equip.MainHand.Type);
            string worn = $"<color=#AAF>{equip.MainHand.ItemName} 숙련도  {F(stats, wStat)}</color>";
            sb.AppendLine($"  {Link(wStat, worn)}");
        }
        sb.AppendLine();

        AppendMagicSkills(sb, stats);
        AppendLaborSkills(sb, stats);

        if (infoText != null) infoText.text = sb.ToString();
    }

    // 등급 + 진행도 텍스트 생성
    static string F(UnitStats stats, StatName name)
    {
        float val   = stats.GetStat(name);
        StatGrade g = stats.GetGrade(name);
        string hex  = ColorUtility.ToHtmlStringRGB(g.GetColor());

        if (g == StatGrade.SSSPlus)
            return $"<color=#{hex}><b>{g.ToDisplay()}</b></color>";

        float currentBase = (int)g * StatGradeExt.StepValue;
        int percent       = Mathf.Clamp(Mathf.FloorToInt((val - currentBase) / StatGradeExt.StepValue * 100f), 0, 99);
        return $"<color=#{hex}><b>{g.ToDisplay()}</b></color> <size=70%><color=#888>{percent}%</color></size>";
    }

    static void AppendMagicSkills(StringBuilder sb, UnitStats s)
    {
        bool any = HasAny(s, StatName.ElementalMagic, StatName.DarkMagic, StatName.HolyMagic,
            StatName.ElementalFire, StatName.ElementalIce, StatName.ElementalLightning, StatName.ElementalPoison,
            StatName.DarkFire, StatName.DarkIce, StatName.DarkLightning, StatName.DarkPoison,
            StatName.HolyFire, StatName.HolyIce, StatName.HolyLightning, StatName.HolyPoison);
        if (!any) return;

        sb.AppendLine(H("■ 마법"));
        if (HasAny(s, StatName.ElementalMagic, StatName.DarkMagic, StatName.HolyMagic))
            sb.AppendLine($"  {Link(StatName.ElementalMagic, $"원소 {F(s, StatName.ElementalMagic)}")}   " +
                          $"{Link(StatName.DarkMagic, $"암흑 {F(s, StatName.DarkMagic)}")}   " +
                          $"{Link(StatName.HolyMagic, $"신성 {F(s, StatName.HolyMagic)}")}");

        AppendElementRow(sb, s, "원소", StatName.ElementalFire,  StatName.ElementalIce,  StatName.ElementalLightning,  StatName.ElementalPoison);
        AppendElementRow(sb, s, "암흑", StatName.DarkFire,       StatName.DarkIce,       StatName.DarkLightning,       StatName.DarkPoison);
        AppendElementRow(sb, s, "신성", StatName.HolyFire,       StatName.HolyIce,       StatName.HolyLightning,       StatName.HolyPoison);
        sb.AppendLine();
    }

    static void AppendElementRow(StringBuilder sb, UnitStats s, string prefix, StatName fire, StatName ice, StatName lightning, StatName poison)
    {
        if (!HasAny(s, fire, ice, lightning, poison)) return;
        sb.AppendLine($"  {prefix}  {Link(fire, $"불 {F(s, fire)}")}  {Link(ice, $"얼음 {F(s, ice)}")}  " +
                      $"{Link(lightning, $"전기 {F(s, lightning)}")}  {Link(poison, $"독 {F(s, poison)}")}");
    }

    // **가진 것만 뜬다**(값 1 초과). 순서는 여기 적힌 대로다.
    static readonly StatName[] LaborStats =
    {
        StatName.Mining, StatName.Growing, StatName.Cutting, StatName.Taming, StatName.Cooking,
        StatName.Medicine, StatName.Research, StatName.Social,
        StatName.WeaponCrafting, StatName.ArmorCrafting,
        StatName.Alchemy, StatName.Smelting, StatName.Stonecutting, StatName.Woodworking,
        StatName.Gemcutting, StatName.Labor,
    };

    static void AppendLaborSkills(StringBuilder sb, UnitStats s)
    {
        var list = new System.Collections.Generic.List<StatName>();
        foreach (var n in LaborStats)
            if (s.GetStat(n) > 1f) list.Add(n);
        if (list.Count == 0) return;

        sb.AppendLine(H("■ 노동"));
        for (int i = 0; i < list.Count; i += 2)
        {
            // 정렬은 **링크를 씌우기 전** 원문에 건다 — 태그까지 세면 열이 어긋난다.
            string left = Cell(s, list[i]);
            if (i + 1 < list.Count)
                sb.AppendLine($"  {Link(list[i], left + Pad(left, 35))}  {Link(list[i + 1], Cell(s, list[i + 1]))}");
            else
                sb.AppendLine($"  {Link(list[i], left)}");
        }
    }

    static string Pad(string raw, int width)
        => raw.Length >= width ? "" : new string(' ', width - raw.Length);

    static bool HasAny(UnitStats s, params StatName[] names)
    {
        foreach (var n in names) if (s.GetStat(n) > 1f) return true;
        return false;
    }

    static StatName WeaponToStat(WeaponType type) => type switch
    {
        WeaponType.OneHandSword       => StatName.OneHandSword,
        WeaponType.OneHandAxe         => StatName.OneHandAxe,
        WeaponType.OneHandMace        => StatName.OneHandMace,
        WeaponType.TwoHandSword       => StatName.TwoHandSword,
        WeaponType.TwoHandAxe         => StatName.TwoHandAxe,
        WeaponType.TwoHandMace        => StatName.TwoHandMace,
        WeaponType.Polearm            => StatName.Polearm,
        WeaponType.Bow                => StatName.Bow,
        WeaponType.Crossbow           => StatName.Crossbow,
        WeaponType.Javelin            => StatName.Javelin,
        WeaponType.ElementalFire      => StatName.ElementalFire,
        WeaponType.ElementalIce       => StatName.ElementalIce,
        WeaponType.ElementalLightning => StatName.ElementalLightning,
        WeaponType.ElementalPoison    => StatName.ElementalPoison,
        WeaponType.DarkFire           => StatName.DarkFire,
        WeaponType.DarkIce            => StatName.DarkIce,
        WeaponType.DarkLightning      => StatName.DarkLightning,
        WeaponType.DarkPoison         => StatName.DarkPoison,
        WeaponType.HolyFire           => StatName.HolyFire,
        WeaponType.HolyIce            => StatName.HolyIce,
        WeaponType.HolyLightning      => StatName.HolyLightning,
        WeaponType.HolyPoison         => StatName.HolyPoison,
        _                             => StatName.MartialArts,
    };

    // **특성 줄.** 이름 옆에 무엇을 얼마나 얹는지 같이 적는다 — 이름만 있으면 `곰손`이 좋은
    // 건지 나쁜 건지 알 수 없고, 툴팁을 따로 만들면 그걸 띄우려고 마우스를 올려야 한다.
    // 좋고 나쁨은 **색이 아니라 부호**가 말한다(특성 하나가 올리고 내리기를 같이 한다).
    static void AppendTraits(StringBuilder sb, UnitBrain brain)
    {
        if (brain.Traits == null || brain.Traits.Count == 0) return;

        sb.AppendLine(H("■ 특성"));
        foreach (var t in brain.Traits)
        {
            if (t == null) continue;
            string effect = t.EffectSummary();
            string row = string.IsNullOrEmpty(effect)
                ? $"<b>{t.DisplayName}</b>"
                : $"<b>{t.DisplayName}</b>  <color=#888>{effect}</color>";
            sb.AppendLine($"  {TraitLink(t, row)}");
        }
        sb.AppendLine();
    }

    static string H(string t) => $"<color=#E8C566>{t}</color>";

    // **한 줄에 스탯 둘.** 정렬 폭(-50)은 예전 그대로 `F()` 결과에만 걸린다 — 링크 태그는
    // 그 **바깥**을 감싸므로 칸이 밀리지 않는다(태그 길이를 정렬에 섞으면 열이 어긋난다).
    static string Row2(UnitStats s, StatName n1, StatName n2)
        => $"  {Link(n1, $"{n1.ToDisplay()}  {F(s, n1),-50}")}  {Link(n2, Cell(s, n2))}";

    static string Cell(UnitStats s, StatName n) => $"{n.ToDisplay()}  {F(s, n)}";

    // TMP `<link>` — 글자 덩어리 **안에서** 영역을 잡는다. 스탯마다 오브젝트를 두지 않고도
    // 칸별로 다른 설명을 띄울 수 있는 이유가 이것이다. id는 숫자라 표시명이 바뀌어도 안 깨진다.
    static string Link(StatName n, string body) => $"<link={Q}stat:{(int)n}{Q}>{body}</link>";

    static string TraitLink(TraitData t, string body) => $"<link={Q}trait:{t.name}{Q}>{body}</link>";

    const string Q = "\"";
}
