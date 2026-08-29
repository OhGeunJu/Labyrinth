using UnityEditor;
using UnityEngine;

// **월드 스트리밍 배선을 씬에 세운다**(2026-08-26).
//
// 손으로 붙이면 빠뜨리는 자리가 많다 — 지형은 서는데 나무가 안 보이거나(규칙이 비었다),
// 폰이 지면 아래를 걷거나(`MapManager.절차 지형`을 안 켰다), 건설 커서가 땅을 못 찾는다
// (레이어가 `Ground`가 아니다). **셋 다 증상이 "왜 안 되지"라 원인을 짚기 어렵다.**
//
// 이 저장소가 UI에 쓰는 방식 그대로다 — **규격은 도구가 씬에 세우고, 그 안을 런타임이 채운다.**
public static class WorldStreamingSetup
{
    const string RootName = "World";

    [MenuItem("DungeonColony/Terrain/0. Setup World Streaming")]
    static void Setup()
    {
        // **이미 붙어 있는 것을 먼저 찾는다** — 이름으로만 찾으면 저작 씬의 루트를 집을 수
        // 있고(거기에 스트리머가 붙으면 판이 열릴 때 그 오브젝트가 통째로 꺼져 **세계가 안 선다**),
        // 이름을 바꿔 둔 씬에서는 두 번째 루트를 만든다.
        var existing = Object.FindFirstObjectByType<TerrainStreamer>(FindObjectsInactive.Include);
        var go = existing != null ? existing.gameObject : GameObject.Find(RootName);

        if (go != null && go.GetComponent<WorldAuthoringRoot>() != null)
        {
            Debug.LogWarning("[월드] `" + go.name + "`은 저작 씬의 루트다 — 스트리밍을 여기 붙이면 " +
                             "판이 열릴 때 통째로 꺼진다. 게임 씬에 새로 세운다.");
            go = null;
        }

        if (go == null)
        {
            go = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(go, "World 만들기");
        }
        // **트랜스폼을 되돌린다** — 스트리머는 자기 루트를 쓰므로 안 깨지지만, 여기 붙은
        // 값이 0이 아니면 다음에 보는 사람이 그것부터 의심한다.
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;

        var terrain = go.GetComponent<TerrainStreamer>() ?? Undo.AddComponent<TerrainStreamer>(go);
        var content = go.GetComponent<WorldContentStreamer>() ?? Undo.AddComponent<WorldContentStreamer>(go);

        // **무리를 실체로 세우는 쪽**(2026-08-27). 이게 없으면 `WorldBandManager`가 토큰만 굴리고
        // **지도 위의 무리가 영영 실제 폰이 되지 않는다** — 습격이 예고돼도 아무도 안 온다.
        // 값은 전부 기본값으로 뜻이 서 있어(반경은 `SimLocality`를 따라간다) 저작할 것이 없다.
        if (go.GetComponent<BandMaterializer>() == null) Undo.AddComponent<BandMaterializer>(go);

        // **지운 컴포넌트의 껍데기를 걷어낸다**(2026-08-29). `CeilingRenderer`를 지웠는데
        // 씬에는 이미 붙어 있어서 *"Missing (Mono Script)"*로 남는다 — 그대로 두면 다음에
        // 여는 사람이 그것부터 의심한다.
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

        // **바위를 그리는 쪽**(2026-08-29). 없으면 `RockMap`이 통행을 막기는 하는데
        // **화면에는 아무것도 안 보인다** — 사방이 벽인데 화면은 뻥 뚫린 벌판이라, 폰이
        // 안 움직이는 이유를 짚을 수가 없다. `BandMaterializer`가 없을 때 무리가 영영
        // 안 나타나던 것과 같은 자리다.
        //
        // **천장 렌더러는 아예 지웠다**(2026-08-29, 사용자: *"검은 안개는 그냥 끔찍해"*).
        // 바위가 곧 벽이자 뚜껑이라 그 위에 한 겹 더 그릴 것이 없었고, 바위가 기본이 된 뒤로는
        // 천장 면이 지면에 눌어붙어 **검은 막**으로만 보였다. `TerrainCeiling`(함수)은 남는다 —
        // 그게 무엇이 바위인지를 답하는 자리다.
        if (go.GetComponent<RockRenderer>() == null) Undo.AddComponent<RockRenderer>(go);

        // **지하의 하루**(2026-08-29, 설계 2단계). 없으면 화면이 언제나 같고, `Vision`도
        // 지상 시계로 돌아가 **캄캄한데 대낮처럼 보는** 상태가 된다.
        if (go.GetComponent<UnderworldLighting>() == null) Undo.AddComponent<UnderworldLighting>(go);

        int layer = LayerMask.NameToLayer("Ground");
        if (layer < 0) { layer = 0; Debug.LogWarning("[월드] 레이어 'Ground'가 없다 — Default로 둔다."); }

        var st = new SerializedObject(terrain);
        st.FindProperty("terrainLayer").intValue = layer;

        // **지하가 기본이다**(2026-08-29, 배경을 지하로 엎었다). 체크박스 하나를 사람이
        // 켜야 하면 *"천장이 왜 안 뜨지"*가 반복된다.
        var ceil = st.FindProperty("ceiling");
        if (ceil != null) ceil.boolValue = true;

        // **저작한 지상 높이맵을 뗀다.** 그건 비가 깎은 지형이라 200m 산이 서 있고, 거기에
        // 천장을 덮으면 굴이 아니라 **산을 감싼 비닐**이 된다. 떼면 `MacroNoise`로 떨어져
        // 완만한 지하 바닥이 나온다 — 지하 침식을 굽기 전까지는 그게 맞다.
        var hm = st.FindProperty("heightmap");
        if (hm != null) hm.objectReferenceValue = null;

        WriteUnderworldProfiles(st);
        st.ApplyModifiedPropertiesWithoutUndo();

        ApplyUnderworldLighting();

        WireScatter(content);
        EnableProceduralTerrain();
        int slept   = SleepOldTerrains(go);
        int screens = SleepLegacyWorldScreen();

        EditorUtility.SetDirty(go);
        Selection.activeGameObject = go;

        Debug.Log($"[월드] 배선 완료 — {go.name}에 TerrainStreamer + WorldContentStreamer + BandMaterializer. " +
                  $"레이어 '{LayerMask.LayerToName(layer)}' · 옛 Terrain {slept}개 · " +
                  $"옛 월드맵 화면 {screens}개 재움." + "\n" +
                  $"세계 {TerrainGrid.WorldSize}m · 청크 {TerrainGrid.ChunkSize}m · " +
                  $"{TerrainGrid.ChunksPerAxis}×{TerrainGrid.ChunksPerAxis}");
    }

    // ── 지하 지형은 거의 평지다 ─────────────────────────────────────────────
    //
    // **코드 기본값은 씬이 값을 들면 아무 힘이 없다**(`EventManager.checkInterval`이 씬에
    // 박혀 있던 그 함정) — 그래서 여기서 **씬에 직접 쓴다.**
    //
    // **바닥은 완전 평지다**(2026-08-29, 사용자 결정 — 참고로 삼은 화면들이 전부 그렇다).
    // 높이의 뜻은 오직 *"파였나 안 파였나"*뿐이고, **기복은 바닥이 아니라 바위가 만든다.**
    // 조금이라도 물결치면 벽 높이가 칸마다 달라져 **파낸 방이 울퉁불퉁한 웅덩이**로 보인다.
    // 주기 값은 안 건드린다 — 진폭이 0이면 곱해질 것이 없다.
    static void WriteUnderworldProfiles(SerializedObject st)
    {
        Set(st, "profile.BaseHeight",        30f);
        Set(st, "profile.ContinentAmplitude", 0f);
        Set(st, "profile.HillAmplitude",      0f);
        Set(st, "profile.DetailAmplitude",    0f);

        // **저작한 산·분지를 비운다** — 이게 빠지면 진폭을 아무리 0으로 눌러도 **바닥이 안
        // 평평하다.** `TerrainHeight.Procedural`이 노이즈와 **별도로** 특징을 더하기 때문이다.
        //
        // 실제로 씬에 `반지름 600m · 높이 120m`짜리 산이 하나 남아 있었다. 세계가 8km였을
        // 때는 한구석의 봉우리였는데 1km로 줄인 뒤로는 **거의 전체를 덮는 언덕**이 됐다 —
        // 그래서 *"평평하게 해 달라"*고 세 번 말해도 화면은 그대로였다.
        //
        // **지우지 않고 비운다** — 배열을 비우는 것은 되돌릴 수 있고(Undo), 지하 지형을
        // 저작할 때 다시 채우면 된다.
        var feats = st.FindProperty("features");
        if (feats != null && feats.arraySize > 0)
        {
            Debug.Log($"[월드] 저작된 지형 특징 {feats.arraySize}개를 비웠다 — " +
                      "**진폭을 0으로 해도 이게 남아 있으면 바닥이 안 평평하다.**");
            feats.arraySize = 0;
        }

        var cfeats = st.FindProperty("ceilingFeatures");
        if (cfeats != null) cfeats.arraySize = 0;

        // **작은 세계에 맞춘다.** 청크가 64m가 된 뒤로 반경 2는 320m라 세계(256m)를 통째로
        // 덮는다 — 그건 괜찮지만, **시뮬 반경 1,200m와 일감 거리 600m는 세계보다 넉 배 크다.**
        // 값이 뜻을 잃으면 다음에 보는 사람이 *"왜 안 걸리지"*를 여기서 찾게 된다.
        Set(st, "simRadius",      320f);
        Set(st, "maxJobDistance", 200f);

        // **천장(여유)도 씬에 쓴다** — 필드를 새로 만든 날 씬이 옛 기본값(+9)으로 굳었으면
        // 코드에서 −7로 바꿔도 **세계가 뻥 뚫린 채**다(바위가 하나도 안 선다).
        // **바위가 압도적으로 많아야 한다.** −7이었을 때는 여유가 문턱(폰 키 1.8m) 언저리를
        // 오르내려서 **바위가 듬성듬성 선 판자**처럼 보였다 — 벽이 뚫려 보인다는 것이 그거다.
        // −11이면 최대 여유가 4.6m라 **저절로 난 굴이 드물게만** 생기고 나머지는 통짜 바위다.
        //
        // **잔요철을 거의 없앤다**(1.5 → 0.4). 이게 문턱 바로 옆에서 흔들려 **칸 하나씩
        // 뚫렸다 막혔다** 하는 얼룩을 만든다 — 굴의 벽이 톱니처럼 되는 원인이다.
        Set(st, "ceilingProfile.BaseClearance",  -11f);
        Set(st, "ceilingProfile.CavernPeriod",   180f);
        Set(st, "ceilingProfile.CavernAmplitude", 11f);
        Set(st, "ceilingProfile.TunnelPeriod",    60f);
        Set(st, "ceilingProfile.TunnelAmplitude",  4f);
        Set(st, "ceilingProfile.RoughPeriod",     18f);
        Set(st, "ceilingProfile.RoughAmplitude", 0.4f);
        Set(st, "ceilingProfile.SinkDepth",        2f);
    }

    // **못 찾으면 알린다.** 조용히 넘어가면 *"값을 썼는데 안 바뀐다"*가 되고, 그 원인이
    // 오타인지 필드 이름이 바뀐 것인지를 화면에서는 알 길이 없다 — 이 저장소가 여러 번
    // 겪은 *"조용히 아무 일도 안 일어난다"*의 자리다.
    static void Set(SerializedObject so, string path, float v)
    {
        var p = so.FindProperty(path);
        if (p == null)
        {
            Debug.LogError($"[월드] `{path}`를 못 찾았다 — 필드 이름이 바뀌었나? **이 값은 안 써졌다.**");
            return;
        }
        p.floatValue = v;
    }

    // ── 지하는 어둡고, 벽 너머는 아무것도 없다 ──────────────────────────────
    //
    // 텍스처만으로는 지하가 안 된다 — **분위기의 절반은 안개와 주변광**이다. 지상 설정
    // 그대로면 파낸 굴이 *대낮의 채석장*으로 보인다.
    //
    // **안개 색이 검정인 것이 핵심이다**(2026-08-29). 참고로 삼은 화면들은 파낸 방 바깥이
    // 전부 **새까맣다** — 초록·회색 안개를 깔면 그 자리가 *흐릿하게 보이는 무언가*가 되어
    // **"저 너머엔 아무것도 없다"가 안 생긴다.** 바위 윗면을 검게 칠한 것과 같은 규칙이다.
    //
    // 주변광은 **푸르게** 둔다 — 벽이 청회색으로 서고, 나중에 놓을 횃불(주황)이 그 위에서
    // 도드라진다. 색이 하나뿐이면 밝고 어두운 것만 있고 **따뜻하고 차가운 것**이 없다.
    //
    // **`RenderSettings`는 씬이 든다** — 여기서 써 두면 저장과 함께 남는다.
    static void ApplyUnderworldLighting()
    {
        RenderSettings.ambientMode         = UnityEngine.Rendering.AmbientMode.Trilight;
        // **2026-08-29에 한 단계 올렸다**(사용자: 너무 어둡다). 처음 값은 *분위기*로는 맞았는데
        // **무엇이 어디 있는지가 안 보였다** — 밤 조명에서 이미 못 박은 규칙이다:
        // **어두운 것과 안 보이는 것은 다르다**(분위기가 아니라 조작 불능이 된다). 대략 두 배.
        RenderSettings.ambientSkyColor     = new Color(0.30f, 0.38f, 0.45f);   // 위에서 오는 푸른빛
        RenderSettings.ambientEquatorColor = new Color(0.20f, 0.26f, 0.32f);
        RenderSettings.ambientGroundColor  = new Color(0.10f, 0.12f, 0.14f);

        // **하늘을 없앤다.** 안개는 먼 것을 흐릴 뿐 **스카이박스를 안 가린다** — 지하인데
        // 화면 위쪽에 **파란 하늘**이 그대로 보인다. 카메라가 지우는 색까지 같이 바꿔야
        // 한다(스카이박스만 떼면 그 자리에 **회색 기본색**이 남는다).
        RenderSettings.skybox = null;
        DynamicGI.UpdateEnvironment();

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,
                                                            FindObjectsSortMode.None))
        {
            if (cam.clearFlags == CameraClearFlags.Depth ||
                cam.clearFlags == CameraClearFlags.Nothing) continue;   // UI 카메라는 안 건드린다

            Undo.RecordObject(cam, "지하 배경색");
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.012f, 0.014f, 0.018f);
            EditorUtility.SetDirty(cam);
        }

        RenderSettings.fog        = true;
        RenderSettings.fogMode    = FogMode.ExponentialSquared;
        RenderSettings.fogColor   = new Color(0.015f, 0.018f, 0.022f);   // **거의 검정**
        // 안개도 절반으로 — 짙으면 **가까운 것까지 회색으로 덮여** 바닥 텍스처가 안 보인다.
        RenderSettings.fogDensity = 0.006f;

        // **하루 주기 조명이 이걸 도로 덮어쓴다** — 지상용이라 아침이 되면 주변광을 밝은
        // 하늘색으로 되돌린다. 지하에는 해가 없으므로 재워 둔다(2단계에서 지하용으로 다시 쓴다).
        int slept = 0;
        foreach (var d in Object.FindObjectsByType<DayNightLighting>(FindObjectsInactive.Include,
                                                                    FindObjectsSortMode.None))
        {
            if (!d.enabled) continue;
            Undo.RecordObject(d, "하루 조명 재우기");
            d.enabled = false;
            EditorUtility.SetDirty(d);
            slept++;
        }

        // **방향광도 재운다.** 하루 조명 컴포넌트를 껐어도 **등 자체는 마지막 값 그대로**라,
        // 아침에 멈춰 있었으면 지하가 **대낮**이다. 지하의 빛은 화톳불에서 와야 한다.
        int suns = 0;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                                                          FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional || !l.enabled) continue;
            Undo.RecordObject(l, "해 재우기");
            l.enabled = false;
            EditorUtility.SetDirty(l);
            suns++;
        }

        Debug.Log($"[월드] 지하 조명 — 하늘 제거 · 방향광 {suns}개 · 푸른 주변광 · **검은 안개**(짙기 0.006) · " +
                  $"하루 주기 조명 {slept}개 재움.\n" +
                  "밝기는 `Window → Rendering → Lighting`에서 바로 만질 것.");
    }

    // 나무·바위 규칙을 물린다. **이미 저작된 규칙이 있으면 안 건드린다** — 도구를 다시 돌렸다고
    // 손으로 맞춘 값이 날아가면 다시 돌릴 엄두가 안 난다(연구 그래프 베이커와 같은 판단).
    static void WireScatter(WorldContentStreamer content)
    {
        var so = new SerializedObject(content);
        var arr = so.FindProperty("scatter");

        // **지하에는 나무가 안 자란다**(2026-08-29). 지상 시절의 규칙(나무·바위)이 그대로
        // 남아 있어서 **파낸 굴 한복판에 소나무가 서 있었다.** 게다가 바위 프리팹은 이제
        // `RockMap`이 그리는 벽과 **뜻이 겹친다**.
        //
        // **비우고 안 채운다** — 지하에 뿌릴 것(버섯·종유석·수정)은 아직 프리팹이 없다.
        // 없는 것을 억지로 채우는 것보다 **아무것도 없는 편**이 낫다.
        if (arr.arraySize > 0)
        {
            Debug.Log($"[월드] 지상 뿌리기 규칙 {arr.arraySize}개를 비웠다 — " +
                      "지하에는 나무가 안 자란다. 지하 프롭이 생기면 여기 채울 것.");
            arr.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void Add(SerializedProperty arr, string name, GameObject prefab,
                    float perChunk, float maxSlope, float minH, float maxH,
                    float spacing, float scaleMin, float scaleMax)
    {
        int i = arr.arraySize;
        arr.arraySize = i + 1;
        var e = arr.GetArrayElementAtIndex(i);
        e.FindPropertyRelative("Name").stringValue          = name;
        e.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        e.FindPropertyRelative("PerChunk").floatValue       = perChunk;
        e.FindPropertyRelative("MaxSlope").floatValue       = maxSlope;
        e.FindPropertyRelative("MinHeight").floatValue      = minH;
        e.FindPropertyRelative("MaxHeight").floatValue      = maxH;
        e.FindPropertyRelative("MinSpacing").floatValue     = spacing;
        e.FindPropertyRelative("ScaleRange").vector2Value   = new Vector2(scaleMin, scaleMax);
    }

    // **폰이 딛는 지면을 절차 지형으로 돌린다.** 안 켜면 화면만 새 지형이고 폰은 옛
    // `Terrain`(또는 평지 0)을 딛어서, **지면 아래를 걷거나 허공에 뜬다.**
    static void EnableProceduralTerrain()
    {
        var maps = Object.FindObjectsByType<MapManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps.Length == 0) { Debug.LogWarning("[월드] 씬에 MapManager가 없다."); return; }

        foreach (var m in maps)
        {
            var so = new SerializedObject(m);
            var p = so.FindProperty("proceduralTerrain");
            if (p == null) continue;
            p.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(m);
        }
        Debug.Log($"[월드] MapManager {maps.Length}개의 '절차 지형'을 켰다.");
    }

    // **손으로 조각해 둔 옛 Terrain을 재운다.** 켜 두면 겹쳐 그려지고, 무엇보다
    // `Terrain.activeTerrain`이 그걸 집어서 높이가 따로 논다. **지우지는 않는다** —
    // 되돌릴 수 있어야 하고, 그건 사람이 정할 일이다.
    static int SleepOldTerrains(GameObject world)
    {
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t == null || t.transform.IsChildOf(world.transform)) continue;
            if (t.gameObject.name.StartsWith("Chunk_") || t.gameObject.name.StartsWith("Edit_")) continue;
            Undo.RecordObject(t.gameObject, "옛 Terrain 재우기");
            t.gameObject.SetActive(false);
            EditorUtility.SetDirty(t.gameObject);
            n++;
        }
        return n;
    }

    // **옛 3D 월드맵 화면의 M키를 끈다.** 안 끄면 지도가 둘 뜬다 — 새 `WorldMapPanel`과
    // `WorldMapView`가 같은 키를 문다. **컴포넌트를 지우지는 않는다**(참조가 40곳이고
    // 씬 배선이 걸려 있다) — 진입로만 끊는다.
    static int SleepLegacyWorldScreen()
    {
        int n = 0;
        foreach (var v in Object.FindObjectsByType<WorldMapView>(FindObjectsInactive.Include,
                                                                 FindObjectsSortMode.None))
        {
            var so = new SerializedObject(v);
            var p = so.FindProperty("legacyWorldScreen");
            if (p == null) continue;
            p.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(v);
            n++;
        }
        return n;
    }

    static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
}
