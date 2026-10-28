using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// **폰을 클립마다 찍어 한 장으로 — 화면을 못 보는 쪽이 자세를 볼 때**(2026-10-06).
//
// 09-23 메모: *"화면을 못 보는 채로 추측하면 다섯 번 헛돈다 · 다시 할 때는 찍어서 보는 장치부터"*. 그때 만들었다가 몸과 함께
// 지운 도구를 다시 세운다. 줄은 자세(대기 · 걷기 넷 · 곡괭이질 둘 · 공격 둘), 칸은 보는 쪽(정면 · 옆 · 게임 시점)이고
// 결과는 `Temp/PoseSheet/sheet.png`다. 씬은 안 건드린다(미리보기 씬은 저장되지 않는다).
//
// 대상은 **프로젝트 창에서 고른 폰 프리팹**, 고른 것이 없으면 비율 시제품(`Proto_Chibi`). 관절이 안 선 프리팹(`Animator`가
// 없다)은 `Pawn/2`부터 돌릴 것.
//
// **애니메이터를 직접 돌린다**(`Animator.Update`) — 매개변수는 판에서 `PawnAnimator`가 넣는 것과 같다(`Speed` · `Work` ·
// `Style` · `Attack`). 휴머노이드 클립은 근육 값이라 `AnimationClip.SampleAnimation`으로는 안 움직인다.
public static class PawnPoseSheet
{
    const int Cell = 240;
    const string OutDir = "Temp/PoseSheet";
    const string Fallback = "Assets/3_Prefab/Pawn/Proto_Chibi.prefab";
    const float Walk = 1.6f;   // `HumanoidClipSet.WalkAt`

    struct Pose
    {
        public string Label; public float Speed; public int Work; public int Style; public bool Attack; public float Time;
        public Pose(string label, float time, float speed = 0f, int work = 0, int style = 0, bool attack = false)
        { Label = label; Time = time; Speed = speed; Work = work; Style = style; Attack = attack; }
    }

    static readonly Pose[] Poses =
    {
        new("대기",        1.0f),
        new("걷기 1/4",    2.00f, speed: Walk),
        new("걷기 2/4",    2.25f, speed: Walk),
        new("걷기 3/4",    2.50f, speed: Walk),
        new("걷기 4/4",    2.75f, speed: Walk),
        new("곡괭이 든다", 1.0f, work: (int)WorkAnim.Mining),
        new("곡괭이 찍는다", 1.6f, work: (int)WorkAnim.Mining),
        new("공격 시작",   0.15f, style: 0, attack: true),
        new("공격 끝",     0.45f, style: 0, attack: true),
    };

    static readonly (string label, Vector3 dir)[] Views =
    {
        ("정면", Vector3.forward),                                   // 폰 앞(+Z)에서 본다
        ("옆",   Vector3.right),
        ("게임", Quaternion.Euler(-50f, 0f, 0f) * Vector3.forward),   // 비스듬히 위 앞
    };

    [MenuItem("DungeonColony/Pawn/8. Pose Sheet (자세 찍기 · 대기 · 걷기 · 일 · 공격)")]
    static void Run()
    {
        // 고른 것 — 프로젝트 창의 프리팹, 또는 **씬에 놓인 폰**(그 원본 프리팹을 찍는다). 못 찾으면 시제품을 찍되 **그렇다고 말한다** —
        // 조용히 시제품으로 넘어가면 다른 몸을 찍은 줄 알고 그림을 견준다(2026-10-09에 세 번 그랬다).
        string path = null;
        foreach (var o in Selection.objects)
        {
            string p = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(p) && o is GameObject sceneGo)
            {
                var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(sceneGo);
                if (src != null) p = AssetDatabase.GetAssetPath(src);
            }
            if (p != null && p.EndsWith(".prefab")) { path = p; break; }
        }
        if (path == null)
        {
            path = Fallback;
            Debug.LogWarning($"[자세] 고른 폰 프리팹이 없어 시제품을 찍는다 — 다른 몸을 찍으려면 프로젝트 창(또는 씬)에서 폰을 고르고 다시.");
        }
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogWarning($"[자세] 프리팹이 없다 — {path} (프로젝트 창에서 폰 프리팹을 고르거나 `Pawn/7`부터)"); return; }

        var scene = EditorSceneManager.NewPreviewScene();
        var rt    = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var shot  = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
        var sheet = new Texture2D(Views.Length * Cell, Poses.Length * Cell, TextureFormat.RGB24, false);
        var bg = new Color(0.30f, 0.31f, 0.34f);
        try
        {
            var camGo = new GameObject("PoseCam");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = bg;
            cam.orthographic = true; cam.targetTexture = rt; cam.nearClipPlane = 0.01f; cam.farClipPlane = 50f;

            var lightGo = new GameObject("PoseLight");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var anim = inst.GetComponentInChildren<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null)
            { Debug.LogWarning($"[자세] {Path.GetFileName(path)}에 애니메이터가 없다 — `Pawn/2. Build Pawn Rigs`부터."); return; }
            // 애니메이터는 바탕 폰에서 복사돼 와도 **관절이 없으면 아무것도 안 움직인다** — 오류 없이 꼿꼿한 그림만 나온다(10-09).
            var rig = inst.GetComponentInChildren<PawnRig>(true);
            if (rig == null || (rig.Hips == null && rig.Body == null) || anim.avatar == null)
            { Debug.LogWarning($"[자세] {Path.GetFileName(path)}에 관절이 없다 — `Pawn/2. Build Pawn Rigs`부터(시제품은 `Pawn/7`이 같이 세운다)."); return; }
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.applyRootMotion = false;
            // 판에서 애니메이터 **뒤에** 도는 몸통 덜어 내기(`ChibiPoseDamp`)도 같이 돌린다 — 플레이 밖이라 `Awake`·`LateUpdate`가
            // 안 돈다. 안 돌리면 판과 다른 자세를 찍는다. 기준 자세는 애니메이터가 돌기 전에 잡는다.
            var damp = inst.GetComponentInChildren<ChibiPoseDamp>();
            if (damp != null) damp.Capture();

            // 틀은 대기 자세의 크기로 한 번 정한다 — 칸마다 맞추면 자세끼리 크기를 못 견준다.
            Sample(anim, Poses[0], damp);
            var b = Bounds(inst);
            float size = Mathf.Max(b.size.y, b.size.x) * 0.62f;
            var center = new Vector3(0f, b.center.y, 0f);

            var log = new System.Text.StringBuilder($"[자세] {Path.GetFileNameWithoutExtension(path)} — 키 {b.size.y:F2}m · 폭 {b.size.x:F2}m\n" +
                                                    "  줄(위부터): ");
            for (int r = 0; r < Poses.Length; r++)
            {
                Sample(anim, Poses[r], damp);
                log.Append(r == 0 ? Poses[r].Label : " · " + Poses[r].Label);
                for (int c = 0; c < Views.Length; c++)
                {
                    var dir = Views[c].dir.normalized;
                    cam.orthographicSize   = size;
                    cam.transform.position = center + dir * 10f;
                    cam.transform.LookAt(center);
                    cam.Render();

                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    shot.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
                    shot.Apply();
                    RenderTexture.active = prev;

                    int row = Poses.Length - 1 - r;   // 텍스처는 아래가 0이다
                    sheet.SetPixels(c * Cell, row * Cell, Cell, Cell, shot.GetPixels());
                }
            }
            log.Append("\n  칸(왼쪽부터): 정면 · 옆 · 게임 시점");

            Directory.CreateDirectory(OutDir);
            string outPath = Path.Combine(OutDir, "sheet.png");
            File.WriteAllBytes(outPath, sheet.EncodeToPNG());
            log.Append($"\n→ {Path.GetFullPath(outPath)}");
            Debug.Log(log.ToString());
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(shot);
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    // 처음부터 다시 돌려 `Time`초째 자세에 세운다(30fps로 걸어 — 전이도 판처럼 지나간다).
    static void Sample(Animator anim, Pose p, ChibiPoseDamp damp)
    {
        anim.Rebind();
        anim.SetFloat("Speed", p.Speed);
        anim.SetInteger("Work", p.Work);
        anim.SetInteger("Style", p.Style);
        anim.Update(0f);
        if (p.Attack) anim.SetTrigger("Attack");
        const float dt = 1f / 30f;
        for (float t = 0f; t < p.Time; t += dt) anim.Update(dt);
        if (damp != null && damp.enabled) damp.Apply();
    }

    static Bounds Bounds(GameObject go)
    {
        var b = new Bounds(go.transform.position + Vector3.up, Vector3.one * 0.5f);
        bool any = false, hasRigid = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            if (r.enabled && !(r is SkinnedMeshRenderer)) { hasRigid = true; break; }
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            // 스킨 메시는 경계가 손으로 잡은 여유값이라 틀에서 뺀다 — 머리·손·신발이 키와 폭을 다 잡는다
            // (굳은 살이 하나도 없는 몸(골렘)이면 그대로 쓴다).
            if (!r.enabled || (r is SkinnedMeshRenderer && hasRigid)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
