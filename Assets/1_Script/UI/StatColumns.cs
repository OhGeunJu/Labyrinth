using UnityEngine;

// **스탯 창의 열 구성 — 단일 출처**(2026-08-23).
//
// 씬에 서 있는 줄과 값을 채우는 쪽(`UnitStatsUI`)이 **같은 표를 읽어야 한다.**
// (창을 세운 도구 `StatPanelSetup`은 2026-09-09에 다 쓰고 지웠다 — 이제 씬이 정본이고,
//  이 표가 바뀌면 런타임이 **줄 수가 안 맞는다고 콘솔에 적는다.**)
// 각자 목록을 들면 스탯을 하나 더할 때 한쪽만 늘어 **줄은 있는데 값이 안 들어오거나 그 반대**가
// 되고, 그 어긋남은 화면에서 빈 줄로만 보인다.
public static class StatColumns
{
    public readonly struct Column
    {
        public readonly string     Header;
        public readonly StatName[] Stats;
        public Column(string header, StatName[] stats) { Header = header; Stats = stats; }
    }

    // **순서가 곧 화면 순서다.** 0번은 왼쪽 상자, 나머지가 기술 열.
    public static readonly Column[] All =
    {
        new("속성", new[]
        {
            StatName.Strength, StatName.Toughness, StatName.Dexterity, StatName.Cognition,
        }),
        new("전투", new[]
        {
            StatName.MeleeAttack, StatName.Defense, StatName.PrecisionShooting,
            StatName.MartialArts, StatName.Evasion, StatName.Athletics,
        }),
        // 무기 종류. **한때 어느 목록에도 없었다** — 지금 든 무기 한 줄로만 떠서, 검을 익혀
        // 놓고 도끼를 들면 검 숙련도를 볼 길이 없었다.
        new("무기", new[]
        {
            StatName.OneHandSword, StatName.OneHandAxe, StatName.OneHandMace,
            StatName.TwoHandSword, StatName.TwoHandAxe, StatName.TwoHandMace,
            StatName.Polearm, StatName.Bow, StatName.Crossbow, StatName.Javelin,
            StatName.Shield,
        }),
        // 원소 일곱. 계열 셋(`ElementalMagic`·`DarkMagic`·`HolyMagic`)은 2026-08-23에 접혔다.
        new("마법", new[]
        {
            StatName.FireMagic, StatName.WaterMagic, StatName.EarthMagic, StatName.LightningMagic,
            StatName.ArcaneMagic, StatName.DarkMagic, StatName.HolyMagic,
        }),
        // **`노동`이라 부르지 않는다** — `StatName.Labor`의 표시명이 이미 `노동`이라
        // 열 이름과 그 안의 줄 하나가 같은 낱말이 된다. 이 저장소는 `JobCategory`를 이미
        // **작업**이라 부르고(P패널 "작업 설정") `CraftSkill`을 **제작**이라 부른다 —
        // 제작도 작업이긴 하지만 **제작은 이미 좁은 뜻으로 자리를 잡았고**(레시피로 물건을
        // 만드는 것 — `CraftJob`·제작대), 그러면 남는 쪽이 작업이 되어 둘이 안 겹친다.
        new("작업", new[]
        {
            StatName.Mining, StatName.Growing, StatName.Cutting, StatName.Taming,
            StatName.Medicine, StatName.Research, StatName.Social,
        }),
        // **요리는 이쪽이다** — `CraftSkill.Cooking`이고 레시피로 만든다.
        //
        // **`노동`도 이쪽이다.** 작업 열에 두면 열 이름과 낱말이 겹쳐 보이는데, 그게 아니라도
        // 애초에 자리가 여기다 — `RecipeData.RequiredSkillType`의 **기본값이 `CraftSkill.Labor`**고
        // `UnitStats.StatForCraft`가 **특정 기술이 없는 레시피의 폴백**으로 이 스탯을 쓴다
        // (`CraftSkill.Labor`의 작업대 이름도 그냥 "작업대"다). 화면에서 지우면 **제작 속도를
        // 정하는 스탯이 안 보이게 된다.**
        new("제작", new[]
        {
            StatName.Cooking, StatName.WeaponCrafting, StatName.ArmorCrafting, StatName.Alchemy,
            StatName.Smelting, StatName.Stonecutting, StatName.Woodworking, StatName.Gemcutting,
            StatName.Labor,
        }),
    };

    // 가장 긴 열 — 창 높이를 셈할 때 쓴다.
    public static int LongestColumn
    {
        get
        {
            int max = 0;
            foreach (var c in All) max = Mathf.Max(max, c.Stats.Length);
            return max;
        }
    }
}
