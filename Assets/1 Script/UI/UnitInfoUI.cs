using UnityEngine;
using TMPro;

public class UnitInfoUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TextMeshProUGUI infoText;
    [SerializeField] SelectionManager selectionManager;

    void Awake()
    {
        panel.SetActive(false);
        selectionManager.OnSelectionChanged += (pawn, selected) => UpdateUI();
    }

    void Update()
    {
        if (selectionManager.Selected.Count > 0)
            UpdateUI();
    }

    void UpdateUI()
    {
        if (selectionManager.Selected.Count == 0)
        {
            panel.SetActive(false);
            return;
        }

        panel.SetActive(true);

        var pawn  = selectionManager.Selected[0];
        var brain = pawn.GetComponent<UnitBrain>();
        var body  = pawn.GetComponent<BodyPartSystem>();

        if (brain == null) return;

        var stats    = brain.Stats;
        var equip    = brain.Equipment;
        var sb       = new System.Text.StringBuilder();

        // 이름 / 유닛 종류
        string unitType = brain.Data is MinionData ? "[하수인]" : "[방랑자]";
        sb.AppendLine($"{(brain.Data != null ? brain.Data.RaceName : pawn.name)} {unitType}");

        // 장착 무기
        string weapon = equip?.MainHand != null ? equip.MainHand.ItemName : "맨손";
        sb.AppendLine($"무기: {weapon}");
        sb.AppendLine();

        // 혈액
        if (body != null)
        {
            sb.AppendLine($"혈액: {body.Blood:F0} / 100");
            sb.AppendLine();

            // 부위별 체력
            sb.AppendLine("── 신체 부위 ──");
            sb.AppendLine(FormatPart(body, BodyPartType.Head,     "머리  "));
            sb.AppendLine(FormatPart(body, BodyPartType.Chest,    "가슴  "));
            sb.AppendLine(FormatPart(body, BodyPartType.Abdomen,  "복부  "));
            sb.AppendLine(FormatPart(body, BodyPartType.LeftArm,  "왼팔  "));
            sb.AppendLine(FormatPart(body, BodyPartType.RightArm, "오른팔"));
            sb.AppendLine(FormatPart(body, BodyPartType.LeftLeg,  "왼다리"));
            sb.AppendLine(FormatPart(body, BodyPartType.RightLeg, "오른다리"));
            sb.AppendLine();
        }

        // 속성
        sb.AppendLine("── 속성 ──");
        sb.AppendLine($"근력:   {stats.Strength:F1}");
        sb.AppendLine($"강인함: {stats.Toughness:F1}");
        sb.AppendLine($"손재주: {stats.Dexterity:F1}");
        sb.AppendLine($"인지력: {stats.Cognition:F1}");
        sb.AppendLine();

        // 전투 기술
        sb.AppendLine("── 전투 기술 ──");
        sb.AppendLine($"근접공격: {stats.MeleeAttack:F1}");
        sb.AppendLine($"근접방어: {stats.MeleeDefense:F1}");
        sb.AppendLine($"정밀사격: {stats.PrecisionShooting:F1}");
        sb.AppendLine($"무술:     {stats.MartialArts:F1}");
        sb.AppendLine($"회피:     {stats.Evasion:F1}");
        sb.AppendLine($"육상:     {stats.Athletics:F1}");
        sb.AppendLine();

        // 노동
        sb.AppendLine("── 노동 ──");
        sb.AppendLine($"채굴: {stats.Mining:F1}   원예: {stats.Growing:F1}");
        sb.AppendLine($"벌목: {stats.Cutting:F1}   조련: {stats.Taming:F1}");
        sb.AppendLine($"요리: {stats.Cooking:F1}   의료: {stats.Medicine:F1}");
        sb.AppendLine($"연구: {stats.Research:F1}");

        infoText.text = sb.ToString();
    }

    string FormatPart(BodyPartSystem body, BodyPartType type, string label)
    {
        var part = body.GetPart(type);
        string state = part.IsDestroyed ? " [파괴]" : part.IsDisabled ? " [기능상실]" : "";
        return $"{label}: {part.CurrentHP:F0} / {part.MaxHP:F0}{state}";
    }
}
