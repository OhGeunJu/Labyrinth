using System.Collections.Generic;
using UnityEngine;

// 레거시 2분법. FactionData(SO)를 붙이지 않은 기존 씬/프리팹의 폴백으로만 남아 있다.
// 신규 작업은 FactionData를 쓸 것 — 세력이 3개 이상이면 이 enum으로는 관계를 표현할 수 없다.
public enum FactionType
{
    Player,
    Enemy,
}

public class Faction : MonoBehaviour
{
    [Tooltip("세력 정의(SO). 비우면 아래 레거시 타입으로 동작한다.")]
    [SerializeField] FactionData data;

    [Tooltip("레거시 폴백 — Data가 비었을 때만 사용(Player/Enemy 2분법).")]
    [SerializeField] FactionType factionType;

    // 살아있는 Faction 전체 (플레이어 세력 조회 등에 사용)
    static readonly List<Faction> _all = new();

    // 세력을 가진 것 전부. 폰을 훑어야 할 때 `FindObjectsByType`을 매번 도는 대신 쓴다
    // (`MaterialPool`이 플레이어 폰의 배낭을 셀 때).
    public static IReadOnlyList<Faction> All => _all;

    void OnEnable()  => _all.Add(this);
    void OnDisable() => _all.Remove(this);

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

    // 레거시 값. Data가 있으면 의미가 없으므로 판정에 직접 쓰지 말 것(IsPlayer/StanceTo 사용).
    public FactionType Type => factionType;

    // 플레이어 조작 세력인가 — 선택/명령/우선순위 등 UI·입력 게이트용.
    public bool IsPlayer => data != null ? data.PlayerControlled : factionType == FactionType.Player;

    // 내가 other를 어떻게 대하는가 (방향별 — other가 나를 어떻게 보는지와 다를 수 있다).
    public FactionStance StanceTo(Faction other)
    {
        if (other == null) return FactionStance.Neutral;

        // 양쪽 다 SO가 있으면 관계표가 정답.
        if (data != null && other.data != null)
            return data.StanceTo(other.data);

        // 둘 다 SO가 없으면 레거시 2분법 그대로 (SO를 안 쓰는 씬의 기존 동작 유지).
        if (data == null && other.data == null)
            return IsPlayer == other.IsPlayer ? FactionStance.Ally : FactionStance.Hostile;

        // 한쪽만 SO가 있는 이행 중 상태 — 관계를 알 수 없으므로 **중립**.
        // (예전엔 Hostile로 떨어져서, 플레이어에 SO를 안 붙였다는 이유만으로
        //  중립이어야 할 마을사람과 싸우는 일이 생겼다.)
        return FactionStance.Neutral;
    }

    // 내가 other를 먼저 공격하는가.
    public bool IsHostileTo(Faction other) => StanceTo(other) == FactionStance.Hostile;

    // 오사 회피 등 "같은 편" 판정.
    public bool IsAllyOf(Faction other) => StanceTo(other) == FactionStance.Ally;
}
