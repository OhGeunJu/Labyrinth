using System.Collections.Generic;
using UnityEngine;

// **끌어서 세울 때 어디에 서는지 보여 준다**(2026-09-08, 사용자: *"토탈워처럼 미리보기랑
// 쭉 누르면서 배치하는 그게 있어도 좋을 거 같아"*).
//
// **미리보기가 없으면 끌기라는 조작 자체가 성립하지 않는다** — 폭과 방향을 손으로 정하는데
// 놓기 전에는 결과가 안 보이니, 매번 놓아 보고 다시 하게 된다. 그건 조작이 아니라 시행착오다.
//
// 그리는 법은 `GridOverlay` 그대로다: **메시 한 장 + `MeshTopology.Lines`**. 즉시 모드
// (`GL.Begin`)로 카메라 렌더 끝에 그리면 **오버레이 UI 위에 얹히고**, 삼각형으로 만들면
// 굵기를 정해야 하는데 그 굵기는 카메라 높이에 따라 뜻이 달라진다.
//
// **씬 배선이 없다** — 아무 상태도 안 들고 저작할 것도 없다(`FormationController`와 한 벌).
[DefaultExecutionOrder(-40)]
public class FormationPreview : MonoBehaviour
{
    public static FormationPreview Instance { get; private set; }

    // 자리 하나를 나타내는 네모의 반변(m). **간격의 절반보다 작아야** 옆자리와 안 겹친다.
    const float Half = 0.4f;

    // 앞을 가리키는 화살의 길이(m). **어느 쪽을 보고 서는지가 진형의 절반**이라, 자리만
    // 찍어 두면 같은 그림이 앞뒤 뒤집힌 두 뜻을 갖는다.
    const float ArrowLen = 1.6f;

    // 바닥에서 살짝 띄운다 — 지면과 같은 높이면 z-파이팅으로 점선처럼 깜빡인다.
    const float Lift = 0.05f;

    static readonly Color SlotColor  = new(0.45f, 0.85f, 1f, 0.9f);
    static readonly Color FrontColor = new(1f, 0.85f, 0.35f, 0.95f);

    MeshRenderer _renderer;
    Mesh         _mesh;
    Material     _mat;

    readonly List<Vector3> _verts = new();
    readonly List<Color>   _cols  = new();
    readonly List<int>     _idx   = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("FormationPreview") { hideFlags = HideFlags.DontSave };
        go.AddComponent<FormationPreview>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        Instance = this;

        _mesh = new Mesh { name = "FormationPreview" };
        _mesh.MarkDynamic();

        gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = gameObject.AddComponent<MeshRenderer>();

        // **깊이 판정을 끈 오버레이 머티리얼** — 격자·영역선과 한 벌이다. 안 끄면 바닥을
        // 깔아 둔 자리에서 미리보기가 슬래브 아래로 들어가 **정작 세우려는 곳에서만 안 보인다.**
        _mat = OverlayMaterial.Create();
        _renderer.sharedMaterial    = _mat;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows    = false;
        _renderer.enabled           = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_mesh != null) Destroy(_mesh);
        if (_mat  != null) Destroy(_mat);
    }

    public static void Hide()
    {
        if (Instance != null && Instance._renderer != null) Instance._renderer.enabled = false;
    }

    // `spots`는 **진형이 낸 자리 그대로**다 — 여기서 다시 계산하면 미리보기와 실제로 서는
    // 자리가 갈린다(*"보이는 것과 놓이는 것이 다르다"*가 가장 나쁘다).
    //
    // **모양을 같이 받는다** — 사각형만은 *앞줄*이라는 것이 없어서 그림이 달라진다(아래).
    // `Formation.Current`를 여기서 읽지 않는 이유는 `Formation.Place`와 같다: 그러면
    // *언제 진형을 쓰는가*(= 소집했나)의 답이 화면 쪽에 숨는다.
    public static void Show(IReadOnlyList<Vector3> spots, Vector3 facing, Formation.Shape shape)
    {
        if (Instance != null) Instance.Build(spots, facing, shape);
    }

    void Build(IReadOnlyList<Vector3> spots, Vector3 facing, Formation.Shape shape)
    {
        if (spots == null || spots.Count == 0) { _renderer.enabled = false; return; }

        facing.y = 0f;
        if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
        facing.Normalize();
        var side = Vector3.Cross(Vector3.up, facing);

        _verts.Clear(); _cols.Clear(); _idx.Clear();

        // **사각형은 전부가 앞이고 각자 바깥을 본다**(2026-09-09). 여기서 앞줄 규칙을 그대로
        // 쓰면 고리의 앞쪽 한두 칸에만 화살이 붙어 **없는 앞을 있다고 그린다** — 사방에서
        // 들어올 때 쓰라고 만든 모양인데 그림이 *한 방향을 보고 선다*고 말하는 셈이다.
        if (shape == Formation.Shape.Ring)
        {
            var center = Vector3.zero;
            foreach (var s in spots) center += s;
            center /= spots.Count;

            foreach (var s in spots)
            {
                var out3 = s - center;
                out3.y = 0f;
                // 한가운데에 선 사람(고리가 한 칸일 때)은 바라볼 바깥이 없다 — 끈 방향을 준다.
                var f = out3.sqrMagnitude < 0.01f ? facing : out3.normalized;
                Square(s, f, Vector3.Cross(Vector3.up, f), FrontColor);
                Arrow(s, f, Vector3.Cross(Vector3.up, f));
            }
        }
        else
        {
            // **가장 앞줄만 다른 색이다.** 자리를 전부 같은 색으로 찍으면 *어디가 앞인가*를
            // 화살 하나로만 읽어야 하는데, 그건 무리가 커질수록 안 읽힌다.
            float front = float.MinValue;
            for (int i = 0; i < spots.Count; i++)
                front = Mathf.Max(front, Vector3.Dot(spots[i], facing));

            for (int i = 0; i < spots.Count; i++)
            {
                bool isFront = Vector3.Dot(spots[i], facing) >= front - Formation.Spacing * 0.5f;
                Square(spots[i], facing, side, isFront ? FrontColor : SlotColor);
                if (isFront) Arrow(spots[i], facing, side);
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(_verts);
        _mesh.SetColors(_cols);
        _mesh.SetIndices(_idx, MeshTopology.Lines, 0);

        // 선은 얇아서 자동 경계가 카메라 밖 판정이면 **통째로 사라진다** — 직접 잡아 준다
        // (격자가 먼저 겪은 자리).
        var b = new Bounds(spots[0], Vector3.zero);
        for (int i = 1; i < spots.Count; i++) b.Encapsulate(spots[i]);
        b.Expand(ArrowLen * 2f);
        _mesh.bounds = b;

        _renderer.enabled = true;
    }

    void Square(Vector3 c, Vector3 f, Vector3 s, Color col)
    {
        c.y += Lift;
        Vector3 a = c + (f + s) * Half, b = c + (f - s) * Half;
        Vector3 d = c - (f + s) * Half, e = c - (f - s) * Half;
        Line(a, b, col); Line(b, d, col); Line(d, e, col); Line(e, a, col);
    }

    void Arrow(Vector3 c, Vector3 f, Vector3 s, float len = ArrowLen)
    {
        c.y += Lift;
        var tip = c + f * len;
        Line(c, tip, FrontColor);
        Line(tip, tip - f * 0.4f + s * 0.25f, FrontColor);
        Line(tip, tip - f * 0.4f - s * 0.25f, FrontColor);
    }

    void Line(Vector3 a, Vector3 b, Color col)
    {
        _idx.Add(_verts.Count); _verts.Add(a); _cols.Add(col);
        _idx.Add(_verts.Count); _verts.Add(b); _cols.Add(col);
    }
}
