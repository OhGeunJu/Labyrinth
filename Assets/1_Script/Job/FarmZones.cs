using System.Collections.Generic;
using UnityEngine;

// **어느 밭들이 한 구역인가 — 답은 여기 하나다**(2026-09-02).
//
// **구역은 번호가 정한다. 맞닿았다고 합쳐지지 않는다.**
//
// 처음에는 *맞닿으면 한 구역*으로 뒀는데, 그러면 **나란히 붙은 두 밭에 다른 것을 심을 수가
// 없다** — 갈라 놓아도 다음 프레임에 다시 한 덩어리가 된다(사용자: *"림월드는 안 붙어
// 있으면 다른 구역이잖아"* — 뒤집으면 **붙어 있어도 다른 구역일 수 있어야 한다**).
//
// 그래서 **번호가 답한다.** 대신 *"밭 스물다섯 장을 한 번에 깔면 구역이 스물다섯 개"*가
// 되지 않게, **놓는 순간 이웃의 번호를 물려받는다**(`FarmPlot.OnEnable`) — 한 번의 드래그로
// 깐 밭은 저절로 한 구역이고, 도구로 가른 뒤에는 붙어 있어도 안 합쳐진다.
//
// **번호가 0이면 구역이 없다** — 그 밭은 저 혼자다(도구의 `삭제`가 그렇게 만든다).
//
// ── 다시 세는 때 ──────────────────────────────────────────────────────────
//
// **묻는 쪽이 매 프레임이다**(패널이 칸 수를 띄우고, 테두리를 그린다). 그때마다 전부 훑으면
// 밭 수의 제곱이 프레임에 붙으므로 **바뀐 것을 알릴 때만 다시 센다**(`MarkDirty`).
public static class FarmZones
{
    static bool _dirty = true;

    // **다시 센 횟수.** 밭이 *"내 색이 아직 맞나"*를 이 숫자로 판단한다 — 색을 매 프레임
    // 다시 칠하면 `MaterialPropertyBlock`을 밭 수만큼 매 프레임 새로 미는 셈이 된다.
    public static int Version { get; private set; }

    public static void MarkDirty() => _dirty = true;

    static readonly Dictionary<FarmPlot, int> _zoneOf = new();
    static readonly List<List<FarmPlot>>      _zones  = new();
    static readonly List<Color>               _colors = new();
    static readonly List<FarmPlot>            _empty  = new();

    // 이 밭이 속한 구역 전부(자기 포함). **들고 있지 말 것** — 다시 세면 목록이 갈린다.
    public static IReadOnlyList<FarmPlot> Of(FarmPlot plot)
    {
        Rebuild();
        return plot != null && _zoneOf.TryGetValue(plot, out int i) ? _zones[i] : _empty;
    }

    // 구역을 서로 가르는 색. **밭의 본래 색에 곱한다** — 덮어쓰면 흙이 무슨 색이었는지가
    // 사라져 밭이 아니라 색종이로 보인다.
    public static Color ColorOf(FarmPlot plot)
    {
        Rebuild();
        return plot != null && _zoneOf.TryGetValue(plot, out int i) ? _colors[i] : Color.white;
    }

    public static int CountOf(FarmPlot plot) => Of(plot).Count;

    // ── 다시 세기 ───────────────────────────────────────────────────────────

    static void Rebuild()
    {
        if (!_dirty) return;
        _dirty = false;
        Version++;

        _zoneOf.Clear();
        _zones.Clear();
        _colors.Clear();

        var byGroup = new Dictionary<int, int>();   // GroupId → 구역 색인

        foreach (var p in FarmPlot.All)
        {
            if (p == null) continue;

            // **번호가 0이면 저 혼자다** — 묶인 적이 없거나 도구가 떼어낸 밭이다.
            if (p.GroupId == 0)
            {
                var t = p.TileCoord;
                Put(p, NewZone(Hue(unchecked((uint)(t.x * 73856093) ^ (uint)(t.y * 19349663)))));
                continue;
            }

            if (!byGroup.TryGetValue(p.GroupId, out int zi))
            {
                zi = NewZone(Hue(unchecked((uint)p.GroupId * 2654435761u)));
                byGroup[p.GroupId] = zi;
            }
            Put(p, zi);
        }
    }

    static int NewZone(Color c)
    {
        _zones.Add(new List<FarmPlot>());
        _colors.Add(c);
        return _zones.Count - 1;
    }

    static void Put(FarmPlot p, int zone)
    {
        _zoneOf[p] = zone;
        _zones[zone].Add(p);
    }

    // 해시 하나에서 **서로 잘 갈리는 색**을 낸다.
    //
    // **채도와 명도는 안 흔든다** — 밝기까지 흔들면 어떤 구역은 어두운 굴에서 아예 안 보인다.
    // 색상만 돌리고 **곱하는 값이라 1에 가깝게 눌러 둔다**(0.45) — 진하게 칠하면 밭이 흙이
    // 아니라 표식으로 보이고, 자란 작물 색도 죽는다.
    static Color Hue(uint h)
    {
        float t = (h % 997u) / 997f;
        var c = Color.HSVToRGB(t, 0.65f, 1f);
        return Color.Lerp(Color.white, c, 0.45f);
    }
}
