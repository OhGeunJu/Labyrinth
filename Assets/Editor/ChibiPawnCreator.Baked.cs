using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// **공룡 악어 · 늑대인간 — 파이썬이 뽑은 메시를 들인다**(2026-11-15, 사용자: *"리자드 악어랑 늑대 둘다 옮겨봐 유니티로"*).
//
// 엔트(`ChibiPawnCreator.Ent`)와 같은 길이다 — 미리보기의 거리장 수식을 C#으로 손으로 옮기지 않고, 미리보기 스크립트의 `pieces()`를 파이썬이 그대로
// 떠서(`Tools/ModelLab/common/bake_sdf.py`) 정점마다 뼈 둘 · 가중치 · 색을 적어 낸다. 여기서는 읽어 줄이고 붙이기만 한다. **여기 수식은 없다.**
//   - 공룡 악어 — `Tools/ModelLab/lizard/bake_croc.py` → `lizard/baked_croc/`(깃 밖) → `Pawn/18`. 리자드맨(`Pawn/16`)과 따로인 큰 종족.
//   - 늑대인간 — `Tools/ModelLab/werewolf/bake_werewolf.py` → `werewolf/baked/`(깃 밖) → `Pawn/19`. 몸은 치비 몸 그대로 + 역관절 다리 + 늑대 머리.
// **모양을 고치면 미리보기를 고치고 → 뽑기 → 그 굽는 문.** 넣기 전에 `common/baked_view.py`로 색 · 뼈 나눔 · 굽힌 자세를 본다.
//
// - **색은 정점 색 그대로**(`Chibi_BakedPaint` — 흰 살에 정점 색을 곱한다). 털 · 비늘 색 변형을 넣을 때는 팔레트로(프리팹을 나누지 않는다 — 엔트 · 리자드맨).
// - **관절은 그 몸의 자리로 옮긴다**(목록의 `joints` → `BoneBody.Joints`) — 역관절 다리(무릎 앞 · 뒤꿈치 뒤)와 굽은 등은 사람 뼈 자리에서 돌리면 엉뚱한 축으로 돈다.
// - **크기는 메시가 든다** — 늑대인간은 사람 치비 눈금 그대로, 공룡 악어는 미리보기의 1.45배(`BRUTE_S`)가 들어 있다. 종족 몸집(`BodyScale`)은 안 쓴다.
// - **메시는 (머리 · 몸) × 선 갈래**로 뽑혀 온다 — 선은 몸 선(`OutlineOuter`) · 가는 선(이 · 손톱 — `Chibi_OutlineBakedFine`) · 없음(눈동자 · 입 속 · 눈 테).
// - 종족 데이터 · 무리는 아직 — 어디서도 안 가리키는 **보기 몸**이다(리자드맨 · 엘프와 같은 길).
public static partial class ChibiPawnCreator
{
    sealed class BakedKind
    {
        public string Tag, Dir, Prefab, Script;
        public string MatPrefix;   // 팔레트 재질 이름 앞머리(`{앞머리}{재질}` — `PaletteLook`이 이 이름으로 찾는다)
        public Shape Shape;
        public int BodyVerts, HeadVerts, FineVerts, NoneVerts, DecorVerts;
    }

    // 체형 줄은 관절이 다 덮어써 실제로는 쓰이지 않는다(머리 높이 · 아바타 이름만) — 미리보기의 체형 줄을 그대로 적어 둔다.
    //                                                                                밑     엉덩이  허리   가슴   어깨      깊이   손     팔K    다리K  몸통K  팔굵기 다리굵기 어깨둥글림
    static readonly Shape DinoCrocShape = new("DinoCroc", "공룡 악어", "Male", new[] { 0.30f, 0.33f, 0.33f, 0.50f, 0.58f }, 0.90f, 0.42f, 1.15f, 1.95f, 1.10f, 1.40f, 1.05f, 0.30f);
    static readonly Shape WerewolfShape = new("Werewolf", "늑대인간",  "Male", new[] { 0.21f, 0.23f, 0.24f, 0.33f, 0.35f }, 0.84f, 0.32f, 1.12f, 1.22f, 1.02f, 1.15f, 1.10f, 0.42f);

    static readonly BakedKind[] BakedKinds =
    {
        new() { Tag = "공룡 악어", Dir = "Tools/ModelLab/lizard/baked_croc", Prefab = "Chibi_DinoCroc", Script = "Tools/ModelLab/lizard/bake_croc.py", MatPrefix = "Chibi_Dino",
                Shape = DinoCrocShape, BodyVerts = 26000, HeadVerts = 14000, FineVerts = 8000, NoneVerts = 10000, DecorVerts = 5000 },
        new() { Tag = "늑대인간", Dir = "Tools/ModelLab/werewolf/baked", Prefab = "Chibi_Werewolf", Script = "Tools/ModelLab/werewolf/bake_werewolf.py", MatPrefix = "Chibi_Wolf",
                Shape = WerewolfShape, BodyVerts = 22000, HeadVerts = 14000, FineVerts = 8000, NoneVerts = 10000, DecorVerts = 5000 },
    };

    const float BakedFineWidth = 0.012f;

    [Serializable] class BakedItem { public string mesh, part, file, mat, line, region; public int verts; }
    [Serializable] class BakedJoint { public string bone; public float x, y, z; }
    [Serializable] class BakedPalSlot { public string mat; public List<Color> colors; }
    [Serializable] class BakedPalette { public string name; public List<BakedPalSlot> slots; }
    [Serializable] class BakedManifest { public List<BakedItem> items; public List<BakedJoint> joints; public List<BakedPalette> palettes; }

    // **팔레트 재질**(11-15 — 공룡 악어 색 열 벌) — 뽑기의 `mat`이 `Paint`가 아니면 정점 색이 칸 몫이다(`ToonOutline._VertexPal` · 칸 `_Pal0~3`).
    // 재질 이름은 `{앞머리}{mat}`(예 `Chibi_DinoHide` · 비늘 · 배 · 볏 막) · 값은 첫 팔레트(씬 뷰 · 프리팹이 보이는 색) · 개체마다 `PaletteLook`이 고른다.
    static void BakedPaletteMaterials(BakedKind k, BakedManifest man, Dictionary<string, Material> mats)
    {
        var first = man.palettes != null && man.palettes.Count > 0 ? man.palettes[0] : null;
        foreach (var it in man.items)
        {
            if (it.mat == "Paint" || string.IsNullOrEmpty(it.mat) || mats.ContainsKey(it.mat)) continue;
            var pm = Variant(mats["SkinOuter"], k.MatPrefix + it.mat, "_BaseColor", Color.white);
            pm.SetFloat("_VertexAO", 0f); pm.SetFloat("_VertexAlt", 0f); pm.SetFloat("_VertexPal", 1f);
            foreach (var sl in first?.slots ?? new List<BakedPalSlot>())
                if (sl.mat == it.mat && sl.colors != null)
                    for (int i = 0; i < sl.colors.Count && i < 4; i++) pm.SetColor($"_Pal{i}", sl.colors[i]);
            EditorUtility.SetDirty(pm);
            mats[it.mat] = pm;
        }
    }

    // 재질 — 칠한 살(정점 색을 곱한다) · 가는 선. 몸 선은 사람 몸 선(`OutlineOuter`) 그대로.
    static Dictionary<string, Material> BakedMaterials(Dictionary<string, Material> mats)
    {
        var m = new Dictionary<string, Material>(mats);
        var paint = Variant(mats["SkinOuter"], "Chibi_BakedPaint", "_BaseColor", Color.white);
        paint.SetFloat("_VertexAO", 1f); paint.SetFloat("_VertexAlt", 0f); paint.SetFloat("_VertexPal", 0f);
        EditorUtility.SetDirty(paint);
        m["Paint"] = paint;
        var o = Variant(mats["OutlineOuter"], "Chibi_OutlineBakedFine", "_OutlineColor", mats["OutlineOuter"].GetColor("_OutlineColor"));
        o.CopyPropertiesFromMaterial(mats["OutlineOuter"]);
        o.SetFloat("_OutlineWidth", BakedFineWidth);
        o.renderQueue = mats["OutlineOuter"].renderQueue;
        EditorUtility.SetDirty(o);
        m["BakedLineFine"] = o;
        return m;
    }

    // 프리팹 하나 — 관절은 `Dress`가 세운다(빈 뼈 몸 · 안 보이는 머리 자리). 여기서는 그 관절에 스킨 메시를 붙인다.
    static Action<GameObject> BakedFinish(BakedKind k, BakedManifest man, Dictionary<string, Material> mats) => root =>
    {
        var model = root.transform.Find("Model");
        if (model == null || !model.TryGetComponent(out PawnRig rig)) { Debug.LogError($"[{k.Tag}] 관절이 없다."); return; }
        var bones = new[] { rig.Hips, rig.Spine, rig.ArmL, rig.LowerArmL, rig.HandL, rig.ArmR, rig.LowerArmR, rig.HandR,
                            rig.LegL, rig.LowerLegL, rig.FootL, rig.LegR, rig.LowerLegR, rig.FootR, rig.Head };
        // 바인드 자세는 팔을 내린 자세(메시가 그 자세로 뽑혔다) — `Dress`가 아바타를 세우려고 벌린 T자를 잠깐 되돌린다.
        Quaternion qL = rig.ArmL.localRotation, qR = rig.ArmR.localRotation;
        rig.ArmL.localRotation = rig.ArmR.localRotation = Quaternion.identity;
        var bind = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * model.localToWorldMatrix;
        var hipsAt = model.InverseTransformPoint(rig.Hips.position);
        foreach (var it in man.items)
        {
            bool head = string.IsNullOrEmpty(it.region) ? it.mesh.Contains("_Head") : it.region == "Head";
            // 고르기 조각 중 장식(뿔 · 볏 · 골판 · 꼬리 끝)은 작게 — 머리 모양 축(`Pick.Head.`)은 머리 살이라 머리 몫.
            bool decor = it.part.Contains(".Pick.") && !it.part.Contains(".Pick.Head.");
            int cap = it.line == "Fine" ? k.FineVerts : it.line == "None" ? k.NoneVerts : decor ? k.DecorVerts : head ? k.HeadVerts : k.BodyVerts;
            bool paint = string.IsNullOrEmpty(it.mat) || it.mat == "Paint";
            if (!mats.TryGetValue(paint ? "Paint" : it.mat, out var skin)) { Debug.LogWarning($"[{k.Tag}] 모르는 재질 `{it.mat}` — {it.mesh}"); continue; }
            var mesh = ImportBakedMesh(Path.Combine(k.Dir, it.file), it.mesh, bind, cap, linear: paint);   // 팔레트 몫은 색이 아니라 선형으로 안 바꾼다
            if (mesh == null) continue;
            // **선 법선을 굽지 않는다 — 정점 법선 그대로**(`w = -2` · 11-15 사용자: *"늑대인간은 외곽선이 이상해"*). 정점 법선이 이미 거리장의 기울기라 매끈한데,
            // 섞기(`OutlineNormals` — 6cm 안의 면 법선)는 한 메시에 겹쳐 묶인 조각(털 껍데기 · 몸통 · 바지 · 털 술)의 면을 같이 섞어 방향이 뒤엉켰고,
            // 껍데기가 실루엣 밖으로 털처럼 찢어져 번졌다(`common/mesh_view.py`로 둘을 나란히 그려 확인).
            var keep = new Vector4[mesh.vertexCount];
            for (int i = 0; i < keep.Length; i++) keep[i] = new Vector4(0f, 0f, 0f, -2f);
            mesh.tangents = keep;
            EditorUtility.SetDirty(mesh);
            var go = new GameObject(it.part) { layer = root.layer };
            go.transform.SetParent(model, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh; smr.bones = bones; smr.rootBone = rig.Hips;
            smr.sharedMaterials = it.line == "None" ? new[] { skin }
                                : new[] { skin, mats[it.line == "Fine" ? "BakedLineFine" : "OutlineOuter"] };
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.localBounds = new Bounds(mesh.bounds.center - hipsAt, mesh.bounds.size + Vector3.one * 0.2f);
        }
        rig.ArmL.localRotation = qL; rig.ArmR.localRotation = qR;

        // 사람 얼굴 축은 비운다(이 몸은 머리를 메시로 든다).
        if (root.TryGetComponent(out PawnLook look))
        {
            var so = new SerializedObject(look);
            foreach (var f in new[] { "shapeOverride", "hairOverride", "earOverride", "noseOverride", "tuskOverride" }) so.FindProperty(f).stringValue = "";
            so.FindProperty("fittedTo").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            look.SetPickChances(new List<PawnLook.PickChance>());   // 고르기 축(머리 모양 · 뿔 · 볏 · 꼬리 끝 …)은 고르게 — 조각 이름이 축을 든다
            EditorUtility.SetDirty(look);
        }
        // 색 — 팔레트가 있으면 개체마다 고른다(`PaletteLook` — 조각 재질이 칸 몫을 든다).
        if (man.palettes != null && man.palettes.Count > 0)
        {
            if (!root.TryGetComponent(out PaletteLook pl)) pl = root.AddComponent<PaletteLook>();
            var list = new List<PaletteLook.Palette>();
            foreach (var p in man.palettes)
            {
                var pal = new PaletteLook.Palette { Name = p.name };
                foreach (var sl in p.slots ?? new List<BakedPalSlot>())
                    for (int i = 0; sl.colors != null && i < sl.colors.Count && i < 4; i++)
                        pal.Slots.Add(new PaletteLook.Slot { Material = k.MatPrefix + sl.mat, Property = $"_Pal{i}", Color = sl.colors[i] });
                list.Add(pal);
            }
            pl.SetPalettes(list);
            EditorUtility.SetDirty(pl);
        }
        if (look != null) look.Apply();
    };

    static bool DressBaked(BakedKind k, Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases)
    {
        string manPath = Path.Combine(k.Dir, "manifest.json");
        if (!File.Exists(manPath)) { Debug.LogWarning($"[{k.Tag}] 목록이 없다 — {manPath} (`python {k.Script}`를 먼저 돌릴 것)"); return false; }
        var man = JsonUtility.FromJson<BakedManifest>(File.ReadAllText(manPath));
        if (man?.items == null || man.items.Count == 0) { Debug.LogWarning($"[{k.Tag}] 구운 메시가 없다 — {manPath}"); return false; }
        PawnPrefabPaths.Ensure(PawnPrefabPaths.FolderOf(k.Prefab));
        string dp = PawnPrefabPaths.PathOf(k.Prefab);
        // 공룡 악어는 처음에 제 폴더(`Pawn/DinoCroc/`)에 구웠다 — 리자드맨 폴더로 옮긴다(guid를 지켜 씬 인스턴스가 이어진다 · 빈 폴더는 지운다).
        foreach (var old in new[] { $"{PawnPrefabPaths.Root}/DinoCroc/{k.Prefab}.prefab" })
        {
            if (old == dp || !File.Exists(old)) continue;
            if (!File.Exists(dp)) { string err = AssetDatabase.MoveAsset(old, dp); if (!string.IsNullOrEmpty(err)) Debug.LogWarning($"[{k.Tag}] {old} → {dp} 옮기기 실패: {err}"); }
            else AssetDatabase.DeleteAsset(old);
            string folder = Path.GetDirectoryName(old).Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder) && AssetDatabase.FindAssets("", new[] { folder }).Length == 0) AssetDatabase.DeleteAsset(folder);
        }
        if (File.Exists(dp)) { File.Copy(Source, dp, true); AssetDatabase.ImportAsset(dp, ImportAssetOptions.ForceUpdate); }
        else                 { AssetDatabase.CopyAsset(Source, dp); AssetDatabase.ImportAsset(dp); }
        var at = new Dictionary<string, Vector3>();
        foreach (var j in man.joints ?? new List<BakedJoint>()) at[j.bone] = new Vector3(j.x, j.y, j.z);
        if (at.Count < (int)B.Count) Debug.LogWarning($"[{k.Tag}] 관절 자리가 {at.Count}/{(int)B.Count}만 있다(`{k.Script}`를 다시 돌릴 것).");
        var d = DimsOf(k.Shape);
        var bb = new BoneBody { ArmX = d.ArmX, ArmZ = 0f, HeadInPlace = true, Joints = at };
        // 안 보이는 `Head` — 목 관절 자리 위(머리는 메시가 든다 · 관절은 `joints`가 옮긴다).
        var headAt = at.TryGetValue("Head", out var h) ? h : new Vector3(0f, d.Top + HeadGap, 0f);
        var head = new List<Part> { new("Head", null, headAt + new Vector3(0f, 0.15f, 0f), Vector3.one * 0.30f, "HeadSkin", on: false) };
        var heads = new Dictionary<string, List<Part>> { ["Male"] = head };
        var km = new Dictionary<string, Material>(mats);
        BakedPaletteMaterials(k, man, km);
        return Dress(dp, k.Shape, heads, km, atlases, isSource: false, BakedFinish(k, man, km), bb);
    }

    static void CreateBakedOnly(BakedKind k)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var toon = Shader.Find(ToonShader); var faceSh = Shader.Find(FaceShader); var outer = Shader.Find(OuterShader);
        if (toon == null || faceSh == null || outer == null) { Debug.LogError($"[{k.Tag}] 셰이더를 못 찾았다."); return; }
        var atlases = new Dictionary<string, ChibiFaceAtlas>();
        foreach (var kind in new[] { "Eyes", "Mouths" })
        {
            var a = AssetDatabase.LoadAssetAtPath<ChibiFaceAtlas>($"{MatDir}/Chibi_{kind}Atlas.asset");
            if (a == null) { Debug.LogError($"[{k.Tag}] 얼굴 아틀라스({kind})가 없다 — `Pawn/3`을 먼저 돌릴 것."); return; }
            atlases[kind] = a;
        }
        var mats = BakedMaterials(EnsureMaterials(toon, faceSh, outer, atlases));
        _saved.Clear();
        Heads = MakeHeads(ChibiFaceLayout.LoadOrCreate());
        bool ok = DressBaked(k, mats, atlases);
        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!_saved.Contains(path) && Path.GetFileName(path).StartsWith(k.Prefab + "_")) AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[{k.Tag}] 보기 몸 {(ok ? 1 : 0)}/1벌(`{PawnPrefabPaths.PathOf(k.Prefab)}`) — 다른 폰은 그대로 · {watch.Elapsed.TotalSeconds:F0}초");
    }

    // `Pawn/3`(모든 폰을 다시 굽는다)이 부른다 — 빼먹으면 그 끝 청소가 이 메시를 지운다.
    static int DressBakedAll(Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases)
    {
        var bm = BakedMaterials(mats); int made = 0;
        foreach (var k in BakedKinds) if (DressBaked(k, bm, atlases)) made++;
        return made;
    }

    [MenuItem("DungeonColony/Pawn/18. Create Dino Croc Only (공룡 악어만 — 파이썬이 뽑은 메시를 들인다 · 다른 폰은 안 건드린다)")]
    public static void CreateDinoCrocOnly() => CreateBakedOnly(BakedKinds[0]);

    [MenuItem("DungeonColony/Pawn/19. Create Werewolf Only (늑대인간만 — 파이썬이 뽑은 메시를 들인다 · 다른 폰은 안 건드린다)")]
    public static void CreateWerewolfOnly() => CreateBakedOnly(BakedKinds[1]);
}
