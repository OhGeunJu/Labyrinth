using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

// 사람 머리카락(2026-11-08, 사용자: *"파이썬 메시 뽑기"*) — 미리보기(`Tools/ModelLab/human/port.py`)가 확인받은 모양 그대로 메시를 뽑고
// (`bake_hair.py` → `Tools/ModelLab/human/baked/` · 깃 밖), 여기서는 읽어 다듬고(펴기 · 줄이기 · 선 법선) 메시 에셋으로 굽기만 한다.
// 수식을 C#으로 손으로 옮기지 않는다 — 옮기면 확인받은 모양과 어긋난다.
//
// - **성별 둘 × 윗머리 모양 둘**(둥근 = 넓은 부드러운 · 네모 = 넓은 네모)만 굽고 나머지 머리에는 늘려 씌운다(`@Fit.` — `PawnLook.FitAxis`).
//   네모 판은 넓은 네모 머리에 맞춰 뽑았으므로 기준 머리 틀에 놓을 때 (기준 ÷ 넓은 네모)로 줄여 둔다 — `PawnLook`이 고른 머리로 옮기면
//   넓은 네모 머리에서 제 크기가 된다.
// - 이름(렌더러 = 고르기 축)은 목록(`manifest.json`)이 든다 — 머리카락 `Head.Pick.Hair.*` · 앞머리 `Head.Pick.Bangs.*`(앞머리 없음 몫은 `HumanBangsNone`).
// - **`Pawn/13`은 메시만 다시 굽는다**(같은 에셋을 덮어 프리팹은 그대로 따라온다). 머리 목록이 바뀌었으면 `Pawn/3`(부위가 바뀐다).
public static partial class ChibiPawnCreator
{
    const string HumanHairDir = "Tools/ModelLab/human/baked";
    const float HumanBangsNone = 0.20f;
    const int   HairMaxVerts   = 9000;    // 머리카락 조각 한 벌의 점 수 — 줄이기의 목표(`ImportHair`)   // 앞머리를 얹는 머리에서 앞머리 없음 몫(11-08 — 임시 · 사용자가 따로 안 정했다)

    [Serializable] class HairItem { public string mesh, part, file, mat, gender, fit; public bool relax; public float keep; }
    [Serializable] class HairManifest { public List<HairItem> items; }

    static HairManifest LoadHairManifest()
    {
        string path = Path.Combine(HumanHairDir, "manifest.json");
        if (!File.Exists(path)) { Debug.LogError($"[사람 머리카락] 목록이 없다 — {path} (`python Tools/ModelLab/human/bake_hair.py`를 먼저 돌릴 것)"); return null; }
        return JsonUtility.FromJson<HairManifest>(File.ReadAllText(path));
    }

    // 네모 판을 기준 머리 틀에 놓을 크기(기준 ÷ 넓은 네모).
    static Vector3 HairFitScale(string gender, string fit)
    {
        if (fit != PawnLook.FitBox) return Vector3.one;
        var basis = BasisOf(gender);
        var sq = Array.Find(Heads, x => x.Gender == gender && x.Name.EndsWith("BroadSquare"));
        if (sq.Name == null) return Vector3.one;
        Vector3 b = HumanSize(basis), q = HumanSize(sq);
        return new Vector3(b.x / q.x, b.y / q.y, b.z / q.z);
    }

    // 뽑은 파일 하나 → 다듬은 메시 에셋.
    // `dir` — 뽑은 파일 폴더(오크 `Pawn/14`도 같은 길로 들인다 · `ChibiPawnCreator.OrcHairBaked`).
    static Mesh ImportHair(HairItem it, string dir = HumanHairDir)
    {
        string path = Path.Combine(dir, it.file);
        if (!File.Exists(path)) { Debug.LogWarning($"[사람 머리카락] 파일이 없다 — {path}"); return null; }
        Vector3[] pts; var tris = new List<int>();
        using (var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
        using (var br = new BinaryReader(gz))
        {
            int nv = br.ReadInt32(), nt = br.ReadInt32();
            pts = new Vector3[nv];
            for (int i = 0; i < nv; i++) pts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < nv; i++) { br.ReadSingle(); br.ReadSingle(); br.ReadSingle(); }   // 파이썬 법선 — 펴고 줄인 뒤 면에서 다시 잰다
            tris.Capacity = nt * 3;
            for (int i = 0; i < nt * 3; i++) tris.Add(br.ReadInt32());
        }
        if (it.relax) { var vl = new List<Vector3>(pts); Relax(vl, tris, 6); pts = vl.ToArray(); }
        // 남길 점 — 몫(`keep`)과 한 벌의 점 수(`HairMaxVerts`) 중 작은 쪽. 긴 머리는 뽑은 점이 14만이라 몫만으로는 5만이 남았다
        // (사람 치비 몸 하나가 1만 언저리).
        int target = Mathf.Min(Mathf.RoundToInt(pts.Length * it.keep), HairMaxVerts);
        if (target < pts.Length) pts = Decimate(pts, tris, Mathf.Max(64, target));
        var normals = FaceNormals(pts, tris);
        var colors = new List<Color>(pts.Length);
        for (int i = 0; i < pts.Length; i++) colors.Add(Color.white);
        var m = SaveSkull(it.mesh, new Surface(new List<Vector3>(pts), new List<Vector3>(normals), colors, tris));
        var sc = HairFitScale(it.gender, it.fit);
        OutlineNormals(m, OutlineSmoothRadius / Mathf.Max(sc.x, Mathf.Max(sc.y, sc.z)));
        EditorUtility.SetDirty(m);
        return m;
    }

    // 넓이 가중 면 법선의 평균(뽑은 면은 바깥을 본다 — `surface_nets.py`가 기울기로 맞췄다).
    static Vector3[] FaceNormals(Vector3[] v, List<int> t)
    {
        var n = new Vector3[v.Length];
        for (int i = 0; i < t.Count; i += 3)
        {
            int a = t[i], b = t[i + 1], c = t[i + 2];
            var f = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            n[a] += f; n[b] += f; n[c] += f;
        }
        for (int i = 0; i < n.Length; i++) n[i] = n[i].sqrMagnitude > 1e-20f ? n[i].normalized : Vector3.up;
        return n;
    }

    // 이 성별의 머리카락 부위 — 구운 메시 에셋을 읽는다(없으면 `Pawn/13` 먼저).
    static List<Part> HumanHairParts(string gender)
    {
        var parts = new List<Part>();
        var man = LoadHairManifest();
        if (man == null) return parts;
        var pos = HumanHeadPos(BasisOf(gender));
        int missing = 0;
        foreach (var it in man.items)
        {
            if (it.gender != gender) continue;
            string path = $"{MeshDir}/{it.mesh}.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m == null) { missing++; continue; }
            _saved.Add(path);   // 끝 청소가 안 지우게
            bool on = it.part.StartsWith("Head.Pick.Hair.Pomp@") && it.fit == PawnLook.FitRound;
            parts.Add(new Part(it.part, m, pos, HairFitScale(gender, it.fit), it.mat, on, outerMat: "OutlineOuterFine"));
        }
        if (missing > 0) Debug.LogWarning($"[사람 머리카락] {gender} — 구운 메시가 {missing}개 없다(`Pawn/13`을 먼저 돌릴 것).");
        return parts;
    }

    // 사람 폰 — 앞머리 없음 몫 · 털 색(`MarkingLook`). `Dress`가 앞머리가 있는 몸에 부른다.
    static void HumanHairLook(GameObject root)
    {
        if (!root.TryGetComponent(out PawnLook look)) return;
        look.SetPickChances(new[] { new PawnLook.PickChance(PawnLook.PickBangs, "None", HumanBangsNone) });
        if (!root.TryGetComponent(out MarkingLook _)) root.AddComponent<MarkingLook>();
    }

    [MenuItem("DungeonColony/Pawn/13. Import Human Hair (사람 머리카락 메시만 — 프리팹은 그대로 따라온다)")]
    public static void ImportHumanHair()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var man = LoadHairManifest();
        if (man == null) return;
        Heads = MakeHeads(ChibiFaceLayout.LoadOrCreate());
        int n = 0;
        try
        {
            for (int i = 0; i < man.items.Count; i++)
            {
                var it = man.items[i];
                if (EditorUtility.DisplayCancelableProgressBar("사람 머리카락", it.mesh, i / (float)man.items.Count)) break;
                if (ImportHair(it) != null) n++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.SaveAssets();
        Debug.Log($"[사람 머리카락] 메시 {n}/{man.items.Count}개 · {watch.Elapsed.TotalSeconds:F0}초. 머리 목록이 바뀌었으면 `Pawn/3`을 돌릴 것.");
    }
}
