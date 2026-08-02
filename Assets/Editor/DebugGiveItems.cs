using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 테스트용 치트. **Play 중에만** 동작한다 — 런타임 상태(창고·폰·연구)를 건드리므로.
//
// 게임 안 단축키로 만들지 않은 이유: 치트를 조작 체계에 넣으면 언젠가 실수로 눌리고,
// 빌드에서 빼는 것도 잊는다. `Assets/Editor/`에 있는 것은 **빌드에 아예 안 들어간다.**
public static class DebugGiveItems
{
    const int DefaultAmount = 20;

    // 전부 담는 데 **190칸**이 필요하다(89종, 그중 33종이 2칸 이상 — 장비는 한 종에 2×4).
    // 큰 창고(192칸)는 숫자로는 딱 맞지만 조각이 남아 실제론 몇 종이 밀린다. 넉넉히 잡는다.
    const int ChestW = 20, ChestH = 14;   // 280칸
    const string ChestName = "[DebugChest]";

    [MenuItem("DungeonColony/Debug: Give All Items")]
    static void GiveAllToStorage()
    {
        if (!EnsurePlaying()) return;

        var chest = EnsureDebugChest();
        var items = CollectItems();
        int placed = 0;
        var failed = new List<string>();

        foreach (var item in items)
        {
            // 스택이 큰 것은 한 칸에 다 들어가지만, 장비(스택 1)는 하나가 한 자리를 먹는다.
            // 장비를 20개씩 주면 창고가 그것만으로 꽉 찬다 — **스택 없는 것은 하나만**.
            int want = item.MaxStack > 1 ? Mathf.Min(DefaultAmount, item.MaxStack) : 1;

            // **디버그 상자에 먼저** 넣는다 — 넉넉하고, 창고 필터에 걸리지 않는다.
            int got = chest != null ? chest.TryAdd(item, want) : 0;

            // 그래도 남으면(상자도 꽉 찼으면) 평소 창고로.
            if (got < want)
                foreach (var st in Storage.All)
                {
                    if (st == null || st == chest || !st.IsActive) continue;
                    got += st.TryAdd(item, want - got);
                    if (got >= want) break;
                }

            if (got > 0) placed++;
            else failed.Add(item.ItemName);
        }

        Debug.Log($"[Debug] {placed}/{items.Count}종 지급 — 디버그 상자를 클릭해 확인하세요.");
        if (failed.Count > 0)
            // 아이템이 89종인데 기본 창고는 80칸이고, **장비 한 종이 2×4(8칸)** 를 먹는다.
            // 다 받으려면 창고가 여러 개 필요하다 — 그걸 모르면 "치트가 안 먹는다"로 보인다.
            Debug.LogWarning($"[Debug] 못 넣은 것 {failed.Count}종(자리 없음 또는 창고 필터): " +
                             string.Join(", ", failed.GetRange(0, Mathf.Min(12, failed.Count))) +
                             (failed.Count > 12 ? " 외" : "") +
                             "\n장비는 한 종이 2×4칸을 먹습니다 — 큰 창고를 더 지으면 나머지가 들어갑니다.");
    }

    [MenuItem("DungeonColony/Debug: Complete All Research")]
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
    static Storage EnsureDebugChest()
    {
        foreach (var st in Storage.All)
            if (st != null && st.name == ChestName) return st;

        var go = new GameObject(ChestName);

        // 던전 핵 옆에 놓는다 — 폰이 운반해 갈 수 있어야 하므로 손 닿는 곳이어야 한다.
        var core = Object.FindFirstObjectByType<DungeonCore>();
        go.transform.position = core != null
            ? core.transform.position + new Vector3(3f, 0f, 3f)
            : Vector3.zero;

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

        Debug.Log($"[Debug] {ChestName} 생성 ({ChestW}×{ChestH} = {ChestW * ChestH}칸). 핵 옆에 있습니다.");
        return storage;
    }

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
