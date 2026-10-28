using System.Collections.Generic;
using UnityEngine;

// **외곽벽 — 칸 하나를 다 차지하는 뚱뚱한 성벽, 이웃과 이어진다**(2026-10-01, 사용자: *"방어탭에 있는 벽이
// 너무 그냥 직사각형이니깐 그것도 좀 수정"* · *"외각벽은 1칸을 다 차지하는 뚱뚱한 벽이고 건물 벽은 얇고 한쪽에
// 딱 붙는 느낌으로"*).
//
// 옛 벽은 0.96 × 2 × 0.96 상자 하나였다 — 늘어놓으면 **틈 4cm로 갈라진 비석 줄**이었다. 성벽은 두 가지가
// 있어야 성벽으로 읽힌다: **이어진다**(칸 사이 틈이 없다) · **머리가 톱니다**(총안). 거기에 받침과 띠돌을 둘러
// 옆면을 끊는다 — 매끈한 면 하나가 *상자*로 읽히는 까닭이다.
//
// ## 모양은 이웃이 정한다 — 열여섯 벌
//
// 사방 이웃이 벽인가(4비트)로 모양이 갈린다. **메시는 그 열여섯 벌을 한 번씩만 짓고 나눠 쓴다**(`_cache`) —
// 벽은 수백 칸 깔리는데 칸마다 짓지 않는다. 곧게 이어진 줄(동서 · 남북)은 **벽**이고, 그 밖(끝 · 모퉁이 ·
// 갈림)은 **망대**다 — 한 단 높고 네 귀에 톱니가 선다. 그래야 줄의 끝과 꺾임이 눈에 걸린다.
//
// ## 이웃은 칸 표로 찾는다
//
// 등록은 `Start`·해제는 `OnDestroy`만 — **꺼질 때 빼지 않는다.** 휴면(`BuildingDormancy`)은 컴포넌트를 재우는데,
// 거기서 빼면 영역 밖 벽 줄이 끊긴 모양으로 굳는다. 청사진 고스트는 `Start` 전에 스크립트가 떨어지므로 표에
// 안 든다(지어진 벽만 잇는다 — 다 지어지면 그때 이어진다).
//
// **길·충돌은 안 건드린다** — 칸은 `PlacedBuilding`이 막고 콜라이더는 프리팹 것 그대로다. 여기는 그림뿐이다.
[DisallowMultipleComponent]
public class FortWall : MonoBehaviour
{
    // 칸 → 벽. 같은 칸에 둘이 서는 일은 배치가 막는다.
    static readonly Dictionary<Vector2Int, FortWall> _at = new();
    static readonly Mesh[] _cache = new Mesh[16];

    [Tooltip("모양을 갈아 끼울 몸통. 비우면 첫 자식의 MeshFilter.")]
    [SerializeField] MeshFilter body;

    Vector2Int _tile;
    bool _registered;
    int _mask = -1;

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };   // 북 동 남 서

    void Start()
    {
        if (MapManager.Instance == null) return;
        if (body == null) body = GetComponentInChildren<MeshFilter>(true);

        _tile = MapManager.Instance.WorldToTile(transform.position);
        if (_at.TryGetValue(_tile, out var other) && other != null && other != this) return;   // 이미 누가 있다 — 그림만 둔다

        _at[_tile] = this;
        _registered = true;
        Refresh();
        foreach (var d in Dirs) if (_at.TryGetValue(_tile + d, out var n) && n != null) n.Refresh();
    }

    void OnDestroy()
    {
        if (!_registered) return;
        if (_at.TryGetValue(_tile, out var me) && me == this) _at.Remove(_tile);
        _registered = false;
        foreach (var d in Dirs) if (_at.TryGetValue(_tile + d, out var n) && n != null) n.Refresh();
    }

    // 도메인 리로드를 꺼 두면 static이 살아남는다 — 판을 새로 열 때 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _at.Clear();

    void Refresh()
    {
        if (body == null) return;

        // 이웃은 **세계 방위**로 잰다 — 몸통이 건물 회전을 따라 돌므로 메시를 고를 때는 그만큼 되돌린다.
        int world = 0;
        for (int i = 0; i < 4; i++)
            if (_at.TryGetValue(_tile + Dirs[i], out var n) && n != null) world |= 1 << i;

        int quarter = Mathf.RoundToInt(transform.eulerAngles.y / 90f) & 3;
        int local = 0;
        for (int i = 0; i < 4; i++)
            if ((world & (1 << i)) != 0) local |= 1 << ((i - quarter + 4) & 3);

        if (local == _mask) return;
        _mask = local;

        var t = body.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;
        body.sharedMesh = MeshFor(local);
    }

    // ── 모양 ────────────────────────────────────────────────────────────────

    // 눈금(m). 높이는 옛 벽(2m)을 그대로 지킨다 — 폰 키와 시야의 감이 바뀌지 않게.
    const float Half      = 0.5f;
    const float Core      = 0.40f;   // 몸통 반폭(끝면) — 0.8m. 이어진 쪽은 칸 끝까지 간다
    const float PlinthH   = 0.32f;   // 받침
    const float PlinthOut = 0.47f;   // 받침 반폭 — 몸통보다 넓다
    const float BandY     = 1.25f;   // 띠돌
    const float BandH     = 0.12f;
    const float BandOut   = 0.44f;
    const float BodyH     = 2.0f;    // 몸통 꼭대기(걷는 면)
    const float CapH      = 0.08f;   // 머리돌 — 살짝 내민다
    const float CapOut    = 0.45f;
    const float MerlonH   = 0.42f;   // 톱니 높이
    const float MerlonT   = 0.16f;   // 톱니 두께
    const float TowerUp   = 0.35f;   // 망대가 줄보다 높은 만큼

    static Mesh MeshFor(int mask)
    {
        if (_cache[mask] != null) return _cache[mask];

        bool n = (mask & 1) != 0, e = (mask & 2) != 0, s = (mask & 4) != 0, w = (mask & 8) != 0;
        bool straightNS = n && s && !e && !w;
        bool straightEW = e && w && !n && !s;
        bool tower = !(straightNS || straightEW);

        var b = new Builder();

        // 한 켜의 반폭을 방위마다 낸다 — 이어진 쪽은 칸 끝(0.5)까지 뻗어 이웃과 맞붙는다.
        void Slab(float y0, float y1, float side)
        {
            float x0 = w ? -Half : -side, x1 = e ? Half : side;
            float z0 = s ? -Half : -side, z1 = n ? Half : side;
            b.Box(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));
        }

        float top = BodyH + (tower ? TowerUp : 0f);

        Slab(0f, PlinthH, PlinthOut);                 // 받침
        Slab(PlinthH, top, Core);                     // 몸통
        Slab(BandY, BandY + BandH, BandOut);          // 띠돌
        Slab(top, top + CapH, CapOut);                // 머리돌

        float y0 = top + CapH, y1 = y0 + MerlonH;
        if (tower)
        {
            // 망대 — 네 귀에 톱니, 사이는 트였다(이어진 쪽도 귀는 선다: 줄이 망대에 꽂힌다).
            const float m = 0.22f;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                float cx = sx * (CapOut - m * 0.5f), cz = sz * (CapOut - m * 0.5f);
                b.Box(new Vector3(cx - m * 0.5f, y0, cz - m * 0.5f), new Vector3(cx + m * 0.5f, y1 + 0.06f, cz + m * 0.5f));
            }
        }
        else
        {
            // 곧은 줄 — 양 바깥 가장자리에 톱니 둘씩(칸 1m에 톱니 · 틈 · 톱니 · 틈). 다음 칸과 박자가 이어진다.
            for (int side = -1; side <= 1; side += 2)
            for (int k = 0; k < 2; k++)
            {
                float a0 = -Half + k * 0.5f + 0.06f, a1 = a0 + 0.26f;           // 줄을 따라
                float o1 = side * CapOut, o0 = side * (CapOut - MerlonT);          // 바깥 가장자리
                float lo = Mathf.Min(o0, o1), hi = Mathf.Max(o0, o1);
                if (straightEW) b.Box(new Vector3(a0, y0, lo), new Vector3(a1, y1, hi));
                else            b.Box(new Vector3(lo, y0, a0), new Vector3(hi, y1, a1));
            }
        }

        var mesh = b.Build($"FortWall_{mask}");
        _cache[mask] = mesh;
        return mesh;
    }

    // 상자를 모아 메시 하나로. **면마다 꼭짓점을 따로 둔다** — 모서리가 각져 보여야 돌이다(공유하면 둥글게 칠해진다).
    // UV는 세계 미터 그대로 — 돌 재질의 무늬가 상자 크기에 따라 늘어나지 않게.
    class Builder
    {
        readonly List<Vector3> _v = new();
        readonly List<Vector3> _n = new();
        readonly List<Vector2> _uv = new();
        readonly List<int> _t = new();

        public void Box(Vector3 lo, Vector3 hi)
        {
            Quad(new(lo.x, lo.y, hi.z), new(hi.x, lo.y, hi.z), new(hi.x, hi.y, hi.z), new(lo.x, hi.y, hi.z), Vector3.forward);
            Quad(new(hi.x, lo.y, lo.z), new(lo.x, lo.y, lo.z), new(lo.x, hi.y, lo.z), new(hi.x, hi.y, lo.z), Vector3.back);
            Quad(new(hi.x, lo.y, hi.z), new(hi.x, lo.y, lo.z), new(hi.x, hi.y, lo.z), new(hi.x, hi.y, hi.z), Vector3.right);
            Quad(new(lo.x, lo.y, lo.z), new(lo.x, lo.y, hi.z), new(lo.x, hi.y, hi.z), new(lo.x, hi.y, lo.z), Vector3.left);
            Quad(new(lo.x, hi.y, lo.z), new(lo.x, hi.y, hi.z), new(hi.x, hi.y, hi.z), new(hi.x, hi.y, lo.z), Vector3.up);
            // 바닥면은 안 그린다 — 땅에 붙어 있거나(받침) 아래 켜에 덮인다.
        }

        // a→b→c→d는 바깥에서 보아 **시계 방향**으로 넘긴다(유니티는 시계 방향이 앞면).
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            for (int k = 0; k < 4; k++) _n.Add(normal);

            // 면이 보는 축을 빼고 남은 두 축이 무늬 축이다.
            Vector2 UV(Vector3 p) => Mathf.Abs(normal.y) > 0.5f ? new Vector2(p.x, p.z)
                                   : Mathf.Abs(normal.x) > 0.5f ? new Vector2(p.z, p.y)
                                   : new Vector2(p.x, p.y);
            _uv.Add(UV(a)); _uv.Add(UV(b)); _uv.Add(UV(c)); _uv.Add(UV(d));

            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            _t.Add(i); _t.Add(i + 2); _t.Add(i + 3);
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }
}
