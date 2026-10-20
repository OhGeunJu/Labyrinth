using UnityEngine;

// **어두우면 덜 보인다 — 그리고 그것이 밤을 위험하게 만든다.**
//
// 조명만 바꾸면 밤은 *보기에만* 밤이다. 폰은 여전히 10m 밖의 적을 정확히 알아채고, 플레이어가
// 잃는 것은 화면이 어둡다는 불편뿐이라 밤을 피할 이유가 안 생긴다. **감지 범위가 줄어야**
// 밤에 적이 코앞까지 오고, 순찰이 뜻을 갖고, 낮에 싸울지 밤을 넘길지가 고르는 일이 된다.
//
// ## 답은 한 벌이어야 한다
//
// "지금 얼마나 보이나"를 묻는 곳이 둘이면(교전 탐지 · 도주 탐지) 반드시 어긋난다 —
// 도망은 밤에도 멀쩡히 치고 싸움만 늦게 붙는 꼴이 된다. 그래서 배율도, 밤눈을 섞는 식도
// 여기 한 벌뿐이다.
//
// ## 시계는 `GameTimeManager`가 든다
//
// 여기는 때(`TimePhase`)만 읽는다. **조명과 같은 축**이라 화면이 저물면 시야도 같이 줄어든다 —
// 어두워 보이는데 잘 보이거나, 밝은데 안 보이면 어느 쪽도 설명할 수 없다.
public static class Vision
{
    // 때마다의 배율. 저녁은 아직 남은 빛이 있고, 새벽이 밤보다 조금 나은 것은 동트기 전
    // 어스름이다. **낮과 아침은 같다** — 아침에 덜 보일 이유가 없다.
    public const float DayFactor     = 1f;
    public const float EveningFactor = 0.7f;
    public const float DawnFactor    = 0.5f;
    public const float NightFactor   = 0.4f;

    // 지금 이 순간의 배율.
    //
    // **지하에서는 다른 표를 쓴다**(2026-08-29). 위의 값은 *"해가 얼마나 남았나"*인데
    // 지하에는 해가 없다 — 그대로 두면 **화면은 캄캄한데 폰은 대낮처럼 보고**, 그러면
    // 화톳불이 *"켜도 아무것도 안 달라지는 장식"*이 된다.
    //
    // **`UnderworldLighting`이 답한다** — 화면의 어둡기를 정하는 바로 그 값이다. 여기서 따로
    // 표를 들면 *"어두워 보이는데 잘 보인다"*가 생기고, 그건 어느 쪽도 못 믿게 만든다.
    // 지상의 밤 조명이 이미 못 박은 규칙이다: **시계는 하나, 답도 하나.**
    public static float Factor
    {
        get
        {
            if (UnderworldLighting.Active) return UnderworldLighting.Tide;

            return GameTimeManager.Phase switch
            {
                TimePhase.Evening => EveningFactor,
                TimePhase.Dawn    => DawnFactor,
                TimePhase.Night   => NightFactor,
                _                 => DayFactor,
            };
        }
    }

    // **이 자리에서의 배율 — 불빛이 닿으면 그만큼 걷힌다**(`LightSource`).
    //
    // 배율이 자리마다 다른 것은 밤에 **플레이어가 손댈 데가 있어야** 하기 때문이다. 때만
    // 보면 밤은 견디는 시간이고, 화톳불 하나로 그 둘레가 낮이 되면 **거점을 어떻게 밝힐지가
    // 결정이 된다**(어디에 세울까 · 목재를 얼마나 댈까 · 어느 불을 죽일까).
    //
    // **낮에는 불을 아예 안 훑는다.** 탐지는 폰마다 0.1초에 한 번 도는 자리고 하루의 3분의 2는
    // 밝다 — 이 한 줄이 그 시간 동안의 레지스트리 순회를 통째로 없앤다.
    //
    // **지하에서는 그 지름길이 없다**(배율이 1이 되는 때가 없다). 대신 거기서는 불빛이
    // *늘* 뜻을 가지므로 훑을 값을 한다 — 지상에서 밤에만 훑던 것이 지하에서는 하루 종일이다.
    public static float FactorAt(Vector3 at)
    {
        float f = Factor;
        if (f >= 1f) return f;
        return Mathf.Lerp(f, 1f, LightSource.BrightestAt(at));
    }

    // **밤눈이 그 배율을 되돌린다**(`UnitData.NightVision`, 0~1 — 1이면 밤에도 낮처럼 본다).
    //
    // 밤을 모두에게 똑같이 걸면 그건 세상이 흐려진 것일 뿐이다. 언데드·야수가 밤에도 보면
    // **밤 습격이 실제로 저쪽에 유리한 판**이 되고, 그러면 밤은 "불편한 시간"이 아니라
    // "저들의 시간"이 된다 — 여기서 종족이 뜻을 하나 더 갖는다.
    //
    // **불빛과는 저절로 맞물린다** — 밤눈이 이미 밝게 보는 놈에게 화톳불은 별로 보태 주지
    // 않는다(둘 다 같은 배율을 1로 미는 일이라 겹치는 만큼은 겹치지 않는다).
    //
    // **`at`은 선택 인자가 아니다.** 기본값을 두면 부르는 쪽이 조용히 빠뜨리고, 그러면 그
    // 경로에서만 불빛이 없는 것이 된다 — `Storage.FindStorageWithItem`이 `from`을 안 받아
    // 폰이 맵을 가로지르던 것과 같은 자리다.
    public static float RangeFor(float baseRange, float nightVision, Vector3 at) =>
        baseRange * Mathf.Lerp(FactorAt(at), 1f, Mathf.Clamp01(nightVision));

    public static float RangeFor(UnitBlackboard bb) =>
        bb?.Self == null ? 0f : RangeFor(bb.DetectRange, bb.NightVision, bb.Self.transform.position);

    // ── 보이나 — 반경 안이고, 사이에 바위가 없다 (2026-10-18) ──────────────────────
    //
    // 전에는 반경만 있었다 — **벽 너머도 보였다**(`FactionScan` 머리말). 그래서 굴 모퉁이를 돌아도 적이 정확히
    // 따라왔다. 가림은 **안개와 같은 판정**이다(`FogOfWar` — `RockMap.IsRock` 하나: 길이 막히는 곳이 곧 눈이 막히는
    // 곳). 지은 벽은 안개처럼 안 막는다 — 한쪽만 바꾸면 *"화면엔 보이는데 폰은 못 본다"*가 생긴다.
    //
    // `rangeMult` — 이미 든 표적을 놓을 때는 조금 넉넉히 본다(`DetectNode.HoldMargin`). 같은 값으로 잡고 놓으면
    // 반경 끝에서 한 틱마다 잡았다 놓았다 한다.
    public static bool CanSee(UnitBlackboard bb, GameObject target, float rangeMult = 1f)
    {
        if (bb?.Self == null || target == null) return false;
        Vector3 from = bb.Self.transform.position, to = target.transform.position;
        float range = RangeFor(bb) * rangeMult;
        Vector3 d = to - from; d.y = 0f;
        if (d.sqrMagnitude > range * range) return false;
        return SightClear(from, to);
    }

    // 두 자리 사이 칸에 바위가 없나 — **양 끝 칸은 안 본다**(선 자리·표적 자리가 바위면 그건 길찾기의 일이다).
    // 칸 단위 DDA라 반경 10m면 열 칸 남짓이다. 거리로 먼저 거른 뒤에만 부른다.
    public static bool SightClear(Vector3 from, Vector3 to)
    {
        var map = MapManager.Instance;
        if (map == null || !RockMap.Enabled) return true;
        Vector2Int a = map.WorldToTile(from), b = map.WorldToTile(to);
        if (a == b) return true;

        int dx = Mathf.Abs(b.x - a.x), dy = Mathf.Abs(b.y - a.y);
        int sx = b.x > a.x ? 1 : -1, sy = b.y > a.y ? 1 : -1;
        int err = dx - dy, x = a.x, y = a.y;
        while (true)
        {
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 <  dx) { err += dx; y += sy; }
            if (x == b.x && y == b.y) return true;
            if (RockMap.IsRock(new Vector2Int(x, y))) return false;
        }
    }
}
