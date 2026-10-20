using System.Collections.Generic;
using UnityEngine;

// **이 칸이 바위인가 — 답은 여기 하나다**(2026-08-29).
//
// 지하는 **사방이 바위이고 캐면서 넓혀 나가는 곳**이다(사용자 결정 — 원래 던전 코어 기획).
// 그러니 세계의 기본 상태는 *열린 굴*이 아니라 **꽉 찬 돌**이고, 플레이어가 하는 일의
// 절반이 그 돌을 파내는 것이다.
//
// ── 바닥·천장과 무엇이 다른가 ──────────────────────────────────────────────
//
//   `TerrainHeight`   바닥 높이  — 함수(노이즈 + 델타). **2m 정점 격자.**
//   `TerrainCeiling`  천장 높이  — 함수(여유). **4m 정점 격자.** 파낸 공간 위의 뚜껑.
//   `RockMap`(여기)   바위인가   — **1m 칸.** 캘 수 있고, 캐면 그 칸만 열린다.
//
// **칸 단위여야 하는 것이 요점이다.** 천장 메시는 4m 간격이라 한 칸을 팔 수가 없다 —
// 그래서 바위는 천장이 아니라 **바닥과 같은 물건**이다: 칸 데이터이고 청크 메시로 그린다
// (`MapManager._floored`와 `FloorRenderer`가 이미 그 모양이다).
//
// ── 무엇이 바위인가 ────────────────────────────────────────────────────────
//
// **천장의 여유를 그대로 쓴다.** 여유가 폰 키보다 작은 자리는 어차피 못 지나가는 곳이고,
// 그게 곧 돌이다. 함수를 하나 더 만들면 *"천장은 열렸는데 바위가 서 있는"* 칸이 생긴다.
//
//     여유 < 폰 키   →  바위       (기본값이 음수라 대부분이 여기다)
//     여유 ≥ 폰 키   →  뚫린 굴    (드물다 — 저절로 난 동굴)
//
// **캔 칸은 절대좌표로 기억한다**(`_mined`) — 핵을 옮겨도, 청크가 내려갔다 올라와도 유지된다.
// 바닥 점유가 쓰는 규칙 그대로다.
public static class RockMap
{
    // **천장을 따라간다** — 지하가 아니면 바위도 없다. 축을 따로 두면 *"천장은 껐는데 바위가
    // 서 있는"* 씬이 생긴다.
    public static bool Enabled => TerrainCeiling.Enabled;

    // 바위 한 칸을 **그리는** 높이(m). 딛는 면에서 이만큼 솟는다.
    //
    // **2026-08-29에 3.2 → 9.6m로 올렸다**(사용자). 3.2m는 폰 키의 두 배가 채 안 돼서 위에서
    // 내려다보면 **낮은 턱**처럼 보였다 — 참고 화면의 그 깊은 굴이 안 나온다.
    //
    // **2026-09-07에 9.6 → 24m**(폰 키의 열세 배). 사용자가 건물 크기에서 거꾸로 짚었다 —
    // *"폰보다 건물이 커야 하는데, 건물을 키웠더니 둘레 벽이 그 3분의 1밖에 안 되면 이상하다."*
    // **벽이 세계의 눈금이라** 벽이 낮으면 그 앞의 모든 것이 커 보인다.
    //
    // **판정에는 안 쓴다** — 지나갈 수 있나는 여유(`TerrainCeiling`)가 답한다. 정점 수도
    // 높이와 무관하다(옆면은 사각형 하나다). 콜라이더는 박스라 **크기와 무관하게 같은
    // 프리미티브 검사**다. 그래서 **이 값을 올리는 데는 값이 안 든다** — 손잡이 하나다.
    //
    // **공짜가 아닌 것이 하나 있다: 가린다.** `TerrainCeilingProfile`에 이미 적어 둔 대로
    // 9.6m 벽도 비스듬한 카메라에서 뒤쪽 바닥을 통째로 가렸고, 24m면 더 가린다. 그 답은
    // 높이를 도로 내리는 것이 아니라 **가까운 쪽을 깎는 것**이다(셰이더 — 지붕의 페이드와
    // 같은 뜻이되, 바위는 **물건이 아니라 밭**이라 `BuildingRoof`를 그대로 못 옮긴다:
    // 바깥이 없고, 청크 하나가 메시 한 장이라 한 기둥만 낮추려면 매 프레임 다시 구워야 한다).
    //
    // **올릴 때 같이 봐야 하는 자리는 `MineDesignator`다** — 드래그를 *윗면 평면*에 떨어뜨리면
    // 벽이 높을수록 어긋남이 커진다(3배로 올린 날 드러났던 그 사고).
    public const float Height = 24f;

    // **막는 높이도 이 값을 쓴다.** 박스라서 *따로 잡을 수는 있다* — 막는 데 필요한 것은
    // 폰이 지나가는 높이뿐이니 2.7m면 되고, 메시 콜라이더로는 그렇게 못 한다(그린 면이 곧
    // 막는 면이다). **그런데 안 나눈다**(2026-08-30): 박스는 **크기와 무관하게 같은
    // 프리미티브 검사**라 낮춰도 값이 안 준다. 얻는 것이 없는데 잃는 것은 있다 — 콜라이더가
    // 낮으면 비스듬히 보는 채로 **벽의 위쪽 면을 눌렀을 때 레이가 그 위를 지나간다.**
    // **값이 안 줄면 손잡이를 늘리지 않는다** — 둘이 어긋날 자리만 하나 생긴다.

    // ── 캔 칸 ───────────────────────────────────────────────────────────────

    static readonly HashSet<Vector2Int> _mined = new();

    // ── 내가 판 칸 — 언제 다시 메워지나 (2026-09-28) ──────────────────────
    //
    // 사용자: *"붕괴 시스템이 고장난 거 같아 그냥 붕괴를 만들지 말고 던전핵이나 건설목을 세운곳이 아니면
    // 이틀 뒤에 재생되는 걸로"*. **천장 격자와 붕괴를 걷어내고 이것으로 바꿨다**(`RockRegrow`가 메운다).
    //
    // 값은 **다시 메워질 게임 시각**(`GameTimeManager.Elapsed`)이다. 들어오는 문은 **`Mine` 하나** —
    // 자연 굴과 판이 열릴 때 뚫어 준 시작 공터·길(`OpenAround`·`OpenPathToCave`)은 여기 없다: 사람이 판
    // 것이 아니라 원래 있던 굴이라 안 메워진다. 나가는 문은 `Refill` 하나다.
    static readonly Dictionary<Vector2Int, float> _dug = new();

    public static IReadOnlyDictionary<Vector2Int, float> Dug => _dug;

    // 다시 볼 시각을 미룬다 — 지켜지는 칸이거나 그 자리에 무엇이 있을 때(`RockRegrow`).
    public static void Postpone(Vector2Int tile, float at)
    {
        if (_dug.ContainsKey(tile)) _dug[tile] = at;
    }

    // ── 벽의 내구도 (2026-10-15) ────────────────────────────────────────────
    //
    // 사용자: *"벽 내구도 표시(커서 위 미리보기까지)"*. **깎인 만큼은 칸이 든다** — 전에는 일감(`MineJob`)이
    // 들고 있어서, 일감이 취소되거나 폰이 놓고 쉬었다 다시 오면 **새 일감이 0부터 다시 팠다.** 보여 줄 값도
    // 일감 안에 숨어 있어 찍지 않은 벽은 물을 데가 없었다.
    //
    // 최대치는 그 칸에서 나오는 것의 눈금(`ResourceNodeData.GaugeRequired` — 광맥이면 광맥, 아니면 `PlainRock`)이라
    // 따로 저작할 것이 없다. **깎인 값은 판 순간·메워진 순간 지운다**(`Mine`·`Refill`) — 메워진 벽은 새 벽이다.
    //
    // **깎여도 `OnChanged`를 안 쏜다** — 그건 청크를 다시 굽는 소식이다. 보는 쪽(살펴보기 · 채굴 커서)이 묻는다.
    static readonly Dictionary<Vector2Int, float> _worn = new();

    // 데이터가 아예 없을 때의 눈금. **경고는 `RockRenderer`가 한 번만 한다.**
    const float FallbackDurability = 60f;

    public static float MaxDurability(Vector2Int tile)
    {
        var vein = VeinAt(tile);
        var data = vein != null ? vein : PlainRock;
        return data != null ? Mathf.Max(1f, data.GaugeRequired) : FallbackDurability;
    }

    public static float Durability(Vector2Int tile)
    {
        float max = MaxDurability(tile);
        return _worn.TryGetValue(tile, out float w) ? Mathf.Max(0f, max - w) : max;
    }

    // 깎는다. **다 깎였으면 참** — 파는 것은 부른 쪽이 한다(`Mine` — 떨굴 것이 그쪽에 있다).
    public static bool Wear(Vector2Int tile, float amount)
    {
        _worn.TryGetValue(tile, out float w);
        w += Mathf.Max(0f, amount);
        _worn[tile] = w;
        return w >= MaxDurability(tile);
    }

    // 캘 것으로 찍어 둔 칸. **바위인지와 다른 축이다** — 찍어 놓고 아직 안 판 상태가 있다.
    static readonly HashSet<Vector2Int> _marked = new();

    // 그리는 쪽이 듣는다. **바닥과 같은 이름·같은 모양**(`MapManager.OnFloorChanged`).
    public static event System.Action<Vector2Int> OnChanged;

    public static IEnumerable<Vector2Int> MarkedTiles => _marked;

    // ── 묻기 ────────────────────────────────────────────────────────────────

    // ── 칸 하나는 한 번만 센다 ──────────────────────────────────────────────
    //
    // **가장 자주 불리는 물음이다** — 길찾기 베이크가 1m 칸마다 부르고(40×40 창 하나에
    // 1,600번), 폰이 늘면 그만큼 곱해진다. 함수 쪽 답은 정점 넷의 이중선형이라 이미
    // `TerrainCeiling`이 정점을 기억하지만, **여기서 한 겹 더 접으면 lerp 넷까지 사라진다.**
    //
    // **캔 칸은 캐시 앞에서 답한다** — 캐시가 담는 것은 *함수의 답*뿐이고, 판 것은 `_mined`가
    // 든다(캘 때도 메울 때도 캐시를 손볼 일이 없다 — `Refill`은 `_mined`에서 빼기만 하면 함수의 답이 다시 선다).
    //
    // 버리는 때는 둘: **천장 저작이 바뀌었을 때**(`TerrainCeiling.Version`)와 **격자 위상이
    // 움직였을 때**다. 위상은 지하에서 0으로 못 박히지만(`ProposeGrid`가 안 건드린다) 여기서
    // 그 전제에 기대면, 언젠가 그 규칙이 바뀌는 날 **세계의 모든 바위가 반 칸 어긋난 채로
    // 굳는다** — 두 float 비교로 막을 수 있는 것을 전제로 막지 않는다.
    const byte Unknown = 0, Rock = 1, Open = 2;

    static byte[]   _cache;
    static int      _cacheVersion = -1;
    static Vector2  _cachePhase;

    public static void Forget()
    {
        if (_cache != null) System.Array.Clear(_cache, 0, _cache.Length);
    }

    // ── 세계의 끝 — 기반암 ──────────────────────────────────────────────────
    //
    // **2026-09-15, 사용자:** *"맵 끝에서 더 못 나가게 경계가 있으면 좋겠어 — 나가려고 하면
    // 못 나간다고 표시를 보여 주는?"*
    //
    // **세계 밖은 이미 바위였다**(2026-09-07) — 막는 것은 됐는데 **보이지 않았다.** 밖에는
    // 청크가 없어 바위 메시가 안 서므로, 길이 테두리에 닿은 자리는 **길이 허공에서 끊기고**
    // 폰은 보이지 않는 벽 앞에 선다. 캐면서 넓히는 세계라 테두리 안쪽 바위를 파 들어가도
    // 마지막 칸 너머가 **아무것도 없는 검은 허공**이다.
    //
    // 그래서 **테두리 안쪽 몇 칸을 캘 수 없는 바위로 둔다.** 세계 *안*이라 청크가 서고 메시가
    // 그려진다 — 막는 곳과 보이는 곳이 한 줄에 선다. 판정은 `IsRock` 한 곳이라 길찾기·건설·
    // 일감·스폰이 통째로 따라온다(세계 밖을 막을 때와 같은 자리).
    //
    // **캐는 문 셋이 전부 거절한다** — `Mark`(찍기) · `Mine`(파기) · `OpenAround`/`Carve`
    // (시작 공터). 하나라도 빠지면 그 길로 **기반암에 구멍**이 나고 그 너머가 허공이다.
    public const int BorderTiles = 3;

    public static bool IsBedrock(Vector2Int tile) => Enabled && InBorder(tile);

    // **층 사이도 기반암이다**(2026-10-01, `Floors.IsSeam`) — 층은 한 평면에 나란히 놓인 사분면이라 막지 않으면 캐서
    // 옆 층으로 넘어간다. 세계 테두리와 같은 두께 · 같은 문(캐는 문 셋이 전부 여기를 본다).
    static bool InBorder(Vector2Int t)
    {
        int w = TerrainGrid.WorldSize;
        return t.x < BorderTiles || t.y < BorderTiles || t.x >= w - BorderTiles || t.y >= w - BorderTiles
            || Floors.IsSeam(t.x, t.y);
    }

    public static bool IsBedrock(Vector3 world)
    {
        var map = MapManager.Instance;
        return IsBedrock(map != null ? map.WorldToTile(world)
                                     : new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z)));
    }

    public static bool IsRock(Vector2Int tile)
    {
        if (!Enabled) return false;

        // **세계 밖은 바위다**(2026-09-07). 여유 함수는 세계 밖에서도 값을 내므로, 그냥 두면
        // **테두리 너머로 굴이 계속 이어진다** — 지형도 바위 메시도 없는 **유령 동굴**이다.
        //
        // 그리고 그 유령 굴은 바위가 섞이지 않아 **어디보다 걷기 좋다.** 그래서 *"핵에서
        // 갈 수 있는 가장 먼 곳"*(`EventManager.TryFindEdgeSpawn` → `FarthestReachable`)이
        // **언제나 맵 밖**을 골랐고, 습격대가 **지형도 없는 허공에서 걸어 나왔다**
        // (진단: `[23,-27] 통행가능/바닥있음 · 지면 +0.00m/공중`).
        //
        // **여기가 답하는 자리다.** `RockMap.Mark`는 2026-08-30에 이미 같은 가드를 달았는데
        // (드래그 박스가 지형 끝 너머까지 닿는다) **판정 자체에는 없어서**, 찍는 것만 막히고
        // 길찾기·건설·일감·스폰은 전부 밖을 열린 곳으로 봤다. 한 곳에서 막으면 그 넷이
        // 통째로 따라온다(`MapManager.HasHeadroom`이 이걸 부른다).
        //
        // **`Enabled` 뒤다** — 지하가 아닌 씬(옛 던전)은 `TerrainGrid`와 무관하므로
        // 위에서 이미 빠져나간다.
        // **기반암도 여기서 막는다**(2026-09-15) — 세계 밖을 포함한다(`InBorder`가 밖에서도 참).
        // **캔 칸 앞이다** — 옛 판에서 테두리를 파 둔 기록이 남아 있어도 다시 막힌다.
        if (InBorder(tile)) return true;

        if (_mined.Contains(tile)) return false;

        // **절벽 칸은 바위가 아니다**(2026-09-26) — 허공이다. 막는 것은 `Chasm.Blocks`가 따로 하고,
        // 여기서 거짓이어야 그 위에 벽 덩어리가 안 서고 채굴 표시도 안 붙는다(절벽은 캘 것이 없다).
        if (Chasm.IsChasm(tile)) return false;

        var map   = MapManager.Instance;
        var phase = map != null ? map.GridPhase : Vector2.zero;

        if (_cacheVersion != TerrainCeiling.Version || _cachePhase != phase)
        {
            _cacheVersion = TerrainCeiling.Version;
            _cachePhase   = phase;
            Forget();
        }

        int size = TerrainGrid.WorldSize;
        bool indexed = (uint)tile.x < size && (uint)tile.y < size;
        int  i = 0;

        if (indexed)
        {
            _cache ??= new byte[size * size];
            i = tile.y * size + tile.x;
            byte c = _cache[i];
            if (c != Unknown) return c == Rock;
        }

        // **칸 한가운데에서 잰다** — 모서리에서 재면 이웃 칸과 답이 갈려 벽이 한 줄 어긋난다.
        Vector3 w = map != null ? map.TileToWorld(tile)
                                : new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);

        bool rock = TerrainCeiling.ClearanceAt(w.x, w.z) < PawnMover.BodyHeight;
        if (indexed) _cache[i] = rock ? Rock : Open;
        return rock;
    }

    public static bool IsRock(Vector3 world)
    {
        var map = MapManager.Instance;
        return IsRock(map != null ? map.WorldToTile(world)
                                  : new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z)));
    }

    public static bool IsMarked(Vector2Int tile) => _marked.Contains(tile);

    // 바위 윗면 높이(m) — 그리는 쪽과 커서가 쓴다.
    public static float TopOf(Vector2Int tile) => SurfaceOf(tile) + Height;

    // **벽이 딛고 선 높이 — `MapManager`가 없어도 답한다**(2026-09-12).
    //
    // 전에는 인스턴스가 없으면 **0**으로 떨어뜨렸다. 런타임에는 늘 있으니 안 드러났는데,
    // 씬 뷰에도 벽을 세우게 되면서 터졌다 — 에디터에는 `Instance`가 없고(`Awake`가 세운다)
    // 지하 바닥은 **0이 아니라 몇 m**라, 벽이 전부 y=0에 서서 **땅에 박혀 보였다**
    // (사용자: *"벽이 땅에 박혀있어"*).
    //
    // 지면은 **함수가 답한다**(`TerrainHeight`) — 그래서 인스턴스 없이도 같은 값을 낼 수
    // 있다. 갈리는 것은 **깔린 바닥**뿐이고(그건 런타임 상태다) 편집 중에는 그것이 맞다.
    //
    // **격자 위상을 안 얹는다** — 지하에서는 0으로 못 박혀 있다(`MapManager.ProposeGrid`가
    // `RockMap.Enabled`면 안 건드린다). 그 전제가 바뀌면 여기도 같이 바뀌어야 한다.
    public static float SurfaceOf(Vector2Int tile)
    {
        var map = MapManager.Instance;
        return map != null ? map.SurfaceHeight(tile)
                           : TerrainHeight.At(tile.x + 0.5f, tile.y + 0.5f);
    }

    // **밖에서 온 것이 설 자리는 어디인가 — 답은 여기 하나다**(2026-08-30).
    //
    // 지하로 내려온 뒤 **세계의 3분의 2가 바위**인데, 무엇을 세우는 코드들은 그걸 하나도
    // 안 보고 있었다: 저작 도구가 놓은 마을 · 실체화하는 무리 · 이벤트가 부르는 상인.
    // 증상이 저마다 다르게 보여서 더 나쁘다 — 마을은 **아예 안 보이고**(벽 속이다), 무리는
    // **벽에 낀 채 경고만 찍고**, 상인은 **오긴 오는데 갇혀 있다.**
    //
    // 셋이 각자 *"열린 데를 찾는다"*를 적으면 반드시 갈린다(반경도, 굴을 파는 것을 세는지도).
    // 그래서 **답을 하나로 둔다.**
    //
    // **`MapManager`가 없어도 답한다** — 무리는 로드 안 된 곳에서도 돌고, 저작 도구는 판이
    // 열리기 전에 묻는다. **캔 칸도 열린 것으로 친다**(`IsRock`이 함수와 캔 칸을 합쳐 답한다).
    public const float OpenProbeStep = 2f;

    public static bool TryNearestOpen(Vector3 world, float maxRadius, out Vector3 found)
    {
        found = world;
        if (!Enabled) return true;                  // 지상이면 어디든 열려 있다
        if (!IsRock(world)) return true;

        // **층을 안 넘는다**(2026-10-06). 층 사이 기반암 이음매는 6m라, 경계 근처의 바위에서 고리를 넓히다
        // **옆 층의 굴을 먼저 만날 수 있다** — 그러면 무리가 안 보이는 층에 서고(안개가 가린다) 포탈로 넘은
        // 폰이 다른 층에 떨어진다. 밖에서 온 것이 설 자리를 묻는 문이 여기 하나라 여기서 막는다.
        int floor = Floors.At(world);

        for (float r = OpenProbeStep; r <= maxRadius; r += OpenProbeStep)
        {
            // 고리 하나를 도는 데 필요한 만큼만 굴린다 — 반경이 커도 눈금이 일정하다.
            int steps = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * r / OpenProbeStep));
            for (int i = 0; i < steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                var p = new Vector3(world.x + Mathf.Cos(a) * r, world.y, world.z + Mathf.Sin(a) * r);

                // **세계 밖으로는 안 내보낸다** — 밖은 열려 있다고 나오지만 지형이 없다.
                if (p.x < 1f || p.z < 1f || p.x > TerrainGrid.WorldSize - 1f || p.z > TerrainGrid.WorldSize - 1f)
                    continue;

                if (Floors.At(p) != floor) continue;
                if (IsRock(p)) continue;
                found = p;
                return true;
            }
        }
        return false;
    }

    // ── 바꾸기 ──────────────────────────────────────────────────────────────

    // 캘 것으로 찍는다 / 무른다. **바위가 아닌 칸은 안 찍는다** — 찍어 놓고 아무 일도 안
    // 일어나면 지정이 먹었는지를 알 수 없다.
    public static bool Mark(Vector2Int tile, bool on)
    {
        // **세계 밖은 안 찍는다**(2026-08-30). 드래그 박스는 콜라이더가 아니라 평면과 교차해
        // 칸을 모으므로 **지형이 끝난 자리 너머까지 닿는다** — 거기서도 `IsRock`은 참을
        // 돌려주므로(여유 함수는 세계 밖에서도 값을 낸다) 찍히고, 일감까지 난다.
        if (on && !TerrainGrid.TileInWorld(tile)) return false;
        if (on && InBorder(tile)) return false;   // 기반암 — 캘 수 없다

        if (on && !IsRock(tile)) return false;
        bool changed = on ? _marked.Add(tile) : _marked.Remove(tile);
        if (changed) OnChanged?.Invoke(tile);
        return changed;
    }

    // 실제로 판다. **찍힌 것도 같이 지운다** — 판 자리에 지정이 남으면 다음 일감이 또 난다.
    public static bool Mine(Vector2Int tile)
    {
        // **테두리는 못 캔다.** 밖이 바위가 된 지금 `IsRock`은 참을 돌려주므로, 안 막으면
        // **세계 밖으로 굴을 파고 나갈 수 있다**(`_mined`에 들어가면 그 칸은 영영 열린다).
        // 찍는 쪽(`Mark`)은 이미 막지만 **파는 문은 여기 하나**라 여기서도 물어야 한다.
        if (InBorder(tile)) return false;   // 세계 밖과 기반암 — 캘 수 없다

        if (!IsRock(tile)) return false;

        _mined.Add(tile);
        _dug[tile] = GameTimeManager.Elapsed + RockRegrow.Days * GameTimeManager.DayDuration;
        _marked.Remove(tile);
        _worn.Remove(tile);

        // **길찾기에 알린다.** 안 알리면 격자가 옛 상태로 남아 **판 자리를 폰이 계속 벽으로
        // 본다.** 점유는 안 건드린다 — 바위는 `_occupied`가 아니라 여기가 들고 있으므로,
        // `SetTile`로 알리면 **판 자리를 통행 가능으로 덮어써** 원래 막혀 있던 건물 점유까지
        // 풀린다.
        //
        // **그래서 소식만 보내려고 `SetTile(tile, GetTile(tile))`을 불렀는데 — 그건 아무것도
        // 안 했다**(2026-09-05에 드러났다). `SetTile`은 **값이 실제로 바뀌었을 때만**
        // `OnTileChanged`를 쏘는데, 지금 값을 그대로 넘기니 `changed`가 늘 거짓이었다.
        // 그 결과 **판 칸이 코스·광역 격자에 영영 안 나타났다** — 로컬은 매번 새로 구워서
        // 멀쩡했고, 그래서 *가까이는 가는데 멀리는 못 가는* 모양으로만 보였다.
        //
        // 이제 **여는 쪽이 직접 부른다**(`OpenAround`·`OpenPathToCave`와 같은 자리 —
        // *"격자가 스스로 알 방법은 없다"*).
        var map = MapManager.Instance;
        if (map != null) HierarchicalPathfinder.Instance?.RebakeObstacleAt(map.TileToWorld(tile));

        OnChanged?.Invoke(tile);

        VerifyOpened(tile);
        return true;
    }

    // **판 칸이 진짜로 열렸는지 그 자리에서 확인한다**(2026-09-18).
    //
    // *"팠는데 못 간다"*는 화면에 **그것 하나로만** 보인다 — 벽도 안 보이고 콜라이더도
    // 없는데 폰만 안 간다. 답하는 층이 넷이라(바위·점유·`IsWorkable`·광역 격자) **어느
    // 하나가 옛 답을 들고 있어도 증상이 똑같고**, 그 사실이 어디에도 안 적힌다.
    //
    // **성할 때는 아무 말도 안 한다** — 어긋났을 때만, 그것도 몇 번만 찍는다(한 칸이
    // 어긋나면 대개 그 둘레가 통째로 어긋나 로그가 그것으로 덮인다).
    static int _verifyLeft = 8;

    static void VerifyOpened(Vector2Int tile)
    {
        if (_verifyLeft <= 0) return;

        var map = MapManager.Instance;
        var hp  = HierarchicalPathfinder.Instance;

        bool rock = IsRock(tile);
        bool occ  = map != null && map.IsOccupied(tile);
        bool work = map == null || map.IsWorkable(tile);
        bool wide = hp  == null || hp.WideWalkable(tile);

        if (!rock && !occ && work && wide) return;

        _verifyLeft--;
        Debug.LogWarning($"[채굴] 판 칸 {tile}이 아직 막혀 있다 — "
                       + $"바위 {rock} · 점유 {occ} · IsWorkable {work} · 광역격자 {wide}"
                       + (hp == null ? " · **길찾기가 씬에 없다**" : ""));
    }

    // ── 다시 메운다 (2026-09-28) ────────────────────────────────────────────
    //
    // **판 칸을 바위로 되돌린다** — `Mine`을 거꾸로 돈다. 메울지 말지(핵 영역 · 버팀목 · 그 자리에 무엇이
    // 있나)는 `RockRegrow`가 정하고, 여기는 *바위로 되돌리는 일*만 한다(`RockMap`은 *무엇이 바위인가*에만 답한다).
    //
    // **길찾기와 그리는 쪽에 알리는 것은 `Mine`과 같은 두 줄이다** — 한쪽만 알리면 메워진 벽을 폰이
    // 뚫고 가거나(격자가 옛 답) 보이지 않는 벽에 막힌다(메시가 옛 답).
    public static bool Refill(Vector2Int tile)
    {
        if (!_dug.Remove(tile)) return false;
        if (!_mined.Remove(tile)) return false;
        _worn.Remove(tile);   // 메워진 벽은 새 벽이다

        var map = MapManager.Instance;
        if (map != null) HierarchicalPathfinder.Instance?.RebakeObstacleAt(map.TileToWorld(tile));

        OnChanged?.Invoke(tile);
        return true;
    }

    // **시작 자리를 열어 둔다**(2026-08-29).
    //
    // 세계가 통째로 바위라 **폰이 바위 속에서 시작하면 아무것도 못 한다** — 걸을 수도, 핵을
    // 놓을 수도, 캘 자리를 고를 수도 없다(캐려면 먼저 그 칸에 커서가 닿아야 하는데 사방이
    // 벽이면 어디를 눌러도 같은 벽이다). **저절로 난 굴에 떨어지기를 바랄 수는 없다.**
    //
    // **둥글게 연다** — 네모로 열면 시작 자리가 *누가 판 방*처럼 보인다. 가장자리를 조금
    // 흔들어 자연스럽게 둔다(완전한 원도 사람이 판 것처럼 보인다).
    public static void OpenAround(Vector3 center, float radius)
    {
        if (!Enabled) return;

        var map = MapManager.Instance;
        var c   = map != null ? map.WorldToTile(center)
                              : new Vector2Int(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z));

        int r = Mathf.CeilToInt(radius);
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            float d = Mathf.Sqrt(dx * dx + dy * dy);

            // 가장자리를 흔든다 — 좌표 해시라 다시 열어도 같은 모양이다.
            float wobble = Mathf.PerlinNoise((c.x + dx) * 0.18f, (c.y + dy) * 0.18f) * 2.4f - 1.2f;
            if (d > radius + wobble) continue;

            var t = new Vector2Int(c.x + dx, c.y + dy);
            if (InBorder(t) || !IsRock(t)) continue;

            _mined.Add(t);
            _marked.Remove(t);
            OnChanged?.Invoke(t);
        }

        // **연 넓이만큼 다시 굽는다**(2026-08-31). 칸 하나만 알리면 광역 셀 하나와 코스
        // 몇 칸만 고쳐진다 — 공터는 12m다(한때 여기서 `SetTile(t, GetTile(t))`로 *한 칸만*
        // 알렸는데, 그건 값이 안 바뀌어 아무것도 안 했다 — 2026-09-05). 안 고치면
        // 격자에 **기지가 바위에 둘러싸인 섬**으로 남아, 멀리 찍었을 때 광역 경로가 아예
        // 안 나오고 폰이 조용히 선다.
        HierarchicalPathfinder.Instance?.RebakeArea(center, radius + 2f);
    }

    // **시작 자리를 굴에 잇는다**(2026-08-30).
    //
    // 통로가 그물로 나 있어도 시작 공터가 그 사이 **바위 한복판**에 떨어질 수 있다. 그러면
    // 첫 화면은 여전히 *둥근 웅덩이 하나*이고, 플레이어는 굴이 있다는 것도 어느 쪽에 있는지도
    // 알 수가 없다 — 사방이 똑같은 벽이라 **어디를 팔지가 뽑기가 된다.**
    //
    // 그래서 **가장 가까운 자연 굴까지 좁은 길 하나를 뚫어 둔다.** 공터에서 뻗어 나가는 그
    // 길이 곧 *"저쪽으로 가면 된다"*이고, 굴의 그물에 올라타는 진입로다.
    //
    // **찾는 것은 함수가 답하는 굴이다** — `IsRock`은 캔 칸까지 열린 것으로 치므로 그걸로
    // 찾으면 **방금 연 시작 공터 자신을 찾아** 길이가 0인 길을 뚫고 끝난다.
    public static bool OpenPathToCave(Vector3 center, float searchRadius = 90f, float width = 3f)
    {
        if (!Enabled) return false;

        var map = MapManager.Instance;
        var from = map != null ? map.WorldToTile(center)
                               : new Vector2Int(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z));

        if (!TryFindNaturalCave(from, searchRadius, out var to)) 
        {
            Debug.LogWarning($"[바위] 반경 {searchRadius:0}m 안에 자연 굴이 없다 — 통로 폭이 0이거나 " +
                             "기준 여유가 너무 깊다. `0. Setup World Streaming`을 돌렸는지 볼 것.");
            return false;
        }

        int dug = Carve(from, to, width);

        // **뚫은 길 전체를 다시 굽는다**(2026-08-31). 이 길은 최대 90m라 출발 칸 하나만
        // 알리면 격자는 길이 없는 세계를 계속 들고 있고, 진입로를 뚫어 둔 뜻이 통째로
        // 사라진다(공터가 섬으로 남는다).
        var mid  = new Vector3((from.x + to.x) * 0.5f, 0f, (from.y + to.y) * 0.5f);
        float rr = Vector2.Distance(new Vector2(from.x, from.y), new Vector2(to.x, to.y)) * 0.5f
                 + width + 2f;
        if (map != null) mid = map.TileToWorld(new Vector2Int(Mathf.RoundToInt(mid.x),
                                                             Mathf.RoundToInt(mid.z)));
        HierarchicalPathfinder.Instance?.RebakeArea(mid, rr);

        return dug > 0;
    }

    // 함수가 **굴이라고 답하는** 가장 가까운 칸. 고리 모양으로 넓혀 가며 찾는다 — 가까운
    // 것부터 봐야 길이 짧다. **성기게 훑는다**(2칸) — 통로 폭이 그보다 넓으므로 안 놓친다.
    //
    // **큰길을 먼저 찾는다**(2026-09-15, 코드 리뷰). 큰 굴 중에는 일부러 길에서 떼어 봉한 것이
    // 있어서(`TerrainCeiling` 큰 굴), 가까운 굴에 아무렇게나 이으면 기지가 **봉한 굴 하나에만
    // 붙은 섬**이 된다. 반경 안에 큰길이 없으면 **아무 굴에나 잇는다** — 막힌 시작도 괜찮다
    // (사용자: *"시작 방이 막힌 동굴 형태여도 된다"*). 자동 배치의 시작 방(`WorldPopulator.StartRooms`)과
    // 같은 순서다.
    static bool TryFindNaturalCave(Vector2Int from, float searchRadius, out Vector2Int found) =>
        TryFindNaturalCave(from, searchRadius, true, out found) ||
        TryFindNaturalCave(from, searchRadius, false, out found);

    static bool TryFindNaturalCave(Vector2Int from, float searchRadius, bool roadOnly, out Vector2Int found)
    {
        found = from;
        var map = MapManager.Instance;
        int max = Mathf.CeilToInt(searchRadius);

        for (int r = 4; r <= max; r += 2)
        for (int dy = -r; dy <= r; dy += 2)
        for (int dx = -r; dx <= r; dx += 2)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;   // 고리 위만

            var t = new Vector2Int(from.x + dx, from.y + dy);
            if (InBorder(t)) continue;   // 기반암 너머의 굴로 길을 내면 기반암에 막혀 끊긴다
            Vector3 w = map != null ? map.TileToWorld(t) : new Vector3(t.x + 0.5f, 0f, t.y + 0.5f);
            if (roadOnly ? TerrainCeiling.RoadOpenness(w.x, w.z) < 0.99f
                         : TerrainCeiling.ClearanceAt(w.x, w.z) < PawnMover.BodyHeight) continue;

            found = t;
            return true;
        }
        return false;
    }

    // 두 칸을 잇는 굴을 판다. **반 칸씩 걸어가며** 둘레를 연다 — 칸 단위로 뛰면 대각선에서
    // 모서리만 닿아 **폰이 못 지나가는 길**이 난다(대각선은 직교 이웃 둘이 다 열려야 간다).
    static int Carve(Vector2Int from, Vector2Int to, float width)
    {
        float half = Mathf.Max(0.5f, width * 0.5f);
        var a = new Vector2(from.x, from.y);
        var b = new Vector2(to.x,   to.y);

        float len   = Vector2.Distance(a, b);
        int   steps = Mathf.Max(1, Mathf.CeilToInt(len * 2f));
        int   r     = Mathf.CeilToInt(half);
        int   dug   = 0;

        for (int i = 0; i <= steps; i++)
        {
            var c = Vector2.Lerp(a, b, i / (float)steps);
            int cx = Mathf.RoundToInt(c.x), cy = Mathf.RoundToInt(c.y);

            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > half * half) continue;

                var t = new Vector2Int(cx + dx, cy + dy);
                if (InBorder(t) || !IsRock(t)) continue;

                _mined.Add(t);
                _marked.Remove(t);
                OnChanged?.Invoke(t);
                dug++;
            }
        }
        return dug;
    }

    // 판을 새로 열 때. **`_marked`도 비운다** — 안 비우면 지난 판의 지정이 남는다.
    public static void Clear()
    {
        _mined.Clear();
        _dug.Clear();
        _marked.Clear();
        _worn.Clear();
        OnChanged?.Invoke(Vector2Int.zero);
    }

    // ── 광맥 ────────────────────────────────────────────────────────────────
    //
    // **좌표 해시로 결정적으로 심는다** — 나무·바위를 뿌리는 것과 같은 방식이라 저장할 것이
    // 없고, 캤다가 다시 와도(그럴 일은 없지만) 같은 자리에 같은 것이 있다.
    //
    // **무엇이 나오는지는 바이옴이 정한다** — `BiomeContentData.ResourceNodes`가 이미 그
    // 목록이다(*"이 바이옴에서 등장하는 자원 노드 풀"*). 여기서 새 표를 만들면 저작이 두 벌이 된다.
    //
    // **덩어리로 심는다.** 칸마다 따로 굴리면 광맥이 소금처럼 흩뿌려져 *"광맥을 따라 판다"*가
    // 성립하지 않는다 — 큰 주기 노이즈로 덩어리를 만들고 그 안에서만 굴린다.
    public const float VeinPeriod = 22f;    // 덩어리 크기(m)

    [Range(0f, 1f)] public static float VeinDensity = 0.16f;

    // **광맥이 없는 맨바위를 캐면 무엇이 나오나.** 씬이 든다(`RockRenderer`가 밀어 넣는다).
    //
    // 전에는 `ResourceGroup.Stone` 묶음의 **첫 번째**를 집었는데, 그 묶음에 **뼈**가 같이
    // 들어 있어서 **벽을 파면 뼈가 나왔다.** 묶음은 *"창고 필터와 화면 분류"*의 축이지
    // *"이 자원이 어디서 나오나"*의 축이 아니다 — 거기서 골라 쓴 것이 잘못이었다.
    //
    // **`ResourceNodeData`를 그대로 쓴다** — 광맥이 이미 그 타입이라 파는 쪽이 **한 가지
    // 모양만** 다루면 된다(나오는 것 · 개수 · 얼마나 오래 걸리나가 다 그 안에 있다).
    // `5_Data/Resource/Stone.asset`이 그 답을 이미 들고 있었다(돌 2~4개 · 게이지 60).
    public static ResourceNodeData PlainRock;

    public static ResourceNodeData VeinAt(Vector2Int tile)
    {
        if (!Enabled) return null;

        // ① 덩어리 안인가
        float n = Mathf.PerlinNoise((tile.x + 0.5f) / VeinPeriod + 91.7f,
                                    (tile.y + 0.5f) / VeinPeriod + 43.1f);
        // **가장 촘촘한 바이옴으로도 덩어리 밖이면 바이옴을 안 묻는다** — 칸 대부분이 여기서 끝난다.
        if (n < 1f - VeinDensity * BiomeContentData.MaxVeinRichness) return null;

        // ② 이 바이옴의 풀에서 하나 — **`string.GetHashCode`를 안 쓴다**(실행마다 달라진다).
        var map = MapManager.Instance;
        Vector3 w = map != null ? map.TileToWorld(tile)
                                : new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);

        var content = BiomeContent.Get(WorldMap.BiomeAtWorld(new Vector2(w.x, w.z)));
        var pool    = content != null ? content.ResourceNodes : null;
        if (pool == null || pool.Count == 0) return null;

        // 덩어리 문턱은 바이옴이 민다(`BiomeContentData.VeinRichness` — 수정동은 촘촘하다).
        if (n < 1f - VeinDensity * content.VeinRichness) return null;

        int h = tile.x * 73856093 ^ tile.y * 19349663;
        return pool[Mathf.Abs(h) % pool.Count];
    }

    // ── 벽에 박힌 조명 (2026-09-28) ─────────────────────────────────────────
    //
    // **이 칸에 야광석·횃불이 박혔나** — 광맥과 같이 좌표 해시로 정하고 저장하지 않는다. 표는
    // 바이옴이 든다(`BiomeContentData.WallLights`). **바위인지는 안 묻는다** — 파낸 뒤에도 같은
    // 답이어야 `MineJob`이 파고 나서 덤을 떨굴 수 있다. 보일지는 그리는 쪽(`RockRenderer`)이
    // *드러난 면이 있나*로 가른다.
    //
    // **기반암에는 안 박는다** — 못 캐는 벽에 캐면 나오는 것이 보이면 거짓말이다(광맥과 같은 규칙).
    public static WallLightData WallLightAt(Vector2Int tile)
    {
        if (!Enabled || IsBedrock(tile)) return null;

        var map = MapManager.Instance;
        Vector3 w = map != null ? map.TileToWorld(tile)
                                : new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);

        var content = BiomeContent.Get(WorldMap.BiomeAtWorld(new Vector2(w.x, w.z)));
        var list    = content != null ? content.WallLights : null;
        if (list == null || list.Count == 0) return null;

        // **광맥과 다른 씨앗을 쓴다** — 같은 해시면 광맥 덩어리 안에서만 조명이 몰린다.
        float r = Hash01(tile, 0x9E3779B9u);
        foreach (var e in list)
        {
            if (e == null || e.Light == null) continue;
            if (r < e.Chance) return e.Light;
            r -= e.Chance;
        }
        return null;
    }

    // 칸 좌표 → [0, 1). **`Random`을 안 쓴다** — 다시 와도 같은 자리에 같은 것이 있어야 한다.
    public static float Hash01(Vector2Int t, uint salt)
    {
        unchecked
        {
            uint h = (uint)t.x * 73856093u ^ (uint)t.y * 19349663u ^ salt;
            h ^= h >> 16; h *= 0x7FEB352Du;
            h ^= h >> 15; h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
