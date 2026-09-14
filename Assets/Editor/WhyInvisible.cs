using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// **"안 보인다"에 답하는 자리 — 여기 하나다**(2026-09-15).
//
// 사흘을 썼다. 사용자가 *"광맥이 안 보인다"*고 다섯 번 말했고, 그때마다 내가 다른 곳을
// 팠다: 렌더링 → 밀도 → 시작 칸 → 발자국. **넷 다 진짜 결함이었는데 넷 다 답이 아니었다.**
//
// 원인은 하나였다 — ***"이 물건이 왜 화면에 없나"*를 물어볼 길이 아무 데도 없었다.**
// 그래서 물을 수 있는 것만 물었다(에셋·프리팹·메시·재질), 그리고 그쪽은 **전부 멀쩡했다**:
// 메시 감김 100% 바깥 · 부호 있는 부피 양수 · URP Lit 불투명 · 렌더러 34개 전부 켜짐 ·
// 레이어 0 · 스케일 1. **멀쩡한 것을 여섯 번 증명하는 동안 정작 놓인 자리는 한 번도 안 쟀다.**
//
// 이 저장소가 이미 적어 둔 규칙이다 — *"에셋도 프리팹도 멀쩡할 때 남는 단서가 아무것도
// 없어서 버섯과 바닥 광맥에 연달아 당했다."* 그때 낸 답이 뿌리기 결산(`ReportCensus`)인데,
// 그건 **뿌린 것**만 답한다. 손으로 씬에 끌어다 놓은 것에는 답하는 것이 없었다.
//
// ## 무엇을 재나
//
// 고른 것 하나에 대해 **그리는 쪽과 놓인 쪽을 같이** 잰다:
//
//   · 렌더러 — 붙은 수 / 실제로 그려지는 수 / 재질 빔 / 합친 경계 / 레이어 / 스케일
//   · 자리   — 바닥 높이 대비 y · 발자국 칸마다 **바위인가** · 머리 위 여유
//   · 화면   — 씬 카메라에서 얼마나 먼가 · 절두체 안인가
//
// **바위 여부가 요점이다.** 세계의 69%가 바위이고 `RockRenderer`가 그 위에 **24m 벽**을
// 세운다 — 벽 속에 놓인 물건은 **멀쩡히 그려지면서 아무에게도 안 보인다.** 씬 뷰에서는
// 자르기(`RockCut`)를 일부러 안 걸어서 벽이 24m 그대로라 더 그렇다.
//
// ## 어기면 조용히 틀리는 것
//
// - **`enabled`와 `activeInHierarchy`를 같이 본다** — `GetComponentsInChildren<Renderer>(true)`는
//   꺼진 것과 비활성까지 세므로, 개수만으로는 *"그려진다"*를 뜻하지 않는다.
// - **경계는 그려지는 것만 모은다** — 꺼진 렌더러의 경계까지 더하면 눈금이 거짓말을 한다.
// - **바위는 `RockMap`에게 묻는다 — 콜라이더에 레이를 쏘지 않는다.** 콜라이더는 로드된
//   청크에만 서므로, 쏘면 *로드된 곳에서만 벽이 있는* 반쪽 답이 나온다.
// - **플레이 중에도 돈다** — 편집 모드에서만 되면 정작 판이 열렸을 때 못 묻는다.
public static class WhyInvisible
{
    [MenuItem("DungeonColony/World/C3. Why Can't I See This? (고른 것이 왜 안 보이나)")]
    static void Ask()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogWarning("[안보임] 하이어라키에서 물건을 하나 고르고 다시 누르세요.");
            return;
        }

        var sb = new StringBuilder();
        sb.Append($"[안보임] `{go.name}` — 무엇이 막고 있나\n");

        Renderers(sb, go);
        Meshes(sb, go);
        Place(sb, go);
        Screen(sb, go);
        Pixels(sb, go);

        Debug.Log(sb.ToString(), go);
    }

    // ── 그리는 쪽 ───────────────────────────────────────────────────────────
    static void Renderers(StringBuilder sb, GameObject go)
    {
        var all = go.GetComponentsInChildren<Renderer>(true);
        int live = 0, noMat = 0;
        var b = new Bounds(go.transform.position, Vector3.zero);

        foreach (var r in all)
        {
            if (r.sharedMaterial == null) noMat++;
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            live++;
            b.Encapsulate(r.bounds);
        }

        sb.Append($"  · 렌더러 {all.Length}개 중 **그려지는 것 {live}개** · 재질 없음 {noMat}개\n");
        sb.Append($"  · 경계 {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2}" +
                  $" · 스케일 {go.transform.localScale.x:F2} · 레이어 {LayerMask.LayerToName(go.layer)}\n");

        if (all.Length == 0)  sb.Append("    ← **모양이 없다** — 프리팹이 비었다\n");
        else if (live == 0)   sb.Append("    ← **꺼졌거나 비활성이다**\n");
        else if (noMat > 0)   sb.Append("    ← **재질이 빈 렌더러가 있다**\n");
        else if (b.size.y < 0.05f) sb.Append("    ← **경계가 0이다** — 크기가 눌렸다\n");
    }

    // ── 메시가 실제로 무엇을 들고 있나 (2026-09-15) ────────────────────────
    //
    // **에셋을 읽는 것과 런타임이 들고 있는 것은 다른 물음이다.** 파일에는 정점 384개와
    // 인덱스 384개가 또렷이 적혀 있는데, `Renderer.bounds`를 모아 보니 **자식들의 위치
    // 분포와 소수점까지 똑같았다** — 지오메트리가 한 톨도 기여하지 않았다는 뜻이다.
    //
    // 그런데 그것도 **추론**이다(위치 분포와 실제 경계가 우연히 비슷할 수도 있다).
    // 그래서 **묻는다**: `sharedMesh`가 null인가 · 정점 몇 개인가 · 삼각형 몇 개인가 ·
    // 제 경계가 얼마인가. 이 넷이면 *에셋은 멀쩡한데 런타임이 비어 있다*가 참인지
    // 거짓인지가 한 줄로 닫힌다. **오늘 다섯 번 틀린 이유가 전부 "안 묻고 짚어서"였다.**
    static void Meshes(StringBuilder sb, GameObject go)
    {
        int nul = 0, empty = 0, ok = 0, verts = 0, tris = 0;
        string first = null;

        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m == null) { nul++; continue; }

            verts += m.vertexCount;
            int t = m.triangles.Length / 3;
            tris += t;

            if (m.vertexCount == 0 || t == 0) empty++;
            else ok++;

            if (first == null)
                first = $"`{m.name}` 정점 {m.vertexCount} · 삼각형 {t} · " +
                        $"경계 {m.bounds.size.x:F2}x{m.bounds.size.y:F2}x{m.bounds.size.z:F2}";
        }

        sb.Append($"  · 메시 — 비었음(null) {nul}개 · 정점/삼각형 0 {empty}개 · 멀쩡 {ok}개" +
                  $" · 합계 정점 {verts} 삼각형 {tris}\n");
        if (first != null) sb.Append($"    첫 메시: {first}\n");

        if (nul > 0)
            sb.Append("    ← **메시 참조가 끊겼다** — 프리팹이 가리키는 에셋을 못 읽는다\n");
        else if (empty > 0)
            sb.Append("    ← **메시는 물렸는데 속이 비었다** — 에셋 파일과 런타임이 다르다\n");
    }

    // ── 놓인 쪽 — 여기가 이 도구의 이유다 ──────────────────────────────────
    static void Place(StringBuilder sb, GameObject go)
    {
        var p = go.transform.position;
        float ground = TerrainHeight.At(p.x, p.z);
        sb.Append($"  · 자리 ({p.x:F1}, {p.y:F2}, {p.z:F1}) · 바닥 {ground:F2}m" +
                  $" → 바닥에서 {p.y - ground:+0.00;-0.00}m\n");

        if (p.y - ground < -0.5f)
            sb.Append("    ← **땅에 묻혔다**\n");

        // **칸은 칸 범위와 견준다**(2026-09-15에 여기서 틀렸다). `InWorld`는 **청크**를
        // 받는 것이고 칸은 `TileInWorld`다 — 둘 다 `Vector2Int`라 컴파일러가 안 잡아 주고,
        // 그래서 (197,198)이 **"세계 밖"**으로 찍혔다(청크 범위는 16까지다).
        // **진단이 거짓말을 하면 없느니만 못하다** — 사용자가 그 줄부터 쫓게 된다.
        if (!TerrainGrid.TileInWorld(new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z))))
            sb.Append("    ← **세계 밖이다** — 지형도 벽도 안 서고 아무것도 안 그려진다\n");

        if (!RockMap.Enabled) { sb.Append("  · 바위: 꺼져 있다(지상 씬)\n"); return; }

        // **발자국으로 묻는다** — 3x3짜리는 칸 하나가 열려도 나머지 여덟이 벽이면 파묻힌다.
        var node = go.GetComponentInChildren<ResourceNode>();
        var foot = node != null ? node.Size : Vector2Int.one;

        int rock = 0, total = 0;
        foreach (var t in ResourceNode.FootprintAt(p, foot))
        {
            total++;
            if (RockMap.IsRock(t)) rock++;
        }

        sb.Append($"  · 덮는 칸 {foot.x}x{foot.y} = {total}칸 중 **바위 {rock}칸**" +
                  $" · 머리 위 여유 {TerrainCeiling.ClearanceAt(p.x, p.z):F1}m\n");

        if (rock == total)
            sb.Append("    ← **통째로 바위 속이다.** 24m 벽이 앞을 가린다 — " +
                      "그려지기는 하는데 볼 방법이 없다.\n");
        else if (rock > 0)
            sb.Append($"    ← **{rock}칸이 벽 속이다.** 덩치의 그만큼이 24m 벽에 파묻혀 있다.\n");
    }

    // ── 정말로 픽셀이 나오나 ────────────────────────────────────────────────
    //
    // **여기까지 와야 "안 보인다"가 닫힌다.** 위의 셋은 전부 *조건*을 재는 것이라, 조건이
    // 다 통과인데도 화면이 비어 있으면 더 물을 것이 없다 — 실제로 그 상태가 됐다
    // (자리 멀쩡 · 바위 0칸 · 카메라 2m · 화면 안 · 렌더러 34개 전부 그려짐).
    //
    // 그래서 **찍어 본다.** 임시 카메라로 그 물건만 담아 렌더링하고 픽셀을 센다.
    // **두 번 찍는 것이 요점이다**:
    //
    //   · **맨 메시**(`Unlit/Color`) — 조명도 파이프라인도 안 타는 흰색. 여기서 0이면
    //     **기하가 화면에 안 맺히는 것**이다(메시·트랜스폼·절두체).
    //   · **제 재질 그대로** — 실제 URP 경로. 맨 메시는 나오는데 여기서 0이면
    //     **재질·셰이더**이고, 나오는데 **까맣다면 조명**이다.
    //
    // 갈라 찍지 않으면 *"안 나온다"* 하나로 뭉쳐서 또 짐작하게 된다.
    const int Shot = 96;

    static void Pixels(StringBuilder sb, GameObject go)
    {
        var live = new System.Collections.Generic.List<Renderer>();
        var b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            if (r.enabled && r.gameObject.activeInHierarchy) { live.Add(r); b.Encapsulate(r.bounds); }

        if (live.Count == 0) { sb.Append("  · 찍어 보기: 그릴 렌더러가 없다\n"); return; }

        float rad = Mathf.Max(b.extents.magnitude, 0.25f);
        var dir   = new Vector3(0.4f, 0.35f, -1f).normalized;
        var eye   = b.center + dir * (rad * 3f);
        var rot   = Quaternion.LookRotation(b.center - eye, Vector3.up);
        var rt    = RenderTexture.GetTemporary(Shot, Shot, 24, RenderTextureFormat.ARGB32);

        // ① 맨 메시 — 조명을 안 탄다
        var flat = new Material(Shader.Find("Unlit/Color")) { hideFlags = HideFlags.HideAndDontSave };
        flat.color = Color.white;

        var cb = new CommandBuffer();
        cb.SetRenderTarget(rt);
        cb.ClearRenderTarget(true, true, new Color(0f, 0f, 0f, 0f));
        // **뷰 행렬은 z를 뒤집는다** — 유니티 카메라는 -z를 보므로 그냥 역행렬을 쓰면 뒤를 본다.
        var view = Matrix4x4.TRS(eye, rot, new Vector3(1f, 1f, -1f)).inverse;
        var proj = GL.GetGPUProjectionMatrix(Matrix4x4.Perspective(40f, 1f, 0.01f, rad * 12f), true);
        cb.SetViewProjectionMatrices(view, proj);
        foreach (var r in live) cb.DrawRenderer(r, flat);
        Graphics.ExecuteCommandBuffer(cb);
        cb.Release();
        int raw = Count(rt, out _);
        Object.DestroyImmediate(flat);

        // ② 제 재질 그대로 — 실제 URP 경로
        //
        // **URP 카메라는 `UniversalAdditionalCameraData`가 있어야 그린다**(2026-09-15에
        // 여기서 틀렸다). 안 붙이고 `Render()`를 불렀더니 **0%가 나왔고, 나는 그것을
        // "안 그려진다"의 증거로 썼다** — 진단 도구가 낸 거짓 0이었다.
        // **재는 도구가 틀리면 그 뒤의 추론이 전부 헛것이 된다.**
        var camGo = new GameObject("~안보임검사") { hideFlags = HideFlags.HideAndDontSave };
        var cam   = camGo.AddComponent<Camera>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
        cam.transform.SetPositionAndRotation(eye, rot);
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask     = 1 << go.layer;
        cam.nearClipPlane   = 0.01f;
        cam.farClipPlane    = rad * 12f;
        cam.fieldOfView     = 40f;
        cam.targetTexture   = rt;
        cam.Render();
        int lit = Count(rt, out var avg);
        cam.targetTexture = null;
        Object.DestroyImmediate(camGo);

        // **기준을 같이 찍는다**(대조군). 유니티 기본 큐브를 같은 방식으로 찍어서
        // **검사 자체가 도는지**를 먼저 보인다 — 이것이 0이면 아래 숫자는 아무 뜻이 없다.
        // 오늘 그 구별을 안 해서 도구가 낸 0을 물건의 0으로 읽었다.
        int baseline = Baseline(rt, eye, rot, b.center, rad);

        RenderTexture.ReleaseTemporary(rt);

        int all = Shot * Shot;
        float bright = avg.r * 0.2126f + avg.g * 0.7152f + avg.b * 0.0722f;

        sb.Append($"  · 찍어 보기({Shot}x{Shot}) — 맨 메시 {raw * 100f / all:F1}%" +
                  $" · 제 재질 {lit * 100f / all:F1}% · 평균 밝기 {bright:F3}" +
                  $" · **기준(기본 큐브) {baseline * 100f / all:F1}%**\n");

        if (baseline == 0)
        {
            sb.Append("    ← **검사 자체가 안 돌았다**(기준도 0이다) — 위 숫자는 " +
                      "아무것도 뜻하지 않는다. 물건이 아니라 이 도구를 고칠 것.\n");
            return;
        }

        if (raw == 0 && lit == 0)
            sb.Append("    ← **둘 다 0이다.** 기하가 화면에 안 맺히거나, 이 검사 자체가 " +
                      "안 돈 것이다 — 아래 `기준`과 견줄 것\n");
        else if (raw == 0)
            sb.Append("    ← **맨 메시가 0인데 제 재질은 나온다** — 이 검사의 맨 메시 쪽이 " +
                      "안 돈 것이다(무시)\n");
        else if (lit == 0)
            sb.Append("    ← **맨 메시는 나오는데 제 재질로는 안 나온다** — 재질·셰이더다\n");
        else if (bright < 0.02f)
            sb.Append("    ← **그려지기는 하는데 새까맣다** — 조명이다(빛이 안 닿는다)\n");
        else
            sb.Append("    ← **정상으로 그려진다.** 화면에 없다면 앞을 무언가가 가리는 것이다\n");
    }

    // 같은 카메라 자리에서 **유니티 기본 큐브**를 찍는다. 이것이 나오면 검사가 도는
    // 것이고, 안 나오면 도구가 고장 난 것이다. **대조군 없는 0은 증거가 아니다.**
    static int Baseline(RenderTexture rt, Vector3 eye, Quaternion rot, Vector3 at, float rad)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.hideFlags = HideFlags.HideAndDontSave;
        cube.transform.SetPositionAndRotation(at, Quaternion.identity);
        cube.transform.localScale = Vector3.one * Mathf.Max(rad, 0.5f);

        var camGo = new GameObject("~기준") { hideFlags = HideFlags.HideAndDontSave };
        var cam   = camGo.AddComponent<Camera>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
        cam.transform.SetPositionAndRotation(eye, rot);
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask     = 1 << cube.layer;
        cam.nearClipPlane   = 0.01f;
        cam.farClipPlane    = rad * 12f;
        cam.fieldOfView     = 40f;
        cam.targetTexture   = rt;
        cam.Render();
        int n = Count(rt, out _);
        cam.targetTexture = null;

        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(cube);
        return n;
    }

    static int Count(RenderTexture rt, out Color avg)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        int n = 0; Color sum = Color.clear;
        foreach (var c in tex.GetPixels()) if (c.a > 0.01f) { n++; sum += c; }
        avg = n > 0 ? sum / n : Color.clear;
        Object.DestroyImmediate(tex);
        return n;
    }

    // ── 화면 쪽 ─────────────────────────────────────────────────────────────
    static void Screen(StringBuilder sb, GameObject go)
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null) return;

        var cam = view.camera;
        float d = Vector3.Distance(cam.transform.position, go.transform.position);

        var vp = cam.WorldToViewportPoint(go.transform.position);
        bool front = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;

        sb.Append($"  · 씬 카메라에서 {d:F0}m · 화면 안: {(front ? "예" : "**아니오**")}\n");
        if (!front)
            sb.Append("    ← 씬 뷰가 다른 데를 보고 있다 — 고른 채로 `F`를 누르면 거기로 간다.\n");
    }
}
