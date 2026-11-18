using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// **두더지 · 괴물쥐 — 파이썬이 뽑은 네 발 짐승 메시를 들인다**(2026-11-18, 사용자: *"넣어"*).
//
// 공룡 악어 · 늑대인간(`ChibiPawnCreator.Baked`)과 같은 길이지만 **뼈가 네발 규격이다** — 사람 뼈 열다섯 · 휴머노이드 아바타가 아니라
// `PawnRig` 네발(`Model/Body/{Head, Tail, LegFL, LegFR, LegBL, LegBR}`) + 꼬리 마디 사슬(`Tail/Tail1/…` — `TailSway`가 흔든다).
//   - 두더지 — `Tools/ModelLab/mole/bake_mole.py` → `mole/baked/`(깃 밖) → `Pawn/20`.
//   - 괴물쥐 — `Tools/ModelLab/rat/bake_rat.py` → `rat/baked/`(깃 밖) → `Pawn/21`.
// **모양을 고치면 미리보기를 고치고 → 뽑기 → 그 굽는 문.** 넣기 전에 `common/baked_view.py`로 뼈 나눔 · 굽힌 자세를 본다.
//
// - **몸 틀은 늑대 프리팹을 복사한다**(`Enemy_Wolf` — 이동 · 뇌 · 콜라이더 · 네발 애니메이터). `Model` 아래만 비우고 관절 · 스킨 메시를 새로 세운다.
//   종족 데이터 · 무리는 아직이라 **늑대 데이터를 든 보기 몸**이다(어디서도 안 가리킨다 — 리자드맨 · 엘프와 같은 길).
// - **관절은 목록이 든다**(`bones` 차례 · `parents` · `joints` 자리) — 회전 없이 `Model` 축 그대로 세운다(클립이 관절 이름 경로로 묶인다).
// - **색은 정점 색 그대로**(`Chibi_BakedPaint`) · 선은 몸 선 · 가는 선(이 · 발톱 · 수염) · 없음(눈 · 반짝임 · 입 속).
// - **크기는 메시가 든다**(두더지는 미리보기의 1.6배가 들어 있다) · 부딪히는 몸은 늑대 그대로(한 칸).
public static partial class ChibiPawnCreator
{
    sealed class QuadKind
    {
        public string Tag, Dir, Prefab, Script;
        public int BodyVerts, HeadVerts, FineVerts, NoneVerts, ThinVerts;
    }

    const string QuadSource = PawnPrefabPaths.Root + "/Beast/Enemy_Wolf.prefab";

    static readonly QuadKind[] QuadKinds =
    {
        new() { Tag = "두더지", Dir = "Tools/ModelLab/mole/baked", Prefab = "Chibi_Mole", Script = "Tools/ModelLab/mole/bake_mole.py",
                BodyVerts = 24000, HeadVerts = 12000, FineVerts = 6000, NoneVerts = 28000, ThinVerts = 12000 },
        new() { Tag = "괴물쥐", Dir = "Tools/ModelLab/rat/baked", Prefab = "Chibi_Rat", Script = "Tools/ModelLab/rat/bake_rat.py",
                BodyVerts = 20000, HeadVerts = 14000, FineVerts = 6000, NoneVerts = 26000, ThinVerts = 16000 },
    };

    [Serializable] class QuadParent { public string bone, parent; }
    [Serializable] class QuadManifest { public List<BakedItem> items; public List<BakedJoint> joints; public List<string> bones; public List<QuadParent> parents; }

    static bool DressQuad(QuadKind k, Dictionary<string, Material> mats)
    {
        string manPath = Path.Combine(k.Dir, "manifest.json");
        if (!File.Exists(manPath)) { Debug.LogWarning($"[{k.Tag}] 목록이 없다 — {manPath} (`python {k.Script}`를 먼저 돌릴 것)"); return false; }
        var man = JsonUtility.FromJson<QuadManifest>(File.ReadAllText(manPath));
        if (man?.items == null || man.items.Count == 0 || man.bones == null || man.bones.Count == 0)
        { Debug.LogWarning($"[{k.Tag}] 구운 메시 · 뼈가 없다 — {manPath}"); return false; }
        if (!File.Exists(QuadSource)) { Debug.LogError($"[{k.Tag}] 몸 틀이 없다 — {QuadSource}"); return false; }

        PawnPrefabPaths.Ensure(PawnPrefabPaths.FolderOf(k.Prefab));
        string dp = PawnPrefabPaths.PathOf(k.Prefab);
        // 있으면 속만 갈아 끼운다(guid를 지켜 씬 인스턴스가 이어진다).
        if (File.Exists(dp)) { File.Copy(QuadSource, dp, true); AssetDatabase.ImportAsset(dp, ImportAssetOptions.ForceUpdate); }
        else                 { AssetDatabase.CopyAsset(QuadSource, dp); AssetDatabase.ImportAsset(dp); }

        var at = new Dictionary<string, Vector3>();
        foreach (var j in man.joints ?? new List<BakedJoint>()) at[j.bone] = new Vector3(j.x, j.y, j.z);
        var parentOf = new Dictionary<string, string>();
        foreach (var p in man.parents ?? new List<QuadParent>()) parentOf[p.bone] = p.parent;

        var root = PrefabUtility.LoadPrefabContents(dp);
        try
        {
            root.name = k.Prefab;
            var model = root.transform.Find("Model");
            if (model == null) { Debug.LogError($"[{k.Tag}] 몸 틀에 `Model`이 없다."); return false; }
            for (int i = model.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(model.GetChild(i).gameObject);
            if (!model.TryGetComponent(out PawnRig rig)) rig = model.gameObject.AddComponent<PawnRig>();
            rig.Kind = PawnRig.BodyKind.Quadruped;

            // 관절 — 목록 차례(부모가 먼저 온다) · 회전 없이 `Model` 축 그대로.
            var joint = new Dictionary<string, Transform>();
            foreach (var b in man.bones)
            {
                if (!at.TryGetValue(b, out var pos)) { Debug.LogError($"[{k.Tag}] 관절 자리가 없다 — {b}"); return false; }
                parentOf.TryGetValue(b, out var par);
                var parent = string.IsNullOrEmpty(par) ? model : joint.TryGetValue(par, out var pt) ? pt : null;
                if (parent == null) { Debug.LogError($"[{k.Tag}] `{b}`의 부모 `{par}`가 아직 없다(목록 차례)."); return false; }
                var go = new GameObject(b) { layer = root.layer };
                go.transform.SetParent(parent, false);
                go.transform.position = model.TransformPoint(pos);
                go.transform.rotation = model.rotation;
                joint[b] = go.transform;
            }
            Transform J(string n) => joint.TryGetValue(n, out var t) ? t : null;
            rig.Body = J("Body"); rig.Head = J("Head"); rig.Tail = J("Tail");
            rig.LegFL = J("LegFL"); rig.LegFR = J("LegFR"); rig.LegBL = J("LegBL"); rig.LegBR = J("LegBR");
            rig.Hips = rig.Spine = rig.ArmL = rig.ArmR = rig.LegL = rig.LegR = rig.HandL = rig.HandR = null;
            rig.LowerArmL = rig.LowerArmR = rig.LowerLegL = rig.LowerLegR = rig.FootL = rig.FootR = null;
            if (rig.Body == null || rig.Head == null) { Debug.LogError($"[{k.Tag}] Body · Head 관절이 없다."); return false; }

            var bones = new Transform[man.bones.Count];
            for (int i = 0; i < bones.Length; i++) bones[i] = joint[man.bones[i]];
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * model.localToWorldMatrix;
            var bodyAt = model.InverseTransformPoint(rig.Body.position);

            foreach (var it in man.items)
            {
                bool head = it.region == "Head";
                // 눈 · 입 속(`None`)은 줄이지 않을 만큼 — 반짝임을 눈알 정점 색으로 칠했다(줄이면 뭉개진다).
                int cap = it.line == "Fine" ? k.FineVerts : it.line == "None" ? k.NoneVerts : it.line == "Thin" ? k.ThinVerts : head ? k.HeadVerts : k.BodyVerts;
                var mesh = ImportBakedMesh(Path.Combine(k.Dir, it.file), it.mesh, bind, cap, linear: true);
                if (mesh == null) continue;
                // 선 법선을 굽지 않는다 — 정점 법선(거리장의 기울기) 그대로(`ChibiPawnCreator.Baked`와 같은 까닭).
                var keep = new Vector4[mesh.vertexCount];
                for (int i = 0; i < keep.Length; i++) keep[i] = new Vector4(0f, 0f, 0f, -2f);
                mesh.tangents = keep;
                EditorUtility.SetDirty(mesh);
                var go = new GameObject(it.part) { layer = root.layer };
                go.transform.SetParent(model, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh; smr.bones = bones; smr.rootBone = rig.Body;
                smr.sharedMaterials = it.line == "None" ? new[] { mats["Paint"] }
                                    : it.line == "Thin" ? new[] { mats["PaintThin"] }
                                    : new[] { mats["Paint"], mats[it.line == "Fine" ? "BakedLineFine" : "OutlineOuter"] };
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smr.localBounds = new Bounds(mesh.bounds.center - bodyAt, mesh.bounds.size + Vector3.one * 0.3f);
            }

            // 꼬리 마디(뿌리 `Tail`은 클립이 돌린다 — 그 아래 마디를 `TailSway`가 흔든다).
            var links = new List<Transform>();
            foreach (var b in man.bones) if (b.StartsWith("Tail") && b != "Tail") links.Add(joint[b]);
            root.TryGetComponent(out TailSway sway);
            if (links.Count > 0)
            {
                if (sway == null) sway = root.AddComponent<TailSway>();
                sway.Links = links.ToArray();
            }
            else if (sway != null) UnityEngine.Object.DestroyImmediate(sway);

            PrefabUtility.SaveAsPrefabAsset(root, dp);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return true;
    }

    static void CreateQuadOnly(QuadKind k)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var mats = QuadMaterials(k.Tag);
        if (mats == null) return;
        _saved.Clear();
        bool ok = DressQuad(k, mats);
        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!_saved.Contains(path) && Path.GetFileName(path).StartsWith(k.Prefab + "_")) AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[{k.Tag}] 보기 몸 {(ok ? 1 : 0)}/1벌(`{PawnPrefabPaths.PathOf(k.Prefab)}`) — 다른 폰은 그대로 · {watch.Elapsed.TotalSeconds:F0}초");
    }

    // **얇은 판의 선**(쥐 귀 — 11-18 사용자: *"쥐 귀가 외곽선이 이상하게 나와"*). 몸 선 껍데기(둘째 재질 `OutlineOuter`)가 몇 mm짜리 귀 판을
    // 뚫고 앞면 위에 빗금처럼 그어졌다. 몸 선 셰이더(`ToonOutlineOuter`)에는 깊이를 미는 값이 없어, **살 재질 제 선 패스**(`ToonOutline` `Outline`)를
    // 켜서 가늘게 긋고 깊이를 살 뒤로 민다(`_OutlineBack` — 해골의 가는 뼈 · 감염자 테와 같은 셈). 재질은 하나(`PaintThin`)만 건다.
    const float QuadThinWidth = 0.008f, QuadThinBack = 0.03f;
    static Dictionary<string, Material> WithThinLine(Dictionary<string, Material> m)
    {
        var o = Variant(m["Paint"], "Chibi_BakedPaintThin", "_BaseColor", Color.white);
        o.SetFloat("_OutlineWidth", QuadThinWidth);
        o.SetFloat("_OutlineBack", QuadThinBack);
        o.SetColor("_OutlineColor", m["OutlineOuter"].GetColor("_OutlineColor"));
        EditorUtility.SetDirty(o);
        m["PaintThin"] = o;
        return m;
    }

    static Dictionary<string, Material> QuadMaterials(string tag)
    {
        var toon = Shader.Find(ToonShader); var faceSh = Shader.Find(FaceShader); var outer = Shader.Find(OuterShader);
        if (toon == null || faceSh == null || outer == null) { Debug.LogError($"[{tag}] 셰이더를 못 찾았다."); return null; }
        var atlases = new Dictionary<string, ChibiFaceAtlas>();
        foreach (var kind in new[] { "Eyes", "Mouths" })
        {
            var a = AssetDatabase.LoadAssetAtPath<ChibiFaceAtlas>($"{MatDir}/Chibi_{kind}Atlas.asset");
            if (a == null) { Debug.LogError($"[{tag}] 얼굴 아틀라스({kind})가 없다 — `Pawn/3`을 먼저 돌릴 것."); return null; }
            atlases[kind] = a;
        }
        return WithThinLine(BakedMaterials(EnsureMaterials(toon, faceSh, outer, atlases)));
    }

    // `Pawn/3`(모든 폰을 다시 굽는다)이 부른다 — 빼먹으면 그 끝 청소가 이 메시를 지운다.
    static int DressQuadAll(Dictionary<string, Material> mats)
    {
        var bm = WithThinLine(BakedMaterials(mats)); int made = 0;
        foreach (var k in QuadKinds) if (DressQuad(k, bm)) made++;
        return made;
    }

    [MenuItem("DungeonColony/Pawn/20. Create Mole Only (두더지만 — 파이썬이 뽑은 네 발 메시를 들인다 · 다른 폰은 안 건드린다)")]
    public static void CreateMoleOnly() => CreateQuadOnly(QuadKinds[0]);

    [MenuItem("DungeonColony/Pawn/21. Create Rat Only (괴물쥐만 — 파이썬이 뽑은 네 발 메시를 들인다 · 다른 폰은 안 건드린다)")]
    public static void CreateRatOnly() => CreateQuadOnly(QuadKinds[1]);
}
