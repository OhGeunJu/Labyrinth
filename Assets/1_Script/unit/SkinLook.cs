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
public class SkinLook : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int SkinColor = Shader.PropertyToID("_SkinColor");

    MaterialPropertyBlock _mpb;
    bool  _has;
    Color _tone;

    public bool  HasTone => _has;
    public Color Tone    => _tone;

    void Start() => Apply();

    public void Apply()
    {
        if (!TryGetComponent(out UnitBrain brain) || brain.Data == null) return;
        var tones = brain.Data.SkinTones;
        if (tones == null || tones.Count == 0) return;

        int seed = TryGetComponent(out PawnLook look) ? look.Seed : name.GetHashCode();
        _tone = tones[Mod(seed / 17, tones.Count)];
        _tone.a = 1f;
        _has = true;

        _mpb ??= new MaterialPropertyBlock();
        var model = transform.Find("Model");
        if (model == null) return;

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

        if (TryGetComponent(out PawnApparel apparel)) apparel.Apply();
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
