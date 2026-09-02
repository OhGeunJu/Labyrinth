using UnityEngine;

// **지붕과 페이드는 한 벌이다**(2026-08-28).
//
// 걸어 들어가는 건물에 지붕을 씌우면 **그 순간 안이 안 보인다** — 위에서 내려다보는
// 화면이라 지붕이 곧 뚜껑이다. 안에 놓은 침대·작업대도, 거기서 일하는 폰도 볼 방법이
// 없어진다. 그래서 지붕만 먼저 얹는 것은 기능을 하나 빼는 일이고, **언제 사라지는가**가
// 같이 와야 물건이 성립한다.
//
// ── 언제 걷히나 ────────────────────────────────────────────────────────────
//
// 두 가지다. 둘 다 *"지금 안을 봐야 한다"*는 뜻이다:
//
//  · **안에 내 폰이 있을 때** — 무엇을 하고 있는지 보여야 한다. 남의 폰은 안 센다
//    (지나가는 상인 때문에 내 집 지붕이 벗겨질 이유가 없다).
//  · **건설 모드일 때** — 안에 무엇을 놓을지 고르는 중이다. `WorkSpot` 표식이
//    같은 신호로 뜨고 지는 것과 같은 자리다.
//
// **끄고 켜는 것이 아니라 흐려진다.** 딱 사라지면 폰이 문을 넘는 순간 화면이 튀고,
// *"지붕이 있었나"*가 헷갈린다. 흐려지면 **거기 지붕이 있다는 것을 알면서 속을 본다.**
//
// ── 벽도 같이 걷는다, 다만 앞의 것만 ───────────────────────────────────────
//
// 지붕만 걷으면 **비스듬한 카메라에서는 방 아래쪽 절반이 앞 벽에 가린다.** 그렇다고 벽을
// 다 걷으면 건물의 테두리가 사라져 **무엇이 어디까지인지 안 보인다** — 방이 아니라 바닥에
// 가구가 흩어진 그림이 된다.
//
// 그래서 **바깥 면이 카메라를 향한 벽만** 흐린다. 먼 쪽 벽은 불투명하게 남아 방의 뒷벽
// 노릇을 하고, 앞 벽은 비켜 준다. 카메라를 돌리면 흐려지는 벽도 따라 바뀐다.
//
// **어느 쪽이 바깥인가는 판 자신이 안다** — 얇은 축이 곧 벽이 마주 보는 축이고, 건물
// 한복판에서 그 축으로 어느 쪽에 있느냐가 부호다. 이름이나 순서에 기대지 않는다.
[DisallowMultipleComponent]
public class BuildingRoof : MonoBehaviour
{
    [Tooltip("지붕 오브젝트. 이 아래의 렌더러가 **다 같이** 흐려진다.")]
    [SerializeField] GameObject roof;

    [Tooltip("벽 오브젝트. 이 아래의 판은 **카메라를 향한 것만** 흐려진다. 비워도 된다.")]
    [SerializeField] GameObject walls;

    [Tooltip("이보다 카메라 쪽을 향하면 앞 벽으로 본다(수평 성분). 0에 가까울수록 많이 걷힌다.")]
    [Range(-0.9f, 0f)] [SerializeField] float facingThreshold = -0.05f;

    // **0.15는 너무 진했다**(2026-08-28) — 안을 들여다보는 것이 목적인데 그 위에 회색이
    // 한 겹 덮여 침대와 바닥 색이 죽는다. 0.05면 **윤곽만 남고 속이 제 색으로 보인다.**
    // 0으로는 안 내린다 — 지붕이 통째로 사라지면 *"여기 지붕이 있었나"*가 되고, 그러면
    // 페이드가 아니라 그냥 껐다 켜는 것이다.
    [Tooltip("걷혔을 때의 불투명도. 0이면 아예 사라져 **지붕이 있었는지도 모르게 된다.**")]
    [Range(0f, 0.6f)] [SerializeField] float fadedAlpha = 0.05f;

    [Tooltip("흐려지는 속도(초당). 너무 빠르면 문턱에서 깜빡인다.")]
    [SerializeField] float fadeSpeed = 5f;

    // **매 프레임 폰을 훑지 않는다** — 지붕은 건물마다 하나씩이고 폰은 계속 는다.
    // 문을 넘는 데 걸리는 시간보다 촘촘하면 충분하다.
    const float CheckInterval = 0.2f;

    // 흐려지는 조각 하나. **알파를 조각마다 따로 든다** — 지붕과 앞 벽과 뒤 벽이 서로
    // 다른 목표로 가므로 하나로 묶으면 셋이 같이 움직인다.
    class Piece
    {
        public Material Mat;
        public Vector3  Out;      // 바깥을 향하는 방향(건물 기준). 지붕은 위.
        public bool     Always;   // 지붕 — 방향을 안 본다
        public float    Alpha = 1f;
        public float    Target = 1f;
    }

    readonly System.Collections.Generic.List<Piece> _pieces = new();
    Camera _cam;
    float _nextCheck;

    void Awake()
    {
        if (roof == null && walls == null) { enabled = false; return; }

        Collect(roof,  always: true);
        Collect(walls, always: false);
        if (_pieces.Count == 0) { enabled = false; return; }

        // **지붕은 클릭을 막지 않는다** — 커서 레이캐스트가 마스크 없이 전부를 훑으므로
        // (`SelectionManager.PickVisible`), 콜라이더를 남겨 두면 **안에 있는 폰도 침대도
        // 지붕에 가려 못 고른다.** 벽은 콜라이더를 그대로 둔다 — 몸을 막는 것도, 건물을
        // 클릭해 고르는 것도 벽이 한다.
        if (roof != null)
            foreach (var c in roof.GetComponentsInChildren<Collider>(true)) Destroy(c);

        Apply();
    }

    void Collect(GameObject under, bool always)
    {
        if (under == null) return;

        foreach (var r in under.GetComponentsInChildren<Renderer>(true))
        {
            // **재질을 복제한다.** 공유 재질을 건드리면 세상의 모든 오두막이 같이 흐려진다.
            // 투명으로 바꾸는 식은 `FadeMaterial` 하나가 든다(청사진과 같은 답을 쓴다).
            var mat = FadeMaterial.CloneOf(r.sharedMaterial);
            r.sharedMaterial = mat;
            _pieces.Add(new Piece { Mat = mat, Always = always, Out = OutwardOf(r.transform) });
        }
    }

    // 그 판이 어느 쪽 바깥을 보는가(건물 기준). **얇은 축이 곧 마주 보는 축**이고,
    // 건물 한복판에서 그 축의 어느 쪽에 있느냐가 부호다.
    static Vector3 OutwardOf(Transform t)
    {
        var s = t.localScale;
        var p = t.localPosition;

        // 얇은 축을 고른다. 같으면(정사각 기둥) 더 멀리 나간 축으로 — 모서리 판이 그렇다.
        bool thinX = s.x < s.z - 0.001f;
        bool thinZ = s.z < s.x - 0.001f;
        if (!thinX && !thinZ) thinX = Mathf.Abs(p.x) >= Mathf.Abs(p.z);

        return thinX ? new Vector3(Mathf.Sign(p.x), 0f, 0f)
                     : new Vector3(0f, 0f, Mathf.Sign(p.z));
    }

    void OnDestroy()
    {
        foreach (var p in _pieces) if (p.Mat != null) Destroy(p.Mat);
    }

    void Update()
    {
        if (Time.time >= _nextCheck)
        {
            _nextCheck = Time.time + CheckInterval;
            Retarget();
        }

        bool moved = false;
        float step = fadeSpeed * Time.unscaledDeltaTime;
        foreach (var p in _pieces)
        {
            if (Mathf.Approximately(p.Alpha, p.Target)) continue;
            p.Alpha = Mathf.MoveTowards(p.Alpha, p.Target, step);
            moved = true;
        }
        if (moved) Apply();
    }

    void Retarget()
    {
        bool open = ShouldOpen();

        // 카메라의 **수평** 방향만 본다 — 내려다보는 각(피치)이 크면 그것이 판정을
        // 삼켜서 네 벽이 한꺼번에 흐려진다.
        if (_cam == null) _cam = Camera.main;
        var fwd = _cam != null ? _cam.transform.forward : Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.0001f) fwd.Normalize();

        foreach (var p in _pieces)
        {
            if (!open) { p.Target = 1f; continue; }
            if (p.Always) { p.Target = fadedAlpha; continue; }

            // 바깥 면이 카메라 쪽을 향하면 앞 벽이다.
            var outWorld = transform.TransformDirection(p.Out);
            p.Target = Vector3.Dot(outWorld, fwd) < facingThreshold ? fadedAlpha : 1f;
        }
    }

    // **`unscaledDeltaTime`을 쓴다** — `GameTimeManager`가 `timeScale`을 만지므로,
    // 멈춰 세운 채로 안을 들여다보려는 순간 지붕이 영영 안 걷힌다(멈추는 이유의 절반이
    // 그것이다). 카메라가 같은 이유로 같은 축을 쓴다.

    bool ShouldOpen()
    {
        // 건설 모드 — 안에 무엇을 놓을지 고르는 중이다.
        if (BuildModeManager.IsActive) return true;

        var map = MapManager.Instance;
        var data = GetComponent<PlacedBuilding>()?.Data;
        if (map == null || data == null) return false;

        // 안쪽 = 발자국에서 **막는 칸을 뺀 것**. 벽 칸에 선 폰은 안에 있는 것이 아니다.
        foreach (var f in Faction.All)
        {
            if (f == null || !f.IsPlayer) continue;   // 남의 폰 때문에 내 집이 벗겨지지 않는다

            var t = map.WorldToTile(f.transform.position);
            if (!Inside(data, t)) continue;
            return true;
        }
        return false;
    }

    bool Inside(BuildingData data, Vector2Int tile)
    {
        foreach (var t in PlacedBuilding.FootprintAt(transform.position, data.Size, transform.rotation))
            if (t == tile)
            {
                foreach (var b in PlacedBuilding.BlockingAt(transform.position, data, transform.rotation))
                    if (b == tile) return false;   // 벽 칸이다
                return true;
            }
        return false;
    }

    void Apply()
    {
        foreach (var p in _pieces) FadeMaterial.SetAlpha(p.Mat, p.Alpha);
    }

}
