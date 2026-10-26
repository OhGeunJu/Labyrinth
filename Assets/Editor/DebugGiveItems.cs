using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 테스트용 치트. **Play 중에만** 동작한다 — 런타임 상태(창고·폰·연구)를 건드리므로.
//
// 게임 안 단축키로 만들지 않은 이유: 치트를 조작 체계에 넣으면 언젠가 실수로 눌리고,
// 빌드에서 빼는 것도 잊는다. `Assets/Editor/`에 있는 것은 **빌드에 아예 안 들어간다.**
public static class DebugGiveItems
{
    const int ChestW = 20, ChestH = 14;   // 280칸
    const int MaxChests = 12;             // 안전장치 — 무한히 만들지 않는다
    const string ChestName = "[DebugChest]";

    // **장비는 한 종에 등급 여섯 벌을 다 준다 — 일반에서 전설까지 차례로 나란히**(2026-10-23, 사용자: *"방어구마다 등급이
    // 순서대로 있는건데 하나의 방어구가 하나의 등급이면 어떡해"*).
    //
    // 10-22에 상자 수를 줄이려고 한 종에 하나씩 등급을 돌려 가며 줬다 — 그러면 강철 투구는 희귀 하나뿐이라 같은 물건의
    // 등급끼리 견줄 수가 없었다. 이제 한 종의 여섯 벌을 **이어서** 넣어 상자 안에서 한 줄로 읽힌다. 상자는 다시 여럿이
    // 된다(장비 54종 × 6 — 여덟 개 남짓, 한 줄로 늘어선다).
    //
    // 등급이 있는가는 물건이 답한다(`ItemData.HasQuality`) — 없는 것은 `Common`으로 눌린다.
    static readonly ItemQuality[] GearQualities =
    {
        ItemQuality.Common, ItemQuality.Uncommon, ItemQuality.Rare,
        ItemQuality.Unique, ItemQuality.Epic,     ItemQuality.Legendary,
    };

    static readonly List<Storage> _chests = new();

    [MenuItem("DungeonColony/Debug/Give All Items")]
    static void GiveAllToStorage()
    {
        if (!EnsurePlaying()) return;

        _chests.Clear();
        var items = CollectItems();

        // **넣는 순서가 곧 상자 안의 모양이다** — 무기 → 방어구 → 나머지, 그 안에서 큰 것부터.
        // 큰 것을 먼저 넣어야 빈칸이 덜 남고, 갈래끼리 모여 있어야 열었을 때 읽힌다.
        items.Sort((a, b) =>
        {
            int c = Group(a).CompareTo(Group(b));
            if (c != 0) return c;
            c = (b.Width * b.Height).CompareTo(a.Width * a.Height);
            return c != 0 ? c : string.CompareOrdinal(a.ItemName, b.ItemName);
        });

        int placed = 0;
        var failed = new List<string>();

        foreach (var item in items)
        {
            // 장비는 등급마다 하나씩, 그 밖은 **최대치까지 꽉** 채운다 — 재료가 모자라 제작을 끝까지
            // 못 돌려 보는 일이 없게.
            int got = 0;
            if (item.HasQuality)
                foreach (var q in GearQualities) got += Give(item, 1, q);
            else
                got = Give(item, Mathf.Max(1, item.MaxStack), ItemQuality.Common);

            if (got > 0) placed++;
            else failed.Add(item.ItemName);
        }

        Debug.Log($"[Debug] {placed}/{items.Count}종 지급 (장비는 한 종에 등급 여섯 벌, 그 밖은 최대 스택). " +
                  $"디버그 상자 {_chests.Count}개 — 클릭해 확인하세요.");

        if (failed.Count > 0)
            Debug.LogWarning($"[Debug] 못 넣은 것 {failed.Count}종: " +
                             string.Join(", ", failed.GetRange(0, Mathf.Min(12, failed.Count))) +
                             (failed.Count > 12 ? " 외" : "") +
                             $"\n상자를 {MaxChests}개까지만 만듭니다(무한 생성 방지).");
    }

    // **자리가 모자라면 상자를 하나 더 만든다.**
    //
    // 등급을 곱하면서 한 상자로는 어림도 없어졌다(장비 한 종이 6개가 되고, 그 하나가 2×4칸을
    // 먹는다). 크기를 키우는 대신 **개수를 늘린다** — 격자가 화면보다 커지면 열어 봐도 못 본다.
    //
    // "치트를 쓰려면 먼저 큰 창고를 두 개 지으세요"는 테스트 도구가 할 말이 아니다.
    static int Give(ItemData item, int amount, ItemQuality quality)
    {
        int got = 0;

        for (int i = 0; i < MaxChests && got < amount; i++)
        {
            if (i >= _chests.Count)
            {
                var made = EnsureDebugChest(i);
                if (made == null) break;
                _chests.Add(made);
            }

            var chest = _chests[i];
            if (chest != null) got += chest.TryAdd(item, amount - got, quality);
        }
        return got;
    }

    // **고른 폰에게 장비를 채워 준다** — 하이어라키에서 폰을 고르고 실행.
    //
    // 왜 필요했나: 적은 맨몸으로 태어나서 **죽여도 시체에서 아무것도 안 나온다.** 그러면
    // 시체 창·약탈·되팔기·`ResaleFactor`를 확인할 길이 없다(창을 열어도 늘 비어 있으니
    // "창이 고장 났나 물건이 없나"조차 못 가른다).
    //
    // `Debug: Give All Items`와 다른 축이다 — 그건 **내 창고**를 채우고, 이건 **저 폰**을 채운다.
    // 상대에게 물건을 쥐여 주는 길은 그것 말고는 없다.
    [MenuItem("DungeonColony/Debug/Arm Selected Pawn")]
    static void ArmSelectedPawn()
    {
        if (!EnsurePlaying()) return;

        var pawns = new List<UnitBrain>();
        foreach (var go in Selection.gameObjects)
        {
            var brain = go != null ? go.GetComponentInParent<UnitBrain>() : null;
            if (brain != null && !pawns.Contains(brain)) pawns.Add(brain);
        }

        if (pawns.Count == 0)
        {
            Debug.LogWarning("[Debug] 하이어라키에서 폰을 고르고 실행하세요 " +
                             "(Play 중 씬 뷰에서 클릭하거나 하이어라키에서 선택).");
            return;
        }

        // **이름순으로 고른다.** 레지스트리는 Dictionary라 순서가 실행마다 흔들리는데,
        // 그러면 같은 폰을 두 번 무장시켰을 때 다른 장비가 나와 무엇을 보고 있는지 헷갈린다.
        var items = CollectItems();
        items.Sort((a, b) => string.CompareOrdinal(a.ItemName, b.ItemName));

        foreach (var brain in pawns) Arm(brain, items);
    }

    static void Arm(UnitBrain brain, List<ItemData> items)
    {
        var inv = brain.Inventory;
        var eq  = brain.Equipment;
        if (inv == null) { Debug.LogWarning($"[Debug] {brain.name}: Inventory가 없습니다."); return; }

        int worn = 0, carried = 0;

        // ── 장비 — **인벤토리에 넣은 뒤 장착한다.**
        // `Equipment.Equip*`은 `FindInInventory` + `RemoveOne`으로 동작한다(배낭에서 꺼내 차는 것).
        // 바로 장착시키는 길은 없고, 있으면 안 된다 — 손으로 차는 것과 다른 경로가 생긴다.
        if (eq != null)
        {
            var weapon = items.Find(i => i is WeaponData w && w.Type != WeaponType.Shield) as WeaponData;
            if (weapon != null && inv.TryAdd(weapon, 1) > 0 && eq.Equip(weapon)) worn++;

            // 방패는 **양손 무기를 들면 못 찬다**(`OffHandLocked`) — 실패해도 정상이다.
            var shield = items.Find(i => i is WeaponData w && w.Type == WeaponType.Shield) as WeaponData;
            if (shield != null && inv.TryAdd(shield, 1) > 0 && eq.EquipOffHand(shield)) worn++;

            foreach (ArmorSlot slot in System.Enum.GetValues(typeof(ArmorSlot)))
            {
                var s = slot;
                var armor = items.Find(i => i is ArmorData a && a.Slot == s) as ArmorData;
                if (armor != null && inv.TryAdd(armor, 1) > 0 && eq.EquipArmor(armor)) worn++;
            }
        }

        // ── 소지품 — 약탈해서 되팔 것이 있어야 `ResaleFactor`가 눈에 보인다.
        // 장비만 주면 등급 배율만 확인되고, 자원만 주면 감가가 안 보인다.
        int want = 6;
        foreach (var item in items)
        {
            if (want <= 0) break;
            if (item is WeaponData || item is ArmorData) continue;   // 위에서 이미 다뤘다
            if (Wallet.IsCurrency(item)) continue;                   // 동전은 아래에서 따로
            if (inv.TryAdd(item, Mathf.Min(5, item.MaxStack)) > 0) { carried++; want--; }
        }

        // 동전 — 시체에서 주워 **더블클릭으로 잔고가 되는지**까지 확인하려면 필요하다.
        var coin = Wallet.Coin;
        if (coin != null && inv.TryAdd(coin, 25) > 0) carried++;

        Debug.Log($"[Debug] {brain.UnitName ?? brain.name}: 착용 {worn}점 · 소지품 {carried}종. " +
                  "쓰러뜨린 뒤 시체를 우클릭 → [뒤진다].");
    }

    // **지갑에 바로 넣는다.** `Give All Items`는 상자에 **동전을** 넣어 주는데, 그건 더블클릭해야
    // 잔고가 된다(`InventoryUI.TryClaimCoins`) — 그리고 몸값·시장은 상자가 아니라
    // `Wallet.Balance`를 본다. 월드맵에서 흥정을 시험하려는데 배낭 창을 열어 동전을 눌러야
    // 하는 건 시험이 아니라 준비 작업이다.
    const int DebugGold = 5000;

    [MenuItem("DungeonColony/Debug/Add Gold")]
    static void AddGold()
    {
        if (!EnsurePlaying()) return;

        Wallet.Add(DebugGold);
        Debug.Log($"[Debug] 골드 +{DebugGold} → 잔고 {Wallet.Balance} G");
    }

    [MenuItem("DungeonColony/Debug/Complete All Research")]
    static void CompleteAllResearch()
    {
        if (!EnsurePlaying()) return;

        var mgr = ResearchManager.Instance;
        if (mgr == null) { Debug.LogWarning("[Debug] 씬에 ResearchManager가 없습니다."); return; }

        int n = 0;
        // 선행 연구가 있으므로 **여러 번 돈다** — 한 번만 훑으면 선행이 아직 안 끝난 것이 남는다.
        for (int pass = 0; pass < 8; pass++)
        {
            bool grew = false;
            foreach (var r in DataManager.AllResearch.Values)
            {
                if (r == null || mgr.IsCompleted(r)) continue;
                mgr.Complete(r);
                n++; grew = true;
            }
            if (!grew) break;
        }
        Debug.Log($"[Debug] 연구 {n}개 완료. 건설 목록과 레시피 잠금이 풀립니다.");
    }

    // 테스트용 상자. **창고를 안 지어도 되게** 여기서 직접 만든다 —
    // "치트를 쓰려면 먼저 큰 창고를 두 개 지으세요"는 테스트 도구가 할 말이 아니다.
    //
    // 이미 있으면 그걸 쓴다(다시 눌러도 상자가 늘지 않는다).
    // `index`는 몇 번째 상자인가 — 등급까지 주면서 한 상자로는 모자라졌다.
    static Storage EnsureDebugChest(int index)
    {
        string name = index == 0 ? ChestName : $"{ChestName} {index + 1}";

        foreach (var st in Storage.All)
            if (st != null && st.name == name) return Repair(st);

        var go = new GameObject(name);

        // 던전 핵 옆에 **한 줄로** 늘어놓는다 — 폰이 운반해 갈 수 있어야 하므로 손 닿는 곳이어야
        // 하고, 겹쳐 놓으면 어느 것을 클릭했는지 알 수 없다.
        go.transform.position = SpotNearBase(index);

        // 눈에 보이고 클릭도 되게 — 안 그러면 어디 생겼는지 알 수 없다.
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        body.transform.localScale    = new Vector3(1.6f, 0.8f, 1.2f);
        Object.DestroyImmediate(body.GetComponent<Collider>());   // 콜라이더는 루트에 하나

        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.4f, 0f);
        col.size   = new Vector3(1.7f, 0.9f, 1.3f);

        // **`Inventory`를 먼저 붙이고 크기를 잡는다.** `Storage.Awake`가 스스로 붙이면
        // 기본 8×10이 되어 상자를 만든 의미가 없다. `Resize`는 비어 있을 때만 통한다.
        var inv = go.AddComponent<Inventory>();
        inv.Resize(ChestW, ChestH);

        var storage = go.AddComponent<Storage>();
        storage.BuildingName = "디버그 상자";

        // **진짜 건물로 세운다**(2026-09-05, 사용자: *"내가 옮길 수가 없으니깐"*).
        //
        // 그전에는 `Storage`만 붙인 맨 오브젝트라 **`PlacedBuilding`이 없었다.** 그러면
        // `BuildingRelocator.Begin(pb)`에 넘길 것이 없어 살펴보기 창에 **[이동한다]가 아예
        // 안 뜨고**, 발자국도 등록이 안 돼 길찾기는 상자를 **없는 것으로 본다**(몸은 부딪히는데
        // 지도는 빈 칸 — 폰이 처박히는 그 자리다).
        //
        // 창고 데이터를 그대로 물린다 — 테스트 상자라고 **다른 규칙으로 살 이유가 없다**.
        // 고르기·살펴보기·옮기기·철거가 전부 평소 길로 돌아온다.
        var data = Resources.Load<BuildingData>("Data/Buildings/Storage");
        if (data != null) PlacedBuilding.Attach(go, data);
        else Debug.LogWarning("[Debug] `Data/Buildings/Storage`를 못 찾았습니다 — 상자를 옮길 수 없습니다.");

        Debug.Log($"[Debug] {name} 생성 ({ChestW}×{ChestH} = {ChestW * ChestH}칸) — {go.transform.position}");
        return storage;
    }

    // **이미 있는 상자도 고친다**(2026-09-05). 한 번 파묻힌 상자는 다시 눌러도 `EnsureDebugChest`가
    // *"있으면 그걸 쓴다"*로 그대로 돌려줘서 **영원히 파묻힌 채**였다 — 사람이 못 고르니 지울
    // 수도 없다(하이어라키에서 손으로 지우는 수밖에).
    //
    // **손으로 옮겨 둔 상자는 안 건드린다** — 파묻혔을 때만 끌어올린다. 다시 돌릴 때마다
    // 제자리로 되돌리면 *"옮겨 놨는데 왜 돌아오지"*가 된다.
    static Storage Repair(Storage st)
    {
        var map = MapManager.Instance;
        if (map != null)
        {
            var pos  = st.transform.position;
            float top = map.TileToGround(map.WorldToTile(pos)).y;

            if (pos.y < top - 0.1f)
            {
                st.transform.position = new Vector3(pos.x, top, pos.z);
                Debug.Log($"[Debug] {st.name}이(가) 파묻혀 있어 지면 위로 올렸습니다 — y {pos.y:F2} → {top:F2}");
            }
        }

        // 옛 상자에는 `PlacedBuilding`이 없다 — 그래서 [이동한다]가 안 떴다.
        if (st.GetComponent<PlacedBuilding>() == null)
        {
            var data = Resources.Load<BuildingData>("Data/Buildings/Storage");
            if (data != null)
            {
                PlacedBuilding.Attach(st.gameObject, data);
                Debug.Log($"[Debug] {st.name}에 건물 정보를 물렸습니다 — 이제 고르고 옮길 수 있습니다.");
            }
        }

        return st;
    }

    // **기준은 내 폰이다 — 핵이 아니다**(2026-09-12).
    //
    // 전에는 `DungeonCore`를 찾아 그 자리에 놓았다. 그런데 **판이 열릴 때 핵을 안 준다**
    // (2026-08-27 결정 — 어디에 거점을 세울까가 첫 결정이어야 하므로). 그래서 아직 안 꽂았으면
    // `FindFirstObjectByType`이 **null**이고 기준이 조용히 `Vector3.zero`로 떨어져,
    // 상자가 **세계 원점 옆**에 선다. `Scene1`의 방랑자들이 (21.5, 25.6)에서 시작하므로
    // **30m쯤 떨어진 곳**이다 — 화면에는 *"상자가 폰들이 있는 데랑 좀 다른 데 뜬다"*로만 보이고,
    // 원인이 *"핵이 없다"*라는 표시가 어디에도 없다(폰은 좌표만 보므로 운반은 멀쩡히 된다).
    //
    // **`RockRenderer.Start`가 이미 같은 물음에 답해 두었다** — 판이 열릴 때 굴을 어디에 뚫을지를
    // 정하면서 **내 폰 둘레**를 열고, 아무도 없으면 카메라 앞을 연다. 기준을 거기에 맞춘다:
    // **(10-22부터 시작 자리가 맨 앞) → 내 폰 → (없으면) 핵 → (그것도 없으면) 원점.**
    //
    // **무게중심이 아니라 거기서 가장 가까운 폰의 자리를 쓴다** — 무리가 흩어져 있으면
    // 무게중심이 바위 한복판에 떨어질 수 있고, 그러면 `FindWorkableNear`가 8칸 안에서
    // 아무것도 못 찾아 **center를 그대로 돌려줘 상자가 바위에 박힌다.** 폰이 선 자리는
    // 설 수 있는 자리임이 이미 증명돼 있다.
    //
    // **시작 자리가 있으면 그것이 먼저다**(2026-10-22, 사용자: *"지금 스폰 되는쪽에 생성되게"*).
    // 폰은 흩어져 돌아다니지만 `StartSpot`은 판이 열릴 때 굴이 뚫린 자리라 늘 서 있을 수 있고,
    // 만든 폰이 처음 서는 곳이기도 하다 — 상자가 어디 생겼는지 찾으러 다닐 일이 없다.
    static Vector3 Anchor()
    {
        if (StartSpot.Current != null) return StartSpot.Current.transform.position;

        var all = UnitBrain.All;

        var  sum = Vector3.zero;
        int  n   = 0;
        for (int i = 0; i < all.Count; i++)
        {
            var brain = all[i];
            if (brain == null) continue;
            var f = brain.GetComponent<Faction>();
            if (f == null || !f.IsPlayer) continue;

            sum += brain.transform.position;
            n++;
        }

        if (n > 0)
        {
            var center = sum / n;
            var best   = center;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                var brain = all[i];
                if (brain == null) continue;
                var f = brain.GetComponent<Faction>();
                if (f == null || !f.IsPlayer) continue;

                float d = (brain.transform.position - center).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = brain.transform.position; }
            }
            return best;
        }

        // 폰이 하나도 없는 판(전멸했거나 아직 합류 전) — 그때는 핵이라도 있으면 그것.
        var core = Object.FindFirstObjectByType<DungeonCore>();
        if (core != null) return core.transform.position;

        Debug.LogWarning("[Debug] 내 폰도 핵도 없어 상자를 세계 원점 옆에 놓습니다.");
        return Vector3.zero;
    }

    // **바닥 위에 놓는다 — 핵의 Y를 베끼지 않는다**(2026-09-05).
    //
    // 전에는 핵의 자리에 `(3 + index*2, 0, 3)`을 더했다. 그러면 **높이가 핵의 것**이라, 세 칸
    // 옆 지면이 조금만 낮아도 상자가 **땅에 파묻힌다** — 폰은 좌표만 보므로 운반은 멀쩡히
    // 되는데(사용자: *"그래도 아이템을 갖고오긴 하던데"*) **커서 레이가 지면에 먼저 맞아
    // 사람이 못 고른다.** 보이지도 잡히지도 않는 상자가 되는 것이다.
    //
    // 그래서 **설 수 있는 칸을 찾아 그 윗면에 앉힌다**(`TileToGround` — 기단을 깔았으면
    // 슬래브 윗면이다). 지하에서는 세 칸 옆이 바위일 수도 있으므로 `FindWorkableNear`가
    // 답하게 두고, **이미 놓인 상자 칸은 뺀다**(겹쳐 놓으면 어느 것을 눌렀는지 모른다).
    static Vector3 SpotNearBase(int index)
    {
        var want = Anchor() + new Vector3(3f + index * 2f, 0f, 3f);

        var map = MapManager.Instance;
        if (map == null) return want;

        var taken = new List<Vector2Int>();
        foreach (var st in Storage.All)
            if (st != null && st.name.StartsWith(ChestName))
                taken.Add(map.WorldToTile(st.transform.position));

        var tile = map.FindWorkableNear(map.WorldToTile(want), maxRadius: 8, exclude: taken);
        return map.TileToGround(tile);
    }

    static int Group(ItemData item) => item is WeaponData ? 0 : item is ArmorData ? 1 : item.HasQuality ? 2 : 3;

    // 자원 아이템 + 장비 + 소비품 전부. 골드도 포함(살 것을 사 보려면 필요하다).
    static List<ItemData> CollectItems()
    {
        var list = new List<ItemData>();
        foreach (var kv in DataManager.AllItems)
            if (kv.Value != null) list.Add(kv.Value);
        return list;
    }

    static bool EnsurePlaying()
    {
        if (Application.isPlaying) return true;
        Debug.LogWarning("[Debug] Play 중에만 됩니다 — 런타임 창고·폰을 건드리는 기능입니다.");
        return false;
    }
}
