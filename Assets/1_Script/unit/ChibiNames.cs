using System.Collections.Generic;

// **치비 머리형·얼굴 칸의 이름 — 우리 이름**(2026-09-19).
//
// 처음엔 림월드의 이름을 그대로 썼다(`Male_AverageNormal` · `rim_eye`). 이름은 저작물이 아니지만 **남의 게임에서
// 옮겨 왔다는 표시**가 되므로 우리 말로 바꿨다(사용자: *"캐릭터 저작권 문제는 어케될까?"*):
//
//   머리 폭  Average → **Broad**  · Narrow → **Slim**
//   턱      Normal  → **Soft**   · Pointy → **Sharp** · Wide → **Square**
//   얼굴 칸  `rim_*` → **`ink_*`**(도구가 먹선으로 그리는 칸)
//
// ## 어기면 조용히 깨지는 것
//
// - **옛 이름은 저장된 곳에 남아 있다** — 눈 자리 표(`ChibiFaceLayout`), 폰마다 못 박은 얼굴형·눈·입
//   (`PawnLook`의 `…Override`). 그 자리는 **읽을 때 `Migrate`를 거친다** — 안 거치면 사람이 맞춘 눈 자리가
//   조용히 측정값으로 돌아가고, 못 박은 얼굴이 조용히 씨앗 굴림으로 돌아간다.
// - **옮기는 표는 여기 하나다** — 읽는 자리마다 제 표를 들면 하나가 빠진다.
public static class ChibiNames
{
    static readonly Dictionary<string, string> Legacy = new()
    {
        { "Male_AverageNormal",   "Male_BroadSoft"    }, { "Male_AveragePointy",   "Male_BroadSharp"   },
        { "Male_AverageWide",     "Male_BroadSquare"  }, { "Male_NarrowNormal",    "Male_SlimSoft"     },
        { "Male_NarrowPointy",    "Male_SlimSharp"    }, { "Male_NarrowWide",      "Male_SlimSquare"   },
        { "Female_AverageNormal", "Female_BroadSoft"  }, { "Female_AveragePointy", "Female_BroadSharp" },
        { "Female_AverageWide",   "Female_BroadSquare"}, { "Female_NarrowNormal",  "Female_SlimSoft"   },
        { "Female_NarrowPointy",  "Female_SlimSharp"  }, { "Female_NarrowWide",    "Female_SlimSquare" },
    };

    // 옛 이름이면 새 이름을, 아니면 그대로. 얼굴 칸은 앞머리만 바뀐다(`rim_` → `ink_`).
    public static string Migrate(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (Legacy.TryGetValue(name, out var now)) return now;
        if (name.StartsWith("rim_")) return "ink_" + name.Substring(4);
        return name;
    }
}
