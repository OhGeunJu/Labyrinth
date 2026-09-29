using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// **개미 — 팩 모델(ithappy Fantasy 15번)로 서는 첫 짐승**(2026-09-29, 사용자: *"15번이 개미인데 개미만 넣어봐"*).
//
// ## 늑대와 무엇이 다른가
//
// 늑대·곰은 큐브 조각을 관절에 매달아 **클립을 코드로 굽는다**(`PawnRigBuilder` · `Quadruped.controller`).
// 개미는 **제 뼈와 걷기 클립을 가진 스킨 메시**라 그 길을 안 탄다 — 관절 도구는 스킨 메시를 건너뛴다(골렘과 같은 자리).
// 대신 **걷기 클립 하나로 컨트롤러를 만든다**: 재생 속도를 `Speed`(m/s — `PawnAnimator`가 넣는다)에 곱해
// **서 있으면 멈추고 빨리 걸으면 빨리 걷는다.** `Attack`·`Lying`도 만들어 둔다 — 없는 매개변수에 값을 넣으면
// 유니티가 매 프레임 경고한다(물 때 따로 도는 동작은 아직 없다).
//
// ## 무엇을 만드나 — 다시 돌려도 된다(있으면 덮는다 · guid 유지)
//
//   · `Controller/Ant.controller` — 덮는다
//   · `3_Prefab/Pawn/Enemy_Ant.prefab` — **늑대 프리팹을 본뜬다**(컴포넌트·세력·콜라이더가 그대로) · `Model` 아래를 개미로 ·
//     종족·로드아웃은 개미 것을 물린다. 몸을 다시 짓는 것이 이 도구의 일이라 덮는다(guid 유지)
//   · `Resources/Data/Units/Ant.asset` · `5_Data/Loadout/Loadout_Ant.asset` · `5_Data/World/Band/Band_Ants.asset` —
//     **없을 때만 만든다**(2026-09-29, 사용자: *"개미는 개미만의 데이터를 써야"*). 처음엔 돌릴 때마다 늑대 값을
//     `CopySerialized`로 다시 받아, 개미 값을 고쳐 놓아도 이 도구를 한 번 더 누르면 **늑대로 돌아갔다.**
//     값은 저작물이다 — 도구는 그릇만 놓는다.
//   · `Resources/Data/Factions/Faction_Ant.asset` — 없을 때만(야수를 본뜬다) · 프리팹과 새 개미 떼에 물린다
//   · 바이옴 표의 짐승 줄 → 개미 떼 바꿔 끼우기는 **개미 떼를 새로 만들 때 한 번만**
//   · 바이옴의 **저절로 나는 무리**에서 짐승 무리 줄을 개미 떼로 바꾼다(사용자: 개미만 먼저)
public static class AntCreator
{
    const string ArtPrefab  = "Assets/assetsss/ithappy/Fantasy/Prefabs/animals/Fantasy_animal_15.prefab";
    const string WalkClip   = "Assets/assetsss/ithappy/Fantasy/Animations/fantasy_animal_rig_015_walk.anim";
    const string Controller = "Assets/Controller/Ant.controller";

    const string WolfPrefab = "Assets/3_Prefab/Pawn/Enemy_Wolf.prefab";
    const string AntPrefab  = "Assets/3_Prefab/Pawn/Enemy_Ant.prefab";
    const string WolfUnit   = "Assets/Resources/Data/Units/Wolf.asset";
    const string AntUnit    = "Assets/Resources/Data/Units/Ant.asset";
    const string BeastBand  = "Assets/5_Data/World/Band/Band_Beasts.asset";
    const string AntBand    = "Assets/5_Data/World/Band/Band_Ants.asset";
    const string AntLoadout = "Assets/5_Data/Loadout/Loadout_Ant.asset";
    const string BeastFaction = "Assets/Resources/Data/Factions/Faction_Beast.asset";
    const string AntFaction   = "Assets/Resources/Data/Factions/Faction_Ant.asset";
    const string BiomeDir   = "Assets/Resources/Data/Biomes";

    // 몸길이(m) — 늑대(1m 남짓)와 비슷하게. 팩 모델의 크기는 제각각이라 **재어서 맞춘다.**
    const float BodyLength = 1.1f;

    // 걷기 클립이 1배로 도는 속도(m/s). 이보다 빠르면 빨리 걷는다.
    const float WalkAtSpeed = 2.5f;

    [MenuItem("DungeonColony/Pawn/7. Create Ant (개미 · 개미 떼 · 저절로 나는 무리)")]
    static void Run()
    {
        var art  = AssetDatabase.LoadAssetAtPath<GameObject>(ArtPrefab);
        var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClip);
        if (art == null || walk == null) { Debug.LogError($"[개미] 팩 모델이나 걷기 클립이 없다 — {ArtPrefab} / {WalkClip}"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(WolfPrefab) == null) { Debug.LogError($"[개미] 본뜰 늑대가 없다 — {WolfPrefab}"); return; }

        var ctrl = MakeController(walk);
        var unit = CreateOnce<UnitData>(WolfUnit, AntUnit, out bool newUnit);
        if (unit != null && newUnit)
        {
            unit.RaceName = "개미";
            unit.NamePool = new List<string>();
            unit.CanEquip = false;
            EditorUtility.SetDirty(unit);
        }

        var loadout = AssetDatabase.LoadAssetAtPath<LoadoutData>(AntLoadout);
        if (loadout == null)
        {
            loadout = ScriptableObject.CreateInstance<LoadoutData>();
            AssetDatabase.CreateAsset(loadout, AntLoadout);
        }

        // **개미는 개미 세력이다**(2026-09-29, 사용자: *"야수 팩션으로 한 번에 하는 게 아니라 개미는 개미 팩션에"*).
        // 프리팹을 늑대에서 본뜨므로 안 물리면 돌릴 때마다 야수로 돌아간다.
        var faction = CreateOnce<FactionData>(BeastFaction, AntFaction, out bool newFaction);
        if (faction != null && newFaction)
        {
            var fso = new SerializedObject(faction);
            fso.FindProperty("factionName").stringValue = "개미";
            fso.ApplyModifiedPropertiesWithoutUndo();
        }

        if (!MakePrefab(art, ctrl, unit, loadout, faction)) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AntPrefab);

        var band = CreateOnce<WorldBandData>(BeastBand, AntBand, out bool newBand);
        if (band != null && newBand)
        {
            var so = new SerializedObject(band);
            if (faction != null) so.FindProperty("faction").objectReferenceValue = faction;
            so.FindProperty("bandName").stringValue = "개미 떼";
            so.FindProperty("tokenColor").colorValue = new Color(0.55f, 0.30f, 0.20f);
            var members = so.FindProperty("members");
            members.arraySize = 1;
            var m = members.GetArrayElementAtIndex(0);
            m.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            m.FindPropertyRelative("MinCount").intValue = 3;
            m.FindPropertyRelative("MaxCount").intValue = 6;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // **바꿔 끼우기는 처음 한 번만** — 개미 떼를 새로 만들 때다. 매번 하면 나중에 바이옴 표에 다시 넣은
        // 짐승 무리(늑대·곰)까지 돌릴 때마다 개미로 바뀐다.
        int swapped = newBand ? SwapWildSpawns(AssetDatabase.LoadAssetAtPath<WorldBandData>(BeastBand), band) : 0;

        AssetDatabase.SaveAssets();
        Debug.Log($"[개미] 개미(`{AntPrefab}`) · 컨트롤러를 다시 지었습니다. " +
                  $"종족 {(newUnit ? "새로 만듦(늑대 본뜸 — 값을 고칠 자리는 `Ant.asset`)" : "그대로 둠")} · " +
                  $"개미 떼 {(newBand ? "새로 만듦(3~6)" : "그대로 둠")} · " +
                  $"저절로 나는 무리에서 짐승 줄 {swapped}개를 개미 떼로 바꿨습니다.\n" +
                  "**개미가 옆으로 걷거나 뒤로 걸으면** 모델이 바라보는 쪽이 다른 것입니다 — 말해 주세요.");
    }

    // ── 컨트롤러 — 걷기 클립 하나 ───────────────────────────────────────────

    static AnimatorController MakeController(AnimationClip walk)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller)
                ?? AnimatorController.CreateAnimatorControllerAtPath(Controller);

        // 매개변수 — `PawnAnimator`가 넣는 셋.
        foreach (var p in ctrl.parameters) ctrl.RemoveParameter(p);
        ctrl.AddParameter("Speed",  AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Lying",  AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        foreach (var s in sm.states) sm.RemoveState(s.state);

        var move = sm.AddState("Move");
        move.motion = walk;
        // **재생 속도 = 상수 × `Speed`** — 서 있으면 0이라 그 자세로 멈추고, 빨리 걸으면 빨리 걷는다.
        move.speed = 1f / WalkAtSpeed;
        move.speedParameterActive = true;
        move.speedParameter = "Speed";
        sm.defaultState = move;

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // ── 프리팹 — 늑대를 본뜨고 몸만 개미로 ─────────────────────────────────

    static bool MakePrefab(GameObject art, AnimatorController ctrl, UnitData unit, LoadoutData loadout, FactionData faction)
    {
        // **있으면 내용만 덮는다** — 무리 에셋이 guid로 가리킨다(`ChibiPawnCreator`와 같은 규칙).
        if (System.IO.File.Exists(AntPrefab))
        {
            System.IO.File.Copy(WolfPrefab, AntPrefab, true);
            AssetDatabase.ImportAsset(AntPrefab, ImportAssetOptions.ForceUpdate);
        }
        else AssetDatabase.CopyAsset(WolfPrefab, AntPrefab);

        var root = PrefabUtility.LoadPrefabContents(AntPrefab);
        try
        {
            var model = root.transform.Find("Model");
            if (model == null) { Debug.LogError("[개미] 늑대에 `Model`이 없다."); return false; }

            var kids = new List<GameObject>();
            foreach (Transform c in model) kids.Add(c.gameObject);
            foreach (var k in kids) Object.DestroyImmediate(k);

            // 늑대의 관절 · 애니메이터는 조각 몸의 것이다 — 개미는 제 뼈를 쓴다.
            var oldRig = model.GetComponent<PawnRig>();
            if (oldRig != null) Object.DestroyImmediate(oldRig);
            var oldAnim = model.GetComponent<Animator>();
            if (oldAnim != null) Object.DestroyImmediate(oldAnim);

            var body = (GameObject)PrefabUtility.InstantiatePrefab(art, model);
            body.name = "Art";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale    = Vector3.one;
            foreach (var t in body.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
            foreach (var c in body.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);   // 고르기는 루트 콜라이더 하나

            // **재서 맞춘다** — 가로로 가장 긴 쪽을 몸길이로, **발밑을 피벗에**(피벗이 발밑이라는 규약 — `PawnPlacement`).
            var b = BoundsOf(body);
            float len = Mathf.Max(b.size.x, b.size.z);
            if (len > 1e-4f) body.transform.localScale = Vector3.one * (BodyLength / len);
            b = BoundsOf(body);
            var shift = model.InverseTransformVector(new Vector3(-(b.center.x - model.position.x), -(b.min.y - model.position.y),
                                                                 -(b.center.z - model.position.z)));
            body.transform.localPosition += shift;

            var anim = body.GetComponentInChildren<Animator>(true);   // `??` 금지 — 에디터의 가짜 null
            if (anim == null) anim = body.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var brain = root.GetComponent<UnitBrain>();
            if (brain != null && unit != null)
            {
                var so = new SerializedObject(brain);
                so.FindProperty("unitData").objectReferenceValue = unit;
                if (loadout != null) so.FindProperty("loadout").objectReferenceValue = loadout;   // 늑대 것을 같이 쓰지 않는다
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (root.GetComponent<PawnAnimator>() == null) root.AddComponent<PawnAnimator>();

            var f = root.GetComponent<Faction>();
            if (f != null && faction != null)
            {
                var fso = new SerializedObject(f);
                fso.FindProperty("data").objectReferenceValue = faction;   // 늑대의 야수 세력을 물려받지 않는다
                fso.ApplyModifiedPropertiesWithoutUndo();
            }

            root.name = "Enemy_Ant";
            PrefabUtility.SaveAsPrefabAsset(root, AntPrefab);
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Bounds BoundsOf(GameObject go)
    {
        var b = new Bounds(go.transform.position, Vector3.zero);
        bool any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // ── 에셋 본뜨기 — **없을 때만**(위 머리글: 값은 저작물이다) ───────────────

    static T CreateOnce<T>(string from, string to, out bool created) where T : ScriptableObject
    {
        created = false;
        var dst = AssetDatabase.LoadAssetAtPath<T>(to);
        if (dst != null) return dst;

        if (AssetDatabase.LoadAssetAtPath<T>(from) == null) { Debug.LogWarning($"[개미] 본뜰 에셋이 없다 — {from}"); return null; }
        AssetDatabase.CopyAsset(from, to);
        created = true;
        return AssetDatabase.LoadAssetAtPath<T>(to);
    }

    // 저절로 나는 무리에서 짐승 줄을 개미 떼로 — 확률 · 날수는 그대로.
    static int SwapWildSpawns(WorldBandData from, WorldBandData to)
    {
        if (from == null || to == null) return 0;
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:BiomeContentData", new[] { BiomeDir }))
        {
            var b = AssetDatabase.LoadAssetAtPath<BiomeContentData>(AssetDatabase.GUIDToAssetPath(guid));
            if (b?.WildSpawns == null) continue;
            bool dirty = false;
            foreach (var e in b.WildSpawns)
                if (e != null && e.Band == from) { e.Band = to; n++; dirty = true; }
            if (dirty) EditorUtility.SetDirty(b);
        }
        return n;
    }
}
