using System.Collections.Generic;
using UnityEngine;

// **진형 - 여러 폰에게 자리를 나눠 준다**(2026-09-08, 사용자 결정: 좌표 대형).
//
// 전에도 나눠 주기는 했다(`MoveController.CalcFormation` - 정사각 격자). 없던 것이 셋이다:
//
//   - **모양** - 격자 하나뿐이라 고를 것이 없었다.
//   - **방향** - 격자가 언제나 월드축(+x/+z) 정렬이라 **적 쪽을 안 봤다.** 어느 쪽에서
//     오든 같은 모양으로 서니 앞뒤가 뜻을 갖지 못한다.
//   - **순서** - `positions[i]`와 `pawns[i]`가 짝이라 **누가 앞줄인지가 고른 순서**였다.
//     궁수가 앞줄에 서고 방패가 뒤에 서는 일이 그냥 일어난다.
//
// == 자리는 칸으로 센다 - 미터로 재지 않는다 (2026-09-08) ====================
//
// 처음에는 **미터로 오프셋을 만들고 나중에 칸에 스냅**했다(간격 1.2m). 그 결과가 사용자가
// 본 것이다: *"배치시키는데 뭔가 비어있어 규칙적이지 않아."*
//
// 1.2m 간격을 1m 격자에 떨어뜨리면 **어떤 자리는 한 칸 옆, 어떤 자리는 두 칸 옆**이 된다 -
// 줄에 구멍이 뚫린다. 방향이 대각으로 돌면 더 나빠진다: 회전한 오프셋의 소수부가 자리마다
// 달라 **두 자리가 같은 칸에 떨어지고**, 하나가 밀려나 또 구멍이 생긴다.
//
// **간격을 고쳐서는 못 푼다** - 축 방향에 맞추면 대각에서 어긋나고(대각 한 칸은 1.41m),
// 대각에 맞추면 축에서 벌어진다. 원인은 값이 아니라 **미터로 세고 칸에 떨어뜨린다**는
// 순서 자체다.
//
// 그래서 **처음부터 칸으로 센다**: 방향을 **8방 중 하나로** 접고, 자리를 `중심칸 +
// 옆방향 * 열 - 앞방향 * 줄`로 낸다. 반올림이 아예 없으므로 **줄이 정확히 한 칸씩**
// 떨어지고, 대각으로 서도 같은 모양이 나온다. 격자 위에 서는 게임이니 진형도 격자 위에
// 서는 것이 맞다.
//
// == 굴에서 접히는 문제 =====================================================
//
// 지하는 통로가 좁다(한 칸짜리도 흔하다). **횡대 다섯을 한 칸 통로에 세울 수는 없다** -
// 이건 규칙으로 못 이긴다. 그래서 **접히는 방식을 규칙으로 정한다**: 못 서는 자리는
// `FindWorkableNear`로 가장 가까운 설 자리를 찾고, **이미 배정된 칸은 후보에서 뺀다.**
//
// 그러면 좁은 곳에서 횡대는 저절로 **통로를 따라 한 줄**이 되고 겹치지 않는다. 저작한
// 모양이 그대로 안 나오는 것은 맞지만, **그건 그 자리에 설 수가 없다는 사실이지 버그가
// 아니다** - 대신 *앞뒤 순서*는 좁은 곳에서도 그대로 지켜진다(그게 진형의 절반이다).
public static class Formation
{
    // **모양마다 답하는 물음이 하나씩 있어야 한다.** 늘리는 것은 쉬운데, 늘어난 만큼
    // *언제 무엇을 쓰나*가 사람 머릿속에서 흐려진다 - 굴에서 실제로 다른 그림이 나오고,
    // **그것을 고를 이유가 판 안에 있는 것**만 남긴다(사각형이 2026-09-09에야 붙은 이유가
    // 그것이다: 적이 한 방향에서만 오는 동안에는 고를 이유가 없었다).
    public enum Shape
    {
        Box,      // 밀집 - 예전 그대로. 이동용 기본값이라 안 고른 사람에게는 아무것도 안 바뀐다
        Line,     // 횡대 - 넓은 방에서 벽처럼 선다
        Column,   // 종대 - 통로용. 좁은 굴에서 접히지 않는 유일한 모양이다
        Wedge,    // 삼각형 - 앞이 뾰족하다. 근접이 먼저 닿고 활이 뒤에 남는다

        // 사각형 - **앞이 없는 유일한 모양**이다(2026-09-09, 포위가 생기면서 붙었다).
        //
        // 나머지 넷은 전부 *어느 쪽이 앞인가*를 답하는 모양이라, **사방에서 들어올 때는
        // 넷 다 틀린 답**이다(어느 쪽으로 세워도 등이 하나 생긴다). 습격대가 여러 갈래로
        // 들어오게 된 날 이 모양을 고를 이유가 처음 생겼다 - 그전에는 적이 늘 한 방향에서
        // 왔으므로 사각형은 *일부러 나쁘게 서는 것*이었다.
        //
        // **이름은 보이는 대로 부른다**(2026-09-09, 사용자 결정). 하루 동안 `원형`이었는데
        // 격자에서 고리는 **네모로 나온다**(아래 `Slots`) - 그림과 이름이 다르면 고를 때마다
        // 다시 배워야 한다. 그러면서 `Box`가 `방진`(=네모 진형)이면 **둘 다 네모**라 이름이
        // 겹쳐, 그쪽을 `밀집`으로 옮겼다(속이 찼나 ↔ 둘레만인가가 갈리는 자리다).
        //
        // **코드 이름은 `Ring`으로 둔다** - 자료구조는 고리가 맞고(둘레 8r칸을 도는 것이
        // 이 모양의 전부다), 그 이름까지 화면 글자를 따라가면 `RingCell`이 무엇을 도는
        // 함수인지가 흐려진다.
        //
        // **값은 꼬리에 붙인다** - 진형 바가 `enumValueIndex`로 저작하므로 중간에 끼우면
        // 씬의 버튼이 통째로 딴 진형을 가리킨다.
        Ring,
    }

    // **고른 진형은 한 벌이다** - 폰마다 들면 *무리의 모양*이라는 말이 성립하지 않는다.
    public static Shape Current = Shape.Box;

    public static string NameOf(Shape s) => s switch
    {
        Shape.Line   => "횡대",
        Shape.Column => "종대",
        Shape.Wedge  => "삼각형",
        Shape.Ring   => "사각형",
        _            => "밀집",
    };

    // 한 칸(m). 자리를 칸으로 세므로 **간격이라는 손잡이가 없다** - 늘리려면 칸을 띄워야
    // 하고, 그건 좁은 굴에서 통째로 접히게 만든다.
    public const float Spacing = 1f;

    // 대각으로 설 때 한 칸의 길이. 끈 길이에서 **한 줄 인원**을 낼 때 쓴다.
    public const float Diagonal = 1.41421f;

    // 횡대 한 줄에 몇 명까지(**안 끌었을 때**). 넘으면 뒤로 줄을 하나 더 만든다 - 안 그러면
    // 열 명이 **10m짜리 벽**이 되어 어지간한 굴에서는 통째로 접힌다.
    //
    // 끌었을 때는 사람이 정한 폭이 이긴다 - **폭을 받는 모양은 횡대뿐이다**(아래 `Slots`).
    const int LineWidth = 6;

    // 접을 때 몇 칸까지 찾나. 넓게 잡으면 **한 명이 진형 밖으로 멀리 튄다**.
    const int FoldRadius = 4;

    // 이 방향으로 설 때 한 칸이 몇 m인가 - 끈 길이를 인원으로 옮길 때 쓴다.
    static float StepOf(Vector2Int dir8) => dir8.x != 0 && dir8.y != 0 ? Diagonal : Spacing;

    // == 자리를 낸다 ========================================================
    //
    // 돌려주는 것은 **`pawns`와 같은 순서**다(부르는 쪽이 `pawns[i]`를 `결과[i]`로 쓴다).
    // 안쪽에서만 역할 순서로 슬롯을 배정한다.
    //
    // **모양을 부르는 쪽이 넘긴다** - `Current`를 여기서 읽으면 *언제 진형을 쓰는가*라는
    // 물음의 답이 여기 숨는다. 실제로 그 답은 **소집했나**이고, 그건 명령을 내리는 쪽이 안다.
    //
    // `perRow`는 **한 줄에 몇 명**이다(0이면 기본값). 끌어서 폭을 정할 때 쓰고, 줄이라는
    // 것이 없는 모양(종대, 삼각형, 사각형)에서는 무시된다.
    public static Vector3[] Place(Vector3 center, Vector3 facing, IReadOnlyList<PawnMover> pawns,
                                  Shape shape, int perRow = 0)
    {
        int n = pawns != null ? pawns.Count : 0;
        var result = new Vector3[n];
        if (n == 0) return result;

        var cells = Slots(shape, n, perRow, edge: false);

        var map0 = MapManager.Instance;
        var fwd0 = Dir8(facing);
        return Build(map0,
                     map0 != null ? map0.WorldToTile(center) : default,
                     center, fwd0, new Vector2Int(fwd0.y, -fwd0.x), cells, pawns);
    }

    // **끌어서 세운다 - 처음 찍은 곳이 축이다**(2026-09-08, 사용자: *"처음 찍은 곳을 축으로
    // 거기서 움직이면 안되지"*).
    //
    // 처음에는 끈 선의 **한가운데**를 중심으로 삼았다. 그러면 폰 수보다 길게 끌었을 때
    // **폭은 천장에 걸려 안 늘어나는데 중심만 계속 밀려나** 대형이 통째로 미끄러진다 -
    // 축을 잡고 폭을 정하는 조작이 아니라 **끄는 내내 자리가 흔들리는** 조작이 된다.
    //
    // 이제 **첫 칸이 곧 첫 열**이다: 앞줄이 찍은 칸에서 시작해 끄는 쪽으로 자란다. 더 끌면
    // 넓어지기만 하고, 다 차면 **아무 일도 안 일어난다**(그게 천장의 옳은 모양이다).
    //
    // **옆방향을 끈 선에서 바로 낸다** - 바라볼 방향에서 수직을 되짚으면 8방으로 접는 과정에
    // 부호가 뒤집혀 **자라는 쪽이 반대**가 될 수 있다(축은 그대로인데 대형이 등 뒤로 자란다).
    //
    // == 어느 쪽을 보고 서나 - 끄는 손이 정한다 (2026-09-08) ==================
    //
    // 한때 *수직 둘 중 **무리에서 멀어지는 쪽***으로 잡았다. 외울 것이 없어서 골랐는데,
    // **손에 쥐면 예측이 안 된다**: 같은 자리에 같은 방향으로 그어도 폰들이 어디 서 있느냐에
    // 따라 앞뒤가 뒤집힌다. 무리가 대형 뒤에 있으면 맞지만, 옆에서 돌아 들어가거나 이미
    // 그 자리를 지나쳐 있으면 **등을 보이고 선다.**
    //
    // 사용자 결정: **오른쪽으로 밀면 화면 앞(카메라에서 멀어지는 쪽), 왼쪽으로 밀면 뒤.**
    // 손짓 하나에 답이 하나라 늘 같다.
    //
    // 재는 것은 `cross(끈 선, up)` 하나다 - **카메라를 안 봐도 된다.** 끈 선을 화면 오른쪽
    // 성분과 화면 앞 성분으로 쪼개 보면, 이 수직은 **오른쪽 성분의 부호를 그대로 화면 앞
    // 성분으로 옮긴다.** 그래서 카메라를 돌려도 *오른쪽으로 밀면 앞*이 그대로 성립하고,
    // 카메라 좌표를 여기로 끌고 들어올 이유가 없다(들어오면 화면 없는 자리에서 못 쓴다).
    public static Vector3[] Drag(Vector3 from, Vector3 to,
                                 IReadOnlyList<PawnMover> pawns, Shape shape, out Vector3 facing)
    {
        int n = pawns != null ? pawns.Count : 0;
        facing = Vector3.forward;
        if (n == 0) return new Vector3[0];

        var line  = to - from;
        line.y    = 0f;
        var right = Dir8(line.sqrMagnitude < 0.01f ? Vector3.right : line);

        // `cross(오른쪽, up)` - 오른쪽으로 밀면 화면 앞, 왼쪽으로 밀면 화면 뒤.
        var fwd = new Vector2Int(-right.y, right.x);

        facing = new Vector3(fwd.x, 0f, fwd.y).normalized;

        float step = StepOf(right);
        int perRow = Mathf.Clamp(Mathf.RoundToInt(line.magnitude / step) + 1, 1, n);

        var cells = Slots(shape, n, perRow, edge: true);
        var map   = MapManager.Instance;
        return Build(map, map != null ? map.WorldToTile(from) : default,
                     from, fwd, right, cells, pawns);
    }

    // 자리를 실제로 놓는다 - **중심 기준이든 축 기준이든 여기 하나를 지난다.**
    static Vector3[] Build(MapManager map, Vector2Int origin, Vector3 originWorld,
                           Vector2Int fwd, Vector2Int right,
                           Vector2Int[] cells, IReadOnlyList<PawnMover> pawns)
    {
        int n = pawns.Count;
        var result = new Vector3[n];

        // **앞에 설 사람을 먼저 고른다.** 기준은 **사거리**다 - 저작하지 않는다.
        // (`ResourceNode.NeededCategory`가 `DropItem`에서 파생되는 것과 같은 자리:
        //  역할을 따로 적게 하면 저작 실수 하나가 *궁수가 앞줄에 서는 무리*를 만들고,
        //  무엇보다 무기를 갈아 끼웠을 때 **한쪽만 따라온다**.)
        var order = new List<int>(n);
        for (int i = 0; i < n; i++) order.Add(i);
        order.Sort((a, b) => Compare(pawns[a], pawns[b]));

        // 격자가 없는 씬(옛 던전, 시험 씬)에서는 미터로 낸다 - 그쪽은 칸이 없다.
        if (map == null)
        {
            var r = new Vector3(right.x, 0f, right.y);
            var f = new Vector3(fwd.x, 0f, fwd.y);
            for (int s = 0; s < n; s++)
                result[order[s]] = originWorld + r * cells[s].x - f * cells[s].y;
            return result;
        }

        var used = new HashSet<Vector2Int>();
        for (int s = 0; s < n; s++)
        {
            // 열은 옆으로, 줄은 **뒤로**. `cells[s].y`가 몇 째 줄인가다(0이 앞줄).
            var tile = origin + right * cells[s].x - fwd * cells[s].y;

            if (used.Contains(tile) || !map.IsWorkable(tile))
                tile = map.FindWorkableNear(tile, FoldRadius, used);

            used.Add(tile);
            result[order[s]] = map.TileToGround(tile);
        }
        return result;
    }

    // 사거리가 짧을수록 앞. **이름으로 동점을 가른다** - 안 가르면 같은 무기를 든 넷의
    // 앞뒤가 매번 바뀌어, 같은 자리에 같은 명령을 내려도 폰들이 서로 자리를 맞바꾼다.
    static int Compare(PawnMover a, PawnMover b)
    {
        float ra = RangeOf(a), rb = RangeOf(b);
        if (!Mathf.Approximately(ra, rb)) return ra.CompareTo(rb);
        return string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : "");
    }

    static float RangeOf(PawnMover pawn)
    {
        var brain = pawn != null ? pawn.GetComponent<UnitBrain>() : null;
        var bb    = brain != null ? brain.Blackboard : null;
        return bb != null ? bb.AttackRange : 1f;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.01f ? Vector3.forward : v.normalized;
    }

    // **방향을 8방으로 접는다.** 자유 각도로 두면 회전한 오프셋의 소수부가 자리마다 달라
    // 칸에 떨어질 때 줄이 흐트러진다 - 진형이 격자 위에 서려면 **방향도 격자의 것**이어야 한다.
    public static Vector2Int Dir8(Vector3 facing)
    {
        var f = Flat(facing);
        int i = Mathf.RoundToInt(Mathf.Atan2(f.z, f.x) * Mathf.Rad2Deg / 45f);
        return (((i % 8) + 8) % 8) switch
        {
            0 => new Vector2Int(1, 0),
            1 => new Vector2Int(1, 1),
            2 => new Vector2Int(0, 1),
            3 => new Vector2Int(-1, 1),
            4 => new Vector2Int(-1, 0),
            5 => new Vector2Int(-1, -1),
            6 => new Vector2Int(0, -1),
            _ => new Vector2Int(1, -1),
        };
    }

    // 체비쇼프 반지름 `r`인 고리의 `k`번째 칸 - **앞 한가운데에서 시계 방향**이다.
    // 앞에서 시작하는 이유: 고리에 앞은 없어도 **슬롯 0은 하나여야** 같은 명령이 같은
    // 배치를 낸다(안 그러면 누를 때마다 무리가 고리 위에서 자리를 맞바꾼다).
    //
    // `y`가 음수면 앞이다(`Build`가 `- fwd * y`로 놓는다) - 그래서 다른 모양과 달리
    // **줄 번호가 음수까지 간다.** 고리는 앞뒤로 다 뻗는 유일한 모양이다.
    static Vector2Int RingCell(int r, int k)
    {
        int side = 2 * r;                  // 한 변에 놓이는 칸 수
        k = ((k % (8 * r)) + 8 * r) % (8 * r);

        // 앞 한가운데(0, -r)에서 오른쪽으로 → 아래로 → 왼쪽으로 → 위로.
        if (k < r)            return new Vector2Int(k, -r);                    // 앞면 오른쪽 절반
        k -= r;
        if (k < side)         return new Vector2Int(r, -r + k);                // 오른쪽 면
        k -= side;
        if (k < side)         return new Vector2Int(r - k, r);                 // 뒷면
        k -= side;
        if (k < side)         return new Vector2Int(-r, r - k);                // 왼쪽 면
        k -= side;
        return new Vector2Int(-r + k, -r);                                     // 앞면 왼쪽 절반
    }

    // == 모양 ===============================================================
    //
    // 돌려주는 것은 **칸 단위 (열, 줄)**이고 **앞줄(줄 0)부터 나온다** - 배정이 이 순서에
    // 기대므로(앞 슬롯 = 앞줄) 모양을 새로 만들 때도 그 순서를 지킬 것. 안 지키면
    // **근접이 뒷줄에 선다.**
    //
    // **열은 정수여야 한다** - `(cols-1)/2`처럼 반 칸이 섞이면 짝수 인원에서 줄이 반 칸
    // 밀리고, 그 반 칸이 다시 반올림돼 **칸으로 세는 뜻이 없어진다.** 정수 나눗셈으로 접는다.
    //
    // `edge`가 참이면 **열을 축에서부터 센다**(0, 1, 2...). 끌어서 세울 때 쓴다 - 가운데를
    // 기준으로 두면 처음 찍은 칸이 대형 한복판이 되어 **끄는 내내 축이 움직인다.**
    static Vector2Int[] Slots(Shape shape, int n, int perRow, bool edge)
    {
        var o = new Vector2Int[n];

        switch (shape)
        {
            case Shape.Line:
            {
                int cols = Mathf.Clamp(perRow > 0 ? perRow : LineWidth, 1, n);
                for (int i = 0; i < n; i++)
                {
                    int row  = i / cols, col = i % cols;
                    int wide = Mathf.Min(cols, n - row * cols);
                    o[i] = new Vector2Int(edge ? col : col - wide / 2, row);
                }
                break;
            }

            case Shape.Column:
                for (int i = 0; i < n; i++) o[i] = new Vector2Int(0, i);
                break;

            case Shape.Ring:
            {
                // **한 고리에 다 세운다.** 고리의 반지름은 인원에서 나온다(체비쇼프 반지름
                // r인 고리는 정확히 8r칸) - 그래서 사람이 늘면 고리가 커질 뿐 **간격은
                // 그대로 한 칸**이다.
                //
                // **고르게 벌린다** - 둘레를 순서대로 채우면 절반만 찬 고리가 **호 하나**가
                // 되어 사각형이 아니게 된다(그건 그냥 휘어진 횡대다). 그래서 자리마다
                // `i * 둘레 / n`번째 칸을 집는다.
                //
                // **동그라미로 만들려 하지 말 것.** 격자에서 *가운데에서 같은 거리*는
                // 체비쇼프 거리이고 그 등거리선은 네모다. 동그라미를 흉내내면(유클리드
                // 반지름을 칸에 떨어뜨리면) **대각선 자리에 구멍이 생겨** 적이 그리로 걸어
                // 들어온다 - 사방을 막으려고 만든 모양인데 **둘레가 끊기면 뜻이 없다.**
                // 반지름 1에서는 애초에 *제 둘레 여덟 칸*이라 나올 수 있는 배치가 하나뿐이고,
                // 갈리기 시작하는 반지름 2부터가 바로 그 구멍이 생기는 자리다.
                // (역사적으로도 사방에서 들어올 때 서는 모양은 **infantry square**다.)
                //
                // **끌면 처음 찍은 칸이 고리의 한가운데다** - 다른 모양은 첫 칸이 첫 열인데
                // (`edge`), 고리에는 첫 열이라는 것이 없다. 끄는 길이도 안 받는다(폭을
                // 받는 모양은 횡대뿐이다) - 끄는 손이 정하는 것은 방향뿐이다.
                //
                // **안쪽을 안 채운다** - 두 겹으로 두면 궁수를 안에 넣을 자리가 생기지만,
                // 그때부터 *누가 안인가*를 정해야 하고 그 답은 사거리다... 즉 **앞줄 규칙이
                // 되살아난다.** 앞이 없다는 것이 이 모양의 전부이므로 겹을 늘리지 않는다.
                int r    = Mathf.Max(1, Mathf.CeilToInt(n / 8f));
                int ring = 8 * r;
                for (int i = 0; i < n; i++) o[i] = RingCell(r, i * ring / n);
                break;
            }

            case Shape.Wedge:
            {
                // r번째 줄에 r+1명. 앞이 한 명, 뒤로 갈수록 넓어진다.
                int i = 0;
                for (int r = 0; i < n; r++)
                    for (int c = 0; c <= r && i < n; c++, i++)
                        o[i] = new Vector2Int(c - r / 2, r);
                break;
            }

            default:
            {
                // **밀집은 끈 폭을 안 받는다**(2026-09-08, 사용자: *"방진이랑 횡대랑 차이가
                // 없는 거 같아"* - 그날 이 모양의 이름은 `방진`이었다).
                // 받게 두었더니 끌었을 때 **열 수가 같아져 둘이 똑같은
                // 모양**이 나왔다 - 이름만 둘이고 결과가 하나면 고를 이유가 없다.
                //
                // 가르는 축은 **폭을 누가 정하나**다: 밀집은 *가로세로가 비슷하게 뭉친 것*이라
                // 열 수가 인원에서 나오고(√n), 횡대는 *넓게 늘어선 것*이라 사람이 끌어서 정한다.
                // 그래서 **폭을 받는 모양은 횡대뿐**이고 나머지 셋에게 끌기는 **방향만** 정한다.
                int cols = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(n)), 1, n);
                for (int i = 0; i < n; i++)
                    o[i] = new Vector2Int(edge ? i % cols : i % cols - cols / 2, i / cols);
                break;
            }
        }
        return o;
    }
}
