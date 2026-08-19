using System;
using System.Collections.Generic;
using UnityEngine;   // Debug

// 폰 하나의 작업 허용 설정 — "이 작업을 한다 / 안 한다"만 담는다.
// 무엇을 먼저 할지는 폰별 숫자가 아니라 JobCategoryInfo.QueueOrder(전역 고정)가 결정한다.
//
// 부착: UnitBrain.Awake가 플레이어 세력 폰(방랑자·하수인)에만 자동으로 붙인다.
// 하수인은 MinionData.AllowedJobs를 **초기값**으로만 쓰고, 이후엔 개체별로 바뀐다
// (같은 MinionData를 공유하는 다른 하수인에 영향을 주지 않는다).
public class WorkSettings : MonoBehaviour
{
    public event Action OnChanged;

    // 기본값은 "전부 켬" — 끈 것만 저장한다. **딱 하나 예외가 짐내림이다.**
    //
    // 짐내림은 성격이 다르다: 다른 작업은 **세상에 생긴 일감**을 집어 가는 것이라 켜 두면
    // 할 일이 있을 때만 돈다. 짐내림은 **제 배낭을 비우는 일**이라, 켜 두면 물건이 들어오는
    // 순간(상인에게 사거나 시체를 뒤지거나 약을 챙기거나) 하던 것을 놓고 창고로 간다 —
    // "폰이 왜 저기 가지"의 가장 흔한 원인이었다.
    //
    // 그래서 **기본이 꺼짐**이고, 필요하면 켜거나 그때그때 시킨다. 바닥의 물건이 체크한
    // 것만 실려 가는 것과 같은 축이다(2026-08-13) — **저절로 옮겨지는 것은 없다.**
    static readonly JobCategory[] DefaultOff = { JobCategory.Unload };

    readonly HashSet<JobCategory> _disabled = new(DefaultOff);

    // ── 못 하는 일 ───────────────────────────────────────────────────────────
    //
    // **성장 배경이 막아 둔 것**(`BackstoryData.DisabledJobs`). 끈 것(`_disabled`)과 **다른
    // 축이다**: 끈 것은 "지금은 시키지 말자"라 사람이 도로 켤 수 있지만, 이건 *"이 사람은
    // 그걸 못 한다"*라 켤 수가 없다.
    //
    // **막는 자리를 여기 둔 이유**: 큐 배정이 이미 `IsEnabled` 하나를 지나간다
    // (`JobManager.RequestJob`). 그러니 여기서 거짓을 돌려주면 **작업·전투 코드는 성장 배경을
    // 몰라도 된다** — 특성이 스탯만 얹는 것과 같은 자리의 판단이다(모델 A). 배경이 늘어도
    // 분기는 안 는다.
    readonly HashSet<JobCategory> _blocked = new();

    public bool IsBlocked(JobCategory category) => _blocked.Contains(category);

    public IReadOnlyCollection<JobCategory> Blocked => _blocked;

    // 배경이 얹고 뗀다(`UnitBrain.SetBackstories`). **직접 배정(채굴·벌목·경비)도 받는다** —
    // `IsEnabled`는 큐 작업만 답하지만, 배정하는 쪽은 이 목록을 따로 묻는다
    // (`ResourceNode.Assign`·`PatrolZone.Assign`). 여기까지 걸러 주지 않으면
    // *"무능력인데 바위는 캔다"*가 된다.
    public void SetBlocked(JobCategory category, bool blocked)
    {
        bool changed = blocked ? _blocked.Add(category) : _blocked.Remove(category);
        if (changed) OnChanged?.Invoke();
    }

    public void ClearBlocked()
    {
        if (_blocked.Count == 0) return;
        _blocked.Clear();
        OnChanged?.Invoke();
    }

    public bool IsEnabled(JobCategory category)
        => JobCategoryInfo.IsQueued(category)
        && !_blocked.Contains(category)
        && !_disabled.Contains(category);

    public void SetEnabled(JobCategory category, bool enabled)
    {
        if (!JobCategoryInfo.IsQueued(category)) return;
        if (_blocked.Contains(category)) return;   // 못 하는 일은 켤 수 없다

        bool changed = enabled ? _disabled.Remove(category) : _disabled.Add(category);
        if (changed) OnChanged?.Invoke();
    }

    public void Toggle(JobCategory category) => SetEnabled(category, !IsEnabled(category));

    // ── 자가치유 ─────────────────────────────────────────────────────────────
    //
    // **작업 카테고리가 아니다.** 카테고리는 "남의 일감을 집을까"인데 이건 "누워 있는 동안
    // 제 상처를 스스로 싸맬까"라서 큐를 타지 않는다 — `RestNode`가 직접 한다
    // (침대에 배정된 폰은 BT가 `Rest`에서 멈춰 `Work`까지 가지도 않는다).
    //
    // **기본은 끔**(림월드와 같다). 자가치유는 **약을 축낸다** — 의료가 낮은 폰이 구급낭을
    // 헐어 쓰는 것보다 의사를 기다리는 편이 대개 낫고(`Treatment.SelfPenalty`도 붙는다),
    // 켜져 있는 줄 모르는 채 물자가 새는 것이 가장 나쁘다.
    //
    // 켜는 자리를 둔 이유는 **의사가 없을 때**다 — 혼자 다니는 방랑자, 원정에서 돌아온 폰.
    // 켤지 말지는 지금 사람이 몇인지에 달렸으므로 플레이어가 정한다.
    public bool SelfTend { get; private set; }

    public void SetSelfTend(bool on)
    {
        if (SelfTend == on) return;
        SelfTend = on;
        OnChanged?.Invoke();
    }

    public void ToggleSelfTend() => SetSelfTend(!SelfTend);

    // 하수인 초기값. 목록이 비어 있으면 "전부 허용"(MinionData의 기존 규칙 그대로).
    //
    // **직접 배정 카테고리(채굴·벌목·경비)는 여기서 아무 뜻이 없다.** 그것들은 큐를 타지
    // 않으므로 켜고 끌 대상이 아니다 — 그런데 예전엔 목록에 그것들만 적혀 있어도
    // "목록이 비지 않았다"고 보고 **큐 작업을 전부 껐다.** `Goblin.AllowedJobs`가
    // [채굴, 벌목]이었던 탓에 하수인은 하나같이 운반·건설·제작을 안 했고, 남은 방랑자
    // 한 명이 온 짐을 날랐다("운반을 한 명만 한다"의 정체).
    //
    // 큐 카테고리가 하나도 없는 목록은 큐 작업에 대해 **아무 말도 하지 않은 것**이다.
    // 그럴 땐 빈 목록과 똑같이 전부 허용으로 본다.
    // **기본 꺼짐은 여기서도 지킨다.** `_disabled`를 비우고 다시 쌓는 자리라, 그냥 지우면
    // 하수인만 짐내림이 켜진 채로 태어난다(같은 설정이 종족에 따라 달라진다).
    // `MinionData.AllowedJobs`에 짐내림을 **적어 두면** 그때는 켜진다 — 저작이 기본값을 이긴다.
    public void InitFromAllowed(IReadOnlyList<JobCategory> allowed)
    {
        _disabled.Clear();
        if (allowed == null || allowed.Count == 0)
        {
            foreach (var c in DefaultOff) _disabled.Add(c);
            OnChanged?.Invoke();
            return;
        }

        bool mentionsQueue = false;
        for (int i = 0; i < allowed.Count; i++)
            if (JobCategoryInfo.IsQueued(allowed[i])) { mentionsQueue = true; break; }

        if (!mentionsQueue)
        {
            Debug.LogWarning($"[WorkSettings] {name}: 허용 작업 목록에 큐 작업이 하나도 없습니다 " +
                             "(채굴·벌목·경비는 직접 배정이라 여기서 켜고 끄는 대상이 아닙니다). " +
                             "전부 허용으로 둡니다 — 의도한 제한이 있다면 MinionData에 큐 작업으로 적으세요.");
            foreach (var c in DefaultOff) _disabled.Add(c);
            OnChanged?.Invoke();
            return;
        }

        foreach (var category in JobCategoryInfo.QueueOrder)
            if (!Contains(allowed, category))
                _disabled.Add(category);

        OnChanged?.Invoke();
    }

    static bool Contains(IReadOnlyList<JobCategory> list, JobCategory category)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == category) return true;
        return false;
    }
}
