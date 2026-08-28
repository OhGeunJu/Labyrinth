using UnityEngine;

// **건설 격자를 그린다 — 세상의 일부로.**
//
// 한동안 `GL.Begin(GL.LINES)`로 `RenderPipelineManager.endCameraRendering`에서 즉시 그렸다.
// 그러면 **UI 위에 격자가 얹힌다** — 화면 아래 띠에도, 갈래 패널에도 흰 줄이 그어져 창이
// 반투명한 것처럼 보였다. 즉시 모드로 카메라 렌더 *끝*에 그리는 것은 스크린 스페이스 오버레이
// 캔버스와 순서를 다투는 일이라, 어느 쪽이 이기는지가 렌더 파이프라인 버전에 달린다.
//
// **메시로 두면 그 다툼이 없다.** 평범한 `MeshRenderer`라 세상의 다른 물건과 같은 규칙으로
// 그려지고, 오버레이 UI는 언제나 그 위다. 덤으로 **매 프레임 팔백 줄을 다시 쌓지 않는다** —
// 지도가 바뀔 때 한 번만 짓는다.
//
// **`Gizmos`가 아니다** — 기즈모는 에디터에서만 보이고 빌드에서는 아무것도 안 그린다.
public class GridOverlay : MonoBehaviour
{
    [SerializeField] Color lineColor = new Color(1f, 1f, 1f, 0.2f);

    [Tooltip("격자를 띄울 높이. 바닥과 같은 높이면 z-파이팅으로 깜빡인다.")]
    [SerializeField] float lift = 0.02f;

    // **기본은 끔**(2026-08-28, 사용자 결정). 어느 칸에 지어지는지는 초록 미리보기가 이미
    // 답하고, 200×200이 지면을 따라 깔려 있으면 **읽는 데 보태는 것 없이 화면만 어지럽다.**
    // 끄는 것으로 두되 지우지는 않는다 — 켜서 보고 싶을 때가 있다.
    [Tooltip("건설 모드에서 격자선을 그릴 것인가. 끄면 초록 미리보기만 남는다.")]
    [SerializeField] bool showLines = false;

    MeshRenderer _renderer;
    MeshFilter   _filter;
    Mesh         _mesh;
    Material     _mat;

    // 다시 지어야 하는지 가리는 값들. 지도 크기·원점·색이 그대로면 손대지 않는다.
    int     _builtW = -1, _builtH = -1;
    Vector3 _builtOrigin;
    Color   _builtColor;
    int     _builtFloors = -1;
    bool    _visible;

    // **빌드존이 없으면 안 그린다.** 격자의 위상은 첫 조작이 정하고(`MapManager.ProposeGrid`
    // /`ClaimGrid`) 그릴 범위는 빌드존이 정하는데, 핵이 없으면 그 범위가 없다 — 원점 기준
    // 200×200을 그리면 **세계 원점에 박힌 거짓 격자**가 화면에 서고, 핵을 꽂는 순간 선이
    // 통째로 밀린다. 짓는 데는 격자선이 없어도 되고(초록 미리보기가 어느 칸인지 답한다),
    // 있어야 할 자리는 빌드존이 선 뒤다.
    public void SetVisible(bool v)
    {
        _visible = v;
        Apply();
    }

    void Apply()
    {
        var map = MapManager.Instance;
        bool show = showLines && _visible && map != null && map.HasBuildZone;

        if (show) Rebuild();
        if (_renderer != null) _renderer.enabled = show;
    }

    void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_mat  != null) Destroy(_mat);
    }

    void EnsureObjects()
    {
        if (_renderer != null) return;

        // **자식으로 둔다** — 이 컴포넌트가 붙은 오브젝트에 다른 것이 붙어 있을 수 있고,
        // 격자만 껐다 켜려면 제 렌더러가 따로 있어야 한다. **떼지는 않는다** — 정착지 루트가
        // 꺼질 때(`SettlementManager`) 격자만 살아남아 빈 세상에 흰 줄이 남는다.
        var go = new GameObject("GridLines");
        go.transform.SetParent(transform, worldPositionStays: false);
        go.hideFlags = HideFlags.DontSave;
        ZeroWorld(go.transform);

        _filter   = go.AddComponent<MeshFilter>();
        _renderer = go.AddComponent<MeshRenderer>();

        _mesh = new Mesh { name = "GridLines" };
        _mesh.MarkDynamic();
        _filter.sharedMesh = _mesh;

        // **깊이 판정을 끈 오버레이 머티리얼**(`OverlayMaterial` — 영역 원과 한 벌).
        //
        // 전에는 `Sprites/Default`에 렌더 큐만 올려 뒀는데, 그건 **그리는 순서**만 바꿀 뿐
        // 깊이 판정은 그대로라 **바닥을 깔면 격자가 그 아래로 들어가 사라졌다** — 정작
        // 건설할 자리에서만 안 보이는 격자였다. 슬래브 위 `lift`(0.02)로는 못 넘는다.
        _mat = OverlayMaterial.Create();

        _renderer.sharedMaterial   = _mat;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows    = false;
        _renderer.enabled           = false;
    }

    // **부모의 자리를 지운다 — 정점이 월드 좌표이기 때문이다.**
    //
    // `RangeCircle`은 부모 기준 좌표로 쌓지만(핵에 붙어 같이 움직인다) 격자는 붙어 다니는
    // 물건이 아니라 **세계의 격자**라 `MapManager.WorldOrigin`을 그대로 쓴다. 그래서 부모가
    // 원점에 없으면 그린 것이 통째로 그만큼 밀린다 — 씬의 `MapManager`가 `Settlement_0`
    // (y = 87.1) 아래 있어서 **격자가 지면 87m 위에 떠 있었고**, 핵을 꽂아도 그 자리에
    // 아무것도 안 보였다. `MapManager`는 제 `transform`을 아예 안 읽으므로(좌표계의 주인은
    // 첫 핵이다) **어디에 매달리든 격자는 세계 좌표에 서야 맞다.**
    static void ZeroWorld(Transform t)
    {
        t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // 부모가 스케일을 들고 있으면 선 간격이 그만큼 늘어난다 — 되돌린다.
        var s = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(
            Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x,
            Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y,
            Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z);
    }

    // 지도 한 장을 선으로 짓는다. **`MeshTopology.Lines`** — 삼각형으로 만들면 굵기를
    // 정해야 하고, 그 굵기는 카메라 높이에 따라 뜻이 달라진다.
    void Rebuild()
    {
        var map = MapManager.Instance;
        if (map == null) return;

        EnsureObjects();

        // **매번 다시 지운다** — 부모가 나중에 움직이면(정착지 루트를 옮기는 도구가 있다)
        // 한 번 지워 둔 것으로는 안 되고, 격자가 조용히 다시 밀린다.
        ZeroWorld(_filter.transform);

        if (!map.HasBuildZone) return;

        int w = map.Width, h = map.Height;
        float ox = map.WorldOrigin.x, oz = map.WorldOrigin.z;

        if (_builtW == w && _builtH == h && _builtColor == lineColor &&
            _builtFloors == map.FloorCount &&
            Mathf.Approximately(_builtOrigin.x, ox) && Mathf.Approximately(_builtOrigin.z, oz)) return;

        // **교차점마다 지면을 잰다.** 평평한 판 한 장으로 그리던 시절의 잔재가 오래 남아
        // 있었다 — 필드맵이 200m 한 장이고 바닥이 y = 0이던 때는 맞았지만, 세계가 절차
        // 지형으로 바뀐 뒤로는 **격자만 해수면에 누워 있었다.** 깊이 판정이 꺼져 있어
        // 사라지지는 않고 **엉뚱한 자리에 그려진다** — 산 중턱에 핵을 꽂으면 격자가 저 아래
        // 평지에 떠서, 눌러야 할 칸과 보이는 칸이 다르다.
        //
        // 한 줄을 통짜 선분으로 그으면 안 된다 — 두 끝만 지면을 따르고 가운데는 땅을
        // 뚫는다. **칸마다 끊어서** 잇는다.
        var y = new float[w + 1, h + 1];
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int gx = 0; gx <= w; gx++)
            for (int gz = 0; gz <= h; gz++)
            {
                float gy = CornerY(map, map.ZoneOriginTile + new Vector2Int(gx, gz), ox + gx, oz + gz) + lift;
                y[gx, gz] = gy;
                if (gy < minY) minY = gy;
                if (gy > maxY) maxY = gy;
            }

        int count = (w * (h + 1) + h * (w + 1)) * 2;
        var verts = new Vector3[count];
        var cols  = new Color[count];
        var idx   = new int[count];

        int v = 0;
        for (int row = 0; row <= h; row++)
            for (int col = 0; col < w; col++)
            {
                verts[v] = new Vector3(ox + col,     y[col,     row], oz + row); cols[v] = lineColor; idx[v] = v; v++;
                verts[v] = new Vector3(ox + col + 1, y[col + 1, row], oz + row); cols[v] = lineColor; idx[v] = v; v++;
            }
        for (int col = 0; col <= w; col++)
            for (int row = 0; row < h; row++)
            {
                verts[v] = new Vector3(ox + col, y[col, row],     oz + row);     cols[v] = lineColor; idx[v] = v; v++;
                verts[v] = new Vector3(ox + col, y[col, row + 1], oz + row + 1); cols[v] = lineColor; idx[v] = v; v++;
            }

        _mesh.Clear();
        // 큰 지도는 정점이 65535를 넘을 수 있다 — 넘으면 조용히 잘리는 대신 32비트로 짓는다.
        _mesh.indexFormat = count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        _mesh.vertices = verts;
        _mesh.colors   = cols;
        _mesh.SetIndices(idx, MeshTopology.Lines, 0);

        // 선은 얇아서 자동 경계가 카메라 밖으로 판정되면 통째로 사라진다 — 직접 잡아 준다.
        // **높이도 실제 범위로 잡는다**(1m로 두면 비탈에서 통째로 컬링된다).
        _mesh.bounds = new Bounds(new Vector3(ox + w * 0.5f, (minY + maxY) * 0.5f, oz + h * 0.5f),
                                  new Vector3(w, Mathf.Max(maxY - minY, 1f), h));

        _builtW      = w;
        _builtH      = h;
        _builtFloors = map.FloorCount;
        _builtOrigin = new Vector3(ox, 0f, oz);
        _builtColor  = lineColor;
    }

    // 교차점 하나의 높이. **바닥이 깔렸으면 그 윗면이다.**
    //
    // 격자가 그리는 것은 *지을 자리*이지 *땅 모양*이 아니다. 지면만 따르면 기단을 깔아
    // 놓고도 격자는 그 밑 흙에 박혀 있어서 **눌러야 할 칸과 보이는 칸이 어긋난다**
    // (기단이 높을수록 크게 벌어진다).
    //
    // 교차점은 칸 넷이 만나는 자리다 — **그중 가장 높은 바닥**을 따른다. 낮은 쪽을 따르면
    // 선이 옆 슬래브 옆구리에 파묻힌다.
    static float CornerY(MapManager map, Vector2Int corner, float worldX, float worldZ)
    {
        float best = float.MinValue;
        bool  any  = false;

        for (int dx = -1; dx <= 0; dx++)
        for (int dz = -1; dz <= 0; dz++)
            if (map.TryGetFloorSurface(corner + new Vector2Int(dx, dz), out float y) && y > best)
            { best = y; any = true; }

        return any ? best : map.SampleGroundHeight(worldX, worldZ);
    }

    // 지도가 켜진 뒤에 크기가 정해지는 경우가 있어(정착지 전환) **보이는 동안에만** 다시 본다.
    void LateUpdate()
    {
        if (_visible) Apply();
    }
}
