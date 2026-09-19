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
public class InspectPanelUI : MonoBehaviour
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
    Forageable   _forage;
    Vector2Int?  _rock;

    // 바닥에 떨어진 물건. **자동 운반이 켜졌는지**를 볼 자리가 여기다 — 초록 구슬만으로는
    // "왜 안 실려 가지"에 답이 안 된다(구슬이 없는 것과 창고가 없는 것이 같아 보인다).
    readonly List<DroppedItem> _items = new();

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
        SelectionManager.OnForageInspected    += OnForageInspected;
        SelectionManager.OnRockInspected      += OnRockInspected;
        SelectionManager.OnItemsInspected     += OnItemsInspected;
        BuildModeManager.OnActiveChanged      += OnBuildModeChanged;

        BuildHaulToggle();
    }

    // ── 이름 옆의 운반 스위치 ─────────────────────────────────────────────────
    //
    // **글자가 아니라 진짜 버튼이다.** 처음엔 TMP `<mark>`으로 판을 깔아 알약처럼 그렸는데,
    // 색이 글자색과 뒤엉켜 꺼짐 상태가 **회색 판에 회색 글씨**로 안 보였다. 무엇보다 글자는
    // 눌러 보기 전에는 눌리는지 알 수가 없다.
    //
    // **씬의 버튼 템플릿을 그대로 복제한다**(`actionTemplate` — 아래 동작 줄이 쓰는 것과 같은
    // 원본). 모양이 다른 버튼과 저절로 같아지고, 코드가 정하는 것은 **자리와 글자와 색**뿐이다
    // ("모양은 씬이, 코드는 데이터만"). 자리를 코드가 잡는 것은 씬에 앵커를 새로 파지 않기
    // 위해서다 — `ResearchTableUI`가 게이지를 런타임에 버튼 자리로 옮기는 것과 같은 길.
    Button          _haulToggle;
    TextMeshProUGUI _haulLabel;
    Image           _haulBg;

    void BuildHaulToggle()
    {
        if (actionTemplate == null || panel == null) return;
        if (panel.transform is not RectTransform panelRect) return;

        var btn = Instantiate(actionTemplate, panelRect);
        btn.name = "HaulToggle";

        if (btn.transform is RectTransform rt)
        {
            // 패널 **오른쪽 위**. 이름줄과 같은 높이라 "이 물건의 스위치"로 읽힌다.
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-12f, -10f);
            rt.sizeDelta = new Vector2(88f, 28f);
        }

        _haulToggle = btn;
        _haulLabel  = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        _haulBg     = btn.GetComponent<Image>();

        btn.gameObject.SetActive(false);
    }

    // `on == null`이면 숨긴다(고른 것이 바닥 물건이 아닐 때).
    void SetHaulToggle(bool? on)
    {
        if (_haulToggle == null) return;

        if (on == null) { _haulToggle.gameObject.SetActive(false); return; }

        _haulToggle.gameObject.SetActive(true);

        // **꺼짐도 읽혀야 한다.** 판과 글자가 둘 다 회색이면 무슨 글자인지 안 보인다 —
        // 판은 어둡게, 글자는 밝게 둔다(켜짐은 초록 판 + 흰 글자).
        bool value = on.Value;
        if (_haulBg    != null) _haulBg.color    = value ? new Color(0.18f, 0.42f, 0.22f)
                                                         : new Color(0.20f, 0.20f, 0.22f);
        if (_haulLabel != null)
        {
            // **토글이 아니라 상태로 적는다**(사용자 결정) — 체크박스는 *지금 어느 쪽인지*를
            // 눈이 한 번 더 재야 하는데, 살펴보기 창은 **읽는 창**이다. 품목 창에서는
            // 여럿을 빠르게 켜고 끄는 자리라 거기서만 토글이다.
            _haulLabel.text  = value ? "자동운반 켜짐" : "자동운반 꺼짐";
            _haulLabel.color = value ? new Color(0.85f, 0.98f, 0.87f) : new Color(0.80f, 0.80f, 0.82f);
        }

        _haulToggle.onClick.RemoveAllListeners();
        _haulToggle.onClick.AddListener(ToggleHaulOnSelection);
    }

    // **이 한 개가 아니라 그 품목을 켠다**(2026-09-06, 사용자 결정).
    //
    // 하나를 눌러 켜면 **세계의 그 품목 전부**가 따라온다 — 바닥에 널린 갓버섯 스무 개를
    // 하나씩 찍는 일이 없어지고, 무엇보다 **앞으로 떨어지는 것에도 걸린다.** 개별로 찍는
    // 길은 그대로 남는다(`H` 지정 모드·우클릭) — 둘은 *"나른다"*만 말하므로 합집합이고
    // **어느 쪽이 이기는지를 정할 일이 없다**(`DroppedItem.Hauls`).
    //
    // **여럿을 고르면 한 번에 뒤집는다.** 하나라도 꺼져 있으면 전부 켜고, 전부 켜져 있을 때만
    // 끈다 — 섞여 있을 때 "누르면 무엇이 되나"가 애매하면 두 번 눌러 확인하게 된다.
    void ToggleHaulOnSelection()
    {
        bool anyOff = false;
        foreach (var d in _items)
            if (d != null && !HaulPolicy.IsAuto(d.Item)) { anyOff = true; break; }

        foreach (var d in _items)
            if (d != null) HaulPolicy.SetAuto(d.Item, anyOff);

        Refresh();
    }

    void OnDestroy()
    {
        SelectionManager.OnPawnInspected      -= OnPawnInspected;
        SelectionManager.OnBlueprintInspected -= OnBlueprintInspected;
        SelectionManager.OnBuildingInspected  -= OnBuildingInspected;
        SelectionManager.OnCoreInspected      -= OnCoreInspected;
        SelectionManager.OnNodeSelected       -= OnNodeSelected;
        SelectionManager.OnForageInspected    -= OnForageInspected;
        SelectionManager.OnRockInspected      -= OnRockInspected;
        SelectionManager.OnItemsInspected     -= OnItemsInspected;
        BuildModeManager.OnActiveChanged      -= OnBuildModeChanged;
    }

    void OnItemsInspected(IReadOnlyList<DroppedItem> items)
    {
        _forage = null;
        _rock   = null;
        _items.Clear();
        if (items != null)
            foreach (var d in items) if (d != null) _items.Add(d);
        Refresh();
    }

    void OnPawnInspected(UnitBrain brain)
    {
        _inspected = brain;
        _forage = null;
        _rock   = null;
        Refresh();
    }

    void OnBlueprintInspected(BuildingBlueprint bp)
    {
        _blueprint = bp;
        _forage = null;
        _rock   = null;
        Refresh();
    }

    void OnBuildingInspected(PlacedBuilding pb)
    {
        _building = pb;
        _forage = null;
        _rock   = null;
        Refresh();
    }

    void OnCoreInspected(DungeonCore core)
    {
        _core = core;
        Refresh();
    }

    // **채집물은 자원 노드가 아니다** — 한 번 따면 없어지므로 게이지도 배정도 없다.
    // 그래서 창에 적을 것도 다르다: *무엇이 얼마나 나오나 · 찍혔나 · 그 품목이 자동운반인가*.
    // **광맥 창과 같은 눈높이로 적는다**(2026-09-06) — 처음엔 세 줄짜리 맨 글이었는데,
    // 같은 화면의 광맥은 이름을 색으로 세우고 갈래를 달고 무엇이 얼마나 나오는지를 적는다.
    // **창마다 격이 다르면 어떤 물건은 덜 중요한 것처럼 보인다.**
    void ShowForage(Forageable f)
    {
        Show();
        if (infoText == null) return;

        var item = f.Item;
        var sb   = new StringBuilder();

        sb.AppendLine($"<b><color=#E8C566>{(item != null ? item.ItemName : "채집물")}</color></b>  " +
                      "<color=#888>[채집물]</color>");
        sb.AppendLine();

        if (item != null)
        {
            sb.AppendLine($"따면  <b>{item.ItemName}</b>  " +
                          $"<color=#888>한 번에 {f.MinAmount}~{f.MaxAmount}개</color>");

            // **한 번이면 끝난다는 것이 이 물건의 성격이다** — 광맥과 갈리는 자리라 적는다.
            sb.AppendLine("<color=#888>한 번 따면 없어집니다  ·  기술이 필요 없습니다</color>");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(item.Description))
            {
                sb.AppendLine($"<color=#9a9a9a>{item.Description}</color>");
                sb.AppendLine();
            }
        }

        // **찍혔나와 자동운반은 다른 축이다.** 앞은 *이걸 딸까*(이 한 개), 뒤는 *딴 것을
        // 나를까*(그 품목 전부). 둘을 한 줄로 적으면 **왜 안 실려 가는지를 못 가린다.**
        sb.AppendLine(f.Designated
            ? "<color=#7fe08a>채집 지정됨</color>  <color=#888>— 폰이 와서 땁니다</color>"
            : "<color=#F90>지정 안 됨</color>  <color=#888>— 아래 [채집한다] 또는 H로 찍습니다</color>");

        if (item != null)
            sb.AppendLine(HaulPolicy.IsAuto(item)
                ? "<color=#7fe08a>자동운반 켜짐</color>  <color=#888>— 딴 것은 저절로 실려 갑니다</color>"
                : "<color=#c9c9c9>자동운반 꺼짐</color>  <color=#888>— 딴 것은 바닥에 남습니다</color>");

        infoText.text = sb.ToString();

        // **버튼은 이 창에서 할 수 있는 일만 둔다** — 채집물에는 *이동한다·분해한다*가 없다
        // (건물이 아니다). 대신 **찍는 일**과 **그 품목의 자동운반**이 여기 있어야 한다:
        // 지정 모드를 켜러 가지 않고도 눈앞의 것 하나를 시킬 수 있어야 한다.
        var acts = new List<(string, Action)>
        {
            (f.Designated ? "채집 취소" : "채집한다",
             () => { f.SetDesignated(!f.Designated); Refresh(); }),
        };
        if (item != null) acts.Add(HaulSwitch(item));

        SetActions(acts);
    }

    // **자동운반 스위치는 동작 줄의 버튼 하나다**(2026-09-06에 옮겼다).
    //
    // 그전에는 창 **오른쪽 위**의 작은 버튼이었는데 사용자가 *"자동운반 토글은 어디있는지
    // 모르겠고"*라고 했다 — **찾을 수 없는 스위치는 없는 스위치다.** 그리고 그 자리는
    // 바닥 물건에만 떴으므로 채집물·광맥에는 애초에 없었다.
    //
    // **켜는 것은 이 한 개가 아니라 그 품목이다**(`HaulPolicy`) — 그래서 바닥 물건이든
    // 채집물이든 광맥이든 **같은 버튼이 같은 일을 한다.**
    (string, Action) HaulSwitch(ItemData item)
        => (HaulPolicy.Label(item), () => { HaulPolicy.Toggle(item); Refresh(); });

    // **벽 한 칸.** 광맥 창과 같은 눈높이로 적되 **묻는 것이 다르다**: 노드는 *누가 붙었나*가
    // 중심이고 벽은 *파면 무엇이 나오나 · 찍었나*가 중심이다(벽에는 배정이 없다 — 찍으면
    // 큐에 올라가고 아무나 온다).
    void ShowRock(Vector2Int tile)
    {
        Show();
        if (infoText == null) return;

        // **기반암은 파면 무엇이 나오나를 안 적는다** — 못 판다. 캐는 버튼도 안 단다
        // (눌러도 `Mark`가 거절해 아무 일도 안 일어나는 버튼이 된다).
        if (RockMap.IsBedrock(tile))
        {
            infoText.text =
                "<b><color=#E8C566>기반암</color></b>  <color=#888>[세계의 끝]</color>\n" +
                $"<color=#888>({tile.x}, {tile.y})</color>\n\n" +
                "캘 수 없습니다. 이 너머로는 세계가 없습니다.";
            SetActions(new List<(string, Action)>());
            return;
        }

        var vein = RockMap.VeinAt(tile);
        var data = vein != null ? vein : RockMap.PlainRock;
        var item = data != null ? data.DropItem : null;
        bool marked = RockMap.IsMarked(tile);

        var sb = new StringBuilder();
        sb.AppendLine(vein != null
            ? $"<b><color=#E8C566>{(item != null ? item.ItemName : "광맥")} 광맥</color></b>  <color=#888>[벽]</color>"
            : "<b><color=#E8C566>바위</color></b>  <color=#888>[벽]</color>");
        sb.AppendLine($"<color=#888>({tile.x}, {tile.y})</color>");
        sb.AppendLine();

        if (item != null)
            sb.AppendLine($"파면  <b>{item.ItemName}</b>  " +
                          $"<color=#888>한 번에 {data.DropMin}~{data.DropMax}개</color>");
        else
            sb.AppendLine("<color=#F90>파도 나오는 것이 없습니다 — " +
                          "`World → 0. Create Rock Materials`가 맨바위 산출을 물립니다.</color>");
        sb.AppendLine();

        sb.AppendLine(marked
            ? "<color=#7fe08a>채굴 지정됨</color>  <color=#888>— 채굴을 켜 둔 폰이 옵니다(P)</color>"
            : "<color=#c9c9c9>지정 안 됨</color>  <color=#888>— 아래 [채굴한다] 또는 K로 찍습니다</color>");

        if (item != null)
            sb.AppendLine(HaulPolicy.IsAuto(item)
                ? "<color=#7fe08a>자동운반 켜짐</color>  <color=#888>— 캔 것은 저절로 실려 갑니다</color>"
                : "<color=#c9c9c9>자동운반 꺼짐</color>  <color=#888>— 캔 것은 바닥에 남습니다</color>");

        infoText.text = sb.ToString();

        var acts = new List<(string, Action)>
        {
            (marked ? "채굴 취소" : "채굴한다",
             () => { RockMap.Mark(tile, !marked); Refresh(); }),
        };
        if (item != null) acts.Add(HaulSwitch(item));

        SetActions(acts);
    }

    // **힘을 받고 있나.** 없는 건물에는 한 줄도 안 적는다 — 이 축과 무관한 것에 전력 줄이
    // 뜨면 *"이 침대도 전기를 먹나"*가 된다.
    //
    // **모자란 것과 끈 것을 가른다** — 앞은 핵을 키워야 하고 뒤는 누르면 그만이다.
    static void AppendPower(StringBuilder sb, Component c)
    {
        if (!PowerUser.Has(c, out var u)) return;

        if (!u.SwitchedOn)
            sb.AppendLine($"<color=#888>전력 꺼 둠 ({u.Cost})</color>");
        else if (u.Powered)
            sb.AppendLine($"<color=#7fe08a>전력 {u.Cost}</color> <color=#888>— 핵이 대고 있습니다.</color>");
        else
            sb.AppendLine($"<color=#F44336>전력 부족 ({u.Cost} 필요)</color> " +
                          "<color=#888>— 부핵을 세우거나 다른 건물을 끄세요.</color>");
    }

    static (string, Action) PowerSwitch(PowerUser u)
        => (u.SwitchedOn ? "전력 끈다" : "전력 켠다", () => u.ToggleSwitch());

    void OnRockInspected(Vector2Int? tile)
    {
        _rock   = tile;
        _forage = null;
        _items.Clear();
        Refresh();
    }

    void OnForageInspected(Forageable f)
    {
        _forage = f;
        _items.Clear();     // 바닥 물건이 창을 먼저 가져간다 — 눌러 본 것이 이겨야 한다
        Refresh();
    }

    void OnNodeSelected(ResourceNode node)
    {
        _node = node;
        _forage = null;
        _rock   = null;
        Refresh();
    }

    void OnBuildModeChanged(bool active)
    {
        _buildMode = active;
        // 조사(적/NPC·청사진·건물 클릭)는 일회성이라 건설에 들어가면 버린다. 선택은 지속
        // 상태라 그대로 두고, 건설을 빠져나오면 Refresh가 알아서 패널을 되살린다.
        if (active)
        {
            _inspected = null; _blueprint = null; _building = null;
            _core = null; _node = null; _forage = null; _rock = null; _items.Clear();
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

    // 지금 올라와 있는 링크. **창마다 제 상태다** — 스탯 창과 같이 떠 있을 수 있다.
    string _hoveredLink;

    void Update()
    {
        bool showing = selectionManager.Selected.Count > 0 || _inspected != null
                    || _blueprint != null || _building != null || _core != null || _node != null;
        if (!showing) return;

        // **올린 칸의 설명** — 갱신 주기와 따로 돈다(마우스는 0.5초를 기다려 주지 않는다).
        PawnStoryText.Tick(infoText, ref _hoveredLink);

        _refreshTimer -= Time.unscaledDeltaTime;   // 멈춰 놓고 들여다볼 때도 갱신돼야 한다
        if (_refreshTimer > 0f) return;
        _refreshTimer = refreshInterval;

        Refresh();
    }

    // **살펴보기 창은 앞으로 안 올린다**(2026-08-29).
    //
    // 08-29에 여는 자리를 `WindowRaiser.Open`으로 바꿨는데, 그건 **켜고 + 맨 앞으로 올리는**
    // 문이다. 이 창은 사람이 여는 창이 아니라 **선택이 바뀔 때마다 저절로 갱신되는 상시
    // HUD**라, 갱신될 때마다 형제 맨 뒤로 가서 **밑에 있는 패널(모드 바 등)을 덮었다.**
    //
    // 뜨는 모양(흐려지며 나타나기)은 그대로 두고 **순서만 안 건드린다** — `UiTween`이
    // 그 둘을 갈라 놓은 덕에 한 줄로 갈린다.
    void Show() => UiTween.PopIn(panel);

    // **폰의 이름 줄 — 한 벌**(상태표 맨 위가 쓴다). 이름 · [유형] (세력). **시체의 소속은 `Corpse`가 든다**
    // (`BecomeCorpse`가 `Faction`을 지운다 — 안 그러면 죽는 순간 소속이 사라진다).
    public static (string name, string sub) PawnHeader(UnitBrain brain)
    {
        if (brain == null) return ("", "");
        var faction = brain.GetComponent<Faction>();
        var corpse  = brain.GetComponent<Corpse>();
        var data    = faction != null ? faction.Data : corpse != null ? corpse.Faction : null;

        string name = !string.IsNullOrEmpty(brain.UnitName) ? brain.UnitName
                    : brain.Data != null ? brain.Data.RaceName : brain.gameObject.name;
        // 성별은 몸이 답한다 — 몸이 없는 폰(늑대·골렘)은 안 적는다.
        var    g    = brain.Gender;
        string sub  = $"[{UnitTypeLabel(brain, faction)}]" + (g.HasValue ? $" {g.Value.DisplayName()}" : "")
                    + (data != null ? $"  <color=#7AA>({data.FactionName})</color>" : "");
        return (name, sub);
    }

    // **지금 이 창이 보여 주는 폰**(2026-09-19) — 상태표(`PawnSheetUI`)가 읽는다. *방금 눌러 본 것 ↔ 고른 폰*을
    // 여기서 가르므로 상태표가 또 가르면 두 창이 다른 폰을 보여 주는 순간이 생긴다. 폰이 아니면 null.
    public static UnitBrain ShownPawn { get; private set; }

    void Refresh()
    {
        ShownPawn = null;
        // 운반 스위치는 **바닥 물건을 골랐을 때만** 뜬다 — 여기서 한 번 내리고
        // `ShowItems`만 다시 올린다(분기마다 끄면 새 분기가 하나 빠뜨린다).
        SetHaulToggle(null);

        // 건설뿐 아니라 **명령·밭 구역에서도** 치운다(`FocusMode`).
        if (_buildMode || FocusMode.Active) { panel?.SetActive(false); return; }

        // **방금 누른 것이 먼저다**(조사와 같은 규칙). 청사진이 다 지어지거나 취소되면,
        // 건물이 분해되면 `== null`이 되어 저절로 폰 쪽으로 돌아간다.
        if (_blueprint != null) { ShowBlueprint(_blueprint); return; }
        // 주워 가면 파괴되므로 매번 걷어낸다 — 남겨 두면 빈 창이 뜬다.
        for (int i = _items.Count - 1; i >= 0; i--)
            if (_items[i] == null) _items.RemoveAt(i);
        if (_items.Count > 0) { ShowItems();                 return; }
        if (_rock.HasValue && RockMap.IsRock(_rock.Value)) { ShowRock(_rock.Value); return; }
        if (_forage    != null) { ShowForage(_forage);       return; }
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
        ShownPawn = brain;
        Show();

        SetPawnActions(brain);

        var sb    = new StringBuilder();

        var faction = brain.GetComponent<Faction>();
        var corpse  = brain.GetComponent<Corpse>();

        // **시체에는 `Faction` 컴포넌트가 없다** — `BecomeCorpse`가 자동 탐지를 막으려고 지운다.
        // 소속은 `Corpse`가 받아 뒀으므로 거기서 읽는다(안 그러면 죽는 순간 소속이 사라진다).
        var factionData = faction != null ? faction.Data : corpse != null ? corpse.Faction : null;

        // **이름 줄은 상태표 맨 위로 옮겼다**(`PawnSheetUI` ← `PawnHeader`, 사용자: *"이름이랑 순서가 먼저 보여야해"*).

        // **얼마나 삭았나** — 도축해서 무엇이 나올지가 이것으로 갈리고(`Butchering`),
        // 나중에 붙일 "교단이 일으킨다"의 재료도 이 단계다. 산 폰에는 안 뜬다.
        if (corpse != null)
        {
            var stage = corpse.Stage;
            string tint = stage == CorpseStage.Fresh  ? "#9C9"
                        : stage == CorpseStage.Rotten ? "#AA8" : "#CCC";
            sb.AppendLine($"<color=#888>부패</color>  <color={tint}>{Butchering.StageName(stage)}</color>");
        }

        // **그 세력이 나를 어떻게 보는가.** 관계가 움직이는데 볼 자리가 없으면 "왜 갑자기
        // 덤비지"에 답이 없다 — 우호도가 0(저작한 그대로)이면 적지 않는다(늘 떠 있으면 안 읽는다).
        if (factionData != null && !factionData.PlayerControlled
            && !Mathf.Approximately(FactionRelations.Of(factionData), 0f))
            sb.AppendLine($"<color=#9AB>{FactionRelations.Describe(factionData)}</color>");

        sb.AppendLine();

        // **혈액 · 욕구 · 부위 · 상태 이상은 상태표로 옮겼다**(`PawnSheetUI`, 2026-09-19 — 켄시의 그 창). 같은 값을
        // 두 창이 그리면 하나를 고칠 때 다른 하나가 뒤처진다. 여기는 **누구이고 무엇을 지녔나**를 답한다.

        // **업고 있는 것과 갇힌 것은 화면에 나와야 한다.** 둘 다 그 폰이 왜 느린지·왜
        // 아무것도 안 하는지의 답인데, 모델만 봐서는 알기 어렵다.
        AppendCarryState(sb, brain);

        // **배경·특성·장비는 상태표의 줄로 옮겼다**(`PawnSheetUI` — 능력치와 같은 색 규칙, 사용자 결정).
        // 여기 남는 것은 **그때그때 생기는 사정**뿐이다(부패 · 우호도 · 업기/갇힘).

        if (infoText != null) infoText.text = Josa.Fix(sb.ToString());
    }

    // ── 청사진 ────────────────────────────────────────────────────────────
    //
    // 여기서 답하는 것은 **"왜 아직 안 지어졌나"** 하나다. 자재가 모자란 것과 폰이 안 오는
    // 것은 할 일이 다르다 — 앞은 캐 오거나 사 오는 일이고, 뒤는 작업 설정을 켜는 일이다.
    void ShowBlueprint(BuildingBlueprint bp)
    {
        Show();
        if (infoText == null) return;

        var sb = new StringBuilder();
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

        if (bp.CostRequired > 0)
        {
            // **재료마다 한 줄이다**(2026-09-17) — 여기가 답하는 물음이 *"왜 아직 안
            // 지어졌나"*인데, 합쳐 적으면 **무엇을 더 캐 와야 하는지**가 그 답에서 빠진다.
            foreach (var (material, got, need) in bp.Progress)
            {
                if (material == null) continue;

                sb.AppendLine($"{BarColored(got, need, "#6C9", "#353")}  " +
                              $"{material.ItemName} {got}/{need}");

                int left = need - got;
                if (left <= 0) continue;

                // **창고만 세면 손에 든 것이 없는 것이 된다** — 등록 판정과 같은 축으로 본다.
                int stock = MaterialPool.AvailableHere(material);
                if (stock < left)
                    sb.AppendLine($"<color=#F90>  {material.ItemName}이(가) 모자랍니다 — "
                                + $"가진 것 {stock} / 남은 것 {left}</color>");
            }
        }
        else
        {
            sb.AppendLine("<color=#888>자재가 필요 없습니다.</color>");
        }

        infoText.text = Josa.Fix(sb.ToString());

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
        Show();
        if (infoText == null) return;

        var data = pb.Data;
        string name = data != null ? data.BuildingName : pb.name;

        // **등급이 있으면 이름에 붙는다** — `연구대 II`. 이름을 다섯 벌 저작하지 않는
        // 이유이자(`BenchTier.NameOf`), 화면에서 등급을 읽는 **유일한 자리**다.
        var bench = pb.GetComponent<IUpgradableBench>();
        if (bench != null) name = BenchTier.NameOf(name, bench.Tier);

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

        // **조작은 여기 없다.** 이 창은 *고른 것을 보는 자리*고, 그 건물로 무엇을 하는지는
        // 저마다의 창이 답한다(`GraveUI`·`PyreUI`·`ButcherTableUI`). 침대에 창이 없는 것은
        // 배정이 곧 "누워라"라 조작이랄 게 없어서지, 살펴보기 창이 조작을 겸해서가 아니다.
        var grave = pb.GetComponent<Grave>();
        if (grave != null) AppendGraveState(sb, grave);

        var pyre = pb.GetComponent<Pyre>();
        if (pyre != null) AppendPyreState(sb, pyre);

        var butcher = pb.GetComponent<ButcherTable>();
        if (butcher != null) AppendButcherState(sb, butcher);

        var fire = pb.GetComponent<Campfire>();
        if (fire != null) AppendCampfireState(sb, fire);

        // **휴면은 여기서 말해 줘야 한다** — 그 건물의 패널은 아예 안 열리므로, 안 열리는
        // 이유를 들을 데가 없었다.
        var dorm = pb.GetComponent<BuildingDormancy>();
        if (dorm != null && dorm.IsDormant)
            sb.AppendLine("<color=#F90>핵 영역 밖이라 작동을 멈췄습니다.</color>");

        if (bench != null) AppendBenchTier(sb, pb, bench);

        if (data != null && !data.IsFree)
        {
            var parts = new List<string>();
            foreach (var c in data.Costs)
                if (c.Item != null && c.Amount > 0) parts.Add($"{c.Item.ItemName} {c.Amount}");
            if (parts.Count > 0)
                sb.AppendLine($"<color=#888>분해하면 {string.Join(" · ", parts)}을(를) 전부 돌려받습니다.</color>");
        }

        infoText.text = Josa.Fix(sb.ToString());

        var actions = new List<(string, Action)>();
        AddBenchActions(actions, pb, bench);
        actions.Add(("이동한다", (Action)(() => BuildingRelocator.Begin(pb))));
        actions.Add(("분해한다", pb.Demolish));
        SetActions(actions);
    }

    // ── 작업대 등급 ──────────────────────────────────────────────────────────
    //
    // 여기서 답하는 것은 **"올릴 수 있나, 올리면 뭐가 좋나, 지금 어디까지 왔나"** 셋이다.
    // 올리는 일은 건설과 같은 길이라(폰이 자재를 지고 온다) 진행이 보여야 하고, 안 보이면
    // *"눌렀는데 아무 일도 안 일어난다"*가 된다.
    static void AppendBenchTier(StringBuilder sb, PlacedBuilding pb, IUpgradableBench bench)
    {
        sb.AppendLine();
        sb.AppendLine($"<color=#888>등급</color> <b>{BenchTier.Roman(bench.Tier)}</b>"
                    + $"  <color=#888>작업 속도 ×{BenchTier.SpeedOf(bench.Tier):0.##}</color>");

        var up = pb.GetComponent<BenchUpgrade>();

        if (up != null && up.InProgress)
        {
            sb.AppendLine($"<color=#6C9>{BenchTier.Roman(up.Target)} 등급으로 올리는 중</color>");
            foreach (var (item, got, need) in up.Progress)
            {
                if (item == null) continue;
                sb.AppendLine($"{BarColored(got, need, "#6C9", "#353")}  {item.ItemName} {got}/{need}");

                int left = need - got;
                if (left <= 0) continue;
                int stock = MaterialPool.AvailableHere(item);
                if (stock < left)
                    sb.AppendLine($"<color=#F90>  {item.ItemName}이(가) 모자랍니다 — "
                                + $"가진 것 {stock} / 남은 것 {left}</color>");
            }
            return;
        }

        if (bench.Tier >= BenchTier.Max)
        {
            sb.AppendLine("<color=#888>가장 높은 등급입니다.</color>");
            return;
        }

        // **다음 등급에 무엇이 드는지 미리 적는다** — 누르기 전에 알아야 *지금 올릴지*를
        // 정한다(누르고 나서 알면 취소하는 수밖에 없다).
        var cost = BenchUpgrade.CostFor(bench.Tier + 1);
        if (cost.Count == 0) return;

        var parts = new List<string>();
        foreach (var c in cost)
            if (c.Item != null && c.Amount > 0) parts.Add($"{c.Item.ItemName} {c.Amount}");
        sb.AppendLine($"<color=#888>{BenchTier.Roman(bench.Tier + 1)}로 올리려면 "
                    + $"{string.Join(" · ", parts)}</color>");
    }

    static void AddBenchActions(List<(string, Action)> actions, PlacedBuilding pb, IUpgradableBench bench)
    {
        if (bench == null) return;

        var up = pb.GetComponent<BenchUpgrade>();

        if (up != null && up.InProgress)
        {
            // **취소는 전액 환불이다** — 아직 아무것도 안 바뀌었으므로 깎을 손실이 없다.
            actions.Add(($"{BenchTier.Roman(up.Target)} 등급 올리기를 그만둔다", up.Cancel));
            return;
        }

        if (bench.Tier >= BenchTier.Max) return;

        int next = bench.Tier + 1;
        actions.Add(($"{BenchTier.Roman(next)} 등급으로 올린다",
                     (Action)(() => BenchUpgrade.Ensure(pb)?.Request())));
    }

    // ── 무덤 ─────────────────────────────────────────────────────────────────
    //
    // 답해야 하는 것은 둘이다: **누가 묻혀 있나**, 그리고 **꺼낼 수 있나**.
    static void AppendGraveState(StringBuilder sb, Grave grave)
    {
        var buried = grave.Buried;
        if (buried == null)
        {
            sb.AppendLine("<color=#888>비어 있다. 시체를 업어 와 묻을 수 있다.</color>");
            return;
        }

        sb.AppendLine($"{buried.UnitName}  <color=#888>묻혀 있다</color>");

        // **묻히면 시간이 멈춘다**(`Corpse.SetBuried`) — 그 사실이 안 보이면 무덤이
        // 그냥 감추는 자리로 읽힌다.
        sb.AppendLine($"<color=#888>{Butchering.StageName(buried.Stage)}인 채로 멈춰 있다 " +
                      $"({buried.AgeDays:F1}일).</color>");

    }

    // ── 화장터 ───────────────────────────────────────────────────────────────
    static void AppendPyreState(StringBuilder sb, Pyre pyre)
    {
        var who = pyre.Burning;
        if (who == null)
        {
            sb.AppendLine("<color=#888>비어 있다. 시체를 업어 와 태울 수 있다.</color>");
            return;
        }

        sb.AppendLine($"{who.UnitName}  <color=#888>태우는 중</color>");
        sb.AppendLine($"진행  {BarColored(pyre.Progress, 1f, "#E07E3E", "#5A3A20")}  " +
                      $"{pyre.Progress * 100f:F0}%");

        // **왜 안 타는지**는 화면에 있어야 한다 — 연료가 없으면 게이지가 멈춰 있을 뿐이라
        // 이유가 없으면 고장으로 보인다.
        if (pyre.BlockedReason != null)
            sb.AppendLine($"<color=#F90>{pyre.BlockedReason}</color>");

    }

    // ── 화톳불 ───────────────────────────────────────────────────────────────
    //
    // 답해야 하는 것은 둘뿐이다: **지금 타고 있나**, 그리고 **안 타면 그게 시간 탓인가 내가
    // 꺼 둔 탓인가.** 태우는 것이 없으니 게이지도 막힌 사유도 없다.
    static void AppendCampfireState(StringBuilder sb, Campfire fire)
    {
        if (!fire.WantsFire)
        {
            sb.AppendLine("<color=#888>꺼 두었다. 우클릭해서 지필 수 있다.</color>");
            return;
        }

        sb.AppendLine(fire.IsLit
            ? "<color=#FF8C26>타는 중 — 이 둘레는 밤에도 낮처럼 보인다.</color>"
            : "<color=#888>밝을 때는 꺼져 있다. 저녁이 되면 저절로 붙는다.</color>");
    }

    // ── 도축대 ───────────────────────────────────────────────────────────────
    //
    // 규칙이 지금 어떻게 걸려 있는지만 적는다 — **손보는 것은 `ButcherTableUI`**다.
    static void AppendButcherState(StringBuilder sb, ButcherTable table)
    {
        var bill = table.Bill;

        sb.AppendLine(bill.Suspended
            ? "<color=#F90>멈춰 있다.</color>"
            : table.Cutting ? "<color=#4CD95A>손질하는 중.</color>"
                            : "<color=#888>시체를 기다린다.</color>");

        sb.AppendLine($"대상  {bill.TargetName}");
        sb.AppendLine($"상태  {bill.StageName}");
        sb.AppendLine($"반복  {bill.RepeatName}");

        int piled = table.Inventory != null ? table.Inventory.Items.Count : 0;
        sb.AppendLine(piled > 0
            ? $"<color=#888>쌓인 것 {piled}뭉치 — {(table.AllowHaulOut ? "창고로 나른다" : "여기 둔다")}</color>"
            : $"<color=#888>쌓인 것 없음 — {(table.AllowHaulOut ? "창고로 나른다" : "여기 둔다")}</color>");

    }

    // ── 자원 노드 ─────────────────────────────────────────────────────────
    //
    // **설명을 손으로 안 적는다.** 노드에 대해 알고 싶은 것(무엇이 나오나 · 캐는가 베는가 ·
    // 몇 명이 붙나 · 얼마나 어려운가)은 전부 `ResourceNodeData`에서 나온다 — 적어 두면
    // 값을 고칠 때마다 문장이 뒤처지고, 그러면 표가 아니라 거짓말이 된다.
    //
    // 씬에 흩어진 나무·바위는 `BuildingData`가 없어(`ResourceNodeData`만 있다) 이름을 물을
    // 데가 없다 — 그때는 나오는 물건에서 만든다.
    // ── 바닥에 떨어진 물건 ────────────────────────────────────────────────────
    //
    // **답해야 하는 것은 "이게 실려 가나"다.** 초록 구슬이 안 보일 때 그것이 "안 찍었다"인지
    // "찍었는데 창고가 없다"인지 구별이 안 됐다 — 둘은 할 일이 다르다(찍기 / 창고 짓기).
    //
    // 무엇이·얼마나·얼마어치인지도 여기서 답한다. 툴팁(`ItemInfoUI`)은 커서를 얹어야 뜨고
    // 격자 위에서만 도므로, 바닥의 물건에는 볼 자리가 아예 없었다.
    void ShowItems()
    {
        Show();
        // **스위치를 동작 줄로 내렸다**(2026-09-06) — 오른쪽 위의 작은 버튼은 사용자가
        // 못 찾았다(*"자동운반 토글은 어디있는지 모르겠고"*). **찾을 수 없는 스위치는 없는
        // 스위치다.** 이제 채집물·광맥과 **같은 자리에 같은 버튼**이 선다.
        //
        // 여럿을 골랐으면 **품목마다 하나씩** — 같은 품목이 여럿이면 한 번만 세운다.
        var seen = new List<ItemData>();
        var acts = new List<(string, Action)>();
        foreach (var it in _items)
        {
            if (it?.Item == null || seen.Contains(it.Item)) continue;
            seen.Add(it.Item);
            acts.Add(HaulSwitch(it.Item));
        }
        SetActions(acts.Count > 0 ? acts : null);
        if (infoText == null) return;

        var sb = new StringBuilder();

        string head = _items.Count == 1
                    ? OneLineName(_items[0])
                    : $"<b>바닥의 물건</b>  <color=#888>{_items.Count}종</color>";

        sb.AppendLine(head);

        if (_items.Count == 1)
        {
            var only = _items[0];
            sb.AppendLine($"<color=#888>{only.Quality.DisplayName()} · {only.Item.TypeName}</color>");
        }
        sb.AppendLine();

        // ── 합계 ────────────────────────────────────────────────────────────
        int   count  = 0;
        float weight = 0f;
        int   value  = 0;
        foreach (var d in _items)
        {
            count  += d.Amount;
            weight += d.Item.Weight    * d.Amount;
            value  += d.Item.BaseValue * d.Amount;
        }

        sb.AppendLine($"수량  {count}개");
        if (weight > 0f) sb.AppendLine($"무게  {weight:F1}kg");
        if (value  > 0)  sb.AppendLine($"가치  {value}G");
        sb.AppendLine();

        // **왜 안 실려 가는지까지 적는다.** 찍어 놨는데 받아 줄 창고가 없으면 아무 일도
        // 안 일어나는데, 그건 찍기를 잊은 것과 화면에서 똑같이 보인다.
        //
        // **`Hauls`로 센다 — `HaulAllowed`가 아니다**(2026-09-06). 품목 규칙으로 켜진 것은
        // 개별로 안 찍혀 있으므로, 옛 기준으로 세면 **자동운반이 켜진 물건을 "지정 안 됨"**
        // 이라고 적는다(스위치를 켜 놓고 창은 아니라고 말하는 꼴이다).
        int on = 0;
        foreach (var d in _items) if (d.Hauls) on++;
        bool allOn = on == _items.Count;

        if (on == 0)
        {
            sb.AppendLine("<color=#888>자동운반 꺼짐 · 지정 안 됨 — 아무도 건드리지 않습니다.</color>");
        }
        else
        {
            int stranded = 0;
            foreach (var d in _items)
                if (d.Hauls &&
                    Storage.FindNearest(d.transform.position, d.Item, d.Amount, d.Quality) == null)
                    stranded++;

            string scope = allOn ? "" : $" ({on}/{_items.Count})";
            if (stranded > 0)
            {
                sb.AppendLine($"<color=#F44336>운반 지정됨{scope} — 그중 {stranded}종은 받아 줄 창고가 없습니다.</color>");
                sb.AppendLine("<color=#888>창고를 짓거나 비우면 저절로 실려 갑니다.</color>");
            }
            else
                sb.AppendLine($"<color=#8BC34A>운반 지정됨{scope} — 폰이 창고로 옮깁니다.</color>");
        }

        infoText.text = Josa.Fix(sb.ToString());
    }

    // 이름줄은 **이름만**, 아랫줄이 `등급 · 종류` — `ItemInfoUI`와 같은 규약이다
    // (창마다 다른 모양으로 적으면 같은 물건인지 알기 어렵다).
    static string OneLineName(DroppedItem item)
        => $"<b><color=#{ColorUtility.ToHtmlStringRGB(item.Quality.Color())}>" +
           $"{item.Item.ItemName}</color></b>  <color=#888>×{item.Amount}</color>";

    void ShowNode(ResourceNode node)
    {
        Show();
        if (infoText == null) return;

        var d  = node.Data;
        var pb = node.GetComponentInParent<PlacedBuilding>();

        // **저작한 이름이 먼저다** — 나오는 것이 정체가 아닌 물건이 있다(거대버섯은 목재를
        // 내놓지만 *"목재 나무"*가 아니다). 비어 있으면 예전처럼 파생시킨다.
        string name = pb != null && pb.Data != null ? pb.Data.BuildingName
                    : d != null && !string.IsNullOrEmpty(d.DisplayName) ? d.DisplayName
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

            if (!string.IsNullOrEmpty(d.Description))
            {
                sb.AppendLine($"<color=#9a9a9a>{d.Description}</color>");
                sb.AppendLine();
            }
        }

        // **"왜 안 캐지"의 답이 화면에 있어야 한다.** 이제 답이 자리에 따라 갈린다:
        // 핵 영역 안이면 할 일 없는 폰이 저 혼자 붙지만(`ResourceNode.ClaimFreeFor`),
        // **밖이면 여전히 손으로만 붙인다** — 안 그러면 폰이 맵 전체의 나무를 베러 떠난다.
        // 안 갈라 적으면 영역 밖 광맥 앞에서 오지 않는 폰을 기다리게 된다.
        if (node.CanDesignate)
        {
            // **야생에는 배정이 없다** — *"배정된 폰이 없습니다"*는 여기서 거짓말이다.
            sb.AppendLine(node.Designated
                ? $"<color=#7fe08a>지정됨</color>  <color=#888>— {(tree ? "벌목" : "채굴")}을 켜 둔 폰이 옵니다(P)</color>"
                : $"<color=#F90>지정 안 됨</color>  <color=#888>— 아래 버튼 또는 H로 찍습니다</color>");
        }
        else if (node.AssignedCount == 0)
        {
            var core = DungeonCore.Instance;
            bool inTerritory = core == null || core.IsWithinAnyRangeWorld(node.transform.position);

            sb.AppendLine(inTerritory
                ? "<color=#F90>배정된 폰이 없습니다. 할 일이 없는 폰이 곧 붙습니다 " +
                  $"— {(tree ? "벌목" : "채굴")}을 켜 둔 폰이 있어야 합니다(P).</color>"
                : "<color=#F90>핵 영역 밖이라 저절로 붙지 않습니다. 폰을 고르고 우클릭해 시키세요.</color>");
        }
        else
        {
            sb.AppendLine($"진행  {BarColored(node.GaugeProgress, 1f, "#6C9", "#353")}");

            // **손으로 붙인 것과 저 혼자 간 것을 가른다** — 안 가르면 *"내가 붙였던가"*를
            // 알 수 없고, 그러면 자율 배정을 끄는 스위치가 어디에 걸리는지도 모른다
            // (침대의 `AutoClaimed`가 같은 물음에 답한 자리).
            int auto = 0;
            foreach (var p in node.AssignedPawns)
                if (p != null && p.Blackboard != null && p.Blackboard.NodeAutoClaimed) auto++;

            if (auto > 0)
                sb.AppendLine($"<color=#888>{auto}명은 저 혼자 붙었습니다 — 작업을 끄면 놓습니다.</color>");
        }

        // **유한한 것은 얼마나 남았는지 보여야 한다** — 안 보이면 무한한 것과 화면에서
        // 구별되지 않고, 그러면 *"왜 갑자기 사라졌지"*가 된다.
        if (node.YieldsLeft >= 0)
            sb.AppendLine($"<color=#F90>{node.YieldsLeft}번 더</color> " +
                          $"<color=#888>캐면 없어집니다 — 계속 쓰려면 기지에 심습니다.</color>");
        else if (node.IsBuilt)
            sb.AppendLine("<color=#888>기지에 심은 것이라 마르지 않습니다.</color>");

        int stock = node.Inventory != null && d != null && d.DropItem != null
                  ? node.Inventory.GetTotalQuantity(d.DropItem) : 0;
        if (stock > 0)
            sb.AppendLine($"<color=#888>쌓인 것 {stock}개 — 폰이 창고로 나릅니다.</color>");

        infoText.text = Josa.Fix(sb.ToString());

        // 씬에 흩어진 나무·바위는 `PlacedBuilding`이 없다 — 옮기거나 분해할 수 없다.
        // **그래도 버튼이 하나는 있다**: 여기서 나오는 것을 자동으로 나를지(2026-09-06,
        // 사용자: *"이제 보니깐 벽에도 그런게 없네"*). 캔 것이 바닥에 쌓이는데 그 스위치가
        // 이 창에 없으면, **캔 자리에서는 정할 수가 없다.**
        AppendPower(sb, node);

        var acts = new List<(string, Action)>();
        if (pb != null)
        {
            acts.Add(("이동한다", (Action)(() => BuildingRelocator.Begin(pb))));
            acts.Add(("분해한다", pb.Demolish));
        }

        // **야생은 찍어서 가져온다 — 붙이는 것이 아니다**(2026-09-06). 배정 버튼이 아니라
        // 지정 버튼이 여기 있어야 그 둘이 화면에서 갈린다.
        if (node.CanDesignate)
            acts.Add((node.Designated ? "지정 취소" : (tree ? "벤다" : "캔다"),
                      () => { node.SetDesignated(!node.Designated); Refresh(); }));

        if (PowerUser.Has(node, out var pu)) acts.Add(PowerSwitch(pu));
        if (d != null && d.DropItem != null) acts.Add(HaulSwitch(d.DropItem));

        SetActions(acts.Count > 0 ? acts : null);
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
        Show();
        if (infoText == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("<b><color=#E8C566>던전 핵</color></b>  <color=#888>[중심]</color>");
        sb.AppendLine();
        sb.AppendLine("이 거점의 중심. 하수인을 소환하고, 둘레에 영역을 편다 — 영역 밖 건물은 작동을 멈춘다.");
        sb.AppendLine();
        sb.AppendLine($"<color=#888>하수인 {core.CurrentMinionCount}/{core.TotalMinionCap}</color>");
        if (core.Data != null)
            sb.AppendLine($"<color=#888>영역 반경 {core.Data.BaseRadius:F0}m</color>");

        AppendCorePower(sb, core);

        infoText.text = Josa.Fix(sb.ToString());

        // 소환·목록은 핵 제 패널(`DungeonCoreUI`)이 다룬다 — 여기는 **핵 자체**를 다루는 자리다.
        SetActions(new List<(string, Action)>
        {
            ("이동한다", (Action)(() => CoreRelocator.Begin(core))),
        });
    }

    // **핵이 내는 힘과 그것을 나눠 쓰는 것들.**
    //
    // 총량만 적으면 *"모자란다"*까지는 알아도 **무엇을 끄면 되는지**를 모른다. 그래서 먹는
    // 것을 줄줄이 적고 **못 받고 있는 것에 표를 단다** — 배분이 *먼저 지은 순서*라는 규칙이
    // 목록의 차례로 그대로 보인다(그 규칙을 글로 설명할 필요가 없어진다).
    //
    // **끈 것은 따로 적는다** — 끄는 것이 힘을 돌려주는 유일한 길이라, 껐다는 사실이 안
    // 보이면 *"왜 남는 힘이 안 쓰이지"*가 된다.
    static void AppendCorePower(StringBuilder sb, DungeonCore core)
    {
        int cap  = PowerGrid.CapacityOf(core);
        int used = PowerGrid.UsedOf(core);
        int want = PowerGrid.DemandOf(core);

        sb.AppendLine();
        sb.AppendLine(want > cap
            ? $"전력  <color=#F44336>{used}/{cap}</color>  <color=#888>— {want - cap}만큼 모자랍니다.</color>"
            : $"전력  <color=#7fe08a>{used}/{cap}</color>  <color=#888>— {cap - used} 남았습니다.</color>");

        // **부핵이 얼마를 보태는지 적는다** — 늘리는 길이 그것뿐이라, 그 값이 안 보이면
        // *"부핵을 하나 더 세울까"*를 재 볼 근거가 없다.
        int subs = 0, subPower = 0;
        foreach (var sc in SubCore.All)
            if (sc != null && Territory.Same(sc.transform.position, core))
            { subs++; subPower += sc.EnergyOutput; }

        if (subs > 0)
            sb.AppendLine($"<color=#888>핵 {core.EnergyOutput} + 부핵 {subs}개 {subPower}</color>");

        int listed = 0;
        foreach (var u in PowerUsersIn(core))
        {
            if (listed++ >= 8) { sb.AppendLine("<color=#888>  …</color>"); break; }

            string name = NameOfUser(u);
            sb.AppendLine(!u.SwitchedOn
                ? $"<color=#666>  · {name} {u.Cost} — 꺼 둠</color>"
                : u.Powered
                    ? $"<color=#9a9a9a>  · {name} {u.Cost}</color>"
                    : $"<color=#F44336>  · {name} {u.Cost} — 멈춤</color>");
        }

        if (listed == 0) sb.AppendLine("<color=#888>  힘을 쓰는 건물이 없습니다.</color>");
    }

    static System.Collections.Generic.List<PowerUser> PowerUsersIn(DungeonCore core)
    {
        var list = new System.Collections.Generic.List<PowerUser>();
        foreach (var u in UnityEngine.Object.FindObjectsByType<PowerUser>(FindObjectsSortMode.None))
            if (u != null && Territory.CoreAt(u.transform.position) == core) list.Add(u);
        return list;
    }

    // 건물 이름이 있으면 그것, 없으면 노드가 내놓는 것. **오브젝트 이름은 마지막이다** —
    // `Node_IronVein(Clone)`은 사람이 읽으라고 만든 글자가 아니다.
    static string NameOfUser(PowerUser u)
    {
        var pb = u.GetComponentInParent<PlacedBuilding>();
        if (pb != null && pb.Data != null && !string.IsNullOrEmpty(pb.Data.BuildingName))
            return pb.Data.BuildingName;

        var node = u.GetComponent<ResourceNode>();
        if (node != null && node.Data != null && node.Data.DropItem != null)
            return node.Data.DropItem.ItemName;

        return u.name;
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
            AppendPrisonFood(sb, brain);
            AppendEscapeRisk(sb, brain);
            AppendConversion(sb, brain);
        }

        if (bb.LeavingMap)
            sb.AppendLine("<color=#8BC34A>풀려나 떠나는 중이다. 건드리지 않으면 그냥 간다.</color>");
    }

    // 몸값은 **여기 안 적는다.** 액수도 안 되는 이유도 우클릭 메뉴가 든다 —
    // 넘길지 말지는 그 줄을 누를지 말지의 문제라 **고르는 자리에 붙어 있어야** 하고,
    // 이 창에 적으면 창을 두 개 띄워 놓고 눈을 옮겨야 한다.

    // **왜 굶고 있는지 적는다.**
    //
    // 갇힌 사람은 제 발로 먹으러 못 간다 — 남이 가져다 먹여야 한다(`FeedJob`). 그래서
    // 허기 바가 줄어드는 것만 보이고 **아무 일도 안 일어나는 이유는 안 보인다.** 답은
    // 셋뿐이니 그것을 적는다: 먹일 것이 없거나, 간수가 없거나, 오는 중이거나.
    //
    // 배가 안 고프면 아무 줄도 안 뜬다 — 늘 떠 있으면 읽지 않게 된다.
    static void AppendPrisonFood(StringBuilder sb, UnitBrain brain)
    {
        if (!PrisonFeeding.NeedsFood(brain)) return;

        if (!Feeding.ExistsAnywhere())
            sb.AppendLine("<color=#F44336>먹일 것이 없다 — 창고에 음식이 있어야 먹인다.</color>");
        else if (!PrisonEscort.AnyWarden())
            sb.AppendLine("<color=#F90>간수를 맡은 폰이 없다(P키) — 우클릭 [먹인다]로도 시킬 수 있다.</color>");
        else
            sb.AppendLine("<color=#888>간수가 밥을 가져오는 중.</color>");
    }

    // **도망칠 것 같은지 적는다.**
    //
    // 안 적으면 간수를 켤 이유가 없다 — 어느 날 포로가 사라지고, 그게 간수를 안 둬서인지
    // 원래 그런 것인지 알 길이 없다. 지키는 사람이 있으면 **그래서 낮다**는 것까지 적는다
    // (설정을 켠 값이 화면에 안 보이면 켠 줄도 모른다).
    //
    // 기절해 누워 있으면 시도 자체가 없다 — 그 사실이 곧 "지금은 안전하다"의 답이다.
    static void AppendEscapeRisk(StringBuilder sb, UnitBrain brain)
    {
        // 우리 폰은 갇혀 있어도 도망치지 않는다 — 줄 자체를 안 낸다(없는 위험을 적으면
        // 간수를 켤 이유가 엉뚱한 데서 생긴다).
        if (!PrisonEscort.IsCaptive(brain)) return;

        var bb = brain.Blackboard;
        sb.AppendLine($"탈출  {BarColored(bb.EscapeProgress, 1f, "#F44336", "#4A2A2A")}  " +
                      $"{bb.EscapeProgress * 100f:F0}%");

        if (bb.IsKnockedOut)
        {
            sb.AppendLine("<color=#888>쓰러져 있어 도망칠 궁리를 못 한다 — 게이지가 멈춰 있다.</color>");
            return;
        }

        // **얼마나 남았는지 적는다.** 게이지만 보이면 "지금 이 속도로 얼마나 버티나"에
        // 답이 없어서, 간수를 켤지 말지를 눈대중으로 정하게 된다. 남은 시간은
        // `PrisonBreak.RateFor` 하나로 낸다 — 보이는 수와 실제가 갈리면 안 된다.
        float rate = PrisonBreak.RateFor(brain);
        string left = rate > 0f
                    ? $" — 이대로면 {(PrisonBreak.Complete - bb.EscapeProgress) / rate / 1800f:F1}일 뒤"
                    : "";

        if (PrisonBreak.Watched)
            sb.AppendLine($"<color=#8BC34A>간수가 지키고 있다{left}.</color>");
        else
            sb.AppendLine($"<color=#F44336>지키는 사람이 없다{left} (P키에서 간수).</color>");

        // 굶기면 빨라진다 — 먹이기를 게을리하는 것도 관리를 안 하는 것이다.
        if (Feeding.IsHungry(brain))
            sb.AppendLine("<color=#F90>굶주려 필사적이다 — 탈출이 배로 빨라진다.</color>");
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




    static string BarColored(float val, float max, string fill, string empty)
    {
        int filled = Mathf.Clamp(Mathf.RoundToInt(val / max * 10), 0, 10);
        return $"<color={fill}>{new string('█', filled)}</color><color={empty}>{new string('░', 10 - filled)}</color>";
    }

}
