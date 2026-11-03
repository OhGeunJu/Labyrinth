using System.Collections.Generic;
using UnityEngine;

// **문신 색 · 머리카락 색 — 모양과 따로 고른다**(2026-11-01, 사용자: *"색은 따로 색 정하는 칸이 있어서 색따로 모양따로 면 좋겠어"*).
//
// 모양(어느 문신 겹 · 어느 머리 · 어느 수염)은 `PawnLook`이 고르고(`Head.Pick.Tat*` · `Head.Pick.Hair.*` · `Head.Pick.Beard.*`), 여기는
// **같은 메시에 칠만** 한다 — `BoneLook`과 같은 길이다. 씨앗도 `PawnLook.Seed` 하나라 같은 개체는 늘 같은 색이다. 못 박으면 그것이 이긴다.
//
// - **지니나 · 무슨 색은 종족(부족)이 정한다**(`UnitData.TattooChance` · `InkTones` · `LeaderTattoo` · `LeaderInk` — 사용자: *"일반 오크는
//   초록색에 노 타투 or 대장만 빨간 타투"*). 대장은 이끄는 이력(`BackstoryData.Leads`)을 받은 개체다 — 습격대가 두목을 세우면 다시 칠한다.
//   문신이 없으면 겹 축(`Tat*`)을 다 끄고, 있으면 씨앗의 겹을 두되 하나는 켠다(`PawnLook.ForcePick` — 판에서만 · 못 박은 것이 이긴다).
// - 칠하는 살은 이름으로 가린다 — 문신 겹은 먹 색, 머리카락 · 수염 · 짧은 머리(`.Short`)는 머리카락 색, 반삭(`.Buzz`)은 살과 머리색 사이(뼈 고리 · 가죽 · 쇠는 안 칠한다).
// - 렌더러 블록은 **읽고 덧쓴다** — 죽으면 `Corpse.Tint`가 덮는다.
// - **씬 · 프리팹에서도 보인다**(2026-11-01, 사용자: *"씬에 배치에서도 색이 보이게"*) — 블록은 저장되지 않으므로 에디터에서는
//   켜질 때마다 다시 칠한다(`SkinLook`과 같은 길 · `ExecuteAlways`). 그때는 **칠만** 한다 — 겹을 켜고 끄면 렌더러의 켜짐이 씬 ·
//   프리팹에 저장돼 버린다. 인스펙터로 값을 바꿀 때(`OnValidate`)는 전처럼 겹까지 정한다.
[ExecuteAlways]
public class MarkingLook : MonoBehaviour
{
    public enum Ink  { Seeded = -1, Black, Red, Blue, Bone, Custom }
    public enum Tattoo { Seeded = -1, None, Always }

    [Tooltip("문신을 지니나. 비우면(Seeded) 종족의 규칙(대장 · 확률)이 정한다.")]
    [SerializeField] Tattoo tattoo = Tattoo.Seeded;
    public enum HairTone { Seeded = -1, Black, DarkBrown, Grey, Custom }

    [Tooltip("문신 색. 비우면(Seeded) 씨앗이 고른다.")]
    [SerializeField] Ink ink = Ink.Seeded;
    [Tooltip("문신 색을 직접 — 위가 Custom일 때만 쓴다.")]
    [SerializeField] Color customInk = new Color32(0x17, 0x17, 0x1C, 0xFF);
    [Tooltip("머리카락 · 수염 색. 비우면(Seeded) 씨앗이 고른다.")]
    [SerializeField] HairTone hair = HairTone.Seeded;
    [Tooltip("머리카락 색을 직접 — 위가 Custom일 때만 쓴다.")]
    [SerializeField] Color customHair = new Color32(0x22, 0x1E, 0x22, 0xFF);

    static readonly Color[] Inks =
    {
        new Color32(0x17, 0x17, 0x1C, 0xFF),   // 검정
        new Color32(0x9A, 0x1F, 0x1A, 0xFF),   // 빨강
        new Color32(0x29, 0x47, 0x94, 0xFF),   // 파랑
        new Color32(0xDB, 0xD6, 0xC2, 0xFF),   // 뼈색(흰 칠)
    };
    static readonly Color[] Hairs =
    {
        new Color32(0x22, 0x1E, 0x24, 0xFF),   // 검정
        new Color32(0x3E, 0x2A, 0x1E, 0xFF),   // 짙은 갈색
        new Color32(0x6E, 0x6A, 0x66, 0xFF),   // 잿빛
    };

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock _mpb;
    readonly List<Renderer> _ink = new(), _hair = new(), _buzz = new();
    bool _gathered;

    public Ink      InkNow  { get; private set; }
    public bool     Inked   { get; private set; }
    public HairTone HairNow { get; private set; }

    // 판에서는 `Start` — 명세(씨앗)가 선 뒤에. 에디터에서는 켜질 때마다 칠만(다시 구운 몸일 수 있으니 목록도 다시 모은다).
    void OnEnable() { if (!Application.isPlaying) { _gathered = false; Apply(gate: false); } }
    void Start()    { if (Application.isPlaying) Apply(); }

    // **다음 틱으로 미룬다** — 겹을 정하면 렌더러를 켜고 끄는데(`PawnLook.ApplyPicks`), `OnValidate` 안에서는 유니티가 그것을 막는다
    // ("SendMessage cannot be called during … OnValidate" — 2026-11-03 여성 오크 프리팹을 구운 뒤 봤다).
    void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Apply(); };
#endif
    }

    public void Apply() => Apply(gate: true);

    // `gate` — 문신 겹을 켜고 끄기까지 하나(거짓이면 칠만).
    void Apply(bool gate)
    {
        if (!TryGetComponent(out PawnLook look)) return;
        int seed = look.Seed;
        var data = TryGetComponent(out UnitBrain brain) ? brain.Data : null;
        bool leader = brain != null && brain.Adulthood != null && brain.Adulthood.Leads;

        // 지니나 — 못 박은 것 > 종족의 규칙(대장 · 확률). 종족을 모르면(프리팹을 열었을 때) 씨앗의 겹 그대로.
        Inked = tattoo == Tattoo.Always || (tattoo == Tattoo.Seeded &&
                (data == null || (leader && data.LeaderTattoo) || Mod(seed / 29, 1000) < data.TattooChance * 1000f));
        if (gate) GateLayers(look, tattoo != Tattoo.Seeded || data != null);

        // 무슨 색 — 못 박은 것 > 대장 색 > 종족 후보 > 기본 넷.
        InkNow  = ink  != Ink.Seeded      ? ink  : (Ink)Mod(seed / 19, Inks.Length);
        HairNow = hair != HairTone.Seeded ? hair : (HairTone)Mod(seed / 23, Hairs.Length);
        var inkColor = InkNow == Ink.Custom ? customInk : Inks[(int)InkNow];
        if (ink == Ink.Seeded && data != null)
        {
            if (leader && data.LeaderInk.a > 0f) inkColor = data.LeaderInk;
            else if (data.InkTones != null && data.InkTones.Count > 0) inkColor = data.InkTones[Mod(seed / 19, data.InkTones.Count)];
            inkColor.a = 1f;
        }
        var hairColor = HairNow == HairTone.Custom ? customHair : Hairs[(int)HairNow];

        if (!_gathered) Gather();
        _mpb ??= new MaterialPropertyBlock();
        Paint(_ink, inkColor);
        Paint(_hair, hairColor);
        // 반삭(여성 오크 — `….Buzz`) — 살과 머리색 사이(살 0.55 · 머리 0.45). 살은 개체 살색이 있으면 그것, 없으면 머리 살 재질의 색.
        if (_buzz.Count > 0) Paint(_buzz, Color.Lerp(SkinNow(model: transform.Find("Model")), hairColor, 0.45f));
    }

    Color SkinNow(Transform model)
    {
        if (TryGetComponent(out SkinLook skin) && skin.HasTone) return skin.Tone;
        if (model != null)
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("Head.Shape.") && r.name.Split('.').Length == 3 && r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColor))
                    return r.sharedMaterial.GetColor(BaseColor);
        return Color.gray;
    }

    // 문신 겹 축(`Tat*` — On/Off)을 정한다. 안 지니면 다 끄고, 지니면 씨앗의 겹을 두되 다 꺼졌으면 하나(씨앗이 고른 것)를 켠다.
    void GateLayers(PawnLook look, bool ruled)
    {
        var axes = new List<string>();
        if (!look.PickAxes.GetEnumerator().MoveNext()) look.Apply();   // 아직 안 모았으면 모은다(`PawnLook.Start`보다 먼저 돌 수 있다)
        foreach (var a in look.PickAxes) if (a.StartsWith("Tat")) axes.Add(a);
        if (axes.Count == 0) return;
        foreach (var a in axes) look.ForcePick(a, ruled && !Inked ? "Off" : null);
        if (ruled && Inked)
        {
            bool any = false;
            foreach (var a in axes) any |= look.SeededPick(a) == "On";
            if (!any) look.ForcePick(axes[Mod(look.Seed / 31, axes.Count)], "On");
        }
        look.Apply();
    }

    void Paint(List<Renderer> list, Color c)
    {
        foreach (var r in list)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    // 머리 밑의 고르는 살 — 이름 `Head.Pick.{축}.{벌}[.조각][@조건]`(`ChibiPawnCreator.Orc`).
    void Gather()
    {
        _gathered = true;
        _ink.Clear(); _hair.Clear(); _buzz.Clear();
        var model = transform.Find("Model");
        if (model == null) return;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var parts = r.name.Split('@')[0].Split('.');
            if (parts.Length < 4 || parts[0] != "Head" || parts[1] != "Pick") continue;
            if (parts[2].StartsWith("Tat")) _ink.Add(r);
            else if (parts[2] == PawnLook.PickHair || parts[2] == "Beard" || parts[2] == "Brow")   // 눈썹(드워프 — `Head.Pick.Brow.*`)도 털 색
            {
                // 머리카락 덩어리(조각 이름 없음) · 짧은 머리(`.Short`)는 머리색 · 반삭(`.Buzz`)은 살과 섞은 색 · 뼈 고리 · 가죽 · 쇠(`.Cuff` · `.Leather` · `.Metal` · `.Ring`)는 안 칠한다.
                string piece = parts.Length > 4 ? parts[4] : null;
                if (piece == null || piece == "Short") _hair.Add(r);
                else if (piece == "Buzz") _buzz.Add(r);
            }
        }
    }

    static int Mod(int a, int n) => ((a % n) + n) % n;
}
