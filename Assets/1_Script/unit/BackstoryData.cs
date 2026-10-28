using System.Collections.Generic;
using UnityEngine;

// 성장 배경이 채우는 두 칸. **유년기와 성년기를 갈라 둔다**(림월드) — 하나로 뭉치면
// *"어떻게 자랐나"*와 *"무엇을 하며 살았나"*가 섞여, 같은 사람에게 둘 다 있어야 말이 되는
// 것들(거리에서 자란 상단 호위)이 표현되지 않는다.
public enum BackstorySlot
{
    Childhood,   // 유년기 — 어디서 어떻게 자랐나
    Adulthood,   // 성년기 — 무엇을 하며 살았나
}

// **성장 배경 하나** — 이 사람이 여기 오기까지 무엇을 하며 살았는가.
//
// 특성(`TraitData`)과 **나란한 축이지 같은 축이 아니다**: 특성은 타고난 기질이라 굴림에서
// 나오고 배타 묶음으로 겹침을 막지만, 배경은 **살아온 이력**이라 칸이 정해져 있고
// (유년기 하나 · 성년기 하나) 겹칠 일이 없다. 그래서 배타 묶음도 개수 굴림도 없다.
//
// **모델 A를 그대로 따른다**(`docs/design_traits.md`) — **스탯에 숫자만 얹는다.** 림월드는
// 배경으로 작업을 막지만(`무능력: 지적 활동`) 여기서 그렇게 하면 작업·전투 코드가 전부
// "이 폰의 배경이 무엇인가"를 묻게 되고, 배경을 하나 늘릴 때마다 분기가 사방에 는다.
// 막고 싶으면 **`WorkSettings`(작업 on/off)라는 이미 있는 게이트**를 쓰는 것이 맞다.
[CreateAssetMenu(menuName = "DungeonColony/BackstoryData")]
public class BackstoryData : ScriptableObject
{
    [Header("표시")]
    public BackstorySlot Slot;
    public string Title;
    [TextArea] public string Description;

    [Header("효과")]
    // **`TraitData.StatOffset`을 그대로 쓴다.** 같은 모양을 하나 더 만들면 두 벌이 되고,
    // 그렇다고 공용 타입으로 **옮기면** 유니티가 직렬화 타입을 못 찾아 **기존 특성 에셋의
    // 값이 통째로 날아간다**(필드 이름이 같아도 타입이 다르면 비워진다). 남의 집 타입을
    // 빌려 쓰는 모양이 어색해도, 값을 잃는 것보다 낫다.
    [Tooltip("이 배경이 스탯에 얹는 값. 하나가 여러 스탯을 건드려도 된다.")]
    public List<TraitData.StatOffset> StatOffsets = new();

    // **못 하게 만드는 일**(림월드의 `무능력`). 성장 배경이 특성과 갈리는 자리다 — 기질은
    // 숫자를 얹을 뿐이지만, 살아온 내력은 *"이 사람은 그걸 배운 적이 없다"*를 말할 수 있다.
    //
    // **막는 길은 `WorkSettings` 하나다**(`UnitBrain.SetBackstories` → `SetBlocked`).
    // 큐 배정이 이미 `IsEnabled` 하나를 지나가므로, 거기서 거짓을 돌려주면 **작업·전투 코드는
    // 성장 배경을 몰라도 된다** — 모델 A의 취지(분기를 늘리지 않는다)를 그대로 지킨다.
    // 직접 배정(채굴·벌목·경비)만 배정하는 쪽이 `IsBlocked`를 한 번 더 묻는다.
    [Tooltip("이 배경이 **못 하게 만드는 작업**. 사람이 P패널에서도 못 켠다.")]
    public List<JobCategory> DisabledJobs = new();

    [Header("추첨")]
    [Tooltip("뽑힐 가중치. 클수록 흔하다.")]
    public float Weight = 1f;

    // **어느 무리에서 나오는 이력인가.**
    //
    // 완전 무작위면 회유해 온 산적이 `귀족가의 아이 · 요리사`가 된다 — 배경이 *살아온
    // 이력*이라는 말이 곧바로 거짓이 된다. **세력이 제 묶음을 든다**(`FactionData.BackstoryGroup`):
    // 산적은 `bandit`, 마을 사람·변방민은 `civilian`…
    //
    // **공용이라는 것은 없다 — 모든 배경은 어느 무리의 것이다.** 비운 것은 `civilian`으로
    // 친다(여느 사람의 이력). 한때 빈 것을 "공용"으로 두고 늘 후보에 넣었더니 **산적에게서
    // `사원의 시동`이 나왔다** — 무리에서 뽑은 사람이 그 무리와 상관없는 이력을 지고 오면,
    // 세력마다 이력을 갈라 둔 뜻이 통째로 지워진다.
    //
    // **세력 참조 목록이 아니라 문자열인 이유**: 참조로 두면 세력을 하나 늘릴 때마다 배경
    // **전부**를 열어 그 세력을 더해야 한다. 묶음이면 새 세력이 제 묶음 이름 하나만 적는다
    // (`TraitData.ExclusiveGroup`과 같은 자리·같은 방법).
    [Tooltip("이 배경이 속한 무리(civilian · bandit · cult · mercenary). 비우면 civilian으로 친다.")]
    public string Group;

    // **이 이력의 주인은 무리를 이끌었다.**
    //
    // 습격대의 두목을 뽑는 자리가 이걸 본다(`RaidLord.PromoteLeader`). 그전에는 **스탯만**
    // 봐서, *"무리를 이끈"* 고블린이 부하로 서고 옆의 힘센 놈이 두목이 됐다 — 그러면
    // **스탯 창과 머리 위 표식이 서로 다른 말을 한다**(사용자가 판에서 보고 물었다,
    // 2026-09-09).
    //
    // **파생시킬 수 없어서 저작한다.** *"패거리 두목"*이 이끄는 이력이고 *"길잡이"*는
    // 아니라는 것은 **글에만 있는 사실**이라, 이름이나 스탯 보정에서 짜낼 방법이 없다
    // (짜내면 제목을 다듬는 날 두목이 사라진다).
    //
    // **묶음마다 하나씩 있어야 뜻이 산다** — 없는 묶음(지금은 `cult`)에서는 규칙이 조용히
    // 안 걸리고 예전처럼 가장 센 놈이 두목이 된다. 틀리지는 않지만 **그 무리만 이력이
    // 잡졸이다.**
    [Tooltip("이 이력의 주인은 무리를 이끌었다. 습격대의 두목을 뽑을 때 먼저 본다.")]
    public bool Leads;

    public string DisplayName => !string.IsNullOrWhiteSpace(Title) ? Title : name;

    // 스탯 창 툴팁용 — `근력 +8 · 사교 -4`.
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
