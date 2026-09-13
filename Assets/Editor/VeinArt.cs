using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **광맥이 어떻게 생겼나 — 답은 여기 하나다**(2026-09-13, 사용자 요청).
//
// 그전에는 광맥 열다섯이 전부 **작은 큐브를 흩뿌린 것**이라 화면에서 *바위에 박힌 부스러기*로
// 보였고, 무엇보다 **금속이든 보석이든 똑같이 생겼다.** 프리팹 안에 자식 이름만
// `Rock` / `Shard`로 갈려 있었지 모양은 한 벌이었다.
//
// ## 모양이 둘이다 — 무엇이 나오냐가 정한다
//
// | | 결정 | 광석 |
// |---|---|---|
// | 무엇이 | 보석 · 마법 결정 | 금속 · 석탄 |
// | 모양 | **뾰족한 기둥 다발**이 한 덩어리에서 솟는다 | **둥근 바위 덩어리에 알갱이가 박혀** 있다 |
// | 눈에 | 멀리서도 결정으로 읽힌다 | 바위인데 뭔가 섞여 있다 |
//
// 갈라 두는 이유는 **캐러 갈지 정하는 것이 눈이기 때문이다.** 굴에서 벽을 보는 시간이
// 대부분인데 광맥이 전부 같은 모양이면 *무엇이 나오나*를 **눌러 봐야 안다** — 그러면
// 판단이 아니라 뽑기다(`PawnMark`가 답한 그 물음과 같은 자리).
//
// ## 메시는 한 벌이고 크기·각도·색만 얹는다
//
// 종류마다 메시를 만들면 열다섯 벌을 손으로 맞추게 되고 **그중 하나는 반드시 뒤처진다**
// (`RockRenderer`의 바이옴 색조 · `PawnMark`가 답한 그 규칙). 여기서는 **절차 메시 둘**
// (`Shard` · `Boulder`)을 구워 에셋으로 두고, 프리팹은 그것을 **스케일·회전·재질**만 달리해
// 쓴다.
//
// **메시를 에셋으로 저장한다** — 프리팹이 가리켜야 빌드에 들어간다(`Shader.Find`가 잘려
// 나가는 것의 뒷면이다: **가리키는 것만 들어간다**).
//
// **평면 음영으로 굽는다**(면마다 정점을 따로 둔다) — 그림이 각진 면으로 그려져 있고,
// 폰이 블록 인형이라 매끈한 것이 오히려 튄다.
//
// ## 어기면 조용히 틀리는 것
//
// - **콜라이더는 루트가 든 하나뿐이다.** 자식마다 달리면 커서 레이캐스트가 결정 하나를
//   짚고, 그러면 **노드가 아니라 그 조각이 잡힌다**.
// - **트리거가 아니다.** 광맥은 벽에 붙어 서므로 몸으로 막는 것이 맞다(버섯·횃불과 반대).
// - **`ResourceNode.data`를 다시 물린다.** 비면 살펴보기 창이 **날 프리팹 이름**을 띄우고
//   몸이 텅 빈다(`MushroomCreator`가 먼저 겪었다).
// - **같은 경로에 덮어쓴다 — guid가 유지된다.** `ResourceNodeData.Prefab`과 이식 광맥
//   건물이 guid로 가리키므로, 새 파일로 만들면 **둘 다 조용히 끊긴다.**
// - **자리를 굴리되 시드를 못 박는다.** 프리팹은 한 번 구워 두는 것이라, 다시 돌릴 때마다
//   모양이 달라지면 **버전 관리에 매번 diff가 뜬다.**
// - **발광은 `LightSource`로 단다 — 그냥 `Light`가 아니다.** `Vision`이 그 레지스트리를
//   읽으므로 **화면이 밝아지는 만큼 폰도 실제로 더 본다**(장식 조명으로 두면 *"밝아 보이는데
//   안 보인다"*가 생긴다).
public static class VeinArt
{
    const string PrefabDir = "Assets/3_Prefab/Node";
    const string NodeDir   = "Assets/5_Data/Resource";
    const string MeshDir   = "Assets/3_Prefab/Node/Mesh";
    const string MatDir    = "Assets/2_Material";

    // **모양이 넷이다.** `Rock`·`Pebbles`는 광석이 안 박힌 순수한 돌이고, 그래서 같은 표에
    // 둔다 — 굴에 서 있는 돌덩이가 광맥과 다른 결이면 **광맥만 딴 게임에서 온 것**처럼 보인다.
    public enum Shape { Ore, Crystal, Rock, Pebbles }

    // **무엇이 어떤 모양인가 — 저작한다.**
    //
    // 이름에서 짜낼 수 없다(`Node_MagicCrystal`은 결정이지만 `Node_MithrilVein`은 금속이다).
    // 그리고 **`DropItem`의 묶음으로도 못 가른다** — 보석 묶음(`Gem`)은 맞지만 마법 결정은
    // 잡화라 거기 안 걸린다. 열다섯뿐이니 적는 편이 싸다.
    public struct Vein
    {
        public string Name;
        public Shape  Shape;
        public Color  Ore;     // 알갱이·결정
        // **금속은 `_Metallic`이 답한다 — 색이 아니다.** 색만 회색으로 칠하면 *회색 돌*이지
        // 금속이 아니다. 금속으로 보이게 하는 것은 **주변을 반사하는 것**이라 이 값이 든다.
        public float  Metal;   // 0 = 돌·보석(유전체) · 1 = 금속
        public float  Gloss;   // 매끄러움. 거친 광석은 낮게, 벼린 금속은 높게
        public bool   Glow;
    }

    static Color C(int hex) => new Color(((hex >> 16) & 0xFF) / 255f,
                                         ((hex >> 8) & 0xFF) / 255f,
                                         (hex & 0xFF) / 255f);

    // **채도를 올리고 명도를 지킨다**(2026-09-13에 한 번 틀렸다).
    //
    // 처음엔 실제 금속 색에 가깝게 잡았는데(주석 `9AA0A6` · 티타늄 `8C9298` · 은 `D4D8DC`)
    // **셋이 전부 무채색**이라 화면에서 구별이 안 됐다 — 그리고 굴은 어느 때에도 밝기 배율이
    // 1이 아니라(0.30~0.62) **채도가 낮은 색은 전부 같은 회색으로 눌린다.**
    // `RockRenderer`의 바이옴 색조가 먼저 겪은 그 자리인데 같은 함정을 다시 밟았다.
    //
    // 그래서 **실제 색보다 한 칸씩 과장한다.** 은빛 넷(주석·은·티타늄·미스릴)은 명도만으로는
    // 못 가르므로 **색상을 일부러 벌린다** — 주석은 초록 쪽, 티타늄은 하늘 쪽, 은은 거의
    // 흰색, 미스릴은 빛난다. 구리와 철도 둘 다 붉은 계열이라 **명도로 가른다**(밝은 주황 ↔
    // 어두운 적갈).
    // **금속은 색이 아니라 `_Metallic`이 만든다**(2026-09-13, 참조 사진을 받고 고쳤다).
    //
    // 처음엔 색만 칠했는데 그러면 **회색 돌**이지 금속이 아니다. 금속으로 읽히는 것은
    // *주변을 반사하는 것*이고 그건 `_Metallic` + `_Smoothness`가 한다.
    //
    // **철은 적갈색이 아니다** — 처음에 녹슨 색(`9E4A38`)으로 잡았는데 사진의 철광은
    // **어두운 은회색 금속 덩이**다. 그래서 은빛이 다섯이 됐고(철·주석·티타늄·은·미스릴)
    // **명도와 색상으로 층층이 갈랐다**: 철이 가장 어둡고(0.52) → 티타늄은 푸른 쪽(0.70)
    // → 주석은 연한 회백(0.80) → 은은 거의 흰색(0.96) → 미스릴은 혼자 빛난다.
    //
    // **석탄만 금속이 아니다**(`Metal` 0, `Gloss` 낮음) — 탄소라 빛을 먹는다. 새까맣고
    // 광택이 없어서 **금속 넷 사이에서 저 혼자 확실히 갈린다.**
    //
    // **보석은 유전체다**(`Metal` 0) — 금속으로 칠하면 색이 죽고 회색으로 간다.
    // 대신 `Gloss`를 높여 유리처럼 반짝이게 둔다.
    public static readonly Vein[] All =
    {
        // ── 금속·석탄 — 둥근 바위에 광석 덩이가 박힌 모양
        new Vein { Name = "Node_CoalSeam",     Shape = Shape.Ore, Ore = C(0x0B0B0E), Metal = 0.00f, Gloss = 0.28f },
        new Vein { Name = "Node_CopperVein",   Shape = Shape.Ore, Ore = C(0xE0884A), Metal = 0.95f, Gloss = 0.68f },
        new Vein { Name = "Node_TinVein",      Shape = Shape.Ore, Ore = C(0xC2CDC8), Metal = 0.85f, Gloss = 0.50f },
        new Vein { Name = "Node_IronVein",     Shape = Shape.Ore, Ore = C(0x7E858C), Metal = 0.92f, Gloss = 0.52f },
        new Vein { Name = "Node_SilverVein",   Shape = Shape.Ore, Ore = C(0xF2F5F8), Metal = 1.00f, Gloss = 0.86f },
        new Vein { Name = "Node_GoldVein",     Shape = Shape.Ore, Ore = C(0xFFC844), Metal = 1.00f, Gloss = 0.82f },
        new Vein { Name = "Node_TitaniumVein", Shape = Shape.Ore, Ore = C(0x8FB2CC), Metal = 0.95f, Gloss = 0.74f },
        // 미스릴만 금속인데 빛난다 — 5층의 물건이라 눈에 띄어야 한다.
        new Vein { Name = "Node_MithrilVein",  Shape = Shape.Ore, Ore = C(0xB8ECFF), Metal = 0.90f, Gloss = 0.88f, Glow = true },

        // ── 보석·마법 결정 — 뾰족한 기둥 다발. **금속이 아니다**(유전체)
        new Vein { Name = "Node_RubyVein",     Shape = Shape.Crystal, Ore = C(0xE0263F), Gloss = 0.85f },
        new Vein { Name = "Node_SapphireVein", Shape = Shape.Crystal, Ore = C(0x2E6BE8), Gloss = 0.85f },
        new Vein { Name = "Node_TopazVein",    Shape = Shape.Crystal, Ore = C(0xF0B81E), Gloss = 0.85f },
        new Vein { Name = "Node_EmeraldVein",  Shape = Shape.Crystal, Ore = C(0x23C46B), Gloss = 0.85f },
        new Vein { Name = "Node_MagicCrystal", Shape = Shape.Crystal, Ore = C(0x8A5CF0), Gloss = 0.90f, Glow = true },
        new Vein { Name = "Node_EtherCrystal", Shape = Shape.Crystal, Ore = C(0xFFE9A8), Gloss = 0.90f, Glow = true },
        // 공허는 검보라인데 **너무 어두우면 아예 안 보인다** — 명도를 올리고 색으로 가른다.
        new Vein { Name = "Node_VoidCrystal",  Shape = Shape.Crystal, Ore = C(0x6B2FA0), Gloss = 0.90f, Glow = true },

        // ── 그냥 돌 — 박힌 것이 없다. **광맥과 같은 메시·재질을 쓴다**(사용자 요청).
        new Vein { Name = "Stone",             Shape = Shape.Rock },
        new Vein { Name = "Node_Pebbles",      Shape = Shape.Pebbles },
    };

    [MenuItem("DungeonColony/World/D. Rebuild Vein Art (광맥 모양)")]
    static void Rebuild()
    {
        int n = 0;
        foreach (var v in All)
        {
            // **바닥 돌은 `Forageable`이라 노드 데이터가 없다** — 없다고 건너뛰면
            // 그것만 옛 모양으로 남는다.
            var data = AssetDatabase.LoadAssetAtPath<ResourceNodeData>(NodeDir + "/" + v.Name + ".asset");

            var prefab = Build(v, data);
            if (prefab == null) continue;

            if (data != null)
            {
                // **노드가 프리팹을 다시 가리키게 한다** — 덮어써도 참조는 유지되지만,
                // 비어 있던 노드(`FungalWood`가 그랬다)는 여기서 채워진다.
                data.Prefab = prefab;
                EditorUtility.SetDirty(data);
            }
            n++;
        }

        int dropped = DropStaleMaterials();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[광맥] {n}개를 다시 구웠습니다 — 금속은 둥근 바위에 알갱이, 보석은 기둥 다발." +
                  (dropped > 0 ? $" 안 쓰는 옛 색 재질 {dropped}개를 치웠습니다." : ""));
    }

    // **색을 바꾸면 옛 재질이 남는다 — 치운다.**
    //
    // 재질 이름에 색이 들어 있어서(`Vein_8A5B47.mat`) 색을 고치면 **새 파일이 생기고 옛
    // 것은 아무도 안 가리킨 채 남는다.** 열다섯을 두 번만 돌려도 서른 개가 되고, 그러면
    // 다음에 보는 사람이 *"어느 쪽이 진짜인가"*를 재게 된다(옛 `sites` 배열과 같은 자리).
    //
    // **표에서 나오는 것만 남긴다** — 목록을 따로 들면 그것이 또 뒤처진다.
    static int DropStaleMaterials()
    {
        var keep = new HashSet<string> { MatDir + "/Vein_Rock.mat" };
        foreach (var v in All) keep.Add(AssetDatabase.GetAssetPath(OreMat(v)));

        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!System.IO.Path.GetFileName(path).StartsWith("Vein_")) continue;
            if (keep.Contains(path)) continue;

            AssetDatabase.DeleteAsset(path);
            n++;
        }
        return n;
    }

    // ── 프리팹 ──────────────────────────────────────────────────────────────

    // 이름으로 하나만 굽는다 — `MetalCreator`가 새 광맥을 만들 때 부른다.
    // **표에 없으면 경고하고 안 짓는다** — 조용히 기본 모양으로 지으면 *그 광맥만
    // 다르게 생긴 것*이 언제 생겼는지 아무도 모른다.
    public static GameObject BuildOne(string name, ResourceNodeData data)
    {
        foreach (var v in All)
            if (v.Name == name) return Build(v, data);

        Debug.LogWarning($"[광맥] {name}이(가) `VeinArt.All` 표에 없습니다 — 모양을 못 짓습니다.");
        return null;
    }

    // **처음부터 다시 짓지 않는다 — 모양만 갈아 끼운다**(2026-09-13에 크게 틀렸다).
    //
    // 처음엔 `new GameObject`로 새로 지어 덮어썼다. 그랬더니 옛 프리팹에 붙어 있던
    // **`PlacedBuilding`과 `PowerUser`가 통째로 사라졌고**, 이식 광맥이 *건물*이기를
    // 그만두어 **화면에서 아예 안 보였다**(사용자: *"인게임중에 안보여"*).
    //
    // 에셋만 보면 **아무 문제가 없어 보인다** — 메시도 재질도 레이어도 멀쩡하고,
    // guid와 루트 fileID까지 유지된다. **없어진 것은 있는 것들 사이에 안 보인다.**
    //
    // 그래서 **있는 프리팹을 열어 자식만 갈아 끼운다.** 그러면 루트에 무엇이 붙어 있든
    // 그대로 산다 — 다음에 누가 컴포넌트를 하나 더 달아도 이 도구가 그것을 모른 채
    // 지키게 된다. **아는 것만 지키는 목록을 들면 그 목록이 반드시 뒤처진다.**
    public static GameObject Build(Vein v, ResourceNodeData data)
    {
        string path = PrefabDir + "/" + v.Name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        GameObject root;
        if (existing != null)
        {
            root = (GameObject)PrefabUtility.InstantiatePrefab(existing);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely,
                                              InteractionMode.AutomatedAction);
            Strip(root);
        }
        else root = new GameObject(v.Name);

        // **크기는 칸 수가 정한다 — 손으로 적지 않는다**(2026-09-13에 어긋났다).
        //
        // 모델 폭을 직접 적어 뒀더니 **칸 수와 따로 놀았다**: 광맥은 `size` 2x2(2m)인데
        // 모델이 1.51m라 제 칸도 못 채웠고(사용자: *"너무 작고 너무 낮은 거 같은데"*),
        // 반대로 `Stone`은 1x1인데 모델이 1.65m라 **칸 밖으로 삐져나왔다.**
        //
        // 칸 수는 `ResourceNode.size`가 이미 들고 있다(건물 쪽은 `BuildingData.Size`와
        // 같은 값이다). **여기서 읽으면 답이 한 벌이 되고**, 나중에 칸을 늘려도 모델이
        // 저절로 따라온다.
        var foot = FootprintOf(root, v.Shape);
        if (v.Shape == Shape.Ore) WriteFootprint(root, v.Name);

        var rng = new System.Random(StableSeed(v.Name));
        switch (v.Shape)
        {
            case Shape.Crystal: BuildCrystal(root.transform, v, rng); break;
            case Shape.Pebbles: BuildPebbles(root.transform, rng);          break;
            default:            BuildOre(root.transform, v, rng, foot);     break;   // Ore · Rock
        }

        if (v.Shape == Shape.Pebbles)
        {
            // **바닥 돌은 칸을 안 막는다 — 트리거다**(버섯·횃불과 같은 규칙). 통로에
            // 굴러다니는 돌이 길을 끊으면 안 된다. 그래도 콜라이더는 **있어야 한다**:
            // 좌클릭으로 고르는 길이 그것뿐이다.
            var sph = root.AddComponent<SphereCollider>();
            sph.isTrigger = true;
            sph.radius    = 0.55f;
            sph.center    = new Vector3(0f, 0.22f, 0f);
        }
        else
        {
            // **콜라이더는 루트가 든 하나뿐이고 트리거가 아니다** — 광맥·돌은 캘 물건이라
            // 몸으로 막는 것이 맞다.
            var col = root.AddComponent<CapsuleCollider>();
            col.height = v.Shape == Shape.Crystal ? 1.6f : HeightFor(v.Shape, foot);
            col.radius = Mathf.Min(foot.x, foot.y) * 0.46f;
            col.center = new Vector3(0f, col.height * 0.5f, 0f);
        }

        if (v.Glow) AddGlow(root, v.Ore);

        // **채집물은 `ResourceNode`가 아니다** — 달면 한 번 따고 없어져야 할 것이
        // 배정받아 눌러앉는다(`MushroomCreator`가 갓버섯에서 겪은 그 자리).
        if (v.Shape != Shape.Pebbles)
        {
            // **있으면 그대로 쓴다** — 다시 달면 인스펙터에서 맞춰 둔 값이 날아간다.
            var node = root.GetComponent<ResourceNode>() ?? root.AddComponent<ResourceNode>();
            if (data != null)
                SerializedWrite.Set(node, so => so.FindProperty("data").objectReferenceValue = data);
        }

        // **같은 경로에 덮어쓴다** — guid와 루트 fileID가 유지되어야 노드 데이터와 이식
        // 광맥 건물(`BuildingData.Prefab`은 **fileID까지** 가리킨다)이 계속 찾는다.
        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    // **광석 광맥은 3x3이다**(2026-09-13, 사용자 결정 — *"좀 큼직큼직했으면 … 3*3으로
    // 바꿔봐"*). 2x2(2m)에서는 참조 사진 같은 **덩치**가 안 나왔다.
    //
    // **홀수라 칸 한가운데에 앉는다** — 짝수는 칸 **경계**에 놓여 발자국이 한 칸 밀리는
    // 자리를 `ResourceNode.Footprint`가 따로 보정하고 있다(주석에 *"2x1 광맥이 3칸을
    // 막는다"*고 적힌 그 사고). 홀수는 그 보정이 필요 없다.
    static readonly Vector2Int OreFoot = new Vector2Int(3, 3);

    // 칸 수. **광석은 도구가 정하고 나머지는 있는 프리팹이 답한다** — 결정은 종류마다
    // 다르게 저작돼 있어(마법 결정 2x1) 여기서 덮으면 손으로 맞춘 것이 날아간다.
    static Vector2 FootprintOf(GameObject root, Shape shape)
    {
        if (shape == Shape.Ore) return new Vector2(OreFoot.x, OreFoot.y);

        var node = root.GetComponent<ResourceNode>();
        if (node != null)
        {
            var so = new SerializedObject(node);
            var sz = so.FindProperty("size");
            if (sz != null && sz.vector2IntValue.x > 0)
                return new Vector2(sz.vector2IntValue.x, sz.vector2IntValue.y);
        }
        return Vector2.one;
    }

    // **칸 수는 세 군데가 같이 알아야 한다** — 프리팹의 `ResourceNode.size`(발자국·점유),
    // `BuildingData.Size`(이식 광맥을 놓을 때의 미리보기와 점유). 한쪽만 고치면
    // **놓이는 칸과 막는 칸이 어긋난다**(모델은 3칸을 덮는데 2칸만 막히는 식).
    static void WriteFootprint(GameObject root, string name)
    {
        var node = root.GetComponent<ResourceNode>();
        if (node != null)
            SerializedWrite.Set(node, so => so.FindProperty("size").vector2IntValue = OreFoot);

        var bd = AssetDatabase.LoadAssetAtPath<BuildingData>(
                     "Assets/Resources/Data/Buildings/" + name + ".asset");
        if (bd == null) return;      // 야생 전용 광맥(티타늄·미스릴)은 건물이 아직 없다

        bd.Size = OreFoot;
        EditorUtility.SetDirty(bd);
    }

    // 메시 한 벌이 x·z로 1.94, y로 1.84를 덮는다(반지름 0.97 x 2, 눌림 0.95 포함).
    const float MeshW = 1.94f, MeshH = 1.84f;

    // **칸을 꽉 채우지 않는다**(0.92배) — 딱 맞추면 이웃한 광맥 둘이 면끼리 붙어
    // 하나로 보이고, 모서리가 칸 밖으로 반 뼘 삐져나온다.
    static Vector3 BodyScale(Vector2 foot, float height) =>
        new Vector3(foot.x * 0.92f / MeshW, height / MeshH, foot.y * 0.92f / MeshW);

    // **높이는 칸의 짧은 변이 정하되 폭만큼 따라가지는 않는다.**
    //
    // 3x3이면 폭이 2.76m인데 높이까지 그 비율로 키우면 2.5m가 넘어 **좁은 굴에서 천장을
    // 뚫는다**(머리 위 여유의 최소가 폰 키 1.8m다). 0.63배로 두면 3x3에서 **1.89m** —
    // 폰 키 언저리이고 **폭보다 낮아 참조 사진처럼 옆으로 퍼진 바위**가 된다.
    static float HeightFor(Shape shape, Vector2 foot) =>
        shape == Shape.Crystal ? 0.36f : Mathf.Min(foot.x, foot.y) * 0.63f;

    // **내가 만든 것만 걷어낸다** — 자식(시각물) · 콜라이더 · 발광. 그 밖의 컴포넌트는
    // 손대지 않는다(`PlacedBuilding` · `PowerUser` · 나중에 누가 달 것).
    static void Strip(GameObject root)
    {
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        foreach (var c in root.GetComponents<Collider>())    Object.DestroyImmediate(c);
        foreach (var l in root.GetComponents<LightSource>()) Object.DestroyImmediate(l);
        foreach (var l in root.GetComponents<Light>())       Object.DestroyImmediate(l);

        // 옛 프리팹이 루트에 직접 메시를 들고 있었을 수도 있다.
        foreach (var r in root.GetComponents<MeshRenderer>()) Object.DestroyImmediate(r);
        foreach (var f in root.GetComponents<MeshFilter>())   Object.DestroyImmediate(f);
    }

    // **세기는 옅게, 색은 진하게.** 발광이 세면 색이 흰색으로 날아가고, 그러면
    // *파란 결정*이 아니라 *빛나는 무언가*가 된다(갓버섯에서 겪은 그 자리).
    static void AddGlow(GameObject root, Color color)
    {
        var lampGo = new GameObject("Glow");
        lampGo.transform.SetParent(root.transform, false);
        lampGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        var lamp = lampGo.AddComponent<Light>();
        lamp.type      = LightType.Point;
        lamp.color     = color;
        lamp.intensity = 0.35f;
        lamp.range     = 5f;
        lamp.shadows   = LightShadows.None;   // 수백 개가 던지면 프레임이 죽는다

        // **`LightSource`로 단다 — 그냥 `Light`가 아니다.** `Vision`이 그 레지스트리를 읽어
        // **화면이 밝아지는 만큼 폰도 실제로 더 본다.**
        var src = root.AddComponent<LightSource>();
        src.AlwaysLit = true;
        SerializedWrite.Set(src, so =>
        {
            so.FindProperty("radius").floatValue   = 5f;
            so.FindProperty("strength").floatValue = 0.35f;
            so.FindProperty("flicker").floatValue  = 0f;   // 불이 아니라 빛이다
            so.FindProperty("lamp").objectReferenceValue = lamp;
        });
    }

    // 뾰족한 기둥 다발. **큰 것 하나 · 중간 둘 · 작은 둘**이 한 받침에서 저마다 다른
    // 각도로 솟는다 — 다 같은 크기로 세우면 다발이 아니라 울타리가 된다.
    // 뾰족한 기둥 다발이 **큰 받침 바위**에서 솟는다. **큰 것 하나 · 중간 둘 · 작은 둘**이
    // 저마다 다른 각도로 — 다 같은 크기로 세우면 다발이 아니라 울타리가 된다.
    //
    // **받침은 낮게 둔다 — 높이는 기둥이 만든다.** 광석 쪽 돌덩이를 키운 것과 **다른
    // 물음이다**: 저쪽은 *박힐 바위*가 커야 하고, 여기는 *솟는 결정*이 보여야 한다.
    static void BuildCrystal(Transform root, Vein v, System.Random rng)
    {
        // 받침 — **낮다**(0.36m). 한 번 폰 키까지 올렸다가 되돌렸다(2026-09-13, 사용자:
        // *"마력석(보석류) 이런거는 냅두고"*). **높이는 광석 쪽 물음이었다** — 결정은
        // 받침이 아니라 기둥이 키를 만든다.
        // **칸에서 안 낸다 — 이 받침만은 손으로 적은 값이다**(2026-09-13, 사용자:
        // *"마법 결정은 받침을 바꿀 필요가 없어"*). 칸이 2x1이라 거기서 내면 깊이가
        // 0.92m로 눌려 **납작한 널빤지**가 된다. 결정 받침은 결정을 받치는 것이지
        // 칸을 채우는 물건이 아니다. 폭 1.65m · 깊이 1.46m · 높이 0.36m.
        var body = new Vector3(0.85f, 0.195f, 0.75f);
        Chunk(root, "Base", Boulder(), RockMat(), body, Vector3.zero, Vector3.zero);

        // 받침을 회전타원체로 보고 **앉을 높이를 거리에서 낸다** — 둘을 따로 적으면
        // 받침을 손볼 때마다 다섯 줄을 다시 맞춰야 하고, 빠뜨리면 결정이 공중에 뜬다
        // (광석에서 먼저 겪은 그 자리).
        float Rw = MeshW * 0.5f * body.x;
        float Rh = MeshH * 0.5f * body.y;
        float Cy = Rh;

        // (높이, 굵기, 중심에서의 거리, 기울기) — 꼭대기가 약 1.6m다.
        var spec = new[]
        {
            (1.35f, 0.32f, 0.00f,  6f),
            (0.95f, 0.26f, 0.26f, 18f),
            (0.80f, 0.22f, 0.30f, 22f),
            (0.48f, 0.17f, 0.34f, 30f),
            (0.34f, 0.14f, 0.30f, 34f),
        };

        for (int i = 0; i < spec.Length; i++)
        {
            var (h, r, dist, tilt) = spec[i];
            float a = (float)(rng.NextDouble() * System.Math.PI * 2.0);

            // 그 거리에서의 받침 표면 높이. **0.22m 박아 넣는다** — 표면에 얹으면
            // 바위에서 자란 것이 아니라 올려놓은 것으로 보인다.
            float t  = Mathf.Clamp01(dist / Rw);
            float sy = Cy + Rh * Mathf.Sqrt(Mathf.Max(0.05f, 1f - t * t)) - 0.12f;
            var at   = new Vector3(Mathf.Cos(a) * dist, sy, Mathf.Sin(a) * dist);

            // **기울기는 바깥을 향한다** — 아무 방향으로 눕히면 서로 뚫고 지나간다.
            var lean = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f) *
                       Quaternion.Euler(0f, 0f, -tilt) *
                       Quaternion.Euler(0f, (float)rng.NextDouble() * 60f, 0f);

            Chunk(root, "Shard" + i, Shard(), OreMat(v),
                  new Vector3(r, h, r), at, lean.eulerAngles);
        }
    }

    // 큰 돌덩이에 광석이 박힌 모양. **돌 7 : 광석 3**(사용자 결정) —
    // 광석이 더 많으면 *광석 더미*이지 광맥이 아니고, 더 적으면 무엇이 나오는지 안 읽힌다.
    //
    // **높이가 있어야 돌덩이다**(2026-09-13, 사용자: *"y값이 더 높았으면 … 좀 큰돌에
    // 박혀있는 거지"*). 처음엔 메시를 0.62배로 눌러 놓고 폭까지 넓게 잡아 **바닥에 깔린
    // 납작한 원반**이었다. 이제 폭 1.5m · 높이 1.3m이라 **캘 물건으로 보인다.**
    // **밑동을 땅에 박는다**(2026-09-13, 사용자: *"밑부분이 너무 U자 모양이야 이러면 뭔가
    // 흔들릴거 같아서 … 약간 땅에 박혀있게 하자 팰월드도 그렇게 하던데"*).
    //
    // 회전타원체의 밑동을 지면에 맞추면 **한 점으로 서 있는 모양**이라, 옆에서 보면 아래가
    // U자로 파여 **굴러갈 것처럼 보인다.** 22%를 묻으면 지면에서의 폭이 최대 폭의 83%라
    // **땅에서 솟은 것**으로 읽힌다.
    //
    // **묻히는 만큼 더 키운다** — 안 그러면 박는 순간 키가 그만큼 줄어든다. 높이 값은
    // *보이는 높이*이고, 실제 덩어리는 `1/(1-Sink)`배다.
    const float Sink = 0.22f;

    static void BuildOre(Transform root, Vein v, System.Random rng, Vector2 foot)
    {
        float seen = HeightFor(v.Shape, foot);       // 지면 위로 보이는 높이
        float full = seen / (1f - Sink);             // 덩어리의 실제 높이
        float down = full * Sink;                    // 묻히는 깊이

        var body  = BodyScale(foot, full);
        float Rw0 = MeshW * 0.5f * body.x;           // 주 덩어리의 허리께 반지름

        // **덩어리 넷이 깊이 겹쳐 하나의 바위가 된다**(2026-09-13, 사용자: *"너무 돌덩이
        // 두개야 좀 더 자연스럽게"*).
        //
        // 큰 것 하나에 작은 것 하나를 곁에 붙였더니 **나란히 앉은 알 둘**로 보였다. 둘은
        // 눈이 *두 개*로 세고, 셋부터는 *덩어리 하나*로 센다 — 다만 **떨어져 있으면 여전히
        // 여럿**이라 중심에서 0.26~0.34배(반지름의 3분의 1쯤)만 밀어 **서로 깊이 파고들게**
        // 둔다. 같은 재질이라 겹친 자리는 이음매가 안 보이고 **실루엣만 울퉁불퉁해진다.**
        //
        // (각도, 중심에서의 거리, 크기, 높이) — 높이를 조금씩 달리해 **어깨가 생긴다**.
        var lumps = new[]
        {
            (  0f, 0.00f, 1.00f,  0.00f),
            ( 55f, 0.30f, 0.74f, -0.04f),
            (165f, 0.34f, 0.66f, -0.09f),
            (275f, 0.27f, 0.58f,  0.09f),
        };

        for (int i = 0; i < lumps.Length; i++)
        {
            var (deg, dist, sc, dy) = lumps[i];
            float a = deg * Mathf.Deg2Rad;

            // **깊이는 저마다 다르다** — 다 같으면 밑동이 한 줄로 잘린 것처럼 보인다.
            float sink = down * (1f + i * 0.18f);

            Chunk(root, "Rock" + i, Boulder(), RockMat(), body * sc,
                  new Vector3(Mathf.Cos(a) * dist * Rw0, -sink + dy * full, Mathf.Sin(a) * dist * Rw0),
                  // 저마다 다르게 돌려 **같은 메시가 넷 보이지 않게** 한다.
                  new Vector3(0f, 25f + i * 83f, i * 5f));
        }

        if (v.Shape == Shape.Rock) return;   // 그냥 돌 — 박힌 것이 없다

        // **찔끔 내민다 — 돌 사이로 조금**(2026-09-13, 사용자: *"처음했던 찔끔찔끔 보이는게
        // 훨 좋았어 … 지금은 걍 중간쯤 박혀있잖아"*).
        //
        // **자리를 표면 함수에서 직접 낸다 — 타원체로 짐작하지 않는다.** 처음엔 덩어리를
        // 매끈한 타원체로 놓고 반지름을 계산했는데, 실제 메시는 그 모양이 아니라
        // **움푹한 자리에서 광석이 통째로 드러났다**(계산으로는 25%인데 화면에서는 절반).
        //
        // 이제 덩어리가 **평면으로 깎은 다면체**라 `Radius`가 그 방향의 표면을 정확히 준다.
        // 거기서 **덩이 반지름의 0.72배만큼 안으로 밀면** 어느 방향이든 같은 깊이로 박힌다 —
        // 짐작이 없으니 다시 재 볼 일도 없다.
        const int Count = 30;

        // **`Lump`이 밑동을 0으로 끌어올린 그 값** — 표면 함수는 중심 기준이라 같이 올려야
        // 자리가 맞는다(`seatBottom`이 한 일을 여기서 되풀이하는 것이다).
        float seat = Radius(Vector3.down, BoulderFacets) * 0.95f;

        // 덩이 크기. **덩치를 키운 것은 덩어리였지 광석이 아니었다** — 3x3으로 넓히면서
        // 광석도 같은 배율로 키웠더니 꽃잎처럼 붙었다(0.49m). 지금은 0.23m다.
        float unit = Mathf.Min(foot.x, foot.y) * 0.5f;

        for (int i = 0; i < Count; i++)
        {
            // 각도는 고르게 벌리되 흔든다 — 정확히 등간격이면 **톱니바퀴**로 보인다.
            float th  = (i * (360f / Count) + (float)rng.NextDouble() * 26f - 13f) * Mathf.Deg2Rad;
            // 위아래로도 벌린다. **아래쪽은 묻히니 덜 쓴다.**
            float ph  = Mathf.Lerp(-0.25f, 0.95f, (float)rng.NextDouble());
            float sc  = unit * (0.12f + (float)rng.NextDouble() * 0.06f);

            var dir = new Vector3(Mathf.Cos(ph) * Mathf.Cos(th),
                                  Mathf.Sin(ph),
                                  Mathf.Cos(ph) * Mathf.Sin(th)).normalized;

            // 그 방향의 **실제** 표면. 덩어리와 같은 함수·같은 누르기·같은 밑동 맞춤·같은
            // 스케일을 **순서까지 그대로** 탄다 — 하나라도 빠뜨리면 광석이 표면에서 떨어진다.
            var surf = dir * Radius(dir, BoulderFacets);
            surf.y = surf.y * 0.95f + seat;                    // 누르기 + 밑동 맞춤
            surf = Vector3.Scale(surf, body) + new Vector3(0f, -down, 0f);

            // **표면에서 이만큼 안으로.** 이 값 하나가 *박힌 것*과 *붙인 것*을 가른다 —
            // 0이면 절반이 나오고, 덩이 반지름만큼이면 아예 안 보인다.
            //
            // **덩이의 *평균* 반지름에서 잰다**(2026-09-13에 틀렸다). 전에는 최대 반지름을
            // `1.30`으로 박아 뒀는데, 평면 깎기로 바꾼 뒤 실제 범위가 **0.73~1.83**이 되어
            // 상수가 낡았다. 그 결과 너무 깊이 들어가 **셋 중 하나가 통째로 안 보였다** —
            // 옛 방식에서 가져온 숫자가 새 방식에서 조용히 틀린 자리다.
            //
            // 덩이가 아무 각도로 돌아가 있으므로 **바깥을 향하는 반지름은 뽑기**다. 그래서
            // 최대가 아니라 **평균(1.04)** 에서 재야 대부분이 비슷하게 내민다.
            // 지금 값이면 평균 14%가 드러나고 **아예 안 보이는 것은 없다.**
            const float MeanRadius = 1.04f;   // `Facets("Vein_Nugget", 6, 0.62, 1.05)`을 훑어 잰 값
            var pos = surf - dir * (MeanRadius * 0.70f * sc);

            // **땅속에 박지 않는다** — 묻힌 아래쪽에 두면 아무도 못 본다.
            if (pos.y < MeanRadius * sc * 0.6f) continue;

            // **저마다 다르게 돌려 세운다** — 같은 메시를 같은 각도로 박으면
            // 같은 덩어리가 서른 번 복사된 것으로 보인다.
            var rot = new Vector3((float)rng.NextDouble() * 360f,
                                  (float)rng.NextDouble() * 360f,
                                  (float)rng.NextDouble() * 360f);
            // 세 축을 조금씩 다르게 눌러 **덩이마다 비율이 다르게** 만든다.
            var scale = new Vector3(sc,
                                    sc * (0.72f + (float)rng.NextDouble() * 0.46f),
                                    sc * (0.78f + (float)rng.NextDouble() * 0.40f));
            Chunk(root, "Ore" + i, Nugget(), OreMat(v), scale, pos, rot);
        }
    }

    // 바닥에 굴러다니는 돌 넷. **광맥과 같은 메시를 쓴다** — 모양이 갈리면 같은 세계의
    // 돌이 둘로 보인다. 갈리는 것은 **크기와 콜라이더**뿐이다(칸을 안 막는다).
    //
    // **한 칸을 채운다**(무더기 지름 1m) — 칸이 1m라 그만해야 *칸에 놓인 것*으로 보인다.
    static void BuildPebbles(Transform root, System.Random rng)
    {
        for (int i = 0; i < 4; i++)
        {
            float a  = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float r  = (float)rng.NextDouble() * 0.30f;
            float sc = 0.22f + (float)rng.NextDouble() * 0.16f;

            var rot = new Vector3((float)rng.NextDouble() * 360f,
                                  (float)rng.NextDouble() * 360f,
                                  (float)rng.NextDouble() * 360f);
            Chunk(root, "Rock" + i, Nugget(), RockMat(),
                  new Vector3(sc, sc * 0.78f, sc * 0.92f),
                  new Vector3(Mathf.Cos(a) * r, sc * 0.45f, Mathf.Sin(a) * r), rot);
        }
    }

    static void Chunk(Transform parent, string name, Mesh mesh, Material mat,
                      Vector3 scale, Vector3 pos, Vector3 euler)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale    = scale;
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ── 재질 ────────────────────────────────────────────────────────────────

    // **바위는 벽과 같은 텍스처를 쓴다**(`Yughues T_YFRM_02` — `Rock_Wall.mat`이 그것이다).
    //
    // 광맥 덩어리가 벽과 다른 결이면 **바닥에 얹어 놓은 소품**으로 보인다. 같은 텍스처를
    // 쓰면 *벽에서 떼어낸 것*으로 읽히고, 그게 이 물건의 정체다.
    //
    // **재질은 한 벌이다** — 열다섯이 같은 바위를 쓰므로 색을 얹을 이유가 없다. 갈리는 것은
    // **알갱이·결정의 색**뿐이고 거기만 따로 만든다(`RockRenderer`의 바이옴 색조와 같은 규칙:
    // 종류마다 재질을 만들면 그중 하나는 반드시 뒤처진다).
    const string RockTex = "Assets/assetsss/Yughues/YughuesFreeRockMaterials/Textures/T_YFRM_02";

    static Material _rock;

    static Material RockMat()
    {
        if (_rock != null) return _rock;

        string path = MatDir + "/Vein_Rock.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Lit());
            AssetDatabase.CreateAsset(m, path);
        }

        var d = AssetDatabase.LoadAssetAtPath<Texture2D>(RockTex + "_d.tga");
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(RockTex + "_n.tga");
        if (d == null)
            // **팩은 git 밖이라 경로가 폴더 이름에 기댄다** — 없으면 조용히 회색이 되는데,
            // 그러면 *"텍스처가 왜 안 보이지"*를 셰이더에서 찾게 된다.
            Debug.LogWarning("[광맥] Yughues 바위 텍스처를 못 찾았습니다 — " + RockTex + "_d.tga");

        if (d != null) { m.SetTexture("_BaseMap", d); m.SetTexture("_MainTex", d); }
        if (n != null)
        {
            m.SetTexture("_BumpMap", n);
            m.EnableKeyword("_NORMALMAP");
        }
        m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(m);
        return _rock = m;
    }

    // 결정·알갱이는 **텍스처가 없다** — 금속 알갱이도 보석도 매끈한 면이 맞고, 바위 결을
    // 입히면 *색칠한 돌*로 보인다. 갈리는 것은 색과 발광뿐이다.
    //
    // **색을 이름에 넣는다** — 하나로 쓰면 나중에 만든 쪽이 앞엣것의 색을 덮어써서
    // **루비를 넣는 순간 사파이어까지 빨개진다**(색조 캐시에서 겪은 그 자리).
    static Material OreMat(Vein v)
    {
        // **이름에 색과 금속성을 같이 넣는다** — 색만 넣으면 같은 색을 다른 금속성으로 쓰는
        // 날 **먼저 만든 쪽 설정이 뒤엣것까지 물들인다**(지형 색조 캐시에서 겪은 그 자리).
        string key  = ColorUtility.ToHtmlStringRGB(v.Ore)
                    + "_M" + Mathf.RoundToInt(v.Metal * 100f)
                    + (v.Glow ? "_E" : "");
        string path = MatDir + "/Vein_" + key + ".mat";

        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Lit());
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetColor("_BaseColor", v.Ore);
        m.SetColor("_Color",     v.Ore);

        // **금속은 반사로 읽힌다.** `_Metallic`이 0이면 아무리 회색으로 칠해도 *회색 돌*이다.
        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic",   v.Metal);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", v.Gloss);

        // **URP 발광은 `_EMISSION` 키워드를 같이 켜야 한다** — 색만 넣으면 조용히 안 빛난다.
        if (v.Glow)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", v.Ore * 0.8f);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Shader Lit()
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        return sh != null ? sh : Shader.Find("Standard");
    }

    // ── 메시 ────────────────────────────────────────────────────────────────

    static Mesh _shard, _boulder, _nugget;

    // 굽는 동안만 쓰는 UV 바구니 — `Tri`가 채우고 `Save`가 비운다.
    static readonly List<Vector2> _uv = new();

    // 여섯 면 기둥에 깎은 꼭짓점. **피벗이 밑동**이라 크기를 키우면 위로 자란다
    // (가운데면 땅에 반쯤 묻힌다 — 두 손 도구의 피벗을 밑동에 두는 것과 같은 규칙).
    //
    // **감김을 손으로 맞춘다 — 뒤집히면 통째로 안 그려진다**(2026-09-13에 겪었다).
    // 처음엔 고리를 `i → j` 순으로 적었는데 그러면 법선이 **안쪽**을 향해 바깥 면이 전부
    // 컬링됐고, 화면에는 **속이 훤히 보이는 껍데기**로 나왔다(사용자: *"알갱이들의 속이
    // 보여"*). 받침(`Boulder`)은 멀쩡해서 **원인이 메시라는 표시가 없다** — 재질과
    // 셰이더를 아무리 봐도 안 나온다.
    //
    // **바위 벽이 먼저 겪고 `_Cull Off`로 덮은 그 자리다**(`CLAUDE.md` — *"면마다 네 모서리를
    // 손으로 적는데 방향에 따라 감김이 뒤집혀서 뒤집힌 면이 통째로 안 그려졌다"*). 여기서는
    // 면이 열둘뿐이라 **감김을 제대로 맞춘다** — 양면으로 그리면 속이 비쳐 결정이 유리처럼
    // 보이고, 그리는 값도 두 배다.
    //
    // 재는 법: `Cross(b-a, c-a)`가 **바깥**을 향하면 앞면이다(받침이 그래서 멀쩡했다).
    static Mesh Shard()
    {
        // **한 번 돌 동안만 캐싱한다 — 디스크에서 불러오지 않는다.**
        //
        // 처음엔 에셋이 있으면 그걸 돌려줬는데, 그러면 *"있으면 다시 만들지 않는다"*가
        // 여기서는 **"고쳐도 안 바뀐다"**가 된다 — 감김을 바로잡고 다시 돌려도 **옛 메시가
        // 그대로** 나온다(`MushroomCreator.Paint`가 적어 둔 그 함정이다).
        // 이 도구는 상시라 **돌 때마다 다시 굽는 것이 맞다.**
        if (_shard != null) return _shard;
        _uv.Clear();

        const int N = 6;
        var verts = new List<Vector3>();
        var tris  = new List<int>();

        Vector3 Ring(int i, float y, float r)
        {
            float a = i / (float)N * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        var tip = new Vector3(0f, 1f, 0f);
        var hub = Vector3.zero;
        for (int i = 0; i < N; i++)
        {
            int j = (i + 1) % N;
            // 몸통 — 밑에서 어깨까지. **살짝 벌어졌다가 꼭짓점으로 모인다.**
            Quad(verts, tris, Ring(j, 0f, 0.50f), Ring(i, 0f, 0.50f),
                              Ring(i, 0.72f, 0.62f), Ring(j, 0.72f, 0.62f));
            // 깎은 머리
            Tri(verts, tris, Ring(j, 0.72f, 0.62f), Ring(i, 0.72f, 0.62f), tip);
            // **바닥을 막는다.** 처음엔 *"늘 바위에 묻히니까"*라며 뚫어 뒀는데, 그 뒤에
            // 알갱이를 키우고 바깥으로 눕히면서 **열린 밑동이 화면을 향하게 됐다.**
            // 전제가 바뀌면 그 전제에 기대 아낀 것도 같이 무너진다.
            Tri(verts, tris, hub, Ring(i, 0f, 0.50f), Ring(j, 0f, 0.50f));
        }

        return _shard = Save(verts, tris, "Vein_Shard");
    }

    // **평면 여러 장으로 깎아 만든다 — 반지름을 흔들지 않는다**(2026-09-13에 두 번 틀렸다).
    //
    // 처음엔 팔면체를 쪼개고 **정점마다 반지름을 흔들었다.** 그런데 그 방식은 두 가지를
    // 동시에 못 준다:
    //
    // - 크게 흔들면 **표면이 출렁여** 움푹한 자리에서 광석이 통째로 드러난다
    // - 작게 흔들면 **정팔면체가 그대로 드러나** 둥근 덩어리가 된다(사용자: *"뚱이같아"*)
    //
    // **깎는 것이 맞다.** 실제 바위는 결을 따라 쪼개져 **넓은 평면 몇 장**으로 둘러싸인다.
    // 그래서 **평면 아홉 장의 교집합**(볼록 다면체)을 반지름으로 표본화한다 — 면은 평평하고
    // 모서리는 날카롭고, 무엇보다 **표면이 함수라 광석을 어디에 박을지 정확히 계산된다.**
    //
    // | | 받침 바위 | 광석 덩이 |
    // |---|---|---|
    // | 쪼개기 | 두 번(면 128) — 면이 매끈하게 이어진다 | 한 번(면 32) |
    // | 평면 | 아홉 장, 0.80~1.02 | **여섯 장, 0.62~1.05 — 더 험하다** |
    // | 누르기 | y 0.95배 | 안 누른다 |

    // 깎는 평면 한 장 — 바깥을 향한 법선과 중심에서의 거리.
    struct Facet { public Vector3 N; public float D; }

    // **이름에서 결정적으로 낸다** — 프리팹은 한 번 구워 두는 것이라 다시 돌릴 때마다
    // 모양이 달라지면 버전 관리에 매번 diff가 뜬다.
    static Facet[] Facets(string name, int count, float near, float far)
    {
        var rng = new System.Random(StableSeed(name));
        var f   = new Facet[count];

        for (int i = 0; i < count; i++)
        {
            // 피보나치 구면으로 고르게 벌리고 흔든다 — 아무렇게나 뽑으면 한쪽에 몰려
            // **반대쪽이 안 깎인 채 둥글게 남는다.**
            float y   = 1f - 2f * (i + 0.5f) / count;
            float rad = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float th  = i * 2.399963f + (float)rng.NextDouble() * 0.7f;

            f[i].N = new Vector3(Mathf.Cos(th) * rad,
                                 y + (float)rng.NextDouble() * 0.3f - 0.15f,
                                 Mathf.Sin(th) * rad).normalized;
            f[i].D = near + (float)rng.NextDouble() * (far - near);
        }
        return f;
    }

    // 그 방향에서 다면체의 표면까지. **가장 먼저 만나는 평면이 답이다.**
    static float Radius(Vector3 dir, Facet[] f)
    {
        float r = 2f;
        for (int i = 0; i < f.Length; i++)
        {
            float d = Vector3.Dot(dir, f[i].N);
            if (d > 0.08f) r = Mathf.Min(r, f[i].D / d);
        }
        return r;
    }

    static Facet[] _boulderFacets, _nuggetFacets;

    static Facet[] BoulderFacets =>
        _boulderFacets ??= Facets("Vein_Boulder", 9, 0.80f, 1.02f);

    static Mesh Boulder()
    {
        if (_boulder != null) return _boulder;   // 한 번 돌 동안만 — `Shard` 주석 참조
        return _boulder = Lump("Vein_Boulder", subdiv: 2, facets: BoulderFacets,
                               flatten: 0.95f, seatBottom: true);
    }

    // 바위에 박힌 금속 덩이. **결정이 아니다** — 사진의 철광처럼 모서리가 불규칙하다.
    // **밑동을 안 맞춘다**(`seatBottom: false`) — 반쯤 박히는 물건이라 가운데가 피벗이어야
    // 어느 각도로 돌려도 바위 속에 같은 깊이로 들어간다.
    static Mesh Nugget()
    {
        if (_nugget != null) return _nugget;
        _nuggetFacets ??= Facets("Vein_Nugget", 6, 0.62f, 1.05f);
        return _nugget = Lump("Vein_Nugget", subdiv: 1, facets: _nuggetFacets,
                              flatten: 1f, seatBottom: false);
    }

    static Mesh Lump(string name, int subdiv, Facet[] facets, float flatten, bool seatBottom)
    {
        _uv.Clear();

        // 팔면체 여섯 점
        var p = new[]
        {
            new Vector3( 0,  1,  0), new Vector3( 0, -1,  0),
            new Vector3( 1,  0,  0), new Vector3(-1,  0,  0),
            new Vector3( 0,  0,  1), new Vector3( 0,  0, -1),
        };
        var faces = new List<(Vector3, Vector3, Vector3)>
        {
            (p[0],p[4],p[2]), (p[0],p[2],p[5]), (p[0],p[5],p[3]), (p[0],p[3],p[4]),
            (p[1],p[2],p[4]), (p[1],p[5],p[2]), (p[1],p[3],p[5]), (p[1],p[4],p[3]),
        };
        for (int pass = 0; pass < subdiv; pass++)
        {
            var next = new List<(Vector3, Vector3, Vector3)>();
            foreach (var (a, b, c) in faces)
            {
                Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
                next.Add((a, ab, ca)); next.Add((ab, b, bc));
                next.Add((ca, bc, c)); next.Add((ab, bc, ca));
            }
            faces = next;
        }

        // **방향이 같으면 값도 같다** — 순수 함수라 이웃한 두 면이 그 정점을 다르게 밀지
        // 않는다(난수로 흔들면 틈이 벌어진다). 그리고 **누르기는 나중에 곱한다**:
        // 평면을 곱셈으로 눌러도 평면이라 **면이 평평한 채로 남는다.**
        Vector3 Cut(Vector3 d)
        {
            var v = d * Radius(d, facets);
            v.y *= flatten;
            return v;
        }

        var verts = new List<Vector3>();
        var tris  = new List<int>();
        foreach (var (a, b, c) in faces) Tri(verts, tris, Cut(a), Cut(b), Cut(c));

        // 받침은 **밑동을 원점에 맞춘다** — 피벗이 바닥이어야 `TileToGround` 위에 그대로 앉는다.
        if (seatBottom)
        {
            float minY = float.MaxValue;
            foreach (var v in verts) minY = Mathf.Min(minY, v.y);
            for (int i = 0; i < verts.Count; i++) verts[i] -= new Vector3(0f, minY, 0f);
        }

        return Save(verts, tris, name);
    }

    // **면마다 정점을 따로 둔다 — 평면 음영.** 공유하면 법선이 섞여 매끈해지는데,
    // 폰이 블록 인형인 판에서 광맥만 매끈하면 그것만 다른 게임에서 온 것처럼 보인다.
    //
    // **UV를 같이 낸다 — 안 내면 텍스처가 (0,0) 한 픽셀만 샘플된다.** 바위 텍스처를 물려
    // 놓고도 **단색으로 보이는데**, 원인이 재질도 셰이더도 아니라 메시에 있어 찾기 어렵다.
    //
    // **면의 법선이 가장 큰 축을 버리고 나머지 둘로 투영한다**(면별 평면 투영). 절차로 만든
    // 각진 덩어리라 UV 이음매를 설계할 방법이 없고, 면마다 정점이 따로라 **이음매가 생겨도
    // 보이지 않는다**(어차피 면끼리 안 이어져 있다). 위에서 눌러 찍는 한 축 투영으로 두면
    // **옆면이 늘어난다** — 비탈을 triplanar로 다시 뽑는 것과 같은 자리다.
    static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);

        var n = Vector3.Cross(b - a, c - a).normalized;
        _uv.Add(Project(a, n)); _uv.Add(Project(b, n)); _uv.Add(Project(c, n));
    }

    // m당 타일 수. **미터로 적는다** — 메시가 단위 크기라 프리팹이 스케일로 키우면
    // 무늬도 같이 늘어나는데, 광맥은 1m 언저리라 눈에 안 띈다.
    const float Tile = 1.1f;

    static Vector2 Project(Vector3 p, Vector3 n)
    {
        float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
        if (ay >= ax && ay >= az) return new Vector2(p.x, p.z) * Tile;   // 위·아래 면
        if (ax >= az)             return new Vector2(p.z, p.y) * Tile;   // 동·서 면
        return new Vector2(p.x, p.y) * Tile;                              // 남·북 면
    }

    static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Tri(v, t, a, b, c);
        Tri(v, t, a, c, d);
    }

    static Mesh Save(List<Vector3> verts, List<int> tris, string name)
    {
        var m = new Mesh { name = name };
        m.SetVertices(verts);
        m.SetUVs(0, _uv);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        // **접선을 낸다 — 노멀맵이 그것을 쓴다.** 안 내면 법선 지도가 조용히 안 먹어
        // 면이 평평해 보인다.
        m.RecalculateTangents();
        m.RecalculateBounds();

        // **에셋으로 저장해야 프리팹이 가리킬 수 있고, 가리켜야 빌드에 들어간다.**
        //
        // **폴더를 여기서 보장한다 — 부르는 쪽에서가 아니다.** 처음엔 메뉴(`Rebuild`)에서만
        // 만들었는데, `MetalCreator`가 `BuildOne`을 직접 불러 **그 자리를 안 거치고** 터졌다
        // (*"Parent directory must exist"*). **진입로가 늘 때마다 mkdir이 하나씩 따라붙는
        // 구조라면 언젠가 하나를 빠뜨린다** — 쓰는 자리는 여기 하나뿐이다.
        EnsureFolder(MeshDir);
        AssetDatabase.CreateAsset(m, MeshDir + "/" + name + ".asset");
        _uv.Clear();   // **다음 메시를 위해 비운다** — 안 비우면 두 번째가 첫 번째 UV를 물려받는다
        return m;
    }

    // **`System.IO.Directory.CreateDirectory`로는 모자라다** — 그렇게 만든 폴더는
    // `AssetDatabase`가 아직 모르므로 바로 뒤의 `CreateAsset`이 같은 오류로 죽는다
    // (`Refresh`를 부르면 되지만 그건 프로젝트 전체를 다시 훑는 값이다).
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        // 중간 폴더도 없을 수 있다 — 위에서부터 차례로 만든다.
        var parts = path.Split('/');
        string cur = parts[0];                       // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    // **`string.GetHashCode`를 안 쓴다** — 실행마다 달라져 프리팹이 돌릴 때마다 바뀐다.
    static int StableSeed(string s)
    {
        int h = 17;
        foreach (char c in s) h = h * 31 + c;
        return h;
    }
}
