using UnityEngine;

// **힘을 먹는 건물**(2026-09-06). 이식 광맥 · 충전대 · 제단처럼 *가만히 있으면서 계속
// 무언가를 내놓는* 것들이 단다.
//
// **손으로 짓지 않는 것에는 안 단다** — 벽·바닥·침대는 값이 배치 재료로 끝난다. 여기 붙는
// 것은 **시간이 지나도 계속 이득을 내는 것**이고, 그것이 공짜면 개수가 곧 산출이 되어
// *"몇 개나 지을 수 있나"*라는 결정이 사라진다.
//
// ## 못 받으면 멈춘다 — 사라지지는 않는다
//
// 휴면(`BuildingDormancy`)이 컴포넌트를 끄는 것과 다르다. 여기는 **켜져 있고 화면에도
// 보이는데 일만 안 한다** — 힘이 모자란 것은 *영역 밖에 있는 것*과 다른 상태이고, 플레이어는
// 그 둘을 화면에서 갈라 볼 수 있어야 한다(전자는 핵을 키우면 되고 후자는 옮겨야 한다).
[DisallowMultipleComponent]
public class PowerUser : MonoBehaviour
{
    [Tooltip("이 건물이 먹는 힘. 핵이 내는 총량에서 이만큼을 가져간다.")]
    [Min(0)] [SerializeField] int cost = 1;

    [Tooltip("사람이 끈 스위치. 끄면 그 힘이 **뒤에 지은 건물에게 간다** — " +
             "이것이 배분 순서를 바꾸는 유일한 길이다.")]
    [SerializeField] bool switchedOn = true;

    public int  Cost       => Mathf.Max(0, cost);
    public bool SwitchedOn => switchedOn;
    public bool Powered    { get; private set; }

    // **`PowerGrid`만 부른다** — 밖에서 켜고 끄면 총량 계산을 건너뛴 채 도는 건물이 생긴다.
    public void SetPowered(bool on) => Powered = on;

    public void SetSwitch(bool on)
    {
        if (switchedOn == on) return;
        switchedOn = on;
        PowerGrid.Recompute();
    }

    public void ToggleSwitch() => SetSwitch(!switchedOn);

    void OnEnable()
    {
        PowerGrid.Register(this);

        // **영역이 바뀌면 다시 나눈다** — 핵을 옮기거나 부핵을 세우면 총량도 주인도 바뀐다.
        DungeonCore.OnTerritoryChanged += PowerGrid.Recompute;
    }

    void OnDisable()
    {
        DungeonCore.OnTerritoryChanged -= PowerGrid.Recompute;
        PowerGrid.Unregister(this);
        Powered = false;
    }

    // **필요한 것에는 스스로 붙는다**(2026-09-06). `BuildingDormancy.Ensure`와 같은 자리다.
    //
    // 처음에는 **에디터 도구가 프리팹에 물리는 것**만으로 뒀는데, 그러면 셋이 조용히 샌다:
    // 도구가 못 훑은 폴더 · **이미 씬에 서 있는 것** · 옛 프리팹으로 지어 둔 것. 실제로
    // 폴더 하나를 빠뜨려 **광맥 열두 개가 통째로 공짜로 돌았다**(그리고 화면에는 아무 표시가
    // 안 났다 — *"핵이 없는데도 잘 캐진다"*로만 보였다).
    //
    // **붙는 쪽이 스스로 챙기면 빠뜨릴 자리가 없다.** 도구는 이제 *값을 저작하는 일*만 한다.
    public static PowerUser Ensure(Component c, int defaultCost)
    {
        if (c == null) return null;

        var u = c.GetComponentInParent<PowerUser>();
        if (u != null) return u;

        u = c.gameObject.AddComponent<PowerUser>();
        u.cost = Mathf.Max(0, defaultCost);
        return u;
    }

    // **없으면 힘을 안 먹는 것으로 본다** — 이 컴포넌트를 안 단 옛 건물이 전부 멈추면
    // 저장소가 통째로 죽는다(새 축을 넣을 때의 규칙: **안 단 것은 예전 그대로**).
    public static bool Has(Component c, out PowerUser u)
    {
        u = c != null ? c.GetComponentInParent<PowerUser>() : null;
        return u != null;
    }

    public static bool Ok(Component c)
        => !Has(c, out var u) || u.Powered;
}
