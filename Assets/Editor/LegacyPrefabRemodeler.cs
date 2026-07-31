using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 맨 큐브 하나로 남아 있던 옛 프리팹들의 모양을 다시 만든다.
//
// **제자리 수정이다.** 새로 저장하면 안 된다 — BuildingData/ResourceNodeData는 프리팹을
// `guid + 루트 GameObject의 fileID`로 물고 있어서, 프리팹을 새로 만들면 GUID가 같아도
// 루트 fileID가 바뀌어 참조가 끊긴다. `LoadPrefabContents`로 열어 내용만 갈고 같은 경로에
// 저장하면 루트와 그 위의 컴포넌트(ResourceNode·CraftingTable·Bed…)가 그대로 살아남는다.
//
// 앞선 생성기들과 달리 **덮어쓴다** — 모양을 새로 만드는 게 목적이기 때문이다.
// 인스펙터에서 손본 모양이 있으면 다시 돌리기 전에 확인할 것.
public static class LegacyPrefabRemodeler
{
    const string MatDir    = "Assets/2_Material";
    const string PrefabDir = "Assets/3_Prefab";

    // ── 색 팔레트 ─────────────────────────────────────────────────────────
    static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

    static readonly (string name, Color color, float metal, float smooth, Color emis)[] Palette =
    {
        ("Mdl_Stone",     C(0x84,0x82,0x7C), 0f,    0.12f, default),
        ("Mdl_StoneDark", C(0x5C,0x5A,0x56), 0f,    0.10f, default),
        ("Mdl_Bark",      C(0x5B,0x40,0x28), 0f,    0.08f, default),
        ("Mdl_Leaf",      C(0x3E,0x7A,0x3A), 0f,    0.15f, default),
        ("Mdl_LeafDeep",  C(0x2E,0x5E,0x2C), 0f,    0.15f, default),
        ("Mdl_Wood",      C(0x8A,0x5F,0x38), 0f,    0.15f, default),
        ("Mdl_WoodDark",  C(0x53,0x39,0x22), 0f,    0.12f, default),
        ("Mdl_Iron",      C(0x55,0x59,0x5F), 0.85f, 0.42f, default),
        ("Mdl_Cloth",     C(0xD8,0xD2,0xC4), 0f,    0.20f, default),
        ("Mdl_Pillow",    C(0xB8,0xC4,0xD0), 0f,    0.25f, default),
        ("Mdl_Soil",      C(0x5A,0x42,0x2C), 0f,    0.05f, default),
        ("Mdl_SoilDark",  C(0x3E,0x2C,0x1D), 0f,    0.05f, default),
        ("Mdl_Water",     C(0x2E,0x6E,0xA8), 0.1f,  0.90f, default),
        ("Mdl_Paper",     C(0xE6,0xDE,0xC6), 0f,    0.10f, default),
        ("Mdl_BookRed",   C(0x8E,0x30,0x30), 0f,    0.15f, default),
        ("Mdl_BookBlue",  C(0x2F,0x4C,0x8E), 0f,    0.15f, default),
        ("Mdl_Ember",     C(0xFF,0x8A,0x22), 0f,    0.30f, C(0xFF,0x6A,0x10)),
    };

    static readonly Dictionary<string, Material> _mats = new();

    [MenuItem("DungeonColony/Remodel Legacy Prefabs")]
    static void Run()
    {
        _mats.Clear();
        foreach (var p in Palette) _mats[p.name] = EnsureMaterial(p.name, p.color, p.metal, p.smooth, p.emis);

        int done = 0;
        done += Remodel("Stone",                BuildStone,      0f, new Vector3(0f, 0.35f, 0f), new Vector3(1f, 0.7f, 1f));
        done += Remodel("Tree",                 BuildTree,       0f, new Vector3(0f, 0.9f, 0f),  new Vector3(0.9f, 1.8f, 0.9f));
        done += Remodel("CraftingTable_Weapon", BuildWeaponBench,0f, new Vector3(0f, 0.45f, 0f), new Vector3(1f, 0.9f, 1f));
        done += Remodel("CraftingTable_Smelt",  BuildSmelter,    0f, new Vector3(0f, 0.6f, 0f),  new Vector3(1f, 1.2f, 1f));
        done += Remodel("CraftingTable_Cook",   BuildCookStove,  0f, new Vector3(0f, 0.4f, 0f),  new Vector3(1f, 0.8f, 1f));
        done += Remodel("ResearchTable",        BuildResearchDesk,0f,new Vector3(0f, 0.45f, 0f), new Vector3(1f, 0.9f, 1f));
        done += Remodel("Bed",                  BuildBed,        0f, new Vector3(0f, 0.3f, 0f),  new Vector3(0.85f, 0.6f, 1f));
        done += Remodel("storage",              BuildCrate,      0f, new Vector3(0f, 0.33f, 0f), new Vector3(1f, 0.66f, 1f));
        done += Remodel("FarmPlot",             BuildFarmPlot,   0f, new Vector3(0f, 0.09f, 0f), new Vector3(1f, 0.18f, 1f));
        done += Remodel("WaterSource",          BuildWell,       0f, new Vector3(0f, 0.65f, 0f), new Vector3(1f, 1.3f, 1f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Remodel] 완료 — 프리팹 {done}개 모양 갱신. " +
                  "Floor(바닥 슬래브)·sub(부양 서브코어)·pawn·plane은 의도된 모양이라 건드리지 않았다.");
    }

    // ── 공통 처리 ─────────────────────────────────────────────────────────
    // rootY: 루트 localPosition.y. **0으로 두는 게 기본** — 양수면 BuildModeManager가
    // "저작 부양 높이"로 보고 그만큼 띄우는데(CLAUDE.md), 조각마다 높이가 달라 맞지 않는다.
    // 0이면 SnapOnSurface가 렌더러 바닥면을 표면에 맞춘다.
    static int Remodel(string file, Action<Transform> build, float rootY,
                       Vector3 colCenter, Vector3 colSize)
    {
        string path = $"{PrefabDir}/{file}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning($"[Remodel] {path} 없음 — 건너뜀");
            return 0;
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // 옛 모양 걷어내기 — 루트에 직접 붙은 메시와 자식 전부.
            // 루트의 컴포넌트(ResourceNode·CraftingTable·Bed…)는 그대로 둔다.
            var mf = root.GetComponent<MeshFilter>();
            if (mf != null) UnityEngine.Object.DestroyImmediate(mf, true);
            var mr = root.GetComponent<MeshRenderer>();
            if (mr != null) UnityEngine.Object.DestroyImmediate(mr, true);

            for (int i = root.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject, true);

            var p = root.transform.localPosition;
            root.transform.localPosition = new Vector3(p.x, rootY, p.z);

            build(root.transform);

            // 자식 레이어를 루트에 맞춘다 (레이캐스트 마스크가 걸린 씬이 있다)
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = root.layer;

            // 클릭·통행 판정용 콜라이더는 **루트에 하나**. 새 모양을 감싸도록 크기만 맞춘다.
            // 종류가 Box가 아니면(우물의 캡슐 등) 손대지 않는다 — 의도된 형상일 수 있다.
            var box = root.GetComponent<BoxCollider>();
            if (box == null)
            {
                var other = root.GetComponent<Collider>();
                if (other == null) box = root.AddComponent<BoxCollider>();
                else Debug.Log($"[Remodel] {file}: {other.GetType().Name}이 있어 콜라이더는 그대로 둔다");
            }
            if (box != null) { box.center = colCenter; box.size = colSize; }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[Remodel] {file} 갱신 (조각 {root.transform.childCount}개)");
            return 1;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ── 모양들 ────────────────────────────────────────────────────────────

    // 바위 덩어리 — 큐브 셋을 어긋나게 겹쳐 깎인 면이 보이게
    static void BuildStone(Transform r)
    {
        Cube(r, "Rock",  "Mdl_Stone",     new Vector3( 0f,    0.25f,  0f),   new Vector3(0.88f, 0.5f,  0.84f), 12f);
        Cube(r, "Rock2", "Mdl_StoneDark", new Vector3( 0.18f, 0.5f,  -0.1f), new Vector3(0.5f,  0.34f, 0.46f), -28f);
        Cube(r, "Rock3", "Mdl_Stone",     new Vector3(-0.24f, 0.36f,  0.2f), new Vector3(0.44f, 0.34f, 0.4f),   40f);
        Cube(r, "Chip",  "Mdl_StoneDark", new Vector3( 0.3f,  0.14f,  0.3f), new Vector3(0.26f, 0.2f,  0.24f),  18f);
    }

    // 나무 — 전엔 머티리얼이 **비어 있어서** 색이 안 나왔다. 수관을 여러 덩이로 쪼개 실루엣을 살린다.
    static void BuildTree(Transform r)
    {
        Cyl (r, "Flare",  "Mdl_Bark",     new Vector3( 0f,    0.08f,  0f),    new Vector3(0.44f, 0.08f, 0.44f));
        Cyl (r, "Trunk",  "Mdl_Bark",     new Vector3( 0f,    0.6f,   0f),    new Vector3(0.26f, 0.6f,  0.26f));
        Sph (r, "Crown",  "Mdl_Leaf",     new Vector3( 0f,    1.42f,  0f),    new Vector3(1.16f, 1.02f, 1.16f));
        Sph (r, "Crown2", "Mdl_LeafDeep", new Vector3( 0.3f,  1.1f,   0.22f), new Vector3(0.72f, 0.64f, 0.72f));
        Sph (r, "Crown3", "Mdl_LeafDeep", new Vector3(-0.28f, 1.18f, -0.2f),  new Vector3(0.66f, 0.6f,  0.66f));
        Sph (r, "Crown4", "Mdl_Leaf",     new Vector3( 0.05f, 1.82f, -0.05f), new Vector3(0.7f,  0.62f, 0.7f));
    }

    // 무기 제작대 — 작업대 + 모루
    static void BuildWeaponBench(Transform r)
    {
        Bench(r);
        Cube(r, "AnvilBase", "Mdl_Iron", new Vector3(0.2f, 0.72f,  0f), new Vector3(0.2f,  0.1f,  0.2f),  0f);
        Cube(r, "AnvilBody", "Mdl_Iron", new Vector3(0.2f, 0.84f,  0f), new Vector3(0.36f, 0.14f, 0.18f), 0f);
        Cube(r, "Hammer",    "Mdl_Wood", new Vector3(-0.2f,0.7f,   0.1f),new Vector3(0.3f, 0.05f, 0.05f), 20f);
    }

    // 용광로 — 굴뚝 + 벌겋게 달아오른 화구
    static void BuildSmelter(Transform r)
    {
        Cube(r, "Body",    "Mdl_StoneDark", new Vector3(0f,    0.4f,  0f),    new Vector3(0.84f, 0.8f,  0.84f), 0f);
        Cube(r, "Rim",     "Mdl_Stone",     new Vector3(0f,    0.83f, 0f),    new Vector3(0.9f,  0.1f,  0.9f),  0f);
        Cube(r, "Chimney", "Mdl_StoneDark", new Vector3(0.22f, 1.06f, 0.22f), new Vector3(0.28f, 0.44f, 0.28f), 0f);
        Cube(r, "Mouth",   "Mdl_Ember",     new Vector3(0f,    0.3f,  0.43f), new Vector3(0.38f, 0.3f,  0.06f), 0f);
    }

    // 조리대 — 화덕 + 냄비
    static void BuildCookStove(Transform r)
    {
        Cube(r, "Hearth", "Mdl_Stone",     new Vector3(0f, 0.26f, 0f),    new Vector3(0.9f,  0.52f, 0.9f),  0f);
        Cube(r, "Fire",   "Mdl_Ember",     new Vector3(0f, 0.26f, 0.44f), new Vector3(0.34f, 0.26f, 0.05f), 0f);
        Cyl (r, "Pot",    "Mdl_Iron",      new Vector3(0f, 0.62f, 0f),    new Vector3(0.44f, 0.12f, 0.44f));
        Cyl (r, "PotRim", "Mdl_StoneDark", new Vector3(0f, 0.75f, 0f),    new Vector3(0.46f, 0.02f, 0.46f));
    }

    // 연구대 — 책상 + 책더미 + 두루마리
    static void BuildResearchDesk(Transform r)
    {
        Bench(r);
        Cube(r, "Book1",  "Mdl_BookRed",  new Vector3(-0.22f, 0.72f,  0.06f), new Vector3(0.28f, 0.08f, 0.2f),  10f);
        Cube(r, "Book2",  "Mdl_BookBlue", new Vector3(-0.2f,  0.79f,  0.03f), new Vector3(0.25f, 0.06f, 0.18f), -14f);
        Cyl (r, "Scroll", "Mdl_Paper",    new Vector3( 0.24f, 0.71f, -0.12f), new Vector3(0.09f, 0.16f, 0.09f), tiltZ: 90f);
    }

    // 침대 — 틀 + 매트리스 + 베개 (머리는 -Z 쪽)
    static void BuildBed(Transform r)
    {
        Cube(r, "Frame",     "Mdl_WoodDark", new Vector3(0f, 0.13f,  0f),    new Vector3(0.72f, 0.26f, 0.96f), 0f);
        Cube(r, "Mattress",  "Mdl_Cloth",    new Vector3(0f, 0.33f,  0.06f), new Vector3(0.66f, 0.16f, 0.82f), 0f);
        Cube(r, "Pillow",    "Mdl_Pillow",   new Vector3(0f, 0.4f,  -0.33f), new Vector3(0.52f, 0.12f, 0.22f), 0f);
        Cube(r, "HeadBoard", "Mdl_Wood",     new Vector3(0f, 0.38f, -0.47f), new Vector3(0.74f, 0.5f,  0.08f), 0f);
    }

    // 창고 — 나무 상자 + 쇠 띠
    static void BuildCrate(Transform r)
    {
        Cube(r, "Box",   "Mdl_Wood",     new Vector3(0f, 0.3f,  0f), new Vector3(0.9f,  0.6f,  0.9f),  0f);
        Cube(r, "Lid",   "Mdl_WoodDark", new Vector3(0f, 0.63f, 0f), new Vector3(0.96f, 0.08f, 0.96f), 0f);
        Cube(r, "BandX", "Mdl_Iron",     new Vector3(0f, 0.3f,  0f), new Vector3(0.94f, 0.5f,  0.1f),  0f);
        Cube(r, "BandZ", "Mdl_Iron",     new Vector3(0f, 0.3f,  0f), new Vector3(0.1f,  0.5f,  0.94f), 0f);
    }

    // 밭 — 흙판 + 이랑 셋
    static void BuildFarmPlot(Transform r)
    {
        Cube(r, "Soil", "Mdl_Soil",     new Vector3(0f, 0.05f, 0f),    new Vector3(0.98f, 0.1f,  0.98f), 0f);
        Cube(r, "Row1", "Mdl_SoilDark", new Vector3(0f, 0.12f, -0.3f), new Vector3(0.92f, 0.06f, 0.18f), 0f);
        Cube(r, "Row2", "Mdl_SoilDark", new Vector3(0f, 0.12f,  0f),   new Vector3(0.92f, 0.06f, 0.18f), 0f);
        Cube(r, "Row3", "Mdl_SoilDark", new Vector3(0f, 0.12f,  0.3f), new Vector3(0.92f, 0.06f, 0.18f), 0f);
    }

    // 우물 — 돌 테두리 + 물 + 지붕
    static void BuildWell(Transform r)
    {
        Cyl (r, "Rim",   "Mdl_Stone",    new Vector3( 0f,    0.24f, 0f), new Vector3(0.88f, 0.24f, 0.88f));
        Cyl (r, "Water", "Mdl_Water",    new Vector3( 0f,    0.46f, 0f), new Vector3(0.7f,  0.02f, 0.7f));
        Cube(r, "Post1", "Mdl_Wood",     new Vector3( 0.33f, 0.78f, 0f), new Vector3(0.1f,  0.62f, 0.1f), 0f);
        Cube(r, "Post2", "Mdl_Wood",     new Vector3(-0.33f, 0.78f, 0f), new Vector3(0.1f,  0.62f, 0.1f), 0f);
        Cube(r, "Roof",  "Mdl_WoodDark", new Vector3( 0f,    1.14f, 0f), new Vector3(0.96f, 0.1f,  0.72f), 0f);
    }

    // 다리 넷 + 상판 — 작업대/연구대 공용
    static void Bench(Transform r)
    {
        Cube(r, "Top", "Mdl_Wood", new Vector3(0f, 0.61f, 0f), new Vector3(0.95f, 0.12f, 0.72f), 0f);
        const float L = 0.36f, Z = 0.26f;
        Cube(r, "Leg1", "Mdl_WoodDark", new Vector3( L, 0.28f,  Z), new Vector3(0.1f, 0.56f, 0.1f), 0f);
        Cube(r, "Leg2", "Mdl_WoodDark", new Vector3(-L, 0.28f,  Z), new Vector3(0.1f, 0.56f, 0.1f), 0f);
        Cube(r, "Leg3", "Mdl_WoodDark", new Vector3( L, 0.28f, -Z), new Vector3(0.1f, 0.56f, 0.1f), 0f);
        Cube(r, "Leg4", "Mdl_WoodDark", new Vector3(-L, 0.28f, -Z), new Vector3(0.1f, 0.56f, 0.1f), 0f);
    }

    // ── 조각 만들기 ───────────────────────────────────────────────────────
    static void Cube(Transform p, string n, string mat, Vector3 pos, Vector3 s, float yaw) =>
        Piece(PrimitiveType.Cube, p, n, mat, pos, s, yaw, 0f);

    static void Cyl(Transform p, string n, string mat, Vector3 pos, Vector3 s, float tiltZ = 0f) =>
        Piece(PrimitiveType.Cylinder, p, n, mat, pos, s, 0f, tiltZ);

    static void Sph(Transform p, string n, string mat, Vector3 pos, Vector3 s) =>
        Piece(PrimitiveType.Sphere, p, n, mat, pos, s, 0f, 0f);

    static void Piece(PrimitiveType type, Transform parent, string name, string mat,
                      Vector3 pos, Vector3 scale, float yaw, float tiltZ)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        // 조각 콜라이더는 지운다 — 루트 콜라이더 하나로 통일(SelectionManager의 단일 레이캐스트).
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>(), true);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, tiltZ);
        go.transform.localScale    = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = _mats[mat];
    }

    // ── 머티리얼 ──────────────────────────────────────────────────────────
    static Material EnsureMaterial(string name, Color color, float metal, float smooth, Color emission)
    {
        string path = $"{MatDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = name };
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", metal);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smooth);

        if (emission != default && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emission * 1.6f);
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
