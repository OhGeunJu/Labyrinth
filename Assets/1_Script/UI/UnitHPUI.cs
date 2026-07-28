using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

// 선택 유닛의 HP / 신체 / 장착 / 상태이상 — 항상 표시.
public class UnitHPUI : MonoBehaviour
{
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI infoText;
    [SerializeField] SelectionManager selectionManager;

    // 조작 불가 유닛(적/NPC)을 클릭했을 때의 조사 대상. 선택 유닛이 있으면 그쪽이 우선.
    UnitBrain _inspected;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        selectionManager.OnSelectionChanged += (_, __) => Refresh();
        SelectionManager.OnPawnInspected    += OnPawnInspected;
        BuildModeManager.OnActiveChanged    += OnBuildModeChanged;
    }

    void OnDestroy()
    {
        SelectionManager.OnPawnInspected -= OnPawnInspected;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void OnPawnInspected(UnitBrain brain)
    {
        _inspected = brain;
        Refresh();
    }

    void OnBuildModeChanged(bool active)
    {
        if (!active) return;
        _inspected = null;
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (selectionManager.Selected.Count > 0 || _inspected != null) Refresh();
    }

    void Refresh()
    {
        // 내 유닛 선택이 우선, 없으면 클릭으로 조사 중인 유닛
        UnitBrain brain = null;
        if (selectionManager.Selected.Count > 0)
        {
            var selected = selectionManager.Selected[0];
            if (selected != null) brain = selected.GetComponent<UnitBrain>();
        }
        else if (_inspected != null)
        {
            brain = _inspected;
        }

        if (brain == null) { panel?.SetActive(false); return; }   // 선택 없음 / 파괴된 유닛
        panel?.SetActive(true);

        var pawn = brain.gameObject;
        var body = brain.GetComponent<BodyPartSystem>();
        var sfx  = brain.GetComponent<StatusEffectSystem>();

        var sb    = new StringBuilder();
        var equip = brain.Equipment;

        // 헤더 — 이름 [유형] (세력)
        var faction = brain.GetComponent<Faction>();
        string raceName = !string.IsNullOrEmpty(brain.UnitName) ? brain.UnitName
                        : brain.Data != null ? brain.Data.RaceName : pawn.name;
        string factionName = faction != null && faction.Data != null
            ? $"  <color=#7AA>({faction.Data.FactionName})</color>" : "";
        sb.AppendLine($"<b><color=#E8C566>{raceName}</color></b>  <color=#888>[{UnitTypeLabel(brain, faction)}]</color>{factionName}");
        sb.AppendLine();

        // 혈액 / 보호막 / 부패
        if (body != null)
        {
            sb.AppendLine($"혈액  {BloodBar(body.Blood)}");
            if (sfx != null && sfx.ShieldPool > 0f)
                sb.AppendLine($"보호막  <color=#4AF>{sfx.ShieldPool:F0}</color>");
            if (sfx != null && sfx.CorruptionGauge > 0f)
                sb.AppendLine($"부패  {BarColored(sfx.CorruptionGauge, StatusEffectSystem.MaxCorruption, "#A020F0", "#6010A0")}  {sfx.CorruptionGauge:F0}/{StatusEffectSystem.MaxCorruption:F0}");
        }

        // 허기 — 플레이어 세력만 시뮬레이션되므로 그 유닛에만 표시
        if (faction == null || faction.IsPlayer)
        {
            float hunger = brain.Blackboard.Hunger;
            string hungerColor = hunger > 60f ? "#8BC34A" : hunger > 30f ? "#FF9800" : "#F44336";
            sb.AppendLine($"허기  {BarColored(hunger, 100f, hungerColor, hungerColor)}  <color={hungerColor}>{hunger:F0}/100</color>");
        }

        // 활성 상태이상
        if (sfx != null) AppendStatusEffects(sb, sfx);
        sb.AppendLine();

        // 장착
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

        // 신체 부위
        if (body != null)
        {
            sb.AppendLine(H("■ 신체"));
            sb.AppendLine(Row2("머리",   FormatPart(body, BodyPartType.Head),
                               "가슴",   FormatPart(body, BodyPartType.Chest)));
            sb.AppendLine(Row2("복부",   FormatPart(body, BodyPartType.Abdomen),
                               "왼팔",   FormatPart(body, BodyPartType.LeftArm)));
            sb.AppendLine(Row2("오른팔", FormatPart(body, BodyPartType.RightArm),
                               "왼다리", FormatPart(body, BodyPartType.LeftLeg)));
            sb.AppendLine($"  오른다리  {FormatPart(body, BodyPartType.RightLeg)}");
        }

        if (infoText != null) infoText.text = sb.ToString();
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────

    static void AppendStatusEffects(StringBuilder sb, StatusEffectSystem sfx)
    {
        var parts = new List<string>();
        if (sfx.AttackIntervalMult > 1.05f) parts.Add("<color=#4CF>[빙결 둔화]</color>");
        if (sfx.MoveSpeedMult      < 0.95f) parts.Add("<color=#4CF>[이속감소]</color>");
        if (sfx.HealMult           < 0.95f) parts.Add("<color=#F44>[힐감소]</color>");
        if (sfx.AttackChanceMod    < 0.95f) parts.Add("<color=#F84>[약화]</color>");
        if (parts.Count > 0) sb.AppendLine("상태  " + string.Join("  ", parts));
    }

    // 유형 라벨 — 플레이어 유닛은 방랑자/하수인, NPC는 역할, 그 외는 **우리 세력과의 관계**.
    // (비플레이어를 무조건 "적"이라 부르면 중립 세력까지 적으로 표기된다)
    static string UnitTypeLabel(UnitBrain brain, Faction faction)
    {
        if (brain.Data is MinionData) return "하수인";
        if (brain.Data is NpcData npc) return npc.NpcType switch
        {
            NpcType.Villager      => "주민",
            NpcType.Guard         => "경비",
            NpcType.ShopMerchant  => "상인",
            NpcType.EventMerchant => "행상인",
            _                     => "NPC",
        };
        if (faction == null || faction.IsPlayer) return "방랑자";

        var player = Faction.Player;
        if (player == null) return "중립";
        return player.StanceTo(faction) switch
        {
            FactionStance.Hostile => "적",
            FactionStance.Ally    => "우호",
            _                     => "중립",
        };
    }

    static string H(string t)    => $"<color=#E8C566>{t}</color>";
    static string Row2(string l1, string v1, string l2, string v2)
    {
        string left  = l1.Length > 0 ? $"{l1}  <b>{v1}</b>" : v1;
        string right = l2.Length > 0 ? $"{l2}  <b>{v2}</b>" : v2;
        return $"  {left,-20}  {right}";
    }

    static string BloodBar(float blood, float max = 100f)
    {
        int filled = Mathf.Clamp(Mathf.RoundToInt(blood / max * 10), 0, 10);
        string color = blood > 60 ? "#E55" : blood > 30 ? "#F90" : "#F44";
        return $"<color={color}>{new string('█', filled)}{new string('░', 10 - filled)}</color>  {blood:F0}/{max:F0}";
    }

    static string BarColored(float val, float max, string fill, string empty)
    {
        int filled = Mathf.Clamp(Mathf.RoundToInt(val / max * 10), 0, 10);
        return $"<color={fill}>{new string('█', filled)}</color><color={empty}>{new string('░', 10 - filled)}</color>";
    }

    static string FormatPart(BodyPartSystem body, BodyPartType type)
    {
        var part = body.GetPart(type);
        if (part.IsDestroyed) return $"<color=#F44>{part.CurrentHP:F0}/{part.MaxHP:F0} [파괴]</color>";
        if (part.IsDisabled)  return $"<color=#F90>{part.CurrentHP:F0}/{part.MaxHP:F0} [상실]</color>";
        return $"{part.CurrentHP:F0}/{part.MaxHP:F0}";
    }
}
