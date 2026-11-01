using UnityEngine;

// **발밑 그림자 — 폰마다 둥근 어둠 한 장**(2026-11-01, 사용자: *"게임 전체적으로 캐릭터에 그림자를 넣을 수 없나?"* → A + B).
//
// 굴에는 해가 없고 그림자를 던지는 등은 몇 개뿐이라(`ShadowBudget`) 진짜 그림자는 거의 안 보인다 — 그래서 폰이 땅에서 떠 보였다.
// 진짜 그림자 대신 **발밑에 가장자리가 번지는 원판**을 깐다(조명과 상관없이 늘 · 싸다). 몸의 명암(B)은 셰이더가 한다(`ToonOutline._FormShade`).
//
// - **크기는 종족이 든다**(`BodySize.ScaleOf`) — 오우거는 두 배.
// - **폰의 자식이다** — 안개(`FogOfWar`)와 1인칭(`FirstPersonView`)이 폰 아래 렌더러를 통째로 가리므로 그림자도 같이 숨는다.
// - **업혀 있으면 안 그린다** — 몸이 어깨 위로 올라가 원판이 허공에 뜬다.
// - **틀을 재는 곳은 건너뛴다**(`IsShadow`) — 초상화(`PortraitCapture`)와 미리보기(`PawnPreviewStage`)는 렌더러 경계로 틀을 잡는다.
// - 재질 · 메시는 세상에 한 벌(`GroundRing`과 같은 길 — `Sprites/Default` · 정점 색 알파). 깊이 판정은 켠다(벽 너머로 안 비친다).
public class PawnShadow : MonoBehaviour
{
    const string ChildName = "FootShadow";
    const float  Radius = 0.42f;      // 사람 몸집 1일 때(m)
    const float  Alpha  = 0.42f;
    const int    Segments = 32;

    static Material _mat;
    static Mesh     _mesh;

    MeshRenderer _renderer;
    UnitBrain    _brain;
    float        _next;

    public static void Attach(GameObject pawn, UnitData data)
    {
        if (pawn == null || pawn.TryGetComponent(out PawnShadow _)) return;
        var s = pawn.AddComponent<PawnShadow>();
        s.Build(BodySize.ScaleOf(data));
    }

    // 틀을 재는 곳이 거른다 — 이름으로 묻는다(컴포넌트를 찾으러 가지 않게).
    public static bool IsShadow(Renderer r) => r != null && r.gameObject.name == ChildName;

    void Build(float scale)
    {
        TryGetComponent(out _brain);
        var go = new GameObject(ChildName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        go.transform.localScale    = Vector3.one * Mathf.Max(0.3f, scale);
        go.layer = gameObject.layer;
        go.AddComponent<MeshFilter>().sharedMesh = Disc();
        _renderer = go.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial       = Mat();
        _renderer.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows       = false;
        _renderer.lightProbeUsage      = UnityEngine.Rendering.LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // 업혔나는 자주 안 바뀐다 — 0.25초마다 묻는다.
    void LateUpdate()
    {
        if (_renderer == null || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.25f;
        var bb = _brain != null ? _brain.Blackboard : null;
        bool carried = bb != null && bb.CarriedBy != null;
        if (_renderer.enabled == carried) _renderer.enabled = !carried;
    }

    static Material Mat()
    {
        if (_mat != null) return _mat;
        _mat = new Material(RuntimeShaders.Find(RuntimeShaders.SpritesDefault))
        {
            hideFlags   = HideFlags.HideAndDontSave,
            renderQueue = 2990,   // 몸 선(3001) · 발밑 고리(3000)보다 먼저 — 그 둘이 그림자 위에 선다
        };
        return _mat;
    }

    // 가운데가 짙고 가장자리로 갈수록 0 — 고리 다섯 겹(가운데 점 + 네 겹)의 정점 알파로 번지게 한다.
    static Mesh Disc()
    {
        if (_mesh != null) return _mesh;
        float[] rings = { 0.45f, 0.7f, 0.88f, 1f };
        float[] alpha = { 1f, 0.75f, 0.35f, 0f };
        var verts  = new Vector3[1 + rings.Length * Segments];
        var colors = new Color[verts.Length];
        verts[0] = Vector3.zero; colors[0] = new Color(0f, 0f, 0f, Alpha);
        for (int r = 0; r < rings.Length; r++)
        for (int i = 0; i < Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2f;
            int v = 1 + r * Segments + i;
            verts[v]  = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (rings[r] * Radius);
            colors[v] = new Color(0f, 0f, 0f, Alpha * alpha[r]);
        }
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < Segments; i++)   // 가운데 부채
        {
            int a0 = 1 + i, a1 = 1 + (i + 1) % Segments;
            tris.Add(0); tris.Add(a1); tris.Add(a0);
        }
        for (int r = 0; r < rings.Length - 1; r++)
        for (int i = 0; i < Segments; i++)
        {
            int a0 = 1 + r * Segments + i, a1 = 1 + r * Segments + (i + 1) % Segments;
            int b0 = a0 + Segments,         b1 = a1 + Segments;
            tris.Add(a0); tris.Add(a1); tris.Add(b0);
            tris.Add(a1); tris.Add(b1); tris.Add(b0);
        }
        _mesh = new Mesh { name = "PawnFootShadow", hideFlags = HideFlags.HideAndDontSave };
        _mesh.SetVertices(verts);
        _mesh.SetColors(colors);
        _mesh.SetTriangles(tris, 0);
        _mesh.RecalculateBounds();
        return _mesh;
    }
}
