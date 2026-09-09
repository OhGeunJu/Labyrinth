// **피해는 한 축이다**(2026-08-23).
//
// 전에는 `DamageCategory`(Physical/Elemental/Dark/Holy) × `DamageElement`(Fire/Ice/Lightning/
// Poison)로 축이 둘이었다. "이건 무슨 피해인가"는 **물음 하나인데 답이 두 벌**이었고, 그 결과가
// 실측으로 드러났다: 조합 열두 칸 중 **`Holy` 넉 줄은 효과가 아예 없었고**(효과표에 행이 없다)
// `Dark`·`Holy` 무기는 **0개**였다. 정의된 여덟도 `Dark + Ice = 감속`처럼 **칸을 채우려고
// 지어낸 것**이라 왜 그런지가 없었다.
//
// **원소를 늘리는 값은 "고를 것이 갈리는가"다** — 그래서 일곱이 저마다 하나씩만 한다.
// 독은 뺐다: 독은 원소가 아니라 **효과**다(불이 화상을 주듯). `StatusEffectSystem`이 든다.
//
// **암·성은 채운 칸이 아니다.** `Necromancy`가 시체를 일으키고 교단이 무덤을 판다 —
// **성이 그 습격의 답**이고 암은 저쪽의 무기다. 세계관에 있던 축을 피해 타입으로 끌어왔다.
public enum DamageType
{
    // ── 물리 — 갑옷이 막는다 ────────────────────────────────────
    Slash,      // 베기: 추가 출혈
    Pierce,     // 관통: 갑옷 관통
    Blunt,      // 둔기: 피해 ×1.3

    // ── 원소 — 저항이 막는다 ────────────────────────────────────
    Fire,       // 화상 DoT
    Water,      // 젖음 → 감속 + 전기 피해 증폭
    Earth,      // 기절 굴림 보정
    Lightning,  // 경직(공격 인터벌 증가)
    Arcane,     // 갑옷·저항 무시, 대신 낮은 계수
    Dark,       // 회복 차단 + 부패 게이지
    Holy,       // 언데드 특효 ×2
}

public static class DamageTypeExt
{
    // **물리인가** — 갈래는 이 하나로 남는다(물리는 갑옷이, 원소는 저항이 막는다).
    public static bool IsPhysical(this DamageType t)
        => t is DamageType.Slash or DamageType.Pierce or DamageType.Blunt;

    public static string DisplayName(this DamageType t) => t switch
    {
        DamageType.Slash     => "베기",
        DamageType.Pierce    => "관통",
        DamageType.Blunt     => "타격",
        DamageType.Fire      => "불",
        DamageType.Water     => "물",
        DamageType.Earth     => "땅",
        DamageType.Lightning => "전기",
        DamageType.Arcane    => "비전",
        DamageType.Dark      => "암흑",
        DamageType.Holy      => "신성",
        _                    => t.ToString(),
    };
}
