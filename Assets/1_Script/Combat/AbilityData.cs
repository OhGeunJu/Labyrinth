using UnityEngine;

// **능동 능력은 한 그릇에 담는다**(2026-08-23, `docs/design_combat_rework.md` 6절).
//
// 마법·도발·자기 강화가 각자 컴포넌트를 갖게 두면 능력을 하나 더할 때마다 전투 코드에 분기가
// 는다(`TraitData`가 스탯에 숫자만 얹기로 한 **모델 A**와 같은 취지 — *"축이 늘어도 결정이
// 안 늘면 걷어낸다"*).
//
// **능동은 폰이 아니라 장비가 준다**(`WeaponData.Ability` · `ArmorData.Ability`). 폰에 스킬
// 슬롯을 두면 *"어느 폰에게 무엇을 배울까"*가 폰 수만큼 늘어나는 잡일이 되는데, 장비가 주면
// **갈아입히는 것이 곧 역할을 바꾸는 것**이라 이미 있는 조작으로 끝난다. 스킬 트리도
// 스킬 포인트도 없다 — 성장은 `CombatXp` 그대로 **쓰면 는다**.
public enum AbilityKind
{
    Taunt,   // 주변 적의 표적을 나에게 돌린다
}

[CreateAssetMenu(menuName = "DungeonColony/AbilityData")]
public class AbilityData : ScriptableObject
{
    [Header("기본 정보")]
    public string AbilityName = "능력";
    [TextArea] public string Description;
    public Sprite Icon;

    [Header("무엇을 하는가")]
    public AbilityKind Kind = AbilityKind.Taunt;

    [Header("시간 (초)")]
    [Tooltip("다시 쓸 수 있을 때까지")]
    public float Cooldown = 25f;
    [Tooltip("효과가 남아 있는 동안")]
    public float Duration = 8f;

    // **패시브가 아니라 능동인 이유**가 여기 있다. 늘 켜져 있으면 ① 탱커를 데려가는 순간
    // 다른 전술이 사라지고 ② *"도발 중"*이 화면에 안 보인다. 지속 + 쿨다운으로 두면
    // **언제 쓰느냐가 결정**이 된다.

    [Header("범위·인원 — 강인함이 얹는다")]
    [Tooltip("강인함 0일 때의 범위(m)")]
    public float BaseRange = 4f;
    [Tooltip("강인함이 이만큼 오를 때마다 범위 +1m. 0이면 안 얹는다")]
    public float StatPerRange = 30f;
    [Tooltip("강인함 0일 때의 인원")]
    public int BaseTargets = 1;
    [Tooltip("강인함이 이만큼 오를 때마다 인원 +1. 0이면 안 얹는다")]
    public float StatPerTarget = 40f;

    // **강인함이 든다.** 이미 KO 저항을 정하지만 **다른 축**이라 겹치지 않는다
    // (`Social`이 설득과 흥정을 함께 정하는 것과 같다 — 같은 재주다).
    // *맞고 버티는 놈이 앞에 선다*가 스탯 이름 그대로 읽힌다.
    public float RangeFor(float stat) =>
        BaseRange + (StatPerRange > 0f ? stat / StatPerRange : 0f);

    public int TargetsFor(float stat) =>
        Mathf.Max(1, BaseTargets + (StatPerTarget > 0f ? Mathf.FloorToInt(stat / StatPerTarget) : 0));

    // 화면에 뜨는 한 줄. **여기서 만든다** — 창마다 조립하면 액션 바와 툴팁이 다른 말을 한다.
    public string Summary(float stat) =>
        Kind switch
        {
            AbilityKind.Taunt =>
                $"{RangeFor(stat):0.#}m 안의 적 {TargetsFor(stat)}명이 {Duration:0}초 동안 나를 노린다",
            _ => Description,
        };
}
