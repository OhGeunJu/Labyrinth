using System.Collections.Generic;
using UnityEngine;

// **바닥은 오브젝트가 아니라 데이터다**(2026-08-28).
//
// 칸마다 프리팹을 하나씩 세우던 시절엔 20x20 마당 하나가 **GameObject 800개**였다(슬래브와
// 치마가 각각 하나씩). 렌더링은 SRP 배처가 어느 정도 삼켰지만 먼저 아팠던 곳은 따로 있다 —
// 철거 박스를 끄는 동안 `PlacedBuilding.All`을 매 프레임 전부 돌며 화면 좌표로 옮기는 코드가
// 있어서, **바닥이 늘수록 철거 조작 자체가 프레임을 먹었다.**
//
// 그런데 `MapManager._floored`가 이미 *"여기 바닥이 있고 윗면은 얼마"*의 단일 출처다.
// 오브젝트들은 그것을 한 번 더 들고 있는 **중복**이었다. 그리는 쪽 하나만 있으면 된다.
//
// -- 왜 청크인가 ------------------------------------------------------------
//
// 메시 한 장으로 두면 칸 하나를 헐 때마다 세계 전체를 다시 굽는다. 16x16으로 잘라 두면
// 다시 굽는 것은 그 청크뿐이다(`TerrainStreamer`와 같은 이유, 같은 눈금 감각).
//
// **옆면은 드러난 모서리에만 세운다.** 칸마다 상자를 세우면 안쪽 면이 전부 낭비이고,
// 무엇보다 맞닿은 두 칸 사이에 **면이 두 장 겹쳐 z-파이팅**이 난다.
public class FloorRenderer : MonoBehaviour
{
    public static FloorRenderer Instance { get; private set; }

    const int   ChunkTiles = 16;
    const float Bury       = 0.3f;    // 옆면을 지면 아래로 이만큼 묻는다(실틈 방지)
    const float Same       = 0.01f;   // 이 안이면 같은 높이로 본다

    // **한 프레임에 다 굽지 않는다** — 큰 면을 한 번에 깔면 여러 청크가 동시에 더러워진다.
    const int RebuildsPerFrame = 2;

    Material _mat;

    class Chunk
    {
        public GameObject   Go;
        public Mesh         Mesh;
        public MeshCollider Col;
    }

    readonly Dictionary<Vector2Int, Chunk> _chunks = new();
    readonly HashSet<Vector2Int>           _dirty  = new();

    // 굽는 동안 돌려 쓰는 그릇 — 청크마다 새로 만들면 그것이 곧 GC다.
    readonly List<Vector3> _verts = new();
    readonly List<Vector3> _norms = new();
    readonly List<Vector2> _uvs   = new();
    readonly List<int>     _tris  = new();

    // -- 세우기 ---------------------------------------------------------------
    //
    // **씬 배선이 없다.** 아무 상태도 안 들고 저작할 것도 없다 — 재질만 밖에서 받는데
    // 그건 바닥 프리팹이 이미 들고 있다(`BuildingData.Prefab`). 배선을 요구하면 맵마다
    // 그것을 반복해야 하고, 빠뜨리면 **바닥이 조용히 안 보인다.**
    public static FloorRenderer Ensure(Material mat = null)
    {
        if (Instance == null)
        {
            var go = new GameObject("[Floors]");
            Instance = go.AddComponent<FloorRenderer>();
        }
        if (mat != null) Instance.SetMaterial(mat);
        return Instance;
    }

    public void SetMaterial(Material mat)
    {
        if (mat == null || mat == _mat) return;
        _mat = mat;
        foreach (var c in _chunks.Values)
            if (c.Go != null) c.Go.GetComponent<MeshRenderer>().sharedMaterial = _mat;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        MapManager.OnFloorChanged += OnFloorChanged;
        RebuildAll();
    }

    void OnDestroy()
    {
        MapManager.OnFloorChanged -= OnFloorChanged;
        if (Instance == this) Instance = null;
        foreach (var c in _chunks.Values) Discard(c);
        _chunks.Clear();
    }

    // 칸 하나가 바뀌면 그 청크와 **맞닿은 청크들**이 더러워진다 — 옆면은 이웃을 보고
    // 세우므로, 청크 경계에 붙은 칸이 바뀌면 옆 청크의 면도 달라진다.
    void OnFloorChanged(Vector2Int tile)
    {
        _dirty.Add(ChunkOf(tile));
        _dirty.Add(ChunkOf(tile + Vector2Int.right));
        _dirty.Add(ChunkOf(tile + Vector2Int.left));
        _dirty.Add(ChunkOf(tile + Vector2Int.up));
        _dirty.Add(ChunkOf(tile + Vector2Int.down));
    }

    public void RebuildAll()
    {
        var map = MapManager.Instance;
        if (map == null) return;

        foreach (var t in map.FloorTiles) OnFloorChanged(t);
        foreach (var c in _chunks.Keys)   _dirty.Add(c);
    }

    void LateUpdate()
    {
        int budget = RebuildsPerFrame;
        while (budget-- > 0 && _dirty.Count > 0)
        {
            Vector2Int chunk = default;
            foreach (var c in _dirty) { chunk = c; break; }
            _dirty.Remove(chunk);
            Build(chunk);
        }
    }

    static Vector2Int ChunkOf(Vector2Int tile) =>
        new(Mathf.FloorToInt(tile.x / (float)ChunkTiles), Mathf.FloorToInt(tile.y / (float)ChunkTiles));

    // -- 청크 한 장 굽기 -------------------------------------------------------
    void Build(Vector2Int chunk)
    {
        var map = MapManager.Instance;
        if (map == null) return;

        _verts.Clear(); _norms.Clear(); _uvs.Clear(); _tris.Clear();

        var ph = map.GridPhase;
        int x0 = chunk.x * ChunkTiles, z0 = chunk.y * ChunkTiles;

        for (int ix = 0; ix < ChunkTiles; ix++)
        for (int iz = 0; iz < ChunkTiles; iz++)
        {
            var t = new Vector2Int(x0 + ix, z0 + iz);
            if (!map.TryGetFloorSurface(t, out float y)) continue;

            // **손으로 놓은 바닥은 제 메시가 있다** — 여기서 또 그리면 두 장이 겹친다.
            if (Floor.IsAuthored(t)) continue;

            float wx0 = t.x + ph.x, wx1 = wx0 + 1f;
            float wz0 = t.y + ph.y, wz1 = wz0 + 1f;

            Top(wx0, wz0, wx1, wz1, y);

            Side(map, t, y, Vector2Int.right, wx1, wz0, wx1, wz1, Vector3.right);
            Side(map, t, y, Vector2Int.left,  wx0, wz1, wx0, wz0, Vector3.left);
            Side(map, t, y, Vector2Int.up,    wx1, wz1, wx0, wz1, Vector3.forward);
            Side(map, t, y, Vector2Int.down,  wx0, wz0, wx1, wz0, Vector3.back);
        }

        if (_tris.Count == 0)
        {
            if (_chunks.TryGetValue(chunk, out var dead)) { Discard(dead); _chunks.Remove(chunk); }
            return;
        }

        var c = GetOrCreate(chunk);
        var mesh = c.Mesh;
        mesh.Clear();
        mesh.indexFormat = _verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(_verts);
        mesh.SetNormals(_norms);
        mesh.SetUVs(0, _uvs);
        mesh.SetTriangles(_tris, 0);
        mesh.RecalculateBounds();

        // **콜라이더를 다시 물린다** — 같은 메시라도 갈아 끼워야 물리 쪽이 다시 굽는다.
        c.Col.sharedMesh = null;
        c.Col.sharedMesh = mesh;
    }

    // 윗면 한 장. UV는 월드 XZ라 청크가 갈려도 무늬가 안 끊긴다.
    void Top(float x0, float z0, float x1, float z1, float y)
    {
        int b = _verts.Count;
        Add(new Vector3(x0, y, z0), Vector3.up, new Vector2(x0, z0));
        Add(new Vector3(x1, y, z0), Vector3.up, new Vector2(x1, z0));
        Add(new Vector3(x1, y, z1), Vector3.up, new Vector2(x1, z1));
        Add(new Vector3(x0, y, z1), Vector3.up, new Vector2(x0, z1));
        _tris.Add(b); _tris.Add(b + 2); _tris.Add(b + 1);
        _tris.Add(b); _tris.Add(b + 3); _tris.Add(b + 2);
    }

    // 드러난 모서리 하나. **이웃이 같거나 더 높으면 안 세운다** — 안 그러면 맞닿은 두 칸
    // 사이에 면이 두 장 겹친다. 이웃이 낮으면 **그 윗면까지만** 내린다(그 아래는 이웃의 몫).
    void Side(MapManager map, Vector2Int t, float y, Vector2Int dir,
              float ax, float az, float bx, float bz, Vector3 normal)
    {
        float bottom;

        if (map.TryGetFloorSurface(t + dir, out float ny))
        {
            if (ny >= y - Same) return;
            bottom = ny;
        }
        else
        {
            // 바깥으로 드러난 면 — 지면까지 내리고 조금 묻는다. **두 끝 중 낮은 쪽**을
            // 봐야 비탈에서 한쪽 귀퉁이가 뜨지 않는다.
            bottom = Mathf.Min(map.SampleGroundHeight(ax, az), map.SampleGroundHeight(bx, bz)) - Bury;
        }

        if (bottom >= y - Same) return;

        int b = _verts.Count;
        float h = y - bottom;
        Add(new Vector3(ax, y,      az), normal, new Vector2(0f, h));
        Add(new Vector3(bx, y,      bz), normal, new Vector2(1f, h));
        Add(new Vector3(bx, bottom, bz), normal, new Vector2(1f, 0f));
        Add(new Vector3(ax, bottom, az), normal, new Vector2(0f, 0f));
        _tris.Add(b); _tris.Add(b + 1); _tris.Add(b + 2);
        _tris.Add(b); _tris.Add(b + 2); _tris.Add(b + 3);
    }

    void Add(Vector3 v, Vector3 n, Vector2 uv) { _verts.Add(v); _norms.Add(n); _uvs.Add(uv); }

    Chunk GetOrCreate(Vector2Int chunk)
    {
        if (_chunks.TryGetValue(chunk, out var c)) return c;

        var go = new GameObject($"Floor_{chunk.x}_{chunk.y}");
        go.transform.SetParent(transform, worldPositionStays: false);

        // **`Ground` 레이어에 둔다.** 기단은 걸어 다니는 땅이므로 커서 레이캐스트가
        // 여기에 맞아야 한다 — 안 그러면 커서가 슬래브를 통과해 **밑의 지형**을 짚고,
        // 카메라가 기울어 있는 만큼 **짚은 칸과 보이는 칸이 어긋난다.**
        // (옛 바닥 프리팹은 `Default`에 있어서 실제로 그랬다.)
        int ground = LayerMask.NameToLayer("Ground");
        go.layer = ground >= 0 ? ground : gameObject.layer;

        var mesh = new Mesh { name = go.name };
        mesh.MarkDynamic();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = _mat;
        var col = go.AddComponent<MeshCollider>();

        c = new Chunk { Go = go, Mesh = mesh, Col = col };
        _chunks[chunk] = c;
        return c;
    }

    // 메시는 손으로 지운다 — 코드로 만든 것이라 오브젝트만 지우면 샌다.
    static void Discard(Chunk c)
    {
        if (c == null) return;
        if (c.Mesh != null) Destroy(c.Mesh);
        if (c.Go   != null) Destroy(c.Go);
    }
}
