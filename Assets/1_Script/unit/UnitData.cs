using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StatGradeEntry
{
    public StatName Stat;
    public StatGrade Min = StatGrade.D;
    public StatGrade Max = StatGrade.D;
}

[System.Serializable]
public class StatGrowthEntry
{
    public StatName Stat;
    [Tooltip("이 스탯의 성장 속도 개별 배율. 종족 GrowthRate에 곱해짐")]
    public float Multiplier = 1f;
}

[CreateAssetMenu(fileName = "UnitData", menuName = "DungeonColony/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("식별자. 비우면 에셋 파일명을 쓴다(권장). 표시명과 달리 번역되지 않고 바뀌지 않아야 한다.")]
    public string Id;

    [Tooltip("화면에 보이는 이름. 번역 대상이므로 **식별자로 쓰지 말 것** — Id를 쓴다.")]
    public string RaceName;
    public Sprite Icon;

    [Tooltip("개체 고유 이름 후보 — **성별을 안 가리는 이름**. 비어 있으면 RaceName 사용.")]
    public List<string> NamePool = new();

    // **성별이 있는 이름은 따로 든다**(2026-09-20 — 몸에 성별이 생긴 날). 한 풀에 섞여 있으면 여자 몸에 `던컨`이 붙는다.
    // 고르는 풀 = 그 성별의 이름 + 가리지 않는 이름(`NamePool`). 비워 두면 예전처럼 `NamePool`만 쓴다.
    [Tooltip("남성 이름 후보 — `NamePool`(중성)과 합쳐서 고른다.")]
    public List<string> MaleNames = new();
    [Tooltip("여성 이름 후보 — `NamePool`(중성)과 합쳐서 고른다.")]
    public List<string> FemaleNames = new();

    // 이 성별이 고를 수 있는 이름. 성별을 모르면(늑대·골렘) 중성 풀 그대로다.
    public List<string> NamesFor(Gender? gender)
    {
        var gendered = gender == Gender.Female ? FemaleNames : gender == Gender.Male ? MaleNames : null;
        if (gendered == null || gendered.Count == 0) return NamePool;
        var all = new List<string>(gendered);
        if (NamePool != null) all.AddRange(NamePool);
        return all;
    }

    // **여기에 아이템은 없다.** 무엇을 지니고 무엇을 떨구는지는 전부 `LoadoutData`(`UnitBrain.loadout`)가
    // 든다 — 늑대 가죽까지 포함해서. 한때 "몸에서 나오는 것(가죽)은 종족, 지니는 것(무기)은 역할"로
    // 갈라 두 벌을 굴렸는데, 그건 **서사적 구분이지 기계적 구분이 아니다**: 둘 다 "죽으면 나오는
    // 아이템"이고, 답이 두 벌이면 "이 적이 뭘 떨구나"에 답하려고 에셋 두 개를 열어 **더해야** 한다.
    // 이 저장소가 `CarryCapacity`·표시명·`MaterialPool`에서 반복해서 배운 것과 같다.

    public float MaxHP      = 100f;
    public float MoveSpeed  = 4f;

    [Header("전투")]
    public float AttackDamage  = 10f;
    public float AttackRange   = 1.5f;
    public float DetectRange   = 10f;

    // **밤눈** — 어두운 때에 감지 범위가 얼마나 남는가(`Vision`).
    //
    // 0이면 밤에 남들처럼 안 보이고(배율 그대로), 1이면 **밤에도 낮처럼 본다.** 밤을 모두에게
    // 똑같이 걸면 세상이 흐려진 것일 뿐이지만, 언데드·야수가 밤에도 보면 밤 습격이 실제로
    // 저쪽에 유리한 판이 된다 — 밤이 "불편한 시간"이 아니라 **"저들의 시간"**이 되는 자리다.
    [Range(0f, 1f)]
    public float NightVision = 0f;
    // 전투 성향(교전/도주)·유휴 배회는 종족이 아니라 개체의 역할이므로 여기가 아니라
    // AI 프로필(AiProfileData SO, UnitBrain.aiProfile에 연결)에 있다 — 같은 "사람"이라도 마을사람/경비병이 다르다.

    // **기절하지 않는 몸.**
    //
    // 기절은 *의식을 잃는 것*이라(`BodyPartSystem.KnockOut`) 해골에게는 성립하지 않는다 —
    // 잃을 의식도, 빠져나갈 피도 없다. 켜면 쓰러지는 구간을 건너뛰고 **그 자리에서 부서진다**.
    //
    // **딸려 오는 것을 알고 켤 것**: 이 게임에서 붙잡기·회유·몸값은 전부 기절에서 시작한다
    // (`PawnCarry.CanCarry`는 남이면 기절을 요구한다). 그러니 이걸 켠 종족은 **산 채로 잡을 수
    // 없다** — 해골을 갖고 싶으면 붙잡는 것이 아니라 **뼈로 빚는다**(핵 소환 25). 그편이
    // 뼈로 닫히는 고리와도 맞는다.
    //
    // **일으켜진 몸은 종족과 무관하게 이쪽이 된다**(`UnitBrain.Rise`) — 좀비의 종족은 여전히
    // 사람이라 여기에 안 적히지만, 이미 한 번 죽은 것이 다시 정신을 잃을 수는 없다.
    [Tooltip("**기절하지 않는다 — 쓰러지지 않고 그 자리에서 부서진다**(해골 등). " +
             "붙잡기·회유·몸값이 전부 기절에서 시작하므로, 켜면 그 종족은 산 채로 못 잡는다.")]
    public bool IsUndead;

    // **살아 있지 않은 몸 — 골렘·기계**(2026-09-04).
    //
    // **`IsUndead`와 다른 물음이다.** 저쪽은 *한 번 죽은 것인가*(신성 특효 · 기절 없음 ·
    // 산 채로 못 잡음)를 답하고, 여기는 *애초에 생물이 아닌가*를 답한다. 골렘은 죽은 적이
    // 없으므로 성물에 특히 약할 이유가 없고, 그래서 두 값을 합칠 수 없다.
    //
    // 켜면 **먹지도 자지도 않는다 — 대신 에너지를 채운다**(`Needs.BodyOf` → `BodyKind.Construct`).
    // 그전에는 골렘이 밥을 먹고 침대에 눕고 **창고가 비면 굶어 죽었다.**
    //
    // **판정은 `Needs.Runs`가 한다** — 이 값을 밖에서 직접 물어 축을 가르지 말 것(몸이 늘 때
    // 그 자리가 뒤처진다).
    [Tooltip("**살아 있지 않은 몸 — 골렘·기계.** 켜면 허기·피로가 안 돌고 대신 에너지를 " +
             "충전대에서 채운다. 언데드(IsUndead)와 다른 축이다.")]
    public bool IsConstruct;

    // **타고난 저항**(2026-08-23). 갑옷과 더해진다.
    //
    // **신성 특효는 여기 안 적는다** — 그건 `UnitBrain.IsUndead`가 답한다(해골 종족 ∥
    // **일으켜진 몸**). 표에 적으면 일어난 동료는 사람 종족이라 표에 없어서 **성물이 안 듣는다.**
    [Tooltip("이 종족이 타고난 원소 저항. 음수는 취약. 비우면 전부 0.")]
    public List<DamageResistance> Resistances = new();


    // **살아온 내력이 있는 종족인가.**
    //
    // 켜면 태어날 때 유년기·성년기 배경을 하나씩 굴려 갖는다(`BackstoryPool`).
    //
    // **판정을 종족에 두는 이유**: 배경마다 "어느 종족이 받나"를 적게 하면 배경을 하나 늘릴
    // 때마다 저작할 것이 늘고, 종족을 하나 늘리면 **배경 전부를 고쳐야 한다.** 물음은
    // *"이 종족이 사람처럼 살아왔나"* 하나뿐이라 답도 한 곳이면 된다.
    [Tooltip("**성장 배경(유년기·성년기)을 갖는가.** 사람 종족만 켠다 — 짐승·해골에게는 " +
             "살아온 내력이라는 것이 없다.")]
    public bool UsesBackstory;

    [Header("등급 (기본)")]
    [Tooltip("오버라이드되지 않은 모든 스탯의 기본 등급 범위")]
    public StatGrade DefaultMinGrade = StatGrade.D;
    public StatGrade DefaultMaxGrade = StatGrade.D;

    [Header("등급 오버라이드 (스탯별)")]
    [Tooltip("특정 스탯에 다른 등급 범위 지정. Min==Max면 고정값")]
    public List<StatGradeEntry> StatGrades = new();

    [Header("성장 속도 (스탯별)")]
    [Tooltip("이 종족이 특정 스탯만 빠르게/느리게 성장. 목록에 없는 스탯은 기본(1.0)")]
    public List<StatGrowthEntry> StatGrowthRates = new();

    // ── 도축 ──────────────────────────────────────────────────────────────────
    //
    // **몸에서 나오는 것은 종족이 든다 — 지니던 것(`LoadoutData.Loot`)과 다른 축이다.**
    //
    // 한때 늑대 가죽도 `Loadout`에 적었고, 그때 이유는 *"둘 다 죽으면 나오는 아이템이라 답이
    // 두 벌이면 어긋난다"*였다. 도축이 생기면서 **시점이 갈렸다**: 지니던 것은 죽을 때
    // 떨어지고, 몸에서 나오는 것은 **도축해야** 나온다. 그러면 종족이 드는 것이 맞다 —
    // 늑대 가죽은 그 짐승의 몸이지 주머니 속 물건이 아니고, 같은 `Loadout`을 쓰더라도
    // 종족이 다르면 나오는 것이 달라야 한다.
    //
    // **가죽·고기는 여기, 뼈는 따로.** 뼈는 삭아도 남으므로 단계를 안 가리고 나오지만
    // 살점은 썩으면 못 쓴다 — 그 갈림은 `Butchering`이 한 곳에서 답한다.
    [Header("도축")]
    [Tooltip("몸에서 나오는 것(고기·가죽 등). **썩으면 음식은 안 나온다**(`Butchering`).")]
    public List<LootEntry> ButcherYield = new();

    [Tooltip("뼈. 삭아도 남으므로 **단계를 안 가리고** 나온다.")]
    // **둘 다 비우면 도축 자체가 안 뜬다**(`Butchering.HasYield` — 줄은 남되 흐려진다).
    // 무엇이 나올지는 종족이 저작한다 — 사람도 예외가 아니다(생가죽·생고기·뼈).
    public List<LootEntry> BoneYield = new();

    public float GetGrowthRate(StatName stat)
    {
        for (int i = 0; i < StatGrowthRates.Count; i++)
            if (StatGrowthRates[i].Stat == stat) return StatGrowthRates[i].Multiplier;
        return 1f;
    }

    public (StatGrade min, StatGrade max) GetGradeRange(StatName stat)
    {
        for (int i = 0; i < StatGrades.Count; i++)
            if (StatGrades[i].Stat == stat) return (StatGrades[i].Min, StatGrades[i].Max);
        return (DefaultMinGrade, DefaultMaxGrade);
    }

    // **만든 것은 굴리지 않는다**(2026-08-28, 사용자 결정).
    //
    // 하수인은 태어나는 것이 아니라 **핵이 만드는 것**이다. 그런데 소환할 때마다 스탯이
    // 굴려지면 *"마음에 드는 놈이 나올 때까지 다시 소환한다"*가 성립한다 — 그건 결정이
    // 아니라 **뽑기**이고, 이 저장소가 저작을 두고 이미 한 번 내린 판단과 같다
    // (*"원하는 조합이 나올 때까지 숫자를 바꿔 돌리는 건 저작이 아니라 뽑기다"*).
    //
    // 게다가 하수인은 **성장도 안 한다**(`UnitStats.IsLocked`). 굴린 값이 영영 그 값이니,
    // 나쁘게 나온 골렘은 재료만 버린 것이 된다. **굴림은 자라는 것에게만 뜻이 있다.**
    //
    // 그래서 꺼 두면 **범위의 한가운데**를 쓴다. 저작한 범위를 그대로 두고도 값이 하나로
    // 정해지므로, 종족 에셋을 다시 적을 필요가 없다(그리고 범위는 여전히 *"이 종족은
    // 이쯤이다"*를 적어 두는 자리로 남는다).
    [Tooltip("태어날 때 스탯을 범위 안에서 굴리는가. 끄면 **범위의 한가운데**로 고정된다 — " +
             "하수인처럼 만들어 내는 것은 끄는 쪽이다(소환이 뽑기가 되지 않게).")]
    public bool RollStats = true;

    public StatGrade RollGrade(StatName stat)
    {
        var (min, max) = GetGradeRange(stat);
        if (RollStats) return StatGradeExt.RandomBetween(min, max);

        return (StatGrade)(((int)min + (int)max) / 2);
    }

    // **장비를 낄 수 있는가.** 골렘·짐승처럼 손이 없거나 만든 것에게는 검을 쥐여 줄 수
    // 없어야 한다 — 낄 수 있으면 화면에 칸이 뜨고, 칸이 뜨면 언젠가 무엇이 들어간다.
    //
    // 종족이 답한다(개체가 아니라) — *"이 종족은 도구를 쓰는가"*는 타고난 것이다.
    [Tooltip("장비를 낄 수 있는가. 끄면 장비칸이 뜨지 않고 어떤 길로도 안 끼워진다 " +
             "(골렘·짐승). 맨몸 피해는 그대로 `AttackDamage`가 든다.")]
    public bool CanEquip = true;

    // **이 종족이 아예 못 하는 일.** 성장 배경의 무능력과 **같은 물음**이라 같은 자리로
    // 흘러간다(`UnitBrain.ApplyJobBlocks` → `WorkSettings.SetBlocked`) — 칸에 `×`로 뜨고
    // 사람이 못 켠다.
    //
    // **등급을 바닥에 깔아 두는 것과 다르다.** F−는 *"아주 못한다"*라서 다른 폰이 없으면
    // 그 폰이 가고, 화면에도 켤 수 있는 칸으로 뜬다. 골렘에게 요리는 서툰 것이 아니라
    // **못 하는 것**이다.
    [Tooltip("이 종족이 아예 못 하는 작업. 성장 배경의 무능력과 같은 곳으로 들어간다 " +
             "(칸에 ×로 뜨고 켤 수 없다).")]
    public List<JobCategory> DisabledJobs = new();

    // **길들일 수 있는 짐승인가**(2026-10-01, 사용자: *"개미 같은 동물한테 운반할 수 있게 마차 같은거를 붙여"*).
    // 우리에 가둔 이 종족은 조련으로 돌려세운다(`Conversion.CanTame` — 사람의 설득과 나란한 길). 넘어오면 **가축**이다:
    // 일은 운반뿐이고, 수레를 단 동안만 명령을 받는다(`UnitBrain.IsTamedBeast` · `PawnCart`).
    [Tooltip("길들일 수 있는 짐승 — 우리에 가두면 조련(간수와 나란한 일)으로 돌려세운다. 넘어오면 가축.")]
    public bool Tameable = false;
}
