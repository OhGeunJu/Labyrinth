using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 무리 관리 패널의 폰 한 줄.
// [이름] [작업 체크박스 × QueueOrder] [현재 배정 + 해제] [전투 성향]
// 열 x좌표는 ColonyPanel의 헤더와 공유한다(ColonyColumns).
public class ColonyRowUI : MonoBehaviour
{
    static readonly Color OnColor    = new Color(0.20f, 0.50f, 0.28f, 1f);
    static readonly Color OffColor   = new Color(0.17f, 0.17f, 0.19f, 1f);
    static readonly Color AutoColor  = new Color(0.55f, 0.55f, 0.58f, 1f);
    static readonly Color FightColor = new Color(0.72f, 0.84f, 0.72f, 1f);
    static readonly Color CareColor  = new Color(0.88f, 0.80f, 0.45f, 1f);
    static readonly Color FleeColor  = new Color(0.90f, 0.55f, 0.40f, 1f);

    UnitBrain _brain;
    TextMeshProUGUI _nameText;
    TextMeshProUGUI _assignText;
    Button          _unassignButton;
    TextMeshProUGUI _stanceLabel;

    readonly Dictionary<JobCategory, Image>           _boxImages = new();
    readonly Dictionary<JobCategory, TextMeshProUGUI> _boxMarks  = new();

    public UnitBrain Brain => _brain;

    public void Build(UnitBrain brain, Action<UnitBrain> onSelect)
    {
        _brain = brain;

        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.03f);

        var le = gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = ColonyColumns.RowHeight;

        // 이름 — 클릭하면 해당 폰 선택
        var nameBtn = UIHelper.MakeButton(transform, "Name", brain.UnitName, Vector2.zero,
                                          Vector2.zero, 12f);
        Place(nameBtn.GetComponent<RectTransform>(), ColonyColumns.NameX, ColonyColumns.NameW);
        nameBtn.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        nameBtn.onClick.AddListener(() => onSelect?.Invoke(_brain));

        _nameText = nameBtn.GetComponentInChildren<TextMeshProUGUI>();
        _nameText.alignment = TextAlignmentOptions.Left;
        _nameText.margin    = new Vector4(6f, 0f, 0f, 0f);
        _nameText.color     = brain.Data is MinionData
            ? new Color(0.65f, 0.80f, 0.95f)   // 하수인
            : Color.white;                     // 방랑자

        // 작업 체크박스
        for (int i = 0; i < JobCategoryInfo.QueueOrder.Length; i++)
        {
            var category = JobCategoryInfo.QueueOrder[i];
            var box = UIHelper.MakeButton(transform, $"Job_{category}", "", Vector2.zero,
                                          Vector2.zero, 13f);
            Place(box.GetComponent<RectTransform>(), ColonyColumns.JobX(i), ColonyColumns.JobW);
            box.onClick.AddListener(() =>
            {
                _brain?.Work?.Toggle(category);
                Refresh();
            });

            _boxImages[category] = box.GetComponent<Image>();
            _boxMarks[category]  = box.GetComponentInChildren<TextMeshProUGUI>();
        }

        // 현재 배정 (채굴·벌목·순찰 = 직접 배정이라 토글이 아니라 상태로만 보여준다)
        _assignText = UIHelper.MakeLabel(transform, "Assign", "-", Vector2.zero, Vector2.zero, 11f);
        Place(_assignText.GetComponent<RectTransform>(), ColonyColumns.AssignX, ColonyColumns.AssignW);
        _assignText.alignment = TextAlignmentOptions.Left;
        _assignText.color     = new Color(0.80f, 0.80f, 0.84f);

        _unassignButton = UIHelper.MakeButton(transform, "Unassign", "해제", Vector2.zero,
                                              Vector2.zero, 11f);
        Place(_unassignButton.GetComponent<RectTransform>(), ColonyColumns.UnassignX,
              ColonyColumns.UnassignW);
        _unassignButton.onClick.AddListener(ClearAssignment);

        // 전투 성향
        var stanceBtn = UIHelper.MakeButton(transform, "Stance", "자동", Vector2.zero,
                                            Vector2.zero, 11f);
        Place(stanceBtn.GetComponent<RectTransform>(), ColonyColumns.StanceX, ColonyColumns.StanceW);
        stanceBtn.onClick.AddListener(CycleStance);
        _stanceLabel = stanceBtn.GetComponentInChildren<TextMeshProUGUI>();

        if (_brain.Work != null) _brain.Work.OnChanged += Refresh;

        Refresh();
    }

    void OnDestroy()
    {
        if (_brain != null && _brain.Work != null) _brain.Work.OnChanged -= Refresh;
    }

    // 자동(프로필/역할 기본값) → 교전 → 신중 → 도주 → 자동
    void CycleStance()
    {
        if (_brain == null) return;

        if (!_brain.HasStanceOverride)
            _brain.SetStanceOverride(CombatStance.Fight);
        else
        {
            switch (_brain.EffectiveStance)
            {
                case CombatStance.Fight:
                    _brain.SetStanceOverride(CombatStance.FightWhileHealthy); break;
                case CombatStance.FightWhileHealthy:
                    _brain.SetStanceOverride(CombatStance.Flee); break;
                default:
                    _brain.ClearStanceOverride(); break;
            }
        }

        Refresh();
    }

    void ClearAssignment()
    {
        var bb = _brain != null ? _brain.Blackboard : null;
        if (bb == null) return;

        if (bb.AssignedNode != null)       bb.AssignedNode.Unassign(_brain);
        if (bb.AssignedPatrolZone != null) bb.AssignedPatrolZone.Unassign(_brain);

        var bed = Bed.FindAssigned(_brain);
        if (bed != null) bed.Unassign();   // 침대 해제 = 다시 일하러 감

        Refresh();
    }

    public void Refresh()
    {
        if (_brain == null) return;

        var work = _brain.Work;
        foreach (var category in JobCategoryInfo.QueueOrder)
        {
            bool on = work != null && work.IsEnabled(category);
            if (_boxImages.TryGetValue(category, out var img)) img.color = on ? OnColor : OffColor;
            if (_boxMarks.TryGetValue(category, out var mark))
            {
                mark.text  = on ? "V" : "";
                mark.color = Color.white;
            }
        }

        // 배정 상태 — 침대(휴식)가 작업을 덮으므로 먼저 표시
        var bb  = _brain.Blackboard;
        var bed = Bed.FindAssigned(_brain);
        string assign = "-";
        if (bed != null)
        {
            assign = $"휴식: {bed.BedName}";
        }
        else if (bb != null && bb.AssignedNode != null)
        {
            var data     = bb.AssignedNode.Data;
            bool cutting = data != null && data.ResourceType == ResourceType.Wood;
            string what  = data != null ? data.ResourceType.DisplayName() : "자원";
            assign = $"{(cutting ? "벌목" : "채굴")}: {what}";
        }
        else if (bb != null && bb.AssignedPatrolZone != null)
        {
            assign = $"순찰: {bb.AssignedPatrolZone.ZoneName}";
        }

        _assignText.text = assign;
        bool assigned = assign != "-";
        _unassignButton.interactable = assigned;
        _unassignButton.GetComponent<Image>().color = assigned
            ? new Color(0.35f, 0.25f, 0.25f)
            : new Color(0.18f, 0.18f, 0.20f);

        // 전투 성향
        if (!_brain.HasStanceOverride)
        {
            _stanceLabel.text  = "자동";
            _stanceLabel.color = AutoColor;
        }
        else
        {
            switch (_brain.EffectiveStance)
            {
                case CombatStance.Flee:
                    _stanceLabel.text = "도주"; _stanceLabel.color = FleeColor; break;
                case CombatStance.FightWhileHealthy:
                    _stanceLabel.text = "신중"; _stanceLabel.color = CareColor; break;
                default:
                    _stanceLabel.text = "교전"; _stanceLabel.color = FightColor; break;
            }
        }
    }

    // 열 정렬을 위해 좌측 앵커 고정 배치 (행에는 LayoutGroup을 두지 않는다)
    public static void Place(RectTransform rt, float x, float w)
    {
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot     = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(w, ColonyColumns.RowHeight - 4f);
    }
}

// 헤더와 행이 공유하는 열 좌표
public static class ColonyColumns
{
    public const float RowHeight = 28f;

    public const float NameX = 6f;
    public const float NameW = 128f;

    public const float JobStartX = 140f;
    public const float JobW      = 52f;
    public const float JobGap    = 4f;

    public static float JobX(int index) => JobStartX + index * (JobW + JobGap);

    public static float AfterJobs =>
        JobX(Mathf.Max(0, JobCategoryInfo.QueueOrder.Length - 1)) + JobW + 12f;

    public static float AssignX   => AfterJobs;
    public const  float AssignW   = 138f;
    public static float UnassignX => AssignX + AssignW + 4f;
    public const  float UnassignW = 48f;
    public static float StanceX   => UnassignX + UnassignW + 10f;
    public const  float StanceW   = 62f;

    public static float TotalWidth => StanceX + StanceW + 12f;
}
