// **건설 목록의 갈래.** "무엇을 지으려 하는가"에 한 낱말로 답하는 것이 이 enum의 일이다.
//
// 한동안 넷뿐이었다(자원·방어·건물·기타). 그런데 건물이 마흔 종을 넘기면서 **서른 종이
// `건물` 하나에 들어앉았다** — 창고도 침대도 작업대 아홉도 연구대도 서브코어도 거기 있었다.
// 그러면 갈래가 있으나 마나다: 무엇을 지으려 하든 같은 목록을 끝까지 훑어야 한다.
//
// ## 값은 뒤에 붙인다 — 다만 이름은 바꿔도 된다
//
// 직렬화가 **정수**라 중간에 끼우면 이미 저작된 `BuildingData` 마흔한 개가 통째로 밀린다
// (`JobCategory`·`StatName`과 같은 규칙). 그래서 새 갈래는 전부 꼬리에 붙였다.
//
// **`Building`은 `Living`으로 이름만 바꿨다**(값 2 그대로). 이름은 자리를 안 옮기므로
// 안전하고, 덕분에 **죽은 값이 하나도 안 남는다** — 비워 두면 "이건 왜 있지"가 되고, 다음
// 사람이 새 에셋을 거기 넣는다.
//
// ## 화면 순서는 `Order`가 정한다 — enum 순서가 아니라
//
// 값 순서는 *언제 만들어졌나*이고 화면 순서는 *어떤 차례로 짓나*다. 둘을 같은 것으로 두면
// 갈래를 하나 더할 때마다 화면 순서와 직렬화 중 하나를 포기해야 한다
// (`JobCategoryInfo.QueueOrder`가 같은 자리에서 같은 답을 쓴다).
public enum BuildCategory
{
    Resource,   // 자원 — 밭·우물·나무·돌 + 광맥
    Defense,    // 방어 — 벽·울타리·바리케이드
    Living,     // 거주 — 침대·화톳불 (옛 `Building`. 값 2를 그대로 물려받는다)
    Misc,       // 기타 — 어디에도 안 붙는 것(서브코어·무덤·화장터)

    // ── 아래는 2026-08-22에 붙였다. **반드시 꼬리에** ──────────────────────
    Floor,      // 바닥 — substrate. 무료·코어 무관이라 다른 무엇과도 성질이 다르다
    Storage,    // 보관 — 창고
    Production, // 제작 — 작업대·연구대·도축대 (폰이 붙어 일하는 설비)
    Prison,     // 수용 — 우리·죄수 침대
}

// **갈래를 쪼개는 값은 "고를 것이 갈리는가"다.**
//
// 광맥 열셋을 `자원`에서 떼어 봤다가 되돌렸다 — 밭·우물·나무·돌과 광맥은 **둘 다 지형에 놓는
// 것**이라 짓는 사람이 그 둘 사이에서 헷갈릴 일이 없었고, 갈래만 하나 늘어 칸을 한 번 더
// 누르게 됐다. 열일곱이 한 칸에 있어도 그 칸을 여는 이유는 하나다.
//
// **장례(무덤·화장터)도 되돌려 `기타`에 넣었다** — 둘뿐인 데다 자주 짓는 것이 아니라, 제
// 칸을 가지면 그 칸은 대개 비어 있는 채로 자리만 차지한다.

public static class BuildCategoryExt
{
    public static string DisplayName(this BuildCategory c) => c switch
    {
        BuildCategory.Resource   => "자원",
        BuildCategory.Defense    => "방어",
        BuildCategory.Living     => "거주",
        BuildCategory.Misc       => "기타",
        BuildCategory.Floor      => "바닥",
        BuildCategory.Storage    => "보관",
        BuildCategory.Production => "제작",
        BuildCategory.Prison     => "수용",
        _                        => c.ToString()
    };

    // **짓는 차례에 가깝게 늘어놓는다** — 바닥을 깔고 담을 두르고 창고를 세우고 설비를 놓고
    // 사람을 재우고, 그다음이 붙잡아 두는 것과 치우는 것이다. 자원·광맥은 지형에 놓는 것이라
    // 뒤로 뺐다(자주 여는 갈래가 아니다).
    //
    // **여덟이라 네 열에 두 줄로 떨어진다** — 줄 수가 곧 아래 띠의 높이다. 갈래를 더할 때는
    // 줄이 늘어나는지 보고 띠도 같이 키울 것(`BuildMenuUpdater`).
    //
    // **여기 안 적은 갈래는 화면에 안 뜬다.** 목록의 유일한 근거라 새 갈래를 더하면 여기에도
    // 적어야 한다 — 빠뜨리면 조용히 사라지는 것이 아니라 **그 갈래의 건물이 통째로 안 보인다.**
    public static readonly BuildCategory[] Order =
    {
        BuildCategory.Floor,
        BuildCategory.Defense,
        BuildCategory.Storage,
        BuildCategory.Production,
        BuildCategory.Living,
        BuildCategory.Prison,
        BuildCategory.Resource,
        BuildCategory.Misc,
    };
}
