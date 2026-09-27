using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **다리 — 프리팹 · 건물 데이터 · 절벽 재질을 만든다**(2026-09-26 — `World → J. Create Bridges`).
//
// 동굴 팩(3DForge)의 다리 넷(돌다리 셋 · 나무다리 넷 · 판자 둘)을 **지을 수 있는 건물**로 만든다. 같은
// 프리팹이 자연 다리로도 선다(`ChasmData.Bridges` → `ChasmRenderer`) — 규칙은 `Bridge` 한 벌이다.
// 무너진 돌다리 셋은 **장식**이라 건물 데이터 없이 프리팹만 만든다(`Ruin_*`).
//
// ── 모양은 팩 것을 그대로 쓴다 ──────────────────────────────────────────────
//
// **팩 프리팹을 `[Art]`로 통째로 넣는다** — 메시와 재질이 한 짝이라(팩의 UV에 묶인 아틀라스) 텍스처만 떼면
// 엉뚱한 자리가 잘린다(`VeinArt` 때 배웠다). 넣고 나서 셋을 맞춘다:
//   ① **긴 쪽을 Z로 돌린다** — `BuildingData.Size`가 (폭 2, 길이 L)이다.
//   ② **걷는 윗면을 루트 높이(0)에 맞춘다** — 한가운데를 위에서 쏴 맞은 높이를 잰다(아치 돌다리는
//      한가운데가 가장 높다). 루트가 곧 다리 윗면(`Bridge.SpanOf`)이라 폰의 발과 그림이 맞는다.
//   ③ **팩 콜라이더를 뗀다** — 걷는 면은 루트의 얇은 상자 하나다(아치를 그대로 밟으면 판정과 그림이
//      갈리고, 판정은 `Chasm`의 윗면이 이미 답한다).
//
// **여러 번 돌려도 된다** — 프리팹은 **자식만 갈아 끼우고**(통째로 다시 지으면 루트의 guid·fileID가 바뀌어
// 놓아 둔 인스턴스가 깨진다) 데이터는 값만 맞춘다. 비용은 `World → F`의 표가 들고 끝에서 그것을 다시 적용한다.
public static class BridgeCreator
{
    const string Pack       = "Assets/assetsss/3DForge/Cave Adventure kit/Prefabs";
    const string PrefabDir  = "Assets/3_Prefab/Building";
    const string DataDir    = "Assets/Resources/Data/Buildings";
    const string WorldDir   = "Assets/Resources/World";

    // **팩 모델을 키운다 — 크기는 셋이다**(2026-09-26). 처음엔 팩 그대로라 다리 하나가 절벽 덩어리(2m) 하나만
    // 건넜고(사용자: *"2~3칸에 모델 하나를 생각했다"*) 2배로 키웠다. 그다음 *"크기가 다양했으면"* — 종류마다
    // 작은(×1.25) · 보통(×2) · 큰(×3)을 만든다. **통째로 같은 배율로** 키운다(길이만 늘리면 판자·난간이 늘어진다).
    // 발자국(폭·길이)과 걷는 면은 키운 모델에서 다시 잰다.
    //
    // **보통이 옛 id를 이어받는다**(접미사 없음) — 놓아 둔 다리·자연 다리 목록이 그 guid를 가리킨다.
    // 무너진 다리 장식은 보통 하나만.
    static readonly (string Suffix, string Prefix, float Scale)[] Sizes =
    {
        ("_S", "작은 ", 1.25f),
        ("",   "",      2f),
        ("_L", "큰 ",   3f),
    };
    public const float RuinScale = 2f;

    struct Kind
    {
        public string Id, Name, Source, Desc;
        public bool   Ruin;
        public Kind(string id, string name, string src, string desc, bool ruin = false)
        { Id = id; Name = name; Source = src; Desc = desc; Ruin = ruin; }
    }

    static readonly Kind[] Kinds =
    {
        new("Bridge_RockMoss",     "이끼 돌다리",       "Rock/Moss/Rockbridges/moss_rockbridge.prefab",       "절벽을 건너는 돌다리. 이끼가 앉았다."),
        new("Bridge_RockFrozen",   "얼음 돌다리",       "Rock/Frozen/Rockbridges/frozen_rockbridge.prefab",   "절벽을 건너는 돌다리. 서리가 덮였다."),
        new("Bridge_RockShadow",   "그림자 돌다리",     "Rock/Shadow/Rockbridges/shadow_rockbridge.prefab",   "절벽을 건너는 검은 돌다리."),
        new("Bridge_Wood",         "나무다리",          "Wooden/Wooden bridges & decks/woodenbridge.prefab",        "판자로 짜 맞춘 다리."),
        new("Bridge_WoodRail",     "난간 나무다리",     "Wooden/Wooden bridges & decks/woodenbridge_rail.prefab",   "난간을 두른 나무다리."),
        new("Bridge_WoodMoss",     "이끼 나무다리",     "Wooden/Wooden bridges & decks/moss_woodenbridge.prefab",   "이끼 낀 나무다리."),
        new("Bridge_WoodMossRail", "이끼 난간 나무다리", "Wooden/Wooden bridges & decks/moss_woodenbridge_rail.prefab", "이끼 낀 난간 나무다리."),
        new("Bridge_PlankShort",   "짧은 판자 다리",    "Wooden/Wooden bridges & decks/deck_short.prefab",          "좁은 틈을 건너는 판자."),
        new("Bridge_PlankLong",    "긴 판자 다리",      "Wooden/Wooden bridges & decks/deck_long.prefab",           "판자를 길게 이은 다리."),

        new("Ruin_BridgeMoss",     "무너진 이끼 돌다리",   "Rock/Moss/Rockbridges/moss_rockbridge_broken.prefab",     "", true),
        new("Ruin_BridgeFrozen",   "무너진 얼음 돌다리",   "Rock/Frozen/Rockbridges/frozen_rockbridge_broken.prefab", "", true),
        new("Ruin_BridgeShadow",   "무너진 그림자 돌다리", "Rock/Shadow/Rockbridges/shadow_rockbridge_broken.prefab", "", true),
    };

    [MenuItem("DungeonColony/World/J. Create Bridges (다리 · 절벽 재질)")]
    static void Run()
    {
        EnsureFolder(WorldDir);
        var log = new List<string>();

        Materials(log);
        EnsureChasmData(log);

        int order = 0;
        foreach (var k in Kinds)
        foreach (var (suffix, prefix, scale) in Sizes)
        {
            if (k.Ruin && scale != RuinScale) continue;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Pack}/{k.Source}");
            if (src == null) { Debug.LogWarning($"[다리] 팩 프리팹이 없습니다 — {k.Source}"); break; }

            string id = k.Id + suffix;
            BuildingData data = null;
            if (!k.Ruin)
            {
                data = AssetDatabase.LoadAssetAtPath<BuildingData>($"{DataDir}/{id}.asset");
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<BuildingData>();
                    AssetDatabase.CreateAsset(data, $"{DataDir}/{id}.asset");
                }
            }

            var prefab = BuildPrefab(k, id, scale, src, data, out int width, out int length);
            if (prefab == null) continue;

            if (data != null)
            {
                data.BuildingName  = prefix + k.Name;
                data.Description   = k.Desc + $" 폭 {width}칸 · 길이 {length}칸 — 절벽 폭 {Mathf.Max(0, length - 2)}m까지 건넌다.";
                data.Category      = BuildCategory.Misc;
                data.SortOrder     = 80 + order++;
                data.Size          = new Vector2Int(width, length);
                data.Prefab        = prefab;
                data.UsesBlueprint = true;
                data.IsBridge      = true;
                data.OccupiesTile  = false;
                data.DragBuild     = false;
                EditorUtility.SetDirty(data);
            }
            log.Add($"{prefix}{k.Name}({width}×{length}칸)");
        }

        AssetDatabase.SaveAssets();
        BuildingCostCreator.Wire();   // 비용은 `World → F`의 표 한 벌
        Debug.Log("[다리] " + string.Join(" · ", log) + "\n  자연 다리는 `World → I. Paint Chasms`의 **다리 자동 배치**가 놓는다.");
    }

    // ── 프리팹 ───────────────────────────────────────────────────────────────

    static GameObject BuildPrefab(Kind k, string id, float scale, GameObject src, BuildingData data, out int width, out int length)
    {
        width = 2; length = 0;
        string path = $"{PrefabDir}/{id}.prefab";

        // **있으면 열어서 자식만 갈아 끼운다** — 루트를 지켜야 놓인 인스턴스가 안 깨진다.
        bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        var  root     = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject(id);

        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        // ① 팩 모양을 넣는다(연결을 끊는다 — 콜라이더를 떼야 한다).
        var art = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(art, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        art.name = "[Art]";
        art.transform.SetParent(root.transform, false);
        art.transform.localPosition = Vector3.zero;
        art.transform.localRotation = Quaternion.identity;
        art.transform.localScale    = Vector3.one * scale;

        var b = Bounds(art);
        if (b.size.x > b.size.z) art.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // 긴 쪽을 Z로
        b = Bounds(art);

        // ② 걷는 윗면을 0에 — 한가운데를 위에서 쏜다(아치는 한가운데가 윗면이다).
        float top = TopAt(art, new Vector3(b.center.x, b.max.y + 5f, b.center.z)) ?? b.max.y;
        art.transform.localPosition = new Vector3(-b.center.x, -top, -b.center.z);

        // ③ 팩 콜라이더를 뗀다.
        foreach (var c in art.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        length = Mathf.Max(2, Mathf.RoundToInt(b.size.z));

        // **폭은 덩어리 홀수 개(2m · 6m · 10m)** — 자동 배치는 폭 방향 가운데를 **덩어리 한복판**에 두고
        // (`ChasmPainter.PlaceBridges`) 긴 축만 맞춘다(`Snap`). 짝수 개(4m)면 발자국이 양옆 덩어리를 반씩 걸쳐
        // 절벽 칸(덩어리 단위)과 어긋난다.
        int blocks = 2 * Mathf.FloorToInt((b.size.x / TerrainGrid.VertexSpacing - 1f) * 0.5f + 0.5f) + 1;
        width = Mathf.Max(1, blocks) * TerrainGrid.VertexSpacing;

        if (!k.Ruin)
        {
            // **`??`를 안 쓴다** — 에디터의 `GetComponent`는 없을 때 *가짜 null*을 돌려줘 `??`를 통과한다.
            var pb = root.GetComponent<PlacedBuilding>();
            if (pb == null) pb = root.AddComponent<PlacedBuilding>();
            var so = new SerializedObject(pb);
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();

            var br = root.GetComponent<Bridge>();
            if (br == null) br = root.AddComponent<Bridge>();
            // 크기를 다리에도 적는다 — 미리보기는 `PlacedBuilding`을 떼므로 `Data`로는 못 잰다(`Bridge.size`).
            var bso = new SerializedObject(br);
            bso.FindProperty("size").vector2IntValue = new Vector2Int(width, length);
            bso.ApplyModifiedPropertiesWithoutUndo();

            // 걷는 면 — 윗면이 루트 높이(0)인 얇은 상자. **트리거가 아니다**(밟는 면이다 — 지형에 구멍이 났다).
            var box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.size   = new Vector3(width, 0.3f, length);
            box.center = new Vector3(0f, -0.15f, 0f);
        }

        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        if (existing) PrefabUtility.UnloadPrefabContents(root);
        else          Object.DestroyImmediate(root);
        return saved;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        // 루트 기준으로 옮긴다(`art`는 루트 자식이고 루트는 원점이다).
        return b;
    }

    // 임시 메시 콜라이더를 달아 위에서 쏜다 — 맞은 가장 높은 면.
    static float? TopAt(GameObject art, Vector3 from)
    {
        var temp = new List<MeshCollider>();
        foreach (var mf in art.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            temp.Add(mc);
        }

        Physics.SyncTransforms();
        float? y = null;
        foreach (var mc in temp)
            if (mc.Raycast(new Ray(from, Vector3.down), out var hit, 100f))
                y = y == null ? hit.point.y : Mathf.Max(y.Value, hit.point.y);

        foreach (var mc in temp) Object.DestroyImmediate(mc);
        return y;
    }

    // ── 절벽 재질 ───────────────────────────────────────────────────────────
    //
    // **벽은 바위 벽 재질의 사본이다** — 같은 결이라 절벽이 굴의 일부로 읽힌다. 안개는 반투명 URP Unlit,
    // 바닥은 불투명. **`Resources/World/`에 둔다** — `ChasmRenderer`가 런타임에 읽고, 에셋이라 빌드에 들어간다.
    static void Materials(List<string> log)
    {
        var rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/2_Material/Underworld/Rock_Wall.mat");
        Save(rock != null ? new Material(rock) : NewLit(new Color(0.18f, 0.17f, 0.16f)), "ChasmWall", log);

        var fog = new Material(Shader.Find(RuntimeShaders.Unlit));
        FadeMaterial.Setup(fog);
        FadeMaterial.SetColor(fog, new Color(0.02f, 0.02f, 0.03f, 0.35f));
        Save(fog, "ChasmFog", log);

        var floor = new Material(Shader.Find(RuntimeShaders.Unlit));
        FadeMaterial.SetColor(floor, new Color(0.01f, 0.01f, 0.015f, 1f));
        Save(floor, "ChasmFloor", log);
    }

    static Material NewLit(Color c)
    {
        var m = new Material(Shader.Find(RuntimeShaders.Lit));
        FadeMaterial.SetColor(m, c);
        return m;
    }

    // **있으면 값만 맞춘다** — guid를 지킨다(재질을 새로 만들면 가리키던 쪽이 끊긴다).
    static void Save(Material m, string name, List<string> log)
    {
        string path = $"{WorldDir}/{name}.mat";
        var old = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (old != null) { old.shader = m.shader; old.CopyPropertiesFromMaterial(m); EditorUtility.SetDirty(old); }
        else { AssetDatabase.CreateAsset(m, path); log.Add($"재질 {name}"); }
    }

    static void EnsureChasmData(List<string> log)
    {
        string path = $"{WorldDir}/Chasms.asset";
        if (AssetDatabase.LoadAssetAtPath<ChasmData>(path) != null) return;
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ChasmData>(), path);
        log.Add("절벽 데이터(빈 것)");
    }

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
