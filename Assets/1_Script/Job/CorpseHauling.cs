using System.Collections.Generic;
using UnityEngine;

// **기지 안의 시체에 저장 구역 운반 일감을 건다**(2026-10-06, `StockZones` · `CorpseHaulJob`).
//
// - **핵 영역 안의 몸만** — 사냥터 끝에서 쓰러진 짐승을 업으러 세계 끝까지 걸어가지 않는다(사용자: *"시체가 맵
//   끝자락에 있어 갈거야 안갈거야?"*). 도축대 규칙도 같은 선이다(`ButcherTable.PickTarget`).
// - **내 편 시체는 안 옮긴다** — 동료는 무덤이나 화장터로 가는 몸이지 쌓아 둘 것이 아니다. 세력을 모르는 몸은
//   내 편이 아닌 것으로 본다(`ButcherBill.Matches`와 같은 규약 — `BecomeCorpse`가 `Faction`을 지운다).
// - **이미 구역 안에 누운 몸 · 업혀 있는 몸 · 도축 일감이 걸린 몸은 건너뛴다** — 도축대로 가는 몸을 구역으로
//   돌리면 두 폰이 한 시체를 두고 서로 빼앗는다.
//
// 매 프레임 훑지 않는다 — `TickSeconds`마다(시체는 맵에 수십 구다).
public class CorpseHauling : MonoBehaviour
{
    const float TickSeconds = 3f;   // 실시간

    static CorpseHauling _inst;
    readonly Dictionary<GameObject, CorpseHaulJob> _jobs = new();
    static readonly List<GameObject> _gone = new();
    float _next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => _inst = SelfStanding.Create<CorpseHauling>("CorpseHauling");

    // **구역으로 옮기는 중인 몸인가** — 도축대가 같은 몸을 고르지 않게 묻는다.
    public static bool IsHauling(GameObject body) =>
        _inst != null && body != null && _inst._jobs.TryGetValue(body, out var j) && Live(j);

    static bool Live(CorpseHaulJob j) =>
        j != null && j.Status != JobStatus.Done && j.Status != JobStatus.Cancelled;

    void Update()
    {
        if (!Application.isPlaying || JobManager.Instance == null) return;
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + TickSeconds;

        // 끝난 일감 · 사라진 몸을 거둔다.
        _gone.Clear();
        foreach (var kv in _jobs) if (kv.Key == null || !Live(kv.Value)) _gone.Add(kv.Key);
        foreach (var k in _gone) _jobs.Remove(k);

        if (!StockZones.Any()) return;

        foreach (var corpse in Corpse.All)
        {
            if (!Wants(corpse)) continue;

            var job = CorpseHaulJob.TryCreate(corpse);
            if (job == null) continue;   // 이 몸의 층에는 빈 칸이 없다 — 다른 층의 몸은 또 모른다

            _jobs[corpse.gameObject] = job;
            JobManager.Instance.RegisterJob(job);
        }
    }

    bool Wants(Corpse corpse)
    {
        if (corpse == null || corpse.Buried || corpse.transform.parent != null) return false;
        if (_jobs.ContainsKey(corpse.gameObject)) return false;

        var pos = corpse.transform.position;
        if (StockZones.Contains(pos)) return false;                    // 이미 구역에 누워 있다
        if (!DungeonCore.AnyCovers(pos)) return false;                 // 기지 밖 — 원정은 사람이 시킨다

        var faction = corpse.Faction;
        if (faction != null && faction.PlayerControlled) return false; // 동료는 무덤·화장터로

        var brain = corpse.GetComponent<UnitBrain>();
        if (brain == null || PawnCarry.IsCarried(brain)) return false;

        var mgr = JobManager.Instance;
        if (mgr != null && mgr.HasButcherJobFor(corpse.gameObject)) return false;
        return true;
    }
}
