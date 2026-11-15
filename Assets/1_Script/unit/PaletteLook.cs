using System;
using System.Collections.Generic;
using UnityEngine;

// **팔레트 — 색만 다른 변형을 프리팹으로 나누지 않는다**(2026-11-14, 사용자: *"색만 달라 보이는데 왜 프리팹으로 나뉘어"* →
// *"잎 메시를 한두 종류 더 만들어서 색만 돌리면"*). 모양은 고르기 축(`PawnLook`), 색은 여기서 개체마다 팔레트 하나를 고른다.
//
// - 메시의 정점은 **칸마다 몫**만 든다 — 셰이더가 `Σ 몫 × 칸 색`으로 칠한다(`ToonOutline._VertexPal` · 칸 `_Pal0~7`).
// - 팔레트 = (재질 이름 앞머리, 셰이더 값 이름, 색) 목록. 그 재질이 든 칸에만 렌더러 블록으로 칠한다 — 블록은 **읽고 덧쓴다**
//   (`SkinLook`이 같은 칸에 살색을 칠한다). 재질 이름은 앞머리로 맞춘다(`PawnOutlineId`의 사본은 이름 뒤에 `(폰 N)`이 붙는다).
// - 고르는 씨앗은 `PawnLook.Seed`(같은 개체는 늘 같은 색) · 못 박으려면 `paletteOverride`(인스펙터 — 바꾸면 바로 칠한다).
// - **에디터(프리팹 · 씬)에서는 못 박은 것만 칠한다**(`SkinLook`과 같다 — 블록은 저장되지 않으니 켜질 때마다 다시). 안 박았으면 재질 값(첫 팔레트) 그대로.
//   팔레트 목록의 색을 인스펙터에서 고쳐도 바로 다시 칠한다(색 맞추기).
// - **반점 배치는 개체마다 한 벌**(11-14 사용자: *"색마다 점 위치가 바뀌면"* → *"개체마다"* → *"8개 정도로 정해 놓고 돌려쓰면"*) — 생성기가 배치 여럿을
//   메시에 구워 두고(셋째 · 넷째 UV) 여기서 씨앗으로 하나를 고른다(`spotLayouts` · 못 박기 `spotOverride`). 셰이더는 고른 칸만 읽는다(`_SpotPickA/B`).
// - **섞여 나온다** — 한 무리 안에서도 개체마다 다르다(사용자: *"일단 섞을 수 있게 해놔 — 세력 제대로 세울 때 고민하자"*).
// - **팔레트에 딸린 모양** — 조각 이름의 조건 `@Palette.{이름}`이면 그 팔레트일 때만 켜진다(`PawnLook.PaletteAxis` — 리자드맨: 누런 구렁이의
//   비늘 · 매끈한 뱀의 색마다 다른 줄 덧판). 그래서 고른 이름은 칠하지 않고도 답한다(`Chosen`) · 바꾸면 `PawnLook`을 다시 돌린다.
[ExecuteAlways]
public class PaletteLook : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public string Material;   // 재질 이름 앞머리
        public string Property;   // 셰이더 값(`_Pal0` · `_AltColor` …)
        public Color  Color;
        public bool   Raw;        // 색이 아니라 수 — 선형 공간으로 안 바꾼다(`SetVector`)
    }

    [Serializable]
    public class Palette
    {
        public string     Name;
        public List<Slot> Slots = new();
    }

    [SerializeField] List<Palette> palettes = new();
    [Tooltip("팔레트를 못 박는다 — 번호. -1이면 씨앗이 고른다.")]
    [SerializeField] int paletteOverride = -1;

    [Tooltip("반점 배치를 칠할 재질 이름 앞머리(비우면 반점 없음).")]
    [SerializeField] string spotMaterial = "";
    [Tooltip("구워 둔 반점 배치의 수(0이면 없음 · 최대 8).")]
    [SerializeField] int spotLayouts = 0;
    [Tooltip("반점 배치를 못 박는다 — 번호(0부터). -1이면 씨앗이 고른다.")]
    [SerializeField] int spotOverride = -1;

    MaterialPropertyBlock _mpb;

    public int SpotLayouts => spotLayouts;
    public int SpotIndex { get; private set; } = -1;
    public int SpotOverride { get => spotOverride; set { spotOverride = value; Apply(); } }
    public void SetSpots(string material, int layouts) { spotMaterial = material ?? ""; spotLayouts = Mathf.Clamp(layouts, 0, 8); }

    // 반점 배치 번호 — 못 박았으면 그것 · 아니면 씨앗(팔레트와 다른 열 — 같은 색끼리도 갈린다). 에디터에서도 씨앗(이름)으로 — 씬에서 갈려 보이게.
    int ChooseSpot()
    {
        if (spotLayouts <= 0) return -1;
        if (spotOverride >= 0 && spotOverride < spotLayouts) return spotOverride;
        int seed = TryGetComponent(out PawnLook look) ? look.Seed : name.GetHashCode();
        unchecked { uint h = (uint)seed * 2654435761u; h ^= h >> 15; return (int)(h % (uint)spotLayouts); }
    }

    public IReadOnlyList<Palette> Palettes => palettes;
    public int Index { get; private set; } = -1;
    public string Current => Index >= 0 && Index < palettes.Count ? palettes[Index].Name : null;

    public int PaletteOverride
    {
        get => paletteOverride;
        set { paletteOverride = value; Apply(); if (TryGetComponent(out PawnLook look)) look.Apply(); }
    }

    // 고를 번호 — 못 박았으면 그것 · 판에서는 씨앗 · 에디터에서 안 박았으면 첫 팔레트(재질 값과 같다).
    int ChooseIndex()
    {
        if (palettes.Count == 0) return -1;
        if (paletteOverride >= 0 && paletteOverride < palettes.Count) return paletteOverride;
        if (!Application.isPlaying) return 0;
        int seed = TryGetComponent(out PawnLook look) ? look.Seed : name.GetHashCode();
        return Mod(seed * 31 + 7919, palettes.Count);
    }
    // 고른 팔레트의 이름 — 칠하기 전에도(`PawnLook`이 조각을 켤 때 묻는다).
    public string Chosen { get { int i = ChooseIndex(); return i >= 0 ? palettes[i].Name : null; } }

    public void SetPalettes(IEnumerable<Palette> list) { palettes.Clear(); if (list != null) palettes.AddRange(list); }

    void OnEnable() { if (!Application.isPlaying) Apply(); }
    void Start()    { if (Application.isPlaying)  Apply(); }
#if UNITY_EDITOR
    // 인스펙터에서 번호 · 색을 바꾸면 바로 — `OnValidate` 안에서는 렌더러를 못 만지는 때가 있어 한 박자 미룬다.
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            Apply();
            if (TryGetComponent(out PawnLook look)) look.Apply();   // 팔레트에 딸린 조각(`@Palette.`)
        };
    }
#endif

    public void Apply()
    {
        if (palettes.Count == 0) return;
        // 에디터에서 못 박지 않았으면 첫 팔레트(재질 값과 같다) — 전에 칠한 블록이 남아 있을 수 있어 그것으로 되쓴다.
        Index = ChooseIndex();
        var pal = palettes[Index];

        _mpb ??= new MaterialPropertyBlock();
        SpotIndex = ChooseSpot();
        Vector4 pickA = Vector4.zero, pickB = Vector4.zero;
        if (SpotIndex >= 0) { if (SpotIndex < 4) pickA[SpotIndex] = 1f; else pickB[SpotIndex - 4] = 1f; }
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                bool any = false;
                foreach (var s in pal.Slots)
                {
                    if (string.IsNullOrEmpty(s.Material) || !m.name.StartsWith(s.Material, StringComparison.Ordinal)) continue;
                    if (!any) { r.GetPropertyBlock(_mpb, i); any = true; }
                    if (s.Raw) _mpb.SetVector(s.Property, new Vector4(s.Color.r, s.Color.g, s.Color.b, s.Color.a));
                    else       _mpb.SetColor(s.Property, s.Color);
                }
                if (SpotIndex >= 0 && spotMaterial.Length > 0 && m.name.StartsWith(spotMaterial, StringComparison.Ordinal))
                {
                    if (!any) { r.GetPropertyBlock(_mpb, i); any = true; }
                    _mpb.SetVector("_SpotPickA", pickA); _mpb.SetVector("_SpotPickB", pickB);
                }
                if (any) r.SetPropertyBlock(_mpb, i);
            }
        }
    }

    static int Mod(int a, int n) => ((a % n) + n) % n;
}
