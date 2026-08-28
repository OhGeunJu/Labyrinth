using System.Collections.Generic;
using UnityEngine;

// 월드맵 마을의 시장 — **칸 좌표가 곧 상점의 정체다.**
//
// 마을엔 씬 오브젝트가 없다(지도 위의 마을은 "칸"이다). 그래서 상인 NPC를 세우는 대신
// 칸마다 `Shop`을 하나 들고 있다가 원정대가 도착하면 그걸 연다.
//
// **누가 장사하는가는 저작하지 않는다** — 바이옴의 상인 풀(`BiomeContentData.TownMerchants`)에서
// 칸 좌표 + 시드 해시로 고른다. `WorldDeposits`가 매장 자원을 정하는 방식과 같다:
// 배치(어디가 마을인가)는 저작물이고, 그 칸의 세부(누가 서는가)만 결정적으로 굴린다.
// 덕분에 마을 200개를 손으로 채우지 않아도 지역마다 다른 상인이 서고, 같은 마을에 다시 가면
// 같은 상인이 있다.
public static class TownMarket
{
    // 칸 → 그 마을에 선 상인들. 한 번 연 마을은 계속 같은 사람들을 본다(그날 안에는 재고까지).
    static readonly Dictionary<string, List<Shop>> _shops = new();

    // 칸 → 재고를 마지막으로 갱신한 날. 하루가 지나야 진열이 바뀐다 —
    // 메뉴를 닫았다 열 때마다 새로 뽑으면 원하는 물건이 나올 때까지 여닫게 된다.
    static readonly Dictionary<string, int> _stockedDay = new();

    static readonly List<Shop> None = new();

    // 그 마을에 선 상인들 — **종류당 하나씩**(무기·방어구·재료…).
    //
    // 종류는 **바이옴 풀에 있는 것이 전부 뜬다**. 좌표로 고르는 건 "같은 종류에 후보가 여럿일 때
    // 어느 쪽인가"뿐이다 — 종류까지 추첨하면 무기 상인을 만나려고 마을을 순회하게 되고,
    // 그건 고르는 일이 아니라 찾아다니는 일이 된다.
    public static IReadOnlyList<Shop> ShopsAt(string siteId)
    {
        var site = WorldSites.ById(siteId);
        if (!site.IsValid || site.Kind != WorldNodeContent.Town) return None;

        if (!_shops.TryGetValue(siteId, out var shops))
        {
            shops = Build(site, WorldMap.Instance != null ? WorldMap.Instance.DepositSeed : 0);
            if (shops.Count == 0) return None;

            _shops[siteId]      = shops;
            _stockedDay[siteId] = CurrentDay;
            return shops;
        }

        // 하루가 지났으면 진열을 갈아 끼운다. 상인(가격표)은 그대로다 — 마을에 누가 사는지가
        // 매일 바뀌면 지역별 시세라는 개념이 성립하지 않는다.
        int today = CurrentDay;
        if (_stockedDay.TryGetValue(siteId, out int day) && day == today) return shops;

        foreach (var sh in shops) sh.Refresh();
        _stockedDay[siteId] = today;
        return shops;
    }

    // **옛 화면용 다리** — `WorldMapView`가 아직 칸으로 묻는다.
    public static IReadOnlyList<Shop> ShopsAt(Vector2Int node) =>
        ShopsAt(WorldSites.InRegion(WorldNodeContent.Town, node).Id);

    // 씬을 다시 시작해도 static은 살아남는다(도메인 리로드를 끈 경우). 남은 마을이
    // 옛 월드맵 좌표를 물고 있으면 엉뚱한 상인이 서므로, 월드맵을 새로 지을 때 비운다.
    public static void Clear()
    {
        _shops.Clear();
        _stockedDay.Clear();
    }

    static int CurrentDay => GameTimeManager.CurrentDay;

    // 바이옴 풀을 **종류별로 묶어** 종류마다 하나씩 세운다.
    // 줄 순서는 `MerchantType` 선언 순서를 따른다 — 마을마다 줄이 뒤바뀌면 눈이 매번 새로 읽는다.
    static List<Shop> Build(WorldSites.Site site, int seed)
    {
        var result = new List<Shop>();

        var data = BiomeContent.Get(WorldMap.BiomeAtWorld(site.Position));
        if (data == null || data.TownMerchants == null || data.TownMerchants.Count == 0) return result;

        // 마을의 세력은 **저작한다**(`BiomeContentData.TownFaction`). 비어 있으면 정가다.
        var faction = data.TownFaction;

        var byType = new Dictionary<MerchantType, List<MerchantData>>();
        foreach (var m in data.TownMerchants)
        {
            // null은 먼저 걸러낸다 — 빈 칸이 뽑히면 "상인이 없는 종류"가 무작위로 생겨
            // 원인을 짚기 어렵다.
            if (m == null) continue;
            if (!byType.TryGetValue(m.MerchantType, out var list))
                byType[m.MerchantType] = list = new List<MerchantData>();
            list.Add(m);
        }

        foreach (MerchantType type in System.Enum.GetValues(typeof(MerchantType)))
        {
            if (!byType.TryGetValue(type, out var candidates) || candidates.Count == 0) continue;

            // 같은 종류에 후보가 여럿일 때만 좌표가 고른다. 종류별로 다른 난수열을 써야
            // 한 마을의 상인들이 같은 방향으로 몰려 뽑히지 않는다.
            var rng = new System.Random(Hash(site.Id, seed, (int)type));
            result.Add(new Shop(candidates[rng.Next(candidates.Count)], faction));
        }

        return result;
    }


    // 좌표·시드·종류를 섞어 안정적인 정수 하나로. unchecked — 넘침은 해시에서 정상이다.
    // `WorldDeposits.Hash`와 다른 상수를 쓴다: 같은 열을 쓰면 매장이 많은 칸에 늘 같은
    // 상인이 서는 식으로 두 값이 붙어 다닌다.
    // **신원을 섬는다 — 좌표가 아니라**(2026-08-27). 좌표를 쓰면 저작 씬에서 마을을
    // 200m 옮기는 것만으로 **상점 구성과 재고가 통째로 달라진다** — 자리를 다듬는 일이
    // 저작이 아니라 뽑기가 된다.
    static int Hash(string id, int seed, int salt)
    {
        unchecked
        {
            int h = 23;
            if (id != null) foreach (char c in id) h = h * 31 + c * 40503;
            h = h * 31 + seed  * 92837111;
            h = h * 31 + salt  * 6700417;
            return h;
        }
    }
}
