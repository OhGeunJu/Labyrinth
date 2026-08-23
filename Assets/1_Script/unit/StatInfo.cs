// **스탯이 무엇을 하는가.** 이름만으로는 `인지력`이 연구를 빠르게 하는지 조준을 돕는지 알 수 없고,
// 이름과 숫자만 보이는 창은 결국 "높으면 좋겠지"로 읽힌다.
//
// 표시명(`StatNameExt.ToDisplay`)과 **같은 파일에 두지 않은 이유**: 표시명은 UI가 늘 부르는 것이고
// 설명은 툴팁만 부른다. 다만 둘 다 **한 곳뿐**이라는 규칙은 같다.
public static class StatInfo
{
    public static string Describe(this StatName s) => s switch
    {
        // 속성 — 다른 스탯의 바탕이 된다
        StatName.Strength  => "무거운 것을 들고 세게 때린다. 적재 한도(10 + 근력×0.5 kg)를 정하고, 짐을 지고 걸으면 저절로 자란다.",
        StatName.Toughness => "기절에 버틴다. 같은 곳을 맞아도 늦게 쓰러지고 빨리 깬다. `강인함` 특성이 크게 올린다.",
        StatName.Dexterity => "손이 빠르고 정확하다. 제작 속도와 결과물 등급에 걸린다.",
        StatName.Cognition => "이해가 빠르다. 연구 속도와 배우는 속도에 걸린다.",

        // 전투
        StatName.MeleeAttack       => "근접 무기를 맞힌다. 상대의 근접방어·회피와 겨뤄 명중을 가른다.",
        StatName.Defense           => "공격을 막아 낸다. 근접도 원거리도 같은 솜씨다 — " +
                                      "다만 무기로 받아내는 쪽이라 맨손이면 무술로 대신한다. " +
                                      "방패는 근접·원거리를 저마다 다르게 막아 준다.",
        StatName.PrecisionShooting => "원거리 명중. 거리가 멀수록 더 크게 걸린다.",
        StatName.MartialArts       => "맨손 전투. 무기를 안 들었을 때 **근접공격과 방어를 겸한다** — " +
                                      "때리는 솜씨와 막는 솜씨가 한 스탯이라, 맨손 폰은 이것만 " +
                                      "올리면 둘 다 오른다.",
        StatName.Evasion           => "몸으로 피한다. 방어와 달리 무기가 없어도 걸린다.",
        StatName.Athletics         => "이동 속도와 지구력. 걸으면 저절로 자란다.",
        StatName.Swimming          => "물에서의 이동.",

        // 무기 숙련 — 그 무기를 들었을 때만 걸린다
        StatName.OneHandSword => "한손검 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.OneHandAxe   => "한손도끼 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.OneHandMace  => "한손둔기 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.TwoHandSword => "양손검 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.TwoHandAxe   => "양손도끼 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.TwoHandMace  => "양손둔기 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.Polearm      => "장창 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.Bow          => "활 숙련.",
        StatName.Crossbow     => "쇠뇌 숙련.",
        StatName.Javelin      => "투창 숙련.",

        // 마법 — 계통과 원소가 곱해진다
        StatName.FireMagic      => "불 지팡이. 화상을 남긴다.",
        StatName.WaterMagic     => "물 지팡이. 젖게 해 느리게 하고, **전기가 더 아프게** 한다.",
        StatName.EarthMagic     => "땅 지팡이. 넘어뜨린다(기절 굴림 보정).",
        StatName.LightningMagic => "전기 지팡이. 경직시킨다. 젖은 상대에게 1.5배.",
        StatName.ArcaneMagic    => "비전 지팡이. **갑옷도 저항도 지나간다** — 대신 계수가 낮다.",
        StatName.DarkMagic      => "암흑 지팡이. 회복을 막고 부패를 쌓는다.",
        StatName.HolyMagic      => "신성 지팡이. **언데드에게 두 배.**",
        StatName.Shield         => "방패를 다루는 솜씨. 막기 확률에서 **방패의 몫**을 키운다 " +
                                   "(방패를 들지 않으면 아무것도 안 한다).",

        // 노동 — 일감이 요구하는 것
        StatName.Labor          => "잡일 전반. 특정 기술이 없는 작업의 속도.",
        StatName.Mining         => "바위·광맥을 캔다. 노드가 요구하는 수준보다 낮으면 느리다.",
        StatName.Growing        => "씨를 뿌리고 거둔다. 수확량에도 걸린다.",
        StatName.Cutting        => "나무를 벤다.",
        StatName.Taming         => "짐승을 길들인다.",
        StatName.Cooking        => "요리. 결과물의 등급에 걸린다.",
        StatName.Medicine       => "치료. 회복 속도와 후유증에 걸린다.",
        StatName.Research       => "연구대에서의 진행 속도.",
        StatName.Social         => "말로 상대를 움직이는 솜씨. 포로 설득이 먹힐 확률과 말 붙이는 시간, 그리고 상인과 흥정한 값에 걸린다.",
        StatName.WeaponCrafting => "무기 제작. 속도와 완성품 등급을 정한다.",
        StatName.ArmorCrafting  => "방어구 제작. 속도와 완성품 등급을 정한다.",
        StatName.Alchemy        => "물약·시약 제조.",
        StatName.Smelting       => "광석을 녹여 주괴로. 용광로 작업.",
        StatName.Stonecutting   => "석재 가공.",
        StatName.Woodworking    => "목재 가공.",
        StatName.Gemcutting     => "보석 세공. 값나가는 물건일수록 등급 차이가 크게 벌어진다.",

        _ => "",
    };
}
