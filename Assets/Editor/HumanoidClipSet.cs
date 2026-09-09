using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// **받은 휴머노이드 클립을 컨트롤러 하나로 묶는다**(2026-09-09).
//
// 사용자가 받아 온 `assetsss/Kevin Iglesias/Human Animations`(휴머노이드 클립 300여 개)를 쓴다.
// 휴머노이드 클립은 **휴머노이드 아바타가 있는 뼈대**에 붙는다 — 그 아바타는 `PawnRigBuilder`가
// 블록 인형 관절 위에 `AvatarBuilder`로 만든다(생김새는 그대로, 뼈대만 사람 규격). 그래서 이
// 컨트롤러는 사람·고블린·해골이 같이 쓴다.
//
// **매개변수는 `PawnAnimator`가 넣는 셋 그대로다**(`Speed` · `Attack` · `Lying`) — 네발의 블록
// 관절 컨트롤러와 같아야 `PawnAnimator`가 어느 쪽인지 몰라도 된다.
//
// **공격 클립이 팩에 없다** — 전투 폴더는 피격·죽음뿐이다. 나무 찍기(`TreeChopping01`)를 휘두르기로
// 쓴다. 제대로 된 공격 클립을 받으면 `AttackClip` 한 줄만 바꾸면 된다.
//
// **누움은 아직 루트 회전이다**(`PawnMover`가 눕힌다 — 침대·업기·기절·시체가 전부 그 길). 그래서
// `Lying` 상태는 서 있는 자세를 **얼려 두기만** 한다. 죽음 클립으로 옮기는 것은 그 넷을 한꺼번에
// 옮겨야 하는 다른 일이다.
//
// **팩은 git 밖이다**(`assetsss/`는 무시 목록) — 이 파일의 경로는 그 폴더 이름에 기댄다.
public static class HumanoidClipSet
{
    public const string ControllerPath = "Assets/Controller/Humanoid.controller";

    const string Pack     = "Assets/assetsss/Kevin Iglesias/Human Animations";
    const string ClipRoot = Pack + "/Animations/Male";

    const string IdleClip   = ClipRoot + "/Idles/HumanM@Idle01.fbx";
    const string WalkClip   = ClipRoot + "/Movement/Walk/HumanM@Walk01_Forward.fbx";
    const string RunClip    = ClipRoot + "/Movement/Run/HumanM@Run01_Forward.fbx";
    const string AttackClip = ClipRoot + "/Work/Chopping/HumanM@TreeChopping01 - Loop.fbx";

    // 일 동작 — `WorkAnim` 값마다 하나. 없는 값(None)은 상태를 안 만든다.
    //
    // **채굴은 땅을 내려찍는다**(`Loop Ground`, 2026-09-10에 `Loop Wall`에서 바꿨다 — 사용자:
    // *"아예 내려 찍어야하는 거 아닐까?"*). 팩은 벽용·땅용 두 벌을 주는데, 벽용은 앞으로 **가로로**
    // 치는 동작이라 곡괭이가 땅과 나란히 누운 채 흔들린다. 지하에서 캐는 것이 벽이긴 하지만
    // 화면에서 *판다*로 읽히는 것은 내려찍는 쪽이고, 두 손 자루가 양손을 따라가므로(`HandProp`)
    // 곡괭이도 같이 호를 그린다.
    static readonly (WorkAnim work, string clip)[] WorkClips =
    {
        (WorkAnim.Mining,      ClipRoot + "/Work/Mining/HumanM@Mining01 - Loop Ground.fbx"),
        (WorkAnim.Chopping,    ClipRoot + "/Work/Chopping/HumanM@TreeChopping01 - Loop.fbx"),
        (WorkAnim.HammerWall,  ClipRoot + "/Work/Hammering/HumanM@HammeringWall01_R - Loop.fbx"),
        (WorkAnim.HammerTable, ClipRoot + "/Work/Hammering/HumanM@HammeringTable01_R - Loop.fbx"),
        (WorkAnim.Seeding,     ClipRoot + "/Work/Seeding/HumanM@Seeding01_R.fbx"),
        (WorkAnim.Watering,    ClipRoot + "/Work/Watering/HumanM@Watering01_R.fbx"),
        (WorkAnim.Gathering,   ClipRoot + "/Work/Gathering/HumanM@Gathering01.fbx"),
        (WorkAnim.Skinning,    ClipRoot + "/Work/Skinning/HumanM@SkinningTable01_R - Loop.fbx"),
    };

    // 걷기·달리기 클립이 발이 안 미끄러지는 속도(m/s) — 팩이 적어 두지 않아 눈으로 맞춘 값.
    // 틀리면 발이 밀린다. 인스펙터에서 블렌드 트리의 문턱을 바로 고쳐도 된다.
    const float WalkAt = 1.6f, RunAt = 4.5f;

    // 컨트롤러: Locomotion(Idle·Walk·Run을 `Speed`로 섞는다) · AnyState → Attack → Locomotion ·
    // AnyState ⇄ Lying. **제자리에서 다시 짓는다**(guid 유지) — 지웠다 만들면 프리팹의 참조가 끊긴다.
    public static AnimatorController BuildController()
    {
        var idle   = Clip(IdleClip);
        var walk   = Clip(WalkClip);
        var run    = Clip(RunClip);
        var attack = Clip(AttackClip);
        if (idle == null || walk == null || run == null || attack == null)
        {
            Debug.LogError("[HumanoidClipSet] 클립을 못 찾았다 — 팩이 `" + Pack + "`에 있는지 볼 것.");
            return null;
        }

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else
        {
            for (int i = ctrl.parameters.Length - 1; i >= 0; i--) ctrl.RemoveParameter(i);
            for (int i = ctrl.layers.Length - 1; i >= 0; i--) ctrl.RemoveLayer(i);
            ctrl.AddLayer("Base Layer");
        }

        ctrl.AddParameter("Speed",  AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Lying",  AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Work",   AnimatorControllerParameterType.Int);

        var sm = ctrl.layers[0].stateMachine;

        var loco = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType      = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, WalkAt);
        tree.AddChild(run,  RunAt);
        sm.defaultState = loco;

        var sAttack = sm.AddState("Attack", new Vector3(560, 60));
        sAttack.motion = attack;

        var sLying = sm.AddState("Lying", new Vector3(560, 200));
        sLying.motion = idle;
        sLying.speed  = 0f;   // 자세는 루트 회전이 만든다 — 관절만 멈춘다

        var t = sm.AddAnyStateTransition(sAttack);
        t.hasExitTime = false; t.duration = 0.08f; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If,    0f, "Attack");
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Lying");

        t = sAttack.AddTransition(loco);
        t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.15f;

        t = sm.AddAnyStateTransition(sLying);
        t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, "Lying");

        t = sLying.AddTransition(loco);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Lying");

        // ── 일 상태들 — 서 있을 때(`Speed` < 0.3) `Work`가 그 값이면 들어가고, 값이 바뀌면 나온다.
        // 걷는 동안은 안 들어간다(일감은 자리에서만 `Working`을 적으므로 겹칠 일도 드물다).
        int row = 0;
        foreach (var (work, path) in WorkClips)
        {
            var clip = Clip(path);
            if (clip == null) continue;

            var s = sm.AddState(work.ToString(), new Vector3(60, 320 + row * 60));
            s.motion = clip;
            row++;

            var into = loco.AddTransition(s);
            into.hasExitTime = false; into.duration = 0.15f;
            into.AddCondition(AnimatorConditionMode.Equals, (int)work, "Work");
            into.AddCondition(AnimatorConditionMode.Less,   0.3f,      "Speed");

            var outOf = s.AddTransition(loco);
            outOf.hasExitTime = false; outOf.duration = 0.15f;
            outOf.AddCondition(AnimatorConditionMode.NotEqual, (int)work, "Work");
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // FBX 안의 클립 — 미리보기 클립(`__preview__`)은 빼고 첫 것.
    static AnimationClip Clip(string fbxPath)
    {
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        Debug.LogError("[HumanoidClipSet] 클립 없음 — " + fbxPath);
        return null;
    }
}
