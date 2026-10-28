using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// **메뉴 번호가 겹치면 컴파일 직후 알린다**(2026-09-26, 사용자: *"순서는 왜 맨날 중복이 되는거야"*).
//
// 번호(`1.` · `0b.` · `I.`)는 도구마다 **제 파일의 `[MenuItem]`에 따로** 적는다. 그래서 새 도구를 붙일 때 옆
// 파일에 같은 번호가 있는지를 볼 자리가 없었고, 실제로 `World/I`에 둘(절벽 칠하기 ↔ 씬의 방랑자 치우기)이 서서
// **방랑자 치우기가 잘못 눌렸다**(그 도구는 씬을 바로 저장한다). `Pawn/3`도 둘이었다.
//
// **번호를 한 파일에 모으지 않는다** — 모으면 도구를 옮기거나 지울 때마다 그 파일도 고쳐야 하고, 빠뜨리면 그
// 목록이 거짓말을 한다. 대신 **유니티가 아는 메뉴 전부를 훑어** 같은 폴더에 같은 번호가 둘이면 에러로 올린다 —
// 목록을 따로 들지 않으니 뒤처질 것이 없다. **`1. Rebuild Underworld`는 메뉴를 이름으로 부른다** — 번호를
// 바꾸면 거기 문자열도 같이 고칠 것(못 찾으면 그 묶음이 에러를 내고 멈춘다).
[InitializeOnLoad]
static class MenuOrderCheck
{
    static MenuOrderCheck() => EditorApplication.delayCall += Check;

    // `1.` · `0b.` · `7a.` · `I.` · `C2.` 처럼 **이름 앞의 번호와 점**.
    static readonly Regex Prefix = new(@"^([0-9]+[a-z]?|[A-Z][0-9]?)\.\s");

    static void Check()
    {
        var seen = new Dictionary<string, List<string>>();

        foreach (var method in TypeCache.GetMethodsWithAttribute<MenuItem>())
        foreach (MenuItem a in method.GetCustomAttributes(typeof(MenuItem), false))
        {
            if (a.validate) continue;   // 체크 표시용 짝은 같은 이름이 정상이다
            string path = a.menuItem;
            if (!path.StartsWith("DungeonColony/")) continue;

            int slash = path.LastIndexOf('/');
            string folder = path.Substring(0, slash), leaf = path.Substring(slash + 1);
            var m = Prefix.Match(leaf);
            if (!m.Success) continue;

            string key = folder + "/" + m.Groups[1].Value;
            if (!seen.TryGetValue(key, out var list)) seen[key] = list = new List<string>();
            list.Add($"{leaf} ({method.DeclaringType?.Name})");
        }

        foreach (var kv in seen)
            if (kv.Value.Count > 1)
                Debug.LogError($"[메뉴] 번호가 겹친다 — `{kv.Key}.`에 {kv.Value.Count}개: {string.Join(" · ", kv.Value)}. " +
                               "번호를 바꿀 것(같은 번호면 누를 때 헷갈린다). `1. Rebuild Underworld`가 이름으로 부르는 것이면 거기도 같이.");
    }
}
