// **생김새 조각의 화면 이름 — 여기 하나다**(2026-09-20, 캐릭터 생성 화면이 처음 띄웠다).
//
// 조각의 식별자(`Male_BroadSoft`·`Ponytail`·`Elf`)는 **굽는 도구와 저장이 쓰는 이름**이라 번역하면 안 된다 — 화면에
// 띄울 이름은 여기서 따로 낸다(`StatNameExt.ToDisplay`와 같은 자리). 모르는 이름은 식별자 그대로 낸다: 새 머리형을
// 굽고 여기를 안 고치면 **영어가 보여서** 빠뜨린 것이 드러난다(빈칸이면 안 드러난다).
public static class PawnLookLabels
{
    // 머리형 — `{성별}_{폭}{모양}`(`ChibiNames`): 폭 Broad/Slim · 모양 Soft/Sharp/Square.
    public static string Head(string key)
    {
        if (string.IsNullOrEmpty(key)) return "기본";
        string s = key.Contains("_") ? key.Substring(key.IndexOf('_') + 1) : key;
        string width = s.StartsWith("Broad") ? "넓은" : s.StartsWith("Slim") ? "갸름한" : null;
        string shape = s.EndsWith("Soft") ? "둥근 턱" : s.EndsWith("Sharp") ? "뾰족한 턱" : s.EndsWith("Square") ? "각진 턱" : null;
        return width != null && shape != null ? $"{width} {shape}" : key;
    }

    public static string Hair(string key) => key switch
    {
        PawnLook.Bald => "민머리",
        "Short"       => "짧은 머리",
        "Bob"         => "단발",
        "Long"        => "긴 머리",
        "Bun"         => "올림머리",
        "Ponytail"    => "뒤로 묶은 머리",
        _             => key,
    };

    public static string Ear(string key) => key switch
    {
        PawnLook.None => "없음",
        "Human"       => "보통 귀",
        "Elf"         => "엘프 귀",
        "Goblin"      => "고블린 귀",
        _             => key,
    };

    public static string Nose(string key) => key switch
    {
        PawnLook.None => "없음",
        "Real"        => "콧대 있는 코",
        "Anime"       => "작은 코",
        "Pointy"      => "뾰족한 코",
        "Long"        => "긴 코",
        "Animal"      => "동물 코",
        _             => key,
    };
}
