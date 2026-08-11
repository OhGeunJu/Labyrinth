using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 선택 유닛의 HP / 신체 / 장착 / 상태이상 — 항상 표시.
//
// **폰만 뜨는 창이 아니다.** 청사진을 누르면 같은 자리에 건설 진행이 뜬다 — 청사진은 다른
// 건물과 달리 제 패널이 없고(콜라이더가 없어 클릭 자체가 안 됐다), 그렇다고 그것 하나 때문에
// 패널을 새로 세우면 화면에 창만 는다. **고른 것을 보는 자리**는 하나면 족하다.
public class UnitHPUI : MonoBehaviour
{
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI infoText;
    [SerializeField] SelectionManager selectionManager;

    [Header("동작 버튼 줄 (건물·청사진용)")]
    [Tooltip("버튼들이 들어갈 가로 줄. 보여줄 것이 없으면 통째로 꺼진다.")]
    [SerializeField] Transform actionRow;
    [Tooltip("복제할 버튼 하나. **모양은 씬이 갖는다** — 코드는 글자와 눌렀을 때만 채운다.")]
    [SerializeField] Button actionTemplate;

    // 조작 불가 유닛(적/NPC)을 클릭했을 때의 조사 대상. 선택 유닛이 있으면 그쪽이 우선.
    UnitBrain _inspected;

    // 누르고 있는 청사진. **폰보다 앞선다** — 방금 그것을 눌렀기 때문이다(조사와 같은 규칙).
    BuildingBlueprint _blueprint;

    // 누르고 있는 건물. 청사진과 마찬가지로 한 클릭짜리다.
    PlacedBuilding _building;

    // 던전 핵 — `PlacedBuilding`이 아니라 따로 받는다(분해도 환불도 없고 이전만 있다).
    DungeonCore _core;

    // 자원 노드. **이미 있던 `OnNodeSelected`를 같이 듣는다** — 노드 패널을 여는 이벤트가
    // 이미 있는데 여기에 또 만들면 둘이 어긋난다.
    ResourceNode _node;

    readonly List<Button> _actions = new();

    // 건설 모드 동안 숨김. **플래그로 들고 있어야 한다** — 이 패널은 선택만 있으면
    // 매 프레임 스스로 다시 켜지므로(Refresh가 패널을 켜는 역할까지 겸한다),
    // 건설 진입 때 한 번 끄는 것만으로는 다음 프레임에 되살아난다.
    bool _buildMode;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        // 템플릿은 **원본**이라 그리지 않는다 — 켜 두면 빈 버튼이 늘 한 개 붙어 있다.
        if (actionTemplate != null) actionTemplate.gameObject.SetActive(false);

        // **줄바꿈을 여기서 못 박는다.** 씬에서 꺼져 있었고, 폰 정보는 줄이 짧아 몇 달 동안
        // 드러나지 않았다 — 건물 설명이 들어오자 문장이 오른쪽에서 그냥 잘렸다.
        // 이건 취향이 아니라 **글자가 보이느냐**의 문제라 씬에 맡기지 않는다("모양은 씬이"의
        // 예외다 — 여백·색·크기와 달리 끄면 내용이 사라진다).
        if (infoText != null)
        {
            infoText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            infoText.overflowMode     = TMPro.TextOverflowModes.Overflow;
        }

        selectionManager.OnSelectionChanged   += (_, __) => Refresh();
        SelectionManager.OnPawnInspected      += OnPawnInspected;
        SelectionManager.OnBlueprintInspected += OnBlueprintInspected;
        SelectionManager.OnBuildingInspected  += OnBuildingInspected;
        SelectionManager.OnCoreInspected      += OnCoreInspected;
        SelectionManager.OnNodeSelected       += OnNodeSelected;
        BuildModeManager.OnActiveChanged      += OnBuildModeChanged;
    }

    void OnDestroy()
    {
        SelectionManager.OnPawnInspected      -= OnPawnInspected;
        SelectionManager.OnBlueprintInspected -= OnBlueprintInspected;
        SelectionManager.OnBuildingInspected  -= OnBuildingInspected;
        SelectionManager.OnCoreInspected      -= OnCoreInspected;
        SelectionManager.OnNodeSelected       -= OnNodeSelected;
        BuildModeManager.OnActiveChanged      -= OnBuildModeChanged;
    }

    void OnPawnInspected(UnitBrain brain)
    {
        _inspected = brain;
        Refresh();
    }

    void OnBlueprintInspected(BuildingBlueprint bp)
    {
        _blueprint = bp;
        Refresh();
    }

    void OnBuildingInspected(PlacedBuilding pb)
    {
        _building = pb;
        Refresh();
    }

    void OnCoreInspected(DungeonCore core)
    {
        _core = core;
        Refresh();
    }

    void OnNodeSelected(ResourceNode node)
    {
        _node = node;
        Refresh();
    }

    void OnBuildModeChanged(bool active)
    {
        _buildMode = active;
        // 조사(적/NPC·청사진·건물 클릭)는 일회성이라 건설에 들어가면 버린다. 선택은 지속
        // 상태라 그대로 두고, 건설을 빠져나오면 Refresh가 알아서 패널을 되살린다.
        if (active)
        {
            _inspected = null; _blueprint = null; _building = null; _core = null; _node = null;
        }
        Refresh();
    }

    // **매 프레임 다시 그리지 않는다.** `Refresh`는 큰 문자열을 통째로 짓고(혈액·허기·장착·
    // 신체 7부위·특성), 청사진·노드 화면은 `MaterialPool.AvailableAnywhere`까지 부른다 —
    // 그건 플레이어 폰의 배낭을 전부 훑는 일이다.
    //
    // 여기 뜨는 값 중 프레임 단위로 움직이는 것은 하나도 없다(혈액이 가장 빠른데 초당 몇이다).
    // 다른 패널들이 이미 이 방식이다 — `PawnRosterUI` 0.3초, `ColonyUI` 0.5초.
    [SerializeField] float refreshInterval = 0.15f;
    float _refreshTimer;

    void Update()
    {
        bool showing = selectionManager.Selected.Count > 0 || _inspected != null
                    || _blueprint != null || _building != null || _core != null || _node != null;
        if (!showing) return;

        _refreshTimer -= Time.unscaledDeltaTime;   // 멈춰 놓고 들여다볼 때도 갱신돼야 한다
        if (_refreshTimer > 0f) return;
        _refreshTimer = refreshInterval;

        Refresh();
    }

    void Refresh()
    {
        if (_buildMode) { panel?.SetActive(false); return; }

        // **방금 누른 것이 먼저다**(조사와 같은 규칙). 청사진이 다 지어지거나 취소되면,
        // 건물이 분해되면 `== null`이 되어 저절로 폰 쪽으로 돌아간다.
        if (_blueprint != null) { ShowBlueprint(_blueprint); return; }
        if (_node      != null) { ShowNode(_node);           return; }
        if (_building  != null) { ShowBuilding(_building);   return; }
        if (_core      != null) { ShowCore(_core);           return; }

        // **방금 눌러 본 것이 우선**, 없으면 고른 폰.
        //
        // 순서가 반대였다 — 그래서 적을 보려면 `SelectionManager`가 폰 선택을 지워야 했고,
        // 적을 한 번 볼 때마다 폰을 다시 골라야 했다. 이제 선택은 그대로 두고 **무엇을
        // 보여줄지만** 여기서 가른다. 조사는 일회성이라 내 폰을 다시 고르면 그 순간
        // `OnPawnInspected(null)`이 와서 저절로 풀린다(빈 곳 클릭·사각 선택도 같다).
        UnitBrain brain = _inspected;

        if (brain == null && selectionManager.Selected.Count > 0)
        {
            var selected = selectionManager.Selected[0];
            if (selected != null) brain = selected.GetComponent<UnitBrain>();
        }

        if (brain == null) { panel?.SetActive(false); return; }   // 선택 없음 / 파괴된 유닛
        panel?.SetActive(true);

        SetPawnActions(brain);

        var pawn = brain.gameObject;
        var body = brain.GetComponent<BodyPartSystem>();
        var sfx  = brain.GetComponent<StatusEffectSystem>();

        var sb    = new StringBuilder();
        var equip = brain.Equipment;

        // 헤더 — 이름 [유형] (세력)
        var faction = brain.GetComponent<Faction>();
        var corpse  = brain.GetComponent<Corpse>();

        // **시체에는 `Faction` 컴포넌트가 없다** — `BecomeCorpse`가 자동 탐지를 막으려고 지운다.
        // 소속은 `Corpse`가 받아 뒀으므로 거기서 읽는다(안 그러면 죽는 순간 소속이 사라진다).
        var factionData = faction != null ? faction.Data : corpse != null ? corpse.Faction : null;

        string raceName = !string.IsNullOrEmpty(brain.UnitName) ? brain.UnitName
                        : brain.Data != null ? brain.Data.RaceName : pawn.name;
        string factionName = factionData != null
            ? $"  <color=#7AA>({factionData.FactionName})</color>" : "";
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

        // 허기 — 플레이어 세력만 시뮬레이션되므로 그 유닛에만, 그리고 **살아 있을 때만** 표시.
        // 전엔 `faction == null || faction.IsPlayer`였는데 시체는 `Faction`이 지워져 있어
        // 그 `== null` 구멍으로 들어왔다. 모르는 것은 플레이어가 아니고, **죽은 자는 안 배고프다.**
        if (corpse == null && faction != null && faction.IsPlayer)
        {
            float hunger = brain.Blackboard.Hunger;
            string hungerColor = hunger > 60f ? "#8BC34A" : hunger > 30f ? "#FF9800" : "#F44336";
            sb.AppendLine($"허기  {BarColored(hunger, 100f, hungerColor, hungerColor)}  <color={hungerColor}>{hunger:F0}/100</color>");
        }

        // **업고 있는 것과 갇힌 것은 화면에 나와야 한다.** 둘 다 그 폰이 왜 느린지·왜
        // 아무것도 안 하는지의 답인데, 모델만 봐서는 알기 어렵다.
        AppendCarryState(sb, brain);

        // 활성 상태이상
        if (sfx != null) AppendStatusEffects(sb, sfx);

        // **특성 — 이름만.** 여긴 상태를 보는 창이라 오프셋 숫자까지 늘어놓으면 혈액·허기가
        // 밀린다. 무엇을 얼마나 얹는지는 스탯 창(C)이 답한다.
        AppendTraits(sb, brain);
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

    // ── 청사진 ────────────────────────────────────────────────────────────
    //
    // 여기서 답하는 것은 **"왜 아직 안 지어졌나"** 하나다. 자재가 모자란 것과 폰이 안 오는
    // 것은 할 일이 다르다 — 앞은 캐 오거나 사 오는 일이고, 뒤는 작업 설정을 켜는 일이다.
    void ShowBlueprint(BuildingBlueprint bp)
    {
        panel?.SetActive(true);
        if (infoText == null) return;

        var sb   = new StringBuilder();
        var item = bp.CostItem;
        string name = bp.Data != null ? bp.Data.BuildingName : "청사진";

        sb.AppendLine($"<b><color=#E8C566>{name}</color></b>  <color=#888>[청사진]</color>");
        sb.AppendLine();

        // 아직 물건이 아니므로 **무엇이 될 것인지**를 먼저 말한다 — 지어 놓고 나서
        // "이게 뭐였더라"가 되면 취소할지 말지를 못 정한다.
        if (bp.Data != null && !string.IsNullOrWhiteSpace(bp.Data.Description))
        {
            sb.AppendLine(bp.Data.Description);
            sb.AppendLine();
        }

        if (item != null && bp.CostRequired > 0)
        {
            sb.AppendLine($"자재  {BarColored(bp.Delivered, bp.CostRequired, "#6C9", "#353")}  " +
                          $"{item.ItemName} {bp.Delivered}/{bp.CostRequired}");

            // **창고만 세면 손에 든 것이 없는 것이 된다** — 등록 판정과 같은 축으로 본다.
            int stock = MaterialPool.AvailableAnywhere(item);
            string col = stock >= bp.Remaining ? "#8BC34A" : "#F44336";
            sb.AppendLine($"남은 것  <b>{bp.Remaining}</b>   " +
                          $"가진 것  <color={col}>{stock}</color>");

            if (stock < bp.Remaining)
                sb.AppendLine($"\n<color=#F90>{item.ItemName}이(가) 모자라 짓지 못하고 있습니다.</color>");
        }
        else
        {
            sb.AppendLine("<color=#888>자재가 필요 없습니다.</color>");
        }

        infoText.text = sb.ToString();

        // **버튼과 우클릭이 같은 `Cancel()`을 부른다.** 문이 여럿이어도 하는 일이 한 곳에
        // 있으면 어긋날 데가 없다 — 갈라지는 건 판단이 여러 곳에 흩어질 때다.
        SetActions(new List<(string, Action)> { ("건설을 취소한다", bp.Cancel) });
    }

    // ── 다 지은 건물 ──────────────────────────────────────────────────────
    //
    // 창고·침대·농지는 저마다 패널이 있고 그쪽은 **그 건물로 무엇을 하는지**를 다룬다.
    // 여기는 **건물 자체**다 — 무엇이고, 옮길 것인가, 걷어낼 것인가. 벽처럼 제 패널이 없는
    // 건물은 이것만 뜬다(전에는 눌러도 아무 일도 안 일어났다).
    void ShowBuilding(PlacedBuilding pb)
    {
        panel?.SetActive(true);
        if (infoText == null) return;

        var data = pb.Data;
        string name = data != null ? data.BuildingName : pb.name;

        var sb = new StringBuilder();
        sb.AppendLine($"<b><color=#E8C566>{name}</color></b>  <color=#888>[건물]</color>");
        sb.AppendLine();

        // **무엇을 하는 물건인지가 먼저다.** 크기·환불액은 이미 아는 사람에게나 쓸모가 있고,
        // 처음 보는 사람에게는 "보석 연마대"라는 이름만으로 답이 안 나온다.
        if (data != null && !string.IsNullOrWhiteSpace(data.Description))
        {
            sb.AppendLine(data.Description);
            sb.AppendLine();
        }

        if (data != null && data.Size.x > 0)
            sb.AppendLine($"<color=#888>크기 {Mathf.Max(1, data.Size.x)}×{Mathf.Max(1, data.Size.y)}</color>");

        // **침대는 전용 패널이 없다** — 하던 일(배정/해제)이 전부 우클릭으로 갔고, 남은 것은
        // "이게 어떤 침대이고 누가 누웠나"뿐이라 여기서 답한다.
        var bed = pb.GetComponent<Bed>();
        if (bed != null) AppendBedState(sb, bed);

        // **휴면은 여기서 말해 줘야 한다** — 그 건물의 패널은 아예 안 열리므로, 안 열리는
        // 이유를 들을 데가 없었다.
        var dorm = pb.GetComponent<BuildingDormancy>();
        if (dorm != null && dorm.IsDormant)
            sb.AppendLine("<color=#F90>핵 영역 밖이라 작동을 멈췄습니다.</color>");

        if (data != null && data.CostAmount > 0 && data.CostItem != null)
            sb.AppendLine($"<color=#888>분해하면 {data.CostItem.ItemName} {data.CostAmount}을(를) 전부 돌려받습니다.</color>");

        infoText.text = sb.ToString();

        SetActions(new List<(string, Action)>
        {
            ("이동한다", (Action)(() => BuildingRelocator.Begin(pb))),
            ("분해한다", pb.Demolish),
        });
    }

    // ── 자원 노드 ─────────────────────────────────────────────────────────
    //
    // **설명을 손으로 안 적는다.** 노드에 대해 알고 싶은 것(무엇이 나오나 · 캐는가 베는가 ·
    // 몇 명이 붙나 · 얼마나 어려운가)은 전부 `ResourceNodeData`에서 나온다 — 적어 두면
    // 값을 고칠 때마다 문장이 뒤처지고, 그러면 표가 아니라 거짓말이 된다.
    //
    // 씬에 흩어진 나무·바위는 `BuildingData`가 없어(`ResourceNodeData`만 있다) 이름을 물을
    // 데가 없다 — 그때는 나오는 물건에서 만든다.
    void ShowNode(ResourceNode node)
    {
        panel?.SetActive(true);
        if (infoText == null) return;

        var d  = node.Data;
        var pb = node.GetComponentInParent<PlacedBuilding>();

        string name = pb != null && pb.Data != null ? pb.Data.BuildingName
                    : d != null && d.DropItem != null ? $"{d.DropItem.ItemName} {(d.IsTree ? "나무" : "광맥")}"
                    : node.name;
        bool tree = d != null && d.IsTree;

        var sb = new StringBuilder();
        sb.AppendLine($"<b><color=#E8C566>{name}</color></b>  <color=#888>[{(tree ? "수목" : "광맥")}]</color>");
        sb.AppendLine();

        if (d != null && d.DropItem != null)
        {
            sb.AppendLine($"{(tree ? "베면" : "캐면")}  <b>{d.DropItem.ItemName}</b>  " +
                          $"<color=#888>한 번에 {d.DropMin}~{d.DropMax}개</color>");
            sb.AppendLine($"<color=#888>{(tree ? "벌목" : "채굴")} 권장 {d.RequiredSkill:F0}  ·  " +
                          $"동시 {node.AssignedCount}/{(node.MaxWorkers > 0 ? node.MaxWorkers.ToString() : "제한 없음")}명</color>");
            sb.AppendLine();
        }

        // **직접 배정이라 아무도 안 붙으면 영영 안 캔다** — 큐 작업이 아니라서 켜고 끄는
        // 문제가 아니고, 여기서 말해 주지 않으면 "왜 안 캐지"의 답이 화면에 없다.
        if (node.AssignedCount == 0)
            sb.AppendLine("<color=#F90>배정된 폰이 없습니다. 폰을 고르고 우클릭해 시키세요.</color>");
        else
            sb.AppendLine($"진행  {BarColored(node.GaugeProgress, 1f, "#6C9", "#353")}");

        int stock = node.Inventory != null && d != null && d.DropItem != null
                  ? node.Inventory.GetTotalQuantity(d.DropItem) : 0;
        if (stock > 0)
            sb.AppendLine($"<color=#888>쌓인 것 {stock}개 — 폰이 창고로 나릅니다.</color>");

        infoText.text = sb.ToString();

        // 씬에 흩어진 나무·바위는 `PlacedBuilding`이 없다 — 건드릴 수 없는 지형이므로 버튼도 없다.
        if (pb != null)
            SetActions(new List<(string, Action)>
            {
                ("이동한다", (Action)(() => BuildingRelocator.Begin(pb))),
                ("분해한다", pb.Demolish),
            });
        else
            SetActions(null);
    }

    // 침대가 어떤 용도이고 누가 쓰고 있나. 색 표식과 **같은 말을 글자로도** 적는다 —
    // 색만 있으면 무엇이 주황이었는지 외워야 한다.
    static void AppendBedState(StringBuilder sb, Bed bed)
    {
        sb.AppendLine(bed.ForPrisoners
            ? "용도  <color=#FF8C1A>죄수용</color>"
            : "용도  <color=#4CD95A>보통</color>");

        var who = bed.Occupant ?? bed.AssignedUnit;
        if (who == null)
        {
            sb.AppendLine("<color=#888>비어 있다.</color>");
            return;
        }

        string state = bed.Occupant == who ? "누워 있다" : "배정됨";
        sb.AppendLine($"{who.UnitName}  <color=#888>{state}</color>");

        var body = who.GetComponent<BodyPartSystem>();
        if (body != null && body.NeedsTreatment)
            sb.AppendLine("<color=#F90>치료가 필요하다.</color>");
    }

    // ── 던전 핵 ───────────────────────────────────────────────────────────
    //
    // 핵은 `PlacedBuilding`이 아니다 — 배치로 지은 것이 아니라 씬에 놓인 축이라 **분해도
    // 환불도 없다.** 그래도 "무엇이고 무엇을 할 수 있는가"는 다른 건물과 같은 자리에서 답한다.
    void ShowCore(DungeonCore core)
    {
        panel?.SetActive(true);
        if (infoText == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("<b><color=#E8C566>던전 핵</color></b>  <color=#888>[중심]</color>");
        sb.AppendLine();
        sb.AppendLine("이 거점의 중심. 하수인을 소환하고, 둘레에 영역을 편다 — 영역 밖 건물은 작동을 멈춘다.");
        sb.AppendLine();
        sb.AppendLine($"<color=#888>하수인 {core.CurrentMinionCount}/{core.TotalMinionCap}</color>");
        if (core.Data != null)
            sb.AppendLine($"<color=#888>영역 반경 {core.Data.BaseRadius:F0}m</color>");

        infoText.text = sb.ToString();

        // 소환·목록은 핵 제 패널(`DungeonCoreUI`)이 다룬다 — 여기는 **핵 자체**를 다루는 자리다.
        SetActions(new List<(string, Action)>
        {
            ("이동한다", (Action)(() => CoreRelocator.Begin(core))),
        });
    }

    // 폰을 볼 때의 동작 줄 — **붙잡아 갈 사람에게만 뜬다.**
    //
    // 폰에게 시키는 일 대부분은 우클릭이 답한다(치료·따라가기·공격). 여기 이 둘만 둔 이유는
    // **쓰러진 적을 좌클릭해서 살펴본 다음**(무엇을 지녔나, 얼마나 다쳤나) 바로 정하는 흐름이
    // 실제로 있기 때문이다 — 보고 나서 우클릭을 다시 하려면 그 사이에 창이 닫힌다.
    //
    // 판단도 명령도 `PrisonEscort`가 한다. 우클릭 메뉴와 **같은 답**이어야 한다 — 문이 둘인데
    // 규칙이 둘이면 어디서 눌렀느냐로 결과가 갈린다.
    void SetPawnActions(UnitBrain brain)
    {
        var commanders = selectionManager.Selected;
        var items = new List<(string, Action)>();

        Add("우리로 옮긴다",      EscortDest.Cage);
        Add("죄수 침대로 옮긴다", EscortDest.Bed);

        SetActions(items.Count > 0 ? items : null);

        void Add(string label, EscortDest dest)
        {
            if (!PrisonEscort.CanOrder(commanders, brain, dest)) return;
            items.Add((label, (Action)(() => PrisonEscort.Order(commanders, brain, dest))));
        }
    }

    // ── 동작 버튼 ─────────────────────────────────────────────────────────
    //
    // **모양은 씬이 갖는다**(`actionTemplate`을 복제한다) — 코드가 글자와 눌렀을 때만 채운다.
    // 여백·색·크기를 코드가 만들면 편집 중 모양과 게임 모양이 갈라진다.
    void SetActions(List<(string label, Action act)> items)
    {
        if (actionRow == null || actionTemplate == null) return;

        int count = items?.Count ?? 0;
        actionRow.gameObject.SetActive(count > 0);

        // 남는 버튼은 지우지 않고 **끈다** — 매번 만들고 부수면 클릭한 그 프레임에 자기가
        // 파괴되고, 레이아웃도 한 박자 늦게 따라온다.
        while (_actions.Count < count)
        {
            var b = Instantiate(actionTemplate, actionRow);
            b.name = $"Action{_actions.Count}";
            _actions.Add(b);
        }

        for (int i = 0; i < _actions.Count; i++)
        {
            var b = _actions[i];
            if (b == null) continue;

            if (i >= count) { b.gameObject.SetActive(false); continue; }

            var (label, act) = items[i];
            b.gameObject.SetActive(true);
            var text = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) text.text = label;

            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => act?.Invoke());
        }
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
        // **"하수인"은 내 편일 때만이다.** 같은 종족 에셋을 쓰는 야생 개체가 습격으로
        // 들어오면 적 머리 위에 "하수인"이 뜬다 — 소속을 정하는 건 세력이지 에셋 클래스가 아니다.
        if (brain.Data is MinionData && faction != null && faction.IsPlayer) return "하수인";
        if (brain.Npc != null) return brain.Npc.NpcType switch
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

    // 업고 있나 / 업혀 있나 / 갇혀 있나. 해당 없으면 줄 자체가 안 뜬다.
    static void AppendCarryState(StringBuilder sb, UnitBrain brain)
    {
        var bb = brain.Blackboard;
        if (bb == null) return;

        if (bb.Carrying != null)
        {
            float w   = PawnCarry.WeightOf(bb.Carrying);
            float cap = CarryCapacity.For(brain);
            // 얼마나 느려지는지까지 적는다 — 업기의 값이 곧 이 숫자다.
            string slow = cap > 0f && w > cap ? $"  <color=#F90>속도 {cap / w * 100f:F0}%</color>" : "";
            sb.AppendLine($"업음  <color=#D8C89A>{bb.Carrying.UnitName}</color>  " +
                          $"<color=#888>{w:F0}kg</color>{slow}");
            sb.AppendLine("<color=#888>한 손이 막혀 양손 무기와 방패를 못 쓴다.</color>");
        }

        if (bb.CarriedBy != null)
            sb.AppendLine($"<color=#888>{bb.CarriedBy.UnitName}에게 업혀 있다.</color>");

        if (bb.IsPrisoner)
        {
            sb.AppendLine("<color=#F90>갇혀 있다.</color>");
            AppendConversion(sb, brain);
        }

        if (bb.LeavingMap)
            sb.AppendLine("<color=#8BC34A>풀려나 떠나는 중이다. 건드리지 않으면 그냥 간다.</color>");
    }

    // 회유 진행 — **길이 둘이라 무엇이 채우는지도 같이 적는다.** 게이지만 보이면 "왜 안
    // 차지"에 답이 없다: 하수인 종족은 핵이 채우고(영역 안이어야 한다), 사람은 폰이 설득해야
    // 오른다. 안 차는 이유가 있으면 그것부터 말한다.
    static void AppendConversion(StringBuilder sb, UnitBrain brain)
    {
        var bb = brain.Blackboard;
        bool minionRace = brain.Data is MinionData;

        sb.AppendLine($"회유  {BarColored(bb.ConvertProgress, 1f, "#A66CC9", "#3A2A45")}  " +
                      $"{bb.ConvertProgress * 100f:F0}%");

        if (minionRace)
        {
            string why = Conversion.CoreBlockedReason(brain);
            if (why != null)
                sb.AppendLine($"<color=#F90>{why}</color>");
            else if (Conversion.CanDominateNow(brain))
                // **게이지가 곧 결과가 아니다** — 누르는 순간이 도박이라 확률을 미리 적는다.
                sb.AppendLine($"<color=#8BC34A>지배할 준비가 됐다 — 우클릭 [지배를 시도한다] " +
                              $"(성공 {Conversion.DominateChance() * 100f:F0}%, 실패하면 죽는다).</color>");
            else
                sb.AppendLine("<color=#888>던전 핵이 지배하는 중 — 두면 저절로 찬다.</color>");
        }
        else if (!Conversion.CanPersuade(brain))
        {
            // 우리가 아니면 설득도 안 된다 — **침대는 살려 두는 자리**다.
            sb.AppendLine("<color=#F90>우리에 가둬야 설득할 수 있다.</color>");
        }
        else
        {
            // **설득은 굴림이라 성공률을 적는다**(지배와 같은 규칙 — 모르게 굴리지 않는다).
            // 회차 사이 쉬는 시간도 함께: 안 적으면 "간수를 켰는데 왜 안 가지"가 된다.
            float wait = Conversion.PersuadeCooldownLeft(brain);
            if (wait > 0f)
                sb.AppendLine($"<color=#888>설득을 쉬는 중 — {Mathf.CeilToInt(wait)}초 뒤에 다시 붙는다.</color>");
            else
                sb.AppendLine("<color=#888>말이 통하는 상대 — 간수가 붙거나 우클릭 [설득한다].</color>");
        }
    }

    // 한 줄로 붙여 쓴다 — `특성  강인함 · 겁쟁이`. 없는 폰(0개)은 줄 자체가 안 뜬다.
    static void AppendTraits(StringBuilder sb, UnitBrain brain)
    {
        string names = TraitPool.NameList(brain.Traits);
        if (string.IsNullOrEmpty(names)) return;

        sb.AppendLine($"특성  <color=#D8C89A>{names}</color>");
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
