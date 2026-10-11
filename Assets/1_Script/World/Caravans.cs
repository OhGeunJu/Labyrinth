using System;
using UnityEngine;

// **상단 — 열렸나**(2026-10-01). 상단 자체(편성·규칙·여정)는 `Caravan`이 들고, 여기는 *"교역이라는 것이 이 판에
// 있나"* 하나만 답한다.
//
// **첫 마을을 찾으면 열린다**(`TownSite.Discover`, 사용자: *"첫 마을을 발견시 상단 시스템이 오픈되면 좋겠어"*). 열기
// 전에는 상단 창(`T`)이 *"아직 교역할 곳을 모른다"*만 말한다 — 창을 숨기면 그런 것이 있는 줄도 모른다.
public static class Caravans
{
    public static bool Unlocked { get; private set; }

    public static event Action OnUnlocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Unlocked = false; OnUnlocked = null; }

    public static void Unlock()
    {
        if (Unlocked) return;
        Unlocked = true;
        EventNotificationUI.Notify("상단", "첫 마을을 찾았습니다 - 이제 상단을 꾸려 교역할 수 있습니다. (T)");
        OnUnlocked?.Invoke();
    }
}
