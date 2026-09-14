using System.Collections.Generic;
using UnityEngine;

// 월드맵 한 칸에 묻혀 있는 자원 하나.
public struct WorldDeposit
{
    public ItemData Item;
    public int      Amount;

    public string DisplayName =>
        Item == null ? "?" : (string.IsNullOrEmpty(Item.ItemName) ? Item.name : Item.ItemName);
}

// **칸마다 무엇이 얼마나 묻혀 있는가** — 저작하지 않고 칸 좌표에서 결정적으로 뽑는다.
//
// 종류는 바이옴이 정하고(`BiomeContentData.Deposits`), 수량은 **칸 좌표 + 시드 해시**로 굴린다.
// 그래서 ① 256칸을 손으로 채우지 않아도 칸마다 다른 매장이 나오고
//      ② 같은 시드면 몇 번을 다시 실행해도 같은 값이 나온다(세이브 없이도 일관).
// 이건 "지도를 절차 생성한다"와 다르다 — **배치(바이옴·내용물)는 여전히 저작물**이고,
// 여기서 뽑는 건 그 저작된 칸의 세부 수치뿐이다.
//
// 특별한 칸을 손으로 지정하고 싶어지면 `WorldMapData.Node`에 오버라이드를 얹으면 된다(아직 없음).
public static class WorldDeposits
{
    public static List<WorldDeposit> At(Vector2Int node, WorldTile tile, int seed)
    {
        var result = new List<WorldDeposit>();

        var data = BiomeContent.Get(tile.Biome);
        if (data == null || data.Deposits == null) return result;

        for (int i = 0; i < data.Deposits.Count; i++)
        {
            var e = data.Deposits[i];
            if (e == null || e.Item == null) continue;

            // 항목마다 다른 난수열을 써야 한다 — 같은 열을 쓰면 모든 자원이 함께 나오거나 함께 빠진다.
            var rng = new System.Random(Hash(node, seed, i));

            if (rng.NextDouble() > e.Chance) continue;

            int min = Mathf.Min(e.MinAmount, e.MaxAmount);
            int max = Mathf.Max(e.MinAmount, e.MaxAmount);
            int amount = rng.Next(min, max + 1);
            if (amount <= 0) continue;

            result.Add(new WorldDeposit { Item = e.Item, Amount = amount });
        }

        return result;
    }

    // 좌표·시드·항목을 섞어 안정적인 정수 하나로. unchecked — 넘침은 해시에서 정상이다.
    static int Hash(Vector2Int node, int seed, int salt)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + node.x * 73856093;
            h = h * 31 + node.y * 19349663;
            h = h * 31 + seed  * 83492791;
            h = h * 31 + salt  * 2654435761u.GetHashCode();
            return h;
        }
    }
}
