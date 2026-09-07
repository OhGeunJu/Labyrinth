using UnityEditor;
using UnityEngine;

// **고블린의 이력 한 벌**(2026-09-08, 사용자 결정).
//
// 그전에는 고블린에게 배경이 없었다 — 이유가 둘이었다: `Goblin.asset`의 `UsesBackstory`가
// 꺼져 있고(*"사람만 받는다"* — 늑대에게 유년기를 굴려 주지 않으려고), **저작된 배경
// 서른여섯이 전부 사람 것**이었다(`civilian`·`bandit`·`cult`·`mercenary`).
//
// **그래서 그냥 켤 수가 없다.** 묶음이 없으면 `civilian`으로 폴백해 고블린에게서 *"사원의
// 시동"*이 나오고, 세력마다 이력을 갈라 둔 뜻이 지워진다(`BackstoryData`에 이미 적혀 있다:
// **공용이라는 것은 없다 — 모든 배경은 어느 무리의 것이다**).
//
// 그래서 **묶음을 먼저 만들고 켠다.** 이 도구가 셋을 한 번에 한다:
//   1) `goblin` 묶음의 배경 열둘(유년기 여섯 · 성년기 여섯)
//   2) `Goblin.asset`의 `UsesBackstory` 켜기
//   3) `Faction_Goblin`의 `backstoryGroup`을 `goblin`으로
//
// **셋을 갈라 놓으면 하나를 빠뜨린다** — 켜기만 하면 사람 이력이 섞이고, 만들기만 하면
// 아무 데서도 안 나오며, 세력을 안 물리면 폴백으로 떨어진다. 화면에는 셋 다
// *"배경이 이상하다"* 하나로만 보인다.
public static class GoblinBackstoryCreator
{
    const string Group  = "goblin";
    const string Folder = "Assets/Resources/Data/Backstories";

    // 스탯 보정은 **작게 준다**(4~10). 배경은 *어떤 사람인가*를 말하는 것이지 등급을 대신하는
    // 것이 아니다 — 크게 주면 배경 뽑기가 곧 강함이 된다(`TraitData`와 같은 취지).
    // **나쁜 쪽만 있는 배경은 안 만든다**(특성과 같은 규칙 — 그건 개성이 아니라 벌칙이다).
    struct Entry
    {
        public BackstorySlot Slot;
        // **파일 이름은 따로 든다.** 제목에서 뽑으려 했더니 한글에 ASCII가 하나도 없어
        // 해시 폴백으로 떨어져 `Back_Adult_Gob_2886` 같은 것이 나왔다 — 폴더에서 무엇이
        // 무엇인지 알 수가 없다. **이름은 저작하는 것이지 짜내는 것이 아니다.**
        public string Id;
        public string Title, Desc;
        public float Weight;
        public (StatName stat, int delta)[] Offsets;
    }

    static readonly Entry[] Entries =
    {
        new() { Slot = BackstorySlot.Childhood, Weight = 1.4f, Id = "CaveBorn", Title = "굴의 아이",
                Desc = "빛을 본 적이 없다. 좁은 데를 무서워하지 않는다.",
                Offsets = new[] { (StatName.Mining, 7), (StatName.Athletics, 4), (StatName.Social, -5) } },

        new() { Slot = BackstorySlot.Childhood, Weight = 1.2f, Id = "Scraps", Title = "찌꺼기 줍기",
                Desc = "큰 놈들이 먹고 남긴 것을 주워 먹으며 컸다. 손이 빠르다.",
                Offsets = new[] { (StatName.Dexterity, 6), (StatName.Cooking, 5), (StatName.Toughness, -4) } },

        new() { Slot = BackstorySlot.Childhood, Weight = 1.0f, Id = "Runt", Title = "무리의 막내",
                Desc = "맞으면서 자리를 배웠다. 맞는 데는 이골이 났다.",
                Offsets = new[] { (StatName.Toughness, 8), (StatName.Evasion, 4), (StatName.Cognition, -5) } },

        new() { Slot = BackstorySlot.Childhood, Weight = 0.9f, Id = "Mushroom", Title = "버섯밭에서",
                Desc = "균사림 가장자리에서 컸다. 먹을 것과 못 먹을 것을 안다.",
                Offsets = new[] { (StatName.Growing, 8), (StatName.Medicine, 4), (StatName.MeleeAttack, -4) } },

        new() { Slot = BackstorySlot.Childhood, Weight = 0.8f, Id = "Trapper", Title = "덫 놓는 법",
                Desc = "형들을 따라다니며 덫을 배웠다. 기다릴 줄 안다.",
                Offsets = new[] { (StatName.Bow, 7), (StatName.PrecisionShooting, 5), (StatName.Strength, -4) } },

        new() { Slot = BackstorySlot.Childhood, Weight = 0.7f, Id = "Stones", Title = "돌을 던지던",
                Desc = "던질 것이 돌밖에 없었다. 그래서 잘 던진다.",
                Offsets = new[] { (StatName.Javelin, 8), (StatName.Cognition, 4), (StatName.Research, -6) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 1.4f, Id = "Raider", Title = "약탈꾼",
                Desc = "가진 것을 빼앗는 쪽에 섰다. 도망치는 것도 배웠다.",
                Offsets = new[] { (StatName.OneHandAxe, 8), (StatName.Athletics, 5), (StatName.Social, -6) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 1.2f, Id = "Digger", Title = "굴 파는 손",
                Desc = "무리의 길을 냈다. 어디를 파야 무너지지 않는지 안다.",
                Offsets = new[] { (StatName.Mining, 10), (StatName.Strength, 5), (StatName.Bow, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 1.0f, Id = "Spear", Title = "창잡이",
                Desc = "앞에 서는 자리를 받았다. 그 자리는 오래 사는 자리가 아니다.",
                Offsets = new[] { (StatName.Polearm, 9), (StatName.Defense, 5), (StatName.Evasion, -4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.9f, Id = "Bones", Title = "뼈 손질",
                Desc = "죽은 것에서 쓸 것을 발라냈다. 손이 거칠고 정확하다.",
                Offsets = new[] { (StatName.Cutting, 8), (StatName.WeaponCrafting, 6), (StatName.Growing, -4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.7f, Id = "Chief", Title = "무리를 이끈",
                Desc = "더 큰 놈을 이겨 자리를 얻었다. 그 자리를 지키는 데 힘을 쓴다.",
                Offsets = new[] { (StatName.MeleeAttack, 9), (StatName.Toughness, 6), (StatName.Social, 5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.6f, Id = "Shaman", Title = "샤먼의 시중",
                Desc = "불을 다루는 법을 곁눈질로 배웠다. 제대로 배우지는 못했다.",
                Offsets = new[] { (StatName.FireMagic, 9), (StatName.DarkMagic, 5), (StatName.Strength, -5) } },
    };

    // ── 소환물 ───────────────────────────────────────────────────────────────
    //
    // **소환된 것에게도 무리가 있다**(2026-09-08, 사용자 결정). 처음엔 비워 뒀는데 창에
    // 빈칸이 남았다 — 이 저장소의 규칙은 *"공용이라는 것은 없다"*이지 *"어떤 무리는 배경이
    // 없다"*가 아니다.
    //
    // **한 벌로 골렘·해골·고블린을 다 덮는다.** 종족마다 쓰면 소환물이 늘 때마다 묶음이
    // 늘고, 그중 하나는 반드시 뒤처진다. 그래서 글이 **무엇으로 만들어졌는지를 안 말한다** —
    // *불려 나왔다*와 *무엇에 쓰였다*만 말하면 돌이든 뼈든 맞는다.
    //
    // 스탯 창은 이 칸들을 **비롯됨 / 부려진 일**로 부른다(`UnitStatsUI`) — 소환물에게
    // *유년기*는 없다.
    static readonly Entry[] Summoned =
    {
        new() { Slot = BackstorySlot.Childhood, Weight = 1.3f, Id = "Forged", Title = "핵이 빚었다",
                Desc = "부르는 소리 말고는 아무것도 모르는 채로 섰다.",
                Offsets = new[] { (StatName.Toughness, 6), (StatName.Labor, 5), (StatName.Social, -8) } },

        new() { Slot = BackstorySlot.Childhood, Weight = 1.1f, Id = "Remnant", Title = "남은 것으로",
                Desc = "먼저 스러진 것들의 조각을 모아 세웠다. 어딘가 삐걱인다.",
                Offsets = new[] { (StatName.Toughness, 8), (StatName.Athletics, -5), (StatName.Cognition, 4) } },

        new() { Slot = BackstorySlot.Childhood, Weight = 1.0f, Id = "Deep", Title = "깊은 데서 끌려온",
                Desc = "더 아래에 있던 것이 불려 올라왔다. 어둠을 어려워하지 않는다.",
                Offsets = new[] { (StatName.Mining, 7), (StatName.DarkMagic, 5), (StatName.Medicine, -5) } },

        new() { Slot = BackstorySlot.Childhood, Weight = 0.9f, Id = "Flawed", Title = "설익은 소환",
                Desc = "부르는 손이 떨렸다. 그래서 조금 다르게 나왔다.",
                Offsets = new[] { (StatName.Dexterity, 7), (StatName.Evasion, 5), (StatName.Toughness, -6) } },

        new() { Slot = BackstorySlot.Childhood, Weight = 0.8f, Id = "Bound", Title = "단단히 묶인",
                Desc = "풀리지 않게 여러 번 눌러 새겼다. 무겁고 느리다.",
                Offsets = new[] { (StatName.Strength, 8), (StatName.Defense, 5), (StatName.Athletics, -6) } },

        new() { Slot = BackstorySlot.Childhood, Weight = 0.6f, Id = "Echo", Title = "누군가의 메아리",
                Desc = "부를 때 딸려 온 것이 있다. 가끔 제 것이 아닌 것을 안다.",
                Offsets = new[] { (StatName.Cognition, 8), (StatName.ArcaneMagic, 6), (StatName.Strength, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 1.4f, Id = "Digging", Title = "굴을 팠다",
                Desc = "세운 날부터 벽만 보았다. 어디가 무를지 손이 안다.",
                Offsets = new[] { (StatName.Mining, 10), (StatName.Strength, 5), (StatName.Social, -6) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 1.2f, Id = "Gate", Title = "문을 지켰다",
                Desc = "오래 서 있는 것이 하는 일의 전부였다. 잘 안 밀린다.",
                Offsets = new[] { (StatName.Defense, 9), (StatName.Shield, 6), (StatName.Athletics, -4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 1.0f, Id = "Hauling", Title = "지고 날랐다",
                Desc = "무거운 것을 옮기는 데 쓰였다. 등이 단단해졌다.",
                Offsets = new[] { (StatName.Strength, 9), (StatName.Labor, 6), (StatName.MeleeAttack, -4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.9f, Id = "Vanguard", Title = "앞에 세워졌다",
                Desc = "맞는 자리에 놓였다. 그 자리에 오래 있었다.",
                Offsets = new[] { (StatName.Toughness, 9), (StatName.MeleeAttack, 6), (StatName.Cognition, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.7f, Id = "Relit", Title = "여러 번 다시 세워졌다",
                Desc = "부서질 때마다 도로 불렸다. 부서지는 데 익숙하다.",
                Offsets = new[] { (StatName.Toughness, 7), (StatName.Evasion, 6), (StatName.Growing, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.6f, Id = "Idle", Title = "오래 서 있었다",
                Desc = "부를 일이 없어 구석에 세워 두었다. 아직 새것에 가깝다.",
                Offsets = new[] { (StatName.Dexterity, 6), (StatName.Research, 5), (StatName.MeleeAttack, -5) } },
    };

    // ── 상인 ─────────────────────────────────────────────────────────────────
    //
    // **성년기만 있다**(2026-09-08, 사용자 결정). 상인도 어디선가 자랐고 그건 **세력의
    // 이력**이다 — 유년기까지 갈면 *"태어날 때부터 무기 상인"*이 된다.
    //
    // **무엇을 파느냐로 갈린다.** 묶음 이름은 `MerchantTypeExt.StoryGroup`이 든다 —
    // 여기서 문자열을 또 적으면 오타 하나로 갈려 조용히 `civilian` 폴백을 받는다.
    //
    // 종류당 다섯. **한 무리에 다섯이면 같은 상인을 세 번 만나도 안 겹칠 만하다** —
    // 더 늘려도 되지만(값은 거의 안 든다) **늘릴수록 하나하나가 드물어져 인상이 흐려진다.**
    static readonly Entry[] MerchantWeapon =
    {
        new() { Slot = BackstorySlot.Adulthood, Weight = 1.2f, Id = "Forge", Title = "대장간 앞에서",
                Desc = "벼려 나오는 것을 옆에서 보며 값을 배웠다.",
                Offsets = new[] { (StatName.WeaponCrafting, 7), (StatName.Social, 6), (StatName.Growing, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 1.1f, Id = "Caravan", Title = "무기 짐수레",
                Desc = "날붙이만 싣고 다녔다. 녹을 제일 먼저 알아본다.",
                Offsets = new[] { (StatName.Social, 7), (StatName.Smelting, 5), (StatName.Medicine, -4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.9f, Id = "Quartermaster", Title = "군의 병참",
                Desc = "남의 목숨을 셈으로 다뤘다. 숫자가 빠르다.",
                Offsets = new[] { (StatName.Cognition, 7), (StatName.Social, 6), (StatName.Evasion, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.8f, Id = "Duelist", Title = "팔다 남은 것으로",
                Desc = "안 팔린 검을 제가 썼다. 그래서 쓸 줄도 안다.",
                Offsets = new[] { (StatName.OneHandSword, 8), (StatName.MeleeAttack, 5), (StatName.Cooking, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.6f, Id = "Blackmarket", Title = "뒷골목 거래",
                Desc = "묻지 않고 팔았다. 값은 늘 두 배였다.",
                Offsets = new[] { (StatName.Social, 9), (StatName.Evasion, 5), (StatName.Medicine, -6) } },

    };

    static readonly Entry[] MerchantArmor =
    {
        new() { Slot = BackstorySlot.Adulthood, Weight = 1.2f, Id = "Fitter", Title = "치수를 재던",
                Desc = "몸에 맞춰 파는 법을 배웠다. 눈대중이 정확하다.",
                Offsets = new[] { (StatName.ArmorCrafting, 7), (StatName.Social, 6), (StatName.Bow, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 1.0f, Id = "Salvage", Title = "전장 뒤",
                Desc = "벗겨 온 것을 손질해 팔았다. 흠집을 잘 본다.",
                Offsets = new[] { (StatName.ArmorCrafting, 6), (StatName.Cognition, 6), (StatName.Social, 4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.9f, Id = "Guildhand", Title = "공방의 심부름",
                Desc = "가죽과 쇠를 나르며 값을 외웠다.",
                Offsets = new[] { (StatName.Labor, 7), (StatName.Smelting, 5), (StatName.Research, -4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.8f, Id = "Bodyguard", Title = "호위를 서던",
                Desc = "맞아 본 사람이 파는 갑옷이 잘 팔린다.",
                Offsets = new[] { (StatName.Defense, 8), (StatName.Shield, 6), (StatName.Cooking, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.6f, Id = "Tailor", Title = "천을 다루던",
                Desc = "가죽보다 천을 먼저 만졌다. 손이 곱다.",
                Offsets = new[] { (StatName.ArmorCrafting, 8), (StatName.Dexterity, 5), (StatName.Strength, -5) } },

    };

    static readonly Entry[] MerchantMaterial =
    {
        new() { Slot = BackstorySlot.Adulthood, Weight = 1.2f, Id = "Assay", Title = "광석을 보던",
                Desc = "돌만 보고 값을 맀다. 속는 일이 드물다.",
                Offsets = new[] { (StatName.Mining, 7), (StatName.Cognition, 6), (StatName.Social, 4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 1.1f, Id = "Hauler", Title = "짐만 날랐다",
                Desc = "무거운 것을 오래 졌다. 등이 단단하다.",
                Offsets = new[] { (StatName.Strength, 8), (StatName.Labor, 6), (StatName.Research, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.9f, Id = "Smelter", Title = "용광로 곁",
                Desc = "무엇이 잘 녹는지를 몸으로 안다.",
                Offsets = new[] { (StatName.Smelting, 9), (StatName.Toughness, 5), (StatName.Evasion, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.8f, Id = "Woodman", Title = "나무를 대던",
                Desc = "베어 온 것을 골라 팔았다. 결을 읽는다.",
                Offsets = new[] { (StatName.Woodworking, 8), (StatName.Cutting, 5), (StatName.Social, 4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.6f, Id = "Gemcut", Title = "보석을 갈던",
                Desc = "작은 것에 값이 붙는 이유를 안다.",
                Offsets = new[] { (StatName.Gemcutting, 9), (StatName.Dexterity, 5), (StatName.Strength, -6) } },

    };

    static readonly Entry[] MerchantFood =
    {
        new() { Slot = BackstorySlot.Adulthood, Weight = 1.2f, Id = "Cook", Title = "솥 앞에서",
                Desc = "팔기 전에 먼저 끓였다. 상한 것을 냄새로 안다.",
                Offsets = new[] { (StatName.Cooking, 9), (StatName.Medicine, 4), (StatName.MeleeAttack, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 1.1f, Id = "Farmhand", Title = "밭에서 왔다",
                Desc = "기른 것을 제가 내다 팔았다.",
                Offsets = new[] { (StatName.Growing, 8), (StatName.Labor, 5), (StatName.Social, 4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.9f, Id = "Preserve", Title = "말리고 절이던",
                Desc = "오래 가게 만드는 법이 곧 밑천이었다.",
                Offsets = new[] { (StatName.Cooking, 7), (StatName.Alchemy, 5), (StatName.Athletics, -4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.8f, Id = "Herb", Title = "약초를 섞던",
                Desc = "먹는 것과 듣는 것 사이에서 팔았다.",
                Offsets = new[] { (StatName.Medicine, 8), (StatName.Alchemy, 6), (StatName.Strength, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.6f, Id = "Famine", Title = "굶어 본 사람",
                Desc = "없을 때의 값을 안다. 그래서 잘 안 깎아 준다.",
                Offsets = new[] { (StatName.Social, 9), (StatName.Toughness, 5), (StatName.Cognition, -4) } },

    };

    static readonly Entry[] MerchantGeneral =
    {
        new() { Slot = BackstorySlot.Adulthood, Weight = 1.3f, Id = "Peddler", Title = "등짐 장수",
                Desc = "무엇이든 지고 다녔다. 길을 잘 안다.",
                Offsets = new[] { (StatName.Athletics, 7), (StatName.Social, 6), (StatName.MeleeAttack, -4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 1.1f, Id = "Stall", Title = "시장 한 칸",
                Desc = "같은 자리에서 오래 팔았다. 얼굴을 기억한다.",
                Offsets = new[] { (StatName.Social, 8), (StatName.Cognition, 5), (StatName.Athletics, -4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.9f, Id = "Ledger", Title = "장부를 맡던",
                Desc = "남의 셈을 대신했다. 숫자에 밝다.",
                Offsets = new[] { (StatName.Cognition, 8), (StatName.Research, 6), (StatName.Strength, -5) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.8f, Id = "Roadwise", Title = "길에서 자랐다",
                Desc = "실려 다니며 컸다. 어디서 무엇이 비싼지 안다.",
                Offsets = new[] { (StatName.Social, 6), (StatName.Athletics, 6), (StatName.Growing, -4) } },

        new() { Slot = BackstorySlot.Adulthood, Weight = 0.6f, Id = "Ruined", Title = "한 번 망해 본",
                Desc = "크게 잃고 다시 시작했다. 조심스럽다.",
                Offsets = new[] { (StatName.Social, 7), (StatName.Toughness, 6), (StatName.Evasion, 4) } },

    };

    [MenuItem("DungeonColony/World/A. Create Backstories (고블린 · 소환물 · 상인)")]
    static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            Debug.LogError($"[고블린 배경] 폴더가 없습니다: {Folder}");
            return;
        }

        Build(Entries,  Group,                       "Gob", out int made,  out int updated);
        Build(Summoned, PawnSpec.SummonedStoryGroup, "Sum", out int made2, out int upd2);
        made += made2; updated += upd2;

        foreach (var (entries, kind) in Merchants)
        {
            Build(entries, kind.StoryGroup(), Tag(kind), out int m, out int u);
            made += m; updated += u;
        }

        Reconcile(Entries,  Group,                       "Gob");
        Reconcile(Summoned, PawnSpec.SummonedStoryGroup, "Sum");
        foreach (var (entries, kind) in Merchants)
            Reconcile(entries, kind.StoryGroup(), Tag(kind));

        WireRace();
        WireFaction();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[배경] 새로 {made}개 · 갱신 {updated}개 — 묶음 '{Group}'과 " +
                  $"'{PawnSpec.SummonedStoryGroup}'. 종족·세력 배선도 함께 했습니다.");
    }

    static void Build(Entry[] entries, string group, string tag, out int made, out int updated)
    {
        made = 0; updated = 0;
        foreach (var e in entries)
        {
            string slot = e.Slot == BackstorySlot.Childhood ? "Child" : "Adult";
            string path = $"{Folder}/Back_{slot}_{tag}_{e.Id}.asset";

            var data  = AssetDatabase.LoadAssetAtPath<BackstoryData>(path);
            bool isNew = data == null;
            if (isNew) data = ScriptableObject.CreateInstance<BackstoryData>();

            data.Slot        = e.Slot;
            data.Title       = e.Title;
            data.Description = e.Desc;
            data.Weight      = e.Weight;
            data.Group       = group;

            data.StatOffsets.Clear();
            foreach (var (stat, delta) in e.Offsets)
                data.StatOffsets.Add(new TraitData.StatOffset { Stat = stat, Delta = delta });
            data.DisabledJobs.Clear();

            if (isNew) { AssetDatabase.CreateAsset(data, path); made++; }
            else       { EditorUtility.SetDirty(data);          updated++; }
        }
    }

    // **켜 주지 않으면 만들어도 안 나온다** — `PawnSpec.RollCore`가 `UsesBackstory`로 가른다.
    static void WireRace()
    {
        var race = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Resources/Data/Units/Goblin.asset");
        if (race == null) { Debug.LogWarning("[고블린 배경] Goblin.asset을 못 찾았습니다."); return; }

        race.UsesBackstory = true;
        EditorUtility.SetDirty(race);
    }

    // **세력이 제 묶음을 든다**(`FactionData.BackstoryGroup`) — 안 물리면 빈 문자열이라
    // `civilian`으로 떨어져 **고블린에게서 사람 이력이 나온다.**
    static void WireFaction()
    {
        var fac = AssetDatabase.LoadAssetAtPath<FactionData>(
            "Assets/Resources/Data/Factions/Faction_Goblin.asset");
        if (fac == null) { Debug.LogWarning("[고블린 배경] Faction_Goblin.asset을 못 찾았습니다."); return; }

        var so = new SerializedObject(fac);
        var p  = so.FindProperty("backstoryGroup");
        if (p == null) { Debug.LogWarning("[고블린 배경] backstoryGroup 필드가 없습니다."); return; }
        p.stringValue = Group;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(fac);
    }

    // **목록에 없는 옛 고블린 배경은 지운다.** 이름 규칙을 바꾸거나 항목을 빼면 옛 파일이
    // 그대로 남는데, 풀은 폴더를 훑으므로(`Resources.LoadAll`) **지운 배경이 계속 나온다** —
    // 이름을 한 번 잘못 지었을 때 실제로 스물넷이 될 뻔했다.
    //
    // **`goblin` 묶음만 건드린다** — 사람 배경은 이 도구의 것이 아니다.
    static void Reconcile(Entry[] entries, string group, string tag)
    {
        var keep = new System.Collections.Generic.HashSet<string>();
        foreach (var e in entries)
            keep.Add($"Back_{(e.Slot == BackstorySlot.Childhood ? "Child" : "Adult")}_{tag}_{e.Id}");

        int gone = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:BackstoryData", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<BackstoryData>(path);
            if (data == null || data.Group != group) continue;
            if (keep.Contains(System.IO.Path.GetFileNameWithoutExtension(path))) continue;

            AssetDatabase.DeleteAsset(path);
            gone++;
        }
        if (gone > 0) Debug.Log($"[배경] '{group}' 옛 파일 {gone}개를 걷어냈습니다.");
    }

    static readonly (Entry[] entries, MerchantType kind)[] Merchants =
    {
        (MerchantWeapon,   MerchantType.Weapon),
        (MerchantArmor,    MerchantType.Armor),
        (MerchantMaterial, MerchantType.Material),
        (MerchantFood,     MerchantType.Food),
        (MerchantGeneral,  MerchantType.General),
    };

    // 파일 이름 꼬리표. **묶음 이름을 그대로 쓰지 않는다** — `Back_Adult_merchant_weapon_Forge`는
    // 길기만 하고, 폴더에서 눈에 걸리는 것은 짧은 쪽이다.
    static string Tag(MerchantType t) => t switch
    {
        MerchantType.Weapon   => "MWpn",
        MerchantType.Armor    => "MArm",
        MerchantType.Material => "MMat",
        MerchantType.Food     => "MFood",
        _                     => "MGen",
    };
}
