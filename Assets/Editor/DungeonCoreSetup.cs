using System.IO;
using UnityEditor;
using UnityEngine;

// **핵을 건설 목록에 올린다 — 판이 열릴 때 핵을 안 주기로 했다**(2026-08-27, 사용자 결정).
//
// 여기까지 던전 핵은 **씬에 미리 놓인 물건**이었다. 판이 열리면 이미 거점이 있고, 플레이어가
// 하는 일은 그 둘레를 채우는 것뿐이었다. 그런데 **루프는 탐험이 본류다**(`design_progression`) —
// *어디에 거점을 세울까*가 첫 번째 결정이 되어야 한다.
//
// ── 왜 새 배치 모드를 안 만드나 ────────────────────────────────────────────
//
// 핵을 놓는 일은 **건물을 놓는 일과 다르지 않다** — 자리를 고르고, 발자국이 들어가나 보고,
// 폰이 가서 세운다. 전용 모드를 만들면 미리보기 · 회전 · 격자 스냅 · 점유 검사 · 취소를
// **전부 다시 만들어야 하고**, 그 순간 두 벌이 되어 한쪽만 고쳐진다.
//
// 그래서 **`BuildingData` 하나로 푼다.** 건설 메뉴 · 청사진 · 폰 배정 · 취소 환불이 전부
// 이미 있는 것 그대로다.
//
// ── 두 개의 함정 ───────────────────────────────────────────────────────────
//
// · **빌드존 200m** — 빌드존은 *"핵 둘레 이만큼"*이라 **핵이 없으면 가운데가 없다.** 그대로
//   두면 원점 둘레 200m가 유일한 건설 구역이 되어 **핵 놓을 자리를 고르는 것 자체가 막힌다**
//   (세계가 8km인데). `MapManager._centered`가 그것을 푼다 — 가운데가 잡히기 전에는 안 자른다.
//
// · **값을 붙이면 안 된다**(적어도 첫 핵에는) — 창고도 없고 재료도 없는 시점이라 **배낭에
//   있는 것만이 재료**다. 값이 모자라면 청사진이 영영 안 지어지고, 화면에는 *"왜 아무도 안
//   오지"*로만 보인다. 그래서 **첫 핵은 공짜**로 두고, 값은 나중에 이 에셋에서 붙인다.
public static class DungeonCoreSetup
{
    const string PrefabDir  = "Assets/3_Prefab/Building";
    const string PrefabPath = PrefabDir + "/DungeonCore.prefab";

    const string DataDir  = "Assets/Resources/Data/Buildings";
    const string DataPath = DataDir + "/DungeonCore.asset";

    [MenuItem("DungeonColony/Core/1. Make Core Placeable")]
    static void Run()
    {
        var core = Object.FindFirstObjectByType<DungeonCore>();
        if (core == null)
        {
            Debug.LogError("[핵 배치] 열린 씬에 `DungeonCore`가 없다 — 무엇을 프리팹으로 만들지 " +
                           "알 수 없다. 게임 씬(`Scene1`)을 열고 다시 실행할 것.");
            return;
        }

        var prefab = MakePrefab(core);
        if (prefab == null) return;

        var data = MakeData(prefab);
        if (data == null) return;

        Selection.activeObject = data;
        EditorGUIUtility.PingObject(data);

        Debug.Log(
            $"[핵 배치] `{PrefabPath}` + `{DataPath}`를 만들었다 — 건설 메뉴 `기타`에 뜬다.\n" +
            "**남은 것은 씬에서 하는 하나뿐이다: 미리 놓인 핵을 지울 것.** 안 지우면 판이 " +
            "열리자마자 거점이 있어서, 놓을 수 있게 만든 뜻이 없다.\n" +
            "값을 붙이려면 에셋의 `CostItem`/`CostAmount`를 채운다 — 다만 **첫 핵에 값을 붙이면** " +
            "창고가 없어 배낭에 든 것만이 재료다(`Res_Etherstone` 같은 것을 시작 소지품에 넣을 것).");
    }

    // ── 프리팹 ──────────────────────────────────────────────────────────────

    static GameObject MakePrefab(DungeonCore core)
    {
        Directory.CreateDirectory(PrefabDir);

        // **이미 있으면 다시 만들지 않는다.** 프리팹을 통째로 다시 지으면 루트의 fileID가
        // 바뀌어, 씬에 놓인 인스턴스가 *"Prefab mismatch"*로 오버라이드를 잃는다.
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            Debug.Log($"[핵 배치] 프리팹은 이미 있다 — `{PrefabPath}`. 그대로 쓴다.");
            return existing;
        }

        var go = Object.Instantiate(core.gameObject);
        go.name = "DungeonCore";

        // **저작한 부양 높이를 버린다.** 씬의 핵은 그 지형 위에 맞춰 띄워 놓은 것이라,
        // 그 Y를 그대로 프리팹에 담으면 **어디에 지어도 그만큼 뜬다.**
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // `PlacedBuilding`이 있어야 발자국이 길찾기에 등록된다 — 없으면 콜라이더만 아는
        // 상태가 되어 **모델 한복판을 가로지르는 경로**가 나온다.
        if (go.GetComponent<PlacedBuilding>() == null) go.AddComponent<PlacedBuilding>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        if (prefab == null) Debug.LogError($"[핵 배치] 프리팹을 못 만들었다 — `{PrefabPath}`.");
        return prefab;
    }

    // ── 건설 데이터 ─────────────────────────────────────────────────────────

    static BuildingData MakeData(GameObject prefab)
    {
        Directory.CreateDirectory(DataDir);

        var data = AssetDatabase.LoadAssetAtPath<BuildingData>(DataPath);
        bool made = data == null;
        if (made) data = ScriptableObject.CreateInstance<BuildingData>();

        data.Id           = "dungeon_core";
        data.BuildingName = "던전 핵";
        data.Description  = "여기를 거점으로 삼는다. 둘레가 내 영역이 되어 창고와 재료가 " +
                            "하나로 세어지고, 밖에 지은 건물은 잠든다. 옮길 수 있다.";
        data.Category     = BuildCategory.Misc;
        data.SortOrder    = -100;   // 갈래 맨 앞 — 아무것도 없을 때 제일 먼저 눈에 들어와야 한다
        data.Size         = Vector2Int.one;
        data.Prefab       = prefab;

        // **청사진이다 — 즉시 배치가 아니다.** 값이 붙은 건물을 즉시 배치로 두면 자재가
        // 모자랄 때 클릭이 그냥 씹혀 *안 지어지는 것*과 *못 놓는 것*이 구별되지 않는다.
        // 지금은 공짜라 상관없어 보이지만, 값을 붙이는 순간 그 함정에 빠진다.
        data.UsesBlueprint = true;

        // **길을 안 막는다.** 핵은 통행 차단을 제 손으로 한 칸만 한다(`DungeonCore.Occupy`) —
        // 여기서 또 막으면 두 곳이 같은 일을 하고, 핵을 옮겼을 때 한쪽이 안 풀린다.
        data.OccupiesTile = false;

        // **값은 안 붙인다** — 위 주석 참고. 첫 핵은 창고도 재료도 없는 시점에 세운다.
        data.CostItem   = null;
        data.CostAmount = 0;

        if (made) AssetDatabase.CreateAsset(data, DataPath);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return data;
    }
}
