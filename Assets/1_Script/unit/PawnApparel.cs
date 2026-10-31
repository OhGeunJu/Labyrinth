using System.Collections.Generic;
using UnityEngine;

// **찬 방어구가 몸에 보인다**(2026-10-23, 사용자: *"무기나 방어구 같은걸 착용하면 착용시키고 싶었던거야"*).
//
// 갑옷마다 모델을 붙이지 않고 **그 부위의 옷 칸을 갑옷 색으로 칠한다**(림월드의 옷이 그렇다 — 치비 몸에 판금 모델을
// 덧대면 몸이 부푼다). 부위와 칠하는 자리:
//   - 몸통 → 몸 메시의 셔츠 칸(팔까지 한 칸이라 소매가 같이 간다) · 다리 → 바지 칸
//   - 손 → 손 공 둘 · 발 → 신발 둘
//   - 머리 → 투구 껍데기(머리카락 대신 — `PawnLook.Helmeted`) · 허리 → 램프 알(`Hips.Lamp`)
// 색은 갑옷이 든다(`ArmorData.WornTint`). 무기는 `PawnProps`가 손에 쥔다.
//
// **해골은 칠할 옷이 없다 — 껍데기를 켠다**(2026-10-27). 뼈 몸(`ChibiPawnCreator.Bones`)에는 칸마다 껍데기(`관절.Armor.칸` —
// 흉갑 · 다리 · 장갑 · 장화)가 꺼진 채 붙어 있고, 찼으면 켜고 갑옷 색으로 칠한다. 덮은 자리의 뼈만 가려진다.
// **껍데기는 렌더러가 아니라 게임 오브젝트로 켠다** — 렌더러를 다 켜고 끄는 문이 따로 있다(시체를 파낼 때 `Corpse.SetBuried` ·
// 전용 모델을 입힐 때 `UnitBrain.ApplyLook`). 렌더러로 켜면 안 찬 껍데기가 파낸 시체에 다 돋거나, 전용 모델 위에 껍데기가 뜬다.
//
// **판정을 안 만든다** — 무엇을 찼나는 `Equipment`가 답하고 바뀌면 알린다(`OnEquipChanged`). 치비 몸이 아니면(골렘·짐승)
// 찾을 살이 없어 아무 일도 안 한다.
//
// **벗으면 재질 본래 색으로 칠한다** — `MaterialPropertyBlock`은 값 하나만 지울 수 없어서, 비우는 대신 재질의 색을 되써 둔다
// (통째로 `null`을 주면 다른 쪽이 얹은 값까지 날아간다 — 눈·입 판의 칸이 그렇다).
[DisallowMultipleComponent]
public class PawnApparel : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    // 몸 메시의 재질 칸 — `ChibiPawnCreator.BodyMesh`(0 셔츠 · 1 바지 · 2 바깥 선).
    const int ShirtSlot = 0, PantsSlot = 1;
    SkinLook _skin;

    Equipment           _equip;
    PawnLook            _look;
    SkinnedMeshRenderer _body;
    readonly List<Renderer> _hands = new(), _shoes = new();
    Renderer              _lamp;
    readonly List<(Renderer r, ArmorSlot slot)> _shells = new();
    MaterialPropertyBlock _mpb;
    bool                  _gathered;

    void Awake() => _mpb = new MaterialPropertyBlock();
    void OnEnable()  => Equipment.OnEquipChanged += OnChanged;
    void OnDisable() => Equipment.OnEquipChanged -= OnChanged;
    void Start() => Apply();

    void OnChanged(Equipment e)
    {
        if (e != null && e.gameObject == gameObject) Apply();
    }

    public void Apply()
    {
        if (!_gathered) Gather();
        if (_equip == null && !TryGetComponent(out _equip)) return;   // `UnitBrain.Awake`가 붙인다 — 없으면 맨몸

        Paint(_body, ShirtSlot, _equip.GetArmor(ArmorSlot.Chest));
        Paint(_body, PantsSlot, _equip.GetArmor(ArmorSlot.Legs));
        foreach (var r in _hands) Paint(r, -1, _equip.GetArmor(ArmorSlot.Hands));
        foreach (var r in _shoes) Paint(r, -1, _equip.GetArmor(ArmorSlot.Feet));

        foreach (var (r, slot) in _shells)
        {
            var armor = _equip.GetArmor(slot);
            r.gameObject.SetActive(armor != null);
            Paint(r, -1, armor);
        }

        var helmet = _equip.GetArmor(ArmorSlot.Head);
        if (_look != null)
        {
            _look.Helmeted = helmet != null;
            foreach (var r in _look.HelmetRenderers) Paint(r, -1, helmet);
        }

        var belt = _equip.GetArmor(ArmorSlot.Belt);
        if (_lamp != null)
        {
            _lamp.enabled = belt != null;
            Paint(_lamp, -1, belt);
        }
    }

    // `slot` < 0이면 렌더러 전체(손 공·신발은 살 재질 + 바깥 선 재질이고, 선 재질은 `_BaseColor`를 안 쓴다).
    void Paint(Renderer r, int slot, ArmorData armor)
    {
        if (r == null) return;
        var mats = r.sharedMaterials;
        var mat  = mats.Length == 0 ? null : mats[Mathf.Clamp(slot, 0, mats.Length - 1)];
        if (mat == null || !mat.HasProperty(BaseColor)) return;

        // 벗은 칸은 재질 색으로 — **살이면 그 개체의 살색으로**(`SkinLook`). 안 물으면 장비를 갈 때마다 맨몸이 프리팹 색으로 돌아간다.
        if (_skin == null) TryGetComponent(out _skin);
        var color = armor != null ? armor.WornTint
                  : _skin != null && _skin.TryTone(mat, out var tone) ? tone
                  : mat.GetColor(BaseColor);
        if (slot < 0) r.GetPropertyBlock(_mpb); else r.GetPropertyBlock(_mpb, slot);
        _mpb.SetColor(BaseColor, color);
        if (slot < 0) r.SetPropertyBlock(_mpb); else r.SetPropertyBlock(_mpb, slot);
    }

    // 이름으로 찾는다 — 생성기가 붙인 이름이다(`ChibiPawnCreator.DressSkinned`: `Body` · `HandL.Ball` · `FootL.Shoe` · `Hips.Lamp` ·
    // 해골의 `관절.Armor.칸`).
    void Gather()
    {
        _gathered = true;
        TryGetComponent(out _look);
        _hands.Clear(); _shoes.Clear(); _shells.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name;
            if (r is SkinnedMeshRenderer smr && n == "Body") _body = smr;
            else if (n.EndsWith(".Ball") && n.StartsWith("Hand")) _hands.Add(r);
            else if (n.EndsWith(".Shoe")) _shoes.Add(r);
            else if (n == "Hips.Lamp") _lamp = r;
            else
            {
                int at = n.IndexOf(".Armor.", System.StringComparison.Ordinal);
                if (at > 0 && System.Enum.TryParse(n.Substring(at + 7), out ArmorSlot slot)) _shells.Add((r, slot));
            }
        }
    }
}
