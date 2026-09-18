using System.Collections.Generic;
using UnityEngine;

// **너무 크게 파면 무너진다**
// (2026-09-18, 사용자 결정: *"채굴이 마음대로 되잖아? 그거에 제한을 두는 시스템을 만들고
//  싶어 너무 크게 파면 무너지거나 하는거지"*).
//
// ## 무엇이 결정이 되나
//
// 지금까지 채굴에는 **한도가 없었다** — 찍으면 파였고, 굴을 얼마나 넓히든 값이 같았다.
// 그래서 *어디를 팔까*는 있어도 *얼마나 팔까*가 없었다. 여기서 그 축이 생긴다:
// **넓게 파려면 버팀목을 세워야 하고, 버팀목은 나무를 먹는다.**
//
// ## 축이 둘이다 — **무엇이 천장인가**와 **무엇이 받치는가**
//
// **천장 격자는 `RockMap.HasCutRoof`다**(2026-09-18, 사용자: *"걍 천장 격자를 하나 더 해
// 광장이 왜 받쳐"* · *"그냥 아예 림월드처럼 해달라니깐"*) — **무너질 수 있는 것은 파서
// 천장이 생긴 칸뿐이다.** 자연 굴의 천장은 원래부터 서 있던 것이라 안 내려앉고, 한 번
// 무너진 자리는 격자에서 빠져 두 번 안 무너진다.
//
// **지지점은 바위와 버팀목뿐이다.** 한때 *"안 판 칸은 받친다"*로 두어 **광장이 받치게**
// 했는데, 그건 증상에 맞춘 것이었다 — 광장도 위에 바위가 얹혀 있고, 받치는 것은 **기둥**
// 이지 빈 공간이 아니다.
//
// **광장에서 벽을 파자마자 무너지던 것의 원인은 지지 판정이 아니라 *무너질 후보*였다**:
// 판 칸의 둘레 4칸 안의 **열린 칸을 전부** 후보로 넣었는데, 광장 한복판은 바위에서 머니
// **광장 수십 칸이 통째로 무너짐 목록에 실렸다.** 실제로 되메워지는 것은 `_mined`에 있는
// 칸뿐이라 **지형은 그대로인데**, `OnCollapse`에는 그 목록이 그대로 나가 **광장에 서 있던
// 폰이 깔리고** *"굴 수십 칸이 무너졌습니다"*가 떴다. 화면에는 **벽을 파자마자 광장이
// 무너지는 것**으로 보인다 — 그래서 원인이 지지 반경에 있는 것처럼 읽혔다.
//
// 규칙은 이제 눈으로 그려진다: **내가 판 자리가 바위나 버팀목에서 4칸 넘게 떨어지면
// 무너진다** — 광장 옆이라도 벽이 받치면 버티고, 광장을 넓혀 나가면 바위가 멀어지는
// 만큼 규칙적으로 무너진다.
//
// **체비셰프 거리로 잰다**(정사각) — 유클리드로 재면 대각선 방향만 유난히 위험해져,
// *어디까지 팔 수 있나*를 눈으로 못 그린다. 정사각이면 **9×9 방까지 안전**이라 세기 쉽다.
//
// ## 이미 판 곳은 안 건드린다
//
// 사용자가 못 박았다: *"이미 파여져있는거 말고 내가 파려고 하는거에 적용해야해."*
// 판이 열릴 때 이미 큰 굴이 있어도 무너지지 않는다 — **소급하면 시작하자마자 세계가
// 무너지고**, 무엇보다 *내가 한 일*이 아닌 것으로 벌을 받는다.
//
// 그래서 검사는 **방금 판 칸의 둘레**에만 건다.
public static class RockSupport
{
    // 지지점까지 이 칸 수 안이면 버틴다. **9×9 방이 딱 버티는 값**이다(가운데가 4칸).
    // 그보다 넓히려면 버팀목이 든다.
    public const int SafeRadius = 4;

    // 무너질 때 둘러볼 범위. 방금 판 칸에서 이만큼 안의 빈칸이 지지를 잃었나 본다.
    // **`SafeRadius`보다 넓게 볼 이유가 없다** — 더 먼 칸은 이번 채굴로 사정이 안 바뀐다.
    const int CheckRadius = SafeRadius;

    // ── 버팀목 ──────────────────────────────────────────────────────────────
    //
    // **레지스트리다** — 지지 판정이 매번 `FindObjectsByType`을 돌 수는 없다
    // (`ResourceNode._all`과 같은 자리). 휴면이나 철거로 꺼지면 `OnDisable`이 뺀다.
    static readonly List<MineSupport> _supports = new();

    public static void Register(MineSupport s)
    {
        if (s != null && !_supports.Contains(s)) _supports.Add(s);
    }

    public static void Unregister(MineSupport s) => _supports.Remove(s);

    // ── 지지 판정 ───────────────────────────────────────────────────────────

    // **받치는 칸인가 — 답은 여기 하나다.** 기둥이 받치지 빈 공간이 받치지 않는다.
    // 갈라 적으면 미리보기와 실제 무너짐이 다른 말을 한다.
    static bool Anchors(Vector2Int tile) => RockMap.IsRock(tile);

    // 이 칸의 천장이 버티나. **천장 격자 밖은 늘 참이다** — 바위이거나 원래 있던 굴이다.
    public static bool IsSupported(Vector2Int tile)
    {
        if (!RockMap.Enabled) return true;      // 지상에는 천장이 없다
        if (!RockMap.HasCutRoof(tile)) return true;

        return SupportedIgnoring(tile, NoTile);
    }

    // `ignore`가 없을 때 쓰는 자리. **세계 밖이라 칸으로는 못 나온다.**
    static readonly Vector2Int NoTile = new(int.MinValue, int.MinValue);

    // **이 칸을 파면 무너질 것이 생기나** — 찍기 미리보기가 이걸 묻는다.
    //
    // 실제로 파 보지 않고 답해야 하므로 **그 칸이 없다고 치고** 둘레를 본다.
    public static bool WouldCollapse(Vector2Int tile)
    {
        if (!RockMap.Enabled) return false;
        return CountUnsupportedIfMined(tile, null) > 0;
    }

    // 파낸 **뒤에** 부른다 — 지지를 잃은 칸들을 돌려준다(`RockMap.Mine`이 무너뜨린다).
    public static int CollectCollapsed(Vector2Int mined, List<Vector2Int> into)
        => CountUnsupportedIfMined(mined, into);

    // `pretend`가 비면 *그 칸이 이미 파였다*고 치고 센다(미리보기), 아니면 지금 상태로 센다.
    static int CountUnsupportedIfMined(Vector2Int mined, List<Vector2Int> into)
    {
        int n = 0;

        for (int dx = -CheckRadius; dx <= CheckRadius; dx++)
        for (int dz = -CheckRadius; dz <= CheckRadius; dz++)
        {
            var t = new Vector2Int(mined.x + dx, mined.y + dz);

            // **천장 격자 — 무너질 수 있는 것은 내가 판 칸뿐이다.** 자연 굴은 원래부터
            // 서 있던 천장이라 안 내려앉는다. **여기서 안 거르면 광장 수십 칸이 목록에
            // 실려**, 지형은 그대로인데 폰이 깔리고 *"굴 N칸이 무너졌습니다"*가 뜬다.
            // **방금 판 칸 자신은 본다** — 그 칸이 곧 새로 뚫린 천장이다.
            if (t != mined && !RockMap.HasCutRoof(t)) continue;

            if (SupportedIgnoring(t, mined)) continue;

            n++;
            into?.Add(t);
        }
        return n;
    }

    // `ignore` 칸을 **없는 셈 치고** 지지를 본다 — 미리보기가 *파고 나면* 어떻게 되는지를
    // 물어야 하기 때문이다(실제로 파 보고 되돌릴 수는 없다).
    static bool SupportedIgnoring(Vector2Int tile, Vector2Int ignore)
    {
        foreach (var s in _supports)
        {
            if (s == null) continue;
            if (Chebyshev(s.Tile, tile) <= s.Radius) return true;
        }

        for (int dx = -SafeRadius; dx <= SafeRadius; dx++)
        for (int dz = -SafeRadius; dz <= SafeRadius; dz++)
        {
            if (dx == 0 && dz == 0) continue;

            var t = new Vector2Int(tile.x + dx, tile.y + dz);
            if (t == ignore) continue;                 // 이 칸은 곧 없어진다
            if (Anchors(t)) return true;
        }
        return false;
    }

    static int Chebyshev(Vector2Int a, Vector2Int b) =>
        Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
