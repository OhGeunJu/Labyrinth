using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **지형을 다시 구우면 저작 씬의 물건이 뒤에 남는다**(2026-08-27).
//
// 구운 데이터는 높이를 **안 들고 있다** — `WorldObject`는 XZ + `YOffset`뿐이고, 게임에서 설 때
// `TerrainHeight.At(x, z) + YOffset`으로 자리를 낸다. 그래서 **런타임은 지형을 따라간다.**
//
// **저작 씬만 절대 Y를 든다.** 트랜스폼이 자리의 단일 출처이고(표식에 또 적으면 끌어 옮겼을 때
// 둘이 갈린다), 트랜스폼은 지형이 바뀐 것을 모른다. 침식 높이맵을 새로 구우면 땅이 수십~수백 m
// 움직이는데 마을은 옛 높이에 그대로 서서 **공중에 뜨거나 파묻힌다.**
//
// ── 진짜 위험한 것은 화면이 아니다 ──────────────────────────────────────────
//
// 눈에 보이는 것은 떠 있는 마을이지만, **그 상태로 `5. Bake World`를 돌리면 데이터가 오염된다** —
// 굽기는 `YOffset = 현재 Y − 현재 지면`으로 재므로, 어긋난 만큼이 *"저작자가 일부러 띄운 높이"*로
// 구워져 **게임에서도 뜬다.** 한 번 구우면 원래 오프셋이 무엇이었는지 알 길이 없다.
//
// 그래서 이 도구는 **마지막으로 구운 오프셋을 되읽어** 지금 지형 위에 다시 앉힌다.
// 청크 에셋이 그 기록이다.
//
// ── 높이만이 아니다 ─────────────────────────────────────────────────────────
//
// 자동 배치는 *"둘레에서 가장 평평한 데"*를 찾아 마을을 앉혔다. 지형이 바뀌면 **그 자리가 더 이상
// 평평하지 않다** — 마을이 절벽에 걸린다. 그래서 **도구가 낸 것만**(`Generated`) 다시 평평한 데를
// 찾아 옮길 수 있게 둔다. **손으로 놓은 것은 안 건드린다** — 그 자리에 놓은 것이 곧 저작이다.
public class WorldResnap : EditorWindow
{
    bool  _reflatten = true;
    float _maxSlope  = 6f;     // `WorldPopulator`와 같은 눈금
    bool  _dryRun;

    [MenuItem("DungeonColony/World/6. Resnap Sites To Terrain")]
    static void Open()
    {
        var w = GetWindow<WorldResnap>("Resnap");
        w.minSize = new Vector2(380f, 300f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("지형이 바뀐 뒤 물건을 다시 앉힌다", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "구운 데이터는 높이를 안 들고 XZ + 오프셋만 든다. 저작 씬만 절대 Y를 들어서 " +
            "지형을 다시 구우면 뒤에 남는다.", EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(6f);
        _reflatten = EditorGUILayout.ToggleLeft(
            "비탈에 선 것은 평평한 데로 옮긴다 (자동 배치한 것만)", _reflatten);
        using (new EditorGUI.DisabledScope(!_reflatten))
            _maxSlope = EditorGUILayout.Slider("허용 경사(m)", _maxSlope, 1f, 30f);

        EditorGUILayout.Space(4f);
        _dryRun = EditorGUILayout.ToggleLeft("세어만 본다 (안 고친다)", _dryRun);

        EditorGUILayout.Space(10f);
        if (GUILayout.Button(_dryRun ? "세어 본다" : "다시 앉힌다", GUILayout.Height(28f))) Run();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "**`5. Bake World`보다 먼저 돌릴 것.** 어긋난 채로 구우면 그 어긋남이 " +
            "*일부러 띄운 높이*로 구워져 게임에서도 뜬다 — 그러고 나면 되돌릴 수 없다.\n\n" +
            "굽힌 적 없는 것(손으로 방금 놓은 것)은 안 건드린다 — 지금 지형 위에 놓았을 테니 이미 맞다.",
            MessageType.Warning);
    }

    // ── 돌리기 ──────────────────────────────────────────────────────────────

    void Run()
    {
        var root = WorldAuthoringScene.FindRoot();
        if (root == null) { WorldAuthoringScene.NoRoot(); return; }
        if (!WorldAuthoringScene.SyncTerrain("재배치")) return;

        // **마지막으로 구운 오프셋** — 이것이 *"저작자가 얼마나 띄웠나"*의 유일한 기록이다.
        // 지금 씬의 Y에서 역산하면 어긋난 값을 그대로 정답으로 굳힌다.
        var offsets = new Dictionary<string, float>();
        foreach (var chunk in WorldContentAssets.AllChunks())
            foreach (var o in WorldContentAssets.Read(chunk))
                if (!string.IsNullOrEmpty(o.Id)) offsets[o.Id] = o.YOffset;

        var found   = new List<GameObject>();
        var orphans = new List<GameObject>();
        WorldAuthoringScene.Collect(root.transform, found, orphans);

        int moved = 0, flattened = 0, unknown = 0, steep = 0;
        float worst = 0f;

        foreach (var go in found)
        {
            var tag = go.GetComponent<WorldObjectTag>();
            if (tag == null || string.IsNullOrEmpty(tag.Id)) { unknown++; continue; }

            // **굽힌 적 없는 것은 안 건드린다** — 지금 지형 위에 놓았을 테니 이미 맞고,
            // 0으로 눌러 버리면 일부러 띄워 둔 것이 땅에 박힌다.
            if (!offsets.TryGetValue(tag.Id, out float offset)) { unknown++; continue; }

            var p = go.transform.position;
            var xz = new Vector2(p.x, p.z);

            // ── 비탈에 걸렸으면 옮긴다 ──────────────────────────────────────
            if (_reflatten && tag.Generated && tag.Kind != WorldNodeContent.Empty)
            {
                float foot = Footprint(tag.Kind);
                if (Slope(xz, foot) > _maxSlope)
                {
                    steep++;

                    // **시드는 신원에서 낸다** — 실행마다 달라지면 다시 돌릴 때마다 마을이
                    // 걸어 다닌다. `string.GetHashCode`는 실행마다 달라져 못 쓴다.
                    var rng = new System.Random(StableHash(tag.Id));
                    var better = Flattest(xz, foot, rng);
                    if (Slope(better, foot) < Slope(xz, foot))
                    {
                        xz = better;
                        flattened++;
                    }
                }
            }

            float y = TerrainHeight.At(xz.x, xz.y) + offset;
            var want = new Vector3(xz.x, y, xz.y);

            float d = (want - p).magnitude;
            if (d < 0.02f) continue;

            worst = Mathf.Max(worst, Mathf.Abs(want.y - p.y));
            moved++;

            if (!_dryRun)
            {
                Undo.RecordObject(go.transform, "Resnap Site");
                go.transform.position = want;
                EditorUtility.SetDirty(go.transform);
            }
        }

        string head = _dryRun ? "[재배치·세어봄]" : "[재배치]";
        Debug.Log($"{head} 물건 {found.Count}개 중 **{moved}개**를 다시 앉혔다 " +
                  $"(가장 크게 어긋난 것 {worst:0.#}m). " +
                  (_reflatten ? $"비탈에 걸린 것 {steep}개 중 {flattened}개를 평평한 데로 옮겼다. " : "") +
                  (unknown > 0 ? $"굽힌 적 없는 {unknown}개는 안 건드렸다." : ""));

        if (!_dryRun && moved > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log("[재배치] 이제 `5. Bake World`를 돌리면 지금 지형 기준으로 다시 구워진다.");
        }
    }

    // ── 자리 재기 — `WorldPopulator`와 **같은 눈금**이어야 한다 ─────────────
    //
    // 갈리면 배치가 통과시킨 자리를 재배치가 비탈이라고 하고, 그 반대도 생긴다.

    static float Footprint(WorldNodeContent kind) => kind switch
    {
        WorldNodeContent.Town      => 20f,
        WorldNodeContent.EnemyLair => 12f,
        _                          => 8f,
    };

    static float Slope(Vector2 p, float d)
    {
        float a = TerrainHeight.Procedural(p.x - d, p.y - d);
        float b = TerrainHeight.Procedural(p.x + d, p.y - d);
        float c = TerrainHeight.Procedural(p.x - d, p.y + d);
        float e = TerrainHeight.Procedural(p.x + d, p.y + d);
        return Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, e)) - Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, e));
    }

    static Vector2 Flattest(Vector2 around, float foot, System.Random rng)
    {
        const int   Tries  = 16;
        const float Radius = 160f;

        Vector2 best = around;
        float bestSlope = Slope(around, foot);

        for (int i = 0; i < Tries; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt((float)rng.NextDouble()) * Radius;
            var p = new Vector2(around.x + Mathf.Cos(a) * r, around.y + Mathf.Sin(a) * r);

            if (p.x < 0f || p.y < 0f || p.x >= TerrainGrid.WorldSize || p.y >= TerrainGrid.WorldSize)
                continue;

            float s = Slope(p, foot);
            if (s < bestSlope) { bestSlope = s; best = p; }
        }
        return best;
    }

    // **`string.GetHashCode`를 안 쓴다** — 실행마다 달라져 다시 돌릴 때마다 다른 자리가 나온다.
    static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
            return h;
        }
    }
}
