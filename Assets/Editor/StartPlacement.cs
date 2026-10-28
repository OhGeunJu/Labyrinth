using UnityEditor;
using UnityEngine;

// **판이 시작되는 자리는 지도가 답한다 — 씬이 아니다**(2026-09-15).
//
// 답이 두 벌이었다. `WorldMapData.StartNode`(기본 8,8 — 지도 한복판)가 *시작 칸*이라고
// 말하는데, **실제로 판이 열리는 자리는 씬에 폰이 서 있는 곳**이다. 둘이 어긋나 있었고,
// 어긋난 것을 알려 주는 것이 아무 데도 없었다.
//
// ## 어떻게 드러났나
//
// 사용자가 사흘 동안 *"광맥이 안 보인다"*고 했다. 렌더링도 뿌리기도 멀쩡했다 —
// 뿌리기 결산이 **광맥 서른셋이 섰고 렌더러 서른넷이 전부 그려진다**고 답했다.
// 드러난 것은 **첫 자리의 좌표**였다: `(45, 0, 14)` · `(59, 0, 56)` · `(85, 0, 24)` —
// 전부 청크 (0,0) 언저리, 즉 **세계의 남서쪽 구석**이다. 시작 칸 (8,8)은 월드로
// 512~576m인데 폰은 0~64m에 서 있었다.
//
// 그 구석이 하필 가장 나쁜 자리였다:
//
//   · 바이옴이 **동굴**이다(그림의 마지막 줄 첫 글자 `C`). 풀이 `돌·구리·주석`뿐이라
//     **석탄이 안 난다** — 1층 석탄은 폐광에서만 난다.
//   · **식물이 하나도 안 난다.** 갓버섯·거대버섯은 균사림, 이끼·곰팡이·이끼 낀 나무는
//     이끼굴, 물먹은 둥치는 지저늪(2층)이라 **여섯 규칙이 전부 바이옴에서 걸러졌다.**
//     결산에 `0개`가 여섯 줄이었다.
//   · **두 면이 세계 밖이다**("세계 밖은 바위다") — 그래서 바위에 걸려 버려지는 비율이
//     다른 데보다 높다(114/150 = 76%).
//
// 사용자가 1층을 그리면서 *"시작칸쪽을 폐광으로 폐광을 더 크게 그려"*라고 한 것이
// 바로 이 자리를 정한 것이었는데, **게임은 그 그림을 안 보고 있었다.**
//
// ## 왜 씬을 고치지 않고 도구를 두나
//
// 씬이 값을 들면 코드 기본값은 힘이 없다 — 그러니 고칠 곳은 씬이다. 그런데 **씬을 밖에서
// 고치면 유니티가 저장 한 번에 되돌린다**(그리고 이 씬은 바이너리 직렬화다). 그래서
// 도구가 씬 안에서 옮긴다.
//
// **`0. Setup World Streaming`에 얹지 않는다.** 그쪽은 `1. Rebuild Underworld`가 단계로
// 부르는 상시 도구라, 거기 넣으면 **묶음을 돌릴 때마다 무리가 제자리로 끌려온다** —
// 그게 바로 `WireScatter`가 뿌리기 규칙을 매번 비우던 그 함정이다(*"일회성 이행을 상시
// 도구에 남기면 그것이 규칙이 된다"*). 대신 **묶음은 어긋났을 때 경고만 한다**
// (`WorldStreamingSetup.WarnStartTile`) — 옮기는 것은 사람이 누른다.
//
// ## 어기면 조용히 틀리는 것
//
// - **무리를 한 덩어리로 옮긴다.** 하나씩 시작 칸 한복판에 모으면 **서로 겹쳐 서고**
//   손으로 맞춰 둔 대형이 사라진다. 옮기는 것은 **무게중심 하나**이고 나머지는 그
//   차이만큼 따라간다.
// - **카메라도 같이 옮긴다.** `CameraController`는 폰을 안 따라간다(`Start`가 각도만
//   읽는다) — 폰만 옮기면 **판이 열릴 때 빈 바위를 보고 있다.**
// - **y는 함수가 답한다**(`TerrainHeight.At`). 옛 y를 그대로 들고 가면 바닥이 다른
//   자리에서 묻히거나 뜬다.
// - **저작 씬의 폰은 안 건드린다**(`WorldAuthoringRoot` 아래) — 거기 있는 것은 세계
//   저작물이지 내 무리가 아니다.
public static class StartPlacement
{
    // **옮겼으면 되돌릴 길이 있어야 한다**(2026-09-15에 배웠다). 옮겨 놓고 사용자가
    // *"폰들 아까 있던 칸으로 다시 옮기고"*라고 했는데, Undo는 씬을 저장하고 나면 못
    // 넘어간다 — **되돌리는 도구가 되돌린 것을 못 남기면 도구가 아니다**(셰이더 목록에서
    // 같은 것을 한 번 배웠다). 그래서 옮긴 만큼을 프로젝트에 적어 둔다.
    //
    // **씬이 아니라 `EditorPrefs`에 적는다** — 씬에 적으면 되돌리기 위한 값이 저작물에
    // 섞여 들어가고, 다음에 여는 사람이 *"이 숫자는 뭐지"*를 잰다.
    //
    // **차이값이 아니라 있던 자리를 통째로 적는다**(2026-09-15에 틀렸다). 처음에는
    // *얼마나 밀었나*만 적었는데, 그러면 **두 번 누르거나 사이에 Undo가 끼면 어긋난다** —
    // 되돌린 자리가 원래 자리가 아니게 되고, 화면에는 *"C2를 눌렀는데 그대로다"*로만
    // 보인다(실제로 그랬다). 있던 자리를 적으면 **몇 번을 눌러도 같은 자리로 간다.**
    const string BackKey = "DungeonColony.StartPlacement.Back";

    // `이름|x|y|z` 를 줄바꿈으로 이어 적는다. **이름으로 되찾는다** — 인스턴스 id는
    // 유니티를 껐다 켜면 달라져서 다음 날 되돌릴 수가 없다.
    static string Snapshot(Transform[] ts)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var t in ts)
        {
            var p = t.position;
            sb.Append($"{t.name}|{p.x}|{p.y}|{p.z}").Append('~');
        }
        return sb.ToString();
    }
    [MenuItem("DungeonColony/World/C. Move Party To Start Tile (시작 칸으로)")]
    static void Move()
    {
        if (!Read(out var start, out var target, out var biome)) return;

        var pawns = PlayerPawns();
        if (pawns.Length == 0)
        {
            Debug.LogWarning("[시작칸] 씬에 플레이어 세력 폰이 없다 — 옮길 것이 없다. " +
                             "`Faction` 컴포넌트에 SO가 안 물려 있으면 여기서도 안 잡힌다.");
            return;
        }

        var from = Centroid(pawns);
        var off  = new Vector3(target.x - from.x, 0f, target.z - from.z);

        if (off.magnitude < 1f)
        {
            Debug.Log($"[시작칸] 이미 시작 칸 ({start.x},{start.y})에 있다 — 아무것도 안 옮겼다.");
            return;
        }

        var cams = Cameras();

        // **옮기기 전에 찍는다** — 옮기고 나서 찍으면 되돌릴 자리가 새 자리다.
        var shot = new System.Collections.Generic.List<Transform>(pawns);
        shot.AddRange(cams);
        EditorPrefs.SetString(BackKey, Snapshot(shot.ToArray()));

        Undo.SetCurrentGroupName("시작 칸으로 옮기기");
        foreach (var t in pawns) Undo.RecordObject(t, "시작 칸으로");
        foreach (var t in cams)  Undo.RecordObject(t, "시작 칸으로");

        foreach (var t in pawns)
        {
            var p = t.position + off;
            p.y = TerrainHeight.At(p.x, p.z);          // **함수가 답한다** — 옛 y는 저 자리의 것이다
            t.position = p;
        }
        // 카메라는 지면에 안 붙는다 — 보던 각도와 높이를 그대로 들고 옆으로만 간다.
        foreach (var t in cams) t.position += off;

        EditorSceneMarkDirty(pawns[0]);

        Debug.Log($"[시작칸] 무리 {pawns.Length}명 · 카메라 {cams.Length}대를 " +
                  $"({from.x:F0}, {from.z:F0}) → ({target.x:F0}, {target.z:F0})로 옮겼다 " +
                  $"— {off.magnitude:F0}m. 시작 칸 ({start.x},{start.y}) = **{biome}**.\n" +
                  "씬을 저장해야 남는다.");
    }

    [MenuItem("DungeonColony/World/C2. Move Party Back (옮기기 전으로)")]
    static void Back()
    {
        string raw = EditorPrefs.GetString(BackKey, "");
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[시작칸] 되돌릴 기록이 없다 — `C. Move Party To Start Tile`을 " +
                             "이 기계에서 돌린 적이 없다.");
            return;
        }

        var want = new System.Collections.Generic.Dictionary<string, Vector3>();
        foreach (var row in raw.Split('~'))
        {
            var f = row.Split('|');
            if (f.Length != 4) continue;
            if (float.TryParse(f[1], out float x) && float.TryParse(f[2], out float y) &&
                float.TryParse(f[3], out float z))
                want[f[0]] = new Vector3(x, y, z);
        }

        var all = new System.Collections.Generic.List<Transform>(PlayerPawns());
        all.AddRange(Cameras());

        int moved = 0, missed = 0;
        foreach (var t in all)
        {
            if (!want.TryGetValue(t.name, out var p)) { missed++; continue; }
            Undo.RecordObject(t, "옮기기 전으로");
            t.position = p;
            moved++;
        }

        if (moved == 0)
        {
            Debug.LogWarning("[시작칸] 기록의 이름과 맞는 것이 씬에 없다 — 이름이 바뀌었다.");
            return;
        }

        // **기록을 안 지운다** — 절대 좌표라 여러 번 눌러도 같은 자리이고, 지우면
        // *한 번만 되돌릴 수 있는 도구*가 된다(그게 차이값 시절의 다른 얼굴이다).
        EditorSceneMarkDirty(all[0]);

        var now = Centroid(PlayerPawns());
        Debug.Log($"[시작칸] 있던 자리로 되돌렸다 — {moved}개" +
                  (missed > 0 ? $" (이름이 안 맞아 건너뛴 것 {missed}개)" : "") +
                  $". 무리 무게중심 ({now.x:F0}, {now.z:F0}). **씬을 저장해야 남는다.**");
    }

    // 지도가 말하는 시작 자리. **칸 한복판을 겨눈다** — 칸 모서리는 이웃 바이옴과 맞닿아
    // 있어서, 도메인 워프가 자리를 굽히는 순간 옆 지역으로 넘어갈 수 있다.
    public static bool Read(out Vector2Int start, out Vector3 target, out Biome biome)
    {
        start = default; target = default; biome = default;

        var map = Object.FindFirstObjectByType<WorldMap>(FindObjectsInactive.Include);
        var data = map != null
                 ? new SerializedObject(map).FindProperty("mapData")?.objectReferenceValue as WorldMapData
                 : null;
        if (data == null)
        {
            Debug.LogWarning("[시작칸] 씬의 `WorldMap`에 `mapData`가 안 물려 있다 — " +
                             "시작 칸을 물을 데가 없다.");
            return false;
        }

        start  = data.StartNode;
        float r = TerrainGrid.RegionSize;
        target = new Vector3((start.x + 0.5f) * r, 0f, (start.y + 0.5f) * r);
        biome  = data.Get(start.x, start.y).Biome;
        return true;
    }

    // **플레이어 세력 폰만 센다.** 적·중립까지 옮기면 세계에 저작해 둔 무리가 따라온다.
    public static Transform[] PlayerPawns()
    {
        var list = new System.Collections.Generic.List<Transform>();
        foreach (var b in Object.FindObjectsByType<UnitBrain>(FindObjectsInactive.Include,
                                                              FindObjectsSortMode.None))
        {
            if (b.GetComponentInParent<WorldAuthoringRoot>() != null) continue;
            var f = b.GetComponent<Faction>();
            if (f == null || !f.IsPlayer) continue;
            list.Add(b.transform);
        }
        return list.ToArray();
    }

    static Transform[] Cameras()
    {
        var list = new System.Collections.Generic.List<Transform>();
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None))
        {
            if (c.GetComponentInParent<WorldAuthoringRoot>() != null) continue;
            // 초상화용 카메라는 폰에 붙어 따라다닌다 — 따로 옮기면 두 번 옮긴다.
            if (c.GetComponentInParent<UnitBrain>() != null) continue;
            list.Add(c.transform);
        }
        return list.ToArray();
    }

    public static Vector3 Centroid(Transform[] ts)
    {
        var sum = Vector3.zero;
        foreach (var t in ts) sum += t.position;
        return sum / ts.Length;
    }

    static void EditorSceneMarkDirty(Transform any) =>
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(any.gameObject.scene);
}
