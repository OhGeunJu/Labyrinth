using UnityEditor;
using UnityEngine;

// **모델 입히기를 한 번에**(2026-09-30, 사용자: *"한번에 하게 못해?"*).
//
// 얼굴을 입히는 문이 도구마다 따로다 — 표로 입히는 것(`PropDresser`)과 제 손으로 조립하는 것(바리케이드 · 갱도
// 버팀목 · 소환진 · 다리 · 화톳불 · 우리 · 횃불 · 무덤 뚜껑). 하나씩 누르다 빠뜨리면 그 건물만 옛 도형으로 남는다.
//
// **`ExecuteMenuItem`으로 부른다**(`1. Rebuild Underworld`와 같은 규칙) — 사람이 누르는 것과 같은 길이라 도구가 바뀌어도
// 여기가 안 뒤처지고, 메뉴 이름이 바뀌면 **바로 멈춰서 알린다**(조용히 건너뛰지 않는다). **메뉴 번호를 바꾸면 여기도.**
//
// **표로 입히는 것이 먼저다** — 뒤의 도구들은 제 건물만 만진다. 작업대 등급 모델(`3. Create Bench Tier Art`)은 뺐다 —
// 그쪽 프리팹은 아무도 다시 안 지어 늘 남아 있고, 넣으면 매번 다시 굽느라 느려지기만 한다.
public static class DressAll
{
    static readonly string[] Steps =
    {
        "DungeonColony/Building/1. Dress Buildings (모델 입히기)",
        "DungeonColony/Building/6. Dress Wall Torch (횃불 두 모양)",
        "DungeonColony/Building/7. Dress Grave Lid (무덤 뚜껑)",
        "DungeonColony/Building/8. Build Barricade (나무 방벽 조립)",
        "DungeonColony/Building/A. Create Mine Support (갱도 버팀목)",
        "DungeonColony/Building/B. Create Summoning Circle (소환진)",
        "DungeonColony/Building/C. Dress Campfire (모닥불 모델)",
        "DungeonColony/Building/D. Build Prison Cage (쇠창살 우리 2x2)",
        "DungeonColony/World/J. Create Bridges (다리 · 절벽 재질)",
    };

    [MenuItem("DungeonColony/Building/0. Dress All (모델 전부 입히기)")]
    public static void Run()
    {
        int done = 0;
        foreach (var m in Steps)
        {
            if (EditorApplication.ExecuteMenuItem(m)) { done++; continue; }
            Debug.LogError($"[모델 전부] `{m}`을 실행하지 못했다 — 여기서 멈춘다({done}/{Steps.Length}). 메뉴 이름이 바뀌었는지 볼 것.");
            return;
        }
        Debug.Log($"[모델 전부] {done}개 도구를 차례로 돌렸다 — 각 도구의 결과는 위 줄들에 있다.");
    }
}
