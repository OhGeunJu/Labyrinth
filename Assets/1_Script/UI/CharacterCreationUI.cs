using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// **새 게임의 첫 방랑자를 만드는 화면**(2026-09-20, 사용자: *"새 게임을 시작할 때 일단 한명"* · *"스탯은 다 최하부터
// 시작이야"* · *"특성 배경은 리롤돌리게 그리고 림월드처럼 열정을 추가해서"*).
//
// ## 무엇을 고르고 무엇을 굴리나
// - **고른다(생김새)** — 이름 · 종족 · 성별 · 체형 · 머리형 · 머리카락 · 눈 · 입 · 귀 · 코.
// - **굴린다(사람)** — 성장 배경 둘 · 특성 · 열정. `다시 굴리기`가 씨앗을 바꿔 셋을 한꺼번에 다시 뽑는다.
// - **스탯은 전부 최하(F-)** — 굴린 등급을 버리고 F-로 못 박는다. 특성·배경의 보정은 그 **위에 얹힌다**
//   (`UnitBrain.ApplySpec`이 등급 → 특성 → 배경 순서로 입힌다 — 굴림으로 고르는 이유가 거기 있다).
// - **몇 명을 만드나는 설정이다**(`startPawnCount`, 기본 1 — 사용자: *"시작 캐릭 3개일 수도"*). 사람마다 따로 들고(`Draft`)
//   맨 위 탭으로 오간다. 탭은 개수가 설정에서 오므로 **씬의 템플릿을 복제**한다(개수가 정해진 규격이 아니다).
//
// ## 어기면 조용히 깨지는 것
// - **세우는 문은 `PawnSpawn`이다** — 체형 프리팹을 직접 `Instantiate`하면 성별 확인(`ApplySpec`의 경고)을 비껴간다.
// - **명세의 성별 = 고른 몸의 성별** — 굴릴 때 넘긴다(`PawnSpec.Roll(… gender)`). 안 넘기면 여자 몸에 남자 이름이다.
// - **열려 있는 동안 시간을 멈추고 단축키를 막는다**(`GameTimeManager.SetSpeed(0)` · `InputFocus.ModalOpen`) —
//   이름 칸에 `k`를 치면 운반 명령이 열리고, 스페이스가 먹으면 만드는 중에 습격이 온다.
// - **표식(`StartSpot`)이 없는 씬에서는 안 뜬다** — 옛 시험 씬은 씬에 놓인 폰으로 그대로 시작한다.
public class CharacterCreationUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] RawImage   preview;
    [SerializeField] TMP_InputField nameInput;
    [SerializeField] Button     randomNameButton;
    [SerializeField] ChoiceRowUI[] rows;
    [SerializeField] TextMeshProUGUI storyText;
    [Tooltip("스탯 열 — 스탯 창과 **같은 표·같은 줄**을 쓴다(`StatColumns` · `StatColumnUI`). 열정 표식(`+` `++`)도 거기서 뜬다.")]
    [SerializeField] StatColumnUI[] statColumns;
    [SerializeField] Button     rerollButton;
    [SerializeField] Button     startButton;

    [Tooltip("캐릭터 탭 — 이 템플릿을 `startPawnCount`만큼 복제한다(템플릿 자신은 꺼 둔다).")]
    [SerializeField] Button     tabTemplate;

    [Tooltip("방랑자 프리팹 — `racePrefabs`가 비면 이것 하나가 종족 목록이다.")]
    [SerializeField] GameObject wandererPrefab;

    [Tooltip("고를 수 있는 종족 — 저마다 몸 목록(`BodyVariants`)을 든 프리팹. 비우면 `wandererPrefab` 하나.")]
    [SerializeField] GameObject[] racePrefabs;

    [Tooltip("시작할 때 만드는 사람 수(탭 수).")]
    [SerializeField, Range(1, 6)] int startPawnCount = 1;

    [Tooltip("끄면 판이 열려도 안 뜬다(씬에 놓은 폰으로 시험할 때).")]
    [SerializeField] bool showOnStart = true;

    // ── 한 사람 — 고른 것과 굴린 것 ──
    class Draft
    {
        public int      Race;
        public Gender   Gender;
        public int      Body;                   // 그 성별의 몸 중 몇 번째
        public string   Head = "", Hair = "", Ear = PawnLook.DefaultEar, Nose = PawnLook.None, Eyes = "", Mouth = "";
        public bool     LookSet;                // 기본 얼굴을 한 번 읽어 왔나
        public int      Seed;
        public PawnSpec Spec;
        public string   Name = "";
        public bool     NameEdited;
    }

    readonly List<Draft>  _drafts = new();
    readonly List<Button> _tabs   = new();
    int _cur;
    Draft D => _drafts[_cur];

    PawnPreviewStage _stage;
    bool _open;

    public static bool IsOpen { get; private set; }

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        if (tabTemplate != null) tabTemplate.gameObject.SetActive(false);
        if (!showOnStart) return;
        if (FindFirstObjectByType<StartSpot>() == null)
        {
            Debug.Log("[캐릭터 생성] 씬에 `StartSpot`이 없어 안 띄운다 — 씬에 놓인 폰으로 시작한다(씬에 빈 오브젝트 + `StartSpot`을 하나 놓으면 뜬다).");
            return;
        }
        Open();
    }

    // ── 종족 · 몸 ────────────────────────────────────────────────────────────

    GameObject[] Races => racePrefabs != null && racePrefabs.Length > 0 ? racePrefabs
                        : wandererPrefab != null ? new[] { wandererPrefab } : new GameObject[0];

    GameObject RacePrefab(Draft d)
    {
        var r = Races;
        return r.Length == 0 ? null : r[Mathf.Clamp(d.Race, 0, r.Length - 1)];
    }

    UnitData RaceData(Draft d)
    {
        var p = RacePrefab(d);
        var b = p != null ? p.GetComponent<UnitBrain>() : null;
        return b != null ? b.Data : null;
    }

    List<PawnBody> BodiesOf(Draft d)
    {
        var all = new List<PawnBody>();
        var p   = RacePrefab(d);
        var bv  = p != null ? p.GetComponent<BodyVariants>() : null;
        if (bv != null) foreach (var b in bv.Variants) if (b != null) all.Add(b);
        var fit = all.FindAll(b => b.Gender == d.Gender);
        return fit.Count > 0 ? fit : all;
    }

    PawnBody BodyOf(Draft d)
    {
        var list = BodiesOf(d);
        return list.Count == 0 ? null : list[Mathf.Clamp(d.Body, 0, list.Count - 1)];
    }

    void Open()
    {
        if (Races.Length == 0 || BodiesOf(new Draft()).Count == 0)
        {
            Debug.LogError("[캐릭터 생성] 종족 프리팹에 몸 목록(`BodyVariants`)이 없다 — `Pawn/3. Create Chibi Pawns`를 돌릴 것.");
            return;
        }

        _open = IsOpen = true;
        InputFocus.ModalOpen = true;
        GameTimeManager.SetSpeed(0f);
        panel.SetActive(true);
        transform.SetAsLastSibling();
        HideOtherUi();

        // 시작 자리를 미리 비춘다 — 화면 뒤에서 땅이 서 두어야 닫는 순간 폰이 설 곳이 있다.
        var spot = StartSpot.Current != null ? StartSpot.Current : FindFirstObjectByType<StartSpot>();
        var cam  = FindFirstObjectByType<CameraController>();
        if (spot != null && cam != null) cam.FocusOn(spot.transform.position);

        // **그림 크기는 판이 자리를 잡은 뒤에 잰다** — 켠 프레임에는 레이아웃이 안 돌아 256×256으로 굽고 세로로
        // 늘려 붙였다(2026-09-20 스크린샷의 길쭉한 폰).
        Canvas.ForceUpdateCanvases();
        var rt = preview != null ? preview.rectTransform.rect : new Rect(0, 0, 512, 640);
        _stage = PawnPreviewStage.Create(Mathf.Max(256, (int)rt.width), Mathf.Max(256, (int)rt.height));
        if (preview != null) preview.texture = _stage.Texture;

        foreach (var r in rows) if (r != null) r.OnStep += Step;
        if (nameInput        != null) nameInput.onValueChanged.AddListener(v => { D.Name = v; D.NameEdited = true; RefreshTabs(); });
        if (randomNameButton != null) randomNameButton.onClick.AddListener(RandomName);
        if (rerollButton     != null) rerollButton.onClick.AddListener(Reroll);
        if (startButton      != null) startButton.onClick.AddListener(Begin);

        _drafts.Clear();
        for (int i = 0; i < Mathf.Max(1, startPawnCount); i++)
        {
            var d = new Draft { Gender = Random.value < 0.5f ? Gender.Male : Gender.Female, Seed = Random.Range(1, int.MaxValue) };
            _drafts.Add(d);
            RollSpec(d);
        }
        BuildTabs();
        Select(0);
    }

    // ── 탭 ──────────────────────────────────────────────────────────────────

    void BuildTabs()
    {
        foreach (var b in _tabs) if (b != null) Destroy(b.gameObject);
        _tabs.Clear();
        if (tabTemplate == null) return;
        for (int i = 0; i < _drafts.Count; i++)
        {
            int k = i;
            var b = Instantiate(tabTemplate, tabTemplate.transform.parent);
            b.gameObject.SetActive(_drafts.Count > 1);   // 한 명이면 탭 줄은 뜻이 없다
            b.onClick.AddListener(() => Select(k));
            _tabs.Add(b);
        }
        RefreshTabs();
    }

    void RefreshTabs()
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            var b = _tabs[i];
            var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            string n = !string.IsNullOrEmpty(_drafts[i].Name) ? _drafts[i].Name : "";
            if (label != null) label.text = $"{i + 1}. {n}";
            var img = b.GetComponent<Image>();
            if (img != null) img.color = i == _cur ? new Color(0.30f, 0.36f, 0.48f, 1f) : new Color(0.18f, 0.18f, 0.21f, 1f);
        }
    }

    void Select(int i)
    {
        _cur = Mathf.Clamp(i, 0, _drafts.Count - 1);
        ShowBody(resetLook: !D.LookSet);
        if (nameInput != null) nameInput.SetTextWithoutNotify(D.Name);
        RefreshTabs();
        Refresh();
    }

    // ── 몸을 세운다 ─────────────────────────────────────────────────────────
    // 몸이 바뀌면 프리팹이 바뀐다(관절이 몸마다 구워져 있다). 머리형 목록은 성별 따라 다르다.
    void ShowBody(bool resetLook)
    {
        var body = BodyOf(D);
        _stage.Show(body != null ? body.gameObject : null);
        var look = _stage.Look;
        if (look == null) return;

        // **기본 얼굴은 한 번 입힌 뒤에 읽는다** — 막 세운 사본은 `Start`가 안 돌아 머리형이 빈칸·머리카락이 민머리로
        // 읽혔다(스크린샷의 "기본 · 민머리").
        look.Apply();
        var heads = new List<string>(look.ShapeNames);
        if (resetLook || !heads.Contains(D.Head)) D.Head = look.Shape;
        if (resetLook)
        {
            D.Hair = look.Hair;
            D.Ear  = look.Ear;
            D.Nose = PawnLook.None;
            D.Eyes = D.Mouth = "";
            D.LookSet = true;
        }
        ApplyLook(look, D);
        // **거리는 종족이 바뀔 때만 다시 잰다** — 체형마다 재면 모든 체형이 같은 크기로 보인다(`PawnPreviewStage.Fit`).
        if (_fittedRace != D.Race) { _stage.Fit(); _fittedRace = D.Race; }
        Refresh();
    }

    int _fittedRace = -1;

    static void ApplyLook(PawnLook look, Draft d)
    {
        if (look == null) return;
        look.ShapeOverride = d.Head;
        look.HairOverride  = d.Hair;
        look.EarOverride   = d.Ear;
        look.NoseOverride  = d.Nose;
        look.SetFaceOverride("EyeL", d.Eyes);
        look.SetFaceOverride("EyeR", d.Eyes);
        look.SetFaceOverride("Mouth", d.Mouth);
    }

    // ── 한 줄 넘기기 ────────────────────────────────────────────────────────

    void Step(ChoiceRowUI row, int dir)
    {
        var look = _stage != null ? _stage.Look : null;
        switch (row.Key)
        {
            case "race":
                D.Race = Wrap(D.Race + dir, Races.Length);
                D.Body = 0;
                ShowBody(resetLook: true);
                RollSpec(D);             // 종족이 스탯 범위·이름 풀·이력을 바꾼다
                return;
            case "gender":
                D.Gender = D.Gender == Gender.Male ? Gender.Female : Gender.Male;
                D.Body   = 0;
                ShowBody(resetLook: true);
                RollSpec(D);             // 성별이 이름 풀을 바꾼다 — 같은 씨앗으로 다시 굴린다
                return;
            case "body":
                D.Body = Wrap(D.Body + dir, BodiesOf(D).Count);
                ShowBody(resetLook: false);
                return;
            case "head":  D.Head  = Cycle(D.Head,  look != null ? new List<string>(look.ShapeNames) : null, dir); break;
            case "hair":  D.Hair  = Cycle(D.Hair,  HairChoices(look), dir); break;
            case "ear":   D.Ear   = Cycle(D.Ear,   EarChoices(look), dir); break;
            case "nose":  D.Nose  = Cycle(D.Nose,  NoseChoices(look), dir); break;
            case "eyes":  D.Eyes  = Cycle(D.Eyes,  CellChoices(look, "EyeL"), dir); break;
            case "mouth": D.Mouth = Cycle(D.Mouth, CellChoices(look, "Mouth"), dir); break;
        }
        ApplyLook(look, D);
        Refresh();
    }

    static List<string> HairChoices(PawnLook look)
    {
        var l = look != null ? new List<string>(look.HairNames) : new List<string>();
        l.Add(PawnLook.Bald);
        return l;
    }

    static List<string> EarChoices(PawnLook look)
        => look != null ? new List<string>(look.EarNames) : new List<string>();

    static List<string> NoseChoices(PawnLook look)
    {
        var l = new List<string> { PawnLook.None };
        if (look != null) l.AddRange(look.NoseNames);
        return l;
    }

    // 눈·입 칸 — 맨 앞의 `""`는 *기본 얼굴*이다. 이름이 빈 칸(아틀라스의 빈자리)은 뺀다.
    static List<string> CellChoices(PawnLook look, string feature)
    {
        var l = new List<string> { "" };
        var atlas = look != null ? look.FaceAtlasOf(feature) : null;
        if (atlas != null && atlas.Names != null)
            foreach (var n in atlas.Names) if (!string.IsNullOrEmpty(n)) l.Add(n);
        return l;
    }

    static string Cycle(string cur, List<string> list, int dir)
    {
        if (list == null || list.Count == 0) return cur;
        int i = list.IndexOf(cur);
        return list[Wrap((i < 0 ? 0 : i) + dir, list.Count)];
    }

    static int Wrap(int i, int n) => n <= 0 ? 0 : ((i % n) + n) % n;

    // ── 사람(굴림) ──────────────────────────────────────────────────────────

    void RollSpec(Draft d)
    {
        var data   = RaceData(d);
        var p      = RacePrefab(d);
        var fac    = p != null ? p.GetComponent<Faction>() : null;
        string grp = fac != null && fac.Data != null ? fac.Data.BackstoryGroup : null;

        d.Spec = PawnSpec.Roll(data, d.Seed, null, grp, d.Gender);

        // **스탯은 전부 최하에서 시작한다** — 굴린 등급을 버린다(특성·배경 보정은 `ApplySpec`이 그 위에 얹는다).
        foreach (StatName s in System.Enum.GetValues(typeof(StatName))) d.Spec.Grades[s] = StatGrade.FMinus;
        d.Spec.RecomputeAverage();

        if (!d.NameEdited) d.Name = d.Spec.Name;
        bool current = _drafts.Count > 0 && d == D;
        if (current && nameInput != null) nameInput.SetTextWithoutNotify(d.Name);
        RefreshTabs();
        if (current) Refresh();
    }

    void Reroll()
    {
        D.Seed = Random.Range(1, int.MaxValue);
        RollSpec(D);
    }

    void RandomName()
    {
        D.NameEdited = false;
        var data  = RaceData(D);
        var names = data != null ? data.NamesFor(D.Gender) : null;
        if (names != null && names.Count > 0) D.Name = names[Random.Range(0, names.Count)];
        if (nameInput != null) nameInput.SetTextWithoutNotify(D.Name);
        RefreshTabs();
    }

    // ── 화면 ────────────────────────────────────────────────────────────────

    void Refresh()
    {
        if (_drafts.Count == 0) return;
        var look = _stage != null ? _stage.Look : null;
        var body = BodyOf(D);
        var data = RaceData(D);
        foreach (var r in rows)
        {
            if (r == null) continue;
            switch (r.Key)
            {
                case "race":   r.Show(data != null && !string.IsNullOrEmpty(data.RaceName) ? data.RaceName : "사람", Races.Length > 1); break;
                case "gender": r.Show(D.Gender.DisplayName(), true); break;
                case "body":   r.Show(body != null ? body.ShapeLabel : "", BodiesOf(D).Count > 1); break;
                case "head":   r.Show(PawnLookLabels.Head(D.Head), true); break;
                case "hair":   r.Show(PawnLookLabels.Hair(D.Hair), true); break;
                case "ear":    r.Show(PawnLookLabels.Ear(D.Ear), true); break;
                case "nose":   r.Show(PawnLookLabels.Nose(D.Nose), true); break;
                case "eyes":   r.Show(CellLabel(D.Eyes,  CellChoices(look, "EyeL")), true); break;
                case "mouth":  r.Show(CellLabel(D.Mouth, CellChoices(look, "Mouth")), true); break;
            }
        }

        if (storyText != null && D.Spec != null) storyText.text = StoryOf(D.Spec);
        BindStats();
    }

    // **만들 사람의 스탯** — 최하(F-) + 특성·배경 보정 + 열정. 얹는 함수는 폰이 태어날 때와 **같은 것**이다
    // (`TraitPool.Apply` · `BackstoryPool.Apply`) — 따로 더하면 화면에 뜬 값과 태어난 폰의 값이 갈린다.
    void BindStats()
    {
        var spec = D.Spec;
        if (spec == null || statColumns == null) return;
        var stats = UnitStats.FromData(RaceData(D));
        foreach (var kv in spec.Grades) stats.SetStat(kv.Key, kv.Value.ToValue());
        foreach (var t in spec.Traits) if (t != null) TraitPool.Apply(t, stats);
        BackstoryPool.Apply(spec.Childhood, stats);
        BackstoryPool.Apply(spec.Adulthood, stats);
        stats.SetPassions(spec.Passions);
        foreach (var col in statColumns) if (col != null) col.Bind(stats, null);
    }

    // 눈·입은 그림 이름이 파일 이름이라 번호로 적는다(`기본` · `3 / 12`).
    static string CellLabel(string cell, List<string> choices)
    {
        if (string.IsNullOrEmpty(cell)) return "기본";
        int i = choices.IndexOf(cell);
        return i < 0 ? cell : $"{i} / {choices.Count - 1}";
    }

    static string StoryOf(PawnSpec s)
    {
        var sb = new StringBuilder();
        // **효과는 줄로 안 적고 올렸을 때 띄운다**(2026-09-20, 사용자: *"밑에 회색으로 보여주는 건 없애고 배경에는 툴팁을
        // 넣어서 설명을해"*) — 링크는 스탯 창·살펴보기 창과 **같은 것**(`PawnStoryText`)이라 설명도 같은 글이 뜬다.
        sb.AppendLine("<b>성장 배경</b>");
        sb.AppendLine($"  유년기  {Story(s.Childhood)}");
        sb.AppendLine($"  성년기  {Story(s.Adulthood)}");

        // **재능** — 어느 스탯이 빨리 느나(`Passions`). 표에도 `+`로 뜨지만 스물 줄 사이에서 찾게 두지 않는다.
        sb.AppendLine();
        sb.AppendLine("<b>재능</b>");
        var gifts = new List<string>();
        foreach (Passion want in new[] { Passion.Major, Passion.Minor })
            foreach (var col in StatColumns.All)
                foreach (var st in col.Stats)
                    if (s.Passions.TryGetValue(st, out var p) && p == want)
                        gifts.Add($"{st.ToDisplay()} {Passions.Mark(p)}");
        sb.AppendLine(gifts.Count > 0 ? "  " + string.Join("  ", gifts) : "  <color=#888>없음</color>");

        sb.AppendLine();
        sb.AppendLine("<b>특성</b>");
        if (s.Traits.Count == 0) sb.AppendLine("  <color=#888>없음</color>");
        foreach (var t in s.Traits)
            if (t != null) sb.AppendLine($"  {PawnStoryText.TraitLink(t, $"<color=#CFE3F0>{t.DisplayName}</color>")}");

        return sb.ToString().TrimEnd();
    }

    static string Story(BackstoryData b)
        => b == null ? "<color=#888>없음</color>"
                     : PawnStoryText.StoryLink(b, $"<color=#E8C566>{b.DisplayName}</color>");

    string _hoveredStory;

    // ── 시작 ────────────────────────────────────────────────────────────────

    void Begin()
    {
        if (!_open || _drafts.Count == 0) return;

        var spot = StartSpot.Current != null ? StartSpot.Current : FindFirstObjectByType<StartSpot>();
        Vector3 center = spot != null ? spot.transform.position : Vector3.zero;

        var movers = new List<PawnMover>();
        for (int i = 0; i < _drafts.Count; i++)
        {
            var d = _drafts[i];
            if (d.Spec == null) continue;
            if (!string.IsNullOrWhiteSpace(d.Name)) d.Spec.Name = d.Name.Trim();

            // 첫 사람은 한가운데, 나머지는 둘레에 고르게(공터가 12m라 1.6m 고리는 늘 안이다).
            Vector3 at = center;
            if (i > 0)
            {
                float a = (i - 1) * Mathf.PI * 2f / Mathf.Max(1, _drafts.Count - 1);
                at += new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.6f;
            }
            at.y = MapManager.Instance != null ? MapManager.Instance.SampleGroundHeight(at.x, at.z)
                                              : TerrainHeight.At(at.x, at.z);

            var body = BodyOf(d);
            if (body == null) continue;
            var go = PawnSpawn.Instantiate(body.gameObject, at, Quaternion.identity, null, d.Spec);
            go.transform.position = PawnPlacement.StandingPos(go, at);

            var brain = go.GetComponent<UnitBrain>();
            if (brain != null) brain.ApplySpec(d.Spec);
            ApplyLook(go.GetComponent<PawnLook>(), d);

            var mover = go.GetComponent<PawnMover>();
            if (mover != null) movers.Add(mover);
        }

        Close();

        var sel = FindFirstObjectByType<SelectionManager>();
        if (sel != null && movers.Count > 0) sel.SelectOnly(movers);
        var cam = FindFirstObjectByType<CameraController>();
        if (cam != null) cam.FocusOn(center);
    }

    void Close()
    {
        _open = IsOpen = false;
        InputFocus.ModalOpen = false;
        PawnStoryText.Clear(ref _hoveredStory);   // 창이 닫히면 `OnPointerExit`가 안 와 설명만 남는다
        RestoreOtherUi();
        GameTimeManager.SetSpeed(1f);
        if (panel != null) panel.SetActive(false);
        if (_stage != null) Destroy(_stage.gameObject);
        _stage = null;
    }

    // ── 미리보기 돌리기 — 누른 채 끌면 돈다(사용자: *"캐릭 화면 안돌아가"*) ────────────
    void Update()
    {
        if (!_open) return;

        // 배경·특성 이름에 올리면 설명(`PawnStoryText.Tick` — 스탯 창·살펴보기 창과 같은 길).
        PawnStoryText.Tick(storyText, ref _hoveredStory);

        if (_stage == null || preview == null) return;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;
        if (!RectTransformUtility.RectangleContainsScreenPoint(preview.rectTransform, mouse.position.ReadValue(), null)) return;

        // 휠 — **값의 크기를 믿지 않는다**(2026-09-20, 사용자: *"카메라 줌이 너무 느려"*). 120으로 나눴더니 이 입력 설정에서는
        // 한 칸이 1 안팎이라 0.8%씩 움직였다 — 플랫폼·설정마다 한 칸의 값이 달라서, 부호만 보고 한 번에 한 칸으로 센다.
        float wheel = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) > 0.01f) _stage.Zoom(Mathf.Sign(wheel));

        if (mouse.leftButton.isPressed) _stage.Turn(-mouse.delta.ReadValue().x * 0.5f);
    }

    void OnDestroy()
    {
        if (_open) { InputFocus.ModalOpen = false; RestoreOtherUi(); }
    }

    // ── 다른 UI는 치운다 ─────────────────────────────────────────────────────
    //
    // (2026-09-20, 사용자: *"이창에서는 다른 UI들이 안보이면 좋겠어"* — 무리 명부가 창 위에 겹치고 시간 표시·모드 버튼이 비쳤다.)
    // **치운 것만 기억했다가 그것만 되돌린다** — 닫을 때 일괄로 켜면 원래 꺼져 있던 창까지 따라 열린다(`GameModeManager`가
    // 모드를 바꿀 때와 같은 규칙). **툴팁 둘은 남긴다** — 스탯·배경 설명이 거기로 뜬다. 다른 루트 캔버스(따로 그려지는 HUD)는
    // 캔버스째 끈다 — 같은 캔버스 안이 아니라 순서로는 못 덮는다.
    readonly List<GameObject> _hiddenUi       = new();
    readonly List<Canvas>     _hiddenCanvases = new();

    void HideOtherUi()
    {
        var mine = GetComponentInParent<Canvas>(true);
        var root = mine != null ? mine.rootCanvas : null;

        if (root != null)
            foreach (Transform c in root.transform)
            {
                if (c == transform || !c.gameObject.activeSelf || KeepsShowing(c)) continue;
                c.gameObject.SetActive(false);
                _hiddenUi.Add(c.gameObject);
            }

        foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (cv == null || !cv.isRootCanvas || cv == root || !cv.enabled) continue;
            if (cv.renderMode == RenderMode.WorldSpace) continue;   // 세상에 붙은 글자(머리 위 이름 등)는 화면 UI가 아니다
            if (KeepsShowing(cv.transform)) continue;
            cv.enabled = false;
            _hiddenCanvases.Add(cv);
        }
    }

    static bool KeepsShowing(Transform t)
        => t.GetComponentInChildren<HoverTooltipUI>(true) != null || t.GetComponentInChildren<ItemInfoUI>(true) != null;

    void RestoreOtherUi()
    {
        foreach (var g in _hiddenUi) if (g != null) g.SetActive(true);
        foreach (var c in _hiddenCanvases) if (c != null) c.enabled = true;
        _hiddenUi.Clear();
        _hiddenCanvases.Clear();
    }
}
