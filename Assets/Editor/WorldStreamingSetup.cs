using System.Collections.Generic;
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

        if (!go.TryGetComponent(out TerrainStreamer terrain)) terrain = Undo.AddComponent<TerrainStreamer>(go);
        if (!go.TryGetComponent(out WorldContentStreamer content)) content = Undo.AddComponent<WorldContentStreamer>(go);

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

        // **전장의 안개**(2026-09-15). 없어도 `RockRenderer`가 판이 열릴 때 붙이지만, 씬에 붙어
        // 있어야 **시야 반경을 인스펙터에서 맞추고 남길 수 있다.**
        if (go.GetComponent<FogOfWar>() == null) Undo.AddComponent<FogOfWar>(go);

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
        WireTerrainMaterial(st);
        st.ApplyModifiedPropertiesWithoutUndo();

        ApplyUnderworldLighting();

        WireScatter(content);
        EnableProceduralTerrain();
        int slept   = SleepOldTerrains(go);
        int shaders = RegisterRuntimeShaders();

        WarnStartTile();

        EditorUtility.SetDirty(go);
        Selection.activeGameObject = go;

        Debug.Log($"[월드] 배선 완료 — {go.name}에 TerrainStreamer + WorldContentStreamer + BandMaterializer. " +
                  $"레이어 '{LayerMask.LayerToName(layer)}' · 옛 Terrain {slept}개 · " +
                  $"셰이더 {shaders}개 새로 등록." + "\n" +
                  $"세계 {TerrainGrid.WorldSize}m · 청크 {TerrainGrid.ChunkSize}m · " +
                  $"{TerrainGrid.ChunksPerAxis}×{TerrainGrid.ChunksPerAxis}");
    }

    // ── 판이 시작되는 자리가 지도와 어긋났나 ────────────────────────────────
    //
    // **여기서 옮기지 않고 말만 한다**(2026-09-15). 이 도구는 `1. Rebuild Underworld`가
    // 단계로 부르는 상시 도구라, 여기서 무리를 옮기면 **묶음을 돌릴 때마다 제자리로
    // 끌려온다** — `WireScatter`가 뿌리기 규칙을 매번 비우던 그 함정이다. 옮기는 문은
    // `World → C. Move Party To Start Tile` 하나이고 사람이 누른다.
    //
    // 그래도 여기서 **묻기는 해야 한다.** 어긋나 있어도 화면에는 *"자원이 안 보인다"*로만
    // 보이고(시작 자리의 바이옴이 무엇을 내놓느냐가 통째로 달라진다) 그 원인을 가리키는
    // 것이 아무 데도 없었다 — 사흘을 거기 썼다.
    static void WarnStartTile()
    {
        if (!StartPlacement.Read(out var start, out var target, out var biome)) return;

        var pawns = StartPlacement.PlayerPawns();
        if (pawns.Length == 0) return;

        var from = StartPlacement.Centroid(pawns);
        float d = Vector2.Distance(new Vector2(from.x, from.z), new Vector2(target.x, target.z));
        if (d < TerrainGrid.RegionSize * 0.5f) return;   // 시작 칸 안이다

        Debug.LogWarning($"[월드] 무리가 시작 칸 밖에 서 있다 — ({from.x:F0}, {from.z:F0})인데 " +
                         $"지도가 말하는 시작 칸은 ({start.x},{start.y}) = ({target.x:F0}, {target.z:F0}), " +
                         $"**{d:F0}m 떨어져 있다**.\n" +
                         $"판은 폰이 선 자리에서 열리므로 지금 시작 바이옴은 지도의 **{biome.DisplayName()}**이 " +
                         "아니다 — 거기서 나는 광맥과 식물이 통째로 달라진다. " +
                         "`World → C. Move Party To Start Tile`로 옮긴다.");
    }

    // ── 런타임에 이름으로 찾는 셰이더를 빌드에 넣는다 ──────────────────────
    //
    // **`Shader.Find`는 빌드에 포함된 것만 찾는다.** 그리고 셰이더가 빌드에 들어가는 길은
    // **어떤 재질 에셋이 그것을 가리키는 것** 하나뿐인데, 코드가 이름으로 찾는 것은 참조가
    // 아니다. 그래서 **에디터에서는 멀쩡하고 빌드에서만 검게 나온다** — 2026-09-02에 지하
    // 지형이 통째로 검은 판이었고, 원인이 `DungeonColony/Terrain/Lit`이 잘려 나간 것이었다.
    //
    // **Always Included Shaders가 그 유일한 문이다** — *"재질이 안 가리켜도 넣어라"*.
    // 목록은 `RuntimeShaders.All`이 든다(코드가 찾는 이름과 여기가 넣는 이름이 갈리면
    // 그 셰이더만 조용히 빠진다).
    //
    // **여기 붙이는 이유**: 이건 씬 배선이 아니라 프로젝트 설정이지만, 따로 눌러야 하는
    // 버튼을 하나 더 만들면 **언젠가 그것만 안 누른다**(*"손으로 여섯 번 누르게 하면 언젠가
    // 다섯만 누른다"*). 이미 빠짐없이 도는 버튼에 얹는다.
    static int RegisterRuntimeShaders()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("[월드] Graphics Settings를 못 열었다 — 런타임 셰이더가 빌드에서 잘려 " +
                           "**땅바닥이 검게 나온다.**");
            return 0;
        }

        var so   = new SerializedObject(assets[0]);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        if (list == null)
        {
            Debug.LogError("[월드] `m_AlwaysIncludedShaders`를 못 찾았다 — 필드 이름이 바뀌었나? " +
                           "**셰이더가 안 등록됐다.**");
            return 0;
        }

        // ── ① 넣으면 안 되는 것을 먼저 뺀다 ────────────────────────────────
        //
        // **한 번 잘못 넣어 봤다**(2026-09-02) — 지형 셰이더까지 아홉을 넣었더니 빌드 중에
        // 유니티가 죽었다(변형이 수만 개다. 근거는 `RuntimeShaders.AlwaysInclude`).
        // 그래서 **되돌리는 길을 도구가 든다** — 손으로 지우게 하면 다음 사람이 같은 자리에
        // 다시 빠진다. 지우는 것은 **우리가 넣었던 이름**뿐이라(`RuntimeShaders.All`) 남이
        // 넣어 둔 것은 안 건드린다.
        int removed = 0;
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            var sh = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if (sh == null) continue;

            bool ours = System.Array.IndexOf(RuntimeShaders.All, sh.name) >= 0;
            bool keep = System.Array.IndexOf(RuntimeShaders.AlwaysInclude, sh.name) >= 0;
            if (!ours || keep) continue;

            list.DeleteArrayElementAtIndex(i);
            removed++;
        }

        // ── ② 작은 것만 넣는다 ────────────────────────────────────────────
        int added = 0;
        foreach (var name in RuntimeShaders.AlwaysInclude)
        {
            var sh = Shader.Find(name);

            // **빌트인 중에는 이 프로젝트에 없는 것도 있다**(파이프라인마다 다르다).
            // 폴백으로만 쓰이는 이름이라 없다고 해서 사고는 아니다 — 조용히 넘긴다.
            if (sh == null) continue;

            bool has = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) { has = true; break; }
            if (has) continue;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
            added++;
        }

        if (added > 0 || removed > 0)
        {
            so.ApplyModifiedProperties();

            // **바로 디스크에 쓴다**(2026-09-02). `ApplyModifiedProperties`는 메모리의
            // 오브젝트만 고치고, `ProjectSettings/GraphicsSettings.asset`은 유니티가 저장할 때
            // 써진다 — 그런데 **여기 걸리면 유니티가 빌드 중에 죽는다.** 죽으면 고친 것이
            // 디스크에 안 남아, 다시 켜도 **같은 목록으로 또 죽는다.** 실제로 그렇게 한 바퀴
            // 돌았다. 되돌리는 도구가 되돌린 것을 못 남기면 도구가 아니다.
            AssetDatabase.SaveAssets();
        }
        if (removed > 0)
            Debug.Log($"[월드] Always Included에서 무거운 셰이더 {removed}개를 뺐다 — " +
                      "지형 셰이더는 변형이 수만 개라 **빌드가 죽는다.** 지형은 재질로 넣는다.");
        return added;
    }

    // ── 지형 재질을 만들어 씬에 물린다 ──────────────────────────────────────
    //
    // **이것이 지형 셰이더를 빌드에 넣는 올바른 문이다.** 재질이 셰이더를 가리키면 그것은
    // *참조*라 유니티가 **실제로 쓰이는 변형만** 굽는다 — Always Included처럼 전부 굽지 않는다.
    //
    // 딸려 오는 것: **런타임에 `Shader.Find`를 부를 일이 없어진다**(`TerrainStreamer.Material`이
    // 물린 재질을 그대로 쓴다). 이름으로 찾는 길은 씬 배선을 빠뜨렸을 때의 그물로만 남는다.
    static void WireTerrainMaterial(SerializedObject st)
    {
        const string Dir  = "Assets/2_Material/Underworld";
        const string Path = Dir + "/TerrainChunk.mat";

        var prop = st.FindProperty("terrainMaterial");
        if (prop == null)
        {
            Debug.LogError("[월드] `terrainMaterial` 필드를 못 찾았다 — 이름이 바뀌었나? " +
                           "**빌드에서 땅바닥이 검게 나온다.**");
            return;
        }
        if (prop.objectReferenceValue != null) return;   // 손으로 물린 것은 안 건드린다

        var sh = Shader.Find(RuntimeShaders.TerrainLit)
              ?? Shader.Find(RuntimeShaders.UrpTerrainLit);
        if (sh == null)
        {
            Debug.LogError("[월드] 지형 셰이더를 못 찾았다 — `2_Material/TerrainShader/`가 " +
                           "컴파일에 실패했나? **빌드에서 땅바닥이 검게 나온다.**");
            return;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(Path);
        if (mat == null)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/2_Material", "Underworld");
            mat = new Material(sh) { name = "TerrainChunk" };
            AssetDatabase.CreateAsset(mat, Path);
            AssetDatabase.SaveAssets();
        }
        else if (mat.shader != sh)
        {
            mat.shader = sh;
            EditorUtility.SetDirty(mat);
        }

        prop.objectReferenceValue = mat;
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
    //
    // **바닥은 y = 0이다**(2026-09-12, 사용자 결정 — 전에는 30이었다). 지하 바이옴이 전부
    // `Flat`이라(`BiomeTerrain` — `Base`가 0이다) **높이를 만드는 것이 이 값 하나뿐**이고,
    // 그래서 30은 *아무것도 아닌 30*이었다. 0이면 씬에서 읽는 좌표가 곧 바닥이고,
    // 무엇이 떠 있고 묻혔는지를 눈이 아니라 숫자로 잴 수 있다.
    //
    // **딸려 오는 것 하나** — 바닥이 0이면 *바닥 높이를 0으로 가정한 코드*가 **틀린 채로
    // 맞아 보인다**(`RockMap.SurfaceOf`가 2026-09-12에 그 함정에서 잡혔다: `MapManager`가
    // 없을 때 0으로 떨어뜨려 벽이 전부 y=0에 섰는데, 바닥이 30이라 **화면에 드러났다**).
    // 그러니 폴백은 **0이 아니라 함수가 답하게** 둘 것 — 여기서 값을 내린다고 그 규칙이
    // 느슨해지는 것이 아니다.
    static void WriteUnderworldProfiles(SerializedObject st)
    {
        Set(st, "profile.BaseHeight",         0f);
        Set(st, "profile.ContinentAmplitude", 0f);
        Set(st, "profile.HillAmplitude",      0f);
        Set(st, "profile.DetailAmplitude",    0f);

        // ── 바닥은 평지되 층층이다 (2026-09-24, 사용자 결정) ──────────────
        //
        // *"너무 평면"*이라 높낮이를 넣었는데 **이어지는 기복이 아니다** — 위의 진폭 셋을
        // 되살리면 그건 언덕이고, 지하에서 그것이 안 되는 이유는 이미 `BiomeTerrain`에
        // 적혀 있다(바닥이 물결치면 벽 높이가 칸마다 달라져 **파낸 방이 웅덩이로 보인다**).
        //
        // 계단은 그 규칙을 안 깬다 — **한 패치가 통째로 한 높이**라 방은 여전히 평면이고,
        // 갈리는 것은 *이 평면과 저 평면의 높이*뿐이다. 근거는 `TerrainHeight.Terrace`.
        //
        // **새 필드라 씬은 0을 들고 있다** — 안 쓰면 단이 0이라 계단이 통째로 안 난다.
        // **값은 화면에서 정했다**(2026-09-24, 사용자). 처음엔 단 0.1m으로 넣었는데 재 보니
        // 전이 경사가 **2.9°**라 화면에서 평면과 구별되지 않았다(`0b. Probe Floor Terraces`가
        // 그걸 갈랐다 — 층은 여섯 개 났는데 안 읽혔다). 1.0m에서 **26.6°**이고 그것이
        // 사용자가 고른 값이다.
        //
        // **1.0m은 수직 턱이 아니다** — 전이가 정점 한 간격(2m)에 걸치므로 칸당 상승이
        // 0.5m이고 `PathGrid`가 자연 지형에 허용하는 `SlopeRise`(1.0m/칸) 안이다.
        // `PawnMover.StepUp`(0.5m)은 **바닥이 만든 수직면**에 쓰는 값이라 여기 안 걸린다.
        //
        // **딸려 오는 것 하나** — 평탄 허용치가 `0.6 + 0.2 × 칸`이라 **1×1 건물은 0.8m까지**다.
        // 단이 그보다 크므로 발자국이 단 경계에 걸치면 거절된다. 패치가 6m라 안쪽이면
        // 괜찮고, 걸리는 자리는 **기단이 원래 그 답이다**(`BuildingData.LevelFloor`).
        Set(st,    "profile.TerraceCell",   6f);
        Set(st,    "profile.TerraceStep",   1.0f);
        SetInt(st, "profile.TerraceLevels", 6);
        Set(st,    "profile.TerracePeriod", 40f);

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

        // **세계에 맞춘다.** 청크가 64m라 지형 반경 2는 320m다. 옛 값(시뮬 1,200m · 일감
        // 600m)은 세계가 256m이던 때 **세계보다 넉 배 커서 뜻을 잃었다** — 값이 뜻을 잃으면
        // 다음에 보는 사람이 *"왜 안 걸리지"*를 여기서 찾게 된다.
        //
        // **세계를 1,024m로 넓힌 뒤에도 그대로 둔다**(2026-08-31) — 이제 시뮬 반경이 세계의
        // 3분의 1이라 **밖이 실제로 생긴다**. 그 밖에서 벌어지는 습격은 얼려 두는 것이 아니라
        // `WorldBattle.ResolveRaid`가 추상으로 결과를 낸다.
        Set(st, "simRadius",      320f);
        Set(st, "maxJobDistance", 200f);

        // **천장(여유)도 씬에 쓴다** — 필드를 새로 만든 날 씬이 옛 기본값으로 굳었으면
        // 코드를 고쳐도 **세계가 뻥 뚫린 채**이거나 **통짜 바위**다(둘 다 겪었다).
        //
        // **열림은 점이 아니라 선이다**(2026-08-30). 여유를 노이즈의 **합**으로 내던 때는
        // 열리는 자리가 여러 노이즈의 꼭대기가 겹치는 곳이라 굴이 **물방울로 흩어지고 길이
        // 안 났다** — 화면에는 시작 공터 하나만 둥그렇게 있었다. 이제 능선을 따라 통로가
        // 나고, 값의 뜻도 바뀌었다(`TerrainCeilingProfile` 참조).
        //
        // **주기는 미터로 저작하고 세계 크기를 안 탄다**(2026-08-31에 바로잡았다).
        //
        // 전에는 여기에 *"세계를 넓히면 주기도 같이 키울 것"*이라고 적혀 있었는데, 그건
        // **지도 그림을 기준으로 한 말**이고 판정에는 틀렸다. 폭(간선 9m · 잔가지 5m)은
        // **폰의 눈금**이라 세계가 넓어져도 안 변한다 — 주기만 4배로 키우면 통로 사이가
        // 400m가 되어 **열린 넓이가 4분의 1에서 16분의 1로 떨어지고**, 굴이 아니라
        // *가끔 실금이 간 통짜 바위*가 된다(걸어서 4분 동안 벽만 보게 된다).
        //
        // 주기를 그대로 두면 **국소 그림이 그대로**다: 세계 256m에서 간선이 두어 번
        // 가로지르던 것이 1,024m에서 열 번 가로지를 뿐, 한 자리에서 보이는 굴은 같다.
        // **세계 크기를 따라가야 하는 것은 셀 *수*로 재는 값**(광역 격자)이지 미터로 재는
        // 값이 아니다. 확인은 `World → 2. Preview Cave Map` 한 장으로 한다.
        Set(st, "ceilingProfile.BaseClearance", -11f);
        Set(st, "ceilingProfile.OpenClearance",   6f);
        //
        // **2026-09-15에 개미굴을 큰길로 바꿨다**(사용자) — 주기가 곧 길의 수다. 근거와 흉내 낸
        // 넓이는 `TerrainCeilingProfile.Default`에 있다. **여기 값과 그쪽 기본값을 같이 옮길 것.**
        Set(st, "ceilingProfile.PassagePeriod", 260f);
        Set(st, "ceilingProfile.PassageWidth",   10f);
        Set(st, "ceilingProfile.BranchPeriod",  170f);
        Set(st, "ceilingProfile.BranchWidth",     6f);
        Set(st, "ceilingProfile.ChamberPeriod", 120f);
        Set(st, "ceilingProfile.ChamberRarity", 0.56f);
        Set(st, "ceilingProfile.ChamberWiden",   16f);
        // 큰 굴(09-15, 사용자: *"부분 부분, 이어진 것도 안 이어진 것도"*).
        Set(st, "ceilingProfile.CavernSpacing",  205f);
        Set(st, "ceilingProfile.CavernChance",     1f);
        Set(st, "ceilingProfile.CavernRadiusMin", 15f);
        Set(st, "ceilingProfile.CavernRadiusMax", 30f);
        Set(st, "ceilingProfile.CavernSealed",   0.4f);
        Set(st, "ceilingProfile.CavernWall",       6f);
        Set(st, "ceilingProfile.CavernSpurWidth",  6f);
        Set(st, "ceilingProfile.RoughPeriod",    18f);
        Set(st, "ceilingProfile.RoughAmplitude", 0.6f);
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

    // **정수는 따로 받는다** — `floatValue`를 정수 필드에 넣으면 유니티가 조용히 0으로 둔다.
    static void SetInt(SerializedObject so, string path, int v)
    {
        var p = so.FindProperty(path);
        if (p == null)
        {
            Debug.LogError($"[월드] `{path}`를 못 찾았다 — 필드 이름이 바뀌었나? **이 값은 안 써졌다.**");
            return;
        }
        p.intValue = v;
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

        // **하루 주기 조명(`DayNightLighting`)은 2026-09-12에 지웠다** — 방향광을 하루에 한
        // 바퀴 돌리는 물건인데 **지하에는 해가 없다.** 어두워지는 일은 `UnderworldLighting`이
        // 밀물·썰물로 하고, 그쪽은 주변광과 안개만 만진다. 씬에 껍데기가 남아 있으면 위의
        // `RemoveMonoBehavioursWithMissingScript`가 걷어낸다(스트리밍 루트에 붙어 있을 때).
        //
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

        Debug.Log($"[월드] 지하 조명 — 하늘 제거 · 방향광 {suns}개 재움 · 푸른 주변광 · " +
                  "**검은 안개**(짙기 0.006).\n" +
                  "밝기는 `Window → Rendering → Lighting`에서 바로 만질 것.");
    }

    // **뿌리기 규칙은 여기서 안 건드린다**(2026-09-14에 뒤집었다).
    //
    // 2026-08-29에는 **비웠다.** 지상 시절의 규칙(나무·바위)이 그대로 남아 **파낸 굴 한복판에
    // 소나무가 서 있었고**, 지하에 뿌릴 것은 아직 없었으니 *비우고 안 채우는* 것이 맞았다.
    //
    // **그 뒤로 지하에 뿌릴 것이 생겼다** — 버섯·바닥 돌(2026-09-06) · 바닥 광맥(2026-09-14).
    // 그런데 이 함수가 그대로 남아, **`1. Rebuild Underworld`가 첫 단계에서 그것을 매번
    // 지웠다**(이 묶음이 콘텐츠 도구를 다시 부르지 않으니 아무도 되채우지 않는다). 그래서
    // 씬의 `scatter`가 **비어 있었다** — 에셋도 프리팹도 멀쩡한데 세계에 버섯 한 송이가 없고,
    // 화면에는 *"버섯이 안 보인다"*로만 나타난다.
    //
    // **일회성 이행을 상시 도구에 남겨 두면 그것이 규칙이 된다.** 이 함수의 첫 줄이 이미
    // *"이미 저작된 규칙이 있으면 안 건드린다"*라고 말하고 있었고, 하는 일은 정반대였다.
    //
    // 이제 **채우는 문은 콘텐츠 도구 쪽 하나다**(`ScatterWiring`) — `7. Create Mushrooms`와
    // `B. Create Metal Ladder`. 둘 다 `1. Rebuild Underworld`에 들어가 있다.
    static void WireScatter(WorldContentStreamer content)
    {
        var so  = new SerializedObject(content);
        var arr = so.FindProperty("scatter");

        if (arr.arraySize == 0)
            Debug.LogWarning("[월드] 뿌리기 규칙이 비어 있다 — 굴 바닥에 버섯도 돌도 광맥도 " +
                             "안 난다.\n`World → 7. Create Mushrooms`와 " +
                             "`World → B. Create Metal Ladder`를 돌릴 것(둘 다 씬에 쓴다).");
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

    static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    // ── 계단이 실제로 났나 ──────────────────────────────────────────────────
    //
    // **`안 보인다`는 두 증상이 같은 말로 들린다**(2026-09-24). 바닥에 계단을 넣은 뒤
    // *"높낮이가 차이나는 곳이 안 보여"*를 받았는데, 그 한마디에 원인이 셋 들어 있다:
    //   ① **값이 씬에 안 써졌다** — 새 필드는 씬에서 0이고 단이 0이면 계단이 아예 안 난다
    //   ② **났는데 안 읽힌다** — 0.1m 단은 전이 경사가 2.9°라 화면에서 평면과 구별되지 않는다
    //   ③ **굴 바닥에만 안 났다** — 세계의 3분의 2가 바위라 *난 자리*가 전부 벽 속일 수 있다
    // 셋은 화면에서 똑같이 보이고 **세어 보기 전에는 못 가른다**(바닥 광맥과 버섯에서 연달아
    // 당했다 — `WorldContentStreamer.ReportCensus`가 그 답이었다).
    //
    // **판정과 같은 함수로 잰다** — `TerrainHeight.At`과 `RockMap.IsRock`을 그대로 부른다.
    // 눈금을 따로 들면 *"진단은 층이 났다는데 화면은 평면"*이 되어 층이 하나 더 는다.
    [MenuItem("DungeonColony/Terrain/0b. Probe Floor Terraces (계단 진단)")]
    static void ProbeTerraces()
    {
        var s = Object.FindFirstObjectByType<TerrainStreamer>(FindObjectsInactive.Include);
        if (s == null)
        {
            Debug.LogError("[계단] 씬에 `TerrainStreamer`가 없다 — `0. Setup World Streaming`을 먼저 돌릴 것.");
            return;
        }

        // 씬 값을 함수에 밀어 넣는다. **이게 없으면 코드 기본값을 재게 되어** 씬이 0을 든
        // 바로 그 경우에 *"멀쩡하다"*고 답한다.
        TerrainAuthoring.SyncValues();

        var p = TerrainHeight.Profile;
        int cells = Mathf.Max(1, Mathf.RoundToInt(p.TerraceCell / TerrainGrid.VertexSpacing));
        float cell = cells * TerrainGrid.VertexSpacing;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[계단] 씬(`{s.gameObject.name}`)이 든 값 — 셀 {p.TerraceCell}m → **{cell}m**(정점 간격 " +
                      $"{TerrainGrid.VertexSpacing}m의 {cells}배로 접힘) · 단 {p.TerraceStep}m · " +
                      $"층수 ±{p.TerraceLevels} · 주기 {p.TerracePeriod}m · 시드 {p.Seed}");

        if (p.TerraceStep <= 0.0001f || p.TerraceLevels <= 0)
        {
            Debug.LogError(sb + "\n**계단이 꺼져 있다** — 단이나 층수가 0이다. 새 필드는 씬에서 0으로 " +
                           "시작하므로 `Terrain → 0. Setup World Streaming`을 한 번 돌리거나 " +
                           "`World` 오브젝트의 `Profile`을 펼쳐 손으로 넣을 것.");
            return;
        }

        // 보고 있는 자리를 잰다 — *"안 보인다"*는 그 화면의 얘기다.
        var view = SceneView.lastActiveSceneView;
        Vector3 c = view != null ? view.pivot : new Vector3(TerrainGrid.WorldSize * 0.5f, 0f,
                                                            TerrainGrid.WorldSize * 0.5f);
        const int R = 64;   // ±64m = 129×129칸

        var levels = new Dictionary<int, int>();
        float lo = float.MaxValue, hi = float.MinValue;
        int open = 0, total = 0;

        for (int dz = -R; dz <= R; dz++)
        for (int dx = -R; dx <= R; dx++)
        {
            var tile = new Vector2Int(Mathf.FloorToInt(c.x) + dx, Mathf.FloorToInt(c.z) + dz);
            if (!TerrainGrid.TileInWorld(tile)) continue;
            total++;

            // **열린 칸만 센다** — 바위 속 바닥은 아무도 못 본다. 여기서 안 가르면
            // *"층이 일곱 개 났다"*고 답하면서 화면은 여전히 평면일 수 있다.
            if (RockMap.Enabled && RockMap.IsRock(tile)) continue;
            open++;

            float y = TerrainHeight.At(tile.x + 0.5f, tile.y + 0.5f);
            if (y < lo) lo = y;
            if (y > hi) hi = y;

            int lv = Mathf.RoundToInt(y / p.TerraceStep);
            levels.TryGetValue(lv, out int n);
            levels[lv] = n + 1;
        }

        if (open == 0)
        {
            Debug.LogWarning(sb + $"\n({c.x:F0}, {c.z:F0}) 둘레 {R}m에 **열린 칸이 하나도 없다** — " +
                             "통짜 바위를 보고 있다. 카메라를 굴 쪽으로 옮겨 다시 잴 것.");
            return;
        }

        // 이웃 정점 사이 최대 낙차 — **이것이 *보이나*에 답한다.** 단이 아무리 층져도
        // 전이가 정점 한 간격에 걸쳐 나므로, 화면에 서는 것은 그 각도다.
        // **셀 경계가 휘고부터는(2026-10-28, `TerrainHeight.WarpCell`) 셀 번호의 이웃이 곧 땅의 이웃이 아니다** —
        // 그래서 걸어갈 수 있나도 정점끼리 잰다(대각선까지). 한 단 반을 넘게 벌어진 정점 쌍이 *두 단 턱*이다.
        float maxDrop = 0f;
        int vertexCliffs = 0;
        float cliffDrop = p.TerraceStep * 1.5f;
        int g0x = Mathf.FloorToInt((c.x - R) / TerrainGrid.VertexSpacing);
        int g0z = Mathf.FloorToInt((c.z - R) / TerrainGrid.VertexSpacing);
        int gN = (2 * R) / TerrainGrid.VertexSpacing;
        for (int gz = g0z; gz <= g0z + gN; gz++)
        for (int gx = g0x; gx <= g0x + gN; gx++)
        {
            float h = TerrainHeight.VertexHeightGlobal(gx, gz);
            float a1 = Mathf.Abs(TerrainHeight.VertexHeightGlobal(gx + 1, gz) - h);
            float a2 = Mathf.Abs(TerrainHeight.VertexHeightGlobal(gx, gz + 1) - h);
            float a3 = Mathf.Abs(TerrainHeight.VertexHeightGlobal(gx + 1, gz + 1) - h);
            float a4 = Mathf.Abs(TerrainHeight.VertexHeightGlobal(gx + 1, gz - 1) - h);
            maxDrop = Mathf.Max(maxDrop, Mathf.Max(a1, a2));
            if (a1 > cliffDrop) vertexCliffs++;
            if (a2 > cliffDrop) vertexCliffs++;
            if (a3 > cliffDrop) vertexCliffs++;
            if (a4 > cliffDrop) vertexCliffs++;
        }
        float deg = Mathf.Atan2(maxDrop, TerrainGrid.VertexSpacing) * Mathf.Rad2Deg;

        // **맞닿은 셀이 두 단 이상 벌어진 경계 — 이것이 *걸어갈 수 있나*에 답한다**(2026-09-26).
        // 전이가 2m에 걸치므로 두 단(1m 단이면 2m)이 넘으면 칸당 `SlopeRise`를 넘는 턱이다.
        // 대각선까지 센다 — 폰은 대각선으로도 걷는다. **0이어야 한다**(`TerrainHeight.TerraceLevel`).
        int cliffs = 0, pairs = 0;
        int c0x = Mathf.FloorToInt((c.x - R) / cell), c1x = Mathf.FloorToInt((c.x + R) / cell);
        int c0z = Mathf.FloorToInt((c.z - R) / cell), c1z = Mathf.FloorToInt((c.z + R) / cell);
        for (int iz = c0z; iz <= c1z; iz++)
        for (int ix = c0x; ix <= c1x; ix++)
        {
            int a = TerrainHeight.TerraceCellLevel(ix, iz);
            int[] nb = { TerrainHeight.TerraceCellLevel(ix + 1, iz), TerrainHeight.TerraceCellLevel(ix, iz + 1),
                         TerrainHeight.TerraceCellLevel(ix + 1, iz + 1), TerrainHeight.TerraceCellLevel(ix + 1, iz - 1) };
            foreach (int b in nb) { pairs++; if (Mathf.Abs(a - b) >= 2) cliffs++; }
        }

        var keys = new List<int>(levels.Keys);
        keys.Sort();
        sb.AppendLine($"({c.x:F0}, {c.z:F0}) 둘레 {R}m — 열린 칸 {open} / {total}칸 · " +
                      $"바닥 {lo:F2} ~ {hi:F2}m(차 **{hi - lo:F2}m**) · 서로 다른 층 **{keys.Count}개**");
        foreach (int k in keys)
            sb.AppendLine($"    {k * p.TerraceStep,6:F2}m ({(k > 0 ? "+" : "")}{k}층) — {levels[k],6}칸 " +
                          $"{levels[k] * 100f / open,5:F1}%");
        sb.Append($"이웃 정점 최대 낙차 **{maxDrop:F2}m / {TerrainGrid.VertexSpacing}m = {deg:F1}°** — " +
                  "화면에 서는 것은 이 각도다(30° 넘으면 절벽 텍스처까지 바뀐다).");
        sb.Append($"\n두 단 이상 벌어진 셀 경계 **{cliffs}** / {pairs} — " +
                  (cliffs == 0 ? "어디든 걸어갈 수 있다." : "**못 넘는 턱이 있다.**"));
        sb.Append($"\n한 단 반 넘게 벌어진 이웃 정점 **{vertexCliffs}**" +
                  (vertexCliffs == 0 ? " — 휜 경계에서도 걸어갈 수 있다." : " — **휜 경계에 못 넘는 턱이 있다.**"));

        if (cliffs > 0)
            Debug.LogError(sb + "\n계단 봉투가 이웃 셀을 한 단 이내로 못 누르고 있다 — `TerrainHeight.TerraceLevel`을 볼 것.");
        else if (vertexCliffs > 0)
            Debug.LogError(sb + "\n셀 경계의 휨이 너무 가파르다 — 맞닿은 정점이 먼 셀에 떨어진다. `TerrainHeight.WarpCell`의 폭을 줄일 것.");
        else if (keys.Count <= 1)
            Debug.LogWarning(sb + "\n**층이 하나뿐이다** — 주기가 이 범위보다 커서 통째로 한 층에 " +
                             "들어왔을 수 있다. `Terrace Period`를 줄이거나 더 넓게 잴 것.");
        else if (deg < 5f)
            Debug.Log(sb + $"\n층은 {keys.Count}개 **실제로 났다** — 다만 {deg:F1}°는 눈에 안 걸리는 " +
                      "기울기라 화면에서는 평면과 구별되지 않는다. `Terrace Step`을 올리거나 " +
                      "(0.3m → 8.5° · 0.5m → 14°) `Terrace Cell`을 줄여 전이가 잦아지게 할 것.");
        else
            Debug.Log(sb.ToString());
    }
}
