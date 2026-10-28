using UnityEditor;
using UnityEngine;

// **벽걸이 횃불은 모양이 둘이라 전용 도구가 든다**
// (2026-09-18, 사용자: *"횃불은 torch_metaltip_large 이게 서있는 모델
//  torchholder_with_torch_small 이게 벽에 걸린 모델"*).
//
// ## 왜 `PropDresser`가 못 하나
//
// `WallTorch`는 놓인 자리에 따라 **선 것 / 벽에 걸린 것** 중 하나를 켠다(`IPlacementPose`).
// 저쪽 방식은 *옛 렌더러를 전부 끄고 루트에 소품 하나를 얹는 것*이라, 그대로 쓰면
// **그 두 모양이 통째로 사라지고** 어디에 놓든 같은 그림이 된다.
//
// ## 자식을 갈아 끼운다 — 이름이 아니라 필드로 찾는다
//
// `standing`·`wall`은 **중첩 프리팹**이라(원래도 던전 팩의 `torch_standing_dungeon` /
// `torch_wall_dungeon`이었다) YAML에서 이름이 비어 있다. 그래서 **컴포넌트의 필드에서**
// 참조를 읽는다 — 이름으로 찾으면 팩을 갈아 끼우는 날 조용히 빗나간다.
//
// **불꽃은 안 건드린다**(`Flames`·`Light`는 `mount` 쪽에 따로 붙어 있다) — 화톳불에서
// 배운 자리다: *불꽃은 장식이 아니라 켜졌는지를 보여 주는 유일한 표시*다.
//
// **다시 돌려도 된다** — 먼저 지난번 `[Art]`를 걷는다.
public static class WallTorchDresser
{
    // ── 무덤 뚜껑 ───────────────────────────────────────────────────────────
    //
    // **뚜껑은 `[Art]` 안에 넣으면 안 된다**(2026-09-18, 사용자: *"시체없을때는 열려있고
    // 시체 있으면 닫혀있게"*). 본체와 한 덩어리가 되면 **같이 움직여** 여닫을 수가 없다.
    // 그래서 뚜껑만 **따로 얹고** `Grave.lid`에 물린다 — 여는 것은 런타임(`Grave`)의 일이다.
    const string GravePrefab = "Assets/3_Prefab/Building/Grave.prefab";
    const string LidProp     = Cave + "/Props/Grave/casket_stone_lid.prefab";
    const string LidName     = "[Lid]";

    [MenuItem("DungeonColony/Building/7. Dress Grave Lid (무덤 뚜껑)")]
    static void DressGrave()
    {
        var lidProp = AssetDatabase.LoadAssetAtPath<GameObject>(LidProp);
        if (lidProp == null) { Debug.LogError($"[무덤] 뚜껑 소품을 못 찾았습니다 — {LidProp}"); return; }

        var root = PrefabUtility.LoadPrefabContents(GravePrefab);
        try
        {
            var grave = root.GetComponent<Grave>();
            if (grave == null) { Debug.LogError("[무덤] `Grave` 컴포넌트가 없습니다."); return; }

            // **이미 얹혀 있으면 손대지 않는다**(2026-09-18, 사용자: *"무덤뚜껑은 내가
            // 알아서 잘 조절해놨으니깐 기억해놔 프리팹을 바꿔놨어"*).
            //
            // 자리를 눈으로 맞춘 것은 **저작**이다 — 다시 얹으면 그 손질이 통째로 날아간다.
            // 정말 새로 얹고 싶으면 프리팹에서 `[Lid]`를 지우고 돌리면 된다.
            if (root.transform.Find(LidName) != null)
            {
                Debug.Log("[무덤] 뚜껑이 이미 있어 그대로 두었습니다 — **맞춰 둔 자리를 지킵니다.**\n"
                        + "다시 얹으려면 프리팹에서 `[Lid]`를 지우고 돌리세요.");
                return;
            }

            var lid = (GameObject)PrefabUtility.InstantiatePrefab(lidProp, root.transform);
            if (lid == null) { Debug.LogError("[무덤] 뚜껑을 얹지 못했습니다."); return; }

            lid.name = LidName;
            lid.transform.localPosition = Vector3.zero;
            lid.transform.localRotation = Quaternion.identity;
            lid.transform.localScale    = Vector3.one;

            foreach (var c in lid.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            // **본체와 같은 배율로 줄인다** — `PropDresser`가 `[Art]`를 발자국에 맞춰
            // 줄여 놓았으므로, 뚜껑만 제 크기면 관보다 큰 뚜껑이 얹힌다.
            var art = root.transform.Find("[Art]");
            if (art != null) lid.transform.localScale = art.localScale;

            var so = new SerializedObject(grave);
            so.FindProperty("lid").objectReferenceValue = lid.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GravePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[무덤] 돌 관 뚜껑을 얹고 `Grave.lid`에 물렸습니다 — "
                    + "**비면 열리고 묻으면 닫힙니다.**\n"
                    + "`1. Dress Buildings`(본체)를 **먼저** 돌려야 크기가 맞습니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    const string Cave    = "Assets/assetsss/3DForge/Cave Adventure kit/Prefabs";
    const string Prefab  = "Assets/3_Prefab/Building/WallTorch.prefab";
    const string ArtName = "[Art]";

    const string StandProp = Cave + "/Props/Torches/torch_metaltip_large.prefab";
    const string WallProp  = Cave + "/Props/Torches/torchholder_with_torch_small.prefab";

    [MenuItem("DungeonColony/Building/6. Dress Wall Torch (횃불 두 모양)")]
    static void Run()
    {
        var stand = AssetDatabase.LoadAssetAtPath<GameObject>(StandProp);
        var wall  = AssetDatabase.LoadAssetAtPath<GameObject>(WallProp);
        if (stand == null || wall == null)
        {
            Debug.LogError($"[횃불] 소품을 못 찾았습니다 — {StandProp} / {WallProp}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var torch = root.GetComponent<WallTorch>();
            if (torch == null) { Debug.LogError("[횃불] `WallTorch` 컴포넌트가 없습니다."); return; }

            var so = new SerializedObject(torch);
            var standing = so.FindProperty("standing")?.objectReferenceValue as GameObject;
            var onWall   = so.FindProperty("wall")?.objectReferenceValue as GameObject;

            if (standing == null || onWall == null)
            {
                Debug.LogError("[횃불] `standing`/`wall`이 비어 있습니다 — 프리팹 배선을 확인하세요.");
                return;
            }

            int n = 0;
            if (Swap(standing, stand)) n++;
            if (Swap(onWall,   wall))  n++;

            if (n == 0) { Debug.LogWarning("[횃불] 바꾼 것이 없습니다."); return; }

            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[횃불] 두 모양을 동굴 팩 것으로 갈아 끼웠습니다 ({n}개).\n"
                    + "선 것 ← torch_metaltip_large · 벽 ← torchholder_with_torch_small\n"
                    + "되돌리려면 그 아래 `[Art]`를 지우고 꺼진 렌더러를 켜면 됩니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 그 모양 **아래에** 소품을 얹고 원래 메시는 끈다.
    static bool Swap(GameObject pose, GameObject prop)
    {
        // 지난번 것을 먼저 걷는다.
        var old = pose.transform.Find(ArtName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // **옛 메시는 끄기만 한다** — 지우면 같은 오브젝트의 콜라이더까지 사라진다.
        foreach (var r in pose.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;

        var art = (GameObject)PrefabUtility.InstantiatePrefab(prop, pose.transform);
        if (art == null) return false;

        art.name = ArtName;
        art.transform.localPosition = Vector3.zero;
        art.transform.localRotation = Quaternion.identity;
        art.transform.localScale    = Vector3.one;

        // 길찾기는 타일 점유를 보지 콜라이더를 안 본다 — 남겨 두면 폰이 처박힌다.
        foreach (var c in art.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        // **불꽃 높이는 `WallTorch`가 `head`로 잡는다** — 여기서 자리를 손대면 그 값과
        // 싸운다. 소품은 **제 피벗 그대로** 두고, 어긋나면 프리팹의 `standHead`/`wallHead`를
        // 고치는 것이 맞다(값은 씬이 아니라 프리팹이 든다).
        return true;
    }
}
