using System.Collections.Generic;
using UnityEngine;

// **해골 머리의 칠 — 뼈 색 · 눈구멍 속**(2026-10-27, 사용자: *"빠진 이 뼈 색"* · *"머리안을 검은 거 … 이렇게 선택할 수 있게"*).
//
// 메시를 갈아 끼우는 축(두개골 · 턱 · 이 · 빠진 이)은 `PawnLook`이 고르고, 여기는 **같은 메시에 칠만 달리하는 둘**이다.
// 씨앗도 `PawnLook.Seed` 하나 — 같은 해골은 늘 같은 색이다. 못 박으면 그것이 이긴다.
//
// - **뼈 색**은 `_BaseColor` · **눈구멍 속**은 `_VertexAO`(구운 그늘을 얼마나 곱하나 — 검정 1 · 뼈색 0.4)를 렌더러 블록에 적는다.
//   블록은 **읽고 덧쓴다** — 죽으면 `Corpse.Tint`가 `_BaseColor`만 덮고 그늘은 남는다.
// - 칠하는 살은 `PawnLook.PickRenderers`(해골 머리의 `Head.Pick.*`)와 **몸의 뼈**(`관절.Bone.*` — 2026-10-27)다. 몸에는 색만 적는다
//   (눈구멍 속은 머리의 일이다). 투구 · 방어구 껍데기는 `PawnApparel`이 칠한다.
// - **색은 고를 수도 · 직접 넣을 수도 있다**(사용자: *"스켈레톤 뼈 색도 변경 가능하게"*) — `Custom`이면 `customColor`.
public class BoneLook : MonoBehaviour
{
    public enum Tint   { Seeded = -1, Ivory, Pale, Dark, Custom }
    public enum Hollow { Seeded = -1, Black, Bone }

    [Tooltip("뼈 색. 비우면(Seeded) 씨앗이 고른다.")]
    [SerializeField] Tint tint = Tint.Seeded;
    [Tooltip("뼈 색을 직접 — 위가 Custom일 때만 쓴다.")]
    [SerializeField] Color customColor = new Color32(0xE2, 0xD9, 0xC2, 0xFF);
    [Tooltip("눈구멍·콧구멍 속 — 검정(그늘을 다) · 뼈색(그늘을 조금). 비우면 씨앗이 고른다.")]
    [SerializeField] Hollow hollow = Hollow.Seeded;

    static readonly Color[] Tints =
    {
        new Color32(0xE2, 0xD9, 0xC2, 0xFF),   // 상아 — 해골 옷 벌의 색
        new Color32(0xF0, 0xEC, 0xE2, 0xFF),   // 하얀
        new Color32(0xB4, 0xA4, 0x84, 0xFF),   // 거무스름한(묵은)
    };
    static readonly float[] HollowAO = { 1f, 0.4f };

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int VertexAO  = Shader.PropertyToID("_VertexAO");

    MaterialPropertyBlock _mpb;
    readonly List<Renderer> _bones = new();
    bool _gathered;

    public Tint   TintNow   { get; private set; }
    public Hollow HollowNow { get; private set; }

    void Start() => Apply();

    void OnValidate() { if (isActiveAndEnabled) Apply(); }

    public void Apply()
    {
        if (!TryGetComponent(out PawnLook look)) return;
        int seed = look.Seed;
        TintNow   = tint   != Tint.Seeded   ? tint   : (Tint)Mod(seed / 11, Tints.Length);
        HollowNow = hollow != Hollow.Seeded ? hollow : (Hollow)Mod(seed / 13, HollowAO.Length);
        var color = TintNow == Tint.Custom ? customColor : Tints[(int)TintNow];

        _mpb ??= new MaterialPropertyBlock();
        foreach (var r in look.PickRenderers)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, color);
            _mpb.SetFloat(VertexAO, HollowAO[(int)HollowNow]);
            r.SetPropertyBlock(_mpb);
        }
        if (!_gathered) Gather();
        foreach (var r in _bones)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, color);
            r.SetPropertyBlock(_mpb);
        }
    }

    // 몸의 뼈 — 생성기가 붙인 이름 `관절.Bone.무엇`(`ChibiPawnCreator.Bones`)이고 루트의 `Model` 밑에 있다. 손에 든 소품은 안 든다.
    void Gather()
    {
        _gathered = true;
        _bones.Clear();
        var model = transform.Find("Model");
        if (model == null) return;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var parts = r.name.Split('.');
            if (parts.Length == 3 && parts[1] == "Bone") _bones.Add(r);
        }
    }

    static int Mod(int a, int n) => ((a % n) + n) % n;
}
