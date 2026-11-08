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
        "GoblinLong"  => "긴 고블린 귀",
        "GoblinCut"   => "한쪽 잘린 고블린 귀",
        _             => key,
    };

    public static string Nose(string key) => key switch
    {
        PawnLook.None => "없음",
        "Real"        => "콧대 있는 코",
        "Anime"       => "작은 코",
        "Pointy"      => "뾰족한 코",
        "Long"        => "긴 코",
        "Hook"        => "매부리코",
        "HookBig"     => "큰 매부리코",
        "HookSmall"   => "작은 매부리코",
        "Animal"      => "동물 코",
        _             => key,
    };

    // ── 고르기 축(`Head.Pick.{축}.{벌}` — 종족마다 다르다) ──────────────────────
    // 축 이름 — 모르는 축은 이름 그대로(새 축을 더하면 여기에 한 줄).
    public static string Axis(string axis)
    {
        if (string.IsNullOrEmpty(axis)) return "";
        if (axis.StartsWith("Tat")) return "문신 " + axis.Substring(3);
        return axis switch
        {
            "Hair"  => "머리카락",
            "Bangs" => "앞머리",
            "Beard" => "수염",
            "Brow"  => "눈썹",
            "Brows" => "눈썹",
            "Nose"  => "코",
            "Ear"   => "귀",
            "Eyes"  => "눈",
            "Mouth" => "입",
            "Tusk"  => "엄니",
            "Neck"  => "목걸이",
            "Wrist" => "팔찌",
            "Ankle" => "발찌",
            "Sash"  => "띠",
            _       => axis,
        };
    }

    // 줄 순서 — 머리카락이 앞머리보다 위(사용자 11-08). 모르는 축은 뒤로.
    public static int AxisOrder(string axis) => axis switch
    {
        "Hair" => 0, "Bangs" => 1, "Beard" => 2, "Brow" => 3, "Brows" => 3, "Eyes" => 4,
        "Mouth" => 5, "Nose" => 6, "Ear" => 7, "Tusk" => 8,
        _ => axis != null && axis.StartsWith("Tat") ? 20 : 10,
    };

    // 벌 이름 — 축마다 한글(같은 이름이 축마다 다르다 — `Long`은 긴 머리 · 긴 수염 · 긴 코). 몇 번째인지 함께(`3 / 12  올백`).
    // 새 벌을 더하면 여기에 한 줄 — 빠지면 저작 이름이 그대로 보인다.
    public static string Option(string axis, string option, int index, int count)
    {
        string n = OptionName(axis, option);
        return count > 1 ? $"{index + 1} / {count}  {n}" : n;
    }

    static string OptionName(string axis, string o)
    {
        if (o == PawnLook.Bald) return "민머리";
        if (o == PawnLook.None || o == "None" || o == "Off") return "없음";
        if (o == "On") return "있음";
        string n = axis switch
        {
            "Hair" => o switch
            {
                "BackBraid" => "뒤로 땋은 머리", "BaldWings" => "옆머리만 남은 대머리", "BangsArch" => "둥근 앞머리",
                "BangsM" => "M자 앞머리", "BangsMShort" => "짧은 M자 앞머리", "Bob" => "단발", "Braid" => "한 갈래 땋은 머리",
                "Braids" => "땋은 머리 다발", "Buzz" => "반삭", "BuzzCut" => "짧게 민 머리", "Comma" => "쉼표 머리",
                "Cornrows" => "콘로우", "Crest" => "볏 머리", "CrestTail" => "볏 꽁지머리", "Crop" => "크롭 컷",
                "CrownBraid" => "왕관 땋은 머리", "Curtain" => "커튼 머리", "Dandy" => "댄디 컷", "Dreads" => "드레드",
                "FauxHawk" => "포호크", "Fringe" => "앞머리만 남긴 머리", "HalfBun" => "반묶음 똥머리", "HalfUp" => "반묶음",
                "HighTail" => "높은 꽁지머리", "KnotTail" => "상투 꽁지머리", "Long" => "긴 머리", "LongBraids" => "긴 땋은 머리",
                "LongFringe" => "긴 앞머리", "LongStraight" => "긴 생머리", "LooseDreads" => "풀어 내린 드레드",
                "LowBun" => "낮은 쪽머리", "LowPony" => "낮은 포니테일", "LowTail" => "낮게 묶은 꽁지", "ManBun" => "묶은 똥머리",
                "Mane" => "갈기 머리", "Messy" => "부스스한 머리", "Mohawk" => "모히칸", "Pixie" => "픽시 컷", "Pomp" => "퐁파두르",
                "PonyA" => "포니테일 A", "PonyA2" => "포니테일 A2", "PonyB" => "포니테일 B", "PonyB2" => "포니테일 B2", "PonyC" => "포니테일 C",
                "Ponytail" => "포니테일", "Shag" => "샤기 컷", "ShortFringe" => "짧은 앞머리", "Shoulder" => "어깨 단발",
                "SideSweep" => "옆으로 넘긴 머리", "Slick" => "올백", "SlickMullet" => "올백 멀릿", "Spikes" => "가시 머리",
                "Spiky" => "삐죽 머리", "SpikyBob" => "삐죽 단발", "Straight" => "생머리", "Tail" => "꽁지머리",
                "TopBun" => "정수리 똥머리", "Topknot" => "상투", "TwinBraids" => "양갈래 땋은 머리", "TwinTails" => "양갈래",
                "TwinTailsV" => "낮은 양갈래", "Undercut" => "투블럭", "WarKnot" => "전사 매듭",
                _ => null,
            },
            "Bangs" => o switch
            {
                "Clumps" => "덩어리 앞머리", "Open" => "깐 앞머리", "Parted" => "가르마 앞머리", "SideOpen" => "옆으로 깐 앞머리",
                "Sweep" => "쓸어 넘긴 앞머리", "Wispy" => "숱 적은 앞머리", _ => null,
            },
            "Beard" => o switch
            {
                "Box" => "각진 수염", "Braid" => "땋은 수염", "BraidFull" => "두 갈래 땋은 수염", "Braids" => "땋은 수염 다발",
                "Bushy" => "덥수룩한 수염", "Forked" => "갈래 수염", "Goatee" => "염소수염", "Long" => "긴 수염",
                "Mutton" => "구레나룻", "Shaggy" => "텁수룩한 수염", "Stache" => "콧수염", "Tied" => "묶은 수염",
                "TwinBraid" => "쌍갈래 수염", _ => null,
            },
            "Brow" or "Brows" => o switch
            {
                "Angry" => "치켜 올라간 눈썹", "Arch" => "아치 눈썹", "Bushy" => "숱 많은 눈썹", "Droop" => "처진 눈썹",
                "Heavy" => "굵고 낮은 눈썹", "Soft" => "부드러운 눈썹", "Thick" => "두꺼운 눈썹", "Tufted" => "삐친 눈썹", _ => null,
            },
            "Ear" => o switch
            {
                "Big" => "큰 귀", "Leaf" => "잎 귀", "Long" => "긴 귀", "Mid" => "중간 귀", "Small" => "작은 귀", "Sweep" => "젖힌 귀", _ => null,
            },
            "Mouth" => o switch
            {
                "Frown" => "찡그린 입", "Line" => "일자 입", "Open" => "벌린 입", "Small" => "작은 입", "Smile" => "웃는 입", "Smirk" => "비웃는 입", _ => null,
            },
            "Nose" => o switch
            {
                "Broad" => "넓은 코", "Bulb" => "뭉툭한 코", "Button" => "단추 코", "Flat" => "납작한 코", "Long" => "긴 코",
                "Pointed" => "뾰족한 코", "Straight" => "곧은 코", _ => null,
            },
            _ => null,
        };
        return n ?? o;
    }

    // 머리색(`MarkingLook.HairTone`) — `Seeded`는 종족이 고르는 색.
    public static string HairTone(MarkingLook.HairTone t) => t switch
    {
        MarkingLook.HairTone.Seeded     => "기본(종족)",
        MarkingLook.HairTone.Black      => "검정",
        MarkingLook.HairTone.DarkBrown  => "짙은 갈색",
        MarkingLook.HairTone.Grey       => "회색",
        MarkingLook.HairTone.Brown      => "갈색",
        MarkingLook.HairTone.Blonde     => "금발",
        MarkingLook.HairTone.PaleBlonde => "옅은 금발",
        MarkingLook.HairTone.Platinum   => "백금",
        MarkingLook.HairTone.Silver     => "은발",
        MarkingLook.HairTone.White      => "흰색",
        MarkingLook.HairTone.Auburn     => "적갈색",
        MarkingLook.HairTone.Ginger     => "생강색",
        MarkingLook.HairTone.Red        => "붉은색",
        MarkingLook.HairTone.Honey      => "꿀색",
        _                               => t.ToString(),
    };
}
