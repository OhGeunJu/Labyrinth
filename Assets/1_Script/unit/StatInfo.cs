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
        StatName.MeleeDefense      => "근접 공격을 막는다. 무기로 받아내는 쪽이라 무장 상태에 영향을 받는다.",
        StatName.RangedDefense     => "날아오는 것을 피하거나 막는다.",
        StatName.PrecisionShooting => "원거리 명중. 거리가 멀수록 더 크게 걸린다.",
        StatName.MartialArts       => "맨손 전투. 무기를 안 들었을 때의 공격력과 명중을 정한다.",
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
        StatName.ElementalMagic => "원소 계통 전반. 그 계통 주문 전체에 걸린다.",
        StatName.DarkMagic      => "암흑 계통 전반. 그 계통 주문 전체에 걸린다.",
        StatName.HolyMagic      => "신성 계통 전반. 그 계통 주문 전체에 걸린다.",

        StatName.ElementalFire      => "원소 계통의 불 주문.",
        StatName.ElementalIce       => "원소 계통의 얼음 주문.",
        StatName.ElementalLightning => "원소 계통의 전기 주문.",
        StatName.ElementalPoison    => "원소 계통의 독 주문.",
        StatName.DarkFire           => "암흑 계통의 불 주문.",
        StatName.DarkIce            => "암흑 계통의 얼음 주문.",
        StatName.DarkLightning      => "암흑 계통의 전기 주문.",
        StatName.DarkPoison         => "암흑 계통의 독 주문.",
        StatName.HolyFire           => "신성 계통의 불 주문.",
        StatName.HolyIce            => "신성 계통의 얼음 주문.",
        StatName.HolyLightning      => "신성 계통의 전기 주문.",
        StatName.HolyPoison         => "신성 계통의 독 주문.",

        // 노동 — 일감이 요구하는 것
        StatName.Labor          => "잡일 전반. 특정 기술이 없는 작업의 속도.",
        StatName.Mining         => "바위·광맥을 캔다. 노드가 요구하는 수준보다 낮으면 느리다.",
        StatName.Growing        => "씨를 뿌리고 거둔다. 수확량에도 걸린다.",
        StatName.Cutting        => "나무를 벤다.",
        StatName.Taming         => "짐승을 길들인다.",
        StatName.Cooking        => "요리. 결과물의 등급에 걸린다.",
        StatName.Medicine       => "치료. 회복 속도와 후유증에 걸린다.",
        StatName.Research       => "연구대에서의 진행 속도.",
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
