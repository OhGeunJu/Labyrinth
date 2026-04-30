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

        // 열려있는 동안 매 프레임 갱신
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
        sb.AppendLine($"<b><color=#E8C566>{raceName}</color></b>  <color=#888>스탯</color>");
        sb.AppendLine();

        // 속성
        sb.AppendLine(H("■ 속성"));
        sb.AppendLine(Row2("근력",   $"{stats.Strength:F1}",
                           "강인함", $"{stats.Toughness:F1}"));
        sb.AppendLine(Row2("손재주", $"{stats.Dexterity:F1}",
                           "인지력", $"{stats.Cognition:F1}"));
        sb.AppendLine();

        // 전투 기술
        sb.AppendLine(H("■ 전투"));
        sb.AppendLine(Row2("근접공격",   $"{stats.MeleeAttack:F1}",
                           "근접방어",   $"{stats.MeleeDefense:F1}"));
        sb.AppendLine(Row2("원거리방어", $"{stats.RangedDefense:F1}",
                           "정밀사격",   $"{stats.PrecisionShooting:F1}"));
        sb.AppendLine(Row2("무술",       $"{stats.MartialArts:F1}",
                           "회피",       $"{stats.Evasion:F1}"));
        sb.AppendLine($"  육상  <b>{stats.Athletics:F1}</b>");

        // 장착 무기 숙련도
        if (equip?.MainHand != null)
        {
            float skill = stats.GetSkillForWeapon(equip.MainHand.Type);
            sb.AppendLine($"  <color=#AAF>{equip.MainHand.ItemName} 숙련도  <b>{skill:F1}</b></color>");
        }
        sb.AppendLine();

        // 마법 (기본값 초과 시만)
        AppendMagicSkills(sb, stats);

        // 노동
        AppendLaborSkills(sb, stats);

        if (infoText != null) infoText.text = sb.ToString();
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────

    static void AppendMagicSkills(StringBuilder sb, UnitStats s)
    {
        bool any = s.ElementalMagic > 1 || s.DarkMagic > 1 || s.HolyMagic > 1
                || s.ElementalFire > 1 || s.ElementalIce > 1 || s.ElementalLightning > 1 || s.ElementalPoison > 1
                || s.DarkFire > 1 || s.DarkIce > 1 || s.DarkLightning > 1 || s.DarkPoison > 1
                || s.HolyFire > 1 || s.HolyIce > 1 || s.HolyLightning > 1 || s.HolyPoison > 1;
        if (!any) return;

        sb.AppendLine(H("■ 마법"));
        if (s.ElementalMagic > 1 || s.DarkMagic > 1 || s.HolyMagic > 1)
            sb.AppendLine($"  원소 <b>{s.ElementalMagic:F1}</b>   암흑 <b>{s.DarkMagic:F1}</b>   신성 <b>{s.HolyMagic:F1}</b>");

        AppendElementRow(sb, "원소", s.ElementalFire,  s.ElementalIce,  s.ElementalLightning,  s.ElementalPoison);
        AppendElementRow(sb, "암흑", s.DarkFire,       s.DarkIce,       s.DarkLightning,       s.DarkPoison);
        AppendElementRow(sb, "신성", s.HolyFire,       s.HolyIce,       s.HolyLightning,       s.HolyPoison);
        sb.AppendLine();
    }

    static void AppendElementRow(StringBuilder sb, string prefix, float fire, float ice, float lightning, float poison)
    {
        if (fire <= 1 && ice <= 1 && lightning <= 1 && poison <= 1) return;
        sb.AppendLine($"  {prefix}  불 {fire:F1}  얼음 {ice:F1}  전기 {lightning:F1}  독 {poison:F1}");
    }

    static void AppendLaborSkills(StringBuilder sb, UnitStats s)
    {
        var list = new System.Collections.Generic.List<string>();
        if (s.Mining         > 1) list.Add($"채굴 {s.Mining:F1}");
        if (s.Growing        > 1) list.Add($"원예 {s.Growing:F1}");
        if (s.Cutting        > 1) list.Add($"벌목 {s.Cutting:F1}");
        if (s.Taming         > 1) list.Add($"조련 {s.Taming:F1}");
        if (s.Cooking        > 1) list.Add($"요리 {s.Cooking:F1}");
        if (s.Medicine       > 1) list.Add($"의료 {s.Medicine:F1}");
        if (s.Research       > 1) list.Add($"연구 {s.Research:F1}");
        if (s.WeaponCrafting > 1) list.Add($"무기제작 {s.WeaponCrafting:F1}");
        if (s.ArmorCrafting  > 1) list.Add($"갑옷제작 {s.ArmorCrafting:F1}");
        if (s.Alchemy        > 1) list.Add($"연금 {s.Alchemy:F1}");
        if (s.Labor          > 1) list.Add($"노동 {s.Labor:F1}");
        if (list.Count == 0) return;

        sb.AppendLine(H("■ 노동"));
        for (int i = 0; i < list.Count; i += 2)
        {
            if (i + 1 < list.Count) sb.AppendLine(Row2("", list[i], "", list[i + 1]));
            else                    sb.AppendLine($"  {list[i]}");
        }
    }

    static string H(string t) => $"<color=#E8C566>{t}</color>";

    static string Row2(string l1, string v1, string l2, string v2)
    {
        string left  = l1.Length > 0 ? $"{l1}  <b>{v1}</b>" : v1;
        string right = l2.Length > 0 ? $"{l2}  <b>{v2}</b>" : v2;
        return $"  {left,-20}  {right}";
    }
}
