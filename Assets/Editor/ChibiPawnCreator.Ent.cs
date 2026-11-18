using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

// **엔트 — 파이썬이 뽑은 메시를 들인다**(2026-11-14, 사용자: *"유니티로 옮겨봐"*).
//
// 모양은 미리보기(`Tools/ModelLab/ent/ent.py` v14)에서 사용자가 판마다 보고 고른 것이다. 가지 · 잎이 난수로 자라 수식을 C#으로 옮기면 미리보기와
// 똑같이 못 맞춘다 — 그래서 사람 머리카락(`Pawn/13`)처럼 **파이썬이 메시를 뽑고**(`bake_ent.py` → `ent/baked/` · 깃 밖) 여기서는 읽어 다듬고 붙이기만 한다.
// **모양을 고치면 미리보기를 고치고 → `python bake_ent.py` → `Pawn/17`.** 여기 수식은 없다.
//
// - **종류마다 프리팹 하나**(`3_Prefab/Pawn/Ent/Chibi_Ent_{Oak|Slim|Willow|Snag}`) — 11-14 사용자: *"색만 달라 보이는데 왜 프리팹으로 나뉘어"* →
//   모양은 고르기 축, 색은 팔레트. 잎 모양 넷(`Spine.Pick.Leaf.{A|B|C|D}`) × 계절 색 넷(`PaletteLook` — 봄 · 여름 · 가을 · 늦가을) × 껍질 색 여섯 ×
//   장식. 계절은 무리 안에서도 섞여 나온다(사용자: *"섞을 수 있게 — 세력 세울 때 고민하자"*). 옛 계절 프리팹(`…{종류}Spring`)은 이 이름으로 옮겨
//   guid를 지키고(씬의 인스턴스가 이어진다) 가을 판은 지운다.
// - **계절 색은 메시에 없다** — 잎 · 이끼 · 덩굴 · 겨우살이 · 버들가지의 정점 색은 "칸마다 몫"(뽑기가 `ent.PAL['code']`로 낸다)이고 재질이
//   팔레트를 켠다(`ToonOutline._VertexPal` · 칸 `_Pal0~2`). 재질 값은 첫 팔레트(봄) — 씬 뷰 · 프리팹은 봄이다. 껍질의 이끼 얼룩 색(`_AltColor`)도 팔레트가 든다.
// - **몸은 스킨 메시다** — 정점마다 뼈 둘과 가중치를 파이썬이 적는다(뼈대 선의 캡슐 거리 · 조각마다 고를 뼈 · 둘째 뼈는 이웃만).
//   관 · 얼굴 · 수염 · 겨우살이는 등뼈에 통째로. 뼈대는 사람 것(`BodyJoints` — 같은 계층 · 클립)이되 **관절은 엔트의 자리로 옮긴다**
//   (목록의 `joints` → `BoneBody.Joints` — 아바타를 짓기 전에. 벌어진 다리 · 팔을 사람 뼈 자리(곧게 아래)에서 돌리면 엉뚱한 축으로 돌아
//   손 · 다리가 늘어났다 · 11-14 사용자).
// - **크기는 메시가 든다**(11-14 사용자: *"scale 키우지 말고 처음부터 크기를 키워줘 — 폰 하나가 다리 길이 정도로"*) — 뽑기가 1.7배로 뽑는다
//   (`bake_ent.SCALE` — 엔트 다리 1.07m → 사람 폰 키 1.84m). 종족 몸집(`BodyScale`)은 1.
// - **법선은 파이썬이 낸 거리장의 기울기**를 줄인 점에 그대로 옮긴다(줄인 면에서 다시 재면 울퉁불퉁해 외곽선이 갈라졌다). 줄인 점은 원래 점의 번호를
//   들고 나온다(`Decimate`의 `kept`) — 가장 가까운 점에서 옮겨 오면 맞닿은 다른 조각(손가락 ↔ 다리)의 가중치를 집는다.
// - **선은 엔트 몫**(`EntLine` 3.5cm · 대 · 잎 `EntLineFine` 1.2cm) — 사람 몸 선 6cm를 손가락 · 잔가지에 걸면 검은 띠로 번졌다.
// - **껍질 색은 개체마다 고른다**(`SkinLook` — 재질 이름이 `Chibi_Skin`으로 시작한다 · 후보는 종족 `Ent.asset`의 살색 = 껍질 색 여섯). 껍질 메시의 정점 색은
//   무늬 몫(곱하기)과 이끼 몫(알파 — 셰이더 `_VertexAlt`). 눈처럼 색이 정해진 것은 정점 색 그대로(`Chibi_EntPaint`).
// - **장식은 고르기 축**(`Spine.Pick.{Moss|Beard|Vine|Mistle|Knot1~6}.On@….!Off`) — 붙은 개체도 안 붙은 개체도 있다. 종류마다 잘 붙는 장식이 있다(`EntDecor`).
public static partial class ChibiPawnCreator
{
    const string EntBakedDir = "Tools/ModelLab/ent/baked";
    const string EntData = "Assets/Resources/Data/Units/Ent.asset";

    // 체형 — 미리보기 `HEAVY` · `SLIM`(관절 높이가 여기서 나온다 · 살은 뽑은 메시다).
    //                                                                       밑     엉덩이  허리   가슴   어깨      깊이   손     팔K    다리K  몸통K  팔굵기 다리굵기 어깨둥글림
    static readonly Shape EntHeavyShape = new("EntHeavy", "엔트",      "Male", new[] { 0.26f, 0.28f, 0.27f, 0.32f, 0.34f }, 0.90f, 0.36f, 1.75f, 2.40f, 1.00f, 1f, 1f, 0.45f);
    static readonly Shape EntSlimShape  = new("EntSlim",  "가는 엔트", "Male", new[] { 0.19f, 0.21f, 0.20f, 0.24f, 0.26f }, 0.88f, 0.32f, 1.75f, 2.65f, 1.02f, 1f, 1f, 0.45f);

    static readonly string[] EntKinds = { "Oak", "Slim", "Willow", "Snag" };
    static Shape EntShapeOf(string kind) => kind == "Slim" ? EntSlimShape : EntHeavyShape;

    // 종류마다 잘 붙는 장식(미리보기 `DEFAULT_DECOR`) — 구운 프리팹에서 켜 두고, 판에서는 붙을 몫이 크다.
    static readonly Dictionary<string, string[]> EntDecor = new()
    {
        ["Oak"] = new[] { "Knot1", "Mistle" }, ["Slim"] = new[] { "Moss", "Vine", "Knot4" },
        ["Willow"] = new[] { "Moss", "Beard", "Knot2" }, ["Snag"] = new[] { "Moss", "Knot3", "Knot6" },
    };
    static readonly string[] EntDecorAxes = { "Moss", "Beard", "Vine", "Mistle", "Knot1", "Knot2", "Knot3", "Knot4", "Knot5", "Knot6" };
    const float EntDecorOn = 0.6f, EntDecorRare = 0.15f;
    const string EntLeafAxis = "Leaf", EntLeafDefault = "A";   // 잎 모양 축 — 프리팹 · 씬 뷰에서는 A(판에서는 씨앗이 고른다 · 몫은 고르게)

    // 껍질 색 여섯(미리보기 `BARKS` — 잿빛 · 짙은 밤색 · 붉은 갈색 · 자작 · 서리 · 흑단).
    static readonly Color[] EntBarks =
    {
        new(0.47f, 0.42f, 0.35f), new(0.27f, 0.20f, 0.15f), new(0.46f, 0.27f, 0.18f), new(0.76f, 0.74f, 0.68f), new(0.56f, 0.63f, 0.66f), new(0.215f, 0.20f, 0.25f),
    };

    // 메시마다 남기는 점 수 — 뽑은 몸은 8만 점을 넘는다(사람 치비 몸 하나가 1만 언저리). 가는 것(대 · 잎 — `line` "Fine")은 이미 성겨 안 줄인다.
    // 잎 뭉치(`Leaf` — 관을 다 덮는다)는 장식보다 넉넉히.
    const int EntBarkVerts = 16000, EntPaintVerts = 12000, EntLeafVerts = 10000, EntDecorVerts = 3000;
    const float EntLineWidth = 0.035f, EntLineFineWidth = 0.012f;
    const float EntBodyScale = 1f;   // 종족 몸집(`UnitData.BodyScale`) — 키우지 않는다(크기는 뽑은 메시가 든다 · `bake_ent.SCALE`)

    // 팔레트 재질 — 뽑기의 `mat` 이름 → 재질 이름(이 이름이 `PaletteLook`의 앞머리다).
    static readonly Dictionary<string, string> EntPalMats = new()
        { ["Leaf"] = "Chibi_EntLeaf", ["Moss"] = "Chibi_EntMoss", ["Vine"] = "Chibi_EntVine", ["Mistle"] = "Chibi_EntMistle" };
    const string EntBarkMat = "Chibi_SkinEnt";

    [Serializable] class EntItem { public string mesh, part, file, mat, line, kind; public int verts; }
    [Serializable] class EntJoint { public string kind, bone; public float x, y, z; }
    [Serializable] class EntPalSlot { public string mat; public List<Color> colors; }
    [Serializable] class EntPalette { public string name; public List<EntPalSlot> slots; }
    [Serializable] class EntManifest { public List<EntItem> items; public List<EntJoint> joints; public List<EntPalette> palettes; }

    static EntManifest LoadEntManifest()
    {
        string path = Path.Combine(EntBakedDir, "manifest.json");
        return File.Exists(path) ? JsonUtility.FromJson<EntManifest>(File.ReadAllText(path)) : null;
    }

    static readonly string[] EntMaleNames = { "옹이", "굽은가지", "이끼등", "느린뿌리", "잿빛껍질", "늙은둥치", "가지손", "깊은뿌리", "갈라진나무", "높은그늘" };
    static readonly string[] EntFemaleNames = { "버들", "새순", "잎그늘", "가는가지", "이슬껍질", "물오름", "단풍결", "흰자작" };

    // 엔트 종족 — 없으면 사람을 바탕으로 만든다(엘프와 같은 길). 살색 = 껍질 색 여섯 · 이름은 **비어 있을 때만** 채운다(그 뒤로는 에셋이 답이다).
    static UnitData EnsureEntData()
    {
        var d = AssetDatabase.LoadAssetAtPath<UnitData>(EntData);
        if (d == null)
        {
            var human = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Resources/Data/Units/Human.asset");
            if (human == null) { Debug.LogWarning("[엔트] 바탕 종족(사람)이 없다 — 종족 데이터를 못 만든다."); return null; }
            d = ScriptableObject.CreateInstance<UnitData>();
            EditorUtility.CopySerialized(human, d);
            d.Id = "Ent"; d.RaceName = "엔트";
            d.NamePool = new List<string>(); d.MaleNames = new List<string>(EntMaleNames); d.FemaleNames = new List<string>(EntFemaleNames);
            d.HairTones = new List<Color>(); d.SkinTones = new List<Color>();
            d.BodyScale = EntBodyScale;
            AssetDatabase.CreateAsset(d, EntData);
        }
        if (d.SkinTones == null || d.SkinTones.Count == 0) { d.SkinTones = new List<Color>(EntBarks); EditorUtility.SetDirty(d); AssetDatabase.SaveAssets(); }
        return d;
    }

    // 팔레트 한 벌의 칸들 — (재질 앞머리, 셰이더 값, 색). 껍질의 이끼 얼룩(`BarkAlt`)은 `_AltColor` 하나.
    static IEnumerable<(string mat, string prop, Color c)> EntPalSlots(EntPalette p)
    {
        foreach (var s in p.slots ?? new List<EntPalSlot>())
        {
            if (s.colors == null) continue;
            if (s.mat == "BarkAlt") { if (s.colors.Count > 0) yield return (EntBarkMat, "_AltColor", s.colors[0]); continue; }
            if (!EntPalMats.TryGetValue(s.mat, out var name)) continue;
            for (int i = 0; i < s.colors.Count && i < 8; i++) yield return (name, $"_Pal{i}", s.colors[i]);
        }
    }

    // 재질 — 껍질(이름이 `Chibi_Skin…`이라 `SkinLook`이 개체마다 껍질 색을 얹는다) · 칠한 것(정점 색 그대로) · 팔레트 넷 · 선 둘.
    // 팔레트 재질의 칸 값과 껍질의 이끼 색은 첫 팔레트(씬 뷰 · 프리팹이 보이는 색).
    static Dictionary<string, Material> EntMaterials(Dictionary<string, Material> mats, EntPalette first)
    {
        var m = new Dictionary<string, Material>(mats);
        var bark = Variant(mats["SkinOuter"], EntBarkMat, "_BaseColor", EntBarks[0]);
        bark.SetFloat("_VertexAO", 1f); bark.SetFloat("_VertexAlt", 1f); bark.SetFloat("_VertexPal", 0f);
        EditorUtility.SetDirty(bark);
        m["Bark"] = bark;
        var paint = Variant(mats["SkinOuter"], "Chibi_EntPaint", "_BaseColor", Color.white);
        paint.SetFloat("_VertexAO", 1f); paint.SetFloat("_VertexAlt", 0f); paint.SetFloat("_VertexPal", 0f);
        EditorUtility.SetDirty(paint);
        m["Paint"] = paint;
        foreach (var (key, name) in EntPalMats)
        {
            var pm = Variant(mats["SkinOuter"], name, "_BaseColor", Color.white);
            pm.SetFloat("_VertexAO", 0f); pm.SetFloat("_VertexAlt", 0f); pm.SetFloat("_VertexPal", 1f);
            EditorUtility.SetDirty(pm);
            m[key] = pm;
        }
        if (first != null)
            foreach (var (mat, prop, c) in EntPalSlots(first))
                foreach (var mm in m.Values) if (mm != null && mm.name == mat) mm.SetColor(prop, c);
        // 선 — 몸 선과 같은 재질에 굵기만(스텐실 · 땅 당기기 그대로).
        foreach (var (key, width) in new[] { ("EntLine", EntLineWidth), ("EntLineFine", EntLineFineWidth) })
        {
            var o = Variant(mats["OutlineOuter"], $"Chibi_Outline{key}", "_OutlineColor", mats["OutlineOuter"].GetColor("_OutlineColor"));
            o.CopyPropertiesFromMaterial(mats["OutlineOuter"]);
            o.SetFloat("_OutlineWidth", width);
            o.renderQueue = mats["OutlineOuter"].renderQueue;
            EditorUtility.SetDirty(o);
            m[key] = o;
        }
        return m;
    }

    // 구운 파일 하나를 메시 에셋으로 — 점이 많으면 줄이고(`Decimate`) 색 · 가중치 · 법선은 남은 점의 원래 번호에서 옮겨 온다.
    // 팔레트 재질의 정점 색은 색이 아니라 **몫**이다 — 선형 공간으로 안 바꾼다.
    static Mesh ImportEntMesh(EntItem it, Matrix4x4[] bind)
    {
        int cap = it.line == "Fine" ? int.MaxValue
                : it.part.Contains(".Pick.Leaf.") ? EntLeafVerts
                : it.part.Contains(".Pick.") ? EntDecorVerts
                : it.mat == "Bark" ? EntBarkVerts : EntPaintVerts;
        return ImportBakedMesh(Path.Combine(EntBakedDir, it.file), it.mesh, bind, cap, linear: !EntPalMats.ContainsKey(it.mat));
    }

    // **파이썬이 뽑은 메시 파일 하나 → 메시 에셋**(엔트 · 공룡 악어 · 늑대인간 — `bake_ent.py` · `common/bake_sdf.py`가 같은 꼴로 낸다).
    // 점이 `cap`보다 많으면 줄이고(`Decimate`) 색 · 가중치 · 법선은 남은 점의 원래 번호에서 옮겨 온다.
    // `linear` — 정점 색이 sRGB 색이면 선형 공간으로 바꾼다(팔레트 몫은 색이 아니라 안 바꾼다).
    static Mesh ImportBakedMesh(string path, string meshName, Matrix4x4[] bind, int cap, bool linear)
    {
        if (!File.Exists(path)) { Debug.LogWarning($"[구운 메시] 파일이 없다 — {path}"); return null; }
        Vector3[] pts, nrm; Color[] col; BoneWeight[] bw; var tris = new List<int>();
        using (var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
        using (var br = new BinaryReader(gz))
        {
            int nv = br.ReadInt32(), nt = br.ReadInt32();
            pts = new Vector3[nv];
            for (int i = 0; i < nv; i++) pts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            nrm = new Vector3[nv];
            for (int i = 0; i < nv; i++) nrm[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());   // 거리장의 기울기(대 · 잎은 면 평균)
            tris.Capacity = nt * 3;
            for (int i = 0; i < nt * 3; i++) tris.Add(br.ReadInt32());
            col = new Color[nv];
            for (int i = 0; i < nv; i++) col[i] = new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            var bi = new int[nv * 2];
            for (int i = 0; i < nv * 2; i++) bi[i] = br.ReadInt32();
            bw = new BoneWeight[nv];
            for (int i = 0; i < nv; i++)
            {
                float w0 = br.ReadSingle(), w1 = br.ReadSingle();
                bw[i] = new BoneWeight { boneIndex0 = bi[i * 2], weight0 = w0, boneIndex1 = bi[i * 2 + 1], weight1 = w1 };
            }
        }

        var outPts = pts; var outCol = col; var outBw = bw; var outN = nrm;
        // **덩어리마다 다시 짠다**(2026-11-18 — `TryRemeshBaked`) — 줄이기(`Decimate`)가 잔물결을 세 배로 키웠다. 도구가 없으면 옛 길.
        if (pts.Length > cap && TryRemeshBaked(meshName, pts, nrm, col, bw, tris, cap, out var rp, out var rn, out var rc, out var rw, out var rt))
        {
            outPts = rp; outN = rn; outCol = rc; outBw = rw; tris = rt;
        }
        else if (pts.Length > cap)
        {
            var kept = new List<int>(cap);
            outPts = Decimate(pts, tris, cap, kept: kept);
            outCol = new Color[outPts.Length]; outBw = new BoneWeight[outPts.Length]; outN = new Vector3[outPts.Length];
            for (int i = 0; i < outPts.Length; i++) { int j = kept[i]; outCol[i] = col[j]; outBw[i] = bw[j]; outN[i] = nrm[j]; }
        }
        bool lin = PlayerSettings.colorSpace == ColorSpace.Linear && linear;
        var colors = new List<Color>(outCol.Length);
        foreach (var c in outCol) { var rgb = lin ? new Color(c.r, c.g, c.b).linear : c; colors.Add(new Color(rgb.r, rgb.g, rgb.b, c.a)); }
        var m = SaveSkull(meshName, new Surface(new List<Vector3>(outPts), new List<Vector3>(outN), colors, tris));
        m.boneWeights = outBw;
        m.bindposes = bind;
        EditorUtility.SetDirty(m);
        return m;
    }

    // 프리팹 하나 — 관절은 `Dress`가 세운다(빈 뼈 몸 · 머리 자리만). 여기서는 그 관절에 스킨 메시를 붙인다.
    static Action<GameObject> EntFinish(string kind, List<EntItem> items, List<EntPalette> palettes, Dictionary<string, Material> mats) => root =>
    {
        var model = root.transform.Find("Model");
        if (model == null || !model.TryGetComponent(out PawnRig rig)) { Debug.LogError("[엔트] 관절이 없다."); return; }
        var bones = new[] { rig.Hips, rig.Spine, rig.ArmL, rig.LowerArmL, rig.HandL, rig.ArmR, rig.LowerArmR, rig.HandR,
                            rig.LegL, rig.LowerLegL, rig.FootL, rig.LegR, rig.LowerLegR, rig.FootR, rig.Head };
        // 바인드 자세는 팔을 내린 자세(메시가 그 자세로 뽑혔다) — `Dress`가 아바타를 세우려고 벌린 T자를 잠깐 되돌린다.
        Quaternion qL = rig.ArmL.localRotation, qR = rig.ArmR.localRotation;
        rig.ArmL.localRotation = rig.ArmR.localRotation = Quaternion.identity;
        var bind = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * model.localToWorldMatrix;
        var hipsAt = model.InverseTransformPoint(rig.Hips.position);
        var on = new HashSet<string>(EntDecor[kind]);
        foreach (var it in items)
        {
            var mesh = ImportEntMesh(it, bind);
            if (mesh == null) continue;
            if (!mats.TryGetValue(it.mat, out var skin)) { Debug.LogWarning($"[엔트] 모르는 재질 `{it.mat}` — {it.mesh}"); continue; }
            var go = new GameObject(it.part) { layer = root.layer };
            go.transform.SetParent(model, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh; smr.bones = bones; smr.rootBone = rig.Hips;
            smr.sharedMaterials = new[] { skin, mats[it.line == "Fine" ? "EntLineFine" : "EntLine"] };
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.localBounds = new Bounds(mesh.bounds.center - hipsAt, mesh.bounds.size + Vector3.one * 0.2f);
            // 프리팹 · 씬 뷰에서 켜 둘 것 — 장식은 그 종류에 잘 붙는 것만, 잎은 A만(판에서는 씨앗이 고른다).
            int at = it.part.IndexOf(".Pick.", StringComparison.Ordinal);
            if (at >= 0)
            {
                var seg = it.part.Substring(at + 6).Split('.');
                smr.enabled = seg[0] == EntLeafAxis ? seg.Length > 1 && seg[1] == EntLeafDefault : on.Contains(seg[0]);
            }
        }
        rig.ArmL.localRotation = qL; rig.ArmR.localRotation = qR;

        // 얼굴 비우기 · 장식 몫 · 종족(껍질 색 후보) · 껍질 색 · 계절 팔레트.
        if (root.TryGetComponent(out PawnLook look))
        {
            var so = new SerializedObject(look);
            foreach (var f in new[] { "shapeOverride", "hairOverride", "earOverride", "noseOverride", "tuskOverride" }) so.FindProperty(f).stringValue = "";
            so.FindProperty("fittedTo").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            var ch = new List<PawnLook.PickChance>();
            foreach (var axis in EntDecorAxes) ch.Add(new PawnLook.PickChance(axis, "On", on.Contains(axis) ? EntDecorOn : EntDecorRare));
            look.SetPickChances(ch);   // 잎 모양 축은 안 적는다 — 넷이 고르게
            EditorUtility.SetDirty(look);
        }
        var data = EnsureEntData();
        if (data != null && !Mathf.Approximately(data.BodyScale, EntBodyScale)) { data.BodyScale = EntBodyScale; EditorUtility.SetDirty(data); }
        if (data != null && root.TryGetComponent(out UnitBrain brain))
        {
            var bso = new SerializedObject(brain);
            bso.FindProperty("unitData").objectReferenceValue = data;
            bso.ApplyModifiedPropertiesWithoutUndo();
        }
        if (!root.TryGetComponent(out SkinLook _)) root.AddComponent<SkinLook>();
        if (!root.TryGetComponent(out PaletteLook pl)) pl = root.AddComponent<PaletteLook>();
        var list = new List<PaletteLook.Palette>();
        foreach (var p in palettes ?? new List<EntPalette>())
        {
            var pal = new PaletteLook.Palette { Name = p.name };
            foreach (var (mat, prop, c) in EntPalSlots(p)) pal.Slots.Add(new PaletteLook.Slot { Material = mat, Property = prop, Color = c });
            list.Add(pal);
        }
        pl.SetPalettes(list);
        EditorUtility.SetDirty(pl);
        if (look != null) look.Apply();
    };

    // **옛 계절 프리팹을 종류 하나로**(11-14) — 봄 판을 새 이름으로 옮겨 guid를 지키고(씬에 놓인 봄 엔트가 그대로 이어진다) 가을 판은 지운다.
    static void MigrateEntSeasonPrefabs(string kind)
    {
        string dst = PawnPrefabPaths.PathOf($"Chibi_Ent_{kind}");
        string spring = PawnPrefabPaths.PathOf($"Chibi_Ent_{kind}Spring"), autumn = PawnPrefabPaths.PathOf($"Chibi_Ent_{kind}Autumn");
        if (!File.Exists(dst) && File.Exists(spring))
        {
            string err = AssetDatabase.MoveAsset(spring, dst);
            if (!string.IsNullOrEmpty(err)) Debug.LogWarning($"[엔트] {spring} → {dst} 옮기기 실패: {err}");
        }
        if (File.Exists(autumn)) AssetDatabase.DeleteAsset(autumn);
        foreach (var season in new[] { "Spring", "Autumn" })
        {
            string av = $"Assets/Controller/Avatars/Chibi_Ent_{kind}{season}.asset";
            if (File.Exists(av)) AssetDatabase.DeleteAsset(av);
        }
    }

    // 보기 몸 넷 — 어디서도 안 가리키는 프리팹(`Ent/Chibi_Ent_{종류}`)에 굽는다(리자드맨 · 엘프와 같은 길).
    static int DressEnts(Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases)
    {
        var man = LoadEntManifest();
        if (man == null || man.items == null) { Debug.LogWarning($"[엔트] 목록이 없다 — {EntBakedDir}/manifest.json (`python Tools/ModelLab/ent/bake_ent.py`를 먼저 돌릴 것)"); return 0; }
        if (man.palettes == null || man.palettes.Count == 0) Debug.LogWarning("[엔트] 목록에 팔레트가 없다 — 잎 · 이끼가 흰색이다(`bake_ent.py`를 다시 돌릴 것).");
        var em = EntMaterials(mats, man.palettes != null && man.palettes.Count > 0 ? man.palettes[0] : null);
        // 옛 계절 재질은 이제 아무도 안 쓴다.
        foreach (var season in new[] { "Spring", "Autumn" })
        {
            string old = $"{MatDir}/Chibi_SkinEnt_{season}.mat";
            if (File.Exists(old)) AssetDatabase.DeleteAsset(old);
        }
        int made = 0;
        try
        {
            for (int k = 0; k < EntKinds.Length; k++)
            {
                string kind = EntKinds[k], prefab = $"Chibi_Ent_{kind}";
                EditorUtility.DisplayProgressBar("엔트", prefab, k / (float)EntKinds.Length);
                var items = man.items.FindAll(i => string.Equals(i.kind, kind, StringComparison.OrdinalIgnoreCase));
                if (items.Count == 0) { Debug.LogWarning($"[엔트] {prefab} — 구운 메시가 없다."); continue; }
                PawnPrefabPaths.Ensure(PawnPrefabPaths.FolderOf(prefab));
                MigrateEntSeasonPrefabs(kind);
                string dp = PawnPrefabPaths.PathOf(prefab);
                if (File.Exists(dp)) { File.Copy(Source, dp, true); AssetDatabase.ImportAsset(dp, ImportAssetOptions.ForceUpdate); }
                else                 { AssetDatabase.CopyAsset(Source, dp); AssetDatabase.ImportAsset(dp); }
                var shape = EntShapeOf(kind); var d = DimsOf(shape);
                // 관절 자리 — 뽑기가 낸 엔트의 자리(크기도 그 자리가 든다). 모자라면 사람 자리로 두고 알린다.
                var at = new Dictionary<string, Vector3>();
                foreach (var j in man.joints ?? new List<EntJoint>())
                    if (string.Equals(j.kind, kind, StringComparison.OrdinalIgnoreCase)) at[j.bone] = new Vector3(j.x, j.y, j.z);
                if (at.Count < (int)B.Count) Debug.LogWarning($"[엔트] {prefab} — 관절 자리가 {at.Count}/{(int)B.Count}만 있다(`bake_ent.py`를 다시 돌릴 것).");
                var bb = new BoneBody { ArmX = d.ArmX, ArmZ = 0f, HeadInPlace = true, Joints = at };
                // 목 관절 자리 — 안 보이는 `Head`의 밑이 몸통 꼭대기 위(사람과 같은 자리 · 엔트는 머리가 없고 줄기가 곧 머리다).
                var head = new List<Part> { new("Head", null, new Vector3(0f, d.Top + HeadGap + 0.15f, 0f), Vector3.one * 0.30f, "HeadSkin", on: false) };
                var heads = new Dictionary<string, List<Part>> { ["Male"] = head };
                if (Dress(dp, shape, heads, em, atlases, isSource: false, EntFinish(kind, items, man.palettes, em), bb)) made++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        return made;
    }

    // **엔트만 다시 굽는다** — 얼굴 아틀라스는 구워 둔 것을 읽는다(없으면 `Pawn/3` 먼저). 옛 엔트 메시(이번에 안 만든 `Chibi_Ent_`)만 치운다.
    [MenuItem("DungeonColony/Pawn/17. Create Ents Only (엔트만 — 파이썬이 뽑은 메시를 들인다 · 다른 폰은 안 건드린다)")]
    public static void CreateEntsOnly()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var toon = Shader.Find(ToonShader); var faceSh = Shader.Find(FaceShader); var outer = Shader.Find(OuterShader);
        if (toon == null || faceSh == null || outer == null) { Debug.LogError("[엔트] 셰이더를 못 찾았다."); return; }
        var atlases = new Dictionary<string, ChibiFaceAtlas>();
        foreach (var kind in new[] { "Eyes", "Mouths" })
        {
            var a = AssetDatabase.LoadAssetAtPath<ChibiFaceAtlas>($"{MatDir}/Chibi_{kind}Atlas.asset");
            if (a == null) { Debug.LogError($"[엔트] 얼굴 아틀라스({kind})가 없다 — `Pawn/3`을 먼저 돌릴 것."); return; }
            atlases[kind] = a;
        }
        var mats = EnsureMaterials(toon, faceSh, outer, atlases);
        _saved.Clear();
        Heads = MakeHeads(ChibiFaceLayout.LoadOrCreate());
        int made = DressEnts(mats, atlases);
        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!_saved.Contains(path) && Path.GetFileName(path).StartsWith("Chibi_Ent_")) AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[엔트] 보기 몸 {made}/{EntKinds.Length}벌(`{PawnPrefabPaths.FolderOf("Chibi_Ent_Oak")}`) — 잎 모양 넷 · 계절 팔레트 넷 · 다른 폰은 그대로 · {watch.Elapsed.TotalSeconds:F0}초");
    }
}
