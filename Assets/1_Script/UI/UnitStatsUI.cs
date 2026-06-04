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

        if (panel != null && panel.activeSelf) Refresh();
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

        // 속성
        sb.AppendLine(H("■ 속성"));
        sb.AppendLine(Row2("근력",   F(stats, StatName.Strength),
                           "강인함", F(stats, StatName.Toughness)));
        sb.AppendLine(Row2("손재주", F(stats, StatName.Dexterity),
                           "인지력", F(stats, StatName.Cognition)));
        sb.AppendLine();

        // 전투 기술
        sb.AppendLine(H("■ 전투"));
        sb.AppendLine(Row2("근접공격",   F(stats, StatName.MeleeAttack),
                           "근접방어",   F(stats, StatName.MeleeDefense)));
        sb.AppendLine(Row2("원거리방어", F(stats, StatName.RangedDefense),
                           "정밀사격",   F(stats, StatName.PrecisionShooting)));
        sb.AppendLine(Row2("무술",       F(stats, StatName.MartialArts),
                           "회피",       F(stats, StatName.Evasion)));
        sb.AppendLine($"  육상  {F(stats, StatName.Athletics)}");

        if (equip?.MainHand != null)
        {
            StatName wStat = WeaponToStat(equip.MainHand.Type);
            sb.AppendLine($"  <color=#AAF>{equip.MainHand.ItemName} 숙련도  {F(stats, wStat)}</color>");
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
            sb.AppendLine($"  원소 {F(s, StatName.ElementalMagic)}   암흑 {F(s, StatName.DarkMagic)}   신성 {F(s, StatName.HolyMagic)}");

        AppendElementRow(sb, s, "원소", StatName.ElementalFire,  StatName.ElementalIce,  StatName.ElementalLightning,  StatName.ElementalPoison);
        AppendElementRow(sb, s, "암흑", StatName.DarkFire,       StatName.DarkIce,       StatName.DarkLightning,       StatName.DarkPoison);
        AppendElementRow(sb, s, "신성", StatName.HolyFire,       StatName.HolyIce,       StatName.HolyLightning,       StatName.HolyPoison);
        sb.AppendLine();
    }

    static void AppendElementRow(StringBuilder sb, UnitStats s, string prefix, StatName fire, StatName ice, StatName lightning, StatName poison)
    {
        if (!HasAny(s, fire, ice, lightning, poison)) return;
        sb.AppendLine($"  {prefix}  불 {F(s, fire)}  얼음 {F(s, ice)}  전기 {F(s, lightning)}  독 {F(s, poison)}");
    }

    static void AppendLaborSkills(StringBuilder sb, UnitStats s)
    {
        var list = new System.Collections.Generic.List<string>();
        AddIfAny(list, s, "채굴",     StatName.Mining);
        AddIfAny(list, s, "원예",     StatName.Growing);
        AddIfAny(list, s, "벌목",     StatName.Cutting);
        AddIfAny(list, s, "조련",     StatName.Taming);
        AddIfAny(list, s, "요리",     StatName.Cooking);
        AddIfAny(list, s, "의료",     StatName.Medicine);
        AddIfAny(list, s, "연구",     StatName.Research);
        AddIfAny(list, s, "무기제작", StatName.WeaponCrafting);
        AddIfAny(list, s, "갑옷제작", StatName.ArmorCrafting);
        AddIfAny(list, s, "연금",     StatName.Alchemy);
        AddIfAny(list, s, "제련",     StatName.Smelting);
        AddIfAny(list, s, "석재가공", StatName.Stonecutting);
        AddIfAny(list, s, "목공",     StatName.Woodworking);
        AddIfAny(list, s, "보석가공", StatName.Gemcutting);
        AddIfAny(list, s, "노동",     StatName.Labor);
        if (list.Count == 0) return;

        sb.AppendLine(H("■ 노동"));
        for (int i = 0; i < list.Count; i += 2)
        {
            if (i + 1 < list.Count) sb.AppendLine($"  {list[i],-35}  {list[i + 1]}");
            else                    sb.AppendLine($"  {list[i]}");
        }
    }

    static void AddIfAny(System.Collections.Generic.List<string> list, UnitStats s, string label, StatName name)
    {
        if (s.GetStat(name) > 1f) list.Add($"{label} {F(s, name)}");
    }

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

    static string H(string t) => $"<color=#E8C566>{t}</color>";

    static string Row2(string l1, string v1, string l2, string v2)
        => $"  {l1}  {v1,-50}  {l2}  {v2}";
}
