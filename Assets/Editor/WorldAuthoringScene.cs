using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// **세계를 씬에서 저작한다**(2026-08-27) — `docs/design_world_editor.md` 2단계.
//
//     World.unity (저작 씬)                  Resources/WorldContent/{x}_{y}.asset
//       세계 전체가 진짜 오브젝트로 서 있다 ──굽기──▶  청크별 목록
//       유니티에서 그냥 편집한다                        런타임이 이것만 읽는다
//
// **저작 씬은 빌드에 안 들어간다.** 넣으면 런타임에 세계 전체가 메모리에 올라와 스트리밍이
// 있는 이유가 사라진다.
//
// 쓰는 법:
//   3. Open Authoring Scene    — `World.unity`를 (없으면 만들어) **추가로** 연다
//   4. Load World Into Scene   — 구워 둔 것을 전부 펼친다 (이행할 때 한 번)
//   … 그냥 유니티에서 편집한다 — 옮기고 · 지우고 · 새로 놓고 · 복제하고 …
//   5. Bake World              — 되짚어 굽는다
public static class WorldAuthoringScene
{
    // 이만큼 넘게 떠 있으면 *지형이 밑에서 움직였다*고 의심한다. 일부러 띄우는 저작
    // (다리·부유 구조물)은 이보다 훨씬 크게 띄우므로 겹치지 않는다.
    const float FloatWarn = 3f;

    const string ScenePath = "Assets/0_Scenes/World.unity";
    // **스트리밍 루트도 이름이 "World"다**(`WorldStreamingSetup`) — 저작 씬을 같이 열어 두면
    // `GameObject.Find("World")`가 어느 쪽을 집을지 알 수 없다. 이름을 갈라 둔다.
    const string RootName  = "WorldAuthoring";

    // ── 3. 연다 ─────────────────────────────────────────────────────────────
    //
    // **추가로 연다**(`Additive`). 게임 씬을 닫으면 `TerrainStreamer`가 사라져 지형 프로필을
    // 못 읽고, 그러면 `YOffset`을 잴 수가 없다(아래 굽기 참고). 둘을 같이 열어 두는 것이
    // 정상 작업 상태다.
    [MenuItem("DungeonColony/World/3. Open Authoring Scene")]
    static void Open()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded)
        {
            Debug.Log("[저작 씬] 이미 열려 있다 — " + ScenePath);
            Select();
            return;
        }

        if (!System.IO.File.Exists(ScenePath))
        {
            var created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var go = new GameObject(RootName);
            go.AddComponent<WorldAuthoringRoot>();
            SceneManager.MoveGameObjectToScene(go, created);

            System.IO.Directory.CreateDirectory("Assets/0_Scenes");
            EditorSceneManager.SaveScene(created, ScenePath);

            Debug.Log($"[저작 씬] 만들었다 → {ScenePath}\n" +
                      "**Build Settings에 넣지 말 것** — 넣으면 런타임에 세계 전체가 올라온다.\n" +
                      "다음: `4. Load World Into Scene`으로 구워 둔 것을 펼친다.");
        }
        else
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Debug.Log("[저작 씬] 열었다 — " + ScenePath);
        }
        Select();
    }

    static void Select()
    {
        var root = FindRoot();
        if (root == null) return;

        // 이름이 갈리기 전에 만든 저작 씬을 따라잡는다 — 안 고치면 스트리밍 배선 도구가
        // 이 오브젝트를 집을 수 있다.
        if (root.name == "World")
        {
            Undo.RecordObject(root.gameObject, "저작 루트 이름 바꾸기");
            root.name = RootName;
            Debug.Log("[저작 씬] 루트 이름을 `WorldAuthoring`으로 바꿨다 — " +
                      "스트리밍 루트(`World`)와 겹쳐 있었다. **씬을 저장할 것.**");
        }
        Selection.activeGameObject = root.gameObject;
    }

    // ── 4. 펼친다 ───────────────────────────────────────────────────────────

    [MenuItem("DungeonColony/World/4. Load World Into Scene")]
    static void Load()
    {
        var root = FindRoot();
        if (root == null) { NoRoot(); return; }

        if (root.transform.childCount > 0)
        {
            bool wipe = EditorUtility.DisplayDialog(
                "이미 펼쳐져 있다",
                $"`{root.name}` 아래에 {root.transform.childCount}개가 있다.\n\n" +
                "다시 펼치려면 지금 있는 것을 **전부 지워야** 한다 — 아직 안 구운 편집이 있으면 사라진다.",
                "지우고 다시 펼친다", "그만둔다");
            if (!wipe) return;

            for (int i = root.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        }

        if (!SyncTerrain("펼치기")) return;

        var chunks = WorldContentAssets.AllChunks();
        int made = 0, missing = 0;

        try
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                EditorUtility.DisplayProgressBar("세계를 펼친다", $"청크 {i + 1}/{chunks.Count}",
                                                 (i + 1) / (float)chunks.Count);

                foreach (var o in WorldContentAssets.Read(chunks[i]))
                {
                    if (o == null) continue;
                    if (o.Prefab == null) { missing++; continue; }

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(o.Prefab, root.gameObject.scene);
                    if (go == null) { missing++; continue; }

                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(
                        new Vector3(o.Position.x,
                                    TerrainHeight.At(o.Position.x, o.Position.y) + o.YOffset,
                                    o.Position.y),
                        Quaternion.Euler(0f, o.Yaw, 0f));

                    if (o.Scale != Vector3.zero) go.transform.localScale = o.Scale;

                    // **이름은 `gameObject.name`이 답한다** — 표식에 또 적지 않는다.
                    if (!string.IsNullOrEmpty(o.Name)) go.name = o.Name;

                    var tag = go.GetComponent<WorldObjectTag>() ?? go.AddComponent<WorldObjectTag>();
                    tag.Id         = string.IsNullOrEmpty(o.Id) ? WorldContentAssets.NewId() : o.Id;
                    tag.Kind       = o.Kind;
                    tag.Persistent = o.Persistent;
                    tag.Generated  = o.Generated;
                    tag.Spec       = o.Spec;
                    tag.QuestId    = o.QuestId;

                    made++;
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        root.MarkLoaded(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log($"[저작 씬] 청크 {chunks.Count}개에서 {made}개를 펼쳤다." +
                  (missing > 0 ? $"\n**프리팹이 빈 것 {missing}개는 못 펼쳤다** — 그건 게임에도 안 선다." : "") +
                  "\n이제 그냥 유니티에서 편집하고 `5. Bake World`. **씬 저장을 잊지 말 것.**");
    }

    // ── 5. 굽는다 ───────────────────────────────────────────────────────────

    [MenuItem("DungeonColony/World/5. Bake World")]
    static void Bake()
    {
        var root = FindRoot();
        if (root == null) { NoRoot(); return; }

        // **전부를 담고 있을 때만 굽는다.** 굽기는 지우기까지 하므로, 일부만 펼쳐 놓고 구우면
        // 안 펼친 청크가 통째로 사라진다. 모드를 둘로 나누지 않는 이유는 `WorldAuthoringRoot` 참고.
        if (!root.LoadedWholeWorld)
        {
            Debug.LogError("[굽기] 이 씬이 세계 전부를 담고 있다는 표시가 없다 — 굽지 않는다.\n" +
                           "**굽기는 지우기까지 한다**(씬에 없는 청크의 에셋을 없앤다). 일부만 펼쳐 놓고 구우면 " +
                           "안 펼친 청크가 통째로 사라진다. `4. Load World Into Scene`을 먼저 실행할 것.");
            return;
        }

        if (!SyncTerrain("굽기")) return;

        var found   = new List<GameObject>();
        var orphans = new List<GameObject>();
        Collect(root.transform, found, orphans);

        // **전부를 담고 있으므로, 이미 있는 청크를 빈 목록으로 깔아 둔다** — 씬에서 사라진
        // 청크는 그대로 빈 채 저장되고 `Save`가 에셋을 지운다.
        var byChunk = new Dictionary<Vector2Int, List<WorldObject>>();
        foreach (var c in WorldContentAssets.AllChunks()) byChunk[c] = new List<WorldObject>();

        int outside = 0, fresh = 0, tilted = 0;
        int floating = 0;   // 지형이 밑에서 움직인 흔적
        var seen = new HashSet<string>();

        foreach (var go in found)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (src == null) { orphans.Add(go); continue; }

            Vector3 p = go.transform.position;
            var chunk = TerrainGrid.ChunkAt(p.x, p.z);
            if (!TerrainGrid.InWorld(chunk)) { outside++; continue; }

            var tag = go.GetComponent<WorldObjectTag>();
            if (tag == null) { tag = Undo.AddComponent<WorldObjectTag>(go); }

            // **신원은 유지하고, 없을 때만 발급해 씬에 되쓴다.** 굽을 때마다 새로 주면
            // `Persistent` 추적이 매번 처음 보는 것으로 여긴다.
            if (string.IsNullOrEmpty(tag.Id) || !seen.Add(tag.Id))
            {
                // 복제(Ctrl+D)하면 표식까지 복사되어 **신원이 겹친다** — 뒤에 온 쪽에 새로 준다.
                tag.Id = WorldContentAssets.NewId();
                seen.Add(tag.Id);
                fresh++;
                EditorUtility.SetDirty(tag);
            }

            // **지면에서 얼마나 떴나**를 잰다. 절대 Y로 구우면 지형을 다시 조각한 순간
            // 마을이 공중에 뜨거나 파묻힌다. 1cm 안쪽은 0으로 눌러 부동소수 잡음을 안 남긴다.
            float offset = p.y - TerrainHeight.At(p.x, p.z);
            if (Mathf.Abs(offset) < 0.01f) offset = 0f;

            // **지형이 물건 밑에서 움직였는지 센다**(2026-08-27). 저작 씬만 절대 Y를 들기
            // 때문에, 높이맵을 다시 구우면 물건이 옛 높이에 그대로 남아 뜨거나 파묻힌다.
            // 그 상태로 구우면 어긋난 만큼이 *"일부러 띄운 높이"*로 굳어 **게임에서도 뜬다** —
            // 한 번 구우면 원래 오프셋이 무엇이었는지 알 길이 없다. 그래서 **알린다.**
            if (Mathf.Abs(offset) > FloatWarn) floating++;

            if (!byChunk.TryGetValue(chunk, out var list))
                byChunk[chunk] = list = new List<WorldObject>();

            // **기울기는 담을 자리가 없다** — 조용히 되돌리지 말고 알린다.
            var e = go.transform.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(e.x, 0f)) > 0.5f ||
                Mathf.Abs(Mathf.DeltaAngle(e.z, 0f)) > 0.5f) tilted++;

            list.Add(new WorldObject
            {
                Id         = tag.Id,
                Name       = go.name,
                Kind       = tag.Kind,
                Prefab     = src,
                Position   = new Vector2(p.x, p.z),
                YOffset    = offset,
                Yaw        = Mathf.Repeat(e.y, 360f),
                Scale      = go.transform.localScale,
                Persistent = tag.Persistent,
                Generated  = tag.Generated,
                Spec       = tag.Spec,
                QuestId    = tag.QuestId,
            });
        }

        if (floating > 0 && floating * 4 > found.Count - outside)
            Debug.LogWarning(
                $"[굽기] 물건 {floating}개가 지면에서 {FloatWarn}m 넘게 떠 있다 — " +
                "지형을 다시 굽고 나서 **`6. Resnap Sites To Terrain`을 안 돌린 것**일 수 있다. " +
                "그대로 두면 이 어긋남이 *일부러 띄운 높이*로 구워져 게임에서도 뜬다. " +
                "일부러 띄운 것이라면 무시해도 된다.");

        // **세계 밖으로 나간 청크의 에셋을 지운다**(2026-08-27). `ChunksPerAxis`를 줄이면
        // 옛 청크 파일이 `Resources/`에 그대로 남고, `WorldSites`가 `LoadAll`로 그것까지 읽어
        // **지도 밖의 마을**이 살아 있는 것처럼 굴었다. 굽기는 세계 전체를 펼쳤을 때만
        // 도므로(`LoadedWholeWorld`) 여기가 지우기에 안전한 유일한 자리다.
        int outOfWorld = 0;
        foreach (var chunk in WorldContentAssets.AllChunks())
        {
            if (TerrainGrid.InWorld(chunk)) continue;
            AssetDatabase.DeleteAsset(WorldContentAssets.PathOf(chunk));
            byChunk.Remove(chunk);
            outOfWorld++;
        }

        int wrote = 0, cleared = 0;
        foreach (var kv in byChunk)
        {
            if (kv.Value.Count == 0) { if (WorldContentAssets.Load(kv.Key) != null) cleared++; }
            else wrote++;
            WorldContentAssets.Save(kv.Key, kv.Value);
        }
        WorldContentAssets.Flush();

        // 조각한 지형이 열려 있으면 델타도 같이 굽는다 — 저작자는 *"여기를 고친다"*고 생각하지
        // *"지형을 고친다 / 물건을 고친다"*로 나눠 생각하지 않는다.
        if (GameObject.Find("__TerrainEdit") != null) TerrainDeltaBaker.BakeAll();

        Debug.Log($"[굽기] 물건 {found.Count - outside}개를 청크 {wrote}개에 구웠다." +
                  (cleared > 0 ? $" **빈 청크 {cleared}개의 에셋을 지웠다.**" : "") +
                  (outOfWorld > 0 ? $" **세계 밖으로 나간 청크 {outOfWorld}개를 지웠다**(세계를 줄였다)." : "") +
                  (fresh > 0   ? $" 신원 {fresh}개를 새로 발급했다(복제하거나 새로 놓은 것)." : "") +
                  (outside > 0 ? $"\n**세계 밖 {outside}개는 안 구웠다.**" : "") +
                  (tilted > 0  ? $"\n**기울여 놓은 것 {tilted}개** — 수평 회전만 저장되므로 " +
                                 "게임에서는 똑바로 선다. 기울인 모양이 필요하면 프리팹에서 기울일 것." : "") +
                  (orphans.Count > 0
                      ? $"\n**프리팹 인스턴스가 아닌 것 {orphans.Count}개는 굽지 못했다** — " +
                        $"되짚을 원본이 없다. 첫 번째: `{orphans[0].name}`. " +
                        "프리팹으로 만들거나(`B. Save Selection As Site`) 지울 것."
                      : ""));
    }

    // ── 훑기 ────────────────────────────────────────────────────────────────

    // **프리팹 인스턴스 안으로는 안 내려간다.** 마을 프리팹 안에도 프리팹(건물·폰)이 들어
    // 있는데, 그것까지 물건으로 세면 **마을 하나가 수십 개로 쪼개져** 세계에 두 번 선다.
    // 프리팹이 아닌 것은 폴더로 보고 내려간다 — 하이어라키를 손으로 정리할 수 있어야 한다.
    public static void Collect(Transform t, List<GameObject> found, List<GameObject> orphans)
    {
        foreach (Transform child in t)
        {
            var go = child.gameObject;

            if (PrefabUtility.IsAnyPrefabInstanceRoot(go)) { found.Add(go); continue; }
            if (child.childCount == 0) { orphans.Add(go); continue; }

            Collect(child, found, orphans);
        }
    }

    // ── 거들기 ──────────────────────────────────────────────────────────────

    public static WorldAuthoringRoot FindRoot() =>
        UnityEngine.Object.FindFirstObjectByType<WorldAuthoringRoot>(FindObjectsInactive.Include);

    public static void NoRoot() =>
        Debug.LogError("[저작 씬] `WorldAuthoringRoot`를 못 찾았다 — `3. Open Authoring Scene` 먼저.");

    // **지형 프로필이 없으면 `YOffset`을 잴 수 없다.** 기본 프로필로 재면 지면 높이가 실제와
    // 달라, 구운 오프셋이 통째로 어긋난다(마을이 게임에서 공중에 뜬다). 그래서 **거절한다.**
    public static bool SyncTerrain(string what)
    {
        if (TerrainAuthoring.SyncValues() == null)
        {
            Debug.LogError($"[{what}] 씬에 `TerrainStreamer`가 없다 — 지형 프로필을 못 읽어 높이를 잴 수 없다.\n" +
                           "게임 씬(`Scene1`)을 **추가로** 열어 두고 다시 실행할 것.");
            return false;
        }
        TerrainHeight.ForgetDeltas();
        TerrainHeight.ForgetBiomes();
        return true;
    }
}
