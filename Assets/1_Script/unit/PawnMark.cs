using UnityEngine;

// **폰 머리 위의 표식 — 답은 여기 하나다**(2026-09-09).
//
// 처음 필요해진 자리는 습격대의 지휘관이었다. 2026-09-08에 무리마다 두목이 생겼고
// (`RaidLord.PromoteLeader` — 이미 가장 센 놈을 골라 수준을 세 칸 올려 다시 굴린다),
// 09-09에 습격이 **여러 갈래로** 들어오게 됐다. 그래서야 *무엇을 먼저 쳐야 하나*가 실제
// 결정이 되는데 **두목이 어느 갈래에 있는지는 눌러 봐야 알았다** — 갈래가 셋이면 그건
// 판단이 아니라 뽑기다.
//
// **그런데 만들 물건은 "습격 두목 표식"이 아니다.** 그렇게 지으면 다음 표식(퀘스트를 주는
// 사람 · 지목한 표적 · 계약 중인 용병)이 저마다 메시와 재질과 *언제 감추나*를 다시 갖는다 —
// 이 저장소가 표로 관리하는 그 사고다. 그래서 여기가 답하는 것은 **"폰 위에 표식 하나를
// 띄운다"**이고, *왜 띄우나*는 `Kind` 한 줄로 들어온다.
//
// == 표식을 하나 늘리는 법 ==================================================
//
// `Kind`에 값을 **꼬리에** 붙이고 `Look`에 줄 하나를 더한다. 그게 전부다 — 색과 크기만
// 갈리고 나머지(머리 높이 · 부모 크기 상쇄 · 콜라이더 없음 · 그림자 없음 · 깊이 판정 ·
// 빛 안 탐 · 쓰러지면 거둠)는 여기 한 벌이다.
//
// **재질을 종류마다 만들지 않는다** — 여덟 벌을 손으로 맞추게 되고 그중 하나는 반드시
// 뒤처진다. 재질도 메시도 한 벌이고 **색만 `MaterialPropertyBlock`으로 얹는다**
// (`RockRenderer`가 바이옴 색조에서 쓰는 그 규칙이다).
//
// == 어기면 조용히 깨지는 것 ================================================
//
//   - **콜라이더를 안 단다.** 커서 레이캐스트는 마스크 없이 전부를 훑으므로, 남기면
//     **표식이 붙은 폰을 누를 때마다 표식이 잡혀** 정작 그 폰을 못 고른다(`BuildingRoof`가
//     지붕에서, `CeilingRenderer`가 천장에서 먼저 겪은 자리다).
//   - **깊이 판정을 안 끈다.** 여기서 `OverlayMaterial`을 쓰면 **바위를 뚫고 보인다** —
//     마주치지도 않은 두목의 자리를 벽 너머로 알려 주는 것은 표시가 아니라 투시다.
//     이 표식은 *보이는 것들 중 어느 것인가*에만 답한다.
//   - **빛을 안 탄다**(`Unlit/Color`). 지하는 밝기 배율이 어느 때에도 1이 아니라
//     (0.30~0.62) 보통 재질로 두면 **어두운 굴에서 표식만 검게 눌린다.**
//   - **`Light`를 안 단다.** `Vision`이 `LightSource` 레지스트리를 읽으므로 등을 달면
//     **그 폰 둘레만 실제로 더 잘 보이게 된다** — 표식이 판정을 바꾸면 안 된다.
//   - **부모 크기를 상쇄한다.** 자식으로 달면 몸집(`PawnSpec.BodyScale`)이 곱해져 **작은
//     종족의 표식만 작아진다.** 표식은 폰의 일부가 아니라 화면의 것이라 크기가 한 벌이어야
//     한다(`PawnCarry`가 어깨 자리에서 먼저 겪었다).
//   - **그림자를 안 던진다.**
[DisallowMultipleComponent]
public class PawnMark : MonoBehaviour
{
    // **왜 띄우나.** 값은 **꼬리에 붙인다** — 저작 에셋이 아직 이걸 안 들지만, 언젠가
    // 들게 되면 중간에 끼운 값 하나가 표식을 통째로 뒤바꾼다(이 저장소의 enum 규칙).
    public enum Kind
    {
        Leader,   // 무리를 이끄는 자 — 먼저 칠 대상
    }

    // 종류가 정하는 것은 **색과 크기뿐**이다.
    readonly struct Style
    {
        public readonly Color Color;
        public readonly float Scale;
        public Style(Color color, float scale) { Color = color; Scale = scale; }
    }

    // **이미 쓰는 색과 안 겹쳐야 한다** — 초록은 *내가 지정한 것*(운반·채집·선택), 주황은
    // *캘 자리*, 호박은 *고른 진형*이다. 두목은 진홍.
    static Style Look(Kind kind) => kind switch
    {
        _ => new Style(new Color(0.88f, 0.13f, 0.16f, 1f), 1f),
    };

    const float Radius = 0.26f;   // 표식의 반지름(m)
    const float Height = 0.62f;   // 위아래 끝까지(m)
    const float Gap    = 0.34f;   // 머리 위 여유(m)

    // 0.4초마다 본다 — 폰 하나에 하나뿐이라 값이 없다시피 하고, 쓰러진 폰 위에 표식이 몇
    // 프레임 더 떠 있는 것은 눈에 안 띈다.
    const float TickInterval = 0.4f;

    static Mesh     _sharedMesh;
    static Material _sharedMat;

    UnitBrain               _brain;
    GameObject              _mark;
    MeshRenderer            _renderer;
    MaterialPropertyBlock   _mpb;
    Kind                    _kind;
    float                   _next;

    // ── 부르는 쪽 ────────────────────────────────────────────────────────────

    // **띄운다.** 이미 떠 있으면 종류만 갈아 끼운다 — 두 번 불러도 표식이 겹쳐 서지 않는다
    // (지도에서 걸어온 무리와 이벤트 습격은 **세우는 문이 다르다**).
    public static void Show(GameObject pawn, Kind kind)
    {
        if (pawn == null) return;

        var mark = pawn.GetComponent<PawnMark>();
        if (mark == null) mark = pawn.AddComponent<PawnMark>();
        mark.SetKind(kind);
    }

    // **거둔다.** 표식이 붙은 채로 뜻이 사라지는 경우가 있다 — 회유해 온 두목이 그렇다.
    public static void Hide(GameObject pawn)
    {
        var mark = pawn != null ? pawn.GetComponent<PawnMark>() : null;
        if (mark != null) Destroy(mark);
    }

    // ── 안쪽 ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        _brain = GetComponent<UnitBrain>();
        Build();
        Paint();
    }

    void SetKind(Kind kind)
    {
        _kind = kind;
        if (_mark != null) Paint();   // `Awake` 전이면 그쪽이 칠한다
    }

    void Build()
    {
        // **머리 높이는 콜라이더가 답한다** — 상수로 박으면 늑대·곰처럼 몸이 낮은 폰의
        // 표식이 허공에 뜬다(`PawnPlacement.CenterOffset`이 같은 이유로 상수가 아니다).
        var cc  = GetComponent<CharacterController>();
        float h = cc != null ? cc.height : PawnMover.BodyHeight;

        _mark = new GameObject("Mark");
        _mark.transform.SetParent(transform, false);
        _mark.transform.localPosition = new Vector3(0f, h + Gap, 0f);
        _mark.transform.localScale    = Invert(transform.lossyScale);

        _mark.AddComponent<MeshFilter>().sharedMesh = SharedMesh();

        _renderer = _mark.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial    = SharedMaterial();
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows    = false;
        // **콜라이더는 애초에 안 생긴다** — `MeshRenderer`만 붙인다.
    }

    // **색만 얹는다 — 재질을 종류마다 만들지 않는다.**
    //
    // `MaterialPropertyBlock`을 **필드 초기화자에서 만들지 말 것**: 정적 초기화자는 정적
    // 생성자에서 도는데 그 시점이 `MonoBehaviour` 생성 도중일 수 있어 유니티가 막고,
    // **정적 생성자가 던지면 그 클래스가 통째로 죽는다**(화면에는 *"표식이 안 뜬다"*로만
    // 보인다 — `RockRenderer`가 벽 색조에서 먼저 겪었다).
    void Paint()
    {
        if (_renderer == null) return;
        _mpb ??= new MaterialPropertyBlock();

        var look = Look(_kind);
        _mpb.SetColor("_Color",     look.Color);   // 빌트인 `Unlit/Color`
        _mpb.SetColor("_BaseColor", look.Color);   // URP 폴백 — 읽는 이름이 다르다
        _renderer.SetPropertyBlock(_mpb);

        var s = Invert(transform.lossyScale) * look.Scale;
        _mark.transform.localScale = s;
    }

    void Update()
    {
        if (_mark == null) return;
        if (Time.time < _next) return;
        _next = Time.time + TickInterval;

        // **쓰러졌으면 거둔다.** `BecomeCorpse`는 `UnitBrain`을 **끄기만** 하므로
        // `isActiveAndEnabled`가 곧 "살아서 서 있나"다(`RaidLord.Standing`과 같은 식).
        // 표식이 시체 위에 남으면 *아직 저기 서 있다*고 말하는 셈이다.
        bool show = _brain == null || _brain.isActiveAndEnabled;
        if (show && _brain != null)
        {
            var bb = _brain.Blackboard;
            if (bb != null && (bb.IsKnockedOut || bb.IsPrisoner)) show = false;
        }

        if (_mark.activeSelf != show) _mark.SetActive(show);
    }

    void OnDestroy()
    {
        if (_mark != null) Destroy(_mark);
    }

    static Vector3 Invert(Vector3 s) => new(
        Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x,
        Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y,
        Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z);

    // ── 그리는 것 ────────────────────────────────────────────────────────────

    // **처음 쓸 때 만든다** — 위 `Paint`의 주석과 같은 이유다. 메시도 재질도 네이티브
    // 자원이라 정적 초기화자에서 만들면 안 된다.
    static Material SharedMaterial()
    {
        if (_sharedMat != null) return _sharedMat;

        // **`Unlit/Color`는 `RuntimeShaders.AlwaysInclude`에 있다** — 목록에 없는 이름을
        // 쓰면 빌드에서만 조용히 검게 나오고 에디터에서는 끝까지 재현이 안 된다.
        var sh = RuntimeShaders.Find(RuntimeShaders.UnlitColor, RuntimeShaders.Unlit);
        _sharedMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        return _sharedMat;
    }

    static Mesh SharedMesh()
    {
        if (_sharedMesh != null) return _sharedMesh;

        // 위아래로 뾰족한 마름모. **어느 각도에서 봐도 같은 물건으로 읽힌다** — 카메라가
        // 내려다보는 화면이라 납작한 삼각형 한 장은 위에서 보면 **선 하나로 사라지고**,
        // 그래서 빌보드를 돌리게 된다(매 프레임 도는 코드가 하나 느는 일이다).
        float r = Radius, h = Height * 0.5f;

        var v = new[]
        {
            new Vector3(0f, -h, 0f),   // 0 아래 꼭짓점
            new Vector3(0f,  h, 0f),   // 1 위 꼭짓점
            new Vector3( r, 0f, 0f),   // 2 허리 넷
            new Vector3(0f, 0f,  r),   // 3
            new Vector3(-r, 0f, 0f),   // 4
            new Vector3(0f, 0f, -r),   // 5
        };

        // **양쪽 감김을 다 굽는다.** `Unlit/Color`는 `_Cull`을 안 내놓아 재질로는 못 끄고,
        // 면마다 모서리 순서를 손으로 맞추면 **면을 하나 더할 때마다 또 틀린다**(바위
        // 셰이더가 `_Cull Off`로 간 그 이유). 정점 여섯짜리라 두 벌을 구워도 값이 없다.
        var tri = new System.Collections.Generic.List<int>(48);
        for (int i = 0; i < 4; i++)
        {
            int a = 2 + i, b = 2 + (i + 1) % 4;
            Both(tri, 1, a, b);   // 위쪽 면
            Both(tri, 0, b, a);   // 아래쪽 면
        }

        _sharedMesh = new Mesh { name = "PawnMark", hideFlags = HideFlags.HideAndDontSave };
        _sharedMesh.SetVertices(v);
        _sharedMesh.SetTriangles(tri, 0);
        _sharedMesh.RecalculateNormals();
        _sharedMesh.RecalculateBounds();
        return _sharedMesh;
    }

    static void Both(System.Collections.Generic.List<int> tri, int a, int b, int c)
    {
        tri.Add(a); tri.Add(b); tri.Add(c);
        tri.Add(a); tri.Add(c); tri.Add(b);
    }
}
