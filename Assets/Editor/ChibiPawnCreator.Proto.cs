using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// **비율 시제품 — 머리 하나(넓고 부드러운 남자 머리)에 몸을 처음부터 새로 붙인다**(2026-10-06).
//
// 사용자: *"머리는 broadsoft만 쓰고 전체적으로 새로 만들라"* · *"기존 몸 원래대로 되돌리고 별도 프리팹으로 따로"* ·
// *"팔도 보이게"*. 레퍼런스는 사용자가 보낸 작은 마법사 캐릭터의 정면 전신(옷이 다리를 가리는 것은 안 따른다 — 사용자).
//
// ## 비율 — 머리 높이(`Hh`)를 1로 두고 레퍼런스에서 쟀다
//
//   몸통 높이 0.96 · 몸통 폭 0.68(가슴) · 보이는 다리 0.40 · 신발 높이 0.22 · 장갑 지름 0.32 · 팔(어깨 → 손목) 0.56 ·
//   팔 굵기 0.20. 다리는 10-09에 **바지통**으로 바꿨다(사용자 그림 — 굵기 0.28 · 가랑이 틈 0.04 · 몸통 밑 0.22는 바지).
//
// 그래서 **값을 미터로 적지 않고 `Hh`의 배수로 적는다** — 머리를 바꾸면 몸이 따라 맞는다. 머리 크기 자체는 머리형 그림의
// 눈금(`TorsoH`)에서 온다(본 몸과 같은 머리다).
//
// ## 몸은 한 덩어리다 — 스킨 메시 (2026-10-09)
//
// 사용자: *"몸이 원래 좀 하나로 이어져있는 것처럼 만들어져야하는 거 아니야? 지금은 그냥 너무 도형 이어붙인 느낌인데"*.
// 처음에는 본 몸처럼 **도형을 관절에 하나씩 붙였다**(`PawnRigBuilder`가 팔다리 원통을 반으로 갈라 위·아래 관절에 단다).
// 그 모양은 굽히는 자리마다 이음매가 비쳐서 팔꿈치·무릎 공 · 어깨 공 · 바지 띠로 **틈을 하나씩 메웠는데**, 메울수록
// 도형을 이어 붙인 티가 더 났다. 이제 **몸통·팔·다리가 메시 하나**이고 정점마다 관절 가중치를 준다:
//
//   - **팔꿈치·무릎은 휜다** — 관절 둘레 띠의 정점이 위·아래 뼈에 나눠 걸린다(`Blend`).
//   - **어깨·엉덩이는 통의 위 반구가 관절 한가운데에 있다** — 공을 그 중심으로 돌리면 제자리라, 몸통에 반쯤 묻힌 채
//     돌고 벌어지는 틈이 없다.
//   - **선은 한 번만 선다** — 셋째 재질 칸(`OutlineOuter`)이 몸 전체를 한 껍데기로 긋는다. 셔츠·바지는 칸이 갈릴 뿐 같은 몸이다.
//   - 머리·손·신발은 **따로 관절에 단다** — 큰 머리가 얹힌 것은 이 그림체의 모양이고, 손 공·신발도 떨어져 있어 어색하지 않다.
//     손·신발은 바깥 선만 받는다(`SkinOuter` · `Shoe` — 제 선을 그으면 손목·발목에 띠가 선다).
//
// 관절은 **이 도구가 세운다** — `PawnRigBuilder`는 살을 관절에 붙이는 도구라 스킨 메시는 건너뛴다(`Pawn/2`를 돌려도
// 이 프리팹은 안 건드린다). 아바타·컨트롤러만 그쪽 한 벌을 쓴다(`PawnRigBuilder.FinishBiped`). 관절 이름과 계층은
// `PawnRig` 규격 그대로다 — 클립이 경로로 묶인다.
//
// ## 본 몸과 섞이지 않게
//
// - **프리팹은 따로다**(`Proto_Chibi.prefab`) — 방랑자 몸 목록(`BodyVariants`)에 안 들어가고 어디서도 안 가리킨다.
// - **이 몸만의 메시는 `Proto_` 이름이다** — 본 도구(`Pawn/3`)는 이번에 안 만든 `Chibi_` 메시를 지운다. 머리·이목구비·
//   머리카락은 본 도구가 굽는 것과 **같은 것**을 같은 이름으로 다시 쓴다(내용이 같다 — 머리카락은 머리형 전부로 깎는다).
// - **곡괭이질 대체(`ChibiToolSwing`)·몸통 덜 흔들기(`ChibiPoseDamp`)는 안 붙인다.** 앞의 것은 손 공을 자리로 옮겨 보이는
//   팔에서 손이 떨어진다. 뒤의 것은 10-09에 붙여 찍어 봤다 — 곡괭이를 들 때 눕던 몸은 섰지만 팔이 얼굴 앞을 가로지르고
//   동작이 작아져, 사용자가 *"그냥 원래가 좋은거 같은데"*라고 해서 다시 뗐다.
public static partial class ChibiPawnCreator
{
    const string ProtoPath  = OutDir + "/Proto_Chibi.prefab";
    const string ProtoHead  = "Male_" + PawnLook.BasisShape;

    // ── 비율(× 머리 높이) ───────────────────────────────────────────────────
    const float PTorsoH   = 0.96f;
    const float PLegVis   = 0.40f;                  // 몸통 밑 ↔ 신발 위
    // 다리는 **바지통**이다(2026-10-09, 사용자 그림) — 두 다리가 몸통 폭을 거의 다 채우고 가랑이만 살짝 벌어진다.
    const float PLegW     = 0.28f, PLegX = 0.16f;   // 바깥 끝 0.30(몸통 밑 0.32 안) · 가랑이 틈 0.04
    const float PHipIn    = 0.20f;                  // 엉덩이 관절이 몸통 밑에서 이만큼 위(몸통 속)
    const float PPantsH   = 0.22f;                  // 몸통 밑동의 바지 — 셔츠 밑단이 여기서 끝나 보인다
    const float PShoeH    = 0.22f, PShoeW = 0.33f, PShoeLen = 0.50f, PShoeFwd = 0.08f;
    const float PHand     = 0.32f;
    const float PLamp     = 0.20f;                  // 허리 램프 알의 지름
    const float PArmLen   = 0.56f, PArmW = 0.20f;
    const float PArmIn    = 0.30f;                  // 팔 가운데가 몸통 옆선에서 팔 굵기의 이만큼 밖(나머지는 몸통에 묻힌다)
    const float PShoulder = 0.10f;                  // 어깨가 몸통 꼭대기에서 이만큼 아래
    const float PDepth    = 0.80f;                  // 몸통 깊이 / 폭
    const float PBend     = 0.08f;                  // 팔꿈치·무릎에서 위·아래 뼈에 나눠 걸리는 띠의 반폭
    const float ShoulderDrop = 0.03f;               // 위팔 관절이 팔 꼭대기에서 이만큼 아래(m) — `PawnRigBuilder.RigBiped`와 같다

    // 몸통 옆선(반폭 × 머리 높이) — 밑 · 엉덩이 · 허리 · 가슴 · 어깨. 밑을 넓혀 바지통 두 다리가 그 안에서 나온다.
    static readonly float[] PTorsoHalf = { 0.32f, 0.33f, 0.32f, 0.34f, 0.30f };
    const float PBottomRound = 0.35f;               // 밑동 모서리만 둥글린다(바지 밑이 판판해 보이게)

    // 스킨 메시의 뼈 순서 — `bones` 배열과 가중치의 번호가 이것이다. 이름이 곧 관절 이름이다(`PawnRig` 규격).
    enum B { Hips, Spine, ArmL, LowerArmL, HandL, ArmR, LowerArmR, HandR, LegL, LowerLegL, FootL, LegR, LowerLegR, FootR, Head, Count }

    [MenuItem("DungeonColony/Pawn/7. Create Proportion Proto (시제품 · 머리 하나 · 팔다리)")]
    public static void CreateProto()
    {
        var toon  = Shader.Find(ToonShader);
        var face  = Shader.Find(FaceShader);
        var outer = Shader.Find(OuterShader);
        if (toon == null || face == null || outer == null)
        { Debug.LogError($"[시제품] 셰이더를 못 찾았다 — {ToonShader} / {FaceShader} / {OuterShader}"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Source) == null) { Debug.LogError($"[시제품] 바탕 폰이 없다 — {Source}"); return; }

        var atlases = BuildFaceAtlases();
        var mats    = EnsureMaterials(toon, face, outer, atlases);

        _saved.Clear();
        var all = MakeHeads(ChibiFaceLayout.LoadOrCreate());
        var head = Array.Find(all, x => x.Name == ProtoHead);
        if (head.Name == null) { Debug.LogError($"[시제품] 머리 `{ProtoHead}`가 없다."); return; }

        // ── 머리 — 본 도구와 같은 메시를 같은 이름으로 ─────────────────────────
        var heads    = new Dictionary<string, Mesh>();
        var features = new Dictionary<(string, string), Mesh>();
        {
            var h = head;
            heads[h.Name] = Shear(Lathe($"Chibi_Head_{h.Name}", t => HeadRadius(t, h), 64, 48, out _, normalize: false,
                                        densePole: 2), h.Forward);
            foreach (var f in Features) features[(h.Name, f.Name)] = BuildFeature(h, f, atlases[f.Kind].Cells);
        }

        // 머리카락은 **머리형 전부로** 깎는다 — 본 도구와 같은 메시여야 같은 이름에 덮어써도 본 몸이 안 바뀐다.
        var hairs = new Dictionary<string, Mesh>();
        foreach (var fit in HairFits)
        {
            var group = new List<HeadShape>();
            foreach (var hh in all) if (PawnLook.HairFitOf(hh.Name) == fit) group.Add(hh);
            float forward = 0f;
            foreach (var hh in group) forward += hh.Forward / group.Count;
            Func<float, float> hairR = t => { float r = 0f; foreach (var hh in group) r = Mathf.Max(r, HeadRadius(t, hh)); return r; };
            hairs[$"{fit}.Short"] = Shear(HairShell($"Chibi_Hair_{fit}_Short", hairR, 0.44f, 150f), forward);
            hairs[$"{fit}.Bob"]   = Shear(HairShell($"Chibi_Hair_{fit}_Bob",   hairR, 0.26f, 125f), forward);
            hairs[$"{fit}.Long"]  = Shear(HairShell($"Chibi_Hair_{fit}_Long",  hairR, 0.10f, 110f), forward);
            hairs[$"{fit}.Helmet"] = HelmetShell(fit, hairR, forward);
        }
        var ears = new Dictionary<string, Mesh>();
        foreach (var e in Ears) ears[e.Name] = Lathe($"Chibi_Ear_{e.Name}", e.Profile, 16, 16, out _, densePole: 2);
        var noses = new Dictionary<string, Mesh>();
        foreach (var n in Noses) noses[n.Name] = Lathe($"Chibi_Nose_{n.Name}", n.Profile, 12, 16, out _, densePole: 2);

        // `HeadParts`는 `Heads`(머리형 표)를 돈다 — **이 머리 하나만** 보이게 잠깐 좁혀 부른다.
        var keep = Heads;
        List<Part> headParts;
        try { Heads = new[] { head }; headParts = HeadParts("Male", heads, features, hairs, ears, noses); }
        finally { Heads = keep; }

        // ── 몸 — 본 몸과 같은 짓기(`DressSkinned`) ─────────────────────────────
        float headH = head.Size.y;
        var half = new float[PTorsoHalf.Length];
        for (int i = 0; i < half.Length; i++) half[i] = PTorsoHalf[i] * headH;
        var d = new BodyDims(headH, half, PDepth, PHand * headH, PArmW * headH * 0.5f, PLegX * headH, PLegW * headH * 0.5f);

        // ── 프리팹 — guid를 지킨다(있으면 내용만 덮는다) ─────────────────────────
        if (File.Exists(ProtoPath)) { File.Copy(Source, ProtoPath, true); AssetDatabase.ImportAsset(ProtoPath, ImportAssetOptions.ForceUpdate); }
        else                        { AssetDatabase.CopyAsset(Source, ProtoPath); AssetDatabase.ImportAsset(ProtoPath); }

        bool ok = false;
        var root = PrefabUtility.LoadPrefabContents(ProtoPath);
        try
        {
            if (!DressSkinned(root, d, headParts, mats, atlases, Path.GetFileNameWithoutExtension(ProtoPath), "Proto_Body")) return;

            if (!root.TryGetComponent(out PawnLook _))      root.AddComponent<PawnLook>();
            if (!root.TryGetComponent(out PawnOutlineId _)) root.AddComponent<PawnOutlineId>();
            var bv = root.GetComponent<BodyVariants>();
            if (bv != null) UnityEngine.Object.DestroyImmediate(bv);

            if (!root.TryGetComponent(out PawnBody pb)) pb = root.AddComponent<PawnBody>();
            pb.Gender = Gender.Male; pb.ShapeLabel = "시제품";

            PrefabUtility.SaveAsPrefabAsset(root, ProtoPath);
            ok = true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        AssetDatabase.SaveAssets();
        if (ok)
            Debug.Log($"[시제품] {ProtoPath} — 머리 {ProtoHead}(높이 {d.Hh:F2}m) · 키 {d.Top + HeadGap + d.Hh:F2}m · 몸통 {d.TorsoH:F2} · " +
                      $"보이는 다리 {PLegVis * d.Hh:F2} · 신발 {PShoeH * d.Hh:F2} · 팔 {PArmLen * d.Hh:F2} · 몸은 스킨 메시 하나. " +
                      "**이제 `Pawn/8. Pose Sheet`.**");
    }

    // ── 한 덩어리 몸 짓기 — 시제품과 본 몸(체형 여섯 · 방랑자 · 고블린)이 같이 쓴다 ──────────────────
    //
    // `Model`의 자식을 비우고 → 관절을 세우고(팔을 내린 자세) → 머리·손·신발을 관절에 달고 → 몸통·팔·다리를 스킨 메시
    // 하나로 짓고 → T자로 벌려 아바타를 세운다. 머리 부위(`headParts`)는 옛 몸의 몸통 꼭대기(`TorsoH`)에 맞춰 놓인 것을
    // 받아 이 몸의 꼭대기로 옮긴다. **갈라 적으면 시제품에서 고친 것이 본 몸에 안 간다.**
    static bool DressSkinned(GameObject root, BodyDims d, List<Part> headParts, Dictionary<string, Material> mats,
                             Dictionary<string, ChibiFaceAtlas> atlases, string prefabName, string meshName)
    {
        var model = root.transform.Find("Model");
        if (model == null) { Debug.LogError($"[치비] {prefabName}에 `Model`이 없다."); return false; }
        var kids = new List<GameObject>();
        foreach (Transform c in model) kids.Add(c.gameObject);
        foreach (var k in kids) UnityEngine.Object.DestroyImmediate(k);

        // **머리카락이 맞춰진 머리를 비운다** — 머리카락·귀는 지금 기준 머리에 맞춰 새로 굽는데, 루트의 `PawnLook`은 남아서
        // *"이미 다른 머리에 맞춰 옮겼다"*는 옛 기록(`fittedTo`)을 든다. 그대로 두면 판에서 엉뚱한 틀에서 다시 옮겨 머리카락이
        // 줄거나 밀리고 **정수리로 머리가 뚫고 나온다**(2026-10-09, 사용자: *"어떤 것들은 정수리에 원형탈모가 생겨"* — 체형
        // 프리팹 열둘이 전부 `Male_SlimSquare`를 들고 있었다. 바탕 폰에 한 번 저장된 것이 복사마다 따라왔다).
        if (root.TryGetComponent(out PawnLook look))
        {
            var so = new SerializedObject(look);
            so.FindProperty("fittedTo").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        float headShift = d.Top - TorsoH;
        var rigid = new List<Part>();
        foreach (var p in headParts)
            rigid.Add(new Part(p.Name, p.Mesh, p.Center + Vector3.up * headShift, p.Size, p.Mat, p.On, p.Cell, p.Kind, p.OuterMat, p.Euler));
        float neckY = d.Top + HeadGap;   // 목 관절 = 머리 밑동(`Head` 구의 밑)
        foreach (var p in rigid) if (p.Name == "Head") neckY = p.Center.y - p.Size.y * 0.5f;

        // 손·신발 — 관절에 붙는 굳은 살. 팔·다리 통의 끝이 이 속에 묻힌다.
        rigid.Add(new Part("HandL.Ball", null, new Vector3(-d.ArmX, d.HandY - d.Hand * 0.25f, 0f), Vector3.one * d.Hand, "SkinOuter", outerMat: "OutlineOuter"));
        rigid.Add(new Part("HandR.Ball", null, new Vector3( d.ArmX, d.HandY - d.Hand * 0.25f, 0f), Vector3.one * d.Hand, "SkinOuter", outerMat: "OutlineOuter"));
        rigid.Add(new Part("FootL.Shoe", null, new Vector3(-d.LegX, d.Shoe.y * 0.5f, PShoeFwd * d.Hh), d.Shoe, "Shoe", outerMat: "OutlineOuter"));
        rigid.Add(new Part("FootR.Shoe", null, new Vector3( d.LegX, d.Shoe.y * 0.5f, PShoeFwd * d.Hh), d.Shoe, "Shoe", outerMat: "OutlineOuter"));

        // **허리 램프**(2026-10-23) — 바지 띠 높이, 몸 앞 오른쪽에 반쯤 묻힌 알. 꺼 두고 허리에 무엇을 차면 켜진다(`PawnApparel`).
        {
            float r = BodyTorsoRadius(d.Half, PPantsH / PTorsoH, d.TorsoH);
            rigid.Add(new Part("Hips.Lamp", null, new Vector3(r * 0.55f, d.HemY, r * d.Depth * 0.85f),
                               Vector3.one * PLamp * d.Hh, "Lamp", on: false));
        }

        // 관절 — 팔을 내린 자세로 세운다(몸을 그 자세로 지었으니 바인드 포즈도 그 자세다). T자는 맨 끝에.
        if (!model.TryGetComponent(out PawnRig rig)) rig = model.gameObject.AddComponent<PawnRig>();
        var bones = BodyJoints(model, rig, d, neckY);

        // 굳은 살은 이름의 앞머리(`관절.무엇`)가 붙을 관절이다 — `PawnRigBuilder.RigBiped`와 같은 규칙.
        foreach (var p in rigid)
        {
            Build(model, p, mats, atlases, root.layer);
            var t = model.Find(p.Name);
            int dot = p.Name.IndexOf('.');
            string joint = dot > 0 ? p.Name.Substring(0, dot) : p.Name;
            var at = Enum.TryParse(joint, out B bi) && bi != B.Count ? bones[(int)bi] : bones[(int)B.Spine];
            t.SetParent(at, worldPositionStays: true);
            if (p.Name == "Head") t.name = "HeadMesh";
        }

        // 몸 — 스킨 메시 하나(셔츠 · 바지 · 바깥 선 세 칸).
        var bodyMesh = BodyMesh(d, model, bones, meshName);
        var go = new GameObject("Body");
        go.layer = root.layer;
        go.transform.SetParent(model, false);
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = bodyMesh;
        smr.bones      = bones;
        smr.rootBone   = bones[(int)B.Hips];
        smr.sharedMaterials   = new[] { mats["Shirt"], mats["Pants"], mats["OutlineOuter"] };
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 경계는 엉덩이 기준 · **지은 몸에 딱 맞게**(10cm 여유). 초상화(`PortraitCapture`)·미리보기(`PawnPreviewStage`)가 렌더러
        // 경계로 틀을 잡아서, 휘두를 만큼 넉넉히 잡으면 얼굴이 멀어진다. 팔이 상자 밖으로 휘둘러질 때 몸통이 화면 밖이면 그 팔이
        // 잠깐 안 그려지는 것은 감수한다(옛 몸도 렌더러마다 제 자리 상자였다).
        var mb = bodyMesh.bounds;
        smr.localBounds = new Bounds(mb.center - Vector3.up * d.HipY, mb.size + Vector3.one * 0.1f);

        // **T자** — 아바타의 기준 자세(`PawnRigBuilder.RigBiped`와 같다). 왼팔은 -x, 오른팔은 +x로 벌린다.
        rig.ArmL.localRotation = Quaternion.Euler(0f, 0f, -90f);
        rig.ArmR.localRotation = Quaternion.Euler(0f, 0f,  90f);

        // **팔이 안 보이던 몸의 땜질은 뗀다** — 곡괭이질 대체는 손 공을 자리로 옮겨 보이는 팔에서 손이 떨어지고, 몸통 덜어 내기는
        // 10-09에 찍어 보니 팔이 얼굴을 가로질러 사용자가 다시 떼라고 했다(위 머리말).
        foreach (var hack in new Component[] { root.GetComponent<ChibiPoseDamp>(), root.GetComponent<ChibiToolSwing>() })
            if (hack != null) UnityEngine.Object.DestroyImmediate(hack);

        return PawnRigBuilder.FinishBiped(root, model, prefabName);
    }

    // 몸의 치수 — 전부 Model 기준 미터(발밑 0 · +z 앞). 높이 쪽은 머리 높이(`Hh`)의 배수(시제품 비율)이고, 폭 쪽은 받는다
    // (체형마다 몸통 옆선 · 손 · 팔 · 다리 굵기가 다르다).
    readonly struct BodyDims
    {
        public readonly float   Hh, TorsoH, Lift, Top, HemY, Depth;
        public readonly float[] Half;                      // 몸통 반폭(m) — 밑 · 엉덩이 · 허리 · 가슴 · 어깨(`Stations`)
        public readonly float   HipY, LegX, LegR, FootY, KneeY;
        public readonly float   ArmX, ArmR, ShoulderY, HandY, ElbowY, Hand;
        public readonly Vector3 Shoe;

        public BodyDims(float hh, float[] half, float depth, float hand, float armR, float legX, float legR)
        {
            Hh = hh; Half = half; Depth = depth;
            TorsoH = PTorsoH * hh;
            Lift   = (PShoeH + PLegVis) * hh;           // 몸통 밑
            Top    = Lift + TorsoH;                     // 몸통 꼭대기
            HemY   = Lift + PPantsH * hh;

            HipY  = Lift + PHipIn * hh;
            LegX  = legX; LegR = legR;
            FootY = PShoeH * hh * 0.5f;                 // 다리 밑동(발 관절)은 신발 속
            KneeY = (HipY + FootY) * 0.5f;
            // 신발 폭은 다리를 따라간다(시제품 0.33 : 다리 0.28) — 굵은 다리에 좁은 신발이면 발목이 삐져나온다.
            Shoe  = new Vector3(legR * 2f * (PShoeW / PLegW), PShoeH * hh, PShoeLen * hh);

            Hand      = hand;
            ArmR      = armR;
            ShoulderY = Top - PShoulder * hh - ShoulderDrop;   // 위팔 관절
            ArmX      = BodyTorsoRadius(half, 1f - PShoulder / PTorsoH, TorsoH) + ArmR * 2f * PArmIn;
            HandY     = Top - PShoulder * hh - PArmLen * hh;
            ElbowY    = (ShoulderY + HandY) * 0.5f;
        }
    }

    // 몸통 옆선 — 반폭 표(m)를 잇고 위아래를 둥글린다(옛 몸의 `TorsoRadius`와 같은 셈, 밑동만 덜 둥글다 — 바지 밑이 판판하게).
    static float BodyTorsoRadius(float[] half, float t, float torsoH)
    {
        float y = t * torsoH, w = HalfAt(half, t);
        float rb = half[0] * PBottomRound, rt = half[^1] * TopRound;
        if (y < rb)          return w - rb + Mathf.Sqrt(Mathf.Max(0f, rb * rb - (rb - y) * (rb - y)));
        if (y > torsoH - rt) { float dd = torsoH - y; return w - rt + Mathf.Sqrt(Mathf.Max(0f, rt * rt - (rt - dd) * (rt - dd))); }
        return w;
    }

    // 관절 — `PawnRig` 규격의 계층(Model/Hips/{Spine/{Head, ArmL/…}, LegL/…}). 돌려주는 배열은 `B` 순서다.
    static Transform[] BodyJoints(Transform model, PawnRig rig, BodyDims d, float neckY)
    {
        Transform J(Transform parent, string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.layer = model.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.position = model.TransformPoint(at);
            go.transform.rotation = model.rotation;
            return go.transform;
        }

        var b = new Transform[(int)B.Count];
        b[(int)B.Hips]  = J(model, "Hips",  new Vector3(0f, d.HipY, 0f));
        b[(int)B.Spine] = J(b[(int)B.Hips], "Spine", new Vector3(0f, d.HipY + 0.05f, 0f));
        b[(int)B.Head]  = J(b[(int)B.Spine], "Head", new Vector3(0f, neckY, 0f));
        foreach (int s in new[] { -1, 1 })
        {
            string side = s < 0 ? "L" : "R";
            int o = s < 0 ? 0 : 3;
            b[(int)B.ArmL + o]      = J(b[(int)B.Spine],         "Arm" + side,      new Vector3(s * d.ArmX, d.ShoulderY, 0f));
            b[(int)B.LowerArmL + o] = J(b[(int)B.ArmL + o],      "LowerArm" + side, new Vector3(s * d.ArmX, d.ElbowY, 0f));
            b[(int)B.HandL + o]     = J(b[(int)B.LowerArmL + o], "Hand" + side,     new Vector3(s * d.ArmX, d.HandY, 0f));
            b[(int)B.LegL + o]      = J(b[(int)B.Hips],          "Leg" + side,      new Vector3(s * d.LegX, d.HipY, 0f));
            b[(int)B.LowerLegL + o] = J(b[(int)B.LegL + o],      "LowerLeg" + side, new Vector3(s * d.LegX, d.KneeY, 0f));
            b[(int)B.FootL + o]     = J(b[(int)B.LowerLegL + o], "Foot" + side,     new Vector3(s * d.LegX, d.FootY, 0f));
        }

        rig.Kind = PawnRig.BodyKind.Biped;
        rig.Hips = b[(int)B.Hips]; rig.Spine = b[(int)B.Spine]; rig.Head = b[(int)B.Head];
        rig.ArmL = b[(int)B.ArmL]; rig.LowerArmL = b[(int)B.LowerArmL]; rig.HandL = b[(int)B.HandL];
        rig.ArmR = b[(int)B.ArmR]; rig.LowerArmR = b[(int)B.LowerArmR]; rig.HandR = b[(int)B.HandR];
        rig.LegL = b[(int)B.LegL]; rig.LowerLegL = b[(int)B.LowerLegL]; rig.FootL = b[(int)B.FootL];
        rig.LegR = b[(int)B.LegR]; rig.LowerLegR = b[(int)B.LowerLegR]; rig.FootR = b[(int)B.FootR];
        rig.Body = rig.Tail = rig.LegFL = rig.LegFR = rig.LegBL = rig.LegBR = null;
        return b;
    }

    // ── 몸 메시 ────────────────────────────────────────────────────────────
    //
    // 통 다섯(몸통 · 팔 둘 · 다리 둘)을 한 메시에 담는다. 통끼리는 정점을 안 나누고 **서로 파묻힌다** — 바깥 선은 몸
    // 전체가 한 껍데기라(셋째 칸) 파묻힌 자리에 선이 안 선다. 칸: 0 셔츠 · 1 바지 · 2 바깥 선(모든 삼각형).
    sealed class SkinBuild
    {
        public readonly List<Vector3>    V = new();
        public readonly List<BoneWeight> W = new();
        public readonly List<int>[]      T = { new(), new(), new() };
    }

    const int ProtoSegs = 28;

    // 세로 통 하나 — `rings`는 아래에서 위로 (높이, 반지름). 반지름 0은 꼭짓점 하나(극). `sub`는 띠 가운데 높이로 칸을 고른다.
    static void Tube(SkinBuild sb, float x, float z, float depth, List<(float y, float r)> rings,
                     Func<float, BoneWeight> weight, Func<float, int> sub)
    {
        var start = new int[rings.Count];
        var pole  = new bool[rings.Count];
        for (int i = 0; i < rings.Count; i++)
        {
            var (y, r) = rings[i];
            start[i] = sb.V.Count;
            pole[i]  = r < 1e-5f;
            int n = pole[i] ? 1 : ProtoSegs;
            for (int k = 0; k < n; k++)
            {
                float a = 2f * Mathf.PI * k / ProtoSegs;
                sb.V.Add(new Vector3(x + Mathf.Cos(a) * r, y, z + Mathf.Sin(a) * r * depth));
                sb.W.Add(weight(y));
            }
        }
        for (int i = 0; i + 1 < rings.Count; i++)
        {
            int s = sub((rings[i].y + rings[i + 1].y) * 0.5f);
            for (int k = 0; k < ProtoSegs; k++)
            {
                int k1 = (k + 1) % ProtoSegs;
                int a = pole[i]     ? start[i]     : start[i] + k,     b = pole[i]     ? start[i]     : start[i] + k1;
                int c = pole[i + 1] ? start[i + 1] : start[i + 1] + k, e = pole[i + 1] ? start[i + 1] : start[i + 1] + k1;
                // `Lathe`와 같은 감김(a, c, b · b, c, e) — 극 쪽은 한 삼각형이 접혀 없어진다.
                if (!pole[i])     Tri(sb, s, a, c, b);
                if (!pole[i + 1]) Tri(sb, s, b, c, e);
            }
        }
    }

    static void Tri(SkinBuild sb, int sub, int a, int b, int c)
    {
        sb.T[sub].Add(a); sb.T[sub].Add(b); sb.T[sub].Add(c);
        sb.T[2].Add(a);   sb.T[2].Add(b);   sb.T[2].Add(c);
    }

    // 캡슐 — 아래 반구 중심 `y0` · 위 반구 중심 `y1`. 위 반구는 관절(어깨·엉덩이) 한가운데에 둔다 — 돌려도 제자리다.
    static List<(float y, float r)> Capsule(float y0, float y1, float r, int straight)
    {
        const int Cap = 7;
        var rings = new List<(float, float)> { (y0 - r, 0f) };
        for (int i = 1; i < Cap; i++) { float a = Mathf.PI * 0.5f * i / Cap; rings.Add((y0 - Mathf.Cos(a) * r, Mathf.Sin(a) * r)); }
        for (int i = 0; i <= straight; i++) rings.Add((Mathf.Lerp(y0, y1, i / (float)straight), r));
        for (int i = Cap - 1; i >= 1; i--) { float a = Mathf.PI * 0.5f * i / Cap; rings.Add((y1 + Mathf.Cos(a) * r, Mathf.Sin(a) * r)); }
        rings.Add((y1 + r, 0f));
        return rings;
    }

    static BoneWeight One(B bone) => new() { boneIndex0 = (int)bone, weight0 = 1f };

    // 두 뼈 사이 — `y`가 `at ± band` 띠 안이면 매끄럽게 넘어간다(위 뼈 `upper` · 아래 뼈 `lower`).
    static BoneWeight Blend(float y, float at, float band, B upper, B lower)
    {
        float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(at - band, at + band, y));
        if (u >= 0.999f) return One(upper);
        if (u <= 0.001f) return One(lower);
        // 무거운 쪽이 0번 — 유니티가 가중치를 큰 순서로 기대한다.
        return u >= 0.5f
            ? new BoneWeight { boneIndex0 = (int)upper, weight0 = u, boneIndex1 = (int)lower, weight1 = 1f - u }
            : new BoneWeight { boneIndex0 = (int)lower, weight0 = 1f - u, boneIndex1 = (int)upper, weight1 = u };
    }

    static Mesh BodyMesh(BodyDims d, Transform model, Transform[] bones, string name)
    {
        var sb = new SkinBuild();
        float band = PBend * d.Hh;

        // 몸통 — 밑 바지 · 위 셔츠. 엉덩이 둘레는 엉덩이 뼈, 위는 척추.
        var torso = new List<(float y, float r)> { (d.Lift, 0f) };
        const int TorsoRings = 40;
        for (int i = 0; i <= TorsoRings; i++)
        {
            float t = i / (float)TorsoRings;
            torso.Add((d.Lift + t * d.TorsoH, BodyTorsoRadius(d.Half, t, d.TorsoH)));
        }
        torso.Add((d.Top, 0f));
        Tube(sb, 0f, 0f, d.Depth, torso,
             y => Blend(y, d.HipY + band * 1.5f, band * 1.5f, B.Spine, B.Hips),
             y => y < d.HemY ? 1 : 0);

        foreach (int s in new[] { -1, 1 })
        {
            int o = s < 0 ? 0 : 3;
            B arm = B.ArmL + o, lowerArm = B.LowerArmL + o, leg = B.LegL + o, lowerLeg = B.LowerLegL + o;

            // 팔 — 위 반구가 어깨 관절 한가운데 · 아래 끝은 손 공 속.
            Tube(sb, s * d.ArmX, 0f, 1f, Capsule(d.HandY, d.ShoulderY, d.ArmR, 16),
                 y => Blend(y, d.ElbowY, band, arm, lowerArm), _ => 0);

            // 다리 — 위 반구가 엉덩이 관절 한가운데(몸통 속) · 아래 끝은 신발 속.
            Tube(sb, s * d.LegX, 0f, 1f, Capsule(d.FootY, d.HipY, d.LegR, 16),
                 y => Blend(y, d.KneeY, band, leg, lowerLeg), _ => 1);
        }

        // 바인드 포즈 — 지금(팔을 내린) 자세의 관절. 몸은 `Model` 바로 아래 제자리에 선다.
        var bind = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * model.localToWorldMatrix;

        EnsureDir(MeshDir);
        string path = $"{MeshDir}/{name}.asset";
        _saved.Add(path);
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool fresh = m == null;
        if (fresh) m = new Mesh(); else m.Clear();
        m.name = name;
        m.SetVertices(sb.V);
        m.SetUVs(0, new List<Vector2>(new Vector2[sb.V.Count]));
        m.subMeshCount = 3;
        for (int i = 0; i < 3; i++) m.SetTriangles(sb.T[i], i);
        m.boneWeights = sb.W.ToArray();
        m.bindposes   = bind;
        m.RecalculateNormals();
        m.RecalculateBounds();
        if (fresh) AssetDatabase.CreateAsset(m, path);
        else       EditorUtility.SetDirty(m);
        return m;
    }
}
