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

    // 못 하는 일(성장 배경) — 켠 것도 끈 것도 아닌 **세 번째 상태**라 색도 따로 둔다.
    static readonly Color BlockedColor     = new(0.20f, 0.13f, 0.13f, 1f);
    static readonly Color BlockedMarkColor = new(0.85f, 0.40f, 0.40f, 1f);

    // 직접 배정(채굴·벌목·순찰)에 묶인 폰의 큐 작업 칸. 켜져 있지만 **지금은 안 한다**.
    static readonly Color OnHeldColor   = new Color(0.20f, 0.32f, 0.24f, 1f);
    static readonly Color MarkHeldColor = new Color(0.55f, 0.58f, 0.55f, 1f);
    static readonly Color AutoColor  = new Color(0.55f, 0.55f, 0.58f, 1f);
    static readonly Color FightColor = new Color(0.72f, 0.84f, 0.72f, 1f);
    static readonly Color CareColor  = new Color(0.88f, 0.80f, 0.45f, 1f);
    static readonly Color FleeColor  = new Color(0.90f, 0.55f, 0.40f, 1f);

    UnitBrain _brain;
    TextMeshProUGUI _nameText;
    TextMeshProUGUI _assignText;
    Button          _unassignButton;
    TextMeshProUGUI _stanceLabel;
    Image           _unloadImage;
    TextMeshProUGUI _unloadLabel;
    Image           _selfTendImage;
    TextMeshProUGUI _selfTendLabel;

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
        for (int i = 0; i < ColonyColumns.WorkColumns.Length; i++)
        {
            var category = ColonyColumns.WorkColumns[i];
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

        // 짐내림 — 작업이 아니라 **이 폰을 어떻게 굴릴까**에 가까워 성향 옆에 둔다.
        // 칸 안 글자는 작업 체크박스와 같은 `V`다. 무엇을 켜는 칸인지는 **열 제목**이 말한다 —
        // 칸마다 이름을 다시 적으면 켜짐/꺼짐이 글자에 묻힌다.
        var unloadBtn = UIHelper.MakeButton(transform, "Unload", "", Vector2.zero,
                                            Vector2.zero, 13f);
        Place(unloadBtn.GetComponent<RectTransform>(), ColonyColumns.UnloadX, ColonyColumns.UnloadW);
        unloadBtn.onClick.AddListener(ToggleUnload);
        _unloadImage = unloadBtn.GetComponent<Image>();
        _unloadLabel = unloadBtn.GetComponentInChildren<TextMeshProUGUI>();

        // 자가치유 — 짐내림과 같은 성격이다. 남의 일감을 집는 것이 아니라
        // **이 폰을 어떻게 굴릴까**라서 작업 체크 무리에서 떨어뜨려 둔다.
        var tendBtn = UIHelper.MakeButton(transform, "SelfTend", "", Vector2.zero,
                                          Vector2.zero, 13f);
        Place(tendBtn.GetComponent<RectTransform>(), ColonyColumns.SelfTendX, ColonyColumns.SelfTendW);
        tendBtn.onClick.AddListener(ToggleSelfTend);
        _selfTendImage = tendBtn.GetComponent<Image>();
        _selfTendLabel = tendBtn.GetComponentInChildren<TextMeshProUGUI>();

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

    void ToggleSelfTend()
    {
        _brain?.Work?.ToggleSelfTend();
        Refresh();
    }

    void ToggleUnload()
    {
        _brain?.Work?.Toggle(JobCategory.Unload);
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

        // 배정 상태 — 침대(휴식)가 작업을 덮으므로 먼저 표시
        var bb  = _brain.Blackboard;
        var bed = Bed.FindAssigned(_brain);

        // **직접 배정은 큐 작업을 통째로 밀어낸다.** `WorkNode`는 `AssignedNode`가 있으면
        // 거기서 끝나고(`TickAssigned`는 늘 Running), 순찰도 트리에서 작업보다 위다 —
        // 즉 배정된 폰은 `RequestJob`을 아예 호출하지 않는다.
        //
        // 그런데 화면에는 "운반 V"가 멀쩡히 켜져 있어서, 광맥마다 폰을 붙여 두면
        // **운반 켠 폰이 다섯인데 실제로 나르는 건 배정 안 된 한 명**이 된다.
        // 체크는 남기되 흐리게 그려 "켜 뒀지만 지금은 못 한다"를 눈에 보이게 한다.
        // (침대는 아니다 — 졸릴 때만 쓰이지 폰을 붙들지 않는다.)
        bool held = bb != null && (bb.AssignedNode != null || bb.AssignedPatrolZone != null);

        var work = _brain.Work;
        foreach (var category in ColonyColumns.WorkColumns)
        {
            bool on      = work != null && work.IsEnabled(category);
            bool blocked = work != null && work.IsBlocked(category);

            if (_boxImages.TryGetValue(category, out var img))
                img.color = blocked ? BlockedColor : on ? (held ? OnHeldColor : OnColor) : OffColor;

            if (_boxMarks.TryGetValue(category, out var mark))
            {
                // **못 하는 일은 빈 칸이 아니라 `×`다**(성장 배경의 무능력). 빈 칸으로 두면
                // *"안 켠 것"*과 구별이 안 돼, 눌러 보고 나서야 안 켜진다는 것을 알게 된다.
                mark.text  = blocked ? "×" : on ? "V" : "";
                mark.color = blocked ? BlockedMarkColor : held ? MarkHeldColor : Color.white;
            }
        }

        string assign = "-";
        if (bed != null)
        {
            assign = $"휴식: {bed.BedName}";
        }
        else if (bb != null && bb.AssignedNode != null)
        {
            var node     = bb.AssignedNode;
            var data     = node.Data;
            bool cutting = data != null && data.IsTree;
            string what  = data?.DropItem != null ? data.DropItem.ItemName : "자원";
            // 자리 수도 같이 적는다 — 여기가 "누구를 어디서 빼올까"를 정하는 화면이라,
            // 그 노드가 이미 꽉 찼는지 아닌지가 판단에 바로 필요하다.
            string room  = node.MaxWorkers > 0 ? $" {node.AssignedCount}/{node.MaxWorkers}" : "";
            assign = $"{(cutting ? "벌목" : "채굴")}: {what}{room}";
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

        // 짐내림 — 켜짐/꺼짐만. 직접 배정에 묶여 있어도 짐은 내릴 수 있으므로 흐리게 하지 않는다.
        bool unload = work != null && work.IsEnabled(JobCategory.Unload);
        if (_unloadImage != null) _unloadImage.color = unload ? OnColor : OffColor;
        if (_unloadLabel != null)
        {
            _unloadLabel.text  = unload ? "V" : "";
            _unloadLabel.color = Color.white;
        }

        // 자가치유 — 켜짐/꺼짐만. 의료 스탯이 낮아도 끄지 않는다(맨손으로도 하는 일이다).
        bool selfTend = work != null && work.SelfTend;
        if (_selfTendImage != null) _selfTendImage.color = selfTend ? OnColor : OffColor;
        if (_selfTendLabel != null)
        {
            _selfTendLabel.text  = selfTend ? "V" : "";
            _selfTendLabel.color = Color.white;
        }

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

    // **작업 열은 `짐내림`을 뺀 것들 + 채굴·벌목이다.**
    //
    // 짐내림은 큐를 타긴 하지만 플레이어에게는 "무슨 일을 시킬까"가 아니라 "이 폰이 짐을 지고
    // 다니게 둘까"에 가깝다 — 작업 체크 사이에 끼면 남이 시킨 일과 같은 무게로 읽힌다.
    // 그래서 성향 옆에 따로 세운다.
    //
    // **채굴·벌목은 반대다 — 큐에는 없는데 여기에는 있어야 한다.** 그 둘은 일감을 만들지
    // 않으므로(`WorkNode.TickAssigned`가 노드를 직접 캔다) `QueueOrder`에 넣을 수 없지만,
    // 자율 배정(`ResourceNode.ClaimFreeFor`)이 이 스위치를 본다. **칸이 없으면 끌 수가 없고,
    // 그러면 자율 배정이 강제가 된다.** 그래서 이 배열과 `QueueOrder`는 이제 다른 물음에
    // 답한다: 저쪽은 *큐 배정 순서*, 이쪽은 *무슨 일을 시킬까*.
    //
    // **경비는 안 넣는다** — 순찰 구역은 자율 배정이 없어 여전히 손으로만 붙인다.
    public static readonly JobCategory[] WorkColumns = BuildWorkColumns();

    static JobCategory[] BuildWorkColumns()
    {
        var src = JobCategoryInfo.QueueOrder;
        var list = new List<JobCategory>(src.Length + 2);
        foreach (var c in src)
            if (c != JobCategory.Unload) list.Add(c);

        // 맨 뒤 — 자율 배정이 큐를 다 비운 뒤에 돌므로 우선순위도 실제로 여기다.
        list.Add(JobCategory.Mining);
        list.Add(JobCategory.Logging);
        return list.ToArray();
    }

    public static float JobX(int index) => JobStartX + index * (JobW + JobGap);

    public static float AfterJobs =>
        JobX(Mathf.Max(0, WorkColumns.Length - 1)) + JobW + 12f;

    public static float AssignX   => AfterJobs;
    public const  float AssignW   = 138f;
    public static float UnassignX => AssignX + AssignW + 4f;
    public const  float UnassignW = 48f;
    public static float StanceX   => UnassignX + UnassignW + 10f;
    public const  float StanceW   = 62f;

    // 성향 오른쪽 — 작업 무리에서 떨어뜨려 둔다.
    public static float UnloadX   => StanceX + StanceW + 10f;
    public const  float UnloadW   = 56f;

    // 자가치유 — 짐내림과 같은 성격("이 폰을 어떻게 굴릴까")이라 그 옆에 붙인다.
    public static float SelfTendX => UnloadX + UnloadW + 6f;
    public const  float SelfTendW = 62f;

    public static float TotalWidth => SelfTendX + SelfTendW + 12f;
}
