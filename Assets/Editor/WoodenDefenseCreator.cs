using System.IO;
using UnityEditor;
using UnityEngine;

// **나무 방어물 둘** — 바리케이드와 목책. `DungeonColony → Create Wooden Defenses`
//
// 방어 칸에 돌벽(`Wall`) 하나뿐이었다. 벽은 비싸고(석재 5) 초반에 쓸 것이 없어서, 담을
// 두르는 일이 **중반에나 시작되는 일**이 됐다. 나무는 첫날부터 있는 자재다.
//
// 셋의 자리:
//   **바리케이드**(나무 5) — 급히 세우는 낮은 장애물. 제일 싸다.
//   **목책**(나무 8)       — 통나무를 세워 박은 담. 나무로 짓는 제대로 된 벽.
//   **벽**(석재 5)         — 오래 가는 것.
//
// **셋 다 길을 막는다**(`OccupiesTile`). 엄폐(cover)가 아직 없어서 "막지는 않지만 방어에
// 도움이 되는 것"을 만들 수가 없다 — 안 막는 바리케이드는 지금은 그냥 장식이다.
// 엄폐가 생기면 그때 갈라도 늦지 않다(값과 모양은 이미 다르다).
//
// 모델은 **기본 도형으로 짓는다**(`PrisonCageCreator`와 같은 방식). 갈아 끼울 때는 프리팹만
// 바꾸면 된다 — `BuildingData`가 프리팹을 가리키고 나머지는 컴포넌트가 들고 있다.
//
// ── 다시 돌리면 **모양은 갱신되고 값은 남는다** ──────────────────────────────
//
// 다른 생성기는 "없는 것만 만든다"인데 여기는 갈랐다. **모양은 코드가 짓는 것**이라
// 여기 숫자를 고치고 다시 돌렸을 때 반영이 안 되면 생성기가 아무 쓸모가 없다 —
// 프리팹을 손으로 지우고 돌려야 했다.
//
// 그래서 **프리팹은 늘 다시 짓고**(`SaveAsPrefabAsset`이 같은 경로에 덮어쓰므로 GUID가
// 그대로다 — 씬에 놓인 것도 `BuildingData.Prefab` 참조도 안 끊긴다), **`BuildingData`는
// 없을 때만 만든다**(값·설명은 인스펙터에서 손볼 수 있다).
//
// 프리팹을 인스펙터에서 손봤다면 다시 돌리는 순간 날아간다 — 모양을 바꾸고 싶으면
// **여기 숫자를 고치는 것**이 맞는 자리다.
public static class WoodenDefenseCreator
{
    const string WoodPath = "Assets/Resources/Data/Items/Res_Wood.asset";

    // 나무색. 코드가 색을 들지 않고 **에셋을 골라 끼운다**(침대 매트리스와 같은 규칙) —
    // 그래야 에디터에서도 그 색으로 보이고 머티리얼 인스턴스가 안 생긴다.
    const string PalisadeMat  = "Assets/2_Material/Bld_735433.mat";
    const string BarricadeMat = "Assets/2_Material/Bld_664A33.mat";

    [MenuItem("DungeonColony/Create Wooden Defenses")]
    static void Create()
    {
        MakeBarricade();
        MakePalisade();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[WoodenDefense] 바리케이드·목책 준비 완료. " +
                  "건설 목록은 레지스트리에서 오므로 따로 등록할 것이 없습니다 " +
                  "(방어 칸에 벽과 나란히 뜹니다).");
    }

    // ── 바리케이드 ────────────────────────────────────────────────────────────
    //
    // **담이 아니라 방어물이다** — 줄지어 두르는 것이 아니라 길목에 하나씩 놓는다.
    // 그래서 **두 칸을 차지한다**(2×1): 줄 긋기(`BuildModeManager`의 두 번 클릭)는 한 칸짜리
    // 전용이라 여기 안 걸리고, 한 번 놓으면 통로를 확실히 막는 크기가 된다.
    // (목책은 반대다 — 담이라 1×1이고 줄로 긋는다.)
    static void MakeBarricade()
    {
        const string prefabPath = "Assets/3_Prefab/Barricade.prefab";
        const string dataPath   = "Assets/Resources/Data/Buildings/Barricade.asset";

        GameObject prefab;
        {
            var root = NewRoot("Barricade", height: 1.0f, tilesX: 2);
            var mat  = AssetDatabase.LoadAssetAtPath<Material>(BarricadeMat);

            // ── A자 받침 둘 — **옆에서 보면 긴 삼각형** ──────────────────────
            //
            // 공사장 바리케이드의 그 모양이다. 양 끝에 다리를 벌려 세우고 그 사이를 널빤지로
            // 잇는다 — X자로 엇갈린 것보다 "세워 둔 물건"으로 읽히고, 앞뒤 방향이 생겨서
            // 회전(Q/E)에 뜻이 붙는다.
            foreach (float side in new[] { -0.72f, 0f, 0.72f })
            {
                Beam(root, mat, $"LegF{side:0.00}", new Vector3(side, 0.42f, -0.16f),
                     new Vector3(-22f, 0f, 0f), new Vector3(0.10f, 0.9f, 0.10f));
                Beam(root, mat, $"LegB{side:0.00}", new Vector3(side, 0.42f, 0.16f),
                     new Vector3(22f, 0f, 0f), new Vector3(0.10f, 0.9f, 0.10f));
            }

            // 다리를 묶는 가로 버팀목(삼각형의 밑변).
            Beam(root, mat, "Tie", new Vector3(0f, 0.20f, 0f), Vector3.zero,
                 new Vector3(1.70f, 0.07f, 0.07f));

            // 널빤지 — 위아래 둘. 살짝 기울여 붙여 판자를 덧댄 티를 낸다.
            Beam(root, mat, "PlankTop", new Vector3(0f, 0.80f, 0f), new Vector3(0f, 0f, 1.5f),
                 new Vector3(1.95f, 0.16f, 0.09f));
            Beam(root, mat, "PlankLow", new Vector3(0f, 0.52f, 0f), new Vector3(0f, 0f, -1.2f),
                 new Vector3(1.95f, 0.14f, 0.09f));

            prefab = Save(root, prefabPath);
        }

        EnsureData(dataPath, prefab,
                   name: "나무 바리케이드",
                   desc: "다리를 벌려 세우고 널빤지를 덧댄 장애물. 두 칸을 막는다. " +
                         "담을 두르는 것이 아니라 길목에 하나씩 놓는 물건이다.",
                   sortOrder: 12, woodCost: 8, size: new Vector2Int(2, 1), dragBuild: false);
    }

    // ── 목책 ──────────────────────────────────────────────────────────────────
    // 통나무를 세워 박은 담. 캡슐이라 위가 둥글다 — 각목(바리케이드)과 한눈에 갈린다.
    static void MakePalisade()
    {
        const string prefabPath = "Assets/3_Prefab/Palisade.prefab";
        const string dataPath   = "Assets/Resources/Data/Buildings/Palisade.asset";

        GameObject prefab;
        {
            var root = NewRoot("Palisade", height: 1.8f, tilesX: 1);
            var mat  = AssetDatabase.LoadAssetAtPath<Material>(PalisadeMat);

            // 통나무 다섯. 높이를 조금씩 달리해 손으로 박은 티를 낸다.
            float[] xs = { -0.36f, -0.18f, 0f, 0.18f, 0.36f };
            float[] hs = {  0.78f,  0.85f, 0.82f, 0.86f, 0.76f };

            for (int i = 0; i < xs.Length; i++)
            {
                var log = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                log.name = $"Log{i}";
                log.transform.SetParent(root.transform, false);
                log.transform.localPosition = new Vector3(xs[i], hs[i], 0f);
                log.transform.localScale    = new Vector3(0.17f, hs[i], 0.17f);
                Strip(log, mat);
            }

            // 뒤를 잡아 주는 가로대.
            Beam(root, mat, "Rail", new Vector3(0f, 1.15f, 0.12f), Vector3.zero,
                 new Vector3(0.92f, 0.11f, 0.11f));

            prefab = Save(root, prefabPath);
        }

        EnsureData(dataPath, prefab,
                   name: "목책",
                   desc: "통나무를 세워 박은 담. 길을 막는다. 돌벽보다 싸고, 벽처럼 줄로 그어 두른다.",
                   sortOrder: 13, woodCost: 6, size: new Vector2Int(1, 1), dragBuild: true);
    }

    // ── 공통 ──────────────────────────────────────────────────────────────────

    // **콜라이더는 루트에, 레이어는 Wall.** 길찾기의 국소 경로가 `obstacleLayer`(Wall)만
    // 장애물로 보므로(`HierarchicalPathfinder`), 레이어를 안 맞추면 **타일은 막혔는데 폰이
    // 그 위로 경로를 내는** 꼴이 된다. 크기는 `Size - 0.2` 규약(1×1 → 0.8).
    static GameObject NewRoot(string name, float height, int tilesX)
    {
        var root  = new GameObject(name);
        int wall  = LayerMask.NameToLayer("Wall");
        if (wall >= 0) root.layer = wall;

        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, height * 0.5f, 0f);
        box.size   = new Vector3(tilesX - 0.2f, height, 0.8f);
        // 트리거가 아니다 — `OccupiesTile`이 참인 건물은 실제로 막아야 지도와 물리가 같은 말을 한다.

        return root;
    }

    static void Beam(GameObject root, Material mat, string name,
                     Vector3 pos, Vector3 euler, Vector3 scale)
    {
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = name;
        beam.transform.SetParent(root.transform, false);
        beam.transform.localPosition    = pos;
        beam.transform.localEulerAngles = euler;
        beam.transform.localScale       = scale;
        Strip(beam, mat);
    }

    // 자식의 콜라이더는 **전부 없앤다** — 클릭 판정도 통행 판정도 루트 하나로 답해야 한다
    // (자식에 있으면 `hit.collider.GetComponent<PlacedBuilding>()`이 못 찾는다).
    static void Strip(GameObject go, Material mat)
    {
        Object.DestroyImmediate(go.GetComponent<Collider>());

        int wall = LayerMask.NameToLayer("Wall");
        if (wall >= 0) go.layer = wall;

        var rend = go.GetComponent<Renderer>();
        if (rend != null && mat != null) rend.sharedMaterial = mat;
    }

    static GameObject Save(GameObject root, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    // `BuildingData`를 만들고 **프리팹 양쪽에 서로를 물린다.**
    //
    // `PlacedBuilding.data`를 안 채우면 씬에 손으로 놓은 것이 발자국을 등록 안 해
    // **모델 한복판을 가로지르는 경로**가 나오고, 좌클릭해도 어느 건물인지 몰라 패널이 안 뜬다
    // (`CLAUDE.md`가 적어 둔 함정 — 예전엔 `LargeStorage` 하나만 붙어 있었다).
    static void EnsureData(string dataPath, GameObject prefab,
                           string name, string desc, int sortOrder, int woodCost, Vector2Int size,
                           bool dragBuild)
    {
        var data = AssetDatabase.LoadAssetAtPath<BuildingData>(dataPath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BuildingData>();
            data.BuildingName  = name;
            data.Description   = desc;
            data.Category      = BuildCategory.Defense;
            data.SortOrder     = sortOrder;
            data.Size          = size;
            data.UsesBlueprint = true;
            data.OccupiesTile  = true;
            data.CostItem      = AssetDatabase.LoadAssetAtPath<ItemData>(WoodPath);
            data.CostAmount    = woodCost;

            Directory.CreateDirectory(Path.GetDirectoryName(dataPath));
            AssetDatabase.CreateAsset(data, dataPath);
        }

        if (data.Prefab == null && prefab != null)
        {
            data.Prefab = prefab;
            EditorUtility.SetDirty(data);
        }

        // **크기는 값이 아니라 모양의 짝이라 늘 맞춘다.**
        //
        // `Size`는 모델이 몇 칸을 덮느냐와 한 몸이다 — 프리팹을 2칸짜리로 다시 지어 놓고
        // 에셋이 1×1로 남으면 **모델은 두 칸인데 한 칸만 막고**, 줄 긋기(한 칸짜리 전용)에도
        // 잘못 걸린다(실제로 바리케이드가 그렇게 그어졌다). 값·설명과 달리 인스펙터에서
        // 손볼 것이 아니므로 여기서 못 박는다.
        // `DragBuild`도 같은 축이다 — **잇대어 쓰는 물건인가**는 저작자의 취향이 아니라 그
        // 물건이 무엇인지의 문제다(목책은 담, 바리케이드는 하나씩 놓는 방어물).
        if (data.Size != size || data.DragBuild != dragBuild)
        {
            data.Size      = size;
            data.DragBuild = dragBuild;
            EditorUtility.SetDirty(data);
        }

        // 프리팹의 `PlacedBuilding`이 이 데이터를 가리키게 한다(양방향).
        //
        // **늘 다시 건다** — 프리팹을 매번 새로 짓기 때문에 `PlacedBuilding`도 매번 새것이라
        // 참조가 비어 있다. 안 채우면 씬에 손으로 놓은 것이 발자국을 등록 안 해 모델 한복판을
        // 가로지르는 경로가 나오고, 좌클릭해도 어느 건물인지 모른다.
        if (prefab == null) return;

        var placed = prefab.GetComponent<PlacedBuilding>() ?? prefab.AddComponent<PlacedBuilding>();
        var so     = new SerializedObject(placed);
        var prop   = so.FindProperty("data");
        if (prop != null && prop.objectReferenceValue != data)
        {
            prop.objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
        }
    }
}
