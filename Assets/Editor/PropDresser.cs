using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **건물에 얼굴을 입힌다**(2026-08-28).
//
// 이 저장소의 건물은 대부분 **기본 도형**이다 — 상자에 뚜껑을 얹으면 창고, 납작한 판이면
// 침대. 배선을 막지 않으려고 그렇게 시작한 것이고, 그 판단은 옳았다(모양을 다듬는 것은
// 나중에 모델을 갈아 끼우는 일이다). **지금이 그 나중이다.**
//
// ── 왜 모듈 벽이 아니라 소품인가 ───────────────────────────────────────────
//
// `Fantastic Dungeon Pack`의 **모듈 벽·바닥은 2m 격자에 벽 높이 4m**다(데모 씬의 좌표가
// 전부 2·4의 배수다). 이 게임의 칸은 1m라 **모듈 하나가 두 칸**이고, 그대로 쓰려면 껍데기의
// 벽 길이·문 폭·마스크를 전부 짝수로 다시 저작해야 한다 — 그건 이 팩을 쓰는 일이 아니라
// **격자를 팩에 맞추는 일**이다.
//
// **소품은 그 문제가 없다.** 궤짝도 광맥도 격자를 모르고, 제 발자국 안에 들어가기만 하면
// 된다. 그래서 값이 가장 싸고 화면은 가장 크게 바뀐다.
//
// ── 무엇을 안 건드리나 ─────────────────────────────────────────────────────
//
// **루트를 다시 짓지 않는다** — 프리팹을 새로 만들면 루트 fileID가 바뀌어 씬 인스턴스가
// 오버라이드를 잃는다. **컴포넌트·콜라이더·저작된 자식도 안 건드린다**(`WorkSpot`·눕는
// 자리·트리거 콜라이더가 거기 저작돼 있고, 하나만 빠뜨려도 폰이 비스듬히 눕거나 밭을
// 눌러도 안 열린다).
//
// 하는 일은 둘뿐이다: **옛 메시를 끄고**(지우지 않는다 — 콜라이더가 같은 오브젝트에 붙어
// 있다) **`[Art]` 자식을 하나 붙인다.** 그래서 되돌리기가 `[Art]`를 지우고 렌더러를 켜는
// 것으로 끝나고, 다시 돌려도 같은 결과가 나온다.
public static class PropDresser
{
    const string Pack = "Assets/assetsss/Fantastic Dungeon Pack/prefabs/PROPS";
    const string ArtName = "[Art]";

    // 발자국을 꽉 채우지 않는다 — 옆 칸 건물과 모서리가 닿으면 서로 파고든 것처럼 보인다.
    const float FitMargin = 0.85f;

    // 건물 프리팹 → 입힐 소품. **여러 개를 적으면 그중 첫째를 쓴다**(팩에서 이름이
    // 바뀌었을 때 조용히 아무것도 안 하는 대신 다음 것을 잡게).
    struct Dress
    {
        public string Building;    // `3_Prefab/Building/` 아래 이름
        public string[] Props;     // `PROPS/` 아래 상대 경로(확장자 없이)
        public bool    KeepHeight; // 세로는 안 늘린다 — 광맥·화톳불처럼 제 비례가 중요한 것
    }

    static readonly Dress[] Table =
    {
        // ── 광맥 — **이름까지 맞는다.** 이 팩을 쓸 이유의 절반이 여기 있다.
        new Dress { Building = "Node_CopperVein", Props = new[]{ "natural/P_PROP_orevein_copper_dungeon" }, KeepHeight = true },
        new Dress { Building = "Node_IronVein",   Props = new[]{ "natural/P_PROP_orevein_iron_dungeon"   }, KeepHeight = true },
        new Dress { Building = "Node_GoldVein",   Props = new[]{ "natural/P_PROP_orevein_gold_dungeon"   }, KeepHeight = true },
        new Dress { Building = "Node_SilverVein", Props = new[]{ "natural/P_PROP_orevein_silver_dungeon" }, KeepHeight = true },

        // ── 보관
        new Dress { Building = "Storage",      Props = new[]{ "containers/chests/P_PROP_chest_dungeon_01" } },
        new Dress { Building = "LargeStorage", Props = new[]{ "containers/crates/P_PROP_crate_dungeon_01" } },

        // ── 거주·장례
        //
        // **화톳불은 여기서 뺐다**(2026-09-05) — `CampfireUpgrader`가 그 생김새를 든다.
        // 둘 다 두면 **같은 물건의 얼굴에 답이 둘**이고, 둘 다 돌리면 `[Art]`가 두 겹으로
        // 선다. 무엇보다 여기 방식(*"옛 메시를 끄고 소품을 얹는다"*)은 **불꽃까지 같이
        // 끈다** — 화톳불에서 불꽃은 장식이 아니라 *켜졌는지를 보여 주는 유일한 표시*다
        // (`LightSource.flames`). 그래서 저쪽은 불꽃을 다시 짓고 배선까지 물린다.
        new Dress { Building = "Grave",      Props = new[]{ "furniture/P_PROP_gravestone_dungeon_01" }, KeepHeight = true },
        new Dress { Building = "Pyre",       Props = new[]{ "light/P_PROP_brazier_dungeon_01" }, KeepHeight = true },

        // ── 수용 — **쇠창살이 그대로 있다.**
        new Dress { Building = "PrisonCage", Props = new[]{ "construction/P_PROP_bars_wall_dungeon_01" } },

        // ── 제작·연구
        new Dress { Building = "ResearchTable",       Props = new[]{ "furniture/P_PROP_readingstand_dungeon" } },
        new Dress { Building = "CraftingTable_Labor", Props = new[]{ "furniture/P_PROP_table_dungeon_01" } },
        new Dress { Building = "CraftingTable_Cook",  Props = new[]{ "furniture/P_PROP_fireplace_dungeon" } },
        new Dress { Building = "ButcherTable",        Props = new[]{ "furniture/P_PROP_table_dungeon_02" } },
    };

    [MenuItem("DungeonColony/Building/2. Dress Buildings (모델 입히기)")]
    static void Run()
    {
        int done = 0, missing = 0;
        var notes = new List<string>();

        foreach (var d in Table)
        {
            string path = $"Assets/3_Prefab/Building/{d.Building}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                notes.Add($"건물 없음 — {d.Building}");
                missing++;
                continue;
            }

            var prop = FindProp(d.Props);
            if (prop == null)
            {
                notes.Add($"소품 없음 — {d.Building} ({string.Join(" / ", d.Props)})");
                missing++;
                continue;
            }

            if (Dressing(path, prop, d.KeepHeight)) { done++; notes.Add($"{d.Building} ← {prop.name}"); }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[모델] {done}채에 얼굴을 입혔다(못 찾은 것 {missing}).\n  " + string.Join("\n  ", notes) +
                  "\n되돌리려면 프리팹에서 `[Art]` 자식을 지우고 꺼진 렌더러를 켜면 된다.");
    }

    // ── 되돌리기 ────────────────────────────────────────────────────────────
    //
    // **git이 안전망이 못 된다** — 광맥 넷과 창고는 아직 미커밋이라(2026-08-28 확인)
    // 되돌릴 옛 판이 없다. 그래서 되돌리는 길을 도구가 들고 있어야 한다.
    //
    // 정확히 반대로 한다: `[Art]`를 지우고 렌더러를 켠다. **입힐 때 꺼진 렌더러가 하나도
    // 없었으므로**(그것을 `Run`이 확인하고 경고한다) *"전부 켠다"*가 곧 원래대로다.
    [MenuItem("DungeonColony/Building/3. Undress Buildings (원래대로)")]
    static void Undo()
    {
        int done = 0;
        foreach (var d in Table)
        {
            string path = $"Assets/3_Prefab/Building/{d.Building}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var art = root.transform.Find(ArtName);
                if (art == null) continue;          // 안 입힌 것은 안 건드린다

                Object.DestroyImmediate(art.gameObject);
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    r.enabled = true;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                done++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[모델] {done}채를 원래 도형으로 되돌렸다.");
    }

    static GameObject FindProp(string[] rel)
    {
        foreach (var r in rel)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{Pack}/{r}.prefab");
            if (go != null) return go;
        }
        return null;
    }

    // ── 한 채 입히기 ────────────────────────────────────────────────────────

    static bool Dressing(string path, GameObject prop, bool keepHeight)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // 다시 돌려도 같은 결과가 나오게 — 먼저 지난번 것을 걷는다.
            var old = root.transform.Find(ArtName);
            bool redress = old != null;
            if (redress) Object.DestroyImmediate(old.gameObject);

            // **되돌리기가 "전부 켠다"라서** 원래 꺼져 있던 렌더러가 있으면 그것까지
            // 켜진다. 조용히 그러지 않도록 여기서 알린다(처음 입힐 때만 — 다시 입히는
            // 것이면 꺼져 있는 게 정상이다).
            if (!redress)
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    if (!r.enabled)
                        Debug.LogWarning($"[모델] `{root.name}`의 `{r.name}` 렌더러가 원래 꺼져 있다 — " +
                                         "`3. Undress`로 되돌리면 그것도 같이 켜진다.");

            // **옛 메시는 끄기만 한다.** 지우면 같은 오브젝트에 붙은 콜라이더까지 사라져
            // 클릭도 몸도 안 걸린다(그 콜라이더가 발자국 판정과 짝을 이룬다).
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                r.enabled = false;

            var art = (GameObject)PrefabUtility.InstantiatePrefab(prop, root.transform);
            art.name = ArtName;
            art.transform.localPosition = Vector3.zero;
            art.transform.localRotation = Quaternion.identity;
            art.transform.localScale    = Vector3.one;

            // **소품의 콜라이더는 뗀다** — 길찾기가 보는 것은 타일 점유지 콜라이더가
            // 아니고(`obstacleLayer`는 `Wall`뿐), 남겨 두면 지도는 "빈 칸"이라 하는데
            // 몸이 부딪혀 폰이 처박힌다. 건물의 콜라이더는 이미 루트 쪽에 있다.
            foreach (var c in art.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            Fit(root, art, keepHeight);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 소품을 그 건물의 발자국에 맞춘다.
    //
    // **크기는 `BuildingData.Size`가 답한다** — 옛 큐브의 크기를 재서 맞추면 그 큐브가
    // 발자국과 어긋나 있었을 때 그 실수를 그대로 물려받는다. 데이터가 진짜다.
    static void Fit(GameObject root, GameObject art, bool keepHeight)
    {
        var pb = root.GetComponent<PlacedBuilding>();
        var size = pb != null && pb.Data != null ? pb.Data.Size : Vector2Int.one;
        float wantX = Mathf.Max(1, size.x) * FitMargin;
        float wantZ = Mathf.Max(1, size.y) * FitMargin;

        if (!Bounds(art, out var b) || b.size.x <= 0.0001f || b.size.z <= 0.0001f) return;

        // **가로세로 중 더 빡빡한 쪽에 맞춰 균등하게 줄인다** — 축마다 따로 늘이면
        // 모델이 찌그러지고, 그러면 모델을 바꾼 뜻이 없다.
        float s = Mathf.Min(wantX / b.size.x, wantZ / b.size.z);

        // 세로를 지키는 것들(광맥·불)은 **키우지는 않는다** — 발자국보다 큰 모델을 줄이는
        // 것은 맞지만, 작은 모델을 늘리면 1×1 화톳불이 사람보다 커진다.
        if (keepHeight) s = Mathf.Min(s, 1f);

        art.transform.localScale = Vector3.one * s;

        // 바닥에 발을 붙이고 발자국 한복판에 놓는다 — 소품의 피벗은 팩마다 다르다.
        if (!Bounds(art, out b)) return;
        var c = b.center;
        art.transform.localPosition += new Vector3(-c.x, -b.min.y, -c.z);
    }

    static bool Bounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!found) { bounds = r.bounds; found = true; }
            else        bounds.Encapsulate(r.bounds);
        }
        return found;
    }
}
