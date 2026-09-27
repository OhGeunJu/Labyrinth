using System.Collections.Generic;
using UnityEngine;

// **다리 — 절벽을 가로지르는 걸을 수 있는 면**(2026-09-26, 사용자 결정 — 세부는 `docs/world.md` "절벽").
//
// **종류가 무엇이든 한 물건이다** — 돌다리(이끼·얼음·그림자)·나무다리·판자 다리가 전부 이 컴포넌트를 달고,
// 전부 **지을 수 있고** 전부 **자연에도 걸려 있을 수 있다**(사용자: *"돌다리도 건설할 수 있으면 좋겠는데 ·
// 나무다리나 다른 종류도 그냥 자연 스폰"*). 자연 다리는 *처음부터 지어져 있는 다리*일 뿐이다(`ChasmRenderer`).
//
// ── 무엇을 하나 ─────────────────────────────────────────────────────────────
//
// **발자국 가운데 절벽 칸에 "다리 윗면"을 등록한다**(`Chasm.RegisterDeck`). 그러면 `Chasm.Blocks`가 그 칸을
// 풀고 `MapManager.SurfaceHeight`·`ClimbLimitAt`이 다리 높이를 답한다 — **길찾기·건설·폰 발밑이 새 규칙 없이
// 따라온다**(기단이 *"딛는 면이 여기다"*를 알리는 것과 같은 자리). 기단 목록에 넣지 않는 이유는 `Chasm` 주석.
//
// ── 어기면 조용히 깨지는 것 ────────────────────────────────────────────────
//
// - **등록은 `Start`에서 한다** — 청사진과 고스트는 프리팹을 통째로 세우되 `Start` 전에 스크립트를 끈다.
//   `Awake`/`OnEnable`에서 등록하면 **짓기도 전에 다리가 건널 수 있는 것**이 된다.
// - **`OnDisable`에서 안 푼다** — 핵 영역 밖 건물은 휴면으로 컴포넌트가 꺼지는데(`BuildingDormancy`), 그때
//   풀면 **영역 밖의 다리가 끊긴다.** 푸는 것은 부서질 때뿐이다(`OnDestroy`).
// - **높이는 양 끝이 정하고 그 사이는 비스듬히 잇는다**(`SpanOf`) — 미리보기·청사진·완성품이 같은 답을 쓰도록
//   `IPlacementPose`로 선다.
public class Bridge : MonoBehaviour, IPlacementPose
{
    [Tooltip("다리 윗면을 이 높이만큼 가장자리보다 올린다(발이 땅에 안 묻히게).")]
    [SerializeField] float deckLift = 0.05f;

    // **발자국 크기를 제 손에 든다**(2026-09-26) — 미리보기(고스트)는 스크립트를 떼면서 `PlacedBuilding`도 떼므로
    // `Data`가 비고, 그러면 기본값(2×6)으로 발자국을 재 **엉뚱한 칸의 높이**를 읽었다: 허공 위에서는 그 칸에 땅이
    // 없어 높이가 0으로 잡혀 **미리보기가 떠 보였다.** `BridgeCreator`가 적는다(`World → J`).
    [SerializeField, HideInInspector] Vector2Int size;

    bool _registered;
    bool _natural;
    readonly List<Vector2Int> _deck = new();

    public bool IsNatural => _natural;

    // 자연 다리 — `ChasmRenderer`가 세우면서 부른다. 규칙은 같고 **값을 안 치렀을 뿐**이다.
    public void MarkNatural() => _natural = true;

    BuildingData Data => TryGetComponent<PlacedBuilding>(out var pb) ? pb.Data : null;

    Vector2Int Size
    {
        get
        {
            if (size.x > 0 && size.y > 0) return size;
            var data = Data;
            return data != null ? data.Size : new Vector2Int(2, 6);
        }
    }

    // 지금 자리의 발자국.
    IEnumerable<Vector2Int> Footprint()
    {
        return PlacedBuilding.FootprintAt(transform.position, Size, transform.rotation);
    }

    // ── 양 끝 높이로 비스듬히 놓는다 ─────────────────────────────────────────
    //
    // **처음엔 양 끝 중 높은 쪽으로 평평하게 깔았다** — 계단 바닥에서 한쪽 끝이 한 단 낮으면 다리 끝에서
    // 그 땅으로 내려가는 턱이 `StepUp`(0.5m)을 넘어 **건너고 나서 못 움직였다**(2026-09-26, 사용자가 겪었다).
    // 이제 **뒤 끝 높이에서 앞 끝 높이까지 고르게** 이어진다 — 칸마다 오르는 값이 `StepUp`보다 작으면 폰이
    // 걸어 오르내리고, 끝 칸과 땅이 같은 높이라 내려설 턱이 없다. 모델도 같은 각도로 기운다(보이는 것과
    // 밟는 것이 같아야 한다 — 걷는 상자가 루트에 붙어 있어 같이 기운다).
    public struct Span
    {
        public float BackY, FrontY;   // 양 끝 줄의 윗면(가장 높은 칸)
        public float BackAt, FrontAt; // 긴 축 위 양 끝 줄의 좌표(칸 한가운데, 월드)
        public bool  AlongZ;
        public bool  Ok;                       // 한쪽 끝은 땅이나 다른 다리에 닿는다
        public bool  BackSolid, FrontSolid;    // 그 끝이 닿았나(허공이면 false)

        public float At(Vector2Int t) => AtCoord(AlongZ ? t.y + 0.5f : t.x + 0.5f);

        // 긴 축 위 월드 좌표의 윗면 — 가장자리 너머는 가장자리 높이 그대로.
        public float AtCoord(float c)
        {
            float f = Mathf.Approximately(FrontAt, BackAt) ? 0f : Mathf.Clamp01((c - BackAt) / (FrontAt - BackAt));
            return Mathf.Lerp(BackY, FrontY, f);
        }
    }

    // **높이는 절벽 가장자리의 땅이 정한다**(2026-09-26) — 처음엔 발자국 **맨 끝 줄**의 땅을 쟀다. 다리가 커지며
    // (`BridgeCreator`) 끝이 가장자리 너머 몇 m씩 땅 위에 떨어졌고, 계단 바닥에서는 그 끝이 **다른 층**이라
    // ① 가장자리 땅과 첫 다리 칸 사이에 턱이 생겨 **폰이 다리에 못 올라섰고** ② 땅이 오르는 쪽은 다리가 **파묻혔다.**
    // 이제 줄마다 첫 절벽 칸 바로 앞(뒤 가장자리)과 마지막 절벽 칸 바로 뒤(앞 가장자리)를 재고, 그 둘을 잇는다.
    // 가장자리 너머는 가장자리 높이 그대로다(`At`이 클램프한다).
    //
    // **가장자리는 땅이거나 다른 다리의 윗면이다**, 그리고 **한쪽은 허공이어도 된다**(같은 날, 사용자: *"반대쪽도 벽으로
    // 막혀 있어서 뚫을 수가 없잖아 — 다리를 허공에 설치 가능하게"*). 허공 끝은 닿은 끝의 높이로 평평하게 뻗고, 그
    // 끝에 다음 다리를 이으면 **이은 다리는 앞 다리의 윗면을 가장자리로 삼는다** — 넓은 협곡을 다리 여럿으로 건넌다.
    //
    // **절벽 칸이 하나도 없으면**(땅 위에 놓은 미리보기) 예전처럼 양 끝 줄로 잰다.
    //
    // `isChasm`·`height`·`deck`을 주면 그것으로 잰다 — 에디터의 자동 배치(`ChasmPainter`)가 **아직 반영 안 한 절벽**으로
    // 같은 답을 내야 한다(규칙이 두 벌이면 배치는 된다는데 게임에서는 턱이 된다).
    public static Span SpanOf(IEnumerable<Vector2Int> footprint,
                              System.Func<Vector2Int, bool> isChasm = null,
                              System.Func<Vector2Int, float> height = null,
                              System.Func<Vector2Int, float?> deck = null)
    {
        var map = MapManager.Instance;
        isChasm ??= Chasm.IsChasm;
        height  ??= t => map != null ? map.SurfaceHeight(t) : TerrainHeight.At(t.x + 0.5f, t.y + 0.5f);
        deck    ??= t => Chasm.TryDeck(t, out float y) ? y : (float?)null;

        int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
        foreach (var t in footprint)
        {
            minX = Mathf.Min(minX, t.x); maxX = Mathf.Max(maxX, t.x);
            minZ = Mathf.Min(minZ, t.y); maxZ = Mathf.Max(maxZ, t.y);
        }
        var sp = new Span { AlongZ = (maxZ - minZ) >= (maxX - minX) };
        if (minX == int.MaxValue) return sp;

        int lo = sp.AlongZ ? minZ : minX, hi = sp.AlongZ ? maxZ : maxX;
        int a0 = sp.AlongZ ? minX : minZ, a1 = sp.AlongZ ? maxX : maxZ;
        Vector2Int T(int along, int across) => sp.AlongZ ? new Vector2Int(across, along) : new Vector2Int(along, across);

        // 딛는 면 — 땅이면 땅 높이, 다른 다리의 윗면이면 그 높이, 허공이면 없다.
        float? Solid(Vector2Int t)
        {
            if (!isChasm(t)) return height(t);
            return deck(t);
        }

        float back = float.MinValue, front = float.MinValue;
        int backAt = int.MinValue, frontAt = int.MaxValue;
        bool any = false, backAir = false, frontAir = false;
        for (int c = a0; c <= a1; c++)
        {
            int first = int.MaxValue, last = int.MinValue;
            for (int l = lo; l <= hi; l++)
                if (isChasm(T(l, c))) { first = Mathf.Min(first, l); last = Mathf.Max(last, l); }
            if (first == int.MaxValue) continue;
            any = true;

            var hb = Solid(T(first - 1, c));
            var hf = Solid(T(last + 1, c));
            if (hb == null) backAir  = true; else { back  = Mathf.Max(back,  hb.Value); backAt  = Mathf.Max(backAt,  first - 1); }
            if (hf == null) frontAir = true; else { front = Mathf.Max(front, hf.Value); frontAt = Mathf.Min(frontAt, last + 1); }
        }

        if (!any)
        {
            for (int c = a0; c <= a1; c++)
            {
                back  = Mathf.Max(back,  height(T(lo, c)));
                front = Mathf.Max(front, height(T(hi, c)));
            }
            backAt = lo; frontAt = hi;
        }

        // **한 끝의 줄이 하나라도 허공이면 그 끝은 허공이다** — 반쯤 걸친 끝을 닿은 것으로 치면 걸친 줄로 걸어 나가
        // 허공 줄로 떨어진다(칸 판정은 막지만 모양이 거짓말이 된다).
        sp.BackSolid  = !backAir  && back  > float.MinValue;
        sp.FrontSolid = !frontAir && front > float.MinValue;
        sp.Ok = sp.BackSolid || sp.FrontSolid;

        // 허공 끝은 닿은 끝의 높이로 평평하게. 둘 다 허공이면(미리보기가 절벽 한가운데) 발자국 가운데의 땅 높이 —
        // 0으로 두면 계단 바닥의 층에 따라 떠 보이거나 묻힌다.
        if (!sp.BackSolid && !sp.FrontSolid)
        {
            float mid = TerrainHeight.At((minX + maxX + 1) * 0.5f, (minZ + maxZ + 1) * 0.5f);
            back = front = mid;
            backAt = lo; frontAt = hi;
        }
        else if (!sp.BackSolid)  { back  = front; backAt  = lo; }
        else if (!sp.FrontSolid) { front = back;  frontAt = hi; }

        sp.BackY = back; sp.FrontY = front;
        sp.BackAt  = backAt  + 0.5f;
        sp.FrontAt = frontAt + 0.5f;
        return sp;
    }

    // 양 끝 높이 차가 얼마까지 되나 — **칸마다 오르는 값이 `StepUp`을 안 넘어야** 폰이 걷는다(다리 윗면은 기단과
    // 같은 턱 규칙이다). 그리고 **2m를 안 넘긴다** — 그보다 기울면 사다리지 다리가 아니다.
    public static float MaxRise(int length) => Mathf.Min(2f, Mathf.Max(0, length - 1) * PawnMover.StepUp * 0.8f);

    // **자리에 따라 높이와 기울기가 갈린다** — 미리보기·청사진·완성품이 전부 이걸 부른다(`IPlacementPose`).
    public void PoseAt(Vector2Int tile)
    {
        float yaw = transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, Mathf.Round(yaw / 90f) * 90f, 0f);   // 기울기를 걷고 발자국을 잰다

        var sp = SpanOf(Footprint());
        var p  = transform.position;
        // **루트는 발자국 가운데의 윗면 선 위에 선다** — 가장자리 둘의 평균이 아니다(지은 다리는 틈 한가운데에 안 놓일 수 있다).
        p.y = sp.AtCoord(sp.AlongZ ? p.z : p.x) + deckLift;
        transform.position = p;

        // 앞(+Z 로컬)이 긴 축의 어느 끝인가 — 앞이 더 높으면 앞을 든다(로컬 X로 음의 각).
        var fwd = transform.forward;
        bool frontIsMax = sp.AlongZ ? fwd.z > 0f : fwd.x > 0f;
        float rise = frontIsMax ? sp.FrontY - sp.BackY : sp.BackY - sp.FrontY;
        float span = Mathf.Max(1f, sp.FrontAt - sp.BackAt);
        float pitch = Mathf.Atan2(rise, span) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Quaternion.Euler(-pitch, 0f, 0f);
    }

    void Start()
    {
        PoseAt(default);

        var sp = SpanOf(Footprint());
        _deck.Clear();
        foreach (var t in Footprint())
            if (Chasm.IsChasm(t) && !Chasm.HasDeck(t)) { Chasm.RegisterDeck(t, sp.At(t) + deckLift); _deck.Add(t); }
        _registered = true;

        Rebake();
    }

    void OnDestroy()
    {
        if (!_registered) return;
        foreach (var t in _deck) Chasm.UnregisterDeck(t);
        _deck.Clear();
        _registered = false;
        Rebake();
    }

    // 길찾기에 알린다 — 격자가 스스로 알 방법은 없다(`RockMap.Mine`이 남긴 규칙).
    void Rebake()
    {
        var hp = HierarchicalPathfinder.Instance;
        if (hp == null) return;
        var size = Data != null ? Data.Size : new Vector2Int(2, 6);
        hp.RebakeArea(transform.position, Mathf.Max(size.x, size.y) * 0.5f + 3f);
    }

    // ── 놓을 수 있나 ────────────────────────────────────────────────────────
    //
    // **판정과 설명은 같은 코드다**(`BuildModeManager.WhyNotGrid`가 이걸 부른다). 규칙:
    //   ① 빌드존 안 · 바위 아님 · 점유 없음 · 이미 다리 없음
    //   ② **절벽 칸을 하나는 지난다**(아니면 그냥 판자 길이다 — 땅에는 기단이 있다)
    //   ③ **한쪽 끝은 땅이나 다른 다리에 닿는다** — 다른 쪽은 허공이어도 된다(다음 다리를 잇는다)
    //   ④ **양 끝 높이 차가 `MaxRise` 이내** — 비스듬히 놓이므로 칸마다 오르는 값이 `StepUp` 안이어야 한다
    //   ⑤ **끝이 땅에 안 묻힌다** — ③~⑤는 `WhyNotShape`(에디터 자동 배치와 한 벌)
    public static string WhyNot(List<Vector2Int> footprint)
    {
        var map = MapManager.Instance;
        if (map == null || footprint.Count == 0) return null;

        bool anyChasm = false;
        foreach (var t in footprint)
        {
            if (!map.IsInBuildZone(t)) return "빌드존 밖 — 핵에서 너무 멀다";
            if (RockMap.IsRock(t))     return "바위다 — 먼저 파야 한다";
            if (map.IsOccupied(t))     return "무언가 자리를 막고 있다";
            if (Chasm.HasDeck(t))      return "이미 다리가 있다";
            if (Chasm.IsChasm(t)) anyChasm = true;
        }
        if (!anyChasm) return "절벽을 가로질러 놓아야 한다";

        return WhyNotShape(footprint, null, null);
    }

    // **모양이 절벽에 맞나** — 짓기(`WhyNot`)와 에디터의 자연 다리 배치(`ChasmPainter`)가 **같은 답**을 쓴다.
    // `isChasm`·`height`는 `SpanOf`와 같다(에디터는 아직 반영 안 한 절벽으로 잰다).
    //   ① **한쪽 끝은** 땅이나 다른 다리에 닿는다(다른 쪽은 허공이어도 된다 — 거기에 다음 다리를 잇는다)
    //   ② 가장자리 사이 칸마다 오르는 값이 `StepUp` 안이다(`MaxRise`)
    //   ③ **가장자리 너머 땅이 다리 윗면을 뚫고 올라오지 않는다**(2026-09-26 — 계단 바닥에서 다리 끝이 다음 층에
    //      파묻혔다). 반대로 땅이 낮은 것은 괜찮다(끝이 조금 뜰 뿐 딛는 면은 가장자리다).
    public static string WhyNotShape(IEnumerable<Vector2Int> footprint,
                                     System.Func<Vector2Int, bool> isChasm,
                                     System.Func<Vector2Int, float> height,
                                     System.Func<Vector2Int, float?> deck = null)
    {
        var map = MapManager.Instance;
        isChasm ??= Chasm.IsChasm;
        height  ??= t => map != null ? map.SurfaceHeight(t) : TerrainHeight.At(t.x + 0.5f, t.y + 0.5f);

        var sp = SpanOf(footprint, isChasm, height, deck);
        if (!sp.Ok) return "한쪽 끝은 땅이나 다른 다리에 닿아야 한다";

        int span = Mathf.RoundToInt(sp.FrontAt - sp.BackAt) + 1;
        float diff = Mathf.Abs(sp.FrontY - sp.BackY);
        if (diff > MaxRise(span) + 0.01f)
            return $"양 끝 높이가 너무 다르다 (차이 {diff:0.0}m, 이 자리는 {MaxRise(span):0.0}m까지)";

        foreach (var t in footprint)
        {
            if (isChasm(t)) continue;
            if (height(t) > sp.At(t) + BuryTolerance) return "다리 끝이 땅에 묻힌다 — 가장자리 너머 땅이 높다";
        }
        return null;
    }

    // 가장자리 너머 땅이 다리 윗면보다 이만큼까지는 높아도 된다(판자 두께 · 지형이 2m 정점 사이를 비스듬히 잇는 몫).
    const float BuryTolerance = 0.35f;
}