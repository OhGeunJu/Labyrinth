using System.Collections.Generic;
using UnityEngine;

// **층 사이 통로 — 내려가는 길 · 올라가는 길**(2026-10-01, `Floors`). 설계의 *"층 사이는 걸어서 못 가고 저작한 통로에서
// 전환한다"* 그대로다 — 층은 한 평면에 나란히 놓인 사분면이고 사이는 기반암이라, 걸어서는 못 넘고 **여기서만** 넘는다.
//
// ## 자리는 층 지도가 든다
//
// `WorldMapData.PassageDown`·`PassageUp`(지역 칸 — 비우면 자동: 내려가는 길 (W−4, H−4) · 올라가는 길 (3, 3)). 짝은 **N층의
// 내려가는 길 ↔ N+1층의 올라가는 길**이다. 판이 열릴 때 세우고(`Build` — `RockRenderer`가 시작 공터를 열 때 같이),
// 둘레 바위를 열고 가까운 자연 굴로 길을 낸다(시작 공터와 같은 문 — `RockMap.OpenAround`·`OpenPathToCave`). 안 열면 통로가
// 바위 속에 묻혀 보이지도 닿지도 않는다.
//
// ## 건너는 것은 포탈과 같다
//
// 폰을 고르고 통로를 우클릭 [N층으로 내려간다] → 걸어서 **닿으면** 짝 통로 옆에 선다(`PortalTravel` — 같은 문). 고른 폰이
// 넘어가면 카메라가 따라간다(층을 옮기는 것이 곧 카메라가 다른 사분면으로 가는 것이다).
//
// **모양은 도형이다** — 갱도 입구(돌 테두리 · 어두운 구덩이 · 나무 틀) / 돌계단. 에셋이 들어오면 갈아 끼운다.
public enum PassageKind { Down, Up }

public class FloorPassage : MonoBehaviour, IWorldClickable
{
    public static readonly List<FloorPassage> All = new();

    public PassageKind  Kind   { get; private set; }
    public int          Floor  { get; private set; }
    public FloorPassage Target { get; private set; }

    public int TargetFloor => Kind == PassageKind.Down ? Floor + 1 : Floor - 1;
    public string Verb  => Kind == PassageKind.Down ? "내려간다" : "올라간다";
    public string Label => Kind == PassageKind.Down ? $"{TargetFloor}층으로 내려가는 길" : $"{TargetFloor}층으로 올라가는 길";

    Vector2Int _tile;
    bool _occupied;

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    void OnDestroy()
    {
        if (_occupied && MapManager.Instance != null) MapManager.Instance.SetOccupied(_tile, false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); _mats = null; }

    public void OnWorldClick() =>
        EventNotificationUI.Notify("층 통로", $"{Label} - 폰을 고르고 우클릭해 {Verb}.");

    // ── 세우기 ──────────────────────────────────────────────────────────────

    // 판이 열릴 때 한 번. **이미 섰으면 안 세운다**(씬을 다시 불러도 두 벌이 안 서게).
    public static void Build()
    {
        if (All.Count > 0 || !RockMap.Enabled || MapManager.Instance == null) return;

        for (int f = 1; f < Floors.Count; f++)
        {
            var downAt = PointFor(f, PassageKind.Down);
            var upAt   = PointFor(f + 1, PassageKind.Up);
            var down = Place(f, PassageKind.Down, downAt);
            var up   = Place(f + 1, PassageKind.Up, upAt);
            down.Target = up;
            up.Target   = down;
        }
        Debug.Log($"[층] 통로 {All.Count}개를 세웠다 — 1~{Floors.Count}층.");
    }

    static Vector2Int DownRegion(int floor)
    {
        var d = WorldMap.FloorData(floor);
        int w = d != null ? d.Width : 16, h = d != null ? d.Height : 16;
        if (d != null && d.PassageDown.x >= 0) return d.PassageDown;
        return new Vector2Int(w - 4, h - 4);
    }

    static Vector2Int UpRegion(int floor)
    {
        var d = WorldMap.FloorData(floor);
        if (d != null && d.PassageUp.x >= 0) return d.PassageUp;
        return new Vector2Int(3, 3);
    }

    // **그 층의 통로 자리** — 판이 세울 때와 배치 도구가 *"걸어서 닿나"*를 잴 때(`WorldPopulator`)가 **같은 식**을 쓴다.
    // 1층엔 올라가는 길이, 마지막 층엔 내려가는 길이 없다(부르는 쪽이 거른다).
    public static Vector3 PointFor(int floor, PassageKind kind) =>
        RegionPoint(floor, kind == PassageKind.Down ? DownRegion(floor) : UpRegion(floor));

    // 층 지도의 지역 칸 → 세계 자리(지역 한복판).
    static Vector3 RegionPoint(int floor, Vector2Int local)
    {
        int fw = TerrainGrid.RegionsPerFloor;
        var q = Floors.Quadrant(floor);
        var g = new Vector2Int(q.x * fw + Mathf.Clamp(local.x, 0, fw - 1), q.y * fw + Mathf.Clamp(local.y, 0, fw - 1));
        var c = TerrainGrid.RegionCenter(g);
        return new Vector3(c.x, TerrainHeight.At(c.x, c.z), c.z);
    }

    static FloorPassage Place(int floor, PassageKind kind, Vector3 at)
    {
        var map = MapManager.Instance;
        RockMap.OpenAround(at, 7f);
        RockMap.OpenPathToCave(at, 160f, 3f);

        var tile = map.WorldToTile(at);
        var ground = map.TileToGround(tile);

        var go = new GameObject($"[FloorPassage {floor} {kind}]") { hideFlags = SelfStanding.RuntimeObject };
        go.transform.position = ground;
        var p = go.AddComponent<FloorPassage>();
        p.Kind = kind; p.Floor = floor; p._tile = tile;

        // 한복판 한 칸만 막는다 — 폰은 그 옆에 서고(`PortalTravel`의 닿는 반경), 콜라이더는 누르는 자리다.
        map.SetOccupied(tile, true);
        p._occupied = true;
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(2.2f, 2f, 2.2f);
        col.center = new Vector3(0f, 1f, 0f);

        if (kind == PassageKind.Down) BuildPit(go.transform); else BuildStairs(go.transform);
        AddTorch(go.transform);
        return p;
    }

    // ── 모양(도형) ──────────────────────────────────────────────────────────

    static Material[] _mats;   // 돌 · 어둠 · 나무
    static Material Mat(int i)
    {
        if (_mats == null)
        {
            _mats = new Material[3];
            var sh = RuntimeShaders.Find(RuntimeShaders.Lit, RuntimeShaders.Standard);
            var cols = new[] { new Color(0.34f, 0.32f, 0.30f), new Color(0.015f, 0.015f, 0.02f), new Color(0.33f, 0.21f, 0.11f) };
            for (int k = 0; k < 3; k++)
            {
                if (sh == null) break;
                _mats[k] = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                FadeMaterial.SetColor(_mats[k], cols[k]);
            }
        }
        return _mats[i];
    }

    static void Box(Transform parent, Vector3 pos, Vector3 size, int mat, float yaw = 0f)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(b.GetComponent<Collider>());
        b.transform.SetParent(parent, false);
        b.transform.localPosition = pos;
        b.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        b.transform.localScale = size;
        var m = Mat(mat);
        if (m != null) b.GetComponent<Renderer>().sharedMaterial = m;
    }

    // 갱도 입구 — 돌 테두리 여덟 · 어두운 구덩이 · 나무 틀(기둥 둘 + 들보).
    static void BuildPit(Transform t)
    {
        Box(t, new Vector3(0f, 0.02f, 0f), new Vector3(2.0f, 0.04f, 2.0f), 1);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            Box(t, new Vector3(Mathf.Cos(a) * 1.25f, 0.18f, Mathf.Sin(a) * 1.25f),
                new Vector3(0.7f, 0.36f + (i % 3) * 0.06f, 0.45f), 0, -a * Mathf.Rad2Deg);
        }
        Box(t, new Vector3(-1.1f, 1.2f, 0f), new Vector3(0.22f, 2.4f, 0.22f), 2);
        Box(t, new Vector3( 1.1f, 1.2f, 0f), new Vector3(0.22f, 2.4f, 0.22f), 2);
        Box(t, new Vector3(0f, 2.45f, 0f), new Vector3(2.6f, 0.24f, 0.3f), 2);
    }

    // 돌계단 — 네 단이 위로(+Z 쪽으로 높아진다) · 양옆 돌기둥.
    static void BuildStairs(Transform t)
    {
        for (int i = 0; i < 4; i++)
            Box(t, new Vector3(0f, 0.15f + i * 0.3f, -0.6f + i * 0.45f), new Vector3(1.8f, 0.3f + i * 0.6f, 0.5f), 0);
        Box(t, new Vector3(-1.15f, 1.3f, 0.2f), new Vector3(0.35f, 2.6f, 0.35f), 0);
        Box(t, new Vector3( 1.15f, 1.3f, 0.2f), new Vector3(0.35f, 2.6f, 0.35f), 0);
        Box(t, new Vector3(0f, 2.7f, 0.2f), new Vector3(2.7f, 0.3f, 0.45f), 0);
    }

    // 불 하나 — 굴 속에서 찾을 수 있게. **`LightSource`를 단다** — 밝은 곳은 실제로 더 보인다(`Vision`).
    static void AddTorch(Transform t)
    {
        var go = new GameObject("Torch");
        go.transform.SetParent(t, false);
        go.transform.localPosition = new Vector3(0f, 2.9f, 0f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.72f, 0.42f);
        l.intensity = 2.2f;
        l.range = 9f;
        var src = t.gameObject.AddComponent<LightSource>();
        src.AlwaysLit = true;
        src.enabled = false; src.enabled = true;   // 늘 켜짐으로 다시 걸린다(`OnEnable`이 `alwaysLit`을 본다)
    }
}
