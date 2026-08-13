using System.Collections.Generic;
using UnityEngine;

// 세력. 관계는 전부 `FactionData`(SO)가 답한다 — 이 컴포넌트는 그것을 들고 다닐 뿐이다.
//
// 예전엔 `FactionType { Player, Enemy }` 2분법 폴백이 옆에 같이 있었다(SO를 안 붙인 씬용).
// 세력이 셋 이상이면 표현이 안 되는 데다, **한쪽만 SO가 있는 이행 중 상태**가 관계를
// "알 수 없음 → 중립"으로 떨어뜨려서 적이어야 할 것이 안 싸우는 조용한 실패를 만들었다.
// 씬·프리팹의 `Faction`에 전부 SO를 연결하고 폴백을 걷어냈다(2026-08-03).
public class Faction : MonoBehaviour
{
    [Tooltip("세력 정의(SO). **반드시 연결할 것** — 비우면 아무와도 관계가 없어 " +
             "적대 판정이 전부 중립으로 떨어진다.")]
    [SerializeField] FactionData data;

    // 살아있는 Faction 전체 (플레이어 세력 조회 등에 사용)
    static readonly List<Faction> _all = new();

    // 세력을 가진 것 전부. 폰을 훑어야 할 때 `FindObjectsByType`을 매번 도는 대신 쓴다
    // (`MaterialPool`이 플레이어 폰의 배낭을 셀 때).
    public static IReadOnlyList<Faction> All => _all;

    void OnEnable()  => _all.Add(this);
    void OnDisable() => _all.Remove(this);

    // 폴백이 없어졌으므로 **빠뜨린 연결은 조용히 넘어가지 않는다.** 예전엔 SO가 없으면
    // 레거시 2분법으로 그럭저럭 돌아가서 빠뜨린 줄도 몰랐다 — 지금은 아무와도 관계가 없는
    // 유령이 되므로, 안 싸우거나 선택이 안 되는 증상을 겪기 전에 여기서 말한다.
    //
    // **처음 하나만 말한다.** 이행 중에는 SO 없는 프리팹이 무더기로 스폰되는데, 개체마다
    // 경고를 뱉으면 콘솔이 그것만으로 가득 차 정작 봐야 할 로그가 밀려난다.
    // 이름을 적어 두므로 하나를 고치면 다음 것이 드러난다.
    static bool _warnedMissingData;

    void Awake()
    {
        if (data != null || _warnedMissingData) return;

        _warnedMissingData = true;
        Debug.LogWarning($"[Faction] {name}: FactionData가 연결되지 않았습니다. " +
                         "적대 판정이 전부 중립으로 떨어지고 플레이어 세력으로도 잡히지 않습니다. " +
                         "(같은 경고는 이후 생략합니다 — 이것을 고치면 다음 것이 드러납니다.)");
    }

    // 플레이어 조작 세력의 Faction 하나 (UI에서 "이 유닛이 우리에게 적인가"를 판단할 때).
    public static Faction Player
    {
        get
        {
            foreach (var f in _all)
                if (f != null && f.IsPlayer) return f;
            return null;
        }
    }

    public FactionData Data => data;

    // **세력은 바뀔 수 있다** — 회유(포로가 우리 편이 된다)가 그 길이다.
    //
    // 여기서는 값만 갈아 끼운다. 세력에 딸린 것들(BT·성장 잠금·작업 설정·허기)은
    // `Awake`에서 한 번 정해지므로 **`UnitBrain.ChangeFaction`이 다시 세운다** —
    // 이 setter를 직접 부르면 적 트리를 그대로 든 아군이 된다.
    public void SetData(FactionData to) => data = to;

    // 플레이어 조작 세력인가 — 선택/명령/우선순위 등 UI·입력 게이트용.
    public bool IsPlayer => data != null && data.PlayerControlled;

    // 내가 other를 어떻게 대하는가 (방향별 — other가 나를 어떻게 보는지와 다를 수 있다).
    //
    // SO가 없으면 **중립**이다. 관계를 모르는 것을 적대로 읽으면 연결을 빠뜨린 프리팹 하나가
    // 온 세상과 싸우고, 아군으로 읽으면 적이 안 싸운다 — 둘 다 조용히 틀리므로,
    // 눈에 띄되 해는 적은 쪽으로 떨어뜨린다. 연결을 빠뜨렸는지는 `Awake`가 경고한다.
    public FactionStance StanceTo(Faction other)
    {
        if (other == null || data == null || other.data == null) return FactionStance.Neutral;

        // **우호도가 저작한 값을 덮는다**(`FactionRelations`) — 다만 문턱을 넘었을 때만이고,
        // 사이 구간이면 `FactionData`가 그대로 이긴다. 저작을 지우는 것이 아니라 위에 얹는 것.
        //
        // **여기 한 곳에만 얹는다.** `IsHostileTo`·`IsAllyOf`·탐지·오사 회피가 전부 이 함수를
        // 거치므로, 다른 데서 또 물으면 "화면에는 적대인데 안 때린다" 같은 것이 생긴다.
        //
        // 움직이는 것은 **플레이어와의 관계뿐**이다. 세력끼리는 저작한 그대로다 — 그걸 움직일
        // 사건이 아직 없는데 표만 만들면 무엇이 왜 변했는지 아무도 모른다.
        if (other.IsPlayer && FactionRelations.TryOverride(data, out var mine)) return mine;
        if (IsPlayer       && FactionRelations.TryOverride(other.data, out var his)) return his;

        return data.StanceTo(other.data);
    }

    // 내가 other를 먼저 공격하는가.
    public bool IsHostileTo(Faction other) => StanceTo(other) == FactionStance.Hostile;

    // 오사 회피 등 "같은 편" 판정.
    public bool IsAllyOf(Faction other) => StanceTo(other) == FactionStance.Ally;
}
