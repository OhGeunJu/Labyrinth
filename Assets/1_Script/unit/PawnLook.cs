using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// **턱 · 머리 — 전부 구워 두고 여기서 한 벌씩만 켠다**
// (2026-09-18, 사용자: *"얼굴형도 림월드처럼 여러개 만들어주고 헤어스타일도 여러개"* →
//  *"얼굴형은 턱 모양정도 … 눈은 텍스쳐로 그려서"*).
//
// **표정은 칸을 고르는 일이다**(2026-09-18) — 눈·입은 아틀라스 한 장을 나눠 쓰는 작은 판이고
// (`ChibiFacePart`), 여기는 **왼눈·오른눈·입에 어느 그림을 보일지**만 정한다. 턱이 다섯이라
// 같은 판이 다섯 벌 있는데, 판을 직접 부르면 켜진 턱의 것만 바뀌어 **턱을 갈면 표정이 풀린다** —
// 그래서 여기서 다섯 벌 모두에 건다.
//
// 앞서는 순서: **코드가 부른 표정(`SetFace`) > 인스펙터로 못 박은 얼굴 > 구울 때의 기본.**
// 못 박은 것은 *이 사람의 평소 얼굴*이고, 표정은 그 위에 잠깐 얹혔다 풀리는 것이다.
//
// `PropVariant`와 같은 규칙이다 — 조합마다 프리팹을 내면 천 개가 넘는다. 그래서 `ChibiPawnCreator`가
// **머리 아래에 전부 굽고**(기본 벌만 켠 채) 여기가 **명세의 씨앗**으로 한 벌씩 켠다.
//
// **굴리지 않는다.** `Random`으로 고르면 청크가 다시 올라올 때마다 얼굴이 바뀐다 — 같은 사람이
// 걸어갔다 오면 딴사람이다. 씨앗은 `PawnSpec.Seed`(저작·주입된 사람)이고, 없으면 **이름의
// 안정 해시**다(`string.GetHashCode`는 실행마다 달라서 못 쓴다).
//
// **이름이 곧 묶음이다** — `Head.Shape.Square` · `Head.Hair.Bob.Cap`. 둘째 마디가 묶음,
// 셋째 마디가 벌이다. 기본 머리만은 리거가 `HeadMesh`로 이름을 바꾸는데, 그것은 **공통 윗반쪽**이라 안 건드린다(턱만 `Head.Shape.*`다).
//
// **머리가 바뀌면 머리카락도 같이 옮긴다** — 머리카락은 기준 머리(`…BroadSoft`)에 맞춰 구웠으므로
// 좁고 긴 머리에 그대로 씌우면 정수리가 뚫고 나온다. 기준의 틀에서 고른 머리의 틀로 같은 비로 옮긴다.
//
// **못 박을 수 있다**(2026-09-18, 사용자: *"인스펙터로 바꿀 수 있게 만들자"*) — 둘 다 비우면
// 씨앗이 고르고, 적으면 그것이 이긴다. 퀘스트 NPC처럼 *이 사람은 이 얼굴*이어야 하는 저작이
// 여기 적힌다(`PawnSpecData`가 등급·특성을 못 박는 것과 같은 자리). 인스펙터는 `PawnLookEditor`.
//
// **고르는 축이 여럿인 머리**(2026-10-27 — 해골: 두개골 · 턱 · 이 · 빠진 이) — `Head.Pick.{축}.{벌}`. 축마다 씨앗이 따로 하나를
// 고르고(축 이름을 씨앗에 섞는다), 못 박을 수도 있다(`pickOverrides`). 벌이 다른 축에 매이면 이름 뒤에 `@{축}.{벌}`을 붙인다 —
// `@Jaw.!None`은 *턱이 없지 않을 때만*. 축과 벌은 렌더러 이름에서 모은다(조건에만 나오는 벌도 — 턱 `None`은 렌더러가 없다).
public class PawnLook : MonoBehaviour
{
    // **기준 머리** — 이름이 이것으로 끝나는 머리(`Male_BroadSoft`). 머리카락은 그것에 맞춰 구웠다.
    public const string BasisShape = "BroadSoft";   // 이름은 `ChibiNames`
    public const string Bald       = "Bald";

    // **윗머리 모양 둘** — 머리카락 한 장은 이 둘로 굽는다(`Head.Hair.Short.Shell.Box`).
    // 림월드의 `Wide` 턱은 윗머리까지 네모라 둥근 뚜껑을 씌우면 모서리가 뚫고 나온다.
    public const string FitRound = "Round", FitBox = "Box";
    public static string HairFitOf(string shape) => shape != null && shape.EndsWith("Square") ? FitBox : FitRound;

    [Tooltip("얼굴형을 못 박는다. 비우면 씨앗이 고른다.")]
    [SerializeField] string shapeOverride = "";
    [Tooltip("머리를 못 박는다. 비우면 씨앗이 고른다. `Bald`는 민머리.")]
    [SerializeField] string hairOverride = "";

    [Tooltip("왼눈 그림(아틀라스 이름). 비우면 기본. `-`면 없음.")]
    [SerializeField] string eyeLOverride = "";
    [Tooltip("오른눈 그림. 비우면 기본. `-`면 없음.")]
    [SerializeField] string eyeROverride = "";
    [Tooltip("입 그림. 비우면 기본(없음). `-`면 없음.")]
    [SerializeField] string mouthOverride = "";
    // **코는 입체다**(2026-09-19, 사용자: *"코는 텍스쳐가 아니라 모델링으로"*) — 머리형마다 모양마다 한 벌
    // (`Head.Nose.{모양}.{머리형}`)을 구워 두고 **고른 모양 × 지금 머리형** 하나만 켠다. 비우면 없음.
    [Tooltip("코 모양 — `Button` · `Pointy` · `Long` · `Wide`. 비우거나 `-`면 없음.")]
    [SerializeField] string noseOverride = "";

    // **귀**(2026-09-19) — 머리카락과 같은 길: 모양마다 한 벌(`Head.Ear.{모양}.L/R`)을 구워 두고 하나만 켠다.
    // 비우면 **일반 귀**, `-`면 없음. 귀는 판이 아니라 입체라 머리형이 바뀌면 머리카락처럼 **고른 머리로 옮긴다.**
    public const string DefaultEar = "Human";
    [Tooltip("귀 모양 — `Human` · `Elf` · `Goblin`. 비우면 일반 귀(`Human`). `-`면 없음.")]
    [SerializeField] string earOverride = "";

    public const string None = "-";
    public static readonly string[] FaceFeatures = { "EyeL", "EyeR", "Mouth" };

    [Tooltip("고르는 축을 못 박는다 — `축=벌`(예: `Jaw=None`). 없는 축이면 씨앗이 고른다.")]
    [SerializeField] List<string> pickOverrides = new();

    // 코드가 부른 표정 — 인스펙터보다 앞선다. 비면 안 부른 것이다.
    readonly Dictionary<string, string> _expression = new();
    readonly List<ChibiFacePart> _faceParts = new();

    readonly SortedDictionary<string, List<Renderer>> _shapes = new();
    readonly SortedDictionary<string, List<Renderer>> _hairs  = new();
    readonly SortedDictionary<string, List<Renderer>> _ears   = new();
    readonly SortedDictionary<string, List<Renderer>> _noses  = new();   // 모양 → (머리형마다 한 벌)
    // **투구**(2026-10-23) — 머리카락처럼 기준 머리에 맞춰 굽고 윗머리 모양마다 한 벌(`Head.Helmet.Cap.Shell.Box`).
    // 켜는 것은 찬 장비가 정한다(`PawnApparel` → `Helmeted`) — 씨앗이 고르는 묶음이 아니다.
    readonly List<Renderer> _helmets = new();
    readonly Dictionary<string, Transform> _shapeHead = new();   // 머리형 → 그 머리 살(이목구비 판 말고)

    // 고르는 축 — 축 → 벌들 · 렌더러마다 (축, 벌, 조건들) · 지금 고른 것.
    readonly SortedDictionary<string, SortedSet<string>> _pickAxes = new();
    readonly List<(Renderer r, string axis, string option, (string axis, string option, bool not)[] when)> _pickParts = new();
    readonly Dictionary<string, string> _picked = new();

    // **머리카락이 지금 어느 머리에 맞춰져 있나** — 옮길 때 *그 머리의 틀*에서 *고른 머리의 틀*로
    // 옮긴다. 구운 값을 따로 들면 한 번 옮긴 자리가 저장된 뒤 그것을 *구운 값*으로 다시 읽어 **옮길
    // 때마다 어긋난다**(리그를 다시 세우면 살이 통째로 옮겨 가므로 절대 자리도 못 믿는다). 비면 기준 머리다.
    [SerializeField, HideInInspector] string fittedTo = "";

    bool   _gathered;

    public string Shape      { get; private set; } = "";
    public string Hair       { get; private set; } = Bald;
    public string Ear        { get; private set; } = None;
    public string Nose       { get; private set; } = None;

    // 투구를 썼나 — 쓰면 머리카락을 다 끄고 투구 껍데기를 켠다. 색은 `PawnApparel`이 칠한다.
    bool _helmeted;
    public bool Helmeted { get => _helmeted; set { if (_helmeted == value) return; _helmeted = value; Apply(); } }
    public IReadOnlyList<Renderer> HelmetRenderers { get { if (!_gathered) Gather(); return _helmets; } }

    // 인스펙터가 읽는 목록 — 모은 뒤에만 찬다.
    public IEnumerable<string> ShapeNames => _shapes.Keys;
    public IEnumerable<string> HairNames  => _hairs.Keys;
    public IEnumerable<string> EarNames   => _ears.Keys;
    public IEnumerable<string> NoseNames  => _noses.Keys;
    public IEnumerable<string> PickAxes   => _pickAxes.Keys;
    public IEnumerable<string> PickOptions(string axis) => _pickAxes.TryGetValue(axis, out var set) ? set : Enumerable.Empty<string>();
    public string Picked(string axis) => _picked.TryGetValue(axis, out var v) ? v : "";
    // 고른 축의 살들 — 개체마다 칠하는 쪽(`BoneLook`)이 읽는다.
    public IEnumerable<Renderer> PickRenderers { get { if (!_gathered) Gather(); foreach (var p in _pickParts) if (p.r != null) yield return p.r; } }

    public string PickOverride(string axis)
    {
        foreach (var e in pickOverrides)
            if (e.StartsWith(axis + "=")) return e.Substring(axis.Length + 1);
        return "";
    }

    public void SetPickOverride(string axis, string option)
    {
        pickOverrides.RemoveAll(e => e.StartsWith(axis + "="));
        if (!string.IsNullOrEmpty(option)) pickOverrides.Add($"{axis}={option}");
        Apply();
    }

    public void ClearPickOverrides() { pickOverrides.Clear(); Apply(); }

    // 씨앗 — 같은 사람은 늘 같은 얼굴(`SeedOf`). 칠하는 쪽도 이것으로 고른다.
    public int Seed => SeedOf();

    public string ShapeOverride      { get => shapeOverride;      set { shapeOverride = value ?? "";      Apply(); } }
    public string HairOverride       { get => hairOverride;       set { hairOverride = value ?? "";       Apply(); } }
    public string EarOverride        { get => earOverride;        set { earOverride = value ?? "";        Apply(); } }
    public string NoseOverride       { get => noseOverride;       set { noseOverride = value ?? "";       Apply(); } }

    // 이목구비 — 인스펙터가 읽고 쓴다. **눈과 입은 아틀라스가 따로다** — 눈 줄에서 입 그림을
    // 고를 수 없게(사용자: *"같은곳에서 다 불러올 수 있게 만들지 말고"*).
    public ChibiFaceAtlas FaceAtlasOf(string feature)
    {
        if (!_gathered) Gather();
        foreach (var p in _faceParts)
            if (p != null && FeatureOf(p.name) == feature && p.Atlas != null) return p.Atlas;
        return null;
    }

    public string FaceOverride(string feature) => feature switch
    {
        "EyeL" => eyeLOverride, "EyeR" => eyeROverride, "Mouth" => mouthOverride, _ => ""
    };

    public void SetFaceOverride(string feature, string cell)
    {
        cell ??= "";
        switch (feature)
        {
            case "EyeL":  eyeLOverride  = cell; break;
            case "EyeR":  eyeROverride  = cell; break;
            case "Mouth": mouthOverride = cell; break;
        }
        Apply();
    }

    // **표정** — 코드가 부른다. `null`은 *그 이목구비는 건드리지 않음*, `""`는 *표정을 풀고 평소
    // 얼굴로*, `"-"`는 *없음*. 윙크는 한쪽 눈만 준다: `SetFace(eyeL: "eye_haha", eyeR: null, mouth: null)`.
    public void SetFace(string eyeL = null, string eyeR = null, string mouth = null)
    {
        void Put(string f, string v) { if (v == null) return; if (v.Length == 0) _expression.Remove(f); else _expression[f] = v; }
        Put("EyeL", eyeL); Put("EyeR", eyeR); Put("Mouth", mouth);
        ApplyFace();
    }

    public void ClearExpression() { _expression.Clear(); ApplyFace(); }

    // 계층이 바뀌었을 때(다시 굽거나 리그를 다시 세운 뒤) 목록을 다시 모은다.
    public void Regather() { _gathered = false; Apply(); }

    void Start() => Apply();

    public void Apply()
    {
        // **옛 이름(림월드 이름)을 새 이름으로**(`ChibiNames`) — 못 박아 둔 얼굴이 조용히 굴림으로 돌아가지 않게.
        shapeOverride = ChibiNames.Migrate(shapeOverride);
        eyeLOverride  = ChibiNames.Migrate(eyeLOverride);
        eyeROverride  = ChibiNames.Migrate(eyeROverride);
        mouthOverride = ChibiNames.Migrate(mouthOverride);

        if (!_gathered) Gather();
        if (_shapes.Count == 0 && _pickAxes.Count == 0) return;   // 치비 머리가 아닌 폰 — 아무 일도 안 한다

        int seed = SeedOf();

        // **못 박은 것이 이긴다** — 없거나 이름이 틀리면 씨앗으로 떨어진다.
        Shape = _shapes.ContainsKey(shapeOverride) ? shapeOverride : Pick(_shapes, seed);

        // 머리 벌 + 1 — 마지막 하나가 **민머리**다(모든 머리를 끈다).
        if (hairOverride == Bald)                     Hair = Bald;
        else if (_hairs.ContainsKey(hairOverride))    Hair = hairOverride;
        else
        {
            int hairIndex = Mod(seed / 7, _hairs.Count + 1);
            Hair = hairIndex == _hairs.Count ? Bald : KeyAt(_hairs, hairIndex);
        }

        Show(_shapes, Shape);
        Show(_hairs,  _helmeted ? Bald : Hair);   // 투구 밑으로 머리카락이 뚫고 나오지 않게 — 민머리처럼 다 끈다

        // 귀 — 비우면 둥근 귀, `-`면 없음, 모르는 이름이면 둥근 귀.
        Ear = earOverride == None ? None
            : _ears.ContainsKey(earOverride) ? earOverride
            : _ears.ContainsKey(DefaultEar) ? DefaultEar : None;
        Show(_ears, Ear);

        // 코 — 고른 모양 중 **지금 머리형의 것** 하나만(마지막 마디가 머리형이다).
        Nose = _noses.ContainsKey(noseOverride) ? noseOverride : None;
        foreach (var kv in _noses)
        foreach (var r in kv.Value)
            if (r != null) r.enabled = kv.Key == Nose && r.name.EndsWith("." + Shape);

        // 머리카락은 윗머리 모양이 맞는 벌만 — 마지막 마디가 모양이다(`Shell.Box`).
        string fit = HairFitOf(Shape);
        if (!_helmeted && _hairs.TryGetValue(Hair, out var shown))
            foreach (var r in shown)
            {
                if (r == null) continue;
                var parts = r.name.Split('.');
                if (parts.Length >= 5) r.enabled = parts[4] == fit;
            }
        foreach (var r in _helmets)
        {
            if (r == null) continue;
            var parts = r.name.Split('.');
            r.enabled = _helmeted && parts.Length >= 5 && parts[4] == fit;
        }

        // **머리카락을 고른 머리로 옮긴다** — 기준 머리의 틀(자리·크기)에서 고른 머리의 틀로 같은 비로.
        // 폭만 넓히던 동안은 괜찮았는데, 좁은 머리는 **길고 더 높이 솟아** 폭만 맞추면 정수리가 뚫는다.
        _shapeHead.TryGetValue(Shape, out var chosen);
        if (!_shapeHead.TryGetValue(fittedTo, out var frame)) _shapeHead.TryGetValue(BasisKey(), out frame);
        if (chosen != null && frame != null && chosen != frame)
        {
            Vector3 fP = frame.localPosition, cP = chosen.localPosition;
            Vector3 k  = Div(chosen.localScale, frame.localScale);
            // **귀도 같은 셈으로 옮긴다** — 기준 머리에 맞춰 구웠으므로 머리카락과 같은 틀을 탄다(따로 셈하면 한쪽만 어긋난다).
            // 투구도 같은 틀이다.
            void Fit(Renderer r)
            {
                if (r == null) return;
                var t = r.transform;
                t.localPosition = cP + Vector3.Scale(t.localPosition - fP, k);
                t.localScale    = Vector3.Scale(t.localScale, k);
            }
            foreach (var map in new[] { _hairs, _ears })
            foreach (var kv in map)
            foreach (var r in kv.Value) Fit(r);
            foreach (var r in _helmets) Fit(r);
        }
        if (chosen != null) fittedTo = Shape;

        ApplyPicks(seed);
        ApplyFace();
    }

    void ApplyPicks(int seed)
    {
        _picked.Clear();
        foreach (var kv in _pickAxes)
        {
            string authored = PickOverride(kv.Key);
            _picked[kv.Key] = kv.Value.Contains(authored) ? authored : KeyAt(kv.Value, Mod(Mix(seed, kv.Key), kv.Value.Count));
        }
        foreach (var (r, axis, option, when) in _pickParts)
        {
            if (r == null) continue;
            bool on = _picked[axis] == option;
            foreach (var (a, o, not) in when)
                if (on && a != null && _picked.TryGetValue(a, out var got)) on = not ? got != o : got == o;
            r.enabled = on;
        }
    }

    // 축마다 씨앗을 갈라 쓴다 — 같은 씨앗을 그대로 나누면 벌 수가 같은 축끼리 늘 같은 자리를 고른다.
    static int Mix(int seed, string axis)
    {
        unchecked
        {
            int h = seed;
            foreach (char c in axis) h = (h ^ c) * 16777619;
            return h ^ (h >> 13);
        }
    }

    // 턱 다섯 벌의 판 **모두**에 건다 — 켜진 턱에만 걸면 턱을 가는 순간 표정이 풀린다.
    void ApplyFace()
    {
        if (!_gathered) Gather();
        foreach (var part in _faceParts)
        {
            if (part == null) continue;
            string feature = FeatureOf(part.name);
            if (feature == null) continue;

            if (_expression.TryGetValue(feature, out var expr)) { part.Set(expr); continue; }
            string authored = FaceOverride(feature);
            part.Set(string.IsNullOrEmpty(authored) ? null : authored);   // null = 구울 때의 기본
        }
    }

    string BasisKey()
    {
        foreach (var key in _shapes.Keys) if (key.EndsWith(BasisShape)) return key;
        foreach (var key in _shapes.Keys) return key;
        return "";
    }

    static Vector3 Div(Vector3 a, Vector3 b) =>
        new(a.x / Mathf.Max(1e-4f, b.x), a.y / Mathf.Max(1e-4f, b.y), a.z / Mathf.Max(1e-4f, b.z));

    static string FeatureOf(string name)
    {
        foreach (var f in FaceFeatures) if (name.EndsWith("." + f)) return f;
        return null;
    }

    // ── 모으기 ──────────────────────────────────────────────────────────────

    void Gather()
    {
        _gathered = true;
        _shapes.Clear(); _hairs.Clear(); _ears.Clear(); _noses.Clear(); _shapeHead.Clear(); _faceParts.Clear(); _helmets.Clear();
        _pickAxes.Clear(); _pickParts.Clear();

        // 머리 관절 아래를 본다. 리그가 없으면(아직 안 세운 프리팹) 이름으로 찾는다.
        var rig  = GetComponentInChildren<PawnRig>(true);
        var head = rig != null && rig.Head != null ? rig.Head : FindDeep(transform, "Head");
        if (head == null) return;

        head.GetComponentsInChildren(true, _faceParts);

        foreach (var r in head.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name;
            // 윗반쪽은 **모두가 쓰는 공통 조각**이라 묶음이 아니다 — 늘 켜져 있다.
            if (n == "HeadMesh" || n == "Head") continue;
            if (!n.StartsWith("Head.")) continue;
            if (n.StartsWith("Head.Pick.")) { AddPick(r); continue; }

            var parts = n.Split('.');
            if (parts.Length < 3) continue;
            switch (parts[1])
            {
                case "Shape":
                    Add(_shapes, parts[2], r);
                    if (parts.Length == 3) _shapeHead[parts[2]] = r.transform;
                    break;
                case "Hair": Add(_hairs, parts[2], r); break;
                case "Ear":  Add(_ears,  parts[2], r); break;
                case "Nose": Add(_noses, parts[2], r); break;
                case "Helmet": _helmets.Add(r); break;
            }
        }
    }

    // `Head.Pick.Teeth.Orc@Gap.One@Jaw.!None` → 축 Teeth · 벌 Orc · 조건 (Gap = One) · (Jaw ≠ None).
    void AddPick(Renderer r)
    {
        var segs = r.name.Split('@');
        var head = segs[0].Split('.');
        if (head.Length < 4) return;
        Option(head[2], head[3]);
        var when = new (string, string, bool)[segs.Length - 1];
        for (int i = 1; i < segs.Length; i++)
        {
            int dot = segs[i].IndexOf('.');
            if (dot <= 0) continue;
            string axis = segs[i].Substring(0, dot), opt = segs[i].Substring(dot + 1);
            bool not = opt.StartsWith("!");
            if (not) opt = opt.Substring(1);
            Option(axis, opt);
            when[i - 1] = (axis, opt, not);
        }
        _pickParts.Add((r, head[2], head[3], when));

        void Option(string axis, string opt)
        {
            if (!_pickAxes.TryGetValue(axis, out var set)) _pickAxes[axis] = set = new SortedSet<string>();
            set.Add(opt);
        }
    }

    static string KeyAt(SortedSet<string> set, int index)
    {
        foreach (var k in set) if (index-- == 0) return k;
        return "";
    }

    static void Add(SortedDictionary<string, List<Renderer>> map, string key, Renderer r)
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = new List<Renderer>();
        list.Add(r);
    }

    static void Show(SortedDictionary<string, List<Renderer>> map, string key)
    {
        foreach (var kv in map)
        {
            bool on = kv.Key == key;
            foreach (var r in kv.Value) if (r != null) r.enabled = on;
        }
    }

    static string Pick(SortedDictionary<string, List<Renderer>> map, int seed) =>
        map.Count == 0 ? "" : KeyAt(map, Mod(seed, map.Count));

    static string KeyAt(SortedDictionary<string, List<Renderer>> map, int index)
    {
        foreach (var kv in map) if (index-- == 0) return kv.Key;
        return "";
    }

    static int Mod(int a, int n) => n <= 0 ? 0 : ((a % n) + n) % n;

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
        return null;
    }

    // ── 씨앗 ────────────────────────────────────────────────────────────────

    int SeedOf()
    {
        var brain = GetComponent<UnitBrain>();
        if (brain != null && brain.Spec != null) return brain.Spec.Seed;

        // **안정 해시**(FNV-1a) — 이름이 같으면 판이 바뀌어도 같은 얼굴이다.
        string key = brain != null && !string.IsNullOrEmpty(brain.UnitName) ? brain.UnitName : name;
        unchecked
        {
            int h = (int)2166136261;
            foreach (char c in key) h = (h ^ c) * 16777619;
            return h;
        }
    }
}
