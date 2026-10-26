using UnityEngine;

// **해골 머리의 칠 — 뼈 색 · 눈구멍 속**(2026-10-27, 사용자: *"빠진 이 뼈 색"* · *"머리안을 검은 거 … 이렇게 선택할 수 있게"*).
//
// 메시를 갈아 끼우는 축(두개골 · 턱 · 이 · 빠진 이)은 `PawnLook`이 고르고, 여기는 **같은 메시에 칠만 달리하는 둘**이다.
// 씨앗도 `PawnLook.Seed` 하나 — 같은 해골은 늘 같은 색이다. 못 박으면 그것이 이긴다.
//
// - **뼈 색**은 `_BaseColor` · **눈구멍 속**은 `_VertexAO`(구운 그늘을 얼마나 곱하나 — 검정 1 · 뼈색 0.4)를 렌더러 블록에 적는다.
//   블록은 **읽고 덧쓴다** — 죽으면 `Corpse.Tint`가 `_BaseColor`만 덮고 그늘은 남는다.
// - 칠하는 살은 `PawnLook.PickRenderers`(해골 머리의 `Head.Pick.*`)뿐이다 — 투구는 `PawnApparel`이 칠한다.
public class BoneLook : MonoBehaviour
{
    public enum Tint   { Seeded = -1, Ivory, Pale, Dark }
    public enum Hollow { Seeded = -1, Black, Bone }

    [Tooltip("뼈 색. 비우면(Seeded) 씨앗이 고른다.")]
    [SerializeField] Tint tint = Tint.Seeded;
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

        _mpb ??= new MaterialPropertyBlock();
        foreach (var r in look.PickRenderers)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, Tints[(int)TintNow]);
            _mpb.SetFloat(VertexAO, HollowAO[(int)HollowNow]);
            r.SetPropertyBlock(_mpb);
        }
    }

    static int Mod(int a, int n) => ((a % n) + n) % n;
}
