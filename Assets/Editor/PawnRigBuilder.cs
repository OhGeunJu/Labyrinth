using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// **폰에 관절을 세우고 애니메이션을 붙인다**(2026-09-09).
//
// 폰 프리팹 서른 개는 `Model` 아래에 큐브·구가 **나란한 형제**로 놓인 블록 인형이었다. 그 모양으로는
// 애니메이션을 붙일 자리가 없다 — 팔의 피벗이 팔 한복판이라 돌리면 어깨가 아니라 팔꿈치에서 돈다.
//
// ── 생김새는 그대로, 뼈대만 사람 규격 ──────────────────────────────────────
//
// 사용자가 받아 온 클립(`assetsss/Kevin Iglesias`)은 **휴머노이드**다 — 휴머노이드 아바타가 있는
// 뼈대에만 붙는다. 처음엔 그래서 마네킹 모델로 갈아 끼우려 했는데 **생김새를 바꾸려던 것이
// 아니었다**(사용자). 답은 아바타 쪽에 있다: 유니티의 휴머노이드 아바타는 *사람 뼈 열다섯이 있는
// 계층*이면 어떤 모양이든 `AvatarBuilder`로 만들 수 있다. 그래서 블록 인형의 관절에 **팔꿈치·손·
// 무릎·발**을 더하고(팔·다리 큐브는 반으로 갈라 위아래 관절에 나눠 단다), 그 위에 아바타를 세운다.
// 같은 큐브·같은 색인데 사람처럼 걷고 휘두른다. 사람·고블린·해골이 **같은 컨트롤러**를 쓴다
// (`HumanoidClipSet`).
//
// **T자 자세로 세워 둔다.** 휴머노이드 아바타는 팔을 옆으로 벌린 T자를 기준 자세로 삼는다 —
// 그래서 세운 프리팹은 에디터에서 팔을 벌리고 서 있다(모든 휴머노이드 모델이 그렇다). 판이
// 열리면 애니메이터가 곧바로 Idle 자세를 준다.
//
// 네발(늑대·곰)은 사람 뼈가 없으므로 예전처럼 **관절 회전 곡선을 직접 굽는다**(`Quadruped.*`).
// 골렘(스킨 메시)은 건너뛴다 — 제 뼈가 있다.
//
// **다시 돌려도 된다** — 먼저 살을 `Model` 바로 아래로 되돌리고(가른 큐브는 도로 합친다) 관절을
// 지운 뒤 다시 세운다. 마네킹을 입혀 둔 프리팹(옛 `PawnDresser`의 `BlockModel`)도 되돌린다.
//
// **컨트롤러·아바타는 제자리에서 다시 짓는다**(guid 유지) — 지웠다 만들면 프리팹의 참조가 끊긴다.
// **루트는 안 건드린다** — `Model`의 자식만 바꾼다(CLAUDE.md "루트의 fileID").
public static class PawnRigBuilder
{
    const string PrefabDir = "Assets/3_Prefab/Pawn";
    const string AnimDir   = "Assets/Controller";
    const string ClipDir   = "Assets/Controller/Clips";
    const string AvatarDir = "Assets/Controller/Avatars";

    [MenuItem("DungeonColony/Pawn/0. Rebuild Pawn Rigs And Animations (1 → 2)")]
    public static void RebuildAll()
    {
        BakeAnimations();
        BuildRigs();
    }

    // ═══════════════════════ 1. 클립 · 컨트롤러 ═══════════════════════════

    [MenuItem("DungeonColony/Pawn/1. Bake Pawn Animations (클립 · 컨트롤러)")]
    public static void BakeAnimations()
    {
        EnsureDir(AnimDir);
        EnsureDir(ClipDir);

        // 두발은 받은 휴머노이드 클립을 쓴다 — 옛 블록 관절 클립(Biped_*)은 지운다(답이 두 벌이 된다).
        foreach (var name in new[] { "Biped_Idle", "Biped_Walk", "Biped_Attack", "Biped_Lying" })
            AssetDatabase.DeleteAsset(ClipDir + "/" + name + ".anim");
        AssetDatabase.DeleteAsset(AnimDir + "/Biped.controller");
        AssetDatabase.DeleteAsset(AnimDir + "/Human.controller");   // 상태도 참조도 없던 옛 껍데기
        // 옛 마네킹 재질(`PawnDresser`)도 — 그 길은 접었다.
        foreach (var g in AssetDatabase.FindAssets("t:Material Human_", new[] { "Assets/2_Material/Pawn" }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));

        if (HumanoidClipSet.BuildController() == null) return;

        // ── 네발 ────────────────────────────────────────────────────────
        // 관절 경로는 `PawnRig` 규격 그대로다. 살은 관절 아래에 매달려 있으므로 **회전만** 적는다.
        const string Body = "Body", QHead = "Body/Head", Tail = "Body/Tail";
        const string FL = "Body/LegFL", FR = "Body/LegFR", BL = "Body/LegBL", BR = "Body/LegBR";

        var qIdle = Clip("Quadruped_Idle", loop: true);
        Rot(qIdle, QHead, 'y', (0f, 0f), (0.9f, 8f), (1.8f, -8f), (2.6f, 0f));
        Rot(qIdle, QHead, 'x', (0f, 0f), (1.3f, 3f), (2.6f, 0f));
        Rot(qIdle, Tail,  'y', (0f, -10f), (0.65f, 10f), (1.3f, -10f), (1.95f, 10f), (2.6f, -10f));

        // 걷기(대각 걸음): 앞왼·뒤오른이 한 쌍, 앞오른·뒤왼이 한 쌍.
        var qWalk = Clip("Quadruped_Walk", loop: true);
        Rot(qWalk, FL, 'x', (0f, 25f), (0.25f, -25f), (0.5f, 25f));
        Rot(qWalk, BR, 'x', (0f, 25f), (0.25f, -25f), (0.5f, 25f));
        Rot(qWalk, FR, 'x', (0f, -25f), (0.25f, 25f), (0.5f, -25f));
        Rot(qWalk, BL, 'x', (0f, -25f), (0.25f, 25f), (0.5f, -25f));
        Rot(qWalk, QHead, 'x', (0f, 0f), (0.125f, 3f), (0.25f, 0f), (0.375f, 3f), (0.5f, 0f));
        Rot(qWalk, Tail,  'y', (0f, 12f), (0.25f, -12f), (0.5f, 12f));

        // 물기: 머리를 젖혔다 앞으로 찍는다. 몸통도 살짝 따라간다.
        var qAttack = Clip("Quadruped_Attack", loop: false);
        Rot(qAttack, QHead, 'x', (0f, 0f), (0.12f, -22f), (0.24f, 28f), (0.4f, 0f));
        Rot(qAttack, Body,  'x', (0f, 0f), (0.12f, -4f), (0.24f, 5f), (0.4f, 0f));

        var qLying = Clip("Quadruped_Lying", loop: false);
        foreach (var p in new[] { Body, QHead, Tail, FL, FR, BL, BR })
            foreach (var a in "xyz") Rot(qLying, p, a, (0f, 0f), (0.1f, 0f));

        BuildQuadrupedController(AnimDir + "/Quadruped.controller", qIdle, qWalk, qAttack, qLying);

        AssetDatabase.SaveAssets();
        Debug.Log("[PawnRig] 두발: 휴머노이드 컨트롤러 · 네발: 클립 4개 + 컨트롤러 — " + AnimDir);
    }

    // 클립 하나. **있으면 제자리에서 비우고 다시 적는다**(guid 유지).
    static AnimationClip Clip(string name, bool loop)
    {
        string path = ClipDir + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, path);
        }
        else clip.ClearCurves();

        clip.frameRate = 30f;
        var st = AnimationUtility.GetAnimationClipSettings(clip);
        st.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, st);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    // 관절 하나의 한 축 회전 곡선. 키는 (초, 도).
    static void Rot(AnimationClip clip, string path, char axis, params (float t, float deg)[] keys)
    {
        var curve = new AnimationCurve();
        foreach (var (t, deg) in keys) curve.AddKey(t, deg);
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);

        var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw." + axis);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    // 네발 컨트롤러: Idle ⇄ Walk(`Speed`) · AnyState → Attack → Idle · AnyState ⇄ Lying.
    // 매개변수는 `HumanoidClipSet`과 **같다** — `PawnAnimator`가 어느 쪽인지 몰라야 한다.
    static void BuildQuadrupedController(string path, AnimationClip idle, AnimationClip walk,
                                         AnimationClip attack, AnimationClip lying)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        else
        {
            for (int i = ctrl.parameters.Length - 1; i >= 0; i--) ctrl.RemoveParameter(i);
            for (int i = ctrl.layers.Length - 1; i >= 0; i--) ctrl.RemoveLayer(i);
            ctrl.AddLayer("Base Layer");
        }

        ctrl.AddParameter("Speed",  AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Lying",  AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;

        var sIdle   = sm.AddState("Idle",   new Vector3(300, 0));
        var sWalk   = sm.AddState("Walk",   new Vector3(300, 120));
        var sAttack = sm.AddState("Attack", new Vector3(560, 60));
        var sLying  = sm.AddState("Lying",  new Vector3(560, 200));
        sIdle.motion = idle; sWalk.motion = walk; sAttack.motion = attack; sLying.motion = lying;
        sm.defaultState = sIdle;

        // 걷기는 `Speed`에 재생 속도를 곱한다 — 4m/s에서 1배. 느린 놈은 느리게 걷는다.
        sWalk.speedParameterActive = true;
        sWalk.speedParameter       = "Speed";
        sWalk.speed                = 0.25f;

        var t = sIdle.AddTransition(sWalk);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.Greater, 0.3f, "Speed");

        t = sWalk.AddTransition(sIdle);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.Less, 0.3f, "Speed");

        t = sm.AddAnyStateTransition(sAttack);
        t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If,    0f, "Attack");
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Lying");

        t = sAttack.AddTransition(sIdle);
        t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.1f;

        t = sm.AddAnyStateTransition(sLying);
        t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, "Lying");

        t = sLying.AddTransition(sIdle);
        t.hasExitTime = false; t.duration = 0.1f;
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Lying");

        EditorUtility.SetDirty(ctrl);
    }

    // ═══════════════════════ 2. 관절 세우기 ═══════════════════════════════

    [MenuItem("DungeonColony/Pawn/2. Build Pawn Rigs (관절 · 아바타 · 애니메이터)")]
    public static void BuildRigs()
    {
        var humanoid = AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanoidClipSet.ControllerPath);
        var quad     = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimDir + "/Quadruped.controller");
        if (humanoid == null || quad == null)
        {
            Debug.LogError("[PawnRig] 컨트롤러가 없다 — `1. Bake Pawn Animations`를 먼저 돌릴 것.");
            return;
        }
        EnsureDir(AvatarDir);

        int done = 0, skipped = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string why = Rig(root, System.IO.Path.GetFileNameWithoutExtension(path), humanoid, quad);
                if (why == null) { PrefabUtility.SaveAsPrefabAsset(root, path); done++; }
                else { Debug.Log($"[PawnRig] 건너뜀 {System.IO.Path.GetFileName(path)} — {why}"); skipped++; }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[PawnRig] 관절 세움 {done} · 건너뜀 {skipped}");
    }

    // **관절을 제 손으로 세운 두발의 마무리** — 아바타 · 컨트롤러 · 폰 애니메이터(2026-10-09).
    // 스킨 메시 몸(`ChibiPawnCreator.Proto`)은 관절 자리와 가중치를 몸을 지으면서 낸다 — 살을 관절에 하나씩 붙이는
    // 이 도구의 셈(`RigBiped`)을 안 탄다. 대신 **아바타·컨트롤러는 여기 한 벌을 쓴다**(갈라 적으면 팔 한계·컨트롤러가 갈린다).
    // 부르기 전에 `PawnRig`의 두발 관절이 다 차 있고 **T자**여야 한다.
    public static bool FinishBiped(GameObject root, Transform model, string prefabName)
    {
        var humanoid = AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanoidClipSet.ControllerPath);
        if (humanoid == null) { Debug.LogError("[PawnRig] 컨트롤러가 없다 — `1. Bake Pawn Animations`를 먼저 돌릴 것."); return false; }
        EnsureDir(AvatarDir);

        if (!model.TryGetComponent(out Animator anim)) anim = model.gameObject.AddComponent<Animator>();
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        var avatar = BuildAvatar(model, prefabName);
        if (avatar == null) return false;
        anim.avatar = avatar;
        anim.runtimeAnimatorController = humanoid;

        if (!root.TryGetComponent(out PawnAnimator _)) root.AddComponent<PawnAnimator>();
        if (!root.TryGetComponent(out PawnProps _))    root.AddComponent<PawnProps>();
        return true;
    }

    // 돌려주는 것은 *왜 못 세웠나*(null이면 세웠다).
    static string Rig(GameObject root, string prefabName, AnimatorController humanoid, AnimatorController quad)
    {
        if (root.GetComponent<PawnMover>() == null) return "PawnMover가 없다";
        var model = root.transform.Find("Model");
        if (model == null) return "`Model`이 없다";

        Undress(root, model);
        if (model.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) return "스킨 메시(골렘 · 치비 시제품) — 제 뼈가 있다";

        Flatten(model);

        var parts = new Dictionary<string, Transform>();
        foreach (Transform c in model) parts[c.name] = c;

        PawnRig rig = model.GetComponent<PawnRig>();
        if (rig == null) rig = model.gameObject.AddComponent<PawnRig>();

        var anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.gameObject.AddComponent<Animator>();
        anim.applyRootMotion = false;
        // 화면 밖에서는 관절을 안 움직인다 — 폰이 많을 때 값을 아낀다.
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        if (parts.ContainsKey("LegFL"))
        {
            RigQuadruped(model, parts, rig);
            anim.avatar = null;
            anim.runtimeAnimatorController = quad;
        }
        else if (parts.ContainsKey("LegL"))
        {
            RigBiped(model, parts, rig);
            var avatar = BuildAvatar(model, prefabName);
            if (avatar == null) return "휴머노이드 아바타를 못 세웠다(콘솔 참고)";
            anim.avatar = avatar;
            anim.runtimeAnimatorController = humanoid;
        }
        else return "알아볼 수 있는 살이 없다(LegL/LegFL)";

        if (root.GetComponent<PawnAnimator>() == null) root.AddComponent<PawnAnimator>();
        if (root.GetComponent<PawnProps>()    == null) root.AddComponent<PawnProps>();
        return null;
    }

    // 마네킹을 입혀 둔 프리팹(옛 `PawnDresser`)을 되돌린다 — 그 길은 접었다.
    static void Undress(GameObject root, Transform model)
    {
        var block = root.transform.Find("BlockModel");
        if (block == null) return;

        var human = model.Find("Human");
        if (human != null) Object.DestroyImmediate(human.gameObject);

        model.localPosition = block.localPosition;
        model.localRotation = block.localRotation;
        model.localScale    = block.localScale;
        var kids = new List<Transform>();
        foreach (Transform c in block) kids.Add(c);
        foreach (var c in kids) c.SetParent(model, false);
        Object.DestroyImmediate(block.gameObject);
    }

    // 살을 `Model` 바로 아래로 되돌리고 관절을 지운다 — 다시 세울 수 있게.
    //
    // **관절 회전을 먼저 0으로** — T자로 세워 둔 팔을 그대로 떼면 살이 옆으로 벌어진 자리에
    // 남아, 다시 세울 때 어깨를 엉뚱한 데서 찾는다. **가른 큐브는 도로 합친다**(위 반쪽을 원래
    // 크기로 늘리고 아래 반쪽은 지운다).
    static void Flatten(Transform model)
    {
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
            if (t != model && t.GetComponent<MeshRenderer>() == null) t.localRotation = Quaternion.identity;

        var meshes = new List<Transform>();
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true)) meshes.Add(r.transform);
        foreach (var m in meshes)
        {
            m.SetParent(model, worldPositionStays: true);
            if (m.name.EndsWith("Mesh")) m.name = m.name.Substring(0, m.name.Length - 4);
        }

        foreach (var limb in new[] { "ArmL", "ArmR", "LegL", "LegR" })
        {
            var upper = model.Find(limb);
            var lower = model.Find("Lower" + limb);
            if (upper == null || lower == null) continue;

            var p = (upper.localPosition + lower.localPosition) * 0.5f;
            var s = upper.localScale; s.y *= 2f;
            upper.localPosition = p;
            upper.localScale    = s;
            Object.DestroyImmediate(lower.gameObject);
        }

        var joints = new List<GameObject>();
        foreach (Transform c in model) if (c.GetComponent<MeshRenderer>() == null) joints.Add(c.gameObject);
        foreach (var j in joints) Object.DestroyImmediate(j);

        // **`Model`은 발에 둔다**(2026-09-09). 휴머노이드는 아바타 뿌리(애니메이터가 붙은 오브젝트)를
        // 발 자리로 삼고 엉덩이 높이를 그 위에서 잰다. 블록 인형은 몸 한가운데가 원점이라 `Model`이
        // 1m 떠 있었고, 그대로 아바타를 세우니 엉덩이가 뿌리 **아래**(-0.35)에 있어 클립이 엉덩이를
        // 뿌리 위 0.9m로 놓는 순간 **폰이 통째로 1m 떠서 걸었다**(사용자: *"공중부양해!"*).
        // 살을 그만큼 올려 월드 자리는 그대로 두고 `Model`만 0으로 내린다.
        var off = model.localPosition;
        if (off.sqrMagnitude > 1e-6f)
        {
            float s = model.localScale.y > 1e-4f ? model.localScale.y : 1f;
            foreach (Transform c in model) c.localPosition += off / s;
            model.localPosition = Vector3.zero;
        }
    }

    // ── 두발 ────────────────────────────────────────────────────────────────
    //
    // 관절 자리는 살에서 낸다: 고관절 = 다리 꼭대기 · 무릎 = 다리 한가운데 · 발 = 다리 밑동 ·
    // 어깨 = 팔 꼭대기 · 팔꿈치 = 팔 한가운데 · 손 = 팔 밑동 · 목 = 머리 밑동.
    // 해골은 살의 이름이 다르다(Pelvis·Spine·Rib·Skull) — 같은 관절에 다른 살이 붙을 뿐이다.
    //
    // 계층은 유니티 휴머노이드가 요구하는 그것이다:
    //   Hips/{Spine/{Head, ArmL/LowerArmL/HandL, ArmR/LowerArmR/HandR}, LegL/LowerLegL/FootL, LegR/…}
    static void RigBiped(Transform model, Dictionary<string, Transform> parts, PawnRig rig)
    {
        rig.Kind = PawnRig.BodyKind.Biped;

        var legL = parts["LegL"]; var legR = parts["LegR"];
        var armL = parts["ArmL"]; var armR = parts["ArmR"];
        var head = Pick(parts, "Head", "Skull");

        float hipY = Top(legL);
        var hips  = Joint(model, "Hips", new Vector3(0f, hipY, 0f));
        var spine = Joint(hips,  "Spine", new Vector3(0f, hipY + 0.05f, 0f));
        var jHead = Joint(spine, "Head", new Vector3(0f, head != null ? Bottom(head) : hipY + 1f, 0f));

        Limb(spine, armL, "ArmL", "LowerArmL", "HandL", shoulderDrop: 0.03f, out rig.ArmL, out rig.LowerArmL, out rig.HandL);
        Limb(spine, armR, "ArmR", "LowerArmR", "HandR", shoulderDrop: 0.03f, out rig.ArmR, out rig.LowerArmR, out rig.HandR);
        Limb(hips,  legL, "LegL", "LowerLegL", "FootL", shoulderDrop: 0f,    out rig.LegL, out rig.LowerLegL, out rig.FootL);
        Limb(hips,  legR, "LegR", "LowerLegR", "FootR", shoulderDrop: 0f,    out rig.LegR, out rig.LowerLegR, out rig.FootR);

        if (head != null) Attach(head, jHead, head.name == "Head" ? "HeadMesh" : head.name);

        // **살 이름이 붙을 자리를 말한다**(2026-09-18). `Head.EyeL`·`HandL.Ball`처럼
        // **`관절.무엇` 꼴**이면 그 관절에 붙는다.
        //
        // 없을 때는 아래의 *"남은 살은 몸통이다"*에 전부 걸려서 **눈이 배에 가서 붙었다** —
        // `Model` 아래의 살은 예외 없이 관절 하나에 붙으므로, 머리에 얹을 것을 적을 자리가
        // 아예 없었다. **이름을 그대로 둔다** — `Flatten`이 `Mesh` 꼬리만 떼므로 되돌아온다.
        var joints = new Dictionary<string, Transform>
        {
            { "Hips", hips }, { "Spine", spine }, { "Head", jHead },
            { "ArmL", rig.ArmL }, { "LowerArmL", rig.LowerArmL }, { "HandL", rig.HandL },
            { "ArmR", rig.ArmR }, { "LowerArmR", rig.LowerArmR }, { "HandR", rig.HandR },
            { "LegL", rig.LegL }, { "LowerLegL", rig.LowerLegL }, { "FootL", rig.FootL },
            { "LegR", rig.LegR }, { "LowerLegR", rig.LowerLegR }, { "FootR", rig.FootR },
        };

        // 남은 살(Torso · Pelvis · Spine · Rib…)은 몸통이다.
        foreach (var kv in parts)
        {
            var t = kv.Value;
            if (t == null || t.parent != model) continue;

            int dot = kv.Key.IndexOf('.');
            if (dot > 0 && joints.TryGetValue(kv.Key.Substring(0, dot), out var j) && j != null)
            {
                Attach(t, j, kv.Key);
                continue;
            }

            if (kv.Key == "Pelvis") Attach(t, hips, "Pelvis");
            else                    Attach(t, spine, kv.Key == "Spine" ? "SpineMesh" : kv.Key);
        }

        // **T자 자세** — 아바타의 기준 자세. 왼팔은 -x, 오른팔은 +x로 벌린다.
        rig.ArmL.localRotation = Quaternion.Euler(0f, 0f, -90f);
        rig.ArmR.localRotation = Quaternion.Euler(0f, 0f,  90f);

        rig.Hips = hips; rig.Spine = spine; rig.Head = jHead;
        rig.Body = rig.Tail = rig.LegFL = rig.LegFR = rig.LegBL = rig.LegBR = null;
    }

    // 팔·다리 하나: 큐브를 반으로 갈라 위 관절·아래 관절에 나눠 달고, 끝에 손/발 관절을 둔다.
    static void Limb(Transform parent, Transform cube, string upperName, string lowerName, string tipName,
                     float shoulderDrop, out Transform upper, out Transform lower, out Transform tip)
    {
        float top = Top(cube), mid = cube.localPosition.y, bottom = Bottom(cube);
        float x = cube.localPosition.x, z = cube.localPosition.z;

        upper = Joint(parent, upperName, new Vector3(x, top - shoulderDrop, z));
        lower = Joint(upper,  lowerName, new Vector3(x, mid, z));
        tip   = Joint(lower,  tipName,   new Vector3(x, bottom, z));

        // 반으로 가른다 — 아래 반쪽은 복제.
        var lowerCube = Object.Instantiate(cube.gameObject, cube.parent).transform;
        lowerCube.name = cube.name;
        var half = cube.localScale; half.y *= 0.5f;
        cube.localScale      = half;
        cube.localPosition   = new Vector3(x, (top + mid) * 0.5f, z);
        lowerCube.localScale    = half;
        lowerCube.localPosition = new Vector3(x, (mid + bottom) * 0.5f, z);

        Attach(cube,      upper, upperName + "Mesh");
        Attach(lowerCube, lower, lowerName + "Mesh");
    }

    // ── 휴머노이드 아바타 ───────────────────────────────────────────────────
    //
    // 사람 뼈 열다섯(필수)을 우리 관절 이름에 물린다. 근육 한계는 기본값. 나머지(어깨·목·발가락·
    // 손가락)는 없어도 된다 — 없으면 그 관절은 클립이 그냥 건너뛴다.
    static readonly (string human, string bone)[] HumanBones =
    {
        ("Hips", "Hips"), ("Spine", "Spine"), ("Head", "Head"),
        ("LeftUpperArm", "ArmL"), ("LeftLowerArm", "LowerArmL"), ("LeftHand", "HandL"),
        ("RightUpperArm", "ArmR"), ("RightLowerArm", "LowerArmR"), ("RightHand", "HandR"),
        ("LeftUpperLeg", "LegL"), ("LeftLowerLeg", "LowerLegL"), ("LeftFoot", "FootL"),
        ("RightUpperLeg", "LegR"), ("RightLowerLeg", "LowerLegR"), ("RightFoot", "FootR"),
    };

    // **팔을 끝까지 내릴 수 있게 한다**(2026-09-18). 유니티 휴머노이드에서 팔의 *내림*은 두 근육이 나눠
    // 든다 — 어깨(-15°)와 위팔(-60°). 이 뼈대에는 **어깨 뼈가 없어서** 위팔 몫 -60°만 남고, 그래서 대기
    // 클립이 *팔을 다 내려라*라고 해도 **T자에서 60°까지만** 내려가 팔이 30° 벌어진 채 선다(치비 폰은
    // 그 30°에 손 공이 들려 턱 옆에 떠 있었다). 어깨를 더하는 대신 **위팔의 내림 한계를 90°로** 넓힌다 —
    // 어깨 뼈를 넣으면 관절 규격(`PawnRig`)과 클립 경로가 바뀌어 모든 폰을 다시 세워야 한다.
    // 나머지 축(비틀기·앞뒤)과 올림은 기본값 그대로다.
    static HumanLimit ArmLimit(string humanName, Transform model, string boneName)
    {
        int bone = System.Array.IndexOf(HumanTrait.BoneName, humanName);
        // 이름을 못 찾으면 기본값 — 0으로 채운 한계를 넘기면 **팔이 통째로 굳는다**.
        if (bone < 0)
        {
            Debug.LogWarning($"[PawnRig] 휴머노이드 뼈 이름을 못 찾았다: {humanName} — 팔 한계를 기본값으로 둔다.");
            return new HumanLimit { useDefaultValues = true };
        }
        var min = Vector3.zero; var max = Vector3.zero;
        for (int dof = 0; dof < 3; dof++)
        {
            int m = HumanTrait.MuscleFromBone(bone, dof);
            if (m < 0) continue;
            min[dof] = HumanTrait.GetMuscleDefaultMin(m);
            max[dof] = HumanTrait.GetMuscleDefaultMax(m);
            if (HumanTrait.MuscleName[m].Contains("Down-Up")) min[dof] = -90f;
        }

        // 뼈 길이 — 위팔 관절에서 아래팔 관절까지(`axisLength`를 0으로 두면 한계가 제대로 안 선다).
        float length = 0.25f;
        var upper = FindDeep(model, boneName);
        if (upper != null)
            foreach (Transform c in upper)
                if (c.name.StartsWith("LowerArm")) { length = c.localPosition.magnitude; break; }

        return new HumanLimit { useDefaultValues = false, min = min, max = max, center = Vector3.zero, axisLength = length };
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
        return null;
    }

    static Avatar BuildAvatar(Transform model, string prefabName)
    {
        var human = new HumanBone[HumanBones.Length];
        for (int i = 0; i < HumanBones.Length; i++)
        {
            human[i] = new HumanBone { humanName = HumanBones[i].human, boneName = HumanBones[i].bone };
            human[i].limit.useDefaultValues = true;
            if (HumanBones[i].human == "LeftUpperArm" || HumanBones[i].human == "RightUpperArm")
                human[i].limit = ArmLimit(HumanBones[i].human, model, HumanBones[i].bone);
        }

        // 뼈대 = 계층의 **모든** 트랜스폼(뿌리 포함), 지금 자세(T자) 그대로.
        var all = model.GetComponentsInChildren<Transform>(true);
        var skeleton = new SkeletonBone[all.Length];
        for (int i = 0; i < all.Length; i++)
            skeleton[i] = new SkeletonBone
            {
                name     = all[i].name,
                position = all[i].localPosition,
                rotation = all[i].localRotation,
                scale    = all[i].localScale,
            };

        var desc = new HumanDescription
        {
            human = human, skeleton = skeleton,
            upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
            armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
        };

        var built = AvatarBuilder.BuildHumanAvatar(model.gameObject, desc);
        if (built == null || !built.isValid || !built.isHuman)
        {
            Debug.LogError($"[PawnRig] {prefabName}: 휴머노이드 아바타가 서지 않았다 — 관절 열다섯이 다 있는지, T자인지 볼 것.");
            return null;
        }

        // **제자리에 저장한다**(guid 유지). 아바타는 값을 통째로 갈아 끼울 수 있다.
        // **이름을 먼저 준다** — `BuildHumanAvatar`가 낸 것은 이름이 비어 있어, 그대로 복사하면
        // 에셋의 주 오브젝트 이름이 파일명과 안 맞는다는 경고가 프리팹마다 뜬다.
        built.name = prefabName;
        string path = AvatarDir + "/" + prefabName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Avatar>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(built, path);
            return built;
        }
        EditorUtility.CopySerialized(built, existing);
        existing.name = prefabName;
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // ── 네발 ────────────────────────────────────────────────────────────────
    static void RigQuadruped(Transform model, Dictionary<string, Transform> parts, PawnRig rig)
    {
        rig.Kind = PawnRig.BodyKind.Quadruped;

        var body = parts["Body"];
        var head = parts["Head"];
        var tail = parts.TryGetValue("Tail", out var tl) ? tl : null;

        var jBody = Joint(model, "Body", body.localPosition);

        // 목 = 머리 뒤쪽 면(몸통 쪽). 머리·주둥이·귀가 여기 매달린다.
        var jHead = Joint(jBody, "Head", new Vector3(0f, head.localPosition.y,
                                                     head.localPosition.z - head.localScale.z * 0.5f));
        Transform jTail = null;
        if (tail != null)
            jTail = Joint(jBody, "Tail", new Vector3(0f, tail.localPosition.y,
                                                     tail.localPosition.z + tail.localScale.z * 0.5f));

        var jFL = LegJoint(jBody, parts, "LegFL"); var jFR = LegJoint(jBody, parts, "LegFR");
        var jBL = LegJoint(jBody, parts, "LegBL"); var jBR = LegJoint(jBody, parts, "LegBR");

        Attach(body, jBody, "BodyMesh");
        Attach(head, jHead, "HeadMesh");
        if (tail != null) Attach(tail, jTail, "TailMesh");
        foreach (var kv in parts)
        {
            var t = kv.Value;
            if (t == null || t.parent != model) continue;
            // 주둥이·귀처럼 머리 앞쪽에 있는 것은 머리에, 나머지는 몸통에.
            bool onHead = t.localPosition.z > jHead.localPosition.z - 0.01f;
            Attach(t, onHead ? jHead : jBody, kv.Key);
        }

        rig.Body = jBody; rig.Head = jHead; rig.Tail = jTail;
        rig.LegFL = jFL; rig.LegFR = jFR; rig.LegBL = jBL; rig.LegBR = jBR;
        rig.Hips = rig.Spine = rig.ArmL = rig.ArmR = rig.LegL = rig.LegR = rig.HandL = rig.HandR = null;
        rig.LowerArmL = rig.LowerArmR = rig.LowerLegL = rig.LowerLegR = rig.FootL = rig.FootR = null;
    }

    static Transform LegJoint(Transform parent, Dictionary<string, Transform> parts, string name)
    {
        if (!parts.TryGetValue(name, out var leg)) return null;
        var j = Joint(parent, name, new Vector3(leg.localPosition.x, Top(leg), leg.localPosition.z));
        Attach(leg, j, name + "Mesh");
        return j;
    }

    // ── 도우미 ──────────────────────────────────────────────────────────────

    // 관절 하나. `modelLocal`은 **Model 기준** 자리다 — 부모가 무엇이든 그 자리에 선다.
    static Transform Joint(Transform parent, string name, Vector3 modelLocal)
    {
        var model = parent;
        while (model.GetComponent<PawnRig>() == null && model.parent != null) model = model.parent;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = model.TransformPoint(modelLocal);
        go.transform.rotation = model.rotation;
        go.layer = parent.gameObject.layer;
        return go.transform;
    }

    // 살을 관절 아래로 — **월드 자세를 그대로** 둔다(세운 직후의 모습이 전과 같아야 한다).
    static void Attach(Transform mesh, Transform joint, string newName)
    {
        mesh.SetParent(joint, worldPositionStays: true);
        mesh.name = newName;
    }

    static Transform Pick(Dictionary<string, Transform> parts, params string[] names)
    {
        foreach (var n in names) if (parts.TryGetValue(n, out var t)) return t;
        return null;
    }

    // 살의 위·아래 끝(Model 기준). 살은 큐브·구라 로컬 스케일이 곧 크기다.
    static float Top(Transform t)    => t.localPosition.y + t.localScale.y * 0.5f;
    static float Bottom(Transform t) => t.localPosition.y - t.localScale.y * 0.5f;

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf   = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
