using UnityEditor;
using UnityEngine;

// **우리 — 2×2 쇠창살 방**(2026-09-30, 사용자: *"우리가 일단 너무 작고 이거 말고 다른 모델도 찾아봐"*).
//
// 전에는 1×1칸에 **사슬에 매달린 새장**(던전 팩 `COMP_PROP_cage_dungeon_01`)을 칸 크기로 줄여 입혔다 — 사람이 갇힌
// 자리가 사람보다 작았다. 던전 팩의 우리 넷이 전부 매달린 것이라, **2×2칸에 창살 벽 셋 + 창살 문 하나**(앞 = −z,
// 카메라 쪽)를 둘러 세운다. 돌 10 · 청동 4.
//
// 나무 울타리 우리도 같이 지었다가 **지웠다**(사용자: *"나무 우리는 내가 생각한 우리가 아닌데? 이런 거밖에 없으면
// 그냥 지워"*) — 동굴 팩 울타리는 짐승 우리(목장 울)라 *사람을 가두는 곳*으로 안 읽혔다.
//
// **가두는 일은 그대로 `PrisonCage` 컴포넌트가 한다** — 모양만 바꾼다. 프리팹·에셋이 옛 우리 그대로라 이미 놓인 우리와
// 가리키는 곳이 안 끊긴다.
//
// ## 이름은 `[Cage]`다 — `[Art]`가 아니다
//
// `PropDresser`(`1. Dress Buildings`)가 한때 **표에 없는 건물의 `[Art]`를 지웠다**(2026-09-30에 `Retired` 목록으로 좁혔다).
// 이 이름을 쓰면 그런 도구와 또 엉킨다.
//
// **다시 돌려도 같다** — 지난번 `[Cage]`(와 옛 `[Art]`)를 먼저 걷는다.
public static class PrisonCageBuilder
{
    const string PrefabPath = "Assets/3_Prefab/Building/PrisonCage.prefab";
    const string DataPath   = "Assets/Resources/Data/Buildings/PrisonCage.asset";

    const string BarWall = "Assets/assetsss/djf/prefabs/PROPS/construction/P_PROP_bars_wall_dungeon_01.prefab";
    const string BarDoor = "Assets/assetsss/djf/prefabs/PROPS/construction/P_PROP_bars_doorway_dungeon_01.prefab";

    const string ItemDir  = "Assets/Resources/Data/Items";
    const string CageName = "[Cage]";

    const float Side      = 2f;     // 칸 수 = 한 변(m)
    const float BarHeight = 2.4f;   // 창살 높이(m) — 팩 벽은 4m 모듈이라 사람 키에 맞춰 누른다

    [MenuItem("DungeonColony/Building/D. Build Prison Cage (쇠창살 우리 2x2)")]
    static void Run()
    {
        var wall = AssetDatabase.LoadAssetAtPath<GameObject>(BarWall);
        var door = AssetDatabase.LoadAssetAtPath<GameObject>(BarDoor);
        if (wall == null || door == null) { Debug.LogError($"[우리] 소품을 못 찾았습니다 — {BarWall} / {BarDoor}"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) { Debug.LogError($"[우리] 프리팹이 없습니다 — {PrefabPath}"); return; }

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (var n in new[] { CageName, "[Art]" })
            {
                var old = root.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // **옛 도형은 끄기만 한다** — 가두는 자리(`HoldAnchor`)가 그 옆에 있다.
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;

            Ring(root, i => i == 0 ? door : wall);

            var box = root.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.size   = new Vector3(Side, BarHeight, Side);
                box.center = new Vector3(0f, BarHeight * 0.5f, 0f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var data = AssetDatabase.LoadAssetAtPath<BuildingData>(DataPath);
        if (data != null)
        {
            var stone  = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemDir}/Res_Stone.asset");
            var bronze = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemDir}/Res_Bronze.asset");
            data.BuildingName = "우리";
            data.Description  = "창살로 두른 감옥 칸. 붙잡아 온 사람 한 명이 들어간다. 회복도 치료도 없다 - 살려서 다룰 사람은 죄수 침대에 눕힌다.";
            data.Size         = new Vector2Int((int)Side, (int)Side);
            data.Costs.Clear();
            if (stone  != null) data.Costs.Add(new RecipeData.Ingredient { Item = stone,  Amount = 10 });
            if (bronze != null) data.Costs.Add(new RecipeData.Ingredient { Item = bronze, Amount = 4 });
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[우리] 2×2 쇠창살 우리를 지었습니다(창살 벽 셋 + 창살 문 · 돌 10 · 청동 4). 창살 문이 카메라 쪽(−z)에 섭니다 — 반대면 말해 주세요.");
    }

    // 네 변에 하나씩. `pick(i)` — 변 번호(0 = 앞 −z · 1 = 오른쪽 +x · 2 = 뒤 +z · 3 = 왼쪽 −x)마다 쓸 소품.
    static void Ring(GameObject root, System.Func<int, GameObject> pick)
    {
        var holder = new GameObject(CageName).transform;
        holder.SetParent(root.transform, false);

        for (int i = 0; i < 4; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pick(i), holder);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);   // 고르기는 루트 콜라이더 하나

            // 소품의 긴 축을 잰다 — 그 축이 변을 따라 눕게 돌리고, 길이는 한 변에 · 높이는 사람 키에.
            var b0 = Bounds(go);
            bool alongX = b0.size.x >= b0.size.z;
            float len   = Mathf.Max(b0.size.x, b0.size.z);
            float s     = len > 1e-4f ? Side / len : 1f;
            float sy    = b0.size.y > 1e-4f ? BarHeight / b0.size.y : s;
            go.transform.localScale    = new Vector3(s, sy, s);
            go.transform.localRotation = Quaternion.Euler(0f, -(i * 90f + (alongX ? 0f : 90f)), 0f);

            Vector3 mid = i switch
            {
                0 => new Vector3(0f, 0f, -Side * 0.5f),
                1 => new Vector3( Side * 0.5f, 0f, 0f),
                2 => new Vector3(0f, 0f,  Side * 0.5f),
                _ => new Vector3(-Side * 0.5f, 0f, 0f),
            };

            // 경계의 가운데를 변의 가운데에, 바닥을 0에.
            var b = Bounds(go);
            var want = root.transform.TransformPoint(mid);
            go.transform.position += new Vector3(want.x - b.center.x, root.transform.position.y - b.min.y, want.z - b.center.z);
        }
    }

    static Bounds Bounds(GameObject go)
    {
        var b = new Bounds(go.transform.position, Vector3.zero);
        bool any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
