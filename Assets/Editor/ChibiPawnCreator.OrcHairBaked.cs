using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 오크 머리카락 · 수염(2026-11-08, 사용자: *"13 ㄱㄱ"* — 사람 머리카락 `Pawn/13`과 같은 길). 미리보기(`Tools/ModelLab/orc/orc_parts.py` ·
// `orc_female/hair_v2.py`)가 확인받은 모양 그대로 파이썬이 메시를 뽑고(`Tools/ModelLab/orc/bake_orc_hair.py` → `orc/baked/` · 깃 밖),
// 여기서는 읽어 다듬어(펴기 · 줄이기 · 선 법선 — 사람과 같은 `ImportHair`) 메시 에셋으로 굽기만 한다. 수식을 C#으로 손으로 옮기지 않는다.
//
// - 좌표는 C# 오크 머리와 같은 거리장 좌표 — 변환 없이 놓인다(`OrcSculpt`가 `p => p`).
// - 이름(렌더러 = 고르기 축 · 메시)은 옛 C# 수식 판과 같다 — 남자 `Head.Pick.Hair.*@Hair.!Bald` · `Head.Pick.Beard.*@Beard.!None` · 장식 `.Cuff` ·
//   민 층 `.Buzz` / 여자 `Head.Pick.Hair.*` · `.Leather` · `.Metal` · `.Buzz` · `.Short`(색은 `MarkingLook`이 조각 이름으로 칠한다).
// - **목록이 없으면 옛 C# 수식으로 짓는다**(`OrcHeadParts` · `OrcFemaleHeadParts`) — 뽑기를 안 돌린 체크아웃에서도 `Pawn/3`이 돈다.
// - `Pawn/14`는 메시만 다시 굽는다(같은 에셋을 덮어 프리팹은 그대로 따라온다). 머리 목록이 바뀌었으면 `Pawn/3`(부위가 바뀐다).
public static partial class ChibiPawnCreator
{
    const string OrcHairDir = "Tools/ModelLab/orc/baked";

    [Serializable] class OrcHairItem { public string mesh, part, file, mat, sex; public bool relax; public float keep; }
    [Serializable] class OrcHairManifest { public List<OrcHairItem> items; }

    static OrcHairManifest LoadOrcHairManifest()
    {
        string path = Path.Combine(OrcHairDir, "manifest.json");
        if (!File.Exists(path)) return null;
        return JsonUtility.FromJson<OrcHairManifest>(File.ReadAllText(path));
    }

    // 이 성별의 오크 머리카락 · 수염 부위(구운 메시 에셋). 목록이 없거나 메시가 하나도 없으면 null — 부르는 쪽이 옛 C# 수식으로 짓는다.
    // `firstOn` — 이 이름으로 시작하는 머리 한 벌만 켜 둔다(프리팹 · 씬 뷰 — 판에서는 씨앗이 고른다).
    static List<Part> OrcBakedHairParts(string sex, Vector3 pos, string firstOn = null)
    {
        var man = LoadOrcHairManifest();
        if (man == null || man.items == null) return null;
        var parts = new List<Part>();
        int missing = 0;
        foreach (var it in man.items)
        {
            if (it.sex != sex) continue;
            string path = $"{MeshDir}/{it.mesh}.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m == null) { missing++; continue; }
            _saved.Add(path);   // 끝 청소가 안 지우게
            bool flat = it.part.Contains(".Buzz") || it.part.Contains(".Short");   // 민 층 · 짧은 머리 — 살에 붙은 얇은 층이라 선을 안 긋는다(옛 판과 같다)
            bool on = firstOn != null && (it.part == firstOn || it.part.StartsWith(firstOn + ".") || it.part.StartsWith(firstOn + "@"));
            parts.Add(new Part(it.part, m, pos, Vector3.one, it.mat, on, outerMat: flat ? null : HangLine));
        }
        if (missing > 0) Debug.LogWarning($"[오크 머리카락] {sex} — 구운 메시가 {missing}개 없다(`Pawn/14`를 먼저 돌릴 것).");
        return parts.Count > 0 ? parts : null;
    }

    [MenuItem("DungeonColony/Pawn/14. Import Orc Hair (오크 머리카락 · 수염 메시만 — 프리팹은 그대로 따라온다)")]
    public static void ImportOrcHair()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var man = LoadOrcHairManifest();
        if (man == null) { Debug.LogError($"[오크 머리카락] 목록이 없다 — {OrcHairDir}/manifest.json (`python Tools/ModelLab/orc/bake_orc_hair.py male` · `female`을 먼저 돌릴 것)"); return; }
        int n = 0;
        try
        {
            for (int i = 0; i < man.items.Count; i++)
            {
                var it = man.items[i];
                if (EditorUtility.DisplayCancelableProgressBar("오크 머리카락", it.mesh, i / (float)man.items.Count)) break;
                var hi = new HairItem { mesh = it.mesh, part = it.part, file = it.file, mat = it.mat, gender = it.sex, fit = PawnLook.FitRound, relax = it.relax, keep = it.keep };
                if (ImportHair(hi, OrcHairDir) != null) n++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.SaveAssets();
        Debug.Log($"[오크 머리카락] 메시 {n}/{man.items.Count}개 · {watch.Elapsed.TotalSeconds:F0}초. 머리 목록이 바뀌었으면 `Pawn/3`을 돌릴 것.");
    }
}
