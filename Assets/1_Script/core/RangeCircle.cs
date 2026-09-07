using UnityEngine;
using UnityEngine.Rendering;

// **영역 원을 그린다 — 세상의 일부로**(2026-08-23).
//
// 던전 핵·서브코어·핵 이전 미리보기가 **똑같은 원을 세 벌로** 그리고 있었고, 셋 다
// `GL.Begin`을 `RenderPipelineManager.endCameraRendering`에서 불렀다. 그러면 **오버레이 UI
// 위에 원이 얹힌다** — 스탯 창을 열어 놓으면 그 위로 파란 원이 지나간다. 즉시 모드로 카메라
// 렌더 *끝*에 그리는 것은 스크린 스페이스 오버레이 캔버스와 순서를 다투는 일이고, 어느 쪽이
// 이기는지가 렌더 파이프라인 버전에 달린다.
//
// **`GridOverlay`가 이미 같은 이유로 즉시 모드를 버렸다** — 그때 적어 둔 그대로다:
// *"메시로 두면 그 다툼이 없다. 평범한 `MeshRenderer`라 세상의 다른 물건과 같은 규칙으로
// 그려지고, 오버레이 UI는 언제나 그 위다."* 세 번째로 같은 것을 적지 않으려고 여기 모았다.
//
// **`Gizmos`가 아니다** — 기즈모는 에디터에서만 보이고 빌드에서는 아무것도 안 그린다.
public sealed class RangeCircle
{
    // **칸을 따라 그린다 — 매끄러운 원이 아니다**(2026-09-06, 사용자가 림월드의 중력부양선
    // 범위를 예로 들었다).
    //
    // 영역 판정은 **칸 단위**다(`DungeonCore.IsWithinAnyRange`가 타일을 받는다). 그런데 그림은
    // 매끄러운 원이라 **경계에서 그림과 판정이 어긋났다** — 원 안에 보이는 칸이 실은 밖이거나
    // 그 반대였고, 그건 화면에서 *"왜 여기는 안 지어지지"*로만 보인다.
    //
    // **계단 모양이 정확한 그림이다.** 칸의 변을 따라 그리므로 **선이 곧 판정의 경계**다.
    //
    // **선 하나가 아니라 변 조각들이다** — 한 바퀴 잇는 순서를 알려면 다각형을 추적해야 하고,
    // 안 이어도 화면에서는 똑같다(`MeshTopology.Lines`는 선분 목록이다).
    //
    // 격자가 없는 씬에서는 예전처럼 매끄러운 원이다 — 칸이 없으면 계단을 낼 근거도 없다.
    const int Segments = 64;

    // 바닥에서 띄우는 높이. **깊이 판정을 끄므로 사실상 z-파이팅 방지용**이다.
    public const float Lift = 0.05f;

    readonly Transform _parent;
    readonly string    _name;

    GameObject   _go;
    MeshFilter   _filter;
    MeshRenderer _renderer;
    Mesh         _mesh;
    Material     _mat;

    // 다시 지어야 하는지 가리는 값들. 그대로면 손대지 않는다 —
    // 핵은 거의 안 움직이므로 사실상 처음 한 번만 짓는다.
    Vector3 _builtCenter = new(float.NaN, 0f, 0f);
    float   _builtRadius = -1f;
    Color   _builtColor;

    public RangeCircle(Transform parent, string name)
    {
        _parent = parent;
        _name   = name;
    }

    public void Draw(Vector3 center, float radius, Color color)
    {
        if (radius <= 0f) { Hide(); return; }

        EnsureObjects();
        _renderer.enabled = true;

        if (Approximately(center, _builtCenter) &&
            Mathf.Approximately(radius, _builtRadius) && color == _builtColor) return;

        _builtCenter = center;
        _builtRadius = radius;
        _builtColor  = color;
        Rebuild();
    }

    public void Hide()
    {
        if (_renderer != null) _renderer.enabled = false;
    }

    // 붙어 있던 오브젝트가 사라질 때 같이 치운다. **메시와 머티리얼은 손으로 지운다** —
    // 코드로 만든 것이라 씬을 떠나도 저절로 안 없어진다.
    public void Dispose()
    {
        if (_mesh != null) Object.Destroy(_mesh);
        if (_mat  != null) Object.Destroy(_mat);
        if (_go   != null) Object.Destroy(_go);
        _mesh = null; _mat = null; _go = null;
    }

    void EnsureObjects()
    {
        if (_renderer != null) return;

        // **자식으로 둔다** — 붙은 오브젝트에 이미 렌더러가 있을 수 있고(핵은 모델이 있다),
        // 원만 껐다 켜려면 제 렌더러가 따로 있어야 한다.
        _go = new GameObject(_name) { hideFlags = HideFlags.DontSave };
        _go.transform.SetParent(_parent, worldPositionStays: false);

        _filter   = _go.AddComponent<MeshFilter>();
        _renderer = _go.AddComponent<MeshRenderer>();

        _mesh = new Mesh { name = _name };
        _mesh.MarkDynamic();
        _filter.sharedMesh = _mesh;

        // 정점 색을 쓰는 가장 단순한 셰이더(`GridOverlay`와 같은 것). URP에서도 그대로 돈다.
        _mat = OverlayMaterial.Create();   // 정점 색 · 알파 · 깊이 판정 없음 — 격자와 한 벌

        _renderer.sharedMaterial    = _mat;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows    = false;
    }

    // **`MeshTopology.Lines`** — 삼각형으로 만들면 굵기를 정해야 하고, 그러면 카메라를 당길
    // 때마다 원이 굵어 보인다.
    void Rebuild()
    {
        var verts = new System.Collections.Generic.List<Vector3>();
        var cols  = new System.Collections.Generic.List<Color>();
        var idx   = new System.Collections.Generic.List<int>();

        // **점마다 지면을 잰다**(2026-08-28) — 부르는 쪽이 넘기는 Y(`Lift`)는 **평지 y=0
        // 시절의 값**이라, 지형이 y ≈ 87인 세계에서는 원이 **87m 땅속에** 그려졌다.
        // 격자가 먼저 겪은 것과 같은 함정이고 고치는 법도 같다(`GridOverlay`).
        //
        // **지면만 본다**(바닥·기단은 안 본다) — 원은 *"핵의 영역이 여기까지"*를 땅에
        // 그리는 것이고, 그 위에 무엇을 지었든 영역은 그대로다. 그리고 이 재질은 깊이
        // 판정이 꺼져 있어(`OverlayMaterial`) 기단 밑에 깔려도 안 사라진다.
        var map = MapManager.Instance;
        float minY = float.MaxValue, maxY = float.MinValue;

        // **부모 기준 좌표로 넣는다** — 자식 오브젝트라 월드 좌표를 그대로 넣으면 부모가
        // 움직인 만큼 두 번 밀린다(핵 이전 미리보기가 정확히 그 경로다).
        if (map == null) BuildSmooth(verts, cols, idx, ref minY, ref maxY);
        else             BuildStepped(map, verts, cols, idx, ref minY, ref maxY);

        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetColors(cols);
        _mesh.SetIndices(idx, MeshTopology.Lines, 0);

        // **경계를 손으로 잡는다** — 선은 얇아 자동 경계가 카메라 밖으로 판정되면 통째로
        // 사라진다(`GridOverlay`가 같은 자리에서 겪었다). **높이도 실제 범위로 잡는다** —
        // 1m로 두면 비탈에 걸친 원이 통째로 컬링된다.
        float midY = minY < maxY ? (minY + maxY) * 0.5f : _builtCenter.y;
        _mesh.bounds = new Bounds(
            _go.transform.InverseTransformPoint(new Vector3(_builtCenter.x, midY, _builtCenter.z)),
            new Vector3(_builtRadius * 2f, Mathf.Max(maxY - minY, 1f), _builtRadius * 2f));
    }

    // 칸의 변을 따라 그린다. **안쪽 칸과 바깥 칸이 갈리는 변만** 낸다 — 그것이 곧 경계이고,
    // `FarmZoneOverlay`가 구역 테두리를 내는 방식과 **같은 규칙**이다(면을 칠하지 않고
    // 이웃이 다른 변만 그린다).
    void BuildStepped(MapManager map,
                      System.Collections.Generic.List<Vector3> verts,
                      System.Collections.Generic.List<Color> cols,
                      System.Collections.Generic.List<int> idx,
                      ref float minY, ref float maxY)
    {
        var c = map.WorldToTile(_builtCenter);
        int r = Mathf.CeilToInt(_builtRadius) + 1;

        float rSq = _builtRadius * _builtRadius;

        bool Inside(int tx, int tz)
        {
            var w = map.TileToWorld(new Vector2Int(tx, tz));
            float dx = w.x - _builtCenter.x, dz = w.z - _builtCenter.z;
            return dx * dx + dz * dz <= rSq;
        }

        for (int tz = c.y - r; tz <= c.y + r; tz++)
        for (int tx = c.x - r; tx <= c.x + r; tx++)
        {
            if (!Inside(tx, tz)) continue;

            // 칸의 네 귀퉁이. **격자 위상이 정수가 아닐 수 있으므로 칸 중심에서 낸다.**
            var w = map.TileToWorld(new Vector2Int(tx, tz));
            const float h = 0.5f;
            var sw = new Vector3(w.x - h, 0f, w.z - h);
            var se = new Vector3(w.x + h, 0f, w.z - h);
            var nw = new Vector3(w.x - h, 0f, w.z + h);
            var ne = new Vector3(w.x + h, 0f, w.z + h);

            if (!Inside(tx, tz - 1)) AddLine(map, sw, se, verts, cols, idx, ref minY, ref maxY);
            if (!Inside(tx, tz + 1)) AddLine(map, nw, ne, verts, cols, idx, ref minY, ref maxY);
            if (!Inside(tx - 1, tz)) AddLine(map, sw, nw, verts, cols, idx, ref minY, ref maxY);
            if (!Inside(tx + 1, tz)) AddLine(map, se, ne, verts, cols, idx, ref minY, ref maxY);
        }
    }

    void BuildSmooth(System.Collections.Generic.List<Vector3> verts,
                     System.Collections.Generic.List<Color> cols,
                     System.Collections.Generic.List<int> idx,
                     ref float minY, ref float maxY)
    {
        for (int i = 0; i < Segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / Segments;
            float a1 = (i + 1) * Mathf.PI * 2f / Segments;

            AddLine(null,
                    new Vector3(_builtCenter.x + Mathf.Cos(a0) * _builtRadius, 0f,
                                _builtCenter.z + Mathf.Sin(a0) * _builtRadius),
                    new Vector3(_builtCenter.x + Mathf.Cos(a1) * _builtRadius, 0f,
                                _builtCenter.z + Mathf.Sin(a1) * _builtRadius),
                    verts, cols, idx, ref minY, ref maxY);
        }
    }

    // **점마다 지면을 잰다**(2026-08-28) — 부르는 쪽이 넘기는 Y는 *평지 y=0 시절의 값*이라,
    // 지형이 y = 87인 세계에서는 선이 **87m 땅속에** 그려진다.
    //
    // **지면만 본다**(바닥·기단은 안 본다) — 원은 *"핵의 영역이 여기까지"*를 땅에 그리는
    // 것이고, 그 위에 무엇을 지었든 영역은 그대로다.
    void AddLine(MapManager map, Vector3 a, Vector3 b,
                 System.Collections.Generic.List<Vector3> verts,
                 System.Collections.Generic.List<Color> cols,
                 System.Collections.Generic.List<int> idx,
                 ref float minY, ref float maxY)
    {
        int start = verts.Count;

        for (int k = 0; k < 2; k++)
        {
            var p = k == 0 ? a : b;
            float wy = map != null ? map.SampleGroundHeight(p.x, p.z) + Lift : _builtCenter.y;

            if (wy < minY) minY = wy;
            if (wy > maxY) maxY = wy;

            verts.Add(_go.transform.InverseTransformPoint(new Vector3(p.x, wy, p.z)));
            cols.Add(_builtColor);
        }

        idx.Add(start);
        idx.Add(start + 1);
    }

    static bool Approximately(Vector3 a, Vector3 b) =>
        !float.IsNaN(b.x) && (a - b).sqrMagnitude < 0.0001f;
}
