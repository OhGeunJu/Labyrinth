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

    [Tooltip("개체 고유 이름 후보. 소환 시 랜덤 1개 선택. 비어 있으면 RaceName 사용.")]
    public List<string> NamePool = new();

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

    public StatGrade RollGrade(StatName stat)
    {
        var (min, max) = GetGradeRange(stat);
        return StatGradeExt.RandomBetween(min, max);
    }
}
