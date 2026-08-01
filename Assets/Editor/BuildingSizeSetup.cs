using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 건물의 **차지 칸(`BuildingData.Size`)을 정하고 모델을 그 크기로 다시 짓는다.**
//
// 왜 다시 짓나: 기존 모델은 1×1에 맞춰 생성기가 만든 것이고, 조각들이 저마다 회전을 갖고 있다.
// 부모를 비균등 스케일하면 회전된 조각이 **찌그러진다**. 늘리는 대신 같은 방식(파라미터로 조립)
// 으로 새 크기에 맞게 다시 만드는 편이 깨끗하다.
//
// 규칙:
//   · **제자리 수정** — 같은 경로에 저장해 루트 fileID를 지킨다(`BuildingData.Prefab` 참조 유지).
//   · **`WorkSpot`은 지우지 않는다** — 저작물이다. 새 크기의 앞 가장자리로 옮기기만 한다.
//   · 루트의 컴포넌트(콜라이더·`CraftingTable` 등)는 건드리지 않는다. **시각 조각만** 갈아 낀다.
public static class BuildingSizeSetup
{
    // 프리팹 이름 → (차지 칸, 겉모습 종류)
    // 크기는 "이 정도면 그럴듯하다" 수준의 저작값이다 — 인스펙터에서 바꿔도 된다.
    struct Spec
    {
        public Vector2Int Size;
        public Kind       Look;
        public Spec(int w, int h, Kind look) { Size = new Vector2Int(w, h); Look = look; }
    }

    enum Kind { Bench, Furnace, Research, Bed, Storage, Vein, Crystal }

    static readonly Dictionary<string, Spec> Specs = new()
    {
        { "CraftingTable_Smelt",    new Spec(2, 2, Kind.Furnace)  },
        { "CraftingTable_Weapon",   new Spec(2, 1, Kind.Bench)    },
        { "CraftingTable_Armor",    new Spec(2, 1, Kind.Bench)    },
        { "CraftingTable_Stonecut", new Spec(2, 1, Kind.Bench)    },
        { "CraftingTable_Woodwork", new Spec(2, 1, Kind.Bench)    },
        { "CraftingTable_Cook",     new Spec(2, 1, Kind.Bench)    },
        { "CraftingTable_Alchemy",  new Spec(2, 1, Kind.Bench)    },
        { "CraftingTable_Gemcut",   new Spec(2, 1, Kind.Bench)    },
        { "ResearchTable",          new Spec(3, 2, Kind.Research) },
        { "Bed",                    new Spec(1, 2, Kind.Bed)      },
        { "storage",                new Spec(2, 2, Kind.Storage)  },

        // 광맥은 덩치가 있어야 "캐는 자리"로 읽힌다. 결정류는 뾰족해서 폭이 좁아도 눈에 띈다.
        { "Node_CoalSeam",     new Spec(2, 2, Kind.Vein)    },
        { "Node_CopperVein",   new Spec(2, 2, Kind.Vein)    },
        { "Node_TinVein",      new Spec(2, 2, Kind.Vein)    },
        { "Node_IronVein",     new Spec(2, 2, Kind.Vein)    },
        { "Node_SilverVein",   new Spec(2, 2, Kind.Vein)    },
        { "Node_GoldVein",     new Spec(2, 2, Kind.Vein)    },
        { "Node_RubyVein",     new Spec(2, 1, Kind.Crystal) },
        { "Node_MagicCrystal", new Spec(2, 1, Kind.Crystal) },
        { "Node_EtherCrystal", new Spec(2, 1, Kind.Crystal) },
        { "Node_VoidCrystal",  new Spec(2, 1, Kind.Crystal) },
    };

    // 종류별 색. 작업대가 여덟 개라 **색이 유일한 구분 수단**이다.
    static readonly Dictionary<string, Color> Accents = new()
    {
        { "CraftingTable_Smelt",    new Color(0.75f, 0.35f, 0.20f) },
        { "CraftingTable_Weapon",   new Color(0.55f, 0.55f, 0.62f) },
        { "CraftingTable_Armor",    new Color(0.45f, 0.52f, 0.62f) },
        { "CraftingTable_Stonecut", new Color(0.58f, 0.56f, 0.52f) },
        { "CraftingTable_Woodwork", new Color(0.55f, 0.40f, 0.24f) },
        { "CraftingTable_Cook",     new Color(0.72f, 0.60f, 0.35f) },
        { "CraftingTable_Alchemy",  new Color(0.45f, 0.30f, 0.60f) },
        { "CraftingTable_Gemcut",   new Color(0.30f, 0.60f, 0.58f) },
        { "ResearchTable",          new Color(0.35f, 0.45f, 0.70f) },
        { "Bed",                    new Color(0.60f, 0.35f, 0.35f) },
        { "storage",                new Color(0.50f, 0.44f, 0.34f) },

        // 광맥 색 = 그 자원의 색. 이름을 못 읽어도 무엇이 나오는지 보이게.
        { "Node_CoalSeam",     new Color(0.14f, 0.14f, 0.16f) },
        { "Node_CopperVein",   new Color(0.80f, 0.45f, 0.22f) },
        { "Node_TinVein",      new Color(0.72f, 0.74f, 0.78f) },
        { "Node_IronVein",     new Color(0.52f, 0.54f, 0.58f) },
        { "Node_SilverVein",   new Color(0.85f, 0.87f, 0.90f) },
        { "Node_GoldVein",     new Color(0.92f, 0.76f, 0.25f) },
        { "Node_RubyVein",     new Color(0.80f, 0.16f, 0.24f) },
        { "Node_MagicCrystal", new Color(0.45f, 0.35f, 0.85f) },
        { "Node_EtherCrystal", new Color(0.95f, 0.92f, 0.70f) },
        { "Node_VoidCrystal",  new Color(0.28f, 0.16f, 0.38f) },
    };

    static readonly Color WoodColor = new Color(0.40f, 0.29f, 0.20f);

    const string PrefabDir = "Assets/3_Prefab";
    const string MatDir    = "Assets/2_Material";

    [MenuItem("DungeonColony/Resize Buildings And Rebuild Models")]
    static void Run()
    {
        int done = 0;

        foreach (var pair in Specs)
        {
            string path = $"{PrefabDir}/{pair.Key}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"[BuildingSizeSetup] 프리팹 없음: {path}");
                continue;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Rebuild(root, pair.Key, pair.Value);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                done++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            SetDataSize(pair.Key, pair.Value.Size);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[BuildingSizeSetup] {done}개 건물의 크기·모델을 다시 지었습니다. " +
                  "크기가 마음에 안 들면 BuildingData.Size를 바꾸고 다시 실행하세요.");
    }

    // ── BuildingData.Size ────────────────────────────────────────────────────
    static void SetDataSize(string prefabName, Vector2Int size)
    {
        // 건물 데이터는 레지스트리 폴더에 있다(`Resources/Data/Buildings/`).
        string path = $"Assets/Resources/Data/Buildings/{prefabName}.asset";
        var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);

        // 파일명이 프리팹과 다를 수 있으니 못 찾으면 프리팹 참조로 되짚는다.
        if (data == null)
        {
            foreach (var b in DataAssets())
                if (b.Prefab != null && b.Prefab.name == prefabName) { data = b; break; }
        }
        if (data == null)
        {
            Debug.LogWarning($"[BuildingSizeSetup] {prefabName}의 BuildingData를 못 찾았습니다 — 크기 미설정.");
            return;
        }

        data.Size = size;
        EditorUtility.SetDirty(data);
    }

    static IEnumerable<BuildingData> DataAssets()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:BuildingData"))
        {
            var d = AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(guid));
            if (d != null) yield return d;
        }
    }

    // ── 모델 ─────────────────────────────────────────────────────────────────
    static void Rebuild(GameObject root, string name, Spec spec)
    {
        // 시각 조각만 지운다. `WorkSpot`(저작물)과 컴포넌트만 있는 자식은 남긴다.
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var child = root.transform.GetChild(i).gameObject;
            if (child.GetComponentInChildren<WorkSpot>(true) != null) continue;
            if (child.GetComponentInChildren<MeshFilter>(true) == null) continue;
            Object.DestroyImmediate(child);
        }

        // **루트 스케일을 1로 되돌린다.** 옛 프리팹은 루트에 스케일이 박혀 있었다
        // (`Bed` = (1, 0.5, 2), `storage` = (1, 2, 1)). 이제 모델을 칸 크기에 맞춰 직접 짓는데
        // 루트가 또 곱하면 **보이는 크기가 칸의 두 배**가 된다 — 침대가 4칸 길이로 보이던 원인.
        root.transform.localScale = Vector3.one;

        float w = spec.Size.x;   // 타일 = 1 월드 단위 (`MapManager.TileToWorld`)
        float d = spec.Size.y;
        var accent = Accents.TryGetValue(name, out var c) ? c : Color.gray;

        switch (spec.Look)
        {
            case Kind.Furnace:  BuildFurnace(root, w, d, accent);  break;
            case Kind.Research: BuildResearch(root, w, d, accent); break;
            case Kind.Bed:      BuildBed(root, w, d, accent);      break;
            case Kind.Storage:  BuildStorage(root, w, d, accent);  break;
            case Kind.Vein:     BuildVein(root, w, d, accent);     break;
            case Kind.Crystal:  BuildCrystal(root, w, d, accent);  break;
            default:            BuildBench(root, w, d, accent);    break;
        }

        // 광맥은 차지한 칸을 스스로 막는다 — 컴포넌트에도 같은 크기를 넣어 준다.
        var node = root.GetComponent<ResourceNode>();
        if (node != null)
        {
            var nodeSO = new SerializedObject(node);
            nodeSO.FindProperty("size").vector2IntValue = spec.Size;
            nodeSO.ApplyModifiedProperties();
        }

        // 작업 자리는 **새 크기의 앞 가장자리 바깥 한 칸**으로.
        var spot = root.GetComponentInChildren<WorkSpot>(true);
        if (spot != null)
            spot.transform.localPosition = new Vector3(0f, 0f, -(d * 0.5f + 0.5f));
    }

    // 작업대: 상판 + 다리 넷. 크기에 맞춰 상판이 넓어지고 다리가 모서리로 간다.
    static void BuildBench(GameObject root, float w, float d, Color accent)
    {
        Box(root, "Top", new Vector3(0f, 0.78f, 0f), new Vector3(w * 0.92f, 0.12f, d * 0.92f), accent);

        float lx = w * 0.5f - 0.16f;
        float lz = d * 0.5f - 0.16f;
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            Box(root, "Leg", new Vector3(sx * lx, 0.36f, sz * lz),
                new Vector3(0.12f, 0.72f, 0.12f), WoodColor);
    }

    // 용광로: 아래로 갈수록 넓은 **석조 화덕**. 층을 지어 올리면 상자 하나보다 훨씬 화덕처럼 읽힌다.
    //   받침(넓고 낮음) → 몸통 → 어깨(좁아짐) → 굴뚝 → 갓
    // 정면에 **불 아궁이**를 파고 그 안에 밝은 조각을 넣어 어디가 앞인지 한눈에 보이게 한다.
    static void BuildFurnace(GameObject root, float w, float d, Color accent)
    {
        var stone = new Color(0.38f, 0.36f, 0.34f);
        var dark  = accent * 0.55f;
        var fire  = new Color(1f, 0.55f, 0.15f);

        Box(root, "Base",     new Vector3(0f, 0.10f, 0f),
            new Vector3(w * 0.96f, 0.20f, d * 0.96f), stone);
        Box(root, "Body",     new Vector3(0f, 0.55f, 0f),
            new Vector3(w * 0.80f, 0.70f, d * 0.80f), accent);
        Box(root, "Shoulder", new Vector3(0f, 1.02f, 0f),
            new Vector3(w * 0.60f, 0.24f, d * 0.60f), dark);

        // 아궁이 — 앞면(-Z)에 살짝 파묻어 그림자를 만들고, 그 안에 불을 넣는다.
        Box(root, "Hearth", new Vector3(0f, 0.42f, -(d * 0.40f)),
            new Vector3(w * 0.42f, 0.40f, 0.10f), new Color(0.12f, 0.10f, 0.10f));
        Box(root, "Fire",   new Vector3(0f, 0.38f, -(d * 0.43f)),
            new Vector3(w * 0.30f, 0.24f, 0.06f), fire);

        // 굴뚝은 **뒤쪽 한켠**에 세운다 — 가운데 세우면 아궁이와 겹쳐 보여 앞뒤가 안 읽힌다.
        Box(root, "Chimney", new Vector3(w * 0.24f, 1.55f, d * 0.24f),
            new Vector3(0.24f, 0.90f, 0.24f), stone);
        Box(root, "Cap",     new Vector3(w * 0.24f, 2.02f, d * 0.24f),
            new Vector3(0.34f, 0.10f, 0.34f), dark);

        // 옆구리 풀무 — 실루엣에 요철을 줘서 상자처럼 안 보이게.
        Box(root, "Bellows", new Vector3(-(w * 0.46f), 0.45f, d * 0.12f),
            new Vector3(0.14f, 0.30f, d * 0.34f), WoodColor);
    }

    // 연구대: 넓은 책상 + 뒤판. 폭이 넓어졌으니 **가로로 벌려** 채운다 —
    // 가운데에 몰아 두면 넓은 칸이 텅 비어 보인다.
    static void BuildResearch(GameObject root, float w, float d, Color accent)
    {
        BuildBench(root, w, d, WoodColor);

        Box(root, "Backboard", new Vector3(0f, 1.08f, d * 0.42f),
            new Vector3(w * 0.9f, 0.48f, 0.08f), accent);
        Box(root, "Screen",    new Vector3(-w * 0.22f, 1.12f, d * 0.36f),
            new Vector3(w * 0.28f, 0.30f, 0.04f), Color.Lerp(accent, Color.white, 0.5f));

        Box(root, "Books",     new Vector3(w * 0.30f, 0.94f, -d * 0.16f),
            new Vector3(0.30f, 0.20f, 0.36f), accent * 1.2f);
        Box(root, "Apparatus", new Vector3(-w * 0.32f, 0.96f, -d * 0.18f),
            new Vector3(0.18f, 0.24f, 0.18f), new Color(0.55f, 0.70f, 0.65f));
    }

    static void BuildBed(GameObject root, float w, float d, Color accent)
    {
        Box(root, "Frame",  new Vector3(0f, 0.22f, 0f),
            new Vector3(w * 0.9f, 0.2f, d * 0.94f), WoodColor);
        Box(root, "Mattress", new Vector3(0f, 0.38f, 0f),
            new Vector3(w * 0.84f, 0.14f, d * 0.86f), accent);
        Box(root, "Pillow", new Vector3(0f, 0.48f, -(d * 0.34f)),
            new Vector3(w * 0.6f, 0.12f, d * 0.16f), Color.Lerp(accent, Color.white, 0.55f));
    }

    static void BuildStorage(GameObject root, float w, float d, Color accent)
    {
        Box(root, "Crate", new Vector3(0f, 0.3f, 0f),
            new Vector3(w * 0.9f, 0.6f, d * 0.9f), accent);
        Box(root, "Lid",   new Vector3(0f, 0.63f, 0f),
            new Vector3(w * 0.94f, 0.08f, d * 0.94f), WoodColor);
    }

    // 광맥: 바위 더미에 광석이 박혀 있다.
    // 덩어리를 **높이를 달리해 어긋나게** 쌓아야 바위처럼 보인다 — 같은 크기로 늘어놓으면 상자 더미다.
    static void BuildVein(GameObject root, float w, float d, Color accent)
    {
        var rock = new Color(0.34f, 0.33f, 0.31f);

        Box(root, "Rubble", new Vector3(0f, 0.12f, 0f),
            new Vector3(w * 0.94f, 0.24f, d * 0.94f), rock * 0.9f);

        // 덩어리 넷 — 위치·높이를 서로 다르게. 45° 돌려 각진 실루엣을 만든다.
        AddBoulder(root, new Vector3(-w * 0.20f, 0.42f,  d * 0.16f), w * 0.46f, 0.60f, rock,        20f);
        AddBoulder(root, new Vector3( w * 0.22f, 0.34f, -d * 0.18f), w * 0.40f, 0.46f, rock * 1.1f, -35f);
        AddBoulder(root, new Vector3( w * 0.14f, 0.52f,  d * 0.24f), w * 0.30f, 0.36f, rock * 0.8f,  50f);

        // 광석 — 바위 사이사이에 박힌 작은 조각. 이게 자원의 색이다.
        AddBoulder(root, new Vector3(-w * 0.26f, 0.56f, -d * 0.10f), w * 0.22f, 0.26f, accent,       15f);
        AddBoulder(root, new Vector3( w * 0.06f, 0.60f, -d * 0.02f), w * 0.18f, 0.22f, accent * 1.1f, -25f);
    }

    // 결정: 바닥 무더기 + 뾰족하게 솟은 기둥들. 기울여 세워야 결정처럼 보인다.
    static void BuildCrystal(GameObject root, float w, float d, Color accent)
    {
        var rock = new Color(0.30f, 0.29f, 0.30f);

        Box(root, "Base", new Vector3(0f, 0.10f, 0f),
            new Vector3(w * 0.86f, 0.20f, d * 0.86f), rock);

        // 결정은 **크게** — 작으면 바닥 무더기에 파묻혀 뭐가 나는 곳인지 안 보인다.
        AddShard(root, new Vector3(-w * 0.18f, 0.85f,  0f),        0.38f, 1.55f, accent,        8f);
        AddShard(root, new Vector3( w * 0.18f, 0.64f,  d * 0.16f), 0.30f, 1.15f, accent * 1.15f, -14f);
        AddShard(root, new Vector3( w * 0.02f, 0.50f, -d * 0.20f), 0.24f, 0.88f, accent * 0.85f, 22f);
    }

    static void AddBoulder(GameObject root, Vector3 pos, float width, float height, Color color, float yaw)
    {
        var go = Box(root, "Rock", pos, new Vector3(width, height, width), color);
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    static void AddShard(GameObject root, Vector3 pos, float width, float height, Color color, float tilt)
    {
        var go = Box(root, "Shard", pos, new Vector3(width, height, width), color);
        go.transform.localRotation = Quaternion.Euler(tilt, tilt * 2f, tilt * 0.5f);
    }

    // ── 조각 ─────────────────────────────────────────────────────────────────
    // **콜라이더는 지운다.** 조각마다 달려 있으면 `SelectionManager`의 단일 레이캐스트가
    // 조각을 맞고 건물을 못 찾는다(콜라이더는 루트에 하나라는 규약).
    static GameObject Box(GameObject root, string name, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = EnsureMaterial(color);
        return go;
    }

    static Material EnsureMaterial(Color color)
    {
        string key  = ColorUtility.ToHtmlStringRGB(color);
        string path = $"{MatDir}/Bld_{key}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = $"Bld_{key}" };
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color",     color);

        Directory.CreateDirectory(MatDir);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
