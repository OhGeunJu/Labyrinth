using UnityEditor;
using UnityEngine;

// **자연은 마른다 — 그 눈금을 채운다**(2026-09-06).
//
// `TotalYields`를 냈는데 **거대버섯 하나만 적어 놓고 나머지를 비워 뒀다**(0 = 무한).
// 그래서 씬에 손으로 놓은 나무·돌·광맥이 **여전히 무한**이었다(사용자: *"핵 없어도 나무
// 잘캐고 돌 잘캐는데? 내가 배치한 돌이랑 나무를 말하는거야"*).
//
// **축을 넣고 데이터를 안 채우면 축이 없는 것과 같다** — 이 저장소가 여러 번 겪은 자리다
// (`UnitData.IsConstruct`가 에셋마다 꺼진 채로 태어나던 것과 같은 모양).
//
// ## 표가 아니라 규칙에서 낸다
//
// 손으로 열여섯 개를 적으면 **새 노드를 만드는 날 그것만 무한으로 남는다.** 그래서
// **게이지에서 낸다**: 한 번 캐는 데 오래 걸리는 것일수록 귀한 것이고, 귀한 것은 조금만
// 나온다. `900 / 게이지`를 3~12로 자른다 —
//
//   돌(60) 12번 · 목재(80) 11 · 석탄(100) 9 · 철(150) 6 · 은(220) 4 · 금 이상 3
//
// **이미 적힌 것은 안 건드린다** — 손으로 정한 값(거대버섯 6)이 도구를 돌릴 때마다
// 되돌아가면 다시 돌릴 엄두가 안 난다.
//
// ## 지은 것에는 안 걸린다
//
// `ResourceNode.Depleted`가 `!IsBuilt`를 먼저 본다. 그래서 **같은 데이터**를 쓰는 야생
// 철광맥은 여섯 번 만에 마르고, 기지에 심은 철광맥은 마르지 않는다 — 그것이 *캐 온 것으로
// 이식한다*는 고리의 뜻이다.
public static class WildYields
{
    const string Dir = "Assets/5_Data/Resource";

    [MenuItem("DungeonColony/World/9. Set Wild Yields (야생 매장량)")]
    static void Fill()
    {
        int filled = 0, kept = 0;
        var lines = new System.Text.StringBuilder();

        foreach (var guid in AssetDatabase.FindAssets("t:ResourceNodeData", new[] { Dir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<ResourceNodeData>(path);
            if (data == null) continue;

            bool touched = false;

            // **손으로 적은 것은 그대로** — 도구를 돌릴 때마다 저작이 되돌아가면 다시 돌릴
            // 엄두가 안 난다.
            if (data.TotalYields <= 0)
            {
                data.TotalYields = YieldsFor(data.GaugeRequired);
                touched = true;
            }

            if (!touched) { kept++; continue; }

            EditorUtility.SetDirty(data);
            filled++;

            lines.Append("\n  ").Append(data.name)
                 .Append("  게이지 ").Append(data.GaugeRequired.ToString("0"))
                 .Append("  →  ").Append(data.TotalYields).Append("번");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[야생] 매장량을 채웠다 — 새로 {filled}개(이미 적힌 것 {kept}개는 그대로).{lines}\n" +
                  "**지은 노드에는 안 걸린다**(`ResourceNode.IsBuilt`) — 기지에 심은 광맥은 마르지 않는다.");
    }

    static int YieldsFor(float gauge)
        => Mathf.Clamp(Mathf.RoundToInt(900f / Mathf.Max(1f, gauge)), 3, 12);

}
