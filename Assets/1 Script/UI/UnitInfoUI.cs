using UnityEngine;
using TMPro;
using System.Text;

public class UnitInfoUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TextMeshProUGUI infoText;
    [SerializeField] SelectionManager selectionManager;

    void Awake()
    {
        panel.SetActive(false);
selectionManager.OnSelectionChanged += (_, __) => UpdateUI();
    }

    void Update()
    {
        if (selectionManager.Selected.Count > 0)
            UpdateUI();
    }

    void UpdateUI()
    {
        if (selectionManager.Selected.Count == 0) { panel.SetActive(false); return; }
        panel.SetActive(true);

        var pawn  = selectionManager.Selected[0];
        var brain = pawn.GetComponent<UnitBrain>();
        var body  = pawn.GetComponent<BodyPartSystem>();
        var sfx   = pawn.GetComponent<StatusEffectSystem>();
        if (brain == null) return;

        var stats = brain.Stats;
        var equip = brain.Equipment;
        var sb    = new StringBuilder();

        // ── 헤더 ─────────────────────────────────────────────────────
        string unitType = brain.Data is MinionData ? "하수인" : "방랑자";
        string raceName = brain.Data != null ? brain.Data.RaceName : pawn.name;
        sb.AppendLine($"<b><color=#E8C566>{raceName}</color></b>  <color=#888>[{unitType}]</color>");
        sb.AppendLine();

        // ── 혈액 / 상태이상 ──────────────────────────────────────────
        if (body != null)
        {
            sb.AppendLine($"혈액  {BloodBar(body.Blood)}");

            if (sfx != null && sfx.ShieldPool > 0f)
                sb.AppendLine($"보호막  <color=#4AF>{sfx.ShieldPool:F0}</color>");
            if (sfx != null && sfx.CorruptionGauge > 0f)
                sb.AppendLine($"부패  {BarColored(sfx.CorruptionGauge, StatusEffectSystem.MaxCorruption, "#A020F0", "#6010A0")}  {sfx.CorruptionGauge:F0}/{StatusEffectSystem.MaxCorruption:F0}");
        }

        // 활성 상태이상
        if (sfx != null)
            AppendStatusEffects(sb, sfx);

        sb.AppendLine();

        // ── 장착 ─────────────────────────────────────────────────────
        if (equip != null)
        {
            sb.AppendLine(H("■ 장착"));
            sb.AppendLine(Row2("주손",   equip.MainHand?.ItemName ?? "-",
                               "보조손", equip.OffHand?.ItemName  ?? "-"));
            sb.AppendLine(Row2("머리",   equip.GetArmor(ArmorSlot.Head)?.ItemName  ?? "-",
                               "몸통",   equip.GetArmor(ArmorSlot.Chest)?.ItemName ?? "-"));
            sb.AppendLine(Row2("손",     equip.GetArmor(ArmorSlot.Hands)?.ItemName ?? "-",
                               "다리",   equip.GetArmor(ArmorSlot.Legs)?.ItemName  ?? "-"));
            sb.AppendLine();
        }

        // ── 신체 부위 ─────────────────────────────────────────────────
        if (body != null)
        {
            sb.AppendLine(H("■ 신체"));
            sb.AppendLine(Row2("머리",    FormatPart(body, BodyPartType.Head),
                               "가슴",    FormatPart(body, BodyPartType.Chest)));
            sb.AppendLine(Row2("복부",    FormatPart(body, BodyPartType.Abdomen),
                               "왼팔",    FormatPart(body, BodyPartType.LeftArm)));
            sb.AppendLine(Row2("오른팔",  FormatPart(body, BodyPartType.RightArm),
                               "왼다리",  FormatPart(body, BodyPartType.LeftLeg)));
            sb.AppendLine($"  오른다리  {FormatPart(body, BodyPartType.RightLeg)}");
            sb.AppendLine();
        }

        // ── 속성 ─────────────────────────────────────────────────────
        sb.AppendLine(H("■ 속성"));
        sb.AppendLine(Row2("근력",   $"{stats.Strength:F1}",
                           "강인함", $"{stats.Toughness:F1}"));
        sb.AppendLine(Row2("손재주", $"{stats.Dexterity:F1}",
                           "인지력", $"{stats.Cognition:F1}"));
        sb.AppendLine();

        // ── 전투 기술 ─────────────────────────────────────────────────
        sb.AppendLine(H("■ 전투"));
        sb.AppendLine(Row2("근접공격",   $"{stats.MeleeAttack:F1}",
                           "근접방어",   $"{stats.MeleeDefense:F1}"));
        sb.AppendLine(Row2("원거리방어", $"{stats.RangedDefense:F1}",
                           "정밀사격",   $"{stats.PrecisionShooting:F1}"));
        sb.AppendLine(Row2("무술",       $"{stats.MartialArts:F1}",
                           "회피",       $"{stats.Evasion:F1}"));
        sb.AppendLine($"  육상  {stats.Athletics:F1}");

        // 장착 무기 숙련도
        if (equip?.MainHand != null)
        {
            var w       = equip.MainHand;
            float skill = stats.GetSkillForWeapon(w.Type);
            sb.AppendLine($"  <color=#AAF>{w.ItemName} 숙련도:  {skill:F1}</color>");
        }

        // 마법 숙련도 (1 초과 시만 표시)
        AppendMagicSkills(sb, stats);
        sb.AppendLine();

        // ── 노동 (기본값 1 초과 시만) ──────────────────────────────
        AppendLaborSkills(sb, stats);

        infoText.text = sb.ToString();
    }

    // ── 섹션별 보조 메서드 ─────────────────────────────────────────────

    static void AppendStatusEffects(StringBuilder sb, StatusEffectSystem sfx)
    {
        // StatusEffectSystem의 활성 효과는 private이라 mod 값으로 추정 표시
        var parts = new System.Collections.Generic.List<string>();
        if (sfx.AttackIntervalMult > 1.05f)
            parts.Add($"<color=#4CF>[빙결 둔화]</color>");
        if (sfx.MoveSpeedMult < 0.95f)
            parts.Add($"<color=#4CF>[이속감소]</color>");
        if (sfx.HealMult < 0.95f)
            parts.Add($"<color=#F44>[힐감소]</color>");
        if (sfx.AttackChanceMod < 0.95f)
            parts.Add($"<color=#F84>[약화]</color>");

        if (parts.Count > 0)
            sb.AppendLine("상태  " + string.Join("  ", parts));
    }

    static void AppendMagicSkills(StringBuilder sb, UnitStats s)
    {
        bool any = s.ElementalMagic > 1 || s.DarkMagic > 1 || s.HolyMagic > 1
                || s.ElementalFire > 1 || s.ElementalIce > 1 || s.ElementalLightning > 1 || s.ElementalPoison > 1
                || s.DarkFire > 1 || s.DarkIce > 1 || s.DarkLightning > 1 || s.DarkPoison > 1
                || s.HolyFire > 1 || s.HolyIce > 1 || s.HolyLightning > 1 || s.HolyPoison > 1;
        if (!any) return;

        sb.AppendLine();
        sb.AppendLine(H("■ 마법"));
        if (s.ElementalMagic > 1 || s.DarkMagic > 1 || s.HolyMagic > 1)
            sb.AppendLine(Row3("원소", $"{s.ElementalMagic:F1}", "암흑", $"{s.DarkMagic:F1}", "신성", $"{s.HolyMagic:F1}"));

        AppendElementRow(sb, "원소", s.ElementalFire, s.ElementalIce, s.ElementalLightning, s.ElementalPoison);
        AppendElementRow(sb, "암흑", s.DarkFire,      s.DarkIce,      s.DarkLightning,      s.DarkPoison);
        AppendElementRow(sb, "신성", s.HolyFire,      s.HolyIce,      s.HolyLightning,      s.HolyPoison);
    }

    static void AppendElementRow(StringBuilder sb, string prefix,
                                  float fire, float ice, float lightning, float poison)
    {
        if (fire <= 1 && ice <= 1 && lightning <= 1 && poison <= 1) return;
        sb.AppendLine($"  {prefix}  불 {fire:F1}  얼음 {ice:F1}  전기 {lightning:F1}  독 {poison:F1}");
    }

    static void AppendLaborSkills(StringBuilder sb, UnitStats s)
    {
        var list = new System.Collections.Generic.List<string>();
        if (s.Mining   > 1) list.Add($"채굴 {s.Mining:F1}");
        if (s.Growing  > 1) list.Add($"원예 {s.Growing:F1}");
        if (s.Cutting  > 1) list.Add($"벌목 {s.Cutting:F1}");
        if (s.Taming   > 1) list.Add($"조련 {s.Taming:F1}");
        if (s.Cooking  > 1) list.Add($"요리 {s.Cooking:F1}");
        if (s.Medicine > 1) list.Add($"의료 {s.Medicine:F1}");
        if (s.Research > 1) list.Add($"연구 {s.Research:F1}");
        if (s.WeaponCrafting > 1) list.Add($"무기제작 {s.WeaponCrafting:F1}");
        if (s.ArmorCrafting  > 1) list.Add($"갑옷제작 {s.ArmorCrafting:F1}");
        if (s.Alchemy  > 1) list.Add($"연금 {s.Alchemy:F1}");
        if (s.Labor    > 1) list.Add($"노동 {s.Labor:F1}");

        if (list.Count == 0) return;
        sb.AppendLine(H("■ 노동"));
        for (int i = 0; i < list.Count; i += 2)
        {
            if (i + 1 < list.Count)
                sb.AppendLine(Row2("", list[i], "", list[i + 1]));
            else
                sb.AppendLine($"  {list[i]}");
        }
    }

    // ── 포맷 헬퍼 ────────────────────────────────────────────────────

    static string H(string text) => $"<color=#E8C566>{text}</color>";

    static string Row2(string l1, string v1, string l2, string v2)
    {
        string left  = l1.Length > 0 ? $"{l1}  <b>{v1}</b>" : v1;
        string right = l2.Length > 0 ? $"{l2}  <b>{v2}</b>" : v2;
        return $"  {left,-20}  {right}";
    }

    static string Row3(string l1, string v1, string l2, string v2, string l3, string v3)
        => $"  {l1} <b>{v1}</b>   {l2} <b>{v2}</b>   {l3} <b>{v3}</b>";

    static string BloodBar(float blood, float max = 100f)
    {
        int filled = Mathf.RoundToInt(blood / max * 10);
        filled = Mathf.Clamp(filled, 0, 10);
        string color = blood > 60 ? "#E55" : blood > 30 ? "#F90" : "#F44";
        return $"<color={color}>{new string('█', filled)}{new string('░', 10 - filled)}</color>  {blood:F0}/{max:F0}";
    }

    static string BarColored(float val, float max, string fillColor, string emptyColor)
    {
        int filled = Mathf.Clamp(Mathf.RoundToInt(val / max * 10), 0, 10);
        return $"<color={fillColor}>{new string('█', filled)}</color><color={emptyColor}>{new string('░', 10 - filled)}</color>";
    }

    static string FormatPart(BodyPartSystem body, BodyPartType type)
    {
        var part = body.GetPart(type);
        if (part.IsDestroyed)
            return $"<color=#F44>{part.CurrentHP:F0}/{part.MaxHP:F0} [파괴]</color>";
        if (part.IsDisabled)
            return $"<color=#F90>{part.CurrentHP:F0}/{part.MaxHP:F0} [상실]</color>";
        return $"{part.CurrentHP:F0}/{part.MaxHP:F0}";
    }
}
