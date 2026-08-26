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
        StatName.Toughness => "기절에 버틴다. 같은 곳을 맞아도 늦게 쓰러지고 빨리 깬다. **맞으면서 배운다** — 못 막고 못 피한 한 대가 이걸 키운다. `강인함` 특성이 크게 올린다.",
        StatName.Dexterity => "손이 빠르고 정확하다. **근접 공격 간격**과 **베기 무기 피해**에 걸린다 — 휘두를수록 는다. (제작 속도·등급은 그 작업의 숙련이 정한다.)",
        StatName.Cognition => "원거리 무기를 맞힌다. 상대의 방어·회피와 겨루고, **사거리도 이것이 정한다** — 낮으면 가까이 붙어야 쏜다. **움직이는 표적**을 읽는 것도 여기다. 붙잡혔을 때 설득에 버틴다.",

        // 전투
        StatName.MeleeAttack       => "근접 무기를 맞힌다. 상대의 근접방어·회피와 겨뤄 명중을 가른다.",
        StatName.Defense           => "공격을 막아 낸다. **막아 냈을 때만 자란다.** 근접도 원거리도 같은 솜씨다 — " +
                                      "다만 무기로 받아내는 쪽이라 맨손이면 무술로 대신한다. " +
                                      "방패는 근접·원거리를 저마다 다르게 막아 준다.",
        StatName.PrecisionShooting => "**겨눈 것을 맞힌다.** 빗나가면 화살이 사라지지 않고 **앞에 선 다른 누군가**가 맞는다 — 동료면 손해고, 옆에 선 다른 적이면 그냥 다른 놈이 맞는다. 앞이 비어 있으면 아무 일도 안 한다.",
        StatName.MartialArts       => "맨손 전투. 무기를 안 들었을 때 **근접공격과 방어를 겸한다** — " +
                                      "때리는 솜씨와 막는 솜씨가 한 스탯이라, 맨손 폰은 이것만 " +
                                      "올리면 둘 다 오른다.",
        StatName.Evasion           => "몸으로 피한다. 방어와 달리 무기가 없어도 걸린다.",
        StatName.Athletics         => "이동 속도와 지구력. 걸으면 저절로 자란다.",
        StatName.Swimming          => "물에서의 이동. **아직 쓰이지 않는다** — 물이 들어오면 그때 걸린다.",

        // 무기 숙련 — 그 무기를 들었을 때만 걸린다
        StatName.OneHandSword => "한손검 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.OneHandAxe   => "한손도끼 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.OneHandMace  => "한손둔기 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.TwoHandSword => "양손검 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.TwoHandAxe   => "양손도끼 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.TwoHandMace  => "양손둔기 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.Polearm      => "장창 숙련. 그 무기를 들었을 때의 명중·피해에 얹힌다.",
        StatName.Bow          => "활 숙련. **재장전을 줄인다** — 원거리는 피해가 아니라 속도로 갚는다.",
        StatName.Crossbow     => "쇠뇌 숙련. **재장전을 줄인다** — 원거리는 피해가 아니라 속도로 갚는다.",
        StatName.Javelin      => "투창 숙련. **재장전을 줄인다** — 원거리는 피해가 아니라 속도로 갚는다.",

        // 마법 — 계통과 원소가 곱해진다
        StatName.FireMagic      => "불 지팡이. 화상을 남긴다. 숙련이 오르면 시전 간격이 준다.",
        StatName.WaterMagic     => "물 지팡이. 젖게 해 느리게 하고, **전기가 더 아프게** 한다. 숙련이 오르면 시전 간격이 준다.",
        StatName.EarthMagic     => "땅 지팡이. 넘어뜨린다(기절 굴림 보정). 숙련이 오르면 시전 간격이 준다.",
        StatName.LightningMagic => "전기 지팡이. 경직시킨다. 젖은 상대에게 1.5배. 숙련이 오르면 시전 간격이 준다.",
        StatName.ArcaneMagic    => "비전 지팡이. **갑옷도 저항도 지나간다** — 대신 계수가 낮다. 숙련이 오르면 시전 간격이 준다.",
        StatName.DarkMagic      => "암흑 지팡이. 회복을 막고 부패를 쌓는다. 숙련이 오르면 시전 간격이 준다.",
        StatName.HolyMagic      => "신성 지팡이. **언데드에게 두 배.** 숙련이 오르면 시전 간격이 준다.",
        StatName.Shield         => "방패를 다루는 솜씨. 막기 확률에서 **방패의 몫**을 키운다 " +
                                   "(방패를 들지 않으면 아무것도 안 한다).",

        // 노동 — 일감이 요구하는 것
        StatName.Labor          => "잡일 전반. 특정 기술이 없는 작업의 속도.",
        StatName.Mining         => "바위·광맥을 캔다. 노드가 요구하는 수준보다 낮으면 느리다.",
        StatName.Growing        => "씨를 뿌리고 거둔다. 수확량에도 걸린다.",
        StatName.Cutting        => "나무를 벤다.",
        StatName.Taming         => "짐승을 길들인다. **아직 쓰이지 않는다** — 길들이기가 들어오면 그때 걸린다.",
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
