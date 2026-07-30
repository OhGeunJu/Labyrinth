using System.Collections.Generic;
using UnityEngine;

// 월드맵 격자 — **지형은 만들지 않는다.**
//
// 지형은 두 가지 방식 중 하나로 온다 — 어느 쪽이든 이 컴포넌트는 **표면에 레이캐스트**할 뿐이다:
//   (a) 같은 오브젝트의 `WorldMapTerrain`이 **맵 전체를 이어진 메시 한 장**으로 짓는다(기본)
//   (b) 씬에 지형(Terrain/메시)을 직접 만들어 두고 격자만 그 위에 얹는다 — 그러려면 `WorldMapTerrain`을 빼면 된다
// 이 컴포넌트가 하는 일은 셋뿐:
//   1) 노드 좌표 ↔ 지형 표면의 월드 좌표 변환 (위에서 아래로 레이캐스트해 표면을 찾는다)
//      → 지형이 Unity Terrain이든 모델링한 메시든 **콜라이더만 있으면 똑같이 동작한다.**
//        나중에 Terrain을 메시로 갈아끼워도 이 코드는 그대로다.
//   2) 격자선과 **선택 칸 테두리**를 지형에 붙여 그린다 (격자선은 보고 싶을 때만)
//   3) 격자 지점에 오브젝트를 얹는다 (마을·소굴·자원 + 바이옴 데코)
//
// 그래서 높이·색·생김새는 전부 저작자의 지형 몫이다. 격자 데이터(`WorldMapData`)가 갖는 건
// **논리 정보**(바이옴 = 콘텐츠 게이팅 / 내용물 = 그 칸에 있는 것)뿐이다.
//
// 배치: `gridOrigin`이 격자 (0,0) 칸의 **모서리**다. 여기서 +X/+Z로 `nodeSpacing`씩 뻗어
// `WorldMap.Width × Height` 칸이 깔린다. 저작한 지형 위에 격자가 얹히도록 이 원점을 맞춘다.
[DefaultExecutionOrder(-45)]   // WorldMapView(-40)가 Start에서 쓰므로 그 전에 준비
public class WorldMapGrid : MonoBehaviour
{
    [Header("격자 배치")]
    [Tooltip("격자 (0,0) 칸의 모서리. 비우면 이 오브젝트의 위치.")]
    [SerializeField] Transform gridOrigin;
    [SerializeField] float nodeSpacing = 4f;

    [Header("지형 표면 찾기")]
    [Tooltip("표면으로 인정할 레이어. 저작한 지형만 남기면 오브젝트에 걸리지 않는다.")]
    [SerializeField] LayerMask terrainMask = ~0;
    [Tooltip("이 높이에서 아래로 레이캐스트해 표면을 찾는다. 지형 최고점보다 높아야 한다.")]
    [SerializeField] float sampleFromHeight = 500f;
    [Tooltip("월드맵 지형이 Unity Terrain이고 콜라이더 레이캐스트가 실패할 때만 쓰는 폴백. " +
             "**반드시 월드맵 쪽 Terrain을 직접 연결할 것** — 비워두면 폴백 없이 원점 평면으로 떨어진다.")]
    [SerializeField] Terrain fallbackTerrain;

    [Header("격자선")]
    [SerializeField] bool showGrid = true;
    [SerializeField] Color gridColor = new Color(1f, 1f, 1f, 0.25f);
    [Tooltip("격자선이 지형에 파묻히지 않게 띄우는 높이.")]
    [SerializeField] float lineHover = 0.15f;
    [Tooltip("칸 하나를 몇 조각으로 나눠 선을 지형에 붙일지. 클수록 지형을 잘 따라간다.")]
    [SerializeField, Range(1, 8)] int lineSubdivisions = 4;

    [Header("선택 칸 강조")]
    [SerializeField] Color highlightColor = new Color(1f, 0.9f, 0.3f, 0.95f);
    [Tooltip("격자선보다 더 띄워야 겹치는 변에서 z-파이팅이 안 난다.")]
    [SerializeField] float highlightHover = 0.35f;

    [Header("오브젝트")]
    [SerializeField] WorldTileVisualData visuals;   // 선택 — 없으면 기본 도형 마커
    [SerializeField] float markerScale = 0.35f;     // nodeSpacing 대비 (프리팹 없을 때)
    [SerializeField] float markerHover = 0.25f;     // 표면에서 띄우는 높이
    [Tooltip("바이옴 데코의 흩뿌림/추첨 시드. 같은 값이면 매번 같은 배치가 나온다.")]
    [SerializeField] int decorSeed = 12345;

    public float NodeSpacing => nodeSpacing;
    public Transform Origin => gridOrigin != null ? gridOrigin : transform;

    GameObject _gridLines;
    GameObject _highlight;
    WorldMapTerrain _terrain;
    readonly Dictionary<Vector2Int, GameObject> _markers = new();
    readonly Dictionary<Vector2Int, GameObject> _decor   = new();
    readonly Dictionary<Color, Material>        _matCache = new();

    Transform _objectRoot;
    bool _built;

    // ── 좌표 ─────────────────────────────────────────────────────────────────

    // 노드 중심의 **지형 표면** 좌표. 표면을 못 찾으면 원점 높이로 떨어진다(격자가 지형 밖).
    public Vector3 SurfacePoint(Vector2Int node) => SurfacePoint(node.x + 0.5f, node.y + 0.5f);

    // 칸 단위 실수 좌표(u,v)의 지형 표면. 칸 안 임의 지점(데코 흩뿌림 등)에도 쓴다.
    public Vector3 SurfacePoint(float u, float v)
    {
        TrySurfacePoint(u, v, out var p);
        return p;
    }

    public bool TrySurfacePoint(float u, float v, out Vector3 point)
    {
        var flat = Origin.position + new Vector3(u * nodeSpacing, 0f, v * nodeSpacing);

        // 1) 콜라이더 레이캐스트 — Unity Terrain이든 직접 만든 메시든 똑같이 잡힌다.
        var from = flat + Vector3.up * sampleFromHeight;
        if (Physics.Raycast(from, Vector3.down, out var hit, sampleFromHeight * 2f,
                            terrainMask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            return true;
        }

        // 2) 콜라이더가 없거나 레이어가 어긋난 경우의 폴백 — 연결된 Terrain에서 직접 샘플.
        //    `Terrain.activeTerrain`(전역 조회)은 쓰지 않는다 — 필드맵의 `MapManager.SampleGroundHeight`가
        //    이미 그걸 쓰고 있어서, 씬에 Terrain이 둘이면 어느 쪽이 잡힐지 모른다(에디터에선 둘 다 활성).
        if (fallbackTerrain != null)
        {
            flat.y = fallbackTerrain.transform.position.y + fallbackTerrain.SampleHeight(flat);
            point  = flat;
            return true;
        }

        point = flat;
        return false;
    }

    // 임의의 월드 좌표(XZ)에서의 지형 표면. 자유 이동하는 파티가 매 프레임 지면에 붙을 때 쓴다.
    public Vector3 SurfaceAtWorld(Vector3 worldPos)
    {
        var local = worldPos - Origin.position;
        return SurfacePoint(local.x / nodeSpacing, local.z / nodeSpacing);
    }

    // 월드 좌표 → 노드. 지형 어디를 찍었든 격자 칸으로 환산한다.
    public bool TryNodeAt(Vector3 worldPos, out Vector2Int node)
    {
        var local = worldPos - Origin.position;
        node = new Vector2Int(Mathf.FloorToInt(local.x / nodeSpacing),
                              Mathf.FloorToInt(local.z / nodeSpacing));
        return WorldMap.Instance != null && WorldMap.Instance.InBounds(node);
    }

    public bool TryNodeAt(RaycastHit hit, out Vector2Int node)
    {
        // 오브젝트가 콜라이더를 갖고 있으면 그쪽 표식을 우선(프리팹 대비).
        var refc = hit.collider != null ? hit.collider.GetComponentInParent<WorldTileRef>() : null;
        if (refc != null) { node = refc.Node; return true; }
        return TryNodeAt(hit.point, out node);
    }

    // ── 생성 ─────────────────────────────────────────────────────────────────

    public void Build()
    {
        var wm = WorldMap.Instance;
        if (wm == null) { Debug.LogWarning("[WorldMapGrid] WorldMap.Instance 없음"); return; }

        EnsureObjectRoot();
        Clear();

        // 1) 지형부터. 같은 오브젝트에 `WorldMapTerrain`이 있으면 맵 전체를 메시 한 장으로 짓는다.
        //    없으면 씬에 직접 만들어 둔 지형을 쓰는 것으로 본다.
        _terrain = GetComponent<WorldMapTerrain>();
        _terrain?.Build(nodeSpacing);

        // 방금 지은 메시의 콜라이더가 바로 아래 레이캐스트에 잡히도록 물리 상태를 맞춘다.
        Physics.SyncTransforms();

        // 2) 그 표면 위에 나머지를 앉힌다 — 순서가 뒤집히면 전부 원점 평면에 붙는다.
        int missed = 0;
        for (int x = 0; x < wm.Width;  x++)
        for (int y = 0; y < wm.Height; y++)
        {
            if (!TrySurfacePoint(x + 0.5f, y + 0.5f, out _)) missed++;
            BuildObjects(new Vector2Int(x, y));
        }

        if (showGrid) BuildGridLines(wm);

        if (missed > 0)
            Debug.LogWarning($"[WorldMapGrid] {wm.Width * wm.Height}칸 중 {missed}칸에서 표면을 못 찾았습니다 " +
                             "— WorldMapTerrain이 없는데 씬에 지형도 없거나, " +
                             "격자 원점/칸 크기가 지형과 안 맞거나, terrainMask에 지형 레이어가 빠졌습니다.");

        _built = true;
    }

    // 내용물이 바뀐 칸만 갱신 — 지형 형태와 격자선은 그대로 두고 색과 오브젝트만 다시 만든다.
    public void RefreshNode(Vector2Int node)
    {
        if (!_built) return;
        _terrain?.RefreshColor(node);
        DestroyAt(_markers, node);
        DestroyAt(_decor,   node);
        BuildObjects(node);
    }

    public void SetGridVisible(bool on)
    {
        showGrid = on;
        if (_gridLines != null) _gridLines.SetActive(on);
        else if (on && _built && WorldMap.Instance != null) BuildGridLines(WorldMap.Instance);
    }

    // ── 격자선 ───────────────────────────────────────────────────────────────

    // 선분 메시 한 장. 각 선을 칸당 `lineSubdivisions` 조각으로 잘라 지형 표면을 따라가게 한다.
    void BuildGridLines(WorldMap wm)
    {
        var verts = new List<Vector3>();
        var idx   = new List<int>();

        // 세로선 (x 고정) — v를 따라 훑는다
        for (int x = 0; x <= wm.Width; x++)
            AddPolyline(verts, idx, wm.Height * lineSubdivisions,
                        t => new Vector2(x, t * wm.Height));

        // 가로선 (y 고정) — u를 따라 훑는다
        for (int y = 0; y <= wm.Height; y++)
            AddPolyline(verts, idx, wm.Width * lineSubdivisions,
                        t => new Vector2(t * wm.Width, y));

        var mesh = new Mesh { name = "WorldGridLines" };
        mesh.indexFormat = verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetIndices(idx, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();

        _gridLines = new GameObject("GridLines");
        _gridLines.transform.SetParent(Origin, false);
        _gridLines.transform.localPosition = Vector3.zero;
        _gridLines.AddComponent<MeshFilter>().sharedMesh = mesh;

        var mr = _gridLines.AddComponent<MeshRenderer>();
        mr.sharedMaterial = LineMat(gridColor);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    // ── 선택 칸 강조 ─────────────────────────────────────────────────────────

    // 고른 칸의 테두리를 굵게 — 격자선과 같은 방식으로 지형 표면을 따라간다.
    // 한 번에 한 칸만 강조되므로 기존 것을 지우고 새로 만든다(칸 수만큼 미리 만들어 둘 이유가 없다).
    public void SetHighlight(Vector2Int node)
    {
        ClearHighlight();
        if (WorldMap.Instance == null || !WorldMap.Instance.InBounds(node)) return;

        var verts = new List<Vector3>();
        var idx   = new List<int>();

        float x0 = node.x, x1 = node.x + 1f;
        float y0 = node.y, y1 = node.y + 1f;

        // 네 변을 각각 지형에 붙여 그린다(칸 모서리만 이으면 경사에서 지형을 뚫는다).
        AddPolyline(verts, idx, lineSubdivisions, t => new Vector2(Mathf.Lerp(x0, x1, t), y0), highlightHover);
        AddPolyline(verts, idx, lineSubdivisions, t => new Vector2(x1, Mathf.Lerp(y0, y1, t)), highlightHover);
        AddPolyline(verts, idx, lineSubdivisions, t => new Vector2(Mathf.Lerp(x1, x0, t), y1), highlightHover);
        AddPolyline(verts, idx, lineSubdivisions, t => new Vector2(x0, Mathf.Lerp(y1, y0, t)), highlightHover);

        var mesh = new Mesh { name = "WorldNodeHighlight" };
        mesh.SetVertices(verts);
        mesh.SetIndices(idx, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();

        _highlight = new GameObject("NodeHighlight");
        _highlight.transform.SetParent(Origin, false);
        _highlight.transform.localPosition = Vector3.zero;
        _highlight.AddComponent<MeshFilter>().sharedMesh = mesh;

        var mr = _highlight.AddComponent<MeshRenderer>();
        mr.sharedMaterial = LineMat(highlightColor);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    public void ClearHighlight()
    {
        if (_highlight != null) Destroy(_highlight);
        _highlight = null;
    }

    // 지형에 붙는 폴리라인 한 줄. at(t)는 t∈[0,1]에서 칸 단위 (u,v)를 준다.
    void AddPolyline(List<Vector3> verts, List<int> idx, int segments, System.Func<float, Vector2> at,
                     float hover = -1f)
    {
        if (hover < 0f) hover = lineHover;

        int first = verts.Count;
        for (int s = 0; s <= segments; s++)
        {
            var uv = at((float)s / segments);
            var p  = SurfacePoint(uv.x, uv.y) + Vector3.up * hover;
            verts.Add(Origin.InverseTransformPoint(p));   // 부모가 Origin이라 로컬로 넣는다
        }
        for (int s = 0; s < segments; s++)
        {
            idx.Add(first + s);
            idx.Add(first + s + 1);
        }
    }

    // ── 격자 위의 오브젝트 ────────────────────────────────────────────────────

    void BuildObjects(Vector2Int node)
    {
        var tile = WorldMap.Instance.GetTile(node);

        if (tile.Content != WorldNodeContent.Empty)
            _markers[node] = BuildMarker(node, tile.Content);

        var decor = BuildDecor(node, tile.Biome);
        if (decor != null) _decor[node] = decor;
    }

    GameObject BuildMarker(Vector2Int node, WorldNodeContent content)
    {
        var visual = visuals != null ? visuals.GetContent(content) : null;
        var top    = SurfacePoint(node) + new Vector3(0f, markerHover + (visual?.HeightOffset ?? 0f), 0f);

        GameObject go;
        if (visual != null && visual.Prefab != null)
        {
            go = Instantiate(visual.Prefab, top, Quaternion.identity, _objectRoot);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_objectRoot, false);
            go.transform.localScale = Vector3.one * (nodeSpacing * markerScale);
            go.transform.position   = top + new Vector3(0f, nodeSpacing * markerScale * 0.5f, 0f);
            go.GetComponent<Renderer>().sharedMaterial = LitMat(content.Color());
        }

        go.name = $"POI_{node.x}_{node.y}_{content}";
        // 마커는 클릭 판정에서 빠진다 — 노드 선택은 지형이 받는다.
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
        return go;
    }

    // 바이옴 데코. 프리팹을 안 꽂았으면 아무것도 안 세운다 —
    // 지형에 이미 나무·바위가 있다면 그걸 쓰면 되고, 격자에 맞춰 얹고 싶을 때만 쓰는 층이다.
    GameObject BuildDecor(Vector2Int node, Biome biome)
    {
        var d = visuals != null ? visuals.GetVisual(biome) : null;
        if (d == null || d.Prefab == null) return null;

        // 노드 좌표 해시 기반 — 같은 시드면 재생성해도 배치가 흔들리지 않는다.
        var rng = new System.Random(decorSeed ^ (node.x * 73856093) ^ (node.y * 19349663));
        if (rng.NextDouble() > d.Density) return null;

        float ox = (float)(rng.NextDouble() * 2.0 - 1.0) * d.Scatter;
        float oz = (float)(rng.NextDouble() * 2.0 - 1.0) * d.Scatter;

        // 흩뜨린 **그 자리의** 표면을 다시 샘플한다(칸 중심 높이를 쓰면 경사에서 뜬다).
        var pos = SurfacePoint(node.x + 0.5f + ox / nodeSpacing,
                               node.y + 0.5f + oz / nodeSpacing);

        var rot = d.RandomYaw ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) : Quaternion.identity;
        var go  = Instantiate(d.Prefab, pos, rot, _objectRoot);
        go.name = $"Decor_{node.x}_{node.y}_{biome}";
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
        return go;
    }

    // ── 살림살이 ──────────────────────────────────────────────────────────────

    void EnsureObjectRoot()
    {
        if (_objectRoot != null) return;
        var go = new GameObject("Grid Objects");
        go.transform.SetParent(Origin, false);
        _objectRoot = go.transform;
    }

    public void Clear()
    {
        if (_gridLines != null) Destroy(_gridLines);
        _gridLines = null;
        ClearHighlight();

        _terrain?.Clear();
        foreach (var go in _markers.Values) if (go != null) Destroy(go);
        foreach (var go in _decor.Values)   if (go != null) Destroy(go);
        _markers.Clear();
        _decor.Clear();
    }

    static void DestroyAt(Dictionary<Vector2Int, GameObject> map, Vector2Int node)
    {
        if (map.TryGetValue(node, out var go) && go != null) Destroy(go);
        map.Remove(node);
    }

    Material LineMat(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Unlit/Color")
                 ?? Shader.Find("Sprites/Default");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        return m;
    }

    // 색당 머티리얼 1장만 만든다 — 마커마다 새로 만들면 금세 수백 장이 된다.
    Material LitMat(Color c)
    {
        if (_matCache.TryGetValue(c, out var cached) && cached != null) return cached;

        var sh = Shader.Find("Universal Render Pipeline/Lit")
                 ?? Shader.Find("Standard")
                 ?? Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        _matCache[c] = m;
        return m;
    }
}
