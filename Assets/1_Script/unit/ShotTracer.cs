using System.Collections.Generic;
using UnityEngine;

// **쏜 것이 보인다 — 쏘는 쪽 가슴에서 맞는 쪽 가슴으로 한 줄**(2026-08-24, 사용자 요청).
//
// 그전까지 원거리 전투는 **화면에 아무것도 안 일어났다**: 로그에만 `방어 성공`·`회피 성공`이
// 쌓이고 눈으로는 두 폰이 그냥 서 있었다. 그래서 "왜 안 맞지"를 콘솔을 열어야 알 수 있었고,
// 명중률을 손보는 동안에도 **고친 것이 화면에 보이지 않았다.**
//
// ## 짧은 화살대가 **날아간다** — 그러나 판정을 나르지는 않는다
//
// 처음엔 쏘는 쪽에서 맞는 쪽까지 **선을 통째로 한 번 번쩍**이고 지웠다. 판정이 이미 끝나
// 있으니 그게 정직하긴 한데, **쏜 것처럼 안 보인다** — 화면에 남는 것이 "선이 있었다"뿐이라
// 어느 쪽이 쏜 것인지도 안 읽힌다(사용자: *"진짜 석궁쏘는 느낌이 나는 것처럼 하고 싶었는데"*).
//
// 지금은 **짧은 화살대가 경로를 따라 간다.** 판정은 여전히 쏘는 순간 끝나 있고(`DealRangedDamage`)
// 이건 *이미 벌어진 일*을 그리는 그림이다 — **도착해서 피해를 주지 않는다.** 그 둘을 묶으면
// 표적이 그새 움직이거나 죽었을 때 **화살이 허공을 지나가는데 피가 깎이는** 어긋남이 생긴다.
//
// 나는 시간은 **거리에 비례한다**(`Speed`) — 멀수록 오래 난다. 고정 시간으로 두면 코앞에서
// 쏜 것과 사거리 끝에서 쏜 것이 같은 속도로 보여 거리가 화면에서 사라진다.
//
// ## 자리는 몸통이다
//
// 피벗이 발밑이라(`PawnPlacement.CenterOffset`) 그냥 `transform.position`을 이으면 **땅바닥을
// 기는 선**이 된다. 높이가 종족마다 다르므로(늑대 1.0 · 곰 1.4 · 사람 2.0) 상수로 박지 않는다.
[DisallowMultipleComponent]
public class ShotTracer : MonoBehaviour
{
    const float Speed  = 42f;     // m/s — 사거리 14m를 0.33초에 난다
    const float Length = 1.1f;    // 화살대 길이. 점으로 두면 무엇이 나는지 안 보인다
    const float Width  = 0.05f;
    const float MinLife = 0.06f;  // 코앞에서 쏴도 한 프레임에 사라지지 않게

    // 아군 오사가 눈에 띄어야 한다 — 같은 색이면 누가 누구를 쐈는지 안 보인다.
    static readonly Color Normal   = new(1f, 0.93f, 0.6f, 0.9f);
    // **빗나간 화살은 색이 다르다** — 맞은 것이 동료든 옆에 선 다른 적이든, 이 색이
    // 말하는 것은 *"겨눈 데로 안 갔다"* 하나다.
    static readonly Color Stray    = new(1f, 0.45f, 0.35f, 0.95f);

    // **한 발마다 오브젝트를 만들지 않는다** — 난전에서는 초당 수십 발이고, 그때마다
    // `LineRenderer` + 머티리얼이 생겼다 사라지면 GC가 튄다(`ResearchLinkGraphic`이 선마다
    // `Image`를 두지 않은 것과 같은 자리).
    static readonly Stack<ShotTracer> Pool = new();
    static Material _mat;

    LineRenderer _line;
    Vector3      _from, _to;
    float        _age, _life;
    Color        _color;

    public static void Show(Component shooter, GameObject target, bool stray = false)
    {
        if (shooter == null || target == null) return;
        Vector3 from = Chest(shooter.gameObject);
        Vector3 to   = Chest(target);
        Show(from, to, stray);
    }

    public static void Show(Vector3 from, Vector3 to, bool stray = false)
    {
        // **풀은 `static`인데 오브젝트는 씬과 함께 죽는다.** 씬을 다시 열면 풀에 파괴된
        // 것이 남아 `Pop`이 죽은 참조를 준다 — 그냥 돌아서면 **그만큼의 화살이 조용히
        // 안 그려진다**(풀이 빌 때까지 한 발씩). 죽은 것은 버리고 계속 꺼낸다.
        ShotTracer t = null;
        while (t == null && Pool.Count > 0) t = Pool.Pop();
        if (t == null) t = Create();
        if (t == null) return;

        t._color = stray ? Stray : Normal;
        t._from  = from;
        t._to    = to;
        t._age   = 0f;
        t._life  = Mathf.Max(MinLife, Vector3.Distance(from, to) / Speed);
        t.gameObject.SetActive(true);
        t.Paint(1f);
        t.Place(0f);
    }

    static Vector3 Chest(GameObject pawn) =>
        pawn.transform.position + Vector3.up * PawnPlacement.CenterOffset(pawn);

    static ShotTracer Create()
    {
        // **머티리얼은 한 벌이다** — 깊이 판정 없이 정점 색으로 그리는 그 물건이 이미 있다
        // (`OverlayMaterial`). 여기서 또 만들면 셰이더 고르는 규칙이 두 벌이 된다.
        //
        // **깊이를 안 보는 것이 맞다** — 이건 세상에 놓인 물건이 아니라 *무슨 일이 있었나*를
        // 알려 주는 표시라, 나무 뒤에서 쏜 화살도 보여야 한다(격자·영역 원과 같은 판단).
        _mat ??= OverlayMaterial.Create();

        var go = new GameObject("[ShotTracer]");
        var t  = go.AddComponent<ShotTracer>();
        var lr = go.AddComponent<LineRenderer>();

        lr.useWorldSpace   = true;
        lr.positionCount   = 2;
        lr.widthMultiplier = Width;
        lr.numCapVertices  = 0;
        lr.alignment       = LineAlignment.View;   // 어느 각도에서 봐도 같은 굵기
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows  = false;
        lr.sharedMaterial  = _mat;

        t._line = lr;
        return t;
    }

    void Update()
    {
        // **`unscaledDeltaTime`이다** — `GameTimeManager`가 `Time.timeScale`을 만지므로,
        // 그냥 `deltaTime`을 쓰면 멈춘 동안 선이 **화면에 얼어붙어 남고** 3배속에서는
        // 깜빡이지도 못하고 사라진다(`CameraController`가 같은 이유로 같은 축을 쓴다).
        _age += Time.unscaledDeltaTime;
        if (_age >= _life) { Release(); return; }
        Place(_age / _life);
    }

    // 화살대의 **꼬리**가 0에서 1까지 간다 — 머리가 먼저 도착하고 꼬리가 따라 들어가야
    // *날아가 꽂히는* 그림이 된다(머리를 기준으로 잡으면 끝에서 화살이 표적을 지나쳐 나간다).
    void Place(float t)
    {
        Vector3 d    = _to - _from;
        float   dist = d.magnitude;
        if (dist < 0.001f) { _line.SetPosition(0, _from); _line.SetPosition(1, _to); return; }

        Vector3 dir  = d / dist;
        float   tail = t * dist;
        float   head = Mathf.Min(dist, tail + Length);
        _line.SetPosition(0, _from + dir * tail);
        _line.SetPosition(1, _from + dir * head);
    }

    void Paint(float alpha)
    {
        var c = _color; c.a *= alpha;
        _line.startColor = c;
        _line.endColor   = c;
    }

    void Release()
    {
        gameObject.SetActive(false);
        Pool.Push(this);
    }
}
