using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// **마을 말고 들를 곳 다섯 — 장소 프리팹을 세운다**(2026-10-28, 사용자: *"마을을 제외하고 다양한 탐험 컨텐츠 추가 필요
// ex) 거미 서식지, 고블린 부락, 신전, npc 거주지(텐트 그런거 혼자서), 파괴된 마을"*).
//
// `Site_Lair`와 같은 물건이다 — 기본 도형으로 지은 자리에 **사는 것(폰 프리팹)을 그대로 넣어 둔 프리팹**. 장소가 청크와
// 함께 서면 사는 것도 같이 선다(`WorldChunkContent`). 재질은 소굴이 쓰는 것(`Site_*.mat`)을 같이 쓰고 거미줄 하나만 더한다.
//
// **어디에 몇 개 놓나는 사용자가 정한다** — `Authoring → Show Terrain Here`로 땅을 띄우고 이 프리팹을 놓은 뒤
// `Save Selection As Site` → `World → 5. Bake World`. 지도에 소굴로 뜨게 하려면 놓은 것의 `WorldObjectTag.Kind`를
// `적 소굴`로(거미 둥지 · 고블린 부락).
//
// **장식에는 콜라이더를 안 단다** — 길찾기는 칸으로 굽는다. 모양만 있는 벽이 몸을 막으면 *"길은 있다는데 못 간다"*가 된다.
// 이미 있으면 **안의 것을 갈고 루트는 지킨다**(저작 씬의 인스턴스가 guid로 가리킨다).
public static class ExplorationSiteCreator
{
    const string SiteDir = "Assets/3_Prefab/Site";
    const string PawnDir = "Assets/3_Prefab/Pawn";

    static Material _post, _tent, _fire, _wall, _roof, _web;

    [MenuItem("DungeonColony/World/L. Create Exploration Sites (거미 둥지 · 고블린 부락 · 신전 · 은둔자 · 폐허)")]
    public static void Create()
    {
        _post = Mat("Site_Post"); _tent = Mat("Site_Tent"); _fire = Mat("Site_Fire");
        _wall = Mat("Site_Wall"); _roof = Mat("Site_Roof");
        _web  = WebMaterial();

        var made = new List<string>();
        Build("Site_SpiderNest",    SpiderNest,    made);
        Build("Site_GoblinCamp",    GoblinCamp,    made);
        Build("Site_Shrine",        Shrine,        made);
        Build("Site_Hermit",        Hermit,        made);
        Build("Site_RuinedVillage", RuinedVillage, made);

        AssetDatabase.SaveAssets();
        Debug.Log($"[탐험 장소] {string.Join(" · ", made)} — `{SiteDir}`. 놓는 자리는 저작할 것(`Authoring → Show Terrain Here`). " +
                  "**거미 둥지의 거미는 개미가 대신 선다** — 거미 몸(모델)이 아직 없다.");
    }

    // ── 다섯 장소 ───────────────────────────────────────────────────────────

    // 거미줄 막 · 알주머니 · 뼈 무더기. 사는 것은 개미(거미 몸이 생기면 바꿀 것).
    static void SpiderNest(Transform r)
    {
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.PI * 2f / 7f;
            var p = new Vector3(Mathf.Cos(a) * 7f, 1.6f, Mathf.Sin(a) * 7f);
            // 얇은 상자 — 판(Quad)은 한쪽 면만 그려져 뒤에서 보면 사라진다.
            Piece(r, "Web", PrimitiveType.Cube, p, Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, Hash(i) * 30f - 15f),
                  new Vector3(4f, 3.2f, 0.02f), _web);
        }
        for (int i = 0; i < 9; i++)
        {
            var p = Scatter(i, 4.5f);
            float s = 0.5f + Hash(i + 40) * 0.5f;
            Piece(r, "EggSac", PrimitiveType.Sphere, p + Vector3.up * s * 0.5f, Quaternion.identity, new Vector3(s, s * 1.3f, s), _web);
        }
        for (int i = 0; i < 6; i++)
            Piece(r, "Bones", PrimitiveType.Capsule, Scatter(i + 20, 5.5f) + Vector3.up * 0.1f,
                  Quaternion.Euler(90f, Hash(i + 60) * 180f, 0f), new Vector3(0.15f, 0.5f, 0.15f), _tent);

        Pawn(r, "Enemy_Ant", new Vector3(2f, 0f, 1f));
        Pawn(r, "Enemy_Ant", new Vector3(-2f, 0f, 2f));
        Pawn(r, "Enemy_Ant", new Vector3(0f, 0f, -3f));
    }

    // 천막 셋 · 화덕 · 끝을 깎은 말뚝 울타리. 고블린 다섯.
    static void GoblinCamp(Transform r)
    {
        Tent(r, new Vector3(-5f, 0f, 3f), 20f);
        Tent(r, new Vector3(4f, 0f, 4f), -35f);
        Tent(r, new Vector3(0f, 0f, -5f), 180f);
        Firepit(r, Vector3.zero);

        for (int i = 0; i < 22; i++)
        {
            float a = i * Mathf.PI * 2f / 22f;
            if (i == 0 || i == 1) continue;   // 드나드는 틈
            var p = new Vector3(Mathf.Cos(a) * 10f, 0.9f, Mathf.Sin(a) * 10f);
            Piece(r, "Stake", PrimitiveType.Cylinder, p, Quaternion.Euler(Hash(i) * 14f - 7f, 0f, Hash(i + 9) * 14f - 7f),
                  new Vector3(0.25f, 0.9f + Hash(i + 3) * 0.3f, 0.25f), _post);
        }

        Pawn(r, "Enemy_Goblin",       new Vector3(2f, 0f, 1.5f));
        Pawn(r, "Enemy_Goblin",       new Vector3(-2f, 0f, -1f));
        Pawn(r, "Enemy_GoblinSpear",  new Vector3(1f, 0f, -2.5f));
        Pawn(r, "Enemy_GoblinSpear",  new Vector3(7f, 0f, 1f));
        Pawn(r, "Enemy_GoblinArcher", new Vector3(-6f, 0f, -2f));
    }

    // 단 · 둘레 기둥 · 제단. 사교도 셋이 지킨다.
    static void Shrine(Transform r)
    {
        Piece(r, "Dais", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), Quaternion.identity, new Vector3(12f, 0.3f, 12f), _wall);
        Piece(r, "Step", PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f), Quaternion.identity, new Vector3(7f, 0.1f, 7f), _wall);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            bool broken = Hash(i + 77) < 0.3f;
            float h = broken ? 1.2f + Hash(i) : 4f;
            Piece(r, "Pillar", PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * 5f, 0.3f + h * 0.5f, Mathf.Sin(a) * 5f),
                  Quaternion.identity, new Vector3(0.7f, h * 0.5f, 0.7f), _wall);
        }
        Piece(r, "Altar", PrimitiveType.Cube, new Vector3(0f, 0.9f, 0f), Quaternion.identity, new Vector3(1.8f, 1f, 1f), _roof);
        Piece(r, "Brazier", PrimitiveType.Cylinder, new Vector3(1.4f, 0.8f, 0f), Quaternion.identity, new Vector3(0.4f, 0.4f, 0.4f), _fire);
        Piece(r, "Brazier", PrimitiveType.Cylinder, new Vector3(-1.4f, 0.8f, 0f), Quaternion.identity, new Vector3(0.4f, 0.4f, 0.4f), _fire);

        Pawn(r, "Enemy_CultMagus",   new Vector3(0f, 0.4f, -1.8f));
        Pawn(r, "Enemy_CultAcolyte", new Vector3(2.5f, 0.4f, 2f));
        Pawn(r, "Enemy_CultAcolyte", new Vector3(-2.5f, 0.4f, 2f));
    }

    // 혼자 사는 떠돌이 — 천막 하나 · 화덕 · 짐 상자.
    static void Hermit(Transform r)
    {
        Tent(r, new Vector3(0f, 0f, 2f), 0f);
        Firepit(r, new Vector3(0f, 0f, -1.5f));
        Piece(r, "Crate", PrimitiveType.Cube, new Vector3(2f, 0.35f, 1.5f), Quaternion.Euler(0f, 20f, 0f), new Vector3(0.7f, 0.7f, 0.7f), _post);
        Piece(r, "Log",   PrimitiveType.Cylinder, new Vector3(-1.6f, 0.25f, -1.5f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.4f, 0.8f, 0.4f), _post);

        Pawn(r, "Npc_Drifter", new Vector3(1f, 0f, -1f));
    }

    // 무너진 집 넷 — 반쯤 남은 벽 · 내려앉은 지붕 · 돌무더기. 늑대가 산다.
    static void RuinedVillage(Transform r)
    {
        var houses = new[] { new Vector3(-7f, 0f, 6f), new Vector3(6f, 0f, 7f), new Vector3(-6f, 0f, -6f), new Vector3(7f, 0f, -5f) };
        for (int h = 0; h < houses.Length; h++)
        {
            var c = houses[h];
            float yaw = Hash(h + 5) * 90f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            // 네 벽 중 둘은 무너져 낮다.
            for (int w = 0; w < 4; w++)
            {
                float side = w % 2 == 0 ? 5f : 4f;
                float height = Hash(h * 4 + w) < 0.5f ? 0.6f + Hash(h * 4 + w + 50) * 0.8f : 2.4f;
                var off = w switch { 0 => new Vector3(0f, 0f, 2f), 1 => new Vector3(2.5f, 0f, 0f), 2 => new Vector3(0f, 0f, -2f), _ => new Vector3(-2.5f, 0f, 0f) };
                var size = w % 2 == 0 ? new Vector3(side, height, 0.3f) : new Vector3(0.3f, height, side);
                Piece(r, "Wall", PrimitiveType.Cube, c + rot * (off + Vector3.up * height * 0.5f), rot, size, _wall);
            }
            Piece(r, "FallenRoof", PrimitiveType.Cube, c + rot * new Vector3(0.6f, 0.5f, -0.3f),
                  rot * Quaternion.Euler(0f, 0f, 24f), new Vector3(3.6f, 0.15f, 3.2f), _roof);
            for (int k = 0; k < 4; k++)
            {
                var p = c + rot * new Vector3(Hash(h * 9 + k) * 4f - 2f, 0.2f, Hash(h * 9 + k + 30) * 3f - 1.5f);
                float s = 0.4f + Hash(h * 9 + k + 60) * 0.5f;
                Piece(r, "Rubble", PrimitiveType.Cube, p, Quaternion.Euler(Hash(k) * 40f, Hash(k + 1) * 90f, 0f), Vector3.one * s, _wall);
            }
        }
        Firepit(r, new Vector3(0f, 0f, 0.5f), cold: true);

        Pawn(r, "Enemy_Wolf", new Vector3(1.5f, 0f, -1f));
        Pawn(r, "Enemy_Wolf", new Vector3(-1f, 0f, 2f));
    }

    // ── 조각 ────────────────────────────────────────────────────────────────

    static void Tent(Transform r, Vector3 at, float yaw)
    {
        var rot = Quaternion.Euler(0f, yaw, 0f);
        // 두 장을 기대어 세운 맞배 — 앞뒤는 트여 있다.
        Piece(r, "TentL", PrimitiveType.Cube, at + rot * new Vector3(-0.8f, 0.9f, 0f), rot * Quaternion.Euler(0f, 0f, -48f),
              new Vector3(2.4f, 0.06f, 3f), _tent);
        Piece(r, "TentR", PrimitiveType.Cube, at + rot * new Vector3(0.8f, 0.9f, 0f), rot * Quaternion.Euler(0f, 0f, 48f),
              new Vector3(2.4f, 0.06f, 3f), _tent);
        Piece(r, "TentPole", PrimitiveType.Cylinder, at + Vector3.up * 0.9f, Quaternion.identity, new Vector3(0.08f, 0.9f, 0.08f), _post);
    }

    static void Firepit(Transform r, Vector3 at, bool cold = false)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            Piece(r, "Stone", PrimitiveType.Sphere, at + new Vector3(Mathf.Cos(a) * 0.7f, 0.1f, Mathf.Sin(a) * 0.7f),
                  Quaternion.identity, new Vector3(0.35f, 0.25f, 0.35f), _wall);
        }
        Piece(r, cold ? "Ash" : "Fire", PrimitiveType.Cylinder, at + Vector3.up * 0.05f, Quaternion.identity,
              new Vector3(0.9f, 0.05f, 0.9f), cold ? _wall : _fire);
    }

    static void Piece(Transform r, string name, PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(r, false);
        go.transform.SetLocalPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void Pawn(Transform r, string prefab, Vector3 pos)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{PawnDir}/{prefab}.prefab");
        if (src == null) { Debug.LogWarning($"[탐험 장소] 폰 프리팹이 없다 — {prefab}"); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, r);
        go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, Hash(pos.GetHashCode()) * 360f, 0f));
    }

    // 자리마다 같은 값 — 도구를 다시 돌려도 장소 모양이 안 바뀐다.
    static float Hash(int i)
    {
        uint x = (uint)i * 0x9E3779B9u;
        x ^= x >> 16; x *= 0x7FEB352Du; x ^= x >> 15;
        return (x & 0xFFFFFF) / (float)0x1000000;
    }

    static Vector3 Scatter(int i, float radius)
    {
        float a = Hash(i * 2 + 1) * Mathf.PI * 2f;
        float d = Mathf.Sqrt(Hash(i * 2 + 2)) * radius;
        return new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
    }

    // ── 짓기 ────────────────────────────────────────────────────────────────

    static void Build(string name, System.Action<Transform> fill, List<string> made)
    {
        string path = $"{SiteDir}/{name}.prefab";
        GameObject root;
        bool exists = File.Exists(path);
        if (exists)
        {
            root = PrefabUtility.LoadPrefabContents(path);
            for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        }
        else root = new GameObject(name);

        try
        {
            fill(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            made.Add(name);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    static Material Mat(string name)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>($"{SiteDir}/{name}.mat");
        if (m == null) Debug.LogWarning($"[탐험 장소] 재질이 없다 — {name}");
        return m;
    }

    // 거미줄 — 소굴 재질을 베껴 희게. 한 번 만들면 값만 다시 적는다.
    static Material WebMaterial()
    {
        string path = $"{SiteDir}/Site_Web.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            if (_tent == null) return null;
            m = new Material(_tent) { name = "Site_Web" };
            AssetDatabase.CreateAsset(m, path);
        }
        var c = new Color(0.86f, 0.86f, 0.82f, 1f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        EditorUtility.SetDirty(m);
        return m;
    }
}
