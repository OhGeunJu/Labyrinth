using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// **폰의 상태표 — 켄시의 그 창**(2026-09-19, 사용자가 켄시 화면을 보여 줬다: *"상세설명이랑 체력보여주는 걸
// 이렇게 만들고 싶어"*).
//
//   왼쪽  : 상태 · 목표 · 짐  /  근접공격 · 방어 · 근력 · 민첩 · 강인함 · 회피
//   오른쪽: 상태 이상 한 줄  /  혈액 · 부위 일곱 · 욕구 — **막대**(가운데 이름 · 오른쪽 숫자)
//
// 그전에는 살펴보기 창이 `█░` 글자 막대로 적었다 — 부위가 두 칸씩 섞여 **한 줄에 두 값**이 있어 훑어서는
// 어느 부위가 다쳤는지가 안 읽혔다. 막대 한 줄에 한 부위면 **색과 길이만으로** 읽힌다.
//
// ## 어기면 조용히 깨지는 것
//
// - **누구를 보여 주나는 살펴보기 창이 답한다**(`InspectPanelUI.ShownPawn`) — *방금 눌러 본 것 ↔ 고른 폰*을
//   거기서 이미 가른다. 여기서 또 가르면 두 창이 **다른 폰**을 보여 주는 순간이 생긴다.
// - **같은 값을 두 창이 그리지 않는다** — 혈액·욕구·부위는 여기로 옮겼고 살펴보기 창에서는 뺐다.
// - **모양은 씬이, 여기는 값만**(`docs/editor_creators.md`) — 줄 수가 정해진 왼쪽은 씬에 다 서 있고, 몸마다
//   개수가 다른 막대(욕구가 몸마다 다르다)만 템플릿을 복제한다. 생성기는 `UI → Create Pawn Sheet`.
// - **창은 둘이다 — 왼쪽(상태·능력치 + 살펴보기 글 + 동작)과 오른쪽(체력 막대, 폰일 때만)**(2026-09-19, 사용자:
//   *"두 창을 나눠 Left Right를"*). 폰이 아닌 것의 정보·동작은 **왼쪽 창**이 든다(사용자가 골랐다).
// - **살펴보기 창이 왼쪽 창에 그린다**(2026-09-19, 사용자: *"원래 있던 창은 없애고 상태창에 … 광석
//   정보라던지 벽 정보라던지 원래 뜨던걸 옮겨야해"*). 켜고 끄는 것·글·동작 버튼은 `InspectPanelUI`가 그대로 하고
//   (그 판단이 1,500줄이다 — 옮겨 적으면 두 벌이 된다) **그리는 자리만 이 판으로 옮겼다.** 여기는 폰일 때
//   **켄시 두 칸을 채우고 글 칸을 그 아래로 내리는 것**만 한다. 폰이 아니면 두 칸을 끄고 글이 판을 채운다.
// - **닫기(X)는 없다**(사용자) — 고른 것이 없으면 살펴보기 창이 판을 끈다.
// - **능력치의 `(+2)`·`(-4)`는 아직 없다** — 기본값과 보정값이 갈린 체계가 없어(`UnitStats`는 한 값) 적을 것이
//   없다. 장비·부상이 능력치를 깎는 날 `StatLine`에서 붙인다.
public class PawnSheetUI : MonoBehaviour
{
    [SerializeField] GameObject      panel;
    [Tooltip("왼쪽 창의 상태·능력치 칸 — 폰일 때만 켠다.")]
    [SerializeField] GameObject      pawnView;
    [Tooltip("오른쪽 창(체력 막대) — **제 창이다**(2026-09-19, 사용자: \"두 창을 나눠\"). 폰일 때만 뜬다.")]
    [SerializeField] GameObject      healthPanel;
    [Tooltip("살펴보기 창의 글 칸 — 폰이면 아래 띠(글이 있을 때만), 아니면 판 전체.")]
    [SerializeField] RectTransform   info;
    [Tooltip("살펴보기 창의 동작 버튼 줄 — 켜져 있을 때만 그 높이를 비운다.")]
    [SerializeField] RectTransform   actionRow;
    [SerializeField] float           pad = 10f, notesH = 30f, actionH = 34f;

    [Header("이름 — 맨 위")]
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI subText;   // [유형] (세력)

    [Header("배경·장비 — 능력치와 같은 색 규칙")]
    [SerializeField] TextMeshProUGUI story, traits;
    [SerializeField] TextMeshProUGUI mainHand, offHand, head, chest, hands, legs;

    [Header("왼쪽")]
    [SerializeField] TextMeshProUGUI state;
    [SerializeField] TextMeshProUGUI goal;
    [SerializeField] TextMeshProUGUI load;
    [SerializeField] TextMeshProUGUI attack, defense, strength, dexterity, toughness, evasion;

    [Header("오른쪽")]
    [SerializeField] TextMeshProUGUI condition;
    [SerializeField] RectTransform   barRoot;
    [SerializeField] GameObject      barTemplate;   // Fill(자식 RectTransform) · Label · Value

    [Header("색")]
    [SerializeField] Color healthy = new(0.20f, 0.45f, 0.13f, 1f);
    [SerializeField] Color hurt    = new(0.60f, 0.45f, 0.10f, 1f);
    [SerializeField] Color broken  = new(0.55f, 0.12f, 0.10f, 1f);
    [SerializeField] Color need    = new(0.65f, 0.52f, 0.12f, 1f);
    [SerializeField] float refreshInterval = 0.15f;

    class Bar { public GameObject Go; public RectTransform Fill; public Image FillImg; public TextMeshProUGUI Label, Value; }
    readonly List<Bar> _bars = new();

    UnitBrain _shownFor;
    float     _next;

    void Awake()
    {
        if (barTemplate != null) barTemplate.SetActive(false);
    }

    void Update()
    {
        bool open   = panel != null && panel.activeSelf;
        var  pawn   = open ? InspectPanelUI.ShownPawn : null;
        bool isPawn = pawn != null;

        // 오른쪽 창은 **왼쪽 창이 폰을 보여 줄 때만** — 왼쪽이 꺼지면 같이 꺼진다.
        if (healthPanel != null && healthPanel.activeSelf != isPawn) healthPanel.SetActive(isPawn);
        if (!open) return;

        // 폰이면 상태·능력치 칸을 켜고 글을 그 아래로 · 아니면 끄고 글이 판을 채운다.
        if (pawnView != null && pawnView.activeSelf != isPawn) pawnView.SetActive(isPawn);
        Layout(isPawn);

        if (pawn != _shownFor) { _shownFor = pawn; _next = 0f; }
        if (!isPawn) { PawnStoryText.Clear(ref _hovered); return; }

        HoverLinks();

        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + refreshInterval;
        Fill(pawn);
    }

    // **빈 자리를 안 남긴다**(2026-09-19, 사용자: *"밑에가 좀 비는데 채워줘 빈 공간이 안보이게"*). 폰은 대개 글(사정)도
    // 동작 버튼도 없다 — 그 자리를 늘 비워 두면 판 아래가 통째로 빈다. **있는 것만큼만** 비우고 나머지는 칸들이 채운다.
    //   · 동작 버튼이 있으면 맨 아래 한 줄
    //   · 폰이고 글이 있으면 그 위 한 띠(글이 없으면 띠도 없다)
    //   · 폰이 아니면 글이 판을 채운다
    TMP_Text _infoText;

    void Layout(bool isPawn)
    {
        if (info == null) return;
        if (_infoText == null) _infoText = info.GetComponent<TMP_Text>();

        bool acts   = actionRow != null && actionRow.gameObject.activeSelf;
        float floor = pad + (acts ? actionH + 6f : 0f);

        if (isPawn)
        {
            bool notes = _infoText != null && !string.IsNullOrWhiteSpace(_infoText.text);
            SetRect(info, new Vector2(0, 0), new Vector2(1, 0), floor, floor + (notes ? notesH : 0f));
            if (pawnView != null && pawnView.transform is RectTransform pv)
            {
                float bottom = floor + (notes ? notesH + 6f : 0f);
                if (!Mathf.Approximately(pv.offsetMin.y, bottom)) pv.offsetMin = new Vector2(pv.offsetMin.x, bottom);
            }
        }
        else SetRect(info, Vector2.zero, Vector2.one, floor, -pad);
    }

    // 앵커와 위·아래 변만 바꾼다(옆 여백은 씬이 든 그대로).
    static void SetRect(RectTransform r, Vector2 aMin, Vector2 aMax, float bottom, float top)
    {
        if (r.anchorMin != aMin) r.anchorMin = aMin;
        if (r.anchorMax != aMax) r.anchorMax = aMax;
        if (!Mathf.Approximately(r.offsetMin.y, bottom)) r.offsetMin = new Vector2(r.offsetMin.x, bottom);
        if (!Mathf.Approximately(r.offsetMax.y, top))    r.offsetMax = new Vector2(r.offsetMax.x, top);
    }

    void Fill(UnitBrain brain)
    {
        var body   = brain.GetComponent<BodyPartSystem>();
        var sfx    = brain.GetComponent<StatusEffectSystem>();
        var corpse = brain.GetComponent<Corpse>();
        var stats  = brain.Stats;

        // ── 이름 — 맨 위 ──
        var (name, sub) = InspectPanelUI.PawnHeader(brain);
        Set(nameText, name);
        Set(subText,  sub);

        // ── 배경·장비 — 이름에 링크가 감겨 있다(올리면 설명, `HoverLinks`) ──
        Set(story,  Or(PawnStoryText.StoryNames(brain)));
        Set(traits, Or(PawnStoryText.TraitNames(brain)));
        var eq = brain.Equipment;
        Set(mainHand, Or(eq?.MainHand?.ItemName));
        Set(offHand,  Or(eq?.OffHand?.ItemName));
        Set(head,     Or(eq?.GetArmor(ArmorSlot.Head)?.ItemName));
        Set(chest,    Or(eq?.GetArmor(ArmorSlot.Chest)?.ItemName));
        Set(hands,    Or(eq?.GetArmor(ArmorSlot.Hands)?.ItemName));
        Set(legs,     Or(eq?.GetArmor(ArmorSlot.Legs)?.ItemName));

        // ── 상태·능력치 ──
        Set(state, Condition(brain, body, corpse));
        Set(goal,  corpse != null ? "-" : PawnStatusText.Describe(brain));
        float cap = CarryCapacity.For(brain);
        Set(load,  cap > 0f ? $"{CarryCapacity.Load(brain) / cap * 100f:0}%" : "-");

        if (stats != null)
        {
            Set(attack,    StatLine(stats, StatName.MeleeAttack));
            Set(defense,   StatLine(stats, StatName.Defense));
            Set(strength,  StatLine(stats, StatName.Strength));
            Set(dexterity, StatLine(stats, StatName.Dexterity));
            Set(toughness, StatLine(stats, StatName.Toughness));
            Set(evasion,   StatLine(stats, StatName.Evasion));
        }

        // ── 오른쪽 ──
        Set(condition, Effects(sfx));

        int n = 0;
        if (body != null)
        {
            // **이름은 몸이 정한다** — 뼈에 흐르는 피도, 돌덩이가 흘리는 피도 없다(`Needs.VitalName`).
            float mb = body.MaxBlood > 0f ? body.MaxBlood : 1f;
            BarAt(n++, Needs.VitalName(Needs.BodyOf(brain)), body.Blood, mb, HealthColor(body.Blood / mb));

            foreach (var t in PartOrder)
            {
                var part = body.GetPart(t);
                Color c = part.IsDestroyed ? broken : part.IsDisabled ? broken : HealthColor(part.CurrentHP / part.MaxHP);
                BarAt(n++, PartName(t), part.CurrentHP, part.MaxHP, c);
            }
        }

        // 욕구 — **도는 축만**(`Needs.Simulates` — 화면이 판정과 같은 답을 쓴다). 시체는 안 배고프다.
        if (corpse == null && Needs.Simulates(brain))
            for (int i = 0; i < Needs.Count; i++)
            {
                var kind = (NeedKind)i;
                if (!Needs.Simulates(brain, kind)) continue;
                float v = brain.Blackboard.Need(kind);
                BarAt(n++, Needs.Name(kind), v, 100f, v > Needs.Threshold * 0.5f ? need : broken);
            }

        for (int i = n; i < _bars.Count; i++) _bars[i].Go.SetActive(false);
    }

    // 켄시의 순서 — 위에서 아래로 몸을 따라 내려간다.
    static readonly BodyPartType[] PartOrder =
    {
        BodyPartType.Head, BodyPartType.Abdomen, BodyPartType.Chest,
        BodyPartType.LeftArm, BodyPartType.RightArm, BodyPartType.LeftLeg, BodyPartType.RightLeg,
    };

    static string PartName(BodyPartType t) => t switch
    {
        BodyPartType.Head     => "머리",
        BodyPartType.Chest    => "가슴",
        BodyPartType.Abdomen  => "배",
        BodyPartType.LeftArm  => "왼팔",
        BodyPartType.RightArm => "오른팔",
        BodyPartType.LeftLeg  => "왼다리",
        BodyPartType.RightLeg => "오른다리",
        _                     => t.ToString(),
    };

    Color HealthColor(float ratio) => ratio > 0.6f ? healthy : ratio > 0.25f ? hurt : broken;

    // 몸의 상태 한 낱말 — **무거운 것부터**(죽음이 기절보다, 기절이 출혈보다 먼저 읽혀야 한다).
    static string Condition(UnitBrain brain, BodyPartSystem body, Corpse corpse)
    {
        if (corpse != null) return "<color=#C44>사망</color>";
        var bb = brain.Blackboard;
        if (bb != null && bb.IsKnockedOut)     return "<color=#E66>기절</color>";
        if (bb != null && bb.CarriedBy != null) return "<color=#CA6>업힘</color>";
        if (bb != null && bb.IsPrisoner)       return "<color=#CA6>갇힘</color>";
        // **지금 흘리고 있을 때만**(`CurrentBleed`) — `BleedRate`는 다쳤을 때의 빠르기(상수)라 늘 0보다 크다.
        if (body != null && body.CurrentBleed > 0f) return "<color=#E66>출혈</color>";
        return "정상";
    }

    // 상태 이상 — 살펴보기 창이 적던 것과 같은 문턱(`InspectPanelUI.AppendStatusEffects`에서 옮겨 왔다).
    static string Effects(StatusEffectSystem sfx)
    {
        if (sfx == null) return "이상 없음";
        var parts = new List<string>();
        if (sfx.AttackIntervalMult > 1.05f) parts.Add("빙결 둔화");
        if (sfx.MoveSpeedMult      < 0.95f) parts.Add("이속 감소");
        if (sfx.HealMult           < 0.95f) parts.Add("회복 감소");
        if (sfx.AttackChanceMod    < 0.95f) parts.Add("약화");
        if (sfx.ShieldPool         > 0f)    parts.Add($"보호막 {sfx.ShieldPool:0}");
        if (sfx.CorruptionGauge    > 0f)    parts.Add($"부패 {sfx.CorruptionGauge:0}");
        return parts.Count > 0 ? string.Join(", ", parts) : "이상 없음";
    }

    static string StatLine(UnitStats stats, StatName s) => $"{stats.GetStat(s):0}";

    // 없으면 흐린 `-` — 값 칸이 비어 보이면 *안 그려진 것*인지 *없는 것*인지 모른다.
    static string Or(string v) => string.IsNullOrEmpty(v) ? "<color=#777>-</color>" : v;

    // **배경·특성 이름에 올리면 설명** — 살펴보기 글에 있던 것과 같은 링크(`PawnStoryText.Tick`). 칸이 둘이라
    // **커서가 올라간 칸만** 묻는다 — 둘 다 물으면 안 올라간 쪽이 매 프레임 툴팁을 지워 깜빡인다.
    string _hovered;

    void HoverLinks()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) { PawnStoryText.Clear(ref _hovered); return; }
        Vector2 p = mouse.position.ReadValue();
        var t = Over(story, p) ? story : Over(traits, p) ? traits : null;
        if (t != null) PawnStoryText.Tick(t, ref _hovered);
        else           PawnStoryText.Clear(ref _hovered);
    }

    static bool Over(TextMeshProUGUI t, Vector2 screen)
    {
        if (t == null || t.canvas == null) return false;
        var cam = t.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : t.canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(t.rectTransform, screen, cam);
    }

    void OnDisable() => PawnStoryText.Clear(ref _hovered);

    static void Set(TextMeshProUGUI t, string s) { if (t != null && t.text != s) t.text = s; }

    void BarAt(int i, string label, float value, float max, Color color)
    {
        while (_bars.Count <= i)
        {
            if (barTemplate == null || barRoot == null) return;
            var go = Instantiate(barTemplate, barRoot);
            var b  = new Bar
            {
                Go    = go,
                Fill  = go.transform.Find("Fill") as RectTransform,
                Label = go.transform.Find("Label")?.GetComponent<TextMeshProUGUI>(),
                Value = go.transform.Find("Value")?.GetComponent<TextMeshProUGUI>(),
            };
            b.FillImg = b.Fill != null ? b.Fill.GetComponent<Image>() : null;
            _bars.Add(b);
        }

        var bar = _bars[i];
        if (!bar.Go.activeSelf) bar.Go.SetActive(true);
        Set(bar.Label, label);
        Set(bar.Value, $"{value:0}");
        if (bar.Fill != null)
        {
            // **길이는 앵커로 낸다** — `Image.fillAmount`는 스프라이트가 있어야 먹는다(없으면 조용히 통째로 칠한다).
            float r = max > 0f ? Mathf.Clamp01(value / max) : 0f;
            var a = bar.Fill.anchorMax; a.x = r; bar.Fill.anchorMax = a;
        }
        if (bar.FillImg != null) bar.FillImg.color = color;
    }
}
