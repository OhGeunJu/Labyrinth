using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **사람 이름을 성별로 가른다 — 한 번만 돈다**(2026-09-20, 몸에 성별이 생긴 날).
//
// `Human.asset`의 이름 110개가 한 풀에 섞여 있어 여자 몸에 `던컨`이 붙었다. 성별이 뚜렷한 이름을 `MaleNames`·`FemaleNames`로
// 옮기고, 가리지 않는 이름은 `NamePool`에 남긴다(고르는 풀 = 그 성별 + 중성 — `UnitData.NamesFor`).
//
// **일회성 이행이다 — 이미 갈라져 있으면 아무것도 안 한다.** 상시 도구로 남은 이행은 규칙이 된다(CLAUDE.md
// `0d. Swap Wood Costs To Stone`). 가른 뒤로는 인스펙터에서 손으로 고친다 — 이 표가 다시 덮으면 안 된다.
// 어느 이름이 어느 쪽인가는 **제안**이다(판타지 이름이라 뚜렷하지 않은 것은 중성에 뒀다).
public static class HumanNameSplit
{
    const string Asset = "Assets/Resources/Data/Units/Human.asset";

    static readonly string[] Male   = { "아르반", "도린", "가레스", "테오", "브란", "케일", "로안", "드레이크", "카이런", "로덴", "하웰", "오르막", "루칸", "이든", "발렌", "코르윈", "라이언", "홀든", "오스릭", "바르텔", "던컨", "타르윈", "에드릭", "라울", "고드윈", "말론", "세드릭", "파비안", "노른", "하르트", "아론", "벤딕", "자한", "오웬", "크로우", "발드", "하비", "하렘", "조나스", "미르", "셀림", "바트", "칸토", "데르", "굴람", "파샤", "노먼", "자디드", "마르코", "오스만", "티모", "카림", "요한", "다르윈", "유수프", "보르", "하킴" };
    static readonly string[] Female = { "세라", "릴라", "미라", "이슬라", "유나", "하니", "엘사", "비앙카", "셀린", "다이애", "노라", "아델", "그웬", "페트라", "마린", "델피", "니케", "예린", "모니카", "소피", "베라", "안나", "이레네", "카타", "실비", "미르카", "클로에", "리네", "테사", "니아", "에린", "로자", "이슈타르", "레베카", "사프란", "베냐", "리사", "아그네스", "니콜" };

    [MenuItem("DungeonColony/Pawn/6. Split Human Names By Gender (한 번만)")]
    static void Run()
    {
        var data = AssetDatabase.LoadAssetAtPath<UnitData>(Asset);
        if (data == null) { Debug.LogError($"[이름] {Asset}이 없다."); return; }
        if (data.MaleNames.Count > 0 || data.FemaleNames.Count > 0)
        {
            Debug.Log("[이름] 이미 갈라져 있다 — 아무것도 안 한다(고칠 것은 인스펙터에서).");
            return;
        }

        var male   = new HashSet<string>(Male);
        var female = new HashSet<string>(Female);
        var keep   = new List<string>();
        foreach (var n in data.NamePool)
        {
            if (male.Contains(n))        data.MaleNames.Add(n);
            else if (female.Contains(n)) data.FemaleNames.Add(n);
            else                         keep.Add(n);
        }
        data.NamePool = keep;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"[이름] 남 {data.MaleNames.Count} · 여 {data.FemaleNames.Count} · 중성 {keep.Count} — " +
                  $"중성: {string.Join(", ", keep)}");
    }
}
