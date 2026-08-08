using System;
using System.Collections.Generic;
using UnityEngine;

// **특성 하나** — "이 폰은 어떤 사람인가"를 스탯 숫자 대신 이름으로 답하는 것.
//
// 스탯만으로는 사람이 안 읽힌다. `강인함 62`는 다른 폰과 비교해야 뜻이 생기지만
// `강인함`이라는 딱지는 그 자체로 읽힌다 — 켄시·림월드가 특성을 두는 이유가 그것이다.
//
// **모델 A: 특성은 스탯을 보정한다**(2026-07-26 결정, `docs/design_traits.md`).
// 기능을 특성으로 게이트하지 않는다 — 그러면 전투·작업 코드가 전부 "이 폰이 그 특성을
// 가졌나"를 묻게 되고, 특성을 하나 늘릴 때마다 분기가 사방에 는다. 숫자만 얹으면
// `BodyPartSystem.TryKnockOut`은 지금처럼 `Stats.Toughness`만 읽으면 된다.
[CreateAssetMenu(menuName = "DungeonColony/TraitData")]
public class TraitData : ScriptableObject
{
    [Serializable]
    public class StatOffset
    {
        public StatName Stat;
        [Tooltip("스탯에 더할 값. 음수면 깎는다.")]
        public float Delta;
    }

    [Header("표시")]
    public string TraitName;
    [TextArea] public string Description;
    public Sprite Icon;

    [Header("효과")]
    [Tooltip("이 특성이 스탯에 얹는 값. 특성 하나가 여러 스탯을 건드려도 된다.")]
    public List<StatOffset> StatOffsets = new();

    [Header("추첨")]
    [Tooltip("뽑힐 가중치. 클수록 흔하다.")]
    public float Weight = 1f;

    // **상호 배타 묶음.** 같은 묶음의 특성은 한 폰에 둘이 붙지 않는다 —
    // `용감`과 `겁쟁이`를 같이 가진 폰은 설명할 수 없고, 두 오프셋이 상쇄돼
    // 특성이 둘이나 붙었는데 아무 일도 안 일어난 것처럼 보인다.
    [Tooltip("같은 문자열끼리 배타(예: 체질 · 기질). 비우면 아무와도 안 겹친다.")]
    public string ExclusiveGroup;

    public string DisplayName => !string.IsNullOrWhiteSpace(TraitName) ? TraitName : name;

    // 스탯 창 툴팁용 — `강인함 +25 · 회피 -5`.
    public string EffectSummary()
    {
        if (StatOffsets == null || StatOffsets.Count == 0) return "";

        var parts = new List<string>();
        foreach (var o in StatOffsets)
        {
            if (o == null || Mathf.Approximately(o.Delta, 0f)) continue;
            parts.Add($"{StatNameExt.ToDisplay(o.Stat)} {(o.Delta > 0 ? "+" : "")}{o.Delta:0.#}");
        }
        return string.Join(" · ", parts);
    }
}
