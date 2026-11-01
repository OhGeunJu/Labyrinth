using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// **큰 종족 셋 — 오크 · 오우거 · 트롤**(2026-10-28, 사용자: *"오크 1.25배 큰 크기? 오우거 트롤 2배 큰 크기"*).
//
// 종족(`UnitData`)과 적 프리팹 한 벌씩을 세운다. 오우거 · 트롤의 몸은 사람 몸이 아니라 따로 빚는다(`ChibiPawnCreator.Brutes` —
// 끝에 부르는 `Pawn/3`이 굽는다). **몸집 · 휘두르기 · 체질은 종족 칸**이다(`BodySize` · `Regeneration`) —
// 프리팹은 사람 몸(`Enemy_Bandit`)을 베끼고 `Model`을 종족 크기로 구워 둔다(에디터에서도 큰 몸으로 보이게 — 10-31).
// 판에서는 `BodySize.Apply`가 종족 값을 다시 넣는다(곱이 아니라 두 번 걸리지 않는다).
// 성향은 고블린 프리팹의 것을 물린다 — 다르게 하려면 프리팹에서 바꿀 것.
//
// **세력 · 배경 · 지닐 것은 종족마다 제 것이다**(2026-11-01, 사용자: *"세력이 다 고블린으로 떠 각 캐릭에 맞는 배경이랑 세력
// 생성해줘"*). 성격은 사용자가 정했다:
// - **오크** — 싸움을 좋아하고 명예가 있다(일대일 결투 · 약한 것을 치지 않는다). 몸값을 받고 놓아준다 · 적의가 셋 중 가장 옅다.
// - **오우거** — 머리가 있고 동족끼리 산다(족장 · 거래 · 저장). 덩치 큰 괴물이지만 셈을 한다 — 인지력이 셋 중 가장 높다.
// - **트롤** — 짐승에 가까운 괴물. 굴에 살고 둔기를 휘두르고 산 채로 먹는다. 누구와도 안 섞인다 · 몸값도 없다.
// `Faction_Orc/Ogre/Troll` + 배경 묶음 `orc` · `ogre` · `troll`(묶음마다 이끄는 이력 하나 — `Leads`) + `Loadout_Orc/Ogre/Troll`.
// 셋은 서로도 적이다. **고블린을 적으로 적어 둔 세력**(내 편 · 변방민 · 용병 · 산적)에도 셋을 적으로 적는다 —
// 내 편의 기본 태도가 중립이라 안 적으면 먼저 안 친다. 오우거 · 트롤 몸에는 방어구가 안 보이므로 지닐 것도 무기뿐이다.
//
// **어디에 나오나는 여기서 정하지 않는다** — 바이옴 표(`BiomeContentData.WildSpawns`)와 소굴은 사용자가 저작한다.
//
// 이미 있으면 **값만 다시 적는다**(guid를 지킨다 — 소굴 · 표가 guid로 가리킨다). 끝에 `Pawn/3`을 불러 살을 입힌다
// (옷 색 · 체형은 `ChibiPawnCreator.Outfits`에 있다).
public static class BigRaceCreator
{
    const string UnitDir   = "Assets/Resources/Data/Units";

    const string HumanData = UnitDir + "/Human.asset";
    static readonly string BodyFrom = PawnPrefabPaths.PathOf("Enemy_Bandit");    // 사람 크기의 치비 몸
    static readonly string GangFrom = PawnPrefabPaths.PathOf("Enemy_Goblin");    // 성향
    const string FactionDir = "Assets/Resources/Data/Factions";
    const string StoryDir   = "Assets/Resources/Data/Backstories";
    const string LoadoutDir = "Assets/5_Data/Loadout";

    readonly struct Race
    {
        public readonly string Key, Name;
        public readonly float Scale, Hp, Speed, Damage;
        public readonly int Cleave;
        public readonly float Regen, SlowChance;
        public readonly StatGrade Grade, Strong, Mind;
        public readonly string[] Names;
        public readonly WeaponType[] Forbidden;
        public readonly Color32[] Tones;
        public readonly string Kin;                    // 갈래의 뿌리 — 세력 · 배경 · 지닐 것 · 도축은 뿌리 것을 쓴다(파랑 · 붉은 오우거 → Ogre)
        public readonly (DamageType type, float amount)[] Resist;

        public Race(string key, string name, float scale, float hp, float speed, float damage, int cleave,
                    float regen, float slowChance, StatGrade grade, StatGrade strong, StatGrade mind, WeaponType[] forbidden,
                    string[] names, Color32[] tones = null, string kin = null, (DamageType, float)[] resist = null)
        {
            Tones = tones ?? new Color32[0];
            Kin = kin ?? key;
            Resist = resist ?? new (DamageType, float)[0];
            Key = key; Name = name; Scale = scale; Hp = hp; Speed = speed; Damage = damage; Cleave = cleave;
            Regen = regen; SlowChance = slowChance; Grade = grade; Strong = strong; Mind = mind; Forbidden = forbidden; Names = names;
        }
    }

    static readonly string[] OrcNames = { "그로마크", "우르각", "볼그", "가즈낙", "두르크", "모르곤", "라그낙", "스노가", "타르쉬", "우그락" };

    // **얼굴 문신 — 부족마다**(`UnitData.TattooChance` · `InkTones` · `LeaderInk` → `MarkingLook`). 대장은 늘 문신이다(`LeaderTattoo`).
    //   (대장이 아닌 개체가 지닐 확률, 문신 색 후보, 대장 색 — 비우면 후보에서)
    static readonly Dictionary<string, (float Chance, Color32[] Inks, Color32? Leader)> Marks = new()
    {
        ["Orc"]      = (0f,   new Color32[] { new(0x9A, 0x1F, 0x1A, 0xFF) }, new Color32(0x9A, 0x1F, 0x1A, 0xFF)),   // 대장만 빨간
        ["OrcSand"]  = (0.6f, new Color32[] { new(0xDB, 0xD6, 0xC2, 0xFF) }, null),                                // 흰(뼈색)이 흔하다
        ["OrcFrost"] = (0f,   new Color32[] { new(0x2E, 0x5C, 0xB8, 0xFF) }, new Color32(0x2E, 0x5C, 0xB8, 0xFF)),   // 대장만 · 파랑(사용자)
        ["OrcFlame"] = (0.5f, new Color32[] { new(0xE0, 0x6A, 0x24, 0xFF), new(0xC8, 0x32, 0x1E, 0xFF) }, new Color32(0xF0, 0x8C, 0x2A, 0xFF)),   // 잉걸 주황 · 붉은 — 검은 살에 산다
    };

    // 큰 손으로는 활시위 · 석궁 · 지팡이를 못 다룬다. 오크는 사람만 한 손이라 다 쥔다.
    static readonly WeaponType[] BigHands = { WeaponType.Bow, WeaponType.Crossbow, WeaponType.Staff };

    // **트롤이 오우거보다 세다**(2026-11-01, 사용자) — 체력 · 피해 · 힘이 위고 아물기까지 한다. 대신 인지력이 바닥이고 불에 아묾이 멈춘다.
    static readonly Race[] Races =
    {
        //        키        이름     크기  체력  속도  피해 휩쓸기 아묾  경직  등급            힘·맷집          인지력
        // **오크는 사는 곳마다 부족이 다르다**(2026-11-01, 사용자: *"나오는 곳에 따라 부족이 달라서 뭐 색이라던지 타투라던지가 다르게"*).
        // 같은 전쟁무리라 세력 · 배경 · 지닐 것 · 도축은 오크 것(`Kin`). 문신 규칙은 아래 `Marks`.
        //   - 일반(지저밀림) — 초록 · 대장만 빨간 문신
        new Race("Orc",   "오크",   1.15f, 130f, 4.0f, 13f, 0, 0f,   0f,    StatGrade.D,    StatGrade.C,      StatGrade.D,
                 new WeaponType[0], OrcNames,
                 new Color32[] { new(0x6E, 0x8A, 0x52, 0xFF), new(0x5E, 0x7A, 0x44, 0xFF), new(0x7A, 0x92, 0x5A, 0xFF), new(0x64, 0x7C, 0x4A, 0xFF) }),
        //   - 모래 부족(모래굴) — 황토 · 갈색 · 흰(뼈색) 문신이 흔하다
        new Race("OrcSand",  "모래 부족 오크", 1.15f, 130f, 4.0f, 13f, 0, 0f, 0f, StatGrade.D,  StatGrade.C,      StatGrade.D,
                 new WeaponType[0], OrcNames,
                 new Color32[] { new(0xA8, 0x8A, 0x5A, 0xFF), new(0x9A, 0x7A, 0x4E, 0xFF), new(0xB4, 0x96, 0x60, 0xFF), new(0x8C, 0x70, 0x50, 0xFF) },
                 kin: "Orc"),
        //   - 서리 부족(빙하굴 — 3층) — 붉은 갈색 살 · 대장만 파란 문신(사용자: *"서리는 파랑으로"* — 문신 색) · 3층이라 더 세다(사용자) · 찬 물에 강하고 불에 약하다
        new Race("OrcFrost", "서리 부족 오크", 1.15f, 175f, 4.0f, 18f, 0, 0f, 0f, StatGrade.DPlus, StatGrade.CPlus, StatGrade.D,
                 new WeaponType[0], OrcNames,
                 new Color32[] { new(0x9A, 0x5A, 0x44, 0xFF), new(0x8A, 0x4E, 0x3C, 0xFF), new(0xA8, 0x6A, 0x50, 0xFF), new(0x7E, 0x4A, 0x3A, 0xFF) },
                 kin: "Orc", resist: new[] { (DamageType.Water, 0.4f), (DamageType.Fire, -0.3f) }),
        //   - 용암 부족(용암층 — 3층) — 잿빛 살 두 갈래(사용자: *"용암층에도 검은색 오크"* → 새까만 것은 너무 검다 · 옅은 잿빛 · 회색 섞인 짙은 검정) · 잉걸 주황 문신이 흔하다 · 서리 부족과 같은 세기 · 불에 강하고 물에 약하다
        new Race("OrcFlame", "용암 부족 오크", 1.15f, 175f, 4.0f, 18f, 0, 0f, 0f, StatGrade.DPlus, StatGrade.CPlus, StatGrade.D,
                 new WeaponType[0], OrcNames,
                 new Color32[] { new(0xA6, 0xA2, 0x94, 0xFF), new(0x96, 0x92, 0x86, 0xFF),     // 옅은 잿빛(따뜻한 회색)
                                 new(0x52, 0x54, 0x58, 0xFF), new(0x5E, 0x60, 0x64, 0xFF) },   // 회색 섞인 짙은 검정(슬레이트)
                 kin: "Orc", resist: new[] { (DamageType.Fire, 0.5f), (DamageType.Water, -0.3f) }),
        new Race("Ogre",  "오우거", 2.0f,  260f, 3.4f, 22f, 2, 0f,   0.35f, StatGrade.DMinus, StatGrade.B,     StatGrade.C,
                 BigHands,
                 new[] { "굼보", "두그", "모로", "부르가", "오그", "툼브", "흐룸", "고르바", "바룸", "둠두" },
                 // 오우거는 개체마다 살색이 다르다 — **살색 계통**(2026-11-01, 사용자: *"초록 오우거는 말이 안되는거 같은데 살색 계통으로"*):
                 // 그을린 · 옅은 복숭아 · 불그레한 · 짙은 갈색 · 잿빛 베이지. 푸른 · 보랏빛은 빙하굴로, 붉은 · 숯빛 · 주황은 용암층으로 갈라 냈다(아래 둘).
                 new Color32[] { new(0xC8, 0x98, 0x7A, 0xFF), new(0xE0, 0xB4, 0x9A, 0xFF), new(0xB4, 0x7A, 0x62, 0xFF),
                                 new(0x8E, 0x66, 0x50, 0xFF), new(0xB4, 0xA0, 0x8E, 0xFF) }),
        // **파랑 오우거 · 붉은 오우거**(2026-11-01, 사용자: *"빙하굴은 파랑 오우거 용암층은 붉은 오우거"*) — 3층에 사는 갈래라
        // **오우거보다 세다**(사용자: *"3층 오우거들은 뭐가 특이한 거니깐 더 쌔야"*) — 트롤과 같은 층의 세기(체력은 조금 위 · 대신 안 아문다).
        // 사는 곳의 원소를 견디고 맞은편에 약하다. 살색은 두 갈래씩(사용자: *"두 종류의 색이 나오면"*) — 빙하는 푸른 · 보랏빛, 용암은 붉은 · 숯빛 · 주황(셋).
        // 같은 일족이라 세력 · 배경 · 지닐 것 · 도축은 오우거 것(`Kin`).
        new Race("OgreFrost", "파랑 오우거", 2.0f, 340f, 3.4f, 26f, 2, 0f, 0.35f, StatGrade.D,      StatGrade.BPlus,  StatGrade.C,
                 BigHands,
                 new[] { "굼보", "두그", "모로", "부르가", "오그", "툼브", "흐룸", "고르바", "바룸", "둠두" },
                 new Color32[] { new(0x6E, 0x8F, 0xC8, 0xFF), new(0x4E, 0x68, 0xA0, 0xFF),     // 푸른
                                 new(0x8A, 0x78, 0x98, 0xFF), new(0x9C, 0x8C, 0xB8, 0xFF) },   // 보랏빛
                 kin: "Ogre", resist: new[] { (DamageType.Water, 0.4f), (DamageType.Fire, -0.3f) }),
        new Race("OgreFlame", "붉은 오우거", 2.0f, 340f, 3.4f, 26f, 2, 0f, 0.35f, StatGrade.D,      StatGrade.BPlus,  StatGrade.C,
                 BigHands,
                 new[] { "굼보", "두그", "모로", "부르가", "오그", "툼브", "흐룸", "고르바", "바룸", "둠두" },
                 new Color32[] { new(0xB0, 0x4C, 0x3A, 0xFF), new(0x8C, 0x3A, 0x2E, 0xFF),     // 붉은
                                 new(0x4A, 0x3E, 0x3C, 0xFF), new(0x5E, 0x4A, 0x44, 0xFF),     // 숯빛(식은 용암 · 재)
                                 new(0xC8, 0x6A, 0x2E, 0xFF), new(0xD8, 0x84, 0x3C, 0xFF) },   // 주황(달군 바위 · 사용자)
                 kin: "Ogre", resist: new[] { (DamageType.Fire, 0.5f), (DamageType.Water, -0.3f) }),
        new Race("Troll", "트롤",   2.0f,  320f, 3.8f, 26f, 2, 0.6f, 0.25f, StatGrade.D,      StatGrade.BPlus,  StatGrade.EMinus,
                 BigHands,
                 new[] { "스크라그", "우르스", "그림", "호르크", "자브", "트룩", "브락", "스날", "무르그", "크로그" },
                 // 트롤은 푸른빛과 회색 안에서만 — 푸른 회색 · 슬레이트 · 찬 회색 · 어두운 청색
                 new Color32[] { new(0x5E, 0x6A, 0x7A, 0xFF), new(0x58, 0x60, 0x6A, 0xFF), new(0x7A, 0x80, 0x86, 0xFF), new(0x4E, 0x5E, 0x80, 0xFF) }),
    };

    // ── 세력 ──────────────────────────────────────────────────────────────
    readonly struct Clan
    {
        public readonly string Key, Name, Group, Kin;
        public readonly float  Favor;
        public readonly bool   Ransom;
        public Clan(string key, string name, string group, float favor, bool ransom, string kin = null)
        { Key = key; Name = name; Group = group; Favor = favor; Ransom = ransom; Kin = kin ?? key; }
    }

    // **부족마다 세력이 따로다**(2026-11-01, 사용자: *"다른 부족마다 세력이 다르게 해줘 오우거도"*). 프리팹 · 무리는 제 키의 세력을 쓰고
    // (없으면 뿌리 — `Kin`), 배경 묶음은 갈래(`orc.frost` — 뿌리의 이력과 제 이력에서 함께 뽑는다, `BackstoryPool.InGroup`).
    static readonly Clan[] Clans =
    {
        //        키           이름                 배경          첫 우호  몸값
        new Clan("Orc",       "오크 전쟁무리",     "orc",        -50f,  true),    // 명예가 있다 — 말이 통한다
        new Clan("OrcSand",   "모래 부족",         "orc.sand",   -50f,  true,  kin: "Orc"),
        new Clan("OrcFrost",  "서리 부족",         "orc.frost",  -50f,  true,  kin: "Orc"),
        new Clan("OrcFlame",  "용암 부족",         "orc.flame",  -50f,  true,  kin: "Orc"),
        new Clan("Ogre",      "오우거 일족",       "ogre",       -65f,  true),    // 셈을 한다 — 사로잡은 것도 값을 매긴다
        new Clan("OgreFrost", "파랑 오우거 일족",  "ogre.frost", -65f,  true,  kin: "Ogre"),
        new Clan("OgreFlame", "붉은 오우거 일족",  "ogre.flame", -65f,  true,  kin: "Ogre"),
        new Clan("Troll",     "트롤",              "troll",      -100f, false),   // 먹이일 뿐이다
    };

    // 같은 뿌리끼리의 사이 — 오크 부족끼리는 중립(먼저 치지 않는다) · 오우거 일족끼리는 아군(동족을 챙긴다 — 사용자가 정한 성격).
    // 다른 종족끼리는 예전처럼 적이다.
    static readonly Dictionary<string, FactionStance> KinStance = new() { ["Orc"] = FactionStance.Neutral, ["Ogre"] = FactionStance.Ally };

    // 셋을 적으로 적는 세력 — 고블린을 적으로 적어 둔 곳과 같다.
    static readonly string[] HatedBy = { "Player", "Freefolk", "Mercenary", "Bandit" };

    // ── 배경 ──────────────────────────────────────────────────────────────
    readonly struct Story
    {
        public readonly string Group, File, Title, Text;
        public readonly BackstorySlot Slot;
        public readonly float Weight;
        public readonly bool  Leads;
        public readonly (StatName stat, float delta)[] Offsets;
        public Story(string group, BackstorySlot slot, string file, string title, float weight, string text,
                     (StatName, float)[] offsets, bool leads = false)
        { Group = group; Slot = slot; File = file; Title = title; Weight = weight; Text = text; Offsets = offsets; Leads = leads; }
    }

    const BackstorySlot Child = BackstorySlot.Childhood, Adult = BackstorySlot.Adulthood;

    // **족장은 부족에 하나다**(2026-11-02, 사용자: *"족장은 웬만해선 안나와야하는 거 아니야? 족장을 부족 안에 딱 한명"*). 그래서 이끄는 이력이
    // 둘이다 — **무리 우두머리**(무게 1 · 떼 · 습격대가 세우는 대장 — `BandLeader` → `BackstoryPool.LeaderOf`)와 **족장**(무게 0 — 굴려서도
    // 대장으로도 안 나온다 · 저작한 한 사람만: `5_Data/PawnSpec/Chief_*`를 그 부족 프리팹의 `pawnSpec`에 물린다 — `EnsureChiefs`).
    // 둘 다 `Leads`라 대장 문신(`MarkingLook`)은 같이 받는다.
    static readonly Story[] Stories =
    {
        // 오크 — 싸움을 좋아하고 명예가 있다 · 일대일로 겨루고 진 상대를 욕보이지 않는다
        new Story("orc", Child, "Orc_Pit",       "싸움 구덩이",  1.2f, "어린 것끼리 구덩이에 넣고 맨손으로 붙였다. 지고 운 놈은 없었다.",
                  new[] { (StatName.MeleeAttack, 7f), (StatName.Toughness, 5f), (StatName.Social, -5f) }),
        new Story("orc", Child, "Orc_FirstHunt", "첫 사냥",      1.0f, "혼자 굴 짐승을 잡아 와야 어른 자리에 앉았다. 그는 이틀 만에 돌아왔다.",
                  new[] { (StatName.Javelin, 7f), (StatName.Athletics, 5f), (StatName.Research, -5f) }),
        new Story("orc", Child, "Orc_Fireside",  "불가의 노래",  0.8f, "늙은 전사들이 불가에서 옛 싸움을 읊었다. 이긴 이름보다 잘 진 이름을 더 오래 외웠다.",
                  new[] { (StatName.Social, 7f), (StatName.Cognition, 3f), (StatName.Mining, -4f) }),
        new Story("orc", Child, "Orc_Bellows",   "풀무질",       0.9f, "불 앞에서 풀무를 밟으며 컸다. 날이 서는 소리를 좋아한다.",
                  new[] { (StatName.Smelting, 7f), (StatName.WeaponCrafting, 4f), (StatName.Evasion, -4f) }),
        new Story("orc", Adult, "Orc_Warchief",  "전쟁 족장",    0f, "도전해 온 자를 모두 일대일로 꺾고 자리를 지켰다. 도전은 언제든 받는다.",
                  new[] { (StatName.MeleeAttack, 9f), (StatName.TwoHandAxe, 6f), (StatName.Social, 5f) }, leads: true),
        new Story("orc", Adult, "Orc_Warband", "전쟁무리 우두머리", 1f, "족장의 명을 받아 무리 하나를 이끈다. 무리에서 가장 먼저 도끼를 든다.",
                  new[] { (StatName.MeleeAttack, 7f), (StatName.OneHandAxe, 5f), (StatName.Social, 4f) }, leads: true),   // 무리 우두머리 — 떼마다 하나
        new Story("orc", Adult, "Orc_Duelist",   "결투꾼",       1.0f, "모욕을 받으면 결투로 갚았다. 진 상대의 목숨은 거두지 않았다.",
                  new[] { (StatName.OneHandAxe, 8f), (StatName.Defense, 6f), (StatName.Cognition, -4f) }),
        new Story("orc", Adult, "Orc_Vanguard",  "선봉",         1.4f, "맨 앞에서 가장 큰 놈에게 달려들었다. 등에 난 상처가 없다.",
                  new[] { (StatName.TwoHandAxe, 9f), (StatName.Toughness, 5f), (StatName.Medicine, -5f) }),
        new Story("orc", Adult, "Orc_Wanderer",  "떠도는 전사",  0.8f, "더 센 상대를 찾아 무리를 떠났다. 꺾은 자의 무기를 하나씩 지닌다.",
                  new[] { (StatName.Athletics, 6f), (StatName.MeleeAttack, 5f), (StatName.Growing, -5f) }),
        new Story("orc", Adult, "Orc_Smith",     "날 벼리는 손", 0.8f, "전사들의 날을 벼렸다. 무딘 도끼로 싸우는 것은 상대를 욕보이는 일이다.",
                  new[] { (StatName.WeaponCrafting, 9f), (StatName.Smelting, 5f), (StatName.Evasion, -5f) }),

        // 오우거 — 덩치 큰 괴물이지만 머리가 있다 · 동족끼리 둥지를 이루고 족장 · 곳간 · 거래가 있다
        new Story("ogre", Child, "Ogre_Brood",   "큰 둥지의 막내", 1.2f, "형제가 열이 넘는 둥지에서 컸다. 먹을 것은 셈을 할 줄 아는 놈이 더 받았다.",
                  new[] { (StatName.Strength, 7f), (StatName.Social, 4f), (StatName.Evasion, -5f) }),
        new Story("ogre", Child, "Ogre_Tally",   "족장의 셈판",    0.8f, "족장 곁에서 곳간의 고기와 쇠를 셌다. 숫자를 틀리면 맞았다.",
                  new[] { (StatName.Cognition, 7f), (StatName.Social, 4f), (StatName.MeleeAttack, -4f) }),
        new Story("ogre", Child, "Ogre_Boulder", "바위 굴리기",    1.0f, "놀이가 바위를 굴리는 것이었다. 지금도 힘으로 먼저 푼다.",
                  new[] { (StatName.Labor, 7f), (StatName.Mining, 5f), (StatName.Dexterity, -5f) }),
        new Story("ogre", Adult, "Ogre_Chief",   "둥지의 족장",    0f, "형제들을 먹이고 지켰다. 그래서 형제들이 그의 말을 듣는다.",
                  new[] { (StatName.Social, 7f), (StatName.MeleeAttack, 7f), (StatName.Strength, 5f) }, leads: true),
        new Story("ogre", Adult, "Ogre_Hunt", "사냥 우두머리", 1f, "고기를 구하러 둥지를 나선 무리를 이끈다. 몫은 그가 나눈다.",
                  new[] { (StatName.OneHandMace, 7f), (StatName.Strength, 5f), (StatName.Social, 4f) }, leads: true),   // 무리 우두머리 — 떼마다 하나
        new Story("ogre", Adult, "Ogre_Club",    "몽둥이꾼",       1.4f, "쇠를 박은 몽둥이를 쓴다. 한 번이면 끝났다.",
                  new[] { (StatName.OneHandMace, 9f), (StatName.Toughness, 5f), (StatName.Evasion, -6f) }),
        new Story("ogre", Adult, "Ogre_Larder",  "곳간지기",       0.9f, "둥지의 고기를 말리고 절여 겨울을 났다. 무엇이 얼마나 남았는지 다 안다.",
                  new[] { (StatName.Cooking, 6f), (StatName.Cognition, 6f), (StatName.Athletics, -5f) }),
        new Story("ogre", Adult, "Ogre_Trader",  "거래꾼",         0.8f, "다른 종족과 쇠와 고기를 바꿨다. 값을 깎이는 일이 드물다.",
                  new[] { (StatName.Social, 8f), (StatName.Cognition, 4f), (StatName.Toughness, -4f) }),

        // **부족 이력**(2026-11-01) — 갈래 묶음(`orc.sand` …)이라 그 부족은 뿌리(오크 · 오우거) 공통 이력과 제 이력에서 함께 뽑는다. 두목 이력도 부족마다.
        new Story("orc.sand",  Child, "OrcSand_Dune",      "모래 굴의 아이",  1.0f, "마른 굴에서 물 냄새를 맡는 법부터 배웠다. 모래 밑에 숨은 것을 먼저 찾는다.",
                  new[] { (StatName.Athletics, 6f), (StatName.Toughness, 5f), (StatName.Social, -4f) }),
        new Story("orc.sand",  Adult, "OrcSand_Bonepaint", "뼈 물감 전사",    1.0f, "싸우기 전에 뼈를 갈아 얼굴에 그린다. 그린 무늬가 많을수록 꺾은 적이 많다.",
                  new[] { (StatName.OneHandAxe, 8f), (StatName.Evasion, 5f), (StatName.Medicine, -5f) }),
        new Story("orc.sand",  Adult, "OrcSand_Raider",    "모래 습격꾼",     0.9f, "모래 바람이 불 때 들이쳤다가 바람이 그치기 전에 사라진다.",
                  new[] { (StatName.Javelin, 8f), (StatName.Athletics, 5f), (StatName.Defense, -5f) }),
        new Story("orc.sand",  Adult, "OrcSand_Chief",     "모래 부족 족장",  0f, "가장 깊은 샘을 지켜 부족을 살렸다. 샘을 노리는 자는 그 손에 죽었다.",
                  new[] { (StatName.MeleeAttack, 8f), (StatName.Toughness, 6f), (StatName.Social, 5f) }, leads: true),
        new Story("orc.sand", Adult, "OrcSand_Raidlead", "습격대장", 1f, "모래 바람을 타고 들이치는 무리를 이끈다. 물러설 때를 아는 자다.",
                  new[] { (StatName.MeleeAttack, 7f), (StatName.Javelin, 5f), (StatName.Social, 4f) }, leads: true),   // 무리 우두머리 — 떼마다 하나

        new Story("orc.frost", Child, "OrcFrost_Ice",      "얼음 굴의 아이",  1.0f, "얼음이 갈라지는 소리를 들으며 잤다. 추위에 떨지 않는다.",
                  new[] { (StatName.Toughness, 7f), (StatName.Labor, 4f), (StatName.Dexterity, -4f) }),
        new Story("orc.frost", Adult, "OrcFrost_Hunter",   "서리 사냥꾼",     1.0f, "얼음 위를 소리 없이 걸어 큰 짐승을 쫓는다. 사냥감을 먼저 기다린다.",
                  new[] { (StatName.Javelin, 8f), (StatName.Athletics, 5f), (StatName.Social, -5f) }),
        new Story("orc.frost", Adult, "OrcFrost_Wall",     "얼음벽",          0.9f, "부족의 굴 어귀를 홀로 막았다. 물러선 적이 없다.",
                  new[] { (StatName.Defense, 8f), (StatName.Toughness, 6f), (StatName.Evasion, -5f) }),
        new Story("orc.frost", Adult, "OrcFrost_Chief",    "서리 부족 족장",  0f, "가장 긴 겨울에 부족을 이끌어 아무도 굶기지 않았다. 얼굴의 푸른 무늬가 그 증표다.",
                  new[] { (StatName.MeleeAttack, 8f), (StatName.Social, 6f), (StatName.Toughness, 5f) }, leads: true),
        new Story("orc.frost", Adult, "OrcFrost_Huntlead", "서리 사냥대장", 1f, "얼음 위로 나선 무리를 이끈다. 얼굴의 푸른 무늬가 그 자리의 표시다.",
                  new[] { (StatName.MeleeAttack, 7f), (StatName.Toughness, 5f), (StatName.Social, 4f) }, leads: true),   // 무리 우두머리 — 떼마다 하나

        new Story("orc.flame", Child, "OrcFlame_Ember",    "잉걸 곁의 아이",  1.0f, "용암 강가에서 컸다. 뜨거운 돌을 맨손으로 옮긴다.",
                  new[] { (StatName.Smelting, 7f), (StatName.Toughness, 4f), (StatName.Social, -4f) }),
        new Story("orc.flame", Adult, "OrcFlame_Forge",    "용암 대장장이",   1.0f, "용암으로 쇠를 녹여 부족의 날을 벼린다. 그 도끼는 식지 않는다고들 한다.",
                  new[] { (StatName.WeaponCrafting, 9f), (StatName.Smelting, 5f), (StatName.Evasion, -5f) }),
        new Story("orc.flame", Adult, "OrcFlame_Brand",    "낙인 전사",       1.0f, "달군 쇠로 몸에 무늬를 새겼다. 아픔을 견딘 만큼 앞에 선다.",
                  new[] { (StatName.TwoHandAxe, 8f), (StatName.Toughness, 6f), (StatName.Medicine, -5f) }),
        new Story("orc.flame", Adult, "OrcFlame_Chief",    "용암 부족 족장",  0f, "용암을 건너는 길을 처음 낸 자다. 부족은 그 길로만 다닌다.",
                  new[] { (StatName.MeleeAttack, 9f), (StatName.Strength, 5f), (StatName.Social, 5f) }, leads: true),
        new Story("orc.flame", Adult, "OrcFlame_Warlead", "잉걸 전사장", 1f, "용암 강을 건너 나선 무리를 이끈다. 가장 뜨거운 날을 쥔다.",
                  new[] { (StatName.TwoHandAxe, 7f), (StatName.Toughness, 5f), (StatName.Social, 4f) }, leads: true),   // 무리 우두머리 — 떼마다 하나

        new Story("ogre.frost", Child, "OgreFrost_Snow",   "눈더미 둥지",     1.0f, "얼음 굴에 눈을 쌓아 둥지를 지었다. 형제들과 몸을 붙여 겨울을 났다.",
                  new[] { (StatName.Toughness, 7f), (StatName.Social, 4f), (StatName.Dexterity, -5f) }),
        new Story("ogre.frost", Adult, "OgreFrost_Breaker", "얼음 깨는",      1.0f, "얼어붙은 강을 몽둥이로 깨 물고기를 건졌다. 한 번에 얼음이 갈라진다.",
                  new[] { (StatName.OneHandMace, 8f), (StatName.Labor, 5f), (StatName.Evasion, -5f) }),
        new Story("ogre.frost", Adult, "OgreFrost_Chief",  "얼음 둥지 족장",  0f, "얼음 굴의 둥지들을 하나로 묶었다. 겨울마다 곳간을 나눈다.",
                  new[] { (StatName.Social, 7f), (StatName.MeleeAttack, 6f), (StatName.Toughness, 6f) }, leads: true),
        new Story("ogre.frost", Adult, "OgreFrost_Hunt", "얼음 사냥 우두머리", 1f, "얼어붙은 강으로 나선 무리를 이끈다. 깨뜨린 얼음만큼 몫을 받는다.",
                  new[] { (StatName.OneHandMace, 7f), (StatName.Toughness, 5f), (StatName.Social, 4f) }, leads: true),   // 무리 우두머리 — 떼마다 하나

        new Story("ogre.flame", Child, "OgreFlame_Ash",    "잿더미 둥지",     1.0f, "식은 용암 위에 둥지를 틀었다. 재 냄새가 몸에서 안 빠진다.",
                  new[] { (StatName.Toughness, 6f), (StatName.Strength, 5f), (StatName.Social, -4f) }),
        new Story("ogre.flame", Adult, "OgreFlame_Stoker", "불 지키는",       1.0f, "둥지의 불을 꺼뜨린 적이 없다. 고기를 굽는 일도 그의 몫이다.",
                  new[] { (StatName.Cooking, 7f), (StatName.Smelting, 5f), (StatName.Athletics, -5f) }),
        new Story("ogre.flame", Adult, "OgreFlame_Chief",  "불 둥지 족장",    0f, "용암이 넘친 해에 둥지를 옮겨 일족을 살렸다. 그 뒤로 다들 그의 셈을 믿는다.",
                  new[] { (StatName.Social, 7f), (StatName.Cognition, 5f), (StatName.MeleeAttack, 6f) }, leads: true),
        new Story("ogre.flame", Adult, "OgreFlame_Hunt", "불 사냥 우두머리", 1f, "재 덮인 굴로 나선 무리를 이끈다. 불씨를 지니고 다닌다.",
                  new[] { (StatName.OneHandMace, 7f), (StatName.Strength, 5f), (StatName.Social, 4f) }, leads: true),   // 무리 우두머리 — 떼마다 하나

        // 트롤 — 짐승에 가까운 괴물 · 굴에 산다 · 둔기를 휘두르고 산 채로 먹는다
        new Story("troll", Child, "Troll_Den",    "굴 바닥에서",    1.2f, "빛이 안 드는 굴 바닥에서 컸다. 어둠 속에서 냄새로 먹이를 찾는다.",
                  new[] { (StatName.Toughness, 7f), (StatName.Athletics, 4f), (StatName.Social, -6f) }),
        new Story("troll", Child, "Troll_Prey",   "살아 있는 먹이", 1.0f, "어미가 먹이를 죽이지 않고 던져 줬다. 잡는 법을 그렇게 배웠다.",
                  new[] { (StatName.MartialArts, 7f), (StatName.Strength, 4f), (StatName.Cooking, -6f) }),
        new Story("troll", Child, "Troll_Winter", "굶주린 겨울",    0.6f, "먹을 것이 없던 겨울에 형제가 하나씩 사라졌다. 그는 끝까지 남았다.",
                  new[] { (StatName.Toughness, 6f), (StatName.Strength, 5f), (StatName.Social, -8f) }),
        new Story("troll", Adult, "Troll_Boss",   "굴의 우두머리",  0.6f, "가장 큰 굴을 차지하고 다른 트롤을 내쫓았다. 남은 놈들은 그 뒤를 따른다.",
                  new[] { (StatName.MeleeAttack, 8f), (StatName.Strength, 6f), (StatName.Social, 3f) }, leads: true),
        new Story("troll", Adult, "Troll_Femur",  "뼈 몽둥이",      1.4f, "큰 짐승의 넓적다리뼈를 휘두른다. 부러지면 다음 뼈를 줍는다.",
                  new[] { (StatName.OneHandMace, 9f), (StatName.Toughness, 5f), (StatName.Research, -6f) }),
        new Story("troll", Adult, "Troll_Devour", "산 채로 먹는",   1.0f, "잡은 것을 굴로 끌고 가 산 채로 먹는다. 굴 어귀에 뼈가 쌓여 있다.",
                  new[] { (StatName.MartialArts, 8f), (StatName.Toughness, 5f), (StatName.Medicine, -6f) }),
        new Story("troll", Adult, "Troll_Digger", "굴을 넓히는",    0.8f, "맨손으로 바위를 긁어 굴을 넓힌다. 손톱이 돌보다 단단하다.",
                  new[] { (StatName.Mining, 8f), (StatName.Labor, 5f), (StatName.Cognition, -5f) }),
    };

    // ── 지닐 것 ────────────────────────────────────────────────────────────
    // 오크는 사람 몸이라 갑옷이 보인다 · 오우거 · 트롤은 무기와 주머니만(큰 몸에는 방어구가 안 보인다).
    static readonly Dictionary<string, (string id, float chance)[]> Gear = new()
    {
        ["Orc"]   = new[] { ("Wpn_BronzeAxe", 0.5f), ("wpn_steel_axe", 0.35f), ("Wpn_WoodShield", 0.3f),
                            ("arm_bronze_chest", 0.4f), ("arm_bronze_head", 0.3f) },
        ["Ogre"]  = new[] { ("wpn_steel_mace", 0.7f) },
        ["Troll"] = new[] { ("wpn_bone_club", 0.9f) },
    };

    static readonly Dictionary<string, (string id, float chance, int min, int max)[]> Pockets = new()
    {
        ["Orc"]   = new[] { ("food_dried_meat", 0.5f, 1, 2), ("leather", 0.4f, 1, 2) },
        ["Ogre"]  = new[] { ("food_dried_meat", 0.7f, 2, 4), ("rope", 0.3f, 1, 2), ("leather", 0.3f, 1, 3) },
        ["Troll"] = new[] { ("res_bone", 0.6f, 1, 3) },
    };

    static readonly Dictionary<string, (int min, int max)> Coins = new()
    {
        ["Orc"] = (10, 40), ["Ogre"] = (20, 70), ["Troll"] = (0, 0),
    };

    [MenuItem("DungeonColony/Pawn/9. Create Big Races (오크 · 오우거 · 트롤)")]
    public static void Create()
    {
        var human = AssetDatabase.LoadAssetAtPath<UnitData>(HumanData);
        if (human == null) { Debug.LogError($"[큰 종족] 바탕 종족이 없다 — {HumanData}"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BodyFrom) == null) { Debug.LogError($"[큰 종족] 바탕 몸이 없다 — {BodyFrom}"); return; }

        var gang = AssetDatabase.LoadAssetAtPath<GameObject>(GangFrom);
        var made = new List<string>();

        var factions = EnsureFactions();
        int stories  = EnsureStories();
        int chiefs   = EnsureChiefs();

        foreach (var r in Races)
        {
            var data = EnsureData(r, human);
            if (!factions.TryGetValue(r.Key, out var faction)) factions.TryGetValue(r.Kin, out faction);   // 부족마다 제 세력
            if (EnsurePrefab(r, data, gang, faction, EnsureLoadout(r.Kin))) made.Add($"{r.Name}({r.Scale}배)");
        }

        int bands = EnsureBands();
        AssetDatabase.SaveAssets();
        Debug.Log($"[큰 종족] 무리 {bands} · {string.Join(" · ", made)} · 세력 {factions.Count} · 배경 {stories} · 족장 명세 {chiefs} — 이어서 `Pawn/3`으로 살을 입힌다. " +
                  "**어디에 나오나는 바이옴 표 · 소굴에서 정할 것.**");

        ChibiPawnCreator.Create();
    }

    // ── 무리 ── 떠도는 떼 하나씩(`Band_Orc/Ogre/Troll`). **어디에 나오나는 정하지 않는다** — 바이옴 표(`WildSpawns`)와
    // 소굴은 사용자가 고른다. 디버그 창(`납치 시험`)이 이것으로 눈앞에 세운다. 이미 있으면 값만 다시 적는다(guid를 지킨다).
    const string BandDir = "Assets/5_Data/World/Band";
    static readonly Dictionary<string, (string Name, int Min, int Max, float Power, Color Token)> Bands = new()
    {
        ["Orc"]   = ("오크 전쟁무리", 2, 4, 1.4f, new Color(0.45f, 0.60f, 0.25f)),
        ["OrcSand"]  = ("모래 부족 전쟁무리", 2, 4, 1.4f, new Color(0.66f, 0.54f, 0.35f)),
        ["OrcFrost"] = ("서리 부족 전쟁무리", 2, 4, 2.0f, new Color(0.60f, 0.35f, 0.27f)),
        ["OrcFlame"] = ("용암 부족 전쟁무리", 2, 4, 2.0f, new Color(0.22f, 0.20f, 0.20f)),
        ["Ogre"]  = ("오우거 일족",   1, 2, 2.0f, new Color(0.70f, 0.50f, 0.30f)),
        ["Troll"] = ("굴 트롤",       1, 1, 2.8f, new Color(0.40f, 0.50f, 0.65f)),   // 오우거보다 세다(사용자 — 11-01)
        ["OgreFrost"] = ("파랑 오우거 일족", 1, 2, 2.8f, new Color(0.43f, 0.56f, 0.78f)),
        ["OgreFlame"] = ("붉은 오우거 일족", 1, 2, 2.8f, new Color(0.69f, 0.30f, 0.23f)),
    };

    // **어디에 나오나**(2026-11-01, 사용자가 정함) — 무리 에셋을 **처음 만들 때만** 이 바이옴 표에 적는다. 이미 있는 무리는
    // 안 건드린다 — 그 뒤로는 바이옴 에셋이 답이고, 사용자가 확률을 고치거나 빼도 다시 돌린 생성기가 되살리지 않는다.
    // (오크 · 오우거 · 트롤은 무리 에셋이 먼저 있었으므로 바이옴 에셋에 직접 적었다.)
    const string BiomeDir = "Assets/Resources/Data/Biomes";
    static readonly Dictionary<string, (string Biome, float Chance)[]> Homes = new()
    {
        ["OgreFrost"] = new[] { ("Biome_Glacier", 0.04f) },
        ["OgreFlame"] = new[] { ("Biome_Lava",    0.04f) },
        ["OrcFrost"]  = new[] { ("Biome_Glacier", 0.04f) },
        ["OrcFlame"]  = new[] { ("Biome_Lava",    0.04f) },
        ["OrcSand"]   = new[] { ("Biome_Desert",  0.05f) },   // 모래굴의 일반 오크 자리를 넘겨받는다(`Displaces`)
    };

    // 처음 놓을 때 그 바이옴에서 **빼는** 무리 — 부족이 갈라져 나온 자리(모래굴의 일반 오크 → 모래 부족).
    static readonly Dictionary<string, string> Displaces = new() { ["OrcSand"] = "Orc" };

    public static int EnsureBands()
    {
        int made = 0;
        foreach (var r in Races)
        {
            if (!Bands.TryGetValue(r.Key, out var b)) continue;
            var prefab  = AssetDatabase.LoadAssetAtPath<GameObject>(PawnPrefabPaths.PathOf($"Enemy_{r.Key}"));
            var faction = AssetDatabase.LoadAssetAtPath<FactionData>($"{FactionDir}/Faction_{r.Key}.asset");
            if (faction == null) faction = AssetDatabase.LoadAssetAtPath<FactionData>($"{FactionDir}/Faction_{r.Kin}.asset");
            if (prefab == null || faction == null) { Debug.LogWarning($"[큰 종족] 무리를 못 만든다 — Enemy_{r.Key} · Faction_{r.Kin}가 먼저다(`Pawn/9`)."); continue; }

            string path = $"{BandDir}/Band_{r.Key}.asset";
            var band = AssetDatabase.LoadAssetAtPath<WorldBandData>(path);
            bool fresh = band == null;
            if (fresh) { band = ScriptableObject.CreateInstance<WorldBandData>(); AssetDatabase.CreateAsset(band, path); }

            var so = new SerializedObject(band);
            so.FindProperty("bandName").stringValue        = b.Name;
            so.FindProperty("tokenColor").colorValue       = b.Token;
            so.FindProperty("faction").objectReferenceValue = faction;
            so.FindProperty("behavior").intValue           = (int)WorldBandBehavior.Roam;
            so.FindProperty("speed").floatValue            = r.Speed;
            so.FindProperty("ChaseRadius").floatValue      = 14f;
            so.FindProperty("PowerMultiplier").floatValue  = b.Power;
            var members = so.FindProperty("members");
            members.arraySize = 1;
            var m = members.GetArrayElementAtIndex(0);
            m.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            m.FindPropertyRelative("MinCount").intValue = b.Min;
            m.FindPropertyRelative("MaxCount").intValue = b.Max;
            m.FindPropertyRelative("MinGrade").intValue = (int)r.Grade;
            m.FindPropertyRelative("MaxGrade").intValue = (int)r.Grade;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(band);
            if (fresh && Homes.TryGetValue(r.Key, out var homes))
            {
                if (Displaces.TryGetValue(r.Key, out var old)) RemoveFrom(AssetDatabase.LoadAssetAtPath<WorldBandData>($"{BandDir}/Band_{old}.asset"), homes);
                PlaceIn(band, homes);
            }
            made++;
        }
        AssetDatabase.SaveAssets();
        return made;
    }

    static void RemoveFrom(WorldBandData band, (string Biome, float Chance)[] homes)
    {
        if (band == null) return;
        foreach (var (biome, _) in homes)
        {
            var b = AssetDatabase.LoadAssetAtPath<BiomeContentData>($"{BiomeDir}/{biome}.asset");
            if (b == null) continue;
            var so = new SerializedObject(b);
            var list = so.FindProperty("WildSpawns");
            for (int i = list.arraySize - 1; i >= 0; i--)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Band").objectReferenceValue == band)
                {
                    list.DeleteArrayElementAtIndex(i);
                    Debug.Log($"[큰 종족] {band.BandName} ← {biome}에서 뺐다(부족이 넘겨받는다)");
                }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(b);
        }
    }

    static void PlaceIn(WorldBandData band, (string Biome, float Chance)[] homes)
    {
        foreach (var (biome, chance) in homes)
        {
            var b = AssetDatabase.LoadAssetAtPath<BiomeContentData>($"{BiomeDir}/{biome}.asset");
            if (b == null) { Debug.LogWarning($"[큰 종족] 바이옴이 없다 — {biome}"); continue; }
            var so = new SerializedObject(b);
            var list = so.FindProperty("WildSpawns");
            bool has = false;
            for (int i = 0; i < list.arraySize; i++)
                has |= list.GetArrayElementAtIndex(i).FindPropertyRelative("Band").objectReferenceValue == band;
            if (has) continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            var e = list.GetArrayElementAtIndex(list.arraySize - 1);
            e.FindPropertyRelative("Band").objectReferenceValue = band;
            e.FindPropertyRelative("ChancePerChunk").floatValue = chance;
            e.FindPropertyRelative("RespawnDays").floatValue    = 3f;
            e.FindPropertyRelative("Recruitable").boolValue     = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(b);
            Debug.Log($"[큰 종족] {band.BandName} → {biome} (청크당 {chance:P0})");
        }
    }

    // 도축 — (고기 · 생가죽 · 뼈). 아이템은 `StreamingAssets/Data/items`(사람은 고기 2~4 · 가죽 1~3 · 뼈 1~3). 몸집만큼 많다.
    static readonly Dictionary<string, ((string Id, int Min, int Max) Meat, (string Id, int Min, int Max) Hide, (int Min, int Max) Bone)> Yields = new()
    {
        ["Orc"]   = (("food_meat_orc",   3, 5),  ("res_orchide",   1, 2), (2, 3)),
        ["Ogre"]  = (("food_meat_ogre",  8, 12), ("res_ogrehide",  2, 4), (4, 7)),
        ["Troll"] = (("food_meat_troll", 6, 10), ("res_trollhide", 2, 3), (4, 6)),
    };

    static UnitData EnsureData(Race r, UnitData human)
    {
        string path = $"{UnitDir}/{r.Key}.asset";
        var d = AssetDatabase.LoadAssetAtPath<UnitData>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<UnitData>();
            EditorUtility.CopySerialized(human, d);   // 사람의 나머지 값(도축 · 성장 · 배경)을 바탕으로
            AssetDatabase.CreateAsset(d, path);
        }

        d.name       = r.Key;
        d.Id         = r.Key;
        d.RaceName   = r.Name;
        d.NamePool   = new List<string>(r.Names);
        d.MaleNames  = new List<string>();
        d.FemaleNames = new List<string>();
        d.MaxHP        = r.Hp;
        d.MoveSpeed    = r.Speed;
        d.AttackDamage = r.Damage;

        d.DefaultMinGrade = r.Grade;
        d.DefaultMaxGrade = r.Grade;
        d.StatGrades = new List<StatGradeEntry>
        {
            new() { Stat = StatName.Strength,  Min = r.Strong, Max = r.Strong },
            new() { Stat = StatName.Toughness, Min = r.Strong, Max = r.Strong },
            new() { Stat = StatName.Cognition, Min = r.Mind,   Max = r.Mind },
        };

        d.BodyScale         = r.Scale;
        d.CleaveTargets     = r.Cleave;
        d.CleaveDamage      = 0.5f;
        d.ForbiddenWeapons  = new List<WeaponType>(r.Forbidden);
        d.RegenPerSecond    = r.Regen;
        d.Devours           = r.Kin == "Troll";   // 산 채로 먹는다(사용자 — `Abduction`) · 고블린은 Goblin.asset에 켜 두었다
        d.HitEffect         = StatusEffectType.Slowed;
        d.HitEffectChance   = r.SlowChance;
        d.HitEffectDuration = 2f;
        d.HitEffectStrength = 0.6f;
        // **도축하면 제 것이 나온다**(2026-11-01, 사용자: *"종족마다 따로 나와야지 … 서로 다른 역할을 하게"*) — 사람을 베낀 채로 두면
        // 인간 고기가 나왔다. 가죽은 무두질하면 갑옷 재료가 갈린다(오크 베기 · 오우거 둔기와 무게 · 트롤 어둠 · 땅, 불에 약함).
        var (meat, hide, bone) = Yields[r.Kin];
        d.ButcherYield = new List<LootEntry>
        {
            new() { ItemId = hide.Id, Chance = 0.9f, MinCount = hide.Min, MaxCount = hide.Max },
            new() { ItemId = meat.Id, Chance = 1f,   MinCount = meat.Min, MaxCount = meat.Max },
        };
        d.BoneYield = new List<LootEntry> { new() { ItemId = "res_bone", Chance = 1f, MinCount = bone.Min, MaxCount = bone.Max } };

        d.Resistances = new List<DamageResistance>();
        foreach (var (type, amount) in r.Resist) d.Resistances.Add(new DamageResistance { Type = type, Amount = amount });

        d.SkinTones = new List<Color>();
        foreach (var c in r.Tones) d.SkinTones.Add(c);

        d.InkTones = new List<Color>();
        d.TattooChance = 0f; d.LeaderTattoo = false; d.LeaderInk = new Color(0f, 0f, 0f, 0f);
        if (Marks.TryGetValue(r.Key, out var mark))
        {
            d.TattooChance = mark.Chance;
            d.LeaderTattoo = true;
            foreach (var c in mark.Inks) d.InkTones.Add(c);
            if (mark.Leader is Color32 lc) d.LeaderInk = lc;
        }

        EditorUtility.SetDirty(d);
        return d;
    }

    // 이미 있으면 값만 다시 적는다(guid를 지킨다 — 프리팹 · 무리가 가리킨다).
    static Dictionary<string, FactionData> EnsureFactions()
    {
        FactionData Load(string key) => AssetDatabase.LoadAssetAtPath<FactionData>($"{FactionDir}/Faction_{key}.asset");

        var made = new Dictionary<string, FactionData>();
        foreach (var c in Clans)
        {
            string path = $"{FactionDir}/Faction_{c.Key}.asset";
            var f = Load(c.Key);
            if (f == null) { f = ScriptableObject.CreateInstance<FactionData>(); AssetDatabase.CreateAsset(f, path); }
            made[c.Key] = f;
        }

        foreach (var c in Clans)
        {
            var so = new SerializedObject(made[c.Key]);
            so.FindProperty("factionName").stringValue    = c.Name;
            so.FindProperty("playerControlled").boolValue = false;
            so.FindProperty("acceptsRansom").boolValue    = c.Ransom;
            so.FindProperty("forHire").boolValue          = false;
            so.FindProperty("startingFavor").floatValue   = c.Favor;
            so.FindProperty("backstoryGroup").stringValue = c.Group;
            so.FindProperty("defaultStance").enumValueIndex = (int)FactionStance.Hostile;
            so.FindProperty("relations").ClearArray();   // 다른 종족끼리는 적이다
            if (KinStance.TryGetValue(c.Kin, out var kinStance))
                foreach (var o in Clans)
                    if (o.Key != c.Key && o.Kin == c.Kin) SetRelation(so, made[o.Key], kinStance);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (var h in HatedBy)
        {
            var f = Load(h);
            if (f == null) { Debug.LogWarning($"[큰 종족] 세력이 없다 — Faction_{h}"); continue; }
            var so = new SerializedObject(f);
            foreach (var c in Clans) SetRelation(so, made[c.Key], FactionStance.Hostile);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return made;
    }

    // 같은 상대가 있으면 그 줄을 고치고 없으면 붙인다(첫 항목이 이기므로 겹쳐 적지 않는다).
    static void SetRelation(SerializedObject so, FactionData other, FactionStance stance)
    {
        var list = so.FindProperty("relations");
        for (int i = 0; i < list.arraySize; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("Other").objectReferenceValue != other) continue;
            e.FindPropertyRelative("Stance").enumValueIndex = (int)stance;
            return;
        }
        list.InsertArrayElementAtIndex(list.arraySize);
        var n = list.GetArrayElementAtIndex(list.arraySize - 1);
        n.FindPropertyRelative("Other").objectReferenceValue = other;
        n.FindPropertyRelative("Stance").enumValueIndex      = (int)stance;
    }

    // **족장 — 부족에 한 사람**(저작). 명세 에셋만 세운다 — 어디에 두나는 사용자가 정한다(저작 씬에 그 부족 프리팹을 놓고 `pawnSpec`에 물린다).
    // 이미 있으면 이력만 다시 물린다(이름 · 등급을 고쳐 두었으면 그대로 둔다).
    const string ChiefDir = "Assets/5_Data/PawnSpec";
    static readonly (string Key, string Story)[] Chiefs =
    {
        ("Orc", "Orc_Warchief"),
        ("Ogre", "Ogre_Chief"),
        ("OrcSand", "OrcSand_Chief"),
        ("OrcFrost", "OrcFrost_Chief"),
        ("OrcFlame", "OrcFlame_Chief"),
        ("OgreFrost", "OgreFrost_Chief"),
        ("OgreFlame", "OgreFlame_Chief"),
    };

    static int EnsureChiefs()
    {
        int n = 0;
        foreach (var (key, story) in Chiefs)
        {
            var adult = AssetDatabase.LoadAssetAtPath<BackstoryData>($"{StoryDir}/Back_Adult_{story}.asset");
            if (adult == null) { Debug.LogWarning($"[큰 종족] 족장 이력이 없다 — {story}"); continue; }
            string path = $"{ChiefDir}/Chief_{key}.asset";
            var spec = AssetDatabase.LoadAssetAtPath<PawnSpecData>(path);
            bool fresh = spec == null;
            if (fresh)
            {
                PawnPrefabPaths.Ensure(ChiefDir);
                spec = ScriptableObject.CreateInstance<PawnSpecData>();
                // 처음만 — 무리 우두머리(D~D+에서 세 칸)보다 한 단 위. 고치면 그 값이 이긴다.
                foreach (var st in new[] { StatName.Strength, StatName.Toughness, StatName.MeleeAttack, StatName.Social })
                    spec.Grades.Add(new PawnSpecData.GradeFix { Stat = st, Grade = StatGrade.BPlus });
                AssetDatabase.CreateAsset(spec, path);
            }
            spec.Adulthood = adult;
            EditorUtility.SetDirty(spec);
            n++;
        }
        return n;
    }

    static int EnsureStories()
    {
        foreach (var s in Stories)
        {
            string path = $"{StoryDir}/Back_{(s.Slot == Child ? "Child" : "Adult")}_{s.File}.asset";
            var b = AssetDatabase.LoadAssetAtPath<BackstoryData>(path);
            if (b == null) { b = ScriptableObject.CreateInstance<BackstoryData>(); AssetDatabase.CreateAsset(b, path); }
            b.Slot        = s.Slot;
            b.Title       = s.Title;
            b.Description = s.Text;
            b.Weight      = s.Weight;
            b.Group       = s.Group;
            b.Leads       = s.Leads;
            b.DisabledJobs = new List<JobCategory>();
            b.StatOffsets  = new List<TraitData.StatOffset>();
            foreach (var (stat, delta) in s.Offsets) b.StatOffsets.Add(new TraitData.StatOffset { Stat = stat, Delta = delta });
            EditorUtility.SetDirty(b);
        }
        return Stories.Length;
    }

    static LoadoutData EnsureLoadout(string key)
    {
        string path = $"{LoadoutDir}/Loadout_{key}.asset";
        var l = AssetDatabase.LoadAssetAtPath<LoadoutData>(path);
        if (l == null) { l = ScriptableObject.CreateInstance<LoadoutData>(); AssetDatabase.CreateAsset(l, path); }
        l.Loadout = new List<LoadoutEntry>();
        foreach (var (id, chance) in Gear[key]) l.Loadout.Add(new LoadoutEntry { ItemId = id, Chance = chance });
        l.Loot = new List<LootEntry>();
        foreach (var (id, chance, min, max) in Pockets[key]) l.Loot.Add(new LootEntry { ItemId = id, Chance = chance, MinCount = min, MaxCount = max });
        (l.CoinDropMin, l.CoinDropMax) = Coins[key];
        EditorUtility.SetDirty(l);
        return l;
    }

    static bool EnsurePrefab(Race r, UnitData data, GameObject gang, FactionData faction, LoadoutData loadout)
    {
        string path = PawnPrefabPaths.PathOf($"Enemy_{r.Key}");
        PawnPrefabPaths.Ensure(PawnPrefabPaths.FolderOf($"Enemy_{r.Key}"));
        if (!File.Exists(path) && !AssetDatabase.CopyAsset(BodyFrom, path))
        {
            Debug.LogError($"[큰 종족] {path}를 못 만들었다.");
            return false;
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (!root.TryGetComponent(out UnitBrain brain)) { Debug.LogError($"[큰 종족] {path}에 `UnitBrain`이 없다."); return false; }

            var so = new SerializedObject(brain);
            so.FindProperty("unitData").objectReferenceValue = data;
            if (gang != null && gang.TryGetComponent(out UnitBrain gb))
            {
                var gso = new SerializedObject(gb);
                so.FindProperty("aiProfile").objectReferenceValue = gso.FindProperty("aiProfile").objectReferenceValue;
            }
            so.FindProperty("loadout").objectReferenceValue = loadout;
            so.FindProperty("pawnSpec").objectReferenceValue = null;   // 산적의 명세를 물려받지 않는다
            so.ApplyModifiedPropertiesWithoutUndo();

            if (faction != null && root.TryGetComponent(out Faction f))
            {
                var fso = new SerializedObject(f);
                fso.FindProperty("data").objectReferenceValue = faction;
                fso.ApplyModifiedPropertiesWithoutUndo();
            }

            // **크기는 종족이 준다** — 프리팹에도 같은 값을 구워 둔다(판에서는 `BodySize.Apply`가 넣는다 — 곱하지 않는다).
            var model = root.transform.Find("Model");
            if (model != null) model.localScale = Vector3.one * r.Scale;

            root.name = $"Enemy_{r.Key}";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
