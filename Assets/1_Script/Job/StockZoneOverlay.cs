using System.Collections.Generic;
using UnityEngine;

// **저장 구역의 테두리를 그린다**(2026-10-06, `StockZones`).
//
// 밭 구역 테두리(`FarmZoneOverlay`)와 같은 방법이다 — 칸마다 네 변을 보고 **이웃이 구역이 아닌 변만** 낸다. 붙은 칸은
// 선이 저절로 하나로 이어지고 구멍도 저절로 맞는다. 색은 하나다(구역이 하나의 규칙이라 가를 것이 없다).
//
// - **영역 밖으로 밀려난 칸은 회색**이다 — 칠한 것은 남아 있지만 지금은 안 쓴다(`StockZones.Contains`). 지워진 줄
//   알고 다시 칠하지 않게, 그리고 왜 시체가 안 오는지 보이게.
// - **끌고 있는 사각형은 흰 선**으로 위에 얹는다(밭 미리보기와 같은 색 규약 — 아직 구역이 아니다).
// - **바뀌었을 때만 다시 짓는다** — 칸 수 · 미리보기 · 핵 영역(밀려난 칸의 색이 바뀐다).
[DisallowMultipleComponent]
public class StockZoneOverlay : MonoBehaviour
{
    const float Lift = 0.06f;   // 밭 테두리(0.05)와 겹치지 않게 살짝 위

    static readonly Color Zone     = new(0.95f, 0.75f, 0.30f, 1f);
    static readonly Color Inactive = new(0.55f, 0.55f, 0.55f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => SelfStanding.Create<StockZoneOverlay>("StockZoneOverlay");

    MeshRenderer _renderer;
    Mesh         _mesh;
    Material     _mat;

    int  _builtVersion = -1;
    int  _builtPreview = -1;
    bool _territoryDirty = true;

    readonly List<Vector3> _verts = new();
    readonly List<Color>   _cols  = new();
    readonly List<int>     _idx   = new();
    float _minX, _maxX, _minY, _maxY, _minZ, _maxZ;

    void Awake()
    {
        var filter = gameObject.AddComponent<MeshFilter>();
        _renderer  = gameObject.AddComponent<MeshRenderer>();
        _mesh = new Mesh { name = "StockZoneOutline" };
        _mesh.MarkDynamic();
        filter.sharedMesh = _mesh;

        _mat = OverlayMaterial.Create();
        _renderer.sharedMaterial    = _mat;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows    = false;
        _renderer.enabled = false;
    }

    void OnEnable()  => DungeonCore.OnTerritoryChanged += MarkTerritory;
    void OnDisable() => DungeonCore.OnTerritoryChanged -= MarkTerritory;
    void MarkTerritory() => _territoryDirty = true;

    void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_mat  != null) Destroy(_mat);
    }

    void LateUpdate()
    {
        int preview = FarmZoneDesignator.StockPreviewVersion;
        if (!_territoryDirty && _builtVersion == StockZones.Version && _builtPreview == preview) return;
        _territoryDirty = false;
        _builtVersion   = StockZones.Version;
        _builtPreview   = preview;
        Build();
    }

    void Build()
    {
        _verts.Clear(); _cols.Clear(); _idx.Clear();
        _minX = _minY = _minZ = float.MaxValue;
        _maxX = _maxY = _maxZ = float.MinValue;

        var map = MapManager.Instance;
        if (map == null) { _mesh.Clear(); _renderer.enabled = false; return; }

        var tiles = StockZones.Tiles;
        foreach (var t in tiles)
        {
            bool on = StockZones.Contains(t);
            Outline(map, t, StockZones.IsPainted, on ? Zone : Inactive, Lift);
        }

        if (FarmZoneDesignator.HasStockPreview)
        {
            var a = FarmZoneDesignator.StockPreviewA;
            var b = FarmZoneDesignator.StockPreviewB;
            var rect = new HashSet<Vector2Int>();
            for (int y = Mathf.Min(a.y, b.y); y <= Mathf.Max(a.y, b.y); y++)
            for (int x = Mathf.Min(a.x, b.x); x <= Mathf.Max(a.x, b.x); x++)
                rect.Add(new Vector2Int(x, y));
            foreach (var t in rect) Outline(map, t, rect.Contains, Color.white, Lift * 2f);
        }

        _mesh.Clear();
        if (_verts.Count == 0) { _renderer.enabled = false; return; }

        _mesh.indexFormat = _verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        _mesh.SetVertices(_verts);
        _mesh.SetColors(_cols);
        _mesh.SetIndices(_idx, MeshTopology.Lines, 0);

        // 선은 얇아서 자동 경계가 카메라 밖으로 판정되면 통째로 사라진다 — 직접 잡는다(`FarmZoneOverlay`와 같은 이유).
        _mesh.bounds = new Bounds(
            new Vector3((_minX + _maxX) * 0.5f, (_minY + _maxY) * 0.5f, (_minZ + _maxZ) * 0.5f),
            new Vector3(Mathf.Max(_maxX - _minX, 1f), Mathf.Max(_maxY - _minY, 1f), Mathf.Max(_maxZ - _minZ, 1f)));
        _renderer.enabled = true;
    }

    // 칸 하나의 바깥 변만 — 이웃이 같은 묶음이면 그 변은 안 그린다.
    void Outline(MapManager map, Vector2Int t, System.Func<Vector2Int, bool> inSet, Color col, float lift)
    {
        float y = map.SurfaceHeight(t) + lift;
        var c = map.TileToWorld(t);   // 격자 위상을 얹은 칸 가운데 — 안 얹으면 선이 반 칸 어긋난다
        float xa = c.x - 0.5f, xb = c.x + 0.5f, za = c.z - 0.5f, zb = c.z + 0.5f;

        if (!inSet(t + Vector2Int.left))  Edge(new(xa, y, za), new(xa, y, zb), col);
        if (!inSet(t + Vector2Int.right)) Edge(new(xb, y, za), new(xb, y, zb), col);
        if (!inSet(t + Vector2Int.up))    Edge(new(xa, y, zb), new(xb, y, zb), col);
        if (!inSet(t + Vector2Int.down))  Edge(new(xa, y, za), new(xb, y, za), col);
    }

    void Edge(Vector3 a, Vector3 b, Color c)
    {
        _idx.Add(_verts.Count); _verts.Add(a); _cols.Add(c);
        _idx.Add(_verts.Count); _verts.Add(b); _cols.Add(c);
        _minX = Mathf.Min(_minX, a.x, b.x); _maxX = Mathf.Max(_maxX, a.x, b.x);
        _minZ = Mathf.Min(_minZ, a.z, b.z); _maxZ = Mathf.Max(_maxZ, a.z, b.z);
        _minY = Mathf.Min(_minY, a.y);      _maxY = Mathf.Max(_maxY, a.y);
    }
}
