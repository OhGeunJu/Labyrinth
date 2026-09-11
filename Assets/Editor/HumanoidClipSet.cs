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
// **매개변수는 `PawnAnimator`가 넣는 것 그대로다**(`Speed` · `Attack` · `Lying` · `Work` ·
// `Style`) — 네발의 블록 관절 컨트롤러와 이름이 같아야 `PawnAnimator`가 어느 쪽인지 몰라도
// 된다(네발에 없는 `Work`·`Style`은 그쪽이 있는지 한 번 보고 넣는다).
//
// **공격은 무기마다 다르다**(2026-09-11) — `ARPGPack`이 세트마다 콤보를 주고 그 이름이 우리 갈래와
// 거의 1:1이다(Warrior 한손 · Samurai 양손 · Halberd 장병기). `PawnAnimator`가 넣는 `Style`
// 정수가 어느 공격으로 갈지 고르고, 스타일은 **무기 종류에서 파생된다**(`WeaponTypeExt.AttackStyle`).
//
// **클립이 없는 스타일도 상태를 갖는다**(활·캐스팅·맨손 — 아직 어느 팩에도 없어서 기본 공격으로
// 떨어진다). 빼면 **그 무기를 든 폰은 트리거가 소비되지 않아 아무 동작도 안 한다** — 그게 바로
// 09-09까지 창병이 *공격 모션이 없는 것처럼* 보였던 모양이다. 클립을 받으면 `StyleClips`에 한 줄.
//
// 그전에는 나무 찍기(`TreeChopping01`)가 공격이었다 — KI 팩의 `Combat` 폴더가 피격·죽음뿐이라
// 쓸 것이 없었다.
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

    // `ARPGPack`의 클립은 **휴머노이드 머슬 곡선**이라(`m_HasGenericRootTransform: 0`) 우리
    // `AvatarBuilder` 아바타에 그대로 붙는다. 루트 모션은 애니메이터가 안 쓴다
    // (`PawnRigBuilder`가 `applyRootMotion = false` — 위치는 `PawnMover`가 옮긴다).
    const string ARPG = "Assets/assetsss/ARPGPack";

    // 클립이 없는 스타일이 떨어질 자리. **한손 휘두르기**로 둔다 — 맨손에도 활에도 맞지는
    // 않지만, 아무 동작도 안 하는 것보다는 낫다(그리고 그게 비어 있다는 것이 화면에 보인다).
    const string AttackClip = ARPG + "/ARPGWarrior/Animations/Humanoid/ARPG_Warrior_Attack_Combo1.anim";

    // 스타일 → 공격 클립. **없는 스타일은 적지 않는다**(기본으로 떨어지고 로그가 알린다).
    // 받아야 하는 것 셋: 활(당기기·조준·쏘기) · 캐스팅 · 맨손 지르기.
    static readonly (AttackStyle style, string clip)[] StyleClips =
    {
        (AttackStyle.OneHand, ARPG + "/ARPGWarrior/Animations/Humanoid/ARPG_Warrior_Attack_Combo1.anim"),
        (AttackStyle.TwoHand, ARPG + "/ARPGSamurai/Animations/Humanoid/ARPG_Samurai_Attack_Combo1.anim"),
        (AttackStyle.Polearm, ARPG + "/ARPGHalberd/Animations/Humanoid/ARPG_Halberd_Attack_Combo1.anim"),
    };

    // 일 동작 — `WorkAnim` 값마다 하나. 없는 값(None)은 상태를 안 만든다.
    //
    // **채굴은 땅을 내려찍는다**(`Loop Ground`, 2026-09-10에 `Loop Wall`에서 바꿨다 — 사용자:
    // *"아예 내려 찍어야하는 거 아닐까?"*). 팩은 벽용·땅용 두 벌을 주는데, 벽용은 앞으로 **가로로**
    // 치는 동작이라 곡괭이가 땅과 나란히 누운 채 흔들린다. 지하에서 캐는 것이 벽이긴 하지만
    // 화면에서 *판다*로 읽히는 것은 내려찍는 쪽이고, 두 손 자루가 양손을 따라가므로(`HandProp`)
    // 곡괭이도 같이 호를 그린다.
    // **같은 일도 자리가 다르면 다른 클립이다**(2026-09-11, 사용자) — 도축은 대가 있으면 서서
    // 없으면 쭈그려, 건설은 벽이면 앞을 바닥이면 발밑을 두들긴다. 가르는 것은 **일감이 이미
    // 아는 사실**이라(`_table != null` · `LevelFloor`) 화면이 다시 짐작하지 않는다.
    //
    // **팩이 `_L`·`_R`을 나눠 주지만 오른손만 쓴다** — 왼손잡이 축이 게임에 없어서, 넣으면
    // 폰마다 손을 정하는 저작이 하나 는다(그리고 손에 드는 것도 같이 갈라야 한다).
    //
    // 눈에 안 차면 **여기 한 줄**이다 — 씨 뿌리기는 `Seeding02`, 채집은 `Gathering02`·`03`,
    // 망치질은 `Anvil`도 있다(클립 고르기는 눈으로 맞추는 값이다).
    static readonly (WorkAnim work, string clip)[] WorkClips =
    {
        (WorkAnim.Mining,         ClipRoot + "/Work/Mining/HumanM@Mining01 - Loop Ground.fbx"),
        (WorkAnim.Chopping,       ClipRoot + "/Work/Chopping/HumanM@TreeChopping02 - Loop.fbx"),
        (WorkAnim.HammerWall,     ClipRoot + "/Work/Hammering/HumanM@HammeringWall01_R - Loop.fbx"),
        (WorkAnim.HammerTable,    ClipRoot + "/Work/Hammering/HumanM@HammeringTable01_R - Loop.fbx"),
        (WorkAnim.HammerGround,   ClipRoot + "/Work/Hammering/HumanM@HammeringGround01_R - Loop.fbx"),
        (WorkAnim.Seeding,        ClipRoot + "/Work/Seeding/HumanM@Seeding01_R.fbx"),
        (WorkAnim.Watering,       ClipRoot + "/Work/Watering/HumanM@Watering01_R.fbx"),
        (WorkAnim.Gathering,      ClipRoot + "/Work/Gathering/HumanM@Gathering01.fbx"),
        (WorkAnim.Harvesting,     ClipRoot + "/Work/Gathering/HumanM@Gathering02.fbx"),
        (WorkAnim.Skinning,       ClipRoot + "/Work/Skinning/HumanM@SkinningTable01_R - Loop.fbx"),
        (WorkAnim.SkinningGround, ClipRoot + "/Work/Skinning/HumanM@SkinningGround01_R - Loop.fbx"),
    };

    // 걷기·달리기 클립이 발이 안 미끄러지는 속도(m/s) — 팩이 적어 두지 않아 눈으로 맞춘 값.
    // 틀리면 발이 밀린다. 인스펙터에서 블렌드 트리의 문턱을 바로 고쳐도 된다.
    const float WalkAt = 1.6f, RunAt = 4.5f;

    // 컨트롤러: Locomotion(Idle·Walk·Run을 `Speed`로 섞는다) · AnyState → 스타일마다 공격 하나 →
    // Locomotion · AnyState ⇄ Lying. **제자리에서 다시 짓는다**(guid 유지) — 지웠다 만들면
    // 프리팹의 참조가 끊긴다.
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
        ctrl.AddParameter("Style",  AnimatorControllerParameterType.Int);

        var sm = ctrl.layers[0].stateMachine;

        var loco = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType      = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, WalkAt);
        tree.AddChild(run,  RunAt);
        sm.defaultState = loco;

        var sLying = sm.AddState("Lying", new Vector3(560, 200));
        sLying.motion = idle;
        sLying.speed  = 0f;   // 자세는 루트 회전이 만든다 — 관절만 멈춘다

        // ── 공격 — 스타일마다 상태 하나 ──────────────────────────────────
        //
        // **모든 스타일이 상태를 갖는다.** 클립이 없는 것은 기본 공격(한손)으로 떨어진다 —
        // 상태를 빼면 그 무기를 든 폰에게는 **갈 곳이 없어 트리거만 쌓이고 아무 동작도 안 한다.**
        //
        // 트리거는 **조건이 맞는 전이 하나만** 소비한다 — `Style`이 정수 일치라 서로 배타적이라
        // 같은 프레임에 둘이 걸리는 일이 없다.
        var fellBack = new System.Collections.Generic.List<string>();
        int styleRow  = 0;
        AnimatorStateTransition t;

        foreach (AttackStyle style in System.Enum.GetValues(typeof(AttackStyle)))
        {
            var clip = ClipFor(style);
            if (clip == null) { clip = attack; fellBack.Add(style.ToString()); }

            var s = sm.AddState("Attack_" + style, new Vector3(560, 60 + styleRow * 60));
            s.motion = clip;
            styleRow++;

            t = sm.AddAnyStateTransition(s);
            t.hasExitTime = false; t.duration = 0.08f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If,     0f,           "Attack");
            t.AddCondition(AnimatorConditionMode.IfNot,  0f,           "Lying");
            t.AddCondition(AnimatorConditionMode.Equals, (int)style,   "Style");

            // **되돌아가는 길은 시간이 정한다** — 공격 클립은 한 번 돌고 끝나는 것이라
            // 루프가 아니고, 조건으로 나가면 *다음 트리거까지* 마지막 자세로 굳는다.
            t = s.AddTransition(loco);
            t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.15f;
        }

        if (fellBack.Count > 0)
            Debug.Log("[HumanoidClipSet] 공격 클립이 없어 기본(한손)으로 떨어진 스타일 — " +
                      string.Join(" · ", fellBack) + ". 받으면 `StyleClips`에 한 줄 더할 것.");

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

    // 스타일의 클립 — 표에 없으면 null(부르는 쪽이 기본으로 떨어뜨린다).
    static AnimationClip ClipFor(AttackStyle style)
    {
        foreach (var (s, path) in StyleClips)
            if (s == style) return Clip(path);
        return null;
    }

    // 클립 에셋 — `.anim`이면 그것 자체, FBX면 그 안의 것(미리보기 `__preview__`는 뺀다).
    static AnimationClip Clip(string fbxPath)
    {
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        Debug.LogError("[HumanoidClipSet] 클립 없음 — " + fbxPath);
        return null;
    }
}
