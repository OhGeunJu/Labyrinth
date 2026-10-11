using UnityEngine;

// **벽에 박힌 조명 — 야광석 · 폐광 횃불**(2026-09-28, 사용자: *"벽에 야명주(뭔가 동굴을 빛낼 수 있는
// 것들)을 자연 스폰되게 … 지금은 너무 어둡긴하잖아"* · *"갱도는 횃불 같은 걸 넣어도 되고"*).
//
// ## 광맥과 같은 자리에 산다
//
// 어디에 박혔는지는 **좌표 해시**가 정하고(`RockMap.WallLightAt`) 저장하는 것이 없다 — 광맥
// (`RockMap.VeinAt`)과 같은 방식이다. **드러난 벽면에만 선다**: 안 판 바위 속은 보이지 않고, 파
// 들어가면 새 벽면이 드러나면서 거기 박혀 있던 것이 나타난다(*판 만큼 드러난다*).
//
// **세우고 치우는 것은 `RockRenderer`다** — 바위 청크가 설 때 같이 서고 내려갈 때 같이 내려간다.
// 따로 레지스트리를 들면 벽을 판 뒤에도 허공에 남는 조명이 생긴다(`WallTorch`가 벽을 다시 재는
// 이유와 같은 사고).
//
// ## 불빛은 `LightSource`다
//
// 프리팹에 `LightSource`(`alwaysLit`)를 단다 — 화면이 밝아지는 만큼 **폰도 실제로 더 본다**
// (`Vision`). 장식 `Light`만 달면 *"밝아 보이는데 안 보인다"*가 된다(발광 버섯과 같은 규칙).
//
// ## 어느 바이옴에 얼마나 — 바이옴이 든다
//
// `BiomeContentData.WallLights`가 그 표다(**사용자가 정하는 값** — 처음 배선한 초안 도구 `World → K`는 다 쓰고 2026-09-30에 지웠다).
[CreateAssetMenu(menuName = "DungeonColony/WallLightData")]
public class WallLightData : ScriptableObject
{
    [Tooltip("살펴보기 창에 뜰 이름.")]
    public string DisplayName = "야광석";

    // **피벗은 벽면 위, 앞(+Z)은 벽 밖을 본다.** 그래야 면이 어느 쪽이든 회전 하나로 붙는다.
    [Tooltip("벽에 붙일 프리팹. 피벗 = 벽면, 앞(+Z) = 벽 바깥. `LightSource`를 달 것.")]
    public GameObject Prefab;

    [Header("캐면 나오는 것")]
    [Tooltip("그 벽을 파면 덤으로 나오는 것. 비우면 아무것도 안 나온다(횃불).")]
    public ItemData DropItem;
    [Min(0)] public int DropMin = 1;
    [Min(0)] public int DropMax = 2;

    [Header("자리")]
    [Tooltip("벽 앞 바닥에서 잰 높이(m). 이 사이에서 칸마다 굴린다.")]
    public float HeightMin = 0.7f;
    public float HeightMax = 1.8f;

    [Tooltip("벽면을 따라 좌우로 흔드는 폭(m). 0이면 면 한가운데.")]
    [Range(0f, 0.45f)] public float Jitter = 0.25f;

    // ── 하나하나 다르게 (2026-09-28, 사용자: *"너무 똑같은게 배치되어 있는 느낌이야"*) ──────────
    //
    // 처음에는 결정 모형 하나를 같은 크기·같은 각도로 찍었다 — 벽에 **도장**을 찍은 것처럼 보였다.
    // 자연물은 모양·크기·기울기·빛깔이 **조금씩 다 다르다.** 전부 칸 해시에서 굴리므로 다시 와도 같은
    // 자리에 같은 모양이 선다(저장할 것이 없다).
    //
    // **값을 비우면 예전과 같다**(변형 없음 · 배율 1 · 각 0) — 횃불은 그대로 둔다: 사람이 건 물건은
    // 똑같은 것이 맞다.
    [Header("하나하나 다르게")]
    [Tooltip("모양 후보. 칸마다 하나를 고른다. 비우면 Prefab 하나만 쓴다.")]
    public GameObject[] Variants = System.Array.Empty<GameObject>();

    [Tooltip("크기 배율 범위.")]
    public Vector2 ScaleRange = Vector2.one;

    [Tooltip("벽 바깥 축으로 도는 각(±도). 180이면 아무 쪽으로나 기운다.")]
    [Range(0f, 180f)] public float Roll = 0f;

    [Tooltip("벽에서 비껴 솟는 각(±도). 전부 벽에 똑바로 박히면 못처럼 보인다.")]
    [Range(0f, 45f)] public float Tilt = 0f;

    [Tooltip("빛깔을 흔드는 폭(색상환 비율). 0.05면 푸른빛이 청록~남색 사이로 갈린다. 결정과 등이 같이 흔들린다.")]
    [Range(0f, 0.2f)] public float HueJitter = 0f;

    // ── 무리 (2026-09-28, 사용자: 무리 짓기) ─────────────────────────────────
    //
    // 결정은 하나씩 나지 않는다 — 한 자리에서 큰 것 하나 곁에 작은 것 몇이 같이 솟는다. **곁가지는 모양뿐이다**
    // (각 프리팹의 `[Art]`만 떠 온다): 불빛은 무리에 하나라 결정이 늘어도 등 수는 안 는다(`LightSource`·`Light`가
    // 칸마다 여럿이면 그 값이 곱으로 붙는다).
    [Header("무리")]
    [Tooltip("한 자리에 몇 개가 같이 솟나(첫 것 포함). 1이면 하나만.")]
    [Min(1)] public int ClusterMin = 1;
    [Min(1)] public int ClusterMax = 1;

    [Tooltip("곁가지가 첫 것에서 퍼지는 폭(m, 벽면 위에서).")]
    [Range(0f, 0.6f)] public float ClusterSpread = 0.35f;

    [Tooltip("곁가지 크기 — 첫 것 대비.")]
    public Vector2 MemberScale = new(0.45f, 0.85f);

    // 곁가지를 붙인다 — `Dress` **전에** 부를 것(빛깔·크기가 무리 전체에 같이 입혀져야 한 무더기로 읽힌다).
    public void AddMembers(GameObject go, Vector2Int tile)
    {
        int n = ClusterMin + (int)(RockMap.Hash01(tile, 0x2545F491u) * (ClusterMax - ClusterMin + 1));
        n = Mathf.Clamp(n, ClusterMin, Mathf.Max(ClusterMin, ClusterMax));

        for (int i = 1; i < n; i++)
        {
            // 곁가지마다 다른 칸처럼 굴린다 — 모양·각이 첫 것과 겹치지 않게.
            var key = new Vector2Int(tile.x * 7 + i * 131, tile.y * 13 - i * 97);

            var src = PrefabFor(key);
            var art = src != null ? src.transform.Find("[Art]") : null;
            if (art == null) continue;

            var m = Object.Instantiate(art.gameObject, go.transform, false);
            m.name = "[Member]";

            float u = RockMap.Hash01(key, 0x61C88647u) * 2f - 1f;
            float v = RockMap.Hash01(key, 0x3C6EF372u) * 2f - 1f;
            float s = Mathf.Lerp(MemberScale.x, MemberScale.y, RockMap.Hash01(key, 0x14057B7Eu));

            m.transform.localPosition = art.localPosition + new Vector3(u, v * 0.8f, 0f) * ClusterSpread;
            m.transform.localRotation = TwistFor(key) * art.localRotation;
            m.transform.localScale    = art.localScale * s;
        }
    }

    // 이 칸에 설 모양.
    public GameObject PrefabFor(Vector2Int tile)
    {
        if (Variants == null || Variants.Length == 0) return Prefab;
        int i = (int)(RockMap.Hash01(tile, 0x165667B1u) * Variants.Length);
        var v = Variants[Mathf.Clamp(i, 0, Variants.Length - 1)];
        return v != null ? v : Prefab;
    }

    // 벽 바깥을 보는 기본 회전에 이 칸의 기울기를 얹는다.
    public Quaternion TwistFor(Vector2Int tile) => Quaternion.Euler(
        (RockMap.Hash01(tile, 0xD3A2646Cu) * 2f - 1f) * Tilt,
        (RockMap.Hash01(tile, 0xFD7046C5u) * 2f - 1f) * Tilt,
        (RockMap.Hash01(tile, 0xB55A4F09u) * 2f - 1f) * Roll);

    // **세운 뒤에 입힌다** — 크기와 빛깔. 빛깔은 `MaterialPropertyBlock`으로 **렌더러에만** 얹는다
    // (재질을 복제하면 칸마다 재질이 하나씩 샌다).
    static MaterialPropertyBlock _mpb;

    public void Dress(GameObject go, Vector2Int tile)
    {
        float s = Mathf.Lerp(ScaleRange.x, ScaleRange.y, RockMap.Hash01(tile, 0x68E31DA4u));
        if (!Mathf.Approximately(s, 1f)) go.transform.localScale *= s;

        if (HueJitter <= 0f) return;
        float dh = (RockMap.Hash01(tile, 0x1B56C4E9u) * 2f - 1f) * HueJitter;

        foreach (var l in go.GetComponentsInChildren<Light>(true)) l.color = Shift(l.color, dh);

        _mpb ??= new MaterialPropertyBlock();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterial;
            if (m == null) continue;
            r.GetPropertyBlock(_mpb);
            if (m.HasProperty("_BaseColor"))     _mpb.SetColor("_BaseColor",     Shift(m.GetColor("_BaseColor"), dh));
            if (m.HasProperty("_EmissionColor")) _mpb.SetColor("_EmissionColor", Shift(m.GetColor("_EmissionColor"), dh));
            r.SetPropertyBlock(_mpb);
        }
    }

    // 색상만 돌린다 — 밝기(HDR 세기 포함)는 그대로.
    static Color Shift(Color c, float dh)
    {
        float k = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (k <= 0f) return c;
        Color.RGBToHSV(c / k, out float h, out float sat, out float v);
        var o = Color.HSVToRGB(Mathf.Repeat(h + dh, 1f), sat, v) * k;
        o.a = c.a;
        return o;
    }
}
