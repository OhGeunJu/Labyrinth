using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// **뱀파이어 — 파이썬이 뽑은 메시를 들인다**(2026-11-18, 사용자: 미리보기 확정 뒤 *"ㄱㄱ"*).
//
// 늑대인간 · 공룡 악어(`ChibiPawnCreator.Baked`)와 같은 길이다 — 미리보기(`Tools/ModelLab/vampire/vampire.py` · `lord_hair.py`)의 조각을
// 파이썬이 그대로 떠서(`vampire/bake_vampire.py` → `vampire/baked/{몸}/`(깃 밖)) 정점마다 뼈 둘 · 가중치 · 색을 적어 낸다. **여기 수식은 없다.**
// **모양을 고치면 미리보기를 고치고 → `python bake_vampire.py` → `Pawn/22`.** 넣기 전에 `common/baked_view.py`로 색 · 뼈 나눔 · 굽힌 자세를 본다.
//
// - 몸 넷 — 로드 남 · 여(순혈 귀족) · 노예 남 · 여(물려서 된 하급). 몸은 사람 치비 몸 그대로(머리가 뜬다).
// - **로드 머리는 고르기 축** — `Spine.Pick.Hair.{Curtain|Slick|Long|LowTail}` × `Spine.Pick.Bangs.{On|Off}`(더듬이 — 장발에는 안 붙는다).
//   머리색은 팔레트(`Chibi_VampHairPal` · `PaletteLook` — 먹 · 은 · 갈색 · 적갈 · 포도주).
// - 읽어 붙이는 몸통은 `Baked`의 것을 그대로 쓴다(`BakedFinish` · `BakedPaletteMaterials` · `BakedMaterials`) — 다른 것은 **여자 몸**뿐이다:
//   `Dress`가 머리 부위를 성별 키(`headParts[s.Gender]`)로 찾는데 `DressBaked`는 남자 키만 넣어서, 여기서 둘 다 넣는다(`DressVampire`).
// - 종족 데이터 · 무리는 아직 — 어디서도 안 가리키는 **보기 몸**이다.
public static partial class ChibiPawnCreator
{
    // 정적 필드 초기화 차례는 파일 사이에서 정해지지 않는다 — `Shapes`(다른 파일)를 쓰므로 그때그때 만든다.
    static BakedKind[] VampireKinds() => new BakedKind[]
    {
        Vampire("뱀파이어 로드",   "lord",    "Chibi_VampireLord",    Shapes[0]),
        Vampire("뱀파이어 로드(여)", "lordF",   "Chibi_VampireLordF",   Shapes[1]),
        Vampire("뱀파이어 노예",   "thrall",  "Chibi_VampireThrall",  Shapes[0]),
        Vampire("뱀파이어 노예(여)", "thrallF", "Chibi_VampireThrallF", Shapes[1]),
    };

    static BakedKind Vampire(string tag, string dir, string prefab, Shape shape) => new()
    {
        Tag = tag, Dir = $"Tools/ModelLab/vampire/baked/{dir}", Prefab = prefab, Script = "Tools/ModelLab/vampire/bake_vampire.py", MatPrefix = "Chibi_Vamp",
        // 머리카락 · 더듬이는 고르기 조각이라 `DecorVerts` 몫을 받는다(장발은 크다).
        Shape = shape, BodyVerts = 26000, HeadVerts = 16000, FineVerts = 6000, NoneVerts = 8000, DecorVerts = 12000,
    };

    static bool DressVampire(BakedKind k, Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases)
    {
        string manPath = Path.Combine(k.Dir, "manifest.json");
        if (!File.Exists(manPath)) { Debug.LogWarning($"[{k.Tag}] 목록이 없다 — {manPath} (`python {k.Script}`를 먼저 돌릴 것)"); return false; }
        var man = JsonUtility.FromJson<BakedManifest>(File.ReadAllText(manPath));
        if (man?.items == null || man.items.Count == 0) { Debug.LogWarning($"[{k.Tag}] 구운 메시가 없다 — {manPath}"); return false; }
        PawnPrefabPaths.Ensure(PawnPrefabPaths.FolderOf(k.Prefab));
        string dp = PawnPrefabPaths.PathOf(k.Prefab);
        if (File.Exists(dp)) { File.Copy(Source, dp, true); AssetDatabase.ImportAsset(dp, ImportAssetOptions.ForceUpdate); }
        else                 { AssetDatabase.CopyAsset(Source, dp); AssetDatabase.ImportAsset(dp); }
        var at = new Dictionary<string, Vector3>();
        foreach (var j in man.joints ?? new List<BakedJoint>()) at[j.bone] = new Vector3(j.x, j.y, j.z);
        if (at.Count < (int)B.Count) Debug.LogWarning($"[{k.Tag}] 관절 자리가 {at.Count}/{(int)B.Count}만 있다(`{k.Script}`를 다시 돌릴 것).");
        var d = DimsOf(k.Shape);
        var bb = new BoneBody { ArmX = d.ArmX, ArmZ = 0f, HeadInPlace = true, Joints = at };
        var headAt = at.TryGetValue("Head", out var h) ? h : new Vector3(0f, d.Top + HeadGap, 0f);
        var head = new List<Part> { new("Head", null, headAt + new Vector3(0f, 0.15f, 0f), Vector3.one * 0.30f, "HeadSkin", on: false) };
        var heads = new Dictionary<string, List<Part>> { ["Male"] = head, ["Female"] = head };   // 여자 몸도(`Dress`는 성별 키로 찾는다)
        var km = new Dictionary<string, Material>(mats);
        BakedPaletteMaterials(k, man, km);
        var finish = BakedFinish(k, man, km);
        return Dress(dp, k.Shape, heads, km, atlases, isSource: false, root => { finish(root); HairLines(root, km); }, bb);
    }

    // **머리카락 · 더듬이의 선은 사람 머리카락과 같게** — 털 선(`HangLine`) + 선 법선(`OutlineNormals`)(11-19 첫 화면: 몸 선이 걸려 머리카락 위에 검은 얼룩이 졌다).
    // 털의 선은 한 벌이라는 규칙(`LineFor`)을 이 몸에도 — `BakedFinish`는 모든 조각에 몸 선을 걸므로 여기서 갈아 끼운다.
    static void HairLines(GameObject root, Dictionary<string, Material> mats)
    {
        if (!mats.TryGetValue(HangLine, out var hang)) { Debug.LogWarning("[뱀파이어] 털 선 재질이 없다 — 머리카락에 몸 선이 남는다."); return; }
        // **얼굴 살은 얼굴 표시를 적는다**(11-19 — 앞머리 선이 얼굴 위에 그어졌다). 털 선(`HangLine`)은 스텐실 *제 번호 + 128*(얼굴)인 자리만 비키는데,
        // 구운 칠 재질(`Chibi_BakedPaint`)은 얼굴 표시 없이 번호만 적어 얼굴을 남의 자리로 봤다. 사람 얼굴 살(`HeadSkin`)처럼 얼굴 표시를 켠 사본을 머리 메시에만.
        Material paintFace = null;
        if (mats.TryGetValue("Paint", out var paint))
        {
            paintFace = Variant(paint, "Chibi_BakedPaintFace", "_BaseColor", Color.white);
            paintFace.SetFloat("_StencilFace", 1f); paintFace.SetFloat("_StencilRef", 1 + PawnOutlineId.FaceBit);
            EditorUtility.SetDirty(paintFace);
        }
        // 머리카락 팔레트 재질도 얼굴 표시를 적는다(머리의 일부 — 11-19 사용자: *"앞머리 더듬이가 머리카락에서만 외곽선이 생기네"*).
        // 몸 살을 복사해 만들어져(`BakedPaletteMaterials`) 번호만 적어서, 더듬이의 털 선이 머리카락 위에 그어졌다. 굽을 때마다 다시 만들어지므로 여기서 매번 켠다.
        if (mats.TryGetValue("HairPal", out var hairPal))
        {
            hairPal.SetFloat("_StencilFace", 1f); hairPal.SetFloat("_StencilRef", 1 + PawnOutlineId.FaceBit);
            EditorUtility.SetDirty(hairPal);
        }
        Material fineFace = null;   // 송곳니 둘레 가는 선 — 얼굴 표시를 읽는 사본
        if (mats.TryGetValue("BakedLineFine", out var fineSrc))
        {
            fineFace = Variant(fineSrc, "Chibi_OutlineBakedFineFace", "_OutlineColor", fineSrc.GetColor("_OutlineColor"));
            fineFace.CopyPropertiesFromMaterial(fineSrc);
            fineFace.SetFloat("_StencilFace", 1f); fineFace.SetFloat("_StencilRef", 1 + PawnOutlineId.FaceBit);
            fineFace.renderQueue = fineSrc.renderQueue;
            EditorUtility.SetDirty(fineFace);
        }
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (paintFace != null && smr.name.StartsWith("Spine.Baked.Head"))
            {
                // 얼굴 살의 선도 얼굴 표시를 읽는 선으로(사람 · 감염자 얼굴 살 = `HangLine`) — 몸 선은 번호만 비켜, 얼굴 표시를 적자 제 얼굴(코 · 입) 위에 그어졌다(11-19).
                var hm = smr.sharedMaterials;
                if (hm.Length > 0 && hm[0] == paint) hm[0] = paintFace;
                if (hm.Length > 1 && mats.TryGetValue("OutlineOuter", out var bodyLine) && hm[1] == bodyLine) hm[1] = hang;
                if (hm.Length > 1 && fineFace != null && mats.TryGetValue("BakedLineFine", out var fine) && hm[1] == fine) hm[1] = fineFace;
                smr.sharedMaterials = hm;
                continue;
            }
            if (!smr.name.StartsWith("Spine.Pick.Hair.") && !smr.name.StartsWith("Spine.Pick.Bangs.")) continue;
            var ms = smr.sharedMaterials;
            if (ms.Length < 2) continue;
            ms[1] = hang; smr.sharedMaterials = ms;
            // 선 법선도 사람 머리카락처럼 굽는다(`ImportHair` — 6cm 안의 면 법선을 섞는다). `BakedFinish`는 정점 법선 그대로(`w = -2`)를 적는데,
            // 앞머리가 꺾이는 가장자리에서 그 법선이 갈라져 선이 튀었다(11-19 사용자: *"앞머리 외곽선도 원래 치비 앞머리처럼"*).
            if (smr.sharedMesh != null) { OutlineNormals(smr.sharedMesh, OutlineSmoothRadius); EditorUtility.SetDirty(smr.sharedMesh); }
        }
    }

    // `Pawn/3`(모든 폰을 다시 굽는다)이 부른다 — 빼먹으면 그 끝 청소가 이 메시를 지운다.
    static int DressVampireAll(Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases)
    {
        var bm = BakedMaterials(mats); int made = 0;
        foreach (var k in VampireKinds()) if (DressVampire(k, bm, atlases)) made++;
        return made;
    }

    [MenuItem("DungeonColony/Pawn/22. Create Vampire Only (뱀파이어만 — 로드 · 노예 남녀 넷 · 파이썬이 뽑은 메시를 들인다 · 다른 폰은 안 건드린다)")]
    public static void CreateVampireOnly()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var toon = Shader.Find(ToonShader); var faceSh = Shader.Find(FaceShader); var outer = Shader.Find(OuterShader);
        if (toon == null || faceSh == null || outer == null) { Debug.LogError("[뱀파이어] 셰이더를 못 찾았다."); return; }
        var atlases = new Dictionary<string, ChibiFaceAtlas>();
        foreach (var kind in new[] { "Eyes", "Mouths" })
        {
            var a = AssetDatabase.LoadAssetAtPath<ChibiFaceAtlas>($"{MatDir}/Chibi_{kind}Atlas.asset");
            if (a == null) { Debug.LogError($"[뱀파이어] 얼굴 아틀라스({kind})가 없다 — `Pawn/3`을 먼저 돌릴 것."); return; }
            atlases[kind] = a;
        }
        var mats = BakedMaterials(EnsureMaterials(toon, faceSh, outer, atlases));
        _saved.Clear();
        Heads = MakeHeads(ChibiFaceLayout.LoadOrCreate());
        var kinds = VampireKinds(); int made = 0;
        foreach (var k in kinds) if (DressVampire(k, mats, atlases)) made++;
        // 이번에 안 만든 뱀파이어 메시는 지운다(`Chibi_VampireLord_` · `Chibi_VampireLordF_` … — 이름 끝의 `_`까지 맞춰 서로 안 지운다).
        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid), file = Path.GetFileName(path);
            if (_saved.Contains(path)) continue;
            foreach (var k in kinds) if (file.StartsWith(k.Prefab + "_")) { AssetDatabase.DeleteAsset(path); break; }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[뱀파이어] 보기 몸 {made}/{kinds.Length}벌(`{PawnPrefabPaths.FolderOf(kinds[0].Prefab)}`) — 다른 폰은 그대로 · {watch.Elapsed.TotalSeconds:F0}초");
    }
}
