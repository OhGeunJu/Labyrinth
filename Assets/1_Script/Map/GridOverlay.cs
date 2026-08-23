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

    MeshRenderer _renderer;
    MeshFilter   _filter;
    Mesh         _mesh;
    Material     _mat;

    // 다시 지어야 하는지 가리는 값들. 지도 크기·원점·색이 그대로면 손대지 않는다.
    int     _builtW = -1, _builtH = -1;
    Vector3 _builtOrigin;
    Color   _builtColor;
    bool    _visible;

    public void SetVisible(bool v)
    {
        _visible = v;
        if (v) Rebuild();                       // 켜는 순간 지도가 바뀌었을 수 있다
        if (_renderer != null) _renderer.enabled = v;
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
        // 격자만 껐다 켜려면 제 렌더러가 따로 있어야 한다.
        var go = new GameObject("GridLines");
        go.transform.SetParent(transform, worldPositionStays: false);
        go.hideFlags = HideFlags.DontSave;

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

    // 지도 한 장을 선으로 짓는다. **`MeshTopology.Lines`** — 삼각형으로 만들면 굵기를
    // 정해야 하고, 그 굵기는 카메라 높이에 따라 뜻이 달라진다.
    void Rebuild()
    {
        var map = MapManager.Instance;
        if (map == null) return;

        EnsureObjects();

        if (_builtW == map.Width && _builtH == map.Height &&
            _builtOrigin == map.WorldOrigin && _builtColor == lineColor) return;

        int w = map.Width, h = map.Height;
        float ox = map.WorldOrigin.x, oz = map.WorldOrigin.z;

        int count = (h + 1 + w + 1) * 2;
        var verts = new Vector3[count];
        var cols  = new Color[count];
        var idx   = new int[count];

        int v = 0;
        for (int row = 0; row <= h; row++)
        {
            verts[v] = new Vector3(ox,     lift, oz + row); cols[v] = lineColor; idx[v] = v; v++;
            verts[v] = new Vector3(ox + w, lift, oz + row); cols[v] = lineColor; idx[v] = v; v++;
        }
        for (int col = 0; col <= w; col++)
        {
            verts[v] = new Vector3(ox + col, lift, oz);     cols[v] = lineColor; idx[v] = v; v++;
            verts[v] = new Vector3(ox + col, lift, oz + h); cols[v] = lineColor; idx[v] = v; v++;
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
        _mesh.bounds = new Bounds(new Vector3(ox + w * 0.5f, lift, oz + h * 0.5f),
                                  new Vector3(w, 1f, h));

        _builtW      = w;
        _builtH      = h;
        _builtOrigin = map.WorldOrigin;
        _builtColor  = lineColor;
    }

    // 지도가 켜진 뒤에 크기가 정해지는 경우가 있어(정착지 전환) **보이는 동안에만** 다시 본다.
    void LateUpdate()
    {
        if (_visible) Rebuild();
    }
}
