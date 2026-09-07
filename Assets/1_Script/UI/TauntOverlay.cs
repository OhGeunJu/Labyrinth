using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// **도발당한 폰과 그를 붙든 놈을 선으로 잇는다**(2026-09-07, 사용자 결정).
//
// 적이 능동을 쓰게 만들자마자 물음이 하나 생겼다: *"내가 궁수를 찍었는데 왜 딴 놈을 치지"*.
// 도발은 표적 선정을 **덮어쓰는 것**이라 그렇게 보이는 것이 정상인데, **그 사실이 화면에
// 없으면 조작이 고장 난 것과 구별되지 않는다.**
//
// 림월드의 적도 능동을 쓴다(임피드 화염 · 사이캐스터 광폭화 — 광폭화는 아예 조작을 뺏는다).
// 그런데도 억울하지 않은 이유는 **드물고 · 이름이 있고 · 보이기 때문**이다. 이름은 이미
// 있었고(`PawnStatusText`의 `도발당함`), **없던 것은 누가 끌고 있는지**다.
//
// **손으로 찍은 표적을 지켜 주는 길은 안 골랐다**(사용자 결정) — 그쪽은 안전하지만 도발이
// 약해진다. 뺏기는 것 자체는 두고 **뺏긴 것이 읽히게** 만든다.
public class TauntOverlay : MonoBehaviour
{
    // **씬 배선이 없다** — 저작할 것이 없고(색은 규격이다) 아무 상태도 안 든다.
    // `AbilityController`·`DraftController`와 같은 자리다. 씬에 세우게 하면 맵마다 배선을
    // 반복하게 되고, 하나 빠뜨리면 **그 맵에서만 표시가 없다.**
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("TauntOverlay") { hideFlags = HideFlags.DontSave };
        go.AddComponent<TauntOverlay>();
        DontDestroyOnLoad(go);
    }

    // **한 줄이 아니라 흐르는 토막들이다**(2026-09-08, 사용자: *"레이캐스트 선보다는 좀 더
    // 파티클 느낌이었으면"*).
    //
    // **파티클 시스템을 안 쓴다.** 도발 하나마다 `ParticleSystem`을 세우면 오브젝트가 늘고,
    // 수명·방출·중력이라는 저작거리가 딸려 오는데 **여기서 바라는 것은 흐름 하나**다.
    // 메시로 그리면 지금 쓰는 오버레이 재질을 그대로 쓴다(깊이 판정이 꺼져 있어 바위 뒤에서도
    // 보인다 — 파티클로는 그걸 다시 맞춰야 한다).
    //
    // 대신 **같은 선을 토막으로 끊고 시간에 따라 흘린다.** 토막이 끌려가는 쪽에서 붙든 쪽으로
    // 밀려가고, 앞머리가 밝고 꼬리가 흐리다 — 그것이 곧 *어느 방향으로 끌리는지*다.
    // 화살표를 그리는 길도 있는데 **촉이 줌을 타고 각도마다 달리 보인다.**
    // **누가 붙들었는지로 색이 갈린다**(2026-09-08). 한 색이면 *내 탱커가 셋을 끌어온 것*과
    // *적이 내 폰 셋을 끌어간 것*이 화면에서 똑같이 보인다 — 둘 다 **내 폰에 선이 모이는
    // 그림**이라(사용자: *"우리팀한테도 선이 생기는 거 같아"*).
    //
    // 붙든 쪽이 내 편이면 **푸르게**(내가 끌어온 것 = 좋은 일), 남이면 **붉게**(끌려가는 것).
    static readonly Color Mine  = new(0.35f, 0.8f, 1f, 1f);
    static readonly Color Theirs = new(1f, 0.35f, 0.2f, 1f);

    const float DashLen  = 0.55f;   // 토막 길이(m)
    const float DashGap  = 0.85f;   // 토막 간격(m) — 토막보다 커야 끊겨 보인다
    const float FlowSpeed = 2.6f;   // 흐르는 속도(m/s)
    const float HeadAlpha = 0.95f;  // 앞머리
    const float TailAlpha = 0.15f;  // 꼬리

    // **굵기를 가지려면 선이 아니라 면이어야 한다**(2026-09-08, 사용자: *"선이 더 굵었으면"*).
    // `MeshTopology.Lines`는 **언제나 한 픽셀**이다 — GPU가 굵기를 안 받는다(그래서 이 저장소의
    // 격자·영역 원도 가늘다. 저쪽은 *경계를 알려 주는 실선*이라 그게 맞다).
    //
    // 그래서 토막마다 **카메라를 보는 사각형**을 굽는다. 면이 되면 `_Cull Off`가 필요한데
    // `OverlayMaterial`이 이미 그렇다(깊이 판정도 꺼져 있어 바위 뒤에서도 보인다).
    //
    // **너비가 거리를 탄다** — 세계 단위로 못 박으면 당겼을 때 밧줄처럼 굵고 물러나면 실이
    // 된다. 카메라에서 먼 만큼 넓혀 **화면에서 대충 같은 굵기**로 두되, 바닥(`MinWidth`)을
    // 둬서 아주 가까이서도 납작해지지 않게 한다.
    const float MinWidth     = 0.10f;
    const float WidthPerDist = 0.006f;   // 25m에서 0.15m · 60m에서 0.36m

    MeshRenderer _renderer;
    MeshFilter   _filter;
    Mesh         _mesh;
    Material     _mat;

    readonly List<Vector3> _verts = new();
    readonly List<Color>   _cols  = new();
    readonly List<int>     _idx   = new();
    readonly List<UnitBlackboard> _dead = new();

    void Awake()
    {
        _filter   = gameObject.AddComponent<MeshFilter>();
        _renderer = gameObject.AddComponent<MeshRenderer>();

        _mesh = new Mesh { name = "TauntStreams" };
        _mesh.MarkDynamic();
        _filter.sharedMesh = _mesh;

        // 격자·영역 원과 **한 벌**이다(`OverlayMaterial`): 정점 색 · 알파 · 깊이 판정 없음.
        // 깊이를 켜면 **바위 뒤의 선이 사라져** 정작 벽 너머에서 끌려가는 것을 못 본다.
        _mat = OverlayMaterial.Create();
        _renderer.sharedMaterial    = _mat;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows    = false;
    }

    void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_mat  != null) Destroy(_mat);
    }

    // **`LateUpdate`다** — 폰이 다 움직인 뒤라야 선이 몸에 붙는다. `Update`에서 그리면
    // 한 프레임 뒤처져서 뛰는 폰의 선이 등 뒤에 매달린다.
    void LateUpdate()
    {
        // 지도를 보는 중에는 안 그린다 — 필드의 표시가 월드맵 위에 뜬다.
        bool draw = GameModeManager.IsField;

        _verts.Clear(); _cols.Clear(); _idx.Clear(); _dead.Clear();

        var held = Taunt.Held;
        for (int i = 0; i < held.Count; i++)
        {
            var bb = held[i];

            // **진짜 걸려 있는지는 `HolderOf`가 답한다** — 여기서 시간을 다시 재면 답이
            // 두 벌이 되고, 그때부터 "선은 그려지는데 안 끌려간다"가 생긴다.
            var holder = bb != null ? Taunt.HolderOf(bb) : null;
            if (bb == null || bb.Self == null || holder == null) { _dead.Add(bb); continue; }
            if (!draw) continue;

            // **몸통 자리는 콜라이더에게 묻는다**(`PawnPlacement.CenterOffset`) — 상수로 박으면
            // 늑대는 선이 등 위로 뜨고 곰은 배를 가른다. 피벗이 발밑이라 안 얹으면 발끝을 잇는다.
            Vector3 a = bb.Self.transform.position;
            Vector3 b = holder.transform.position;
            a.y += PawnPlacement.CenterOffset(bb.Self);
            b.y += PawnPlacement.CenterOffset(holder);

            // **붙든 쪽의 세력이 색을 정한다** — 끌려가는 쪽이 아니다(내 탱커가 적을 끌면
            // 끌려가는 쪽은 적이지만 그건 **내가 한 일**이다).
            var hf = holder.GetComponent<Faction>();
            Flow(a, b, hf != null && hf.IsPlayer ? Mine : Theirs);
        }

        // 끌려가는 쪽(`from`)에서 붙든 쪽(`to`)으로 토막을 흘린다.
    //
    // **흐름은 `unscaledTime`이 아니라 `time`이다** — 멈춰 세우면 같이 멎어야 화면이
    // *"지금 멈춰 있다"*를 말한다(카메라가 `unscaledDeltaTime`을 쓰는 것과 **다른 축**이다:
    // 저쪽은 멈춰 놓고 둘러보는 조작이고, 이건 세상에서 벌어지는 일의 표시다).
    void Flow(Vector3 from, Vector3 to, Color warm)
    {
        Vector3 span = to - from;
        float   len  = span.magnitude;
        if (len < 0.01f) return;

        Vector3 dir  = span / len;
        float   step = DashLen + DashGap;

        // 카메라를 향해 눕힌다. 카메라가 없으면(캡처 중 등) 그리지 않는다 — 방향을 짐작하면
        // 각도에 따라 면이 사라져 **깜빡이는 것처럼** 보인다.
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 mid    = (from + to) * 0.5f;
        Vector3 toCam  = cam.transform.position - mid;
        Vector3 side   = Vector3.Cross(dir, toCam);
        if (side.sqrMagnitude < 1e-6f) return;   // 선을 정면으로 내려다보는 순간
        side.Normalize();

        float half = Mathf.Max(MinWidth, toCam.magnitude * WidthPerDist) * 0.5f;

        // 시작점을 시간으로 민다. **`Repeat`으로 감는다** — 그냥 더하면 토막이 선 끝에서
        // 사라지는 자리가 시간에 따라 옮겨 다녀 **길이가 들쭉날쭉해 보인다.**
        float offset = Mathf.Repeat(Time.time * FlowSpeed, step);

        for (float d = offset - step; d < len; d += step)
        {
            float s = Mathf.Max(d, 0f);
            float e = Mathf.Min(d + DashLen, len);
            if (e <= s) continue;

            // **앞머리가 밝다** — 토막 안에서 밝기를 눕히면 굵기 없이 방향이 읽힌다.
            var head = warm; head.a = HeadAlpha;
            var tail = warm; tail.a = TailAlpha;

            Vector3 p0 = from + dir * s, p1 = from + dir * e;
            int b0 = _verts.Count;

            _verts.Add(p0 - side * half); _cols.Add(tail);
            _verts.Add(p0 + side * half); _cols.Add(tail);
            _verts.Add(p1 + side * half); _cols.Add(head);
            _verts.Add(p1 - side * half); _cols.Add(head);

            _idx.Add(b0); _idx.Add(b0 + 1); _idx.Add(b0 + 2);
            _idx.Add(b0); _idx.Add(b0 + 2); _idx.Add(b0 + 3);
        }
    }

    // 풀린 것은 볼 때 걷어낸다 — 도발은 시간으로 풀리므로 *풀리는 순간*을 아는 코드가
        // 없다. 그래서 **지울 때가 아니라 볼 때** 정리하는 것이 유일하게 안 새는 길이다.
        for (int i = 0; i < _dead.Count; i++) Taunt.Forget(_dead[i]);

        _mesh.Clear();
        if (_verts.Count == 0) { _renderer.enabled = false; return; }

        _renderer.enabled = true;
        _mesh.SetVertices(_verts);
        _mesh.SetColors(_cols);
        _mesh.SetIndices(_idx, MeshTopology.Triangles, 0);

        // **경계를 손으로 잡는다** — 선은 얇아 자동 경계가 카메라 밖으로 판정되면 통째로
        // 사라진다(`GridOverlay`·`RangeCircle`이 같은 자리에서 겪었다).
        var bounds = new Bounds(_verts[0], Vector3.zero);
        for (int i = 1; i < _verts.Count; i++) bounds.Encapsulate(_verts[i]);
        bounds.Expand(1f);
        _mesh.bounds = bounds;
    }
}
