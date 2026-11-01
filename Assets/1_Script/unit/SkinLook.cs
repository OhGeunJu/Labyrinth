using System.Collections.Generic;
using UnityEngine;

// **살색 — 개체마다 종족의 후보에서 하나**(2026-10-28, 사용자: *"오우거는 색이 여러개야 · 트롤이 파란색이랑 회색쪽만"*).
//
// 후보는 종족이 든다(`UnitData.SkinTones`). 고르는 씨앗은 `PawnLook.Seed` 하나 — 같은 개체는 늘 같은 색이다(`BoneLook`과 같다).
// 재질은 프리팹 한 벌이라(`ChibiPawnCreator.OutfitMaterials`) 렌더러 블록에 칠한다 — 블록은 **읽고 덧쓴다**.
//
// - **살은 재질 이름으로 가린다** — 생성기가 붙인 `Chibi_Skin*`·`Chibi_HeadSkin*`(살 · 고운 살 · 바깥 선 살 · 머리 살). 맨몸 종족은
//   셔츠·바지·신발 칸에도 살 재질을 물려 두었으므로(`Outfit.Bare`) 같이 칠해진다.
// - **눈·입 판은 살색을 섞는다**(`_SkinColor`) — 안 바꾸면 푸른 얼굴에 프리팹 살색 눈두덩이 뜬다.
// - **옷이 이긴다** — 다 칠한 뒤 `PawnApparel`을 다시 부른다. 벗은 칸이 돌아갈 색은 여기가 답한다(`TryTone`).
//
// **못 박을 수 있다**(2026-11-01, 사용자: *"그 색을 고르는게 없고 프리팹을 보면 … 일반 오거랑 똑같이 보여"*) —
// `toneOverride`는 후보의 번호(-1이면 씨앗). 인스펙터는 `SkinLookEditor`.
// **에디터(프리팹 · 씬)에서는 프리팹의 기본 색이다**(같은 날, 사용자: *"프리팹마다 기본색이 다른 걸 원하는 건데"*) — 기본 색은 프리팹마다
// 따로 든 살 재질이 든다(`ChibiPawnCreator`의 `Brutes` · `Outfits` — `Chibi_Skin*_{프리팹}`). 씨앗 색은 판에서만 덧칠하고, 에디터에서는
// 못 박은 것만 칠한다(`ExecuteAlways` — 블록은 저장되지 않으니 켜질 때마다 다시). 에디터에서는 살만 — 옷(`PawnApparel`)은 판의 것이다.
[ExecuteAlways]
public class SkinLook : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int SkinColor = Shader.PropertyToID("_SkinColor");

    [Tooltip("살색을 못 박는다 — 종족 후보(`UnitData.SkinTones`)의 번호. -1이면 씨앗이 고른다.")]
    [SerializeField] int toneOverride = -1;

    MaterialPropertyBlock _mpb;
    bool  _has;
    Color _tone;

    public bool  HasTone   => _has;
    public Color Tone      => _tone;
    public int   ToneIndex { get; private set; } = -1;

    public int ToneOverride { get => toneOverride; set { toneOverride = value; Apply(); } }

    // 종족의 후보 — 인스펙터가 읽는다.
    public IReadOnlyList<Color> Tones => TryGetComponent(out UnitBrain brain) && brain.Data != null ? brain.Data.SkinTones : null;

    // 판에서는 `Start` — 명세(씨앗)가 선 뒤에 고른다. 에디터에서는 켜질 때마다(프리팹을 열거나 씬을 불러올 때).
    void OnEnable() { if (!Application.isPlaying) Apply(); }
    void Start()    { if (Application.isPlaying)  Apply(); }

    public void Apply()
    {
        var tones = Tones;
        if (tones == null || tones.Count == 0) return;

        int seed = TryGetComponent(out PawnLook look) ? look.Seed : name.GetHashCode();
        bool pinned = toneOverride >= 0 && toneOverride < tones.Count;
        ToneIndex = pinned ? toneOverride : Mod(seed / 17, tones.Count);
        _tone = tones[ToneIndex];
        _tone.a = 1f;
        _has = true;

        _mpb ??= new MaterialPropertyBlock();
        var model = transform.Find("Model");
        if (model == null) return;

        // 에디터에서 못 박지 않았으면 프리팹의 기본 색 — 전에 칠해 둔 블록이 남아 있을 수 있으니 재질 색으로 되써 둔다.
        if (!Application.isPlaying && !pinned)
        {
            ToneIndex = -1;
            _has = false;
            Restore(model);
            return;
        }

        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            if (r is SkinnedMeshRenderer)
            {
                // 몸은 칸마다 다른 재질 — 살인 칸에만.
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!IsSkin(mats[i])) continue;
                    r.GetPropertyBlock(_mpb, i);
                    _mpb.SetColor(BaseColor, _tone);
                    r.SetPropertyBlock(_mpb, i);
                }
                continue;
            }

            bool skin = false, face = false;
            foreach (var m in mats) { skin |= IsSkin(m); face |= IsFace(m); }
            if (!skin && !face) continue;

            r.GetPropertyBlock(_mpb);
            if (skin) _mpb.SetColor(BaseColor, _tone);
            if (face) _mpb.SetColor(SkinColor, _tone);
            r.SetPropertyBlock(_mpb);
        }

        if (Application.isPlaying && TryGetComponent(out PawnApparel apparel)) apparel.Apply();
    }

    // 살 · 눈입 판의 블록 값을 재질 본래 색으로 되쓴다(블록은 값 하나만 지울 수 없다 — `PawnApparel`과 같다).
    void Restore(Transform model)
    {
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            if (r is SkinnedMeshRenderer)
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!IsSkin(mats[i]) || !mats[i].HasProperty(BaseColor)) continue;
                    r.GetPropertyBlock(_mpb, i);
                    _mpb.SetColor(BaseColor, mats[i].GetColor(BaseColor));
                    r.SetPropertyBlock(_mpb, i);
                }
                continue;
            }

            Material skin = null, face = null;
            foreach (var m in mats) { if (skin == null && IsSkin(m)) skin = m; if (face == null && IsFace(m)) face = m; }
            if (skin == null && face == null) continue;

            r.GetPropertyBlock(_mpb);
            if (skin != null && skin.HasProperty(BaseColor)) _mpb.SetColor(BaseColor, skin.GetColor(BaseColor));
            if (face != null && face.HasProperty(SkinColor)) _mpb.SetColor(SkinColor, face.GetColor(SkinColor));
            r.SetPropertyBlock(_mpb);
        }
    }

    // 벗은 칸이 돌아갈 색 — 살 재질이면 이 개체의 살색(`PawnApparel`이 묻는다).
    public bool TryTone(Material m, out Color c)
    {
        c = _tone;
        return _has && IsSkin(m);
    }

    static bool IsSkin(Material m) =>
        m != null && (m.name.StartsWith("Chibi_Skin") || m.name.StartsWith("Chibi_HeadSkin"));

    static bool IsFace(Material m) =>
        m != null && (m.name.StartsWith("Chibi_FaceEyes") || m.name.StartsWith("Chibi_FaceMouths"));

    static int Mod(int a, int n) => ((a % n) + n) % n;
}
