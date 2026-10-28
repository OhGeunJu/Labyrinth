using System.Collections.Generic;
using UnityEngine;

// **버팀목이 지키는 칸 — 판 굴이 다시 메워지지 않는다**
// (2026-09-28, 사용자: *"붕괴를 만들지 말고 던전핵이나 건설목을 세운곳이 아니면 이틀 뒤에 재생되는 걸로"*).
//
// ## 무엇이 바뀌었나
//
// 09-18에는 **너무 넓게 파면 무너졌다**(림월드의 천장 격자 — 지지점에서 4칸 넘게 떨어진 판 칸의 천장이
// 내려앉았다). 사용자가 그 규칙이 고장 난 것 같다며 걷어내고 **판 굴은 이틀 뒤 바위로 되돌아온다**로
// 바꿨다(`RockRegrow`). 그 아래 두 자리만 예외다: **던전 핵 영역**과 **버팀목 둘레**.
//
// 그래서 버팀목의 뜻이 *"천장을 받친다"*에서 **"이 굴을 지킨다"**로 바뀌었다. 핵 밖으로 길을 내고
// 그 길을 남겨 두려면 나무를 들여 버팀목을 세워야 한다 — *얼마나 팔까*의 값은 여전히 버팀목이 치른다.
//
// **체비셰프 거리로 잰다**(정사각) — *어디까지 지키나*를 눈으로 그릴 수 있게.
public static class RockSupport
{
    // 버팀목 하나가 지키는 반경(칸). **9×9가 지켜진다**(가운데에서 4칸).
    public const int SafeRadius = 4;

    // ── 버팀목 ──────────────────────────────────────────────────────────────
    //
    // **레지스트리다** — 판정이 매번 `FindObjectsByType`을 돌 수는 없다
    // (`ResourceNode._all`과 같은 자리). 휴면이나 철거로 꺼지면 `OnDisable`이 뺀다.
    static readonly List<MineSupport> _supports = new();

    public static void Register(MineSupport s)
    {
        if (s != null && !_supports.Contains(s)) _supports.Add(s);
    }

    public static void Unregister(MineSupport s) => _supports.Remove(s);

    // **버팀목이 지키는 칸인가 — 답은 여기 하나다.** 메우는 쪽(`RockRegrow`)과 커서 옆 안내
    // (`MineDesignator`)가 같이 묻는다 — 갈라 적으면 *안 메워진다더니 메워졌다*가 생긴다.
    public static bool IsHeld(Vector2Int tile)
    {
        foreach (var s in _supports)
            if (s != null && Chebyshev(s.Tile, tile) <= s.Radius) return true;
        return false;
    }

    static int Chebyshev(Vector2Int a, Vector2Int b) =>
        Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
