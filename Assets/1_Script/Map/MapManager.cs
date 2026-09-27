using System;
using System.Collections.Generic;
using UnityEngine;

// 정착지 단위 타일 그리드. 씬에 여러 개 공존 가능 (플레이어 던전 + NPC 도시 등).
// - 1 tile = 1 world unit, 절대 월드 좌표 기준 (tile.x = floor(world.x), tile.y = floor(world.z))
// - Instance = 플레이어 던전존 (DungeonCore가 RecenterAt 호출)
// - GetForPosition(worldPos) = 해당 좌표를 포함하는 존 반환 → 미래 다중 정착지 길찾기 진입점
[DefaultExecutionOrder(-50)]
public class MapManager : MonoBehaviour
{
    // 플레이어 던전존. DungeonCore 전용 API(RecenterAt 등)에 사용.
    public static MapManager Instance { get; private set; }

    // 모든 활성 존 레지스트리 (플레이어 + NPC 정착지)
    static readonly List<MapManager> _allZones = new();

    public static event Action<Vector2Int> OnTileChanged;
    public static event Action OnZoneRecentered;
    // 바닥 타일 등록/해제 통보 (바닥 지오메트리 렌더링용 — 후속).
    public static event Action<Vector2Int> OnFloorChanged;

    [Header("빌드존 크기 (타일 단위) — 최대 바깥 경계 클램프. 실제 건설 게이트는 코어 union.")]
    [SerializeField] int buildZoneSize = 200;

    [Header("건설 — 평탄 판정")]
    [Tooltip("작은 건물이 잔기복에 걸리지 않게 하는 기본 여유(m). 1×1이 이만큼까지는 봐준다.")]
    [SerializeField] float buildHeightTolerance = 0.6f;

    [Tooltip("발자국 한 변당 더 봐주는 높이(m/m) — **기울기**다. 이게 없으면 큰 건물일수록 " +
             "불리해진다(같은 비탈이라도 넓을수록 절대 높이차가 커지므로). 0.2면 5m짜리 " +
             "건물이 1m까지 기울어도 선다. 자동 배치가 마을을 앉힐 때 쓰는 눈금이 0.15다.")]
    [SerializeField, Range(0f, 0.6f)] float buildSlopeTolerance = 0.2f;

    [Header("절차 지형 (켄시 규모 이어진 세계)")]
    [Tooltip("켜면 지면 높이를 `TerrainHeight`(노이즈 + 손댄 델타)에서 읽는다. " +
             "Terrain·콜라이더를 안 보므로 로드 안 된 청크도 답이 나온다. " +
             "손으로 조각한 Terrain 한 장짜리 맵은 꺼 둘 것.")]
    [SerializeField] bool proceduralTerrain = false;

    [Header("지형 샘플")]
    [Tooltip("지면 높이 레이캐스트 마스크. Terrain이 있으면 불필요. 평면 바닥 씬은 바닥 레이어만 지정. " +
             "Everything/Nothing이면 레이캐스트 생략(평지 y=0 처리) — 건물/유닛 오인 방지.")]
    [SerializeField] LayerMask groundMask = 0;

    // 점유 타일 희소 저장 (절대 월드 타일 좌표). 핵 이동(RecenterAt)에도 살아남는다.
    // ResourceNode 등 막힌 타일만 등록 — 비면 통행 가능이 기본값.
    readonly HashSet<Vector2Int> _occupied = new();

    // 바닥 타일 희소 저장 (절대 월드 타일 좌표, 모델 A). 코어와 무관하게 깔리는 substrate.
    // 비면 = 바닥 시스템 미사용(필드/지형 맵) → 건물이 바닥을 요구하지 않음.
    // 하나라도 있으면 = 던전형 맵 → 건물은 바닥 위(floored)에만 배치 가능.
    // 값 = 바닥 윗면 월드 Y (건물이 이 높이 위에 얹힌다). 슬래브 두께를 반영하려고 높이까지 들고 있음.
    readonly Dictionary<Vector2Int, float> _floored = new();

    Vector2Int _zoneOriginTile; // 빌드존 좌하단 타일 (절대 월드 타일 좌표)

    // **핵이 아직 없으면 빌드존도 없다**(2026-08-27). 빌드존은 *"핵 둘레 이만큼"*이라는
    // 뜻이라 **가운데가 정해져야 성립한다.** 판이 열릴 때 핵을 안 주기로 하면서(사용자 결정)
    // 그 가운데가 없는 시간이 생겼는데, 그때 원점 둘레 200m가 유일한 건설 가능 구역이 되어
    // **핵을 놓을 자리를 고르는 것 자체가 막혔다** — 세계가 8km인데.
    //
    // 그래서 **가운데가 잡히기 전에는 아무 데나 놓을 수 있다.** 핵이 서면 그 자리를 중심으로
    // 잠기고, 그때부터는 지금까지와 똑같다.
    bool _centered;

    // **격자의 위상 — 첫 핵이 정한다**(2026-08-27, 사용자 결정).
    //
    // 전에는 격자가 **세계 원점에 박혀 있었다** — 타일이 언제나 정수 월드 좌표였다. 200m
    // 한 장짜리 필드맵일 때는 그게 곧 지도였으니 맞았는데, 세계가 8km가 되자 **아무 근거도
    // 없는 격자**가 됐다. 핵을 아무 데나 꽂아도 건물은 세계 원점 기준으로 줄을 서서,
    // 거점이 제 격자에 안 맞는다.
    //
    // 이제 **첫 핵을 꽂은 자리가 격자의 원점**이다. 다만 옮기는 것은 **위상(0~1)뿐**이다 —
    // 타일 인덱스는 그대로 정수라, 타일을 쓰는 코드는 아무것도 안 바뀐다.
    //
    // **한 번 정해지면 다시는 안 움직인다.** 점유·바닥·건물이 전부 타일 좌표로 저장돼 있어서,
    // 나중에 위상이 밀리면 **이미 지은 것들이 통째로 반 칸 어긋난다.** 그래서 두 번째 핵도,
    // 핵 이전도 격자를 안 건드린다 — 첫 호출만 정한다.
    Vector2 _gridPhase;
    bool    _gridSet;

    public bool HasBuildZone => _centered;

    // 격자가 정해졌나. **가제안도 정해진 것으로 친다**(2026-08-28) — 아래 `ProposeGrid` 참고.
    // **지하에서는 판이 열릴 때부터 정해져 있다** — 바로 아래 설명.
    public bool HasGrid    => _gridSet || _gridProposed || RockGrid;

    // 다시는 안 움직이나. 화면에 *"아직 확정 전"*을 알릴 때만 쓴다.
    public bool GridLocked => _gridSet || RockGrid;

    // ── 지하에서는 바위가 격자의 근거다 ────────────────────────────────────
    //
    // **위상이 커서를 따라 움직인다는 전제는 "격자 위에 아직 아무것도 없다"였다.** 지상에서는
    // 참이었다 — 핵을 꽂기 전 세계에는 격자에 맞춰 놓인 것이 하나도 없었으니 위상은 아무 값이나
    // 돼도 됐다.
    //
    // **지하에서 그 전제가 깨졌다**(2026-08-30). 판이 열리는 순간 이미 **세계 전체가 격자에
    // 맞춰 선 바위**다. 그런데 `RockMap.IsRock`은 칸을 월드 좌표로 되돌려(`TileToWorld`)
    // 여유를 재므로, **위상이 0.4m 밀리면 세계의 모든 바위가 0.4m 움직인다** — 그리는 것만이
    // 아니라 *어디가 바위인가*라는 답 자체가 밀린다.
    //
    // 증상은 이렇게 나타났다(사용자: *"채굴을 지정하면 돌벽의 위치가 달라진다"*): 건설 모드에
    // 들어가 커서를 움직이면 `ProposeGrid`가 매 프레임 위상을 바꾸는데, **이미 구워 둔 바위
    // 메시는 옛 위상 그대로다.** 그러다 한 칸을 찍으면 그 청크만 다시 구워져 — **그 16×16
    // 덩어리가 통째로 1m 미만만큼 미끄러진다.**
    //
    // **그래서 지하에서는 위상을 0으로 못 박는다.** *"근거 없는 세계 원점 격자를 쓰지 않는다"*는
    // 원래 규칙의 뜻은 지켜진다 — 근거가 **바위**이고, 바위는 정수 월드 좌표 위에 서 있다.
    static bool RockGrid => RockMap.Enabled;

    public Vector2 GridPhase => _gridPhase;

    // ── 아직 확정되지 않은 격자 ────────────────────────────────────────────
    //
    // **핵을 놓기 전에도 격자로 지어야 한다**(2026-08-28). 위상을 첫 핵이 정하기로 한
    // 뒤로, 핵을 꽂기 전에는 격자가 아예 없어서 **자유 배치뿐**이었다 — 그러면 두 번
    // 클릭으로 줄·면을 한 번에 까는 길(`DragBuild`)이 통째로 막힌다. 바닥 한 장을
    // 깔려고 백 번을 눌러야 하는 것은 격자가 없다는 사실보다 훨씬 큰 불편이다.
    //
    // 그래서 **정하는 순간을 앞당긴다**: 커서가 정할 위상을 계속 제안해 두고(`Propose`),
    // **첫 격자 조작이 그것을 붙든다**(`Claim`). 원래 규칙의 뜻은 그대로다 —
    // *"근거 없는 세계 원점 격자를 쓰지 않는다"*와 *"한 번 정해지면 다시는 안 움직인다"*.
    // 달라진 것은 그 근거를 **핵만 줄 수 있다**는 제약뿐이고, 그건 필요하지 않았다.
    bool _gridProposed;

    // 아직 붙들리지 않았으면 이 자리를 칸 한복판으로 삼는 위상을 제안한다.
    public void ProposeGrid(Vector3 worldCenter)
    {
        if (_gridSet) return;

        // **바위가 이미 격자 위에 서 있으면 제안할 것이 없다** — 위 `RockGrid` 참고.
        // 여기서 안 막으면 커서를 움직이는 것만으로 세계의 바위가 통째로 미끄러진다.
        if (RockGrid) return;

        _gridPhase    = new Vector2(Frac(worldCenter.x - 0.5f), Frac(worldCenter.z - 0.5f));
        _gridProposed = true;
    }

    // 제안을 확정한다 — 이 뒤로는 커서를 따라 움직이지 않는다.
    public void ClaimGrid()
    {
        if (_gridProposed) _gridSet = true;
    }

    public int Width  => buildZoneSize;
    public int Height => buildZoneSize;
    public Vector2Int ZoneOriginTile => _zoneOriginTile;

    void Awake()
    {
        _allZones.Add(this);
        if (Instance == null) Instance = this;
    }

    void OnDestroy()
    {
        _allZones.Remove(this);
        if (Instance == this) Instance = null;
    }

    // 월드 좌표를 포함하는 존 반환. 어느 존에도 없으면 Instance 폴백.
    public static MapManager GetForPosition(Vector3 worldPos)
    {
        foreach (var zone in _allZones)
            if (zone.IsInBuildZone(worldPos)) return zone;
        return Instance;
    }

    // DungeonCore가 호출 — 이 존을 플레이어 던전존으로 지정.
    public void SetAsPlayerZone() => Instance = this;

    // 미궁핵 이동 시 호출. 빌드존 중심(라우팅용 origin)만 옮긴다.
    // 점유(_occupied)는 절대좌표 희소 저장이라 wipe하지 않음 → "이동 시 빌드존 리셋" 방지.
    // **음수에서도 0~1이다** — `x % 1`은 음수 쪽에서 음수를 돌려줘 위상이 반대로 밀린다.
    static float Frac(float x) => x - Mathf.Floor(x);

    public void RecenterAt(Vector3 worldCenter)
    {
        // **첫 핵만 격자를 정한다.** 위의 `_gridPhase` 설명 참고 — 나중에 밀면 이미 지은
        // 것이 통째로 반 칸 어긋난다. 핵이 칸 한복판에 오도록 위상을 잡는다.
        // **지하에서는 핵도 격자를 못 옮긴다** — 이미 바위가 그 격자 위에 서 있고, 밀면
        // 세계의 벽이 통째로 미끄러진다(위 `RockGrid`).
        if (!_gridSet && !RockGrid)
        {
            _gridPhase = new Vector2(Frac(worldCenter.x - 0.5f), Frac(worldCenter.z - 0.5f));
            _gridSet   = true;
        }

        var centerTile = WorldToTile(worldCenter);
        _zoneOriginTile = centerTile - new Vector2Int(buildZoneSize / 2, buildZoneSize / 2);
        _centered = true;
        OnZoneRecentered?.Invoke();
    }

    // ── 좌표 변환 (절대 월드 타일) ────────────────────────────────────────────

    public Vector2Int WorldToTile(Vector3 world) =>
        new Vector2Int(Mathf.FloorToInt(world.x - _gridPhase.x),
                       Mathf.FloorToInt(world.z - _gridPhase.y));

    public Vector3 TileToWorld(Vector2Int tile)
    {
        // Y는 0(평면) 기준. 지형 높이는 배치 시 온디맨드 샘플(step 2)에서 처리.
        return new Vector3(tile.x + 0.5f + _gridPhase.x, 0f, tile.y + 0.5f + _gridPhase.y);
    }

    // **위상까지 얹는다** — 격자를 그리는 쪽(`GridOverlay`)이 이걸 쓰므로, 안 얹으면
    // 선은 세계 원점 기준인데 건물은 핵 기준이 되어 **눈으로 보이는 격자가 거짓말을 한다.**
    public Vector3 WorldOrigin =>
        new Vector3(_zoneOriginTile.x + _gridPhase.x, 0f, _zoneOriginTile.y + _gridPhase.y);

    // ── 빌드존 판정 ────────────────────────────────────────────────────────────

    public bool IsInBuildZone(Vector2Int tile) =>
        TryToLocal(tile, out _, out _);

    public bool IsInBuildZone(Vector3 world) => IsInBuildZone(WorldToTile(world));

    // 하위 호환: 기존 코드의 IsInBounds 호출은 빌드존 검사로 매핑
    public bool IsInBounds(Vector2Int tile) => IsInBuildZone(tile);
    public bool IsInBounds(int x, int y)    => IsInBuildZone(new Vector2Int(x, y));

    bool TryToLocal(Vector2Int tile, out int lx, out int ly)
    {
        // 가운데가 없으면 자를 것도 없다 — 위의 `_centered` 설명 참고.
        if (!_centered) { lx = ly = 0; return true; }

        lx = tile.x - _zoneOriginTile.x;
        ly = tile.y - _zoneOriginTile.y;
        return lx >= 0 && lx < buildZoneSize && ly >= 0 && ly < buildZoneSize;
    }

    // ── 점유 ─────────────────────────────────────────────────────────────────
    //
    // **칸이 든 것은 "막혔나" 하나다.** 전에는 `TileData`(종류·이동 벌점·높이·법선)를 돌려줬는데
    // 읽는 쪽은 전부 `IsPassable`만 봤고 나머지 셋은 아무도 안 채웠다(2026-09-09에 걷어냈다).
    // 빌드존 경계와 무관한 절대좌표 희소 저장이라 핵 이동에도 유지된다.
    public bool IsOccupied(Vector2Int tile) => _occupied.Contains(tile);
    public bool IsOccupied(int x, int y)   => _occupied.Contains(new Vector2Int(x, y));

    // 값이 실제로 바뀔 때만 알린다 — 같은 값을 다시 넣는 것은 소식이 아니다.
    public void SetOccupied(Vector2Int tile, bool occupied)
    {
        bool changed = occupied ? _occupied.Add(tile) : _occupied.Remove(tile);
        if (changed) OnTileChanged?.Invoke(tile);
    }

    // ── 바닥(floor) ────────────────────────────────────────────────────────────
    // 코어와 무관한 substrate. Floor 컴포넌트가 자기 footprint를 등록/해제한다.

    // 깔린 바닥 칸 수. **격자가 다시 그릴 때를 아는 데 쓴다** — 바닥이 늘거나 줄면
    // 격자가 앉을 면이 바뀐다(`GridOverlay`).
    public int FloorCount => _floored.Count;

    // 깔린 칸 전부. `FloorRenderer`가 처음 설 때 한 번 훑는다.
    public IEnumerable<Vector2Int> FloorTiles => _floored.Keys;
    // **바닥은 기단이지 발판이 아니다**(2026-09-09). 한때 *"바닥 위에만 서고 짓는다"*는
    // 요구(`FloorRequired`)가 있었다 — 발밑이 공허인 던전 씬의 것이고, 그 설계를 접은 뒤에도
    // 씬의 `requireFloor: 1` 잔재 하나가 세계 전체의 건설을 막은 적이 있다(2026-08-27).
    // 지하는 어디에나 땅이 있으므로 그 축을 통째로 걷어냈다.
    public bool IsFloored(Vector2Int tile) => _floored.ContainsKey(tile);

    // 바닥 윗면 Y (바닥 없으면 false). 배치 높이 계산용.
    public bool TryGetFloorSurface(Vector2Int tile, out float surfaceY) => _floored.TryGetValue(tile, out surfaceY);

    // 유닛이 설 수 있는 타일인가. 점유(IsOccupied)와는 별개 축이므로 필요 시 둘 다 검사할 것.
    //
    // **머리 위를 본다**(2026-08-29). 지하에서 *"설 수 있나"*는 발밑과 머리 위 **둘 다**의
    // 물음이다 — 여유가 폰 키보다 작으면 바위 속이다. 묻는 자리를 여기 하나로 두면 길찾기도
    // 건설도 일감 고르기도 **같은 답**을 쓴다(`IsWorkable`이 이걸 부르고 `LocalGridManager`도
    // 이걸 본다). 갈라 두면 *"경로는 있는데 서 있을 자리가 없다"*가 생긴다.
    //
    // **천장이 꺼져 있으면 언제나 참이라 지상 판정은 한 줄도 안 바뀐다.**
    public bool IsStandable(Vector2Int tile) => HasHeadroom(tile);

    // 그 칸을 지나갈 수 있나 — **지하에서는 곧 "바위가 아닌가"다**(2026-08-29).
    //
    // 처음엔 천장 높이를 직접 쟀는데, 바위를 **캘 수 있게** 만들면서 답이 한 겹 더 생겼다:
    // 판 칸은 천장 함수로는 여전히 바위다(함수는 판 것을 모른다). **`RockMap`이 그 둘을
    // 합쳐 답하므로** 여기서 다시 재면 갈린다 — 판 자리를 폰이 계속 벽으로 본다.
    // **절벽도 여기서 막는다**(2026-09-26) — 바위와 같은 자리라 길찾기·건설·일감이 통째로 따라온다.
    // 다리가 깔린 절벽 칸은 지나간다(`Chasm.Blocks`).
    public bool HasHeadroom(Vector2Int tile) => !RockMap.IsRock(tile) && !Chasm.Blocks(tile);
    public bool IsStandable(Vector3 world)   => IsStandable(WorldToTile(world));

    // 폰이 **실제로 설 수 있는** 칸인가 — 통행 가능 + 바닥 있음.
    public bool IsWorkable(Vector2Int tile) => !IsOccupied(tile) && IsStandable(tile);

    // 두 칸 사이의 **직선이 뚫려 있나.** 길찾기가 아니라 **어림값**이다 — 막혀 있으면
    // *"실제로는 돌아가야 한다"*는 뜻이고, 뚫려 있어도 지형 턱까지 봐 주지는 않는다.
    //
    // **경로를 재는 것과는 값이 다르다.** 후보마다 길찾기를 돌리면 요청 한 번에 A*를
    // 여러 번 굴리게 되는데, 이건 사전 몇십 번이면 끝난다. **그만큼만 하는 것이 요점이다.**
    //
    // `maxTiles`보다 멀면 **뚫린 것으로 본다** — 모르는 것을 막힌 것으로 치면 먼 일감이
    // 전부 벌점을 받아, 거리로 이미 지고 있는 쪽을 한 번 더 깎는 꼴이 된다.
    public bool LineIsClear(Vector2Int a, Vector2Int b, int maxTiles)
    {
        int dx = Mathf.Abs(b.x - a.x), dz = Mathf.Abs(b.y - a.y);
        if (dx + dz > maxTiles) return true;

        int sx = a.x < b.x ? 1 : -1, sz = a.y < b.y ? 1 : -1;
        int err = dx - dz;
        int x = a.x, z = a.y;

        while (x != b.x || z != b.y)
        {
            int e2 = err * 2;
            if (e2 > -dz) { err -= dz; x += sx; }
            if (e2 <  dx) { err += dx; z += sz; }

            if (x == b.x && z == b.y) break;      // 도착 칸 자체는 안 본다(일감이 거기 있다)

            // **바위도 막는 것이다**(2026-09-10). 여기서 점유(`IsOccupied`)만 보던 시절은
            // 지상이었다 — 지하에서는 통행을 막는 것의 거의 전부가 바위라, 안 보면 이 함수가
            // **통짜 바위를 뚫린 것으로** 답한다. `IsWorkable` = 통행 가능 + 설 수 있음.
            if (!IsWorkable(new Vector2Int(x, z))) return false;
        }
        return true;
    }

    // 그 칸으로 **올라설 때** 허용되는 높이차(m). 바닥이 깔린 칸은 옆면이 수직이라 폰이
    // 넘을 수 있는 턱까지, 자연 지형은 비탈이라 `slopeLimit`이 판단한다.
    //
    // **길찾기와 일자리 고르기가 같은 답을 써야 한다** — 갈리면 *"경로는 있는데 서 있을
    // 자리가 없다"*(또는 그 반대)가 되고, 폰은 그 자리에서 일감을 집었다 놓기만 한다.
    public float ClimbLimitAt(Vector2Int tile) =>
        IsFloored(tile) || Chasm.HasDeck(tile) ? PawnMover.StepUp : PawnMover.SlopeRise;   // 다리 윗면은 기단과 같은 턱

    // `from`에 선 폰이 `to`에 설 수 있나 — **양쪽으로** 묻는다(가서 일하고 돌아와야 한다).
    public bool SameLevel(Vector2Int from, Vector2Int to)
    {
        float hf = SurfaceHeight(from), ht = SurfaceHeight(to);
        float up = ht > hf ? ClimbLimitAt(to) : ClimbLimitAt(from);
        return Mathf.Abs(ht - hf) <= up + 0.01f;
    }

    // `center` 주변에서 설 수 있는 가장 가까운 칸을 찾는다. 못 찾으면 `center` 자신.
    //
    // **왜 반경을 넓혀야 하나**: 예전엔 상하좌우 4칸만 봤다. 건물·자원 노드가 몰려 있으면
    // 네 칸이 전부 막히는 일이 흔한데, 그때 막힌 칸 자신을 목적지로 돌려주면 폰은
    // **영영 도착 판정에 못 걸린다**(6초 뒤 일감을 놓고, 다시 집고, 또 놓는다).
    // 대각선과 반경 2까지 보면 대부분 살아난다.
    //
    // `exclude`는 **지금은 지나갈 수 있지만 서면 안 되는 칸**이다 — 청사진 발자국이 그렇다.
    // 통행 판정만으로는 못 거른다(청사진은 일부러 칸을 안 막는다).
    //
    // `prefer`는 **거르는 조건이 아니라 고르는 눈금이다**(2026-09-10). 같은 고리 안에 후보가
    // 여럿일 때 이것을 만족하는 쪽이 이긴다 — 없으면 예전처럼 가장 가까운 것. 걸러 버리면
    // *"조건에 맞는 자리가 없으면 아예 못 간다"*가 되는데, 그건 서 있을 자리를 잃는 것이라
    // 훨씬 나쁘다(폰을 부르는 쪽이 이 함수에 기대는 것은 **어디든 설 자리 하나**다).
    public Vector2Int FindWorkableNear(Vector2Int center, int maxRadius = 2,
                                       ICollection<Vector2Int> exclude = null,
                                       System.Func<Vector2Int, bool> prefer = null)
    {
        // **낭떠러지 건너편은 가까워도 후보가 아니다**(2026-08-28). 기단 가장자리에 건물을
        // 지으면 *가장 가까운* 설 자리가 **기단 아래**로 뽑혔고, 폰은 거기로 가려다 못 가서
        // 건설이 통째로 멎었다 — 화면에는 "아무도 안 온다"로만 보인다.
        bool Allowed(Vector2Int t) =>
            IsWorkable(t) && SameLevel(center, t) && (exclude == null || !exclude.Contains(t));

        if (Allowed(center)) return center;

        for (int r = 1; r <= maxRadius; r++)
        {
            Vector2Int best = center;
            float bestDist = float.MaxValue;
            bool  bestPref = false;

            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                // 이번 고리(껍질)만 본다 — 안쪽은 앞선 반경에서 이미 봤다.
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;

                var t = new Vector2Int(center.x + dx, center.y + dy);
                if (!Allowed(t)) continue;

                float d = dx * dx + dy * dy;      // 같은 고리 안에서는 대각선보다 직선이 가깝다
                bool  p = prefer == null || prefer(t);

                // 선호가 먼저, 그다음 가까움. 갈라 두면 *바위 건너편의 더 가까운 칸*이 이긴다.
                if (p != bestPref ? p : d < bestDist) { bestDist = d; best = t; bestPref = p; }
            }

            if (bestDist < float.MaxValue) return best;
        }
        return center;
    }

    // surfaceY = 바닥 윗면 월드 Y. 같은 타일에 여러 바닥이 겹치면 더 높은 면을 채택(건물이 위에 얹히도록).
    public void RegisterFloor(Vector2Int tile, float surfaceY = 0f)
    {
        if (_floored.TryGetValue(tile, out float prev))
        {
            // **높이가 바뀌면 알린다** — 그리는 쪽(`FloorRenderer`)이 못 들으면 메시가
            // 옛 높이 그대로 남아, 판정과 화면이 갈린다.
            if (surfaceY > prev) { _floored[tile] = surfaceY; OnFloorChanged?.Invoke(tile); }
            return;
        }
        _floored[tile] = surfaceY;
        OnFloorChanged?.Invoke(tile);
    }

    public void UnregisterFloor(Vector2Int tile)
    {
        if (_floored.Remove(tile)) OnFloorChanged?.Invoke(tile);
    }

    // ── 온디맨드 지형 샘플 ──────────────────────────────────────────────────────

    // 월드 (x,z)의 지면 높이.
    //  0) 절차 지형이 켜져 있으면 `TerrainHeight`(노이즈 + 델타). **콜라이더도 Terrain도 안 본다.**
    //  1) Terrain.activeTerrain → 지면만 샘플(건물/유닛에 안 걸림).
    //  2) groundMask가 "특정 레이어"로 설정된 경우에만 레이캐스트. Everything(~0)/Nothing(0)이면
    //     엉뚱한 콜라이더(건물·코어·유닛)를 지면으로 오인할 수 있어 건너뛰고 평지(0)로 처리.
    //  3) 그 외 → 0 (평지 가정). 평면 바닥 씬은 groundMask를 바닥 레이어로 지정하면 됨.
    //
    // **0번이 맨 앞인 이유**(2026-08-26): 켄시 규모의 이어진 세계에서는 지형이 청크로
    // 스트리밍되므로 **아직 로드 안 된 곳의 높이를 물어야 할 때가 있다**(길찾기가 먼 곳의
    // 통행을 재거나, 폰이 지도 반대편에 있을 때). 1·2번은 둘 다 **그 자리에 물건이 실제로
    // 있어야** 답하므로 그때 0을 돌려주고, 폰이 지면 아래로 떨어진다.
    //
    // **기존 씬은 끄고 쓴다** — 손으로 조각한 Terrain 한 장짜리 맵은 1번이 맞다.
    public float SampleGroundHeight(float worldX, float worldZ)
    {
        if (proceduralTerrain) return TerrainHeight.At(worldX, worldZ);

        var t = Terrain.activeTerrain;
        if (t != null) return t.SampleHeight(new Vector3(worldX, 0f, worldZ)) + t.transform.position.y;

        int mask = groundMask.value;
        if (mask != 0 && mask != ~0)
        {
            var origin = new Vector3(worldX, 500f, worldZ);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 1000f, mask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
        }
        return 0f;
    }

    public float SampleGroundHeight(Vector3 worldPos) => SampleGroundHeight(worldPos.x, worldPos.z);

    // 타일의 표면 높이 — 바닥이 깔려 있으면 그 윗면, 아니면 지형/평지 높이.
    // 건물은 이 높이 "위에" 얹혀야 한다(바닥 슬래브 두께만큼 파묻히지 않도록).
    public float SurfaceHeight(Vector2Int tile)
    {
        if (_floored.TryGetValue(tile, out float floorY)) return floorY;
        if (Chasm.TryDeck(tile, out float deckY)) return deckY;   // 다리 윗면 — 슬래브는 안 그린다
        return SampleGroundHeight(tile.x + 0.5f + _gridPhase.x, tile.y + 0.5f + _gridPhase.y);
    }

    // 타일 중심을 표면에 스냅한 월드 좌표 (배치/시각화용). 길찾기용 TileToWorld(Y=0)와 분리.
    public Vector3 TileToGround(Vector2Int tile)
    {
        return new Vector3(tile.x + 0.5f + _gridPhase.x, SurfaceHeight(tile), tile.y + 0.5f + _gridPhase.y);
    }

    // footprint 바운드의 4코너 지면 높이차가 tolerance 이내인지 (울퉁불퉁 지형 자동 반영).
    public bool IsFlatEnough(IEnumerable<Vector2Int> footprint)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in footprint)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }
        if (minX > maxX) return false; // 빈 footprint

        float lo, hi;

        // **바닥이 깔린 자리는 지면이 아니라 그 바닥을 잰다**(2026-08-28). 안 그러면
        // **평탄 기단이 아무 소용이 없다** — 비탈을 평평하게 덮으려고 깐 기단 위에서도
        // 검사가 밑의 지면을 재서 *"땅이 기울었다"*로 거절한다. 기단은 그 위에 짓기 위한
        // 물건이므로 그 판정이 기단을 못 보면 물건 자체가 성립하지 않는다.
        //
        // 재는 자리가 갈린다 — 지면은 **타일 경계 네 모서리**(연속이라 어디서든 물을 수
        // 있다), 바닥은 **칸마다 하나**(`_floored`가 칸 단위라 모서리에 값이 없다).
        if (AnyFloored(footprint))
        {
            lo = float.MaxValue; hi = float.MinValue;
            foreach (var p in footprint)
            {
                float y = SurfaceHeight(p);
                if (y < lo) lo = y;
                if (y > hi) hi = y;
            }
        }
        else
        {
            // 월드 코너 = 타일 경계 (minTile 좌하단 ~ maxTile+1 우상단)
            float px = _gridPhase.x, pz = _gridPhase.y;
            float h00 = SampleGroundHeight(minX     + px, minY     + pz);
            float h10 = SampleGroundHeight(maxX + 1 + px, minY     + pz);
            float h01 = SampleGroundHeight(minX     + px, maxY + 1 + pz);
            float h11 = SampleGroundHeight(maxX + 1 + px, maxY + 1 + pz);
            lo = Mathf.Min(Mathf.Min(h00, h10), Mathf.Min(h01, h11));
            hi = Mathf.Max(Mathf.Max(h00, h10), Mathf.Max(h01, h11));
        }

        // **재는 것은 높이차가 아니라 기울기다**(2026-08-27). 절대 높이차 하나로 자르면
        // **큰 건물일수록 불리해진다** — 같은 비탈이라도 넓을수록 모서리 사이가 벌어지기
        // 때문이다. 침식 지형으로 옮긴 뒤로는 그게 *"어디에도 못 짓는다"*로 나타났다
        // (5×5짜리가 0.5m 안에 들려면 기울기가 0.1 미만이어야 하는데, 그런 땅이 거의 없다).
        //
        // 기본 여유는 남긴다 — **잔기복은 크기와 무관**하고, 그것까지 기울기로 재면
        // 1×1이 통과 못 한다.
        _lastDrop    = hi - lo;
        _lastAllowed = Allowed(maxX - minX + 1, maxY - minY + 1);
        return _lastDrop <= _lastAllowed;
    }

    // 마지막 평탄 검사가 실제로 잰 값 — **화면에 띄우기 위한 것**이다. 판정에는 안 쓴다.
    float _lastDrop, _lastAllowed;

    // 평탄해서 되면 `null`, 안 되면 **숫자가 든 이유**. *"기울어서 안 된다"*만으로는
    // 얼마나 모자란지를 몰라 손잡이를 어디로 돌릴지 알 수 없다.
    public string WhyNotFlat(IEnumerable<Vector2Int> footprint)
    {
        if (IsFlatEnough(footprint)) return null;
        return $"땅이 기울었다 — 모서리 높이차 {_lastDrop:0.00}m (허용 {_lastAllowed:0.00}m)";
    }

    // 그 크기의 발자국이 얼마나 기울어도 되나(m). 한 변이 긴 쪽으로 잰다 —
    // 대각선으로 재면 정사각형이 아닌 건물에서 짧은 쪽이 덤으로 봐줘진다.
    public float Allowed(int tilesX, int tilesY) =>
        buildHeightTolerance + buildSlopeTolerance * Mathf.Max(tilesX, tilesY);

    // ── 건설 가능 판정 ─────────────────────────────────────────────────────────

    // 건설 판정(모델 A): 존 클램프 + 바닥 위 + 점유 안 됨 + (지형 있으면) 평탄.
    //  - 코어 union은 건설을 차단하지 않는다(union 밖 건물은 BuildingDormancy가 휴면 처리).
    //  - 존(buildZoneSize=200)은 최대 바깥 경계 클램프.
    //  - 바닥 시스템이 활성(HasFloorSystem)일 때만 "바닥(floored) 위에만" 강제 → 바닥 없는 필드/지형 맵은 무영향.
    //  - 바닥 자체를 까는 배치는 이 검사를 우회해야 함(공허에 바닥을 놓는 것) — 후속 floor placeable에서 처리.
    public bool IsBuildable(IEnumerable<Vector2Int> footprint) => WhyNotBuildable(footprint) == null;

    // **거절하는 이유를 말로 돌려준다**(2026-08-27). 참/거짓만 돌려주면 화면에서는
    // *"빨간데 왜 빨간지 모르겠다"*가 되고, 실제로 그것 때문에 **평지에서 건설이 안 되는
    // 것을 지형 탓인지 점유 탓인지 가릴 수가 없었다.**
    //
    // **판정과 설명이 같은 코드여야 한다** — 따로 두면 *"안 된다는데 이유는 없다"*가 생긴다.
    // 그래서 `IsBuildable`이 이걸 부른다(반대가 아니다).
    //
    // 지을 수 있으면 `null`.
    public string WhyNotBuildable(IEnumerable<Vector2Int> footprint)
    {
        foreach (var pos in footprint)
        {
            if (!IsInBuildZone(pos)) return "빌드존 밖 — 핵에서 너무 멀다";
            if (IsOccupied(pos)) return "무언가 자리를 막고 있다";

            // **파야 지을 수 있다.** *"못 짓는다"*가 아니라 **무엇을 하면 되는지**를 말한다 —
            // 지하에서는 자리가 없는 것이 아니라 아직 안 판 것이다.
            if (RockMap.IsRock(pos)) return "바위다 — 먼저 파야 한다 (K)";
            if (Chasm.IsChasm(pos))  return "절벽이다 — 다리만 놓을 수 있다";
        }
        return WhyNotFlat(footprint);
    }

    // 발자국에 바닥이 한 칸이라도 깔려 있나. `IsFlatEnough`가 무엇을 잴지 가른다.
    bool AnyFloored(IEnumerable<Vector2Int> footprint)
    {
        foreach (var p in footprint)
            if (_floored.ContainsKey(p)) return true;
        return false;
    }

    // 바닥 배치 판정: 존 클램프 + 아직 바닥 아님 + 점유 안 됨 + (지형시) 평탄.
    // 건물과 달리 "바닥 위에만" 요구를 받지 않음 — 바닥 자체를 공허에 까는 것이므로.
    //
    // **`level`은 평탄 기단이다** — 경사를 무시하고 한 높이로 깔리는 것이라 평탄 검사를
    // 안 받는다(그 검사를 통과할 수 있는 땅이면 애초에 기단이 필요 없다). 대신 얼마나
    // 뜨는지는 부르는 쪽이 잰다(`BuildingData.LevelMaxStep`) — 여기서는 **어떤 높이로
    // 깔릴지를 모르기 때문이다.**
    public bool CanPlaceFloor(IEnumerable<Vector2Int> footprint, bool level)
    {
        foreach (var pos in footprint)
        {
            if (!IsInBuildZone(pos)) return false;      // 최대 경계 클램프
            if (_floored.ContainsKey(pos)) return false;   // 이미 바닥
            if (IsOccupied(pos)) return false; // 점유
            if (Chasm.IsChasm(pos)) return false;   // 절벽 위에는 기단을 못 깐다(다리의 일이다)
        }
        return level || IsFlatEnough(footprint);
    }
}
