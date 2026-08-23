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
        var verts = new Vector3[Segments];
        var cols  = new Color[Segments];
        var idx   = new int[Segments * 2];

        // **부모 기준 좌표로 넣는다** — 자식 오브젝트라 월드 좌표를 그대로 넣으면 부모가
        // 움직인 만큼 두 번 밀린다(핵 이전 미리보기가 정확히 그 경로다).
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            var world = new Vector3(_builtCenter.x + Mathf.Cos(a) * _builtRadius,
                                    _builtCenter.y,
                                    _builtCenter.z + Mathf.Sin(a) * _builtRadius);
            verts[i] = _go.transform.InverseTransformPoint(world);
            cols[i]  = _builtColor;

            idx[i * 2]     = i;
            idx[i * 2 + 1] = (i + 1) % Segments;
        }

        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetColors(cols);
        _mesh.SetIndices(idx, MeshTopology.Lines, 0);

        // **경계를 손으로 잡는다** — 선은 얇아 자동 경계가 카메라 밖으로 판정되면 통째로
        // 사라진다(`GridOverlay`가 같은 자리에서 겪었다).
        _mesh.bounds = new Bounds(_go.transform.InverseTransformPoint(_builtCenter),
                                  new Vector3(_builtRadius * 2f, 1f, _builtRadius * 2f));
    }

    static bool Approximately(Vector3 a, Vector3 b) =>
        !float.IsNaN(b.x) && (a - b).sqrMagnitude < 0.0001f;
}
