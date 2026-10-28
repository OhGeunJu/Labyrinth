using System;
using System.Collections.Generic;

// **어떤 품목을 자동으로 나르나 — 답은 여기 하나다**(2026-09-06, 사용자 결정).
//
// ## 스위치는 물건이 아니라 품목을 켠다
//
// 사용자: *"살펴보기 창에서 자동운반을 키면 A버섯과 B버섯중 자동운반을 킨 A버섯만 운반하는게
// 아니라, 자동운반을 키면 모든 버섯은 채집물을 운반한다가 규칙이 되는거야."*
//
// **그래서 "어느 쪽이 이기나"를 정할 일이 없다.** 살펴보기 창의 스위치도, 품목 창의 토글도
// **같은 곳에 쓴다** — 창이 둘이지 답이 둘인 것이 아니다. 이 저장소가 여러 번 겪은
// *"같은 물음에 두 곳이 답하면 반드시 어긋난다"*를 **자리를 하나로 두어** 피한다.
//
// ## 개별 지정과 싸우지 않는다 — 둘 다 "나른다"만 말한다
//
// `DroppedItem.HaulAllowed`(H로 찍는 것)는 **이 한 개**를 말하고, 여기 정책은 **그 품목
// 전부**를 말한다. 둘은 **켜짐끼리만 겹치므로 충돌이 없다**(합집합이다):
//
//   나른다 = 이 한 개가 찍혔다  또는  이 품목이 켜졌다
//
// 그래서 우선순위 규칙이 필요 없다. 다만 **품목이 켜져 있으면 한 개만 빼 둘 수는 없다** —
// 그건 *품목 규칙*이라는 말의 뜻 그대로다(빼고 싶으면 품목을 끄고 하나씩 찍는다).
//
// ## 알림은 버스다
//
// 사용자: *"위에 매니저를 냅둬놓고 그게 규칙을 켜진다 꺼진다을 받게하면 되잖아 마치 이벤트
// 버스처럼."* — 켜는 순간 **이미 바닥에 있는 것들도 따라와야** 한다. 정책이 바뀌었다고
// 외치고, 듣는 쪽(바닥의 물건)이 제 표식과 일감을 다시 맞춘다. 훑는 쪽을 정책이 알면
// (`FindObjectsByType`) **정책이 화면을 아는 꼴**이 되고, 그러면 창이 늘 때마다 여기가 는다.
//
// ## `static`인 이유
//
// 세이브가 아직 없고, 씬 오브젝트로 두면 **정착지마다 배선을 반복**해야 한다
// (`HaulDesignator`가 씬 배선 없이 스스로 서는 것과 같은 기준). 판이 열릴 때 비운다 —
// 도메인 리로드를 끄면 지난 판의 정책을 그대로 들고 시작한다.
public static class HaulPolicy
{
    // **키는 `ItemData`가 아니라 `Id`다** — JSON 아이템은 런타임 생성이라 인스턴스가
    // 다시 만들어질 수 있고, 그러면 참조로 잡아 둔 정책이 조용히 사라진다
    // (`MerchantPoolEntry`·`LoadoutData`가 문자열 `ItemId`를 쓰는 것과 같은 이유).
    static readonly HashSet<string> _auto = new();

    // **바뀌었다는 사실만 알린다** — 무엇이 바뀌었는지는 듣는 쪽이 제 것만 다시 보면 된다.
    public static event Action OnChanged;

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { _auto.Clear(); OnChanged = null; }

    public static bool IsAuto(ItemData item)
        => item != null && _auto.Contains(KeyOf(item));

    public static void SetAuto(ItemData item, bool on)
    {
        if (item == null) return;

        string key = KeyOf(item);
        bool changed = on ? _auto.Add(key) : _auto.Remove(key);
        if (changed) OnChanged?.Invoke();
    }

    public static void Toggle(ItemData item) => SetAuto(item, !IsAuto(item));

    // **화면에 그대로 내보내는 말** — 토글이 아니라 상태로 보인다(사용자 결정:
    // *"그냥 자동운반 켜짐 자동운반 꺼짐으로 나타나면 좋겠네"*). 켜고 끄는 체크박스는
    // *지금 어느 쪽인지*를 눈이 한 번 더 재야 하는데, 살펴보기 창은 **읽는 창**이다.
    public static string Label(ItemData item)
        => IsAuto(item) ? "자동운반 켜짐" : "자동운반 꺼짐";

    // 비어 있으면 에셋 이름을 쓴다 — `ItemData.Id`의 규약 그대로다.
    static string KeyOf(ItemData item)
        => string.IsNullOrEmpty(item.Id) ? item.name : item.Id;
}
