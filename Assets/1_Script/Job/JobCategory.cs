public enum JobCategory
{
    None,
    Mining,
    Hauling,
    Farming,
    Logging,
    Construction,
    Guard,
    Crafting,
    Research,
    Unload,
    // **뒤에 붙일 것** — 직렬화가 정수라 중간에 끼우면 `MinionData.AllowedJobs`가 통째로 밀린다.
    Medical,
    Warden,
    // 도축 — **큐를 탄다**(제작 뒤 · 운반 앞). 한동안 안 태웠고 그 이유가 여기 적혀 있었다:
    // *"자동으로 가르기 시작하면 시키지도 않았는데 동료 시신을 손질한다."* **그 걱정을 없앤
    // 것은 자동화를 막는 것이 아니라 규칙이다**(`ButcherBill`) — 무엇을 가를지 사람이 미리
    // 정해 두면 폰이 알아서 집어가도 동의 없이 벌어지는 일이 아니다. `QueueOrder`가 그
    // 단일 출처이므로 여기 반대로 적혀 있으면 **그 파일을 안 열어 본 사람이 그대로 속는다.**
    Butcher,
    // 채집 — **바닥에서 나는 것을 한 번에 딴다**(갓버섯). **큐 작업이다**(`ForageJob`) —
    // 자원 노드가 아니라 `Forageable`이 일감을 건다.
    //
    // 벌목과 갈라 둔 이유: 거대버섯은 **베고**(배정·게이지·그 자리에 남는다) 갓버섯은
    // **딴다**(큐·한 번·없어진다). **둘을 한 스위치에 묶으면 "목재는 그만, 먹을 것만"이
    // 성립하지 않는다.**
    Foraging,
}
