using System.IO;
using UnityEditor;
using UnityEngine;

// **걸어 들어가는 건물을 찍어낸다**(2026-08-28. `HutCreator`가 한 채만 내던 것을 표로 넓혔다).
//
// 방향은 *"건물 채로 짓는다"*로 정했다(2026-08-28, 사용자 결정) — 바닥·벽·계단을 하나씩
// 놓는 것이 아니라 **기능이 있는 것은 통째로**, 선(성벽·바리케이드)만 조각으로.
//
// ── 왜 도구로 만드나 ───────────────────────────────────────────────────────
//
// 걸어 들어가는 건물은 **벽이 선 자리**와 **길을 막는 칸**(`BuildingData.BlockMask`)이
// 정확히 같아야 한다. 어긋나면 둘 중 하나다: 벽이 없는데 막혀서 **문간이 안 열리거나**,
// 벽이 있는데 안 막혀서 **폰이 벽을 통과한다.** 그리고 그 어긋남은 **놓아 보기 전에는
// 안 보인다** — 화면에는 멀쩡한 집이 서 있으니까.
//
// 손으로 만들면 어긋나는 것은 시간문제다. **한 도구가 둘을 같이 내면 그 어긋남이
// 구조적으로 없다** — 여기서는 `Mask`가 유일한 출처이고 벽도 데이터도 값도 거기서 나온다.
//
// ── 왜 표인가 ──────────────────────────────────────────────────────────────
//
// 채마다 도구를 하나씩 만들면 **벽 두께·지붕·페이드·값 계산이 채 수만큼 복사된다** —
// 그중 하나만 고치면 나머지가 조용히 뒤처진다. 다른 것은 **그림과 이름뿐**이므로
// 표에 그것만 적는다.
//
// ── 다시 돌려도 되나 ───────────────────────────────────────────────────────
//
// 된다. **루트는 그대로 두고 자식만 다시 짓는다** — 프리팹을 통째로 새로 만들면 루트의
// fileID가 바뀌어 씬에 놓인 인스턴스가 *"Prefab mismatch"*로 오버라이드를 잃는다.
// 자식은 없어져도 되는 쪽이다(평범한 편집이라 그 오버라이드만 조용히 버려진다).
public static class ShellCreator
{
    const string PrefabDir = "Assets/3_Prefab/Building";
    const string DataDir   = "Assets/Resources/Data/Buildings";

    // 벽 재질은 `Wall.prefab`이 쓰는 것을 그대로 쓴다 — 따로 만들면 성벽과 집의 돌이
    // 다른 색이 된다.
    const string WallPrefabPath = PrefabDir + "/Wall.prefab";

    // ── 채마다 다른 것은 이것뿐이다 ─────────────────────────────────────────
    //
    // **그림은 위에서 내려다본 그대로다** — 첫 줄이 가장 뒤(+Z), 왼쪽이 −X.
    // `#`가 벽, 그 밖이 트인 칸. 앞줄의 트인 칸이 문간이다.
    //
    // **가로는 홀수로 둔다** — 문간이 한복판에 오려면 그래야 한다(짝수면 반 칸 치우친다).
    struct Shell
    {
        public string        Id, Name, Desc;
        public int           Sort;
        public float         Height;     // 벽 높이(m)
        public string[]      Mask;
    }

    // **갈래는 전부 `건물`이다**(2026-08-28, 사용자 결정) — 거주용이냐 작업용이냐로 갈라
    // 놓으니 **같은 물건이 두 칸에 흩어졌다.** 어느 칸을 열지 정하려면 그 안에 무엇을
    // 놓을지를 먼저 정해야 하는 꼴이라 순서가 거꾸로다. 짓는 사람에게 이것들은 전부
    // *"들어가는 집"* 하나이고, 무엇에 쓸지는 **안에 무엇을 놓느냐**가 정한다.
    const BuildCategory Cat = BuildCategory.Living;   // 표시명이 `건물`이다

    static readonly Shell[] Shells =
    {
        new Shell
        {
            Id = "cell", Name = "감옥", Sort = 20, Height = 2.4f,
            Desc = "좁고 낮은 방. 우리와 죄수 침대를 들여놓는다 — 낮은 천장이 곧 그 방의 성격이다.",
            Mask = new[]
            {
                "#####",
                "#...#",
                "#...#",
                "#...#",
                "##.##",
            },
        },
        new Shell
        {
            Id = "corridor", Name = "복도", Sort = 21, Height = 2.6f,
            Desc = "양 끝이 트인 한 칸 폭 통로. 떨어져 선 집들을 잇는다.",
            Mask = new[]
            {
                "#.#", "#.#", "#.#", "#.#", "#.#", "#.#", "#.#", "#.#", "#.#",
            },
        },
        new Shell
        {
            Id = "hut", Name = "오두막", Sort = 22, Height = 2.6f,
            Desc = "침대 몇과 작업대가 같이 들어가는 집.",
            Mask = new[]
            {
                "#######",
                "#.....#",
                "#.....#",
                "#.....#",
                "#.....#",
                "###.###",
            },
        },
        new Shell
        {
            // **가로가 짝수라 문이 한복판에 안 온다** — 긴 집은 어차피 짧은 쪽으로 드나드는
            // 물건이라 치우친 문이 오히려 자연스럽다.
            Id = "longhouse", Name = "긴 집", Sort = 23, Height = 2.8f,
            Desc = "좁고 긴 집. 침대를 줄지어 놓기 좋다.",
            Mask = new[]
            {
                "######",
                "#....#", "#....#", "#....#", "#....#", "#....#",
                "#....#", "#....#", "#....#", "#....#", "#....#",
                "##.###",
            },
        },
        new Shell
        {
            Id = "granary", Name = "곳간", Sort = 24, Height = 3.6f,
            Desc = "네모반듯한 저장고. 궤짝을 벽마다 붙여 놓는다.",
            Mask = new[]
            {
                "#########",
                "#.......#", "#.......#", "#.......#",
                "#.......#", "#.......#", "#.......#", "#.......#",
                "###...###",
            },
        },
        new Shell
        {
            // **문을 하나로 합쳤다**(2026-08-28) — 작은 문 둘은 드나드는 길을 나누기는
            // 하지만 **어느 쪽으로 갈지 매번 갈려** 두 줄이 생긴다. 넓은 문 하나면
            // 병목도 없고 *"여기가 입구다"*가 한눈에 읽힌다.
            Id = "workshop", Name = "작업장", Sort = 25, Height = 4.8f,
            Desc = "작업대를 여럿 들여놓는 넓은 집. 앞 한복판이 통째로 문이다.",
            Mask = new[]
            {
                "###########",
                "#.........#", "#.........#", "#.........#",
                "#.........#", "#.........#", "#.........#",
                "####...####",
            },
        },
        new Shell
        {
            // 감옥과 **발자국이 같고 높이만 다르다** — 높이가 표에 있으니 그것만으로
            // 다른 건물이 된다.
            Id = "tower", Name = "망루", Sort = 26, Height = 6.0f,
            Desc = "발자국은 좁은데 높다. 멀리서도 보이라고 세운다.",
            Mask = new[]
            {
                "#####",
                "#...#",
                "#...#",
                "#...#",
                "##.##",
            },
        },
        new Shell
        {
            Id = "hall", Name = "회관", Sort = 27, Height = 6.0f,
            Desc = "가장 큰 껍데기. 넓고 높아 안에 무엇을 놓아도 답답하지 않다.",
            Mask = new[]
            {
                "#############",
                "#...........#", "#...........#", "#...........#", "#...........#",
                "#...........#", "#...........#", "#...........#", "#...........#",
                "#####...#####",
            },
        },
    };

    // **모서리도 같은 두께다.** 모서리만 통짜로 두면 얇은 벽에 붙어 걷던 폰이 거기서
    // **갑자기 튀어나온 면**에 막혀 옆으로 삐져나온다(칸은 이어져 있어서 길찾기 그림에는
    // 안 나온다 — 몸이 걸리는 것이다). 바깥 면이 한 바퀴 내내 평평해야 한다.
    const float WallThick = 0.3f;

    // 뻗어 나가는 축의 길이. 딱 1.0으로 두면 이웃한 벽끼리 면이 겹쳐 z-파이팅이 난다.
    const float WallRun = 0.98f;

    // **문간의 높이**(m). 벽이 아무리 높아도 드나드는 구멍은 사람 키다.
    //
    // 이게 없으면 **문간이 벽 높이만큼 뚫린다** — 회관(6m)은 앞면이 통째로 열린 창고처럼
    // 보이고, 문이라는 것이 읽히지 않는다. 그 위를 막는 것은 **껍데기의 몫이다**(문 조각은
    // 그 집이 몇 미터인지 모른다).
    //
    // 폰의 캡슐이 2m라 그보다 위여야 머리가 안 걸린다.
    const float DoorHeight = 2.1f;

    // 지붕 두께(m). 벽 위에 얹히므로 높이는 그 채의 `Height`부터다.
    //
    // **높이도 표가 든다**(2026-08-28) — 넓은 집에 낮은 천장을 씌우면 눌린 것처럼 보인다.
    // 발자국에서 자동으로 뽑을 수도 있지만, 감옥은 일부러 낮고 망루는 일부러 좁고 높다 —
    // **높이가 그 채의 성격이라** 크기에서 파생시키면 그 저작이 사라진다.
    const float RoofThick = 0.2f;

    // 벽 한 칸당 자재. **값도 마스크에서 나온다** — 크기를 바꿨는데 값이 그대로면
    // 큰 집이 작은 집과 같은 값이 되고, 그건 아무도 안 알려 준다.
    const int CostPerWall = 3;

    [MenuItem("DungeonColony/Building/1. Create Shells (걸어 들어가는 건물)")]
    static void Run()
    {
        var mat = WallMaterial();
        if (mat == null) return;

        Directory.CreateDirectory(PrefabDir);
        Directory.CreateDirectory(DataDir);

        var made = new System.Collections.Generic.List<string>();

        foreach (var shell in Shells)
        {
            int w = shell.Mask[0].Length, h = shell.Mask.Length;

            bool ragged = false;
            foreach (var row in shell.Mask)
                if (row.Length != w) ragged = true;
            if (ragged)
            {
                Debug.LogError($"[껍데기] `{shell.Name}`의 그림에서 줄 길이가 다르다 — {w}칸이어야 한다.");
                continue;
            }

            var prefab = MakePrefab(shell, w, h, mat);
            if (prefab == null) continue;

            var data = MakeData(shell, prefab, w, h);
            if (data == null) continue;

            LinkData(shell, prefab, data);

            made.Add($"{shell.Name} {w}×{h} · 안쪽 {(w - 2) * (h - 2)}칸 · " +
                     $"벽 {WallCount(shell)}칸 · 목재 {WallCount(shell) * CostPerWall}");
        }

        MakeDoor(mat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[껍데기] " + made.Count + "채를 만들었다 — 건설 메뉴에 뜬다.\n  " +
                  string.Join("\n  ", made) +
                  "\n안에 내 폰이 있거나 건설 모드면 **지붕과 카메라 쪽 벽이 흐려진다**(`BuildingRoof`).\n" +
                  "**비탈에는 안 선다** — 발자국이 넓을수록 평탄 검사가 빡빡하므로 " +
                  "먼저 `바닥`으로 기단을 깔고 그 위에 놓을 것.");
    }

    // ── 문 ──────────────────────────────────────────────────────────────────
    //
    // **문은 껍데기가 아니라 부속이다.** 마스크도 지붕도 없고, 하는 일은 *"여기가 입구다"*를
    // 보이게 하는 것 하나다 — 껍데기의 문간 칸에 끼워 넣는다.
    //
    // **길을 막지 않는다.** 막으면 그 집에 못 들어간다 — 문이 문 노릇을 못 하는 것이다.
    // 그래서 `OccupiesTile`이 꺼져 있고, 규약대로 **콜라이더는 트리거**다(`BuildingData`의
    // 그 설명: 안 그러면 지도는 "빈 칸"이라 하는데 몸이 부딪혀 폰이 처박힌다).
    // 레이어도 `Wall`이 아니라 기본이다 — `Wall`에 두면 길찾기의 장애물 검사가 이 칸을
    // 통째로 막는다.
    //
    // **여닫히지 않는다.** 여닫는 문은 *누가 지나갈 수 있나*를 칸마다 세력별로 답해야 해서
    // 점유 축을 한 번 더 갈라야 한다 — 그건 별건이다. 지금은 **문틀**이다.
    static void MakeDoor(Material mat)
    {
        const string path = PrefabDir + "/Door.prefab";
        const string data = DataDir + "/Door.asset";

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject root = existing != null
            ? PrefabUtility.LoadPrefabContents(path)
            : new GameObject("Door");

        if (existing != null)
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.layer = 0;
        if (root.GetComponent<PlacedBuilding>() == null) root.AddComponent<PlacedBuilding>();

        // 문설주 둘과 인방 하나. 문간 칸의 벽과 **같은 두께**로 세워 이음매를 맞춘다.
        const float JambW = 0.1f, LintelH = 0.25f;
        Slab(root, mat, "JambL", new Vector3(-0.5f + JambW * 0.5f, DoorHeight * 0.5f, 0f),
             new Vector3(JambW, DoorHeight, WallThick));
        Slab(root, mat, "JambR", new Vector3( 0.5f - JambW * 0.5f, DoorHeight * 0.5f, 0f),
             new Vector3(JambW, DoorHeight, WallThick));
        Slab(root, mat, "Lintel", new Vector3(0f, DoorHeight + LintelH * 0.5f, 0f),
             new Vector3(WallRun, LintelH, WallThick));

        // **몸이 걸릴 것을 하나도 안 남긴다.** 문설주가 콜라이더를 들고 있으면 남는 폭이
        // 0.76m인데 폰이 0.7m다 — 오늘 문간에서 낀 것이 정확히 그 여유였다.
        foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        // 클릭으로 고르고 헐 수 있어야 하므로 **트리거 하나**는 둔다(`Size − 0.2` 규약).
        var pick = root.AddComponent<BoxCollider>();
        pick.isTrigger = true;
        pick.center    = new Vector3(0f, DoorHeight * 0.5f, 0f);
        pick.size      = new Vector3(0.8f, DoorHeight, 0.8f);

        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        if (existing != null) PrefabUtility.UnloadPrefabContents(root);
        else                  Object.DestroyImmediate(root);
        if (saved == null) { Debug.LogError($"[껍데기] 문 프리팹을 못 만들었다 — `{path}`."); return; }

        var asset = AssetDatabase.LoadAssetAtPath<BuildingData>(data);
        bool fresh = asset == null;
        if (fresh) asset = ScriptableObject.CreateInstance<BuildingData>();

        asset.Id           = "door";
        asset.BuildingName = "문";
        asset.Description  = "집의 문간에 끼우는 문틀. 길을 막지 않는다 — 여기가 입구라는 표시다.";
        asset.Category     = Cat;
        asset.SortOrder    = 19;
        asset.Size         = Vector2Int.one;
        asset.Prefab       = saved;
        asset.UsesBlueprint = true;
        asset.CostItem      = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Data/Items/Res_Wood.asset");
        asset.CostAmount    = 4;
        asset.OccupiesTile  = false;
        asset.BlockMask     = null;

        if (fresh) AssetDatabase.CreateAsset(asset, data);
        EditorUtility.SetDirty(asset);

        var root2 = PrefabUtility.LoadPrefabContents(path);
        var pb    = root2.GetComponent<PlacedBuilding>();
        if (pb != null)
        {
            var so = new SerializedObject(pb);
            so.FindProperty("data").objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root2, path);
        }
        PrefabUtility.UnloadPrefabContents(root2);
    }

    // ── 경로 ────────────────────────────────────────────────────────────────

    static string PrefabPathOf(Shell s) => $"{PrefabDir}/{Cap(s.Id)}.prefab";
    static string DataPathOf(Shell s)   => $"{DataDir}/{Cap(s.Id)}.asset";
    static string Cap(string id)        => char.ToUpper(id[0]) + id.Substring(1);

    // ── 재질 ────────────────────────────────────────────────────────────────

    static Material WallMaterial()
    {
        var wall = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
        if (wall != null)
            foreach (var r in wall.GetComponentsInChildren<MeshRenderer>(true))
                if (r.sharedMaterial != null) return r.sharedMaterial;

        Debug.LogError($"[껍데기] `{WallPrefabPath}`에서 벽 재질을 못 찾았다 — 벽 프리팹이 " +
                       "옮겨졌으면 `ShellCreator.WallPrefabPath`를 고칠 것.");
        return null;
    }

    // ── 프리팹 ──────────────────────────────────────────────────────────────

    static GameObject MakePrefab(Shell shell, int w, int h, Material mat)
    {
        string path = PrefabPathOf(shell);
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        // 이미 있으면 **루트만 살려 두고 자식을 갈아 끼운다** — 위의 fileID 설명 참고.
        GameObject root = existing != null
            ? PrefabUtility.LoadPrefabContents(path)
            : new GameObject(Cap(shell.Id));

        if (existing != null)
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.layer = LayerOf("Wall", root.layer);

        if (root.GetComponent<PlacedBuilding>() == null) root.AddComponent<PlacedBuilding>();

        // **벽을 한 그릇에 담는다** — `BuildingRoof`가 *"이 아래 것들이 벽"*으로 알고
        // 카메라 쪽만 흐린다. 이름으로 찾게 두면 나중에 자식이 늘 때 조용히 섞인다.
        var wallRoot = new GameObject("Walls");
        wallRoot.transform.SetParent(root.transform, worldPositionStays: false);
        wallRoot.layer = root.layer;

        // 벽 한 칸. **칸 중심은 발자국 계산과 같은 식으로 낸다**
        // (`PlacedBuilding.FootprintAt`: 칸 x의 중심은 건물 중심에서 `x + 0.5 - w/2`).
        // 여기서 어긋나면 모델과 막히는 칸이 반 칸씩 밀려 **문간이 벽 안쪽에 생긴다.**
        for (int lz = 0; lz < h; lz++)
        for (int lx = 0; lx < w; lx++)
        {
            if (!Blocks(shell, lx, lz, h)) continue;

            // 이웃이 벽인 축으로만 길게. **문간 옆 벽은 X로 이어지고 Z로는 얇다** —
            // 그래야 문이 얕은 틈이 된다.
            bool runX = Blocks(shell, lx - 1, lz, w, h) || Blocks(shell, lx + 1, lz, w, h);
            bool runZ = Blocks(shell, lx, lz - 1, w, h) || Blocks(shell, lx, lz + 1, w, h);

            var pos = new Vector3(lx + 0.5f - w * 0.5f, shell.Height * 0.5f, lz + 0.5f - h * 0.5f);

            // **방향마다 판을 하나씩 낸다.** 하나로 합쳐 두 축을 다 키우면 모서리가
            // 통짜가 되어 바깥으로 튀어나온다 — 그 턱에 몸이 걸린다.
            if (runX) Slab(wallRoot, mat, $"Wall_{lx}_{lz}_X", pos, new Vector3(WallRun, shell.Height, WallThick));
            if (runZ) Slab(wallRoot, mat, $"Wall_{lx}_{lz}_Z", pos, new Vector3(WallThick, shell.Height, WallRun), 0.01f);
            if (!runX && !runZ)
                Slab(wallRoot, mat, $"Wall_{lx}_{lz}", pos, new Vector3(WallThick, shell.Height, WallThick));
        }

        // 문간 위를 막는다 — **벽이 높을수록 그 위가 크게 비어 있다.**
        //
        // 문간은 마스크의 **가장자리에 뚫린 칸**이다(안쪽 빈 칸과 그것으로 갈린다).
        // 이웃한 벽이 어느 축으로 뻗는지를 보고 같은 두께로 얹어 이음매를 맞춘다.
        for (int lz = 0; lz < h; lz++)
        for (int lx = 0; lx < w; lx++)
        {
            if (Blocks(shell, lx, lz, h)) continue;
            bool onEdge = lx == 0 || lx == w - 1 || lz == 0 || lz == h - 1;
            if (!onEdge) continue;
            if (shell.Height <= DoorHeight + 0.1f) continue;   // 낮은 집은 위가 없다

            // 옆이 벽이면 그 벽과 같은 축으로 뻗는다.
            bool alongX = Blocks(shell, lx - 1, lz, w, h) || Blocks(shell, lx + 1, lz, w, h);

            float lintel = shell.Height - DoorHeight;
            Slab(wallRoot, mat, $"Head_{lx}_{lz}",
                 new Vector3(lx + 0.5f - w * 0.5f, DoorHeight + lintel * 0.5f, lz + 0.5f - h * 0.5f),
                 alongX ? new Vector3(WallRun, lintel, WallThick)
                        : new Vector3(WallThick, lintel, WallRun));
        }

        // 지붕 — **발자국 전체를 덮는다.** 벽 위에 걸쳐야 처마가 생기고, 안쪽만 덮으면
        // 벽과 지붕 사이로 하늘이 보인다.
        var roof = new GameObject("Roof");
        roof.transform.SetParent(root.transform, worldPositionStays: false);
        roof.layer = root.layer;

        Slab(roof, mat, "RoofSlab",
             new Vector3(0f, shell.Height + RoofThick * 0.5f, 0f),
             new Vector3(w + 0.04f, RoofThick, h + 0.04f));

        var fade = root.GetComponent<BuildingRoof>();
        if (fade == null) fade = root.AddComponent<BuildingRoof>();

        // 비공개 필드라 `SerializedObject`로 꽂는다. **안 꽂으면 스스로 꺼진다** —
        // 그러면 지붕이 영영 안 걷혀서 안을 못 본다.
        var fso = new SerializedObject(fade);
        fso.FindProperty("roof").objectReferenceValue  = roof;
        fso.FindProperty("walls").objectReferenceValue = wallRoot;
        fso.ApplyModifiedPropertiesWithoutUndo();

        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);

        if (existing != null) PrefabUtility.UnloadPrefabContents(root);
        else                  Object.DestroyImmediate(root);

        if (saved == null) Debug.LogError($"[껍데기] 프리팹을 못 만들었다 — `{path}`.");
        return saved;
    }

    // 판 한 장. `lift`는 두 판이 겹치는 모서리에서 **윗면이 같은 높이라 깜빡이는 것**을
    // 피하려고 1cm 올리는 값이다(내려다보는 화면에서는 안 보인다).
    static void Slab(GameObject parent, Material mat, string name, Vector3 pos, Vector3 scale,
                     float lift = 0f)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name  = name;
        cube.layer = parent.layer;
        cube.transform.SetParent(parent.transform, worldPositionStays: false);
        cube.transform.localPosition = pos + Vector3.up * lift;
        cube.transform.localScale    = scale;
        cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // 마스크는 첫 줄이 **뒤**(+Z)라 뒤집어 읽는다 — `BuildingData.BlocksLocal`과 같은 식이다.
    static bool Blocks(Shell s, int lx, int lz, int h) => s.Mask[h - 1 - lz][lx] == '#';

    // 범위 밖은 벽이 아니다 — 이웃을 물을 때 쓴다.
    static bool Blocks(Shell s, int lx, int lz, int w, int h) =>
        lx >= 0 && lx < w && lz >= 0 && lz < h && Blocks(s, lx, lz, h);

    static int WallCount(Shell s)
    {
        int n = 0;
        foreach (var row in s.Mask)
            foreach (var c in row)
                if (c == '#') n++;
        return n;
    }

    static int LayerOf(string name, int fallback)
    {
        int i = LayerMask.NameToLayer(name);
        return i >= 0 ? i : fallback;
    }

    // ── 건설 데이터 ─────────────────────────────────────────────────────────

    static BuildingData MakeData(Shell shell, GameObject prefab, int w, int h)
    {
        string path = DataPathOf(shell);

        var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
        bool fresh = data == null;
        if (fresh) data = ScriptableObject.CreateInstance<BuildingData>();

        data.Id           = shell.Id;
        data.BuildingName = shell.Name;
        data.Description  = $"{w}×{h} · 안쪽 {w - 2}×{h - 2}칸. {shell.Desc}";
        data.Category     = Cat;
        data.SortOrder    = shell.Sort;
        data.Size         = new Vector2Int(w, h);
        data.Prefab       = prefab;

        // **값이 붙었으니 청사진이다** — 즉시 배치로 두면 자재가 모자랄 때 클릭이 그냥 씹혀
        // *안 지어지는 것*과 *못 놓는 것*이 구별되지 않는다.
        data.UsesBlueprint = true;
        data.CostItem      = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Data/Items/Res_Wood.asset");
        data.CostAmount    = WallCount(shell) * CostPerWall;

        // **여기가 이 도구의 요점이다** — 벽을 세운 그 그림이 그대로 막는 칸이 된다.
        data.OccupiesTile = true;
        data.BlockMask    = (string[])shell.Mask.Clone();

        if (fresh) AssetDatabase.CreateAsset(data, path);
        EditorUtility.SetDirty(data);

        if (data.CostItem == null)
            Debug.LogWarning($"[껍데기] `{shell.Name}`이 `Res_Wood`를 못 찾아 값이 비었다 — 에셋에서 채울 것.");

        return data;
    }

    // **프리팹의 `PlacedBuilding`이 이 데이터를 가리켜야 한다.** 안 가리키면 배치는 되는데
    // 발자국 등록도 정보 패널도 안 붙는다(`data`는 `[SerializeField]` 비공개다).
    static void LinkData(Shell shell, GameObject prefab, BuildingData data)
    {
        string path = PrefabPathOf(shell);
        var root = PrefabUtility.LoadPrefabContents(path);
        var pb   = root.GetComponent<PlacedBuilding>();
        if (pb != null)
        {
            var so = new SerializedObject(pb);
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        PrefabUtility.UnloadPrefabContents(root);
    }
}
