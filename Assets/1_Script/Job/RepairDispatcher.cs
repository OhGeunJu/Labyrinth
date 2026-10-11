using System.Collections.Generic;
using UnityEngine;

// **상한 건물에 수리 일감을 건다**(2026-09-30, `RepairJob`).
//
// - **핵 영역 안의 것만** — 마을·폐허처럼 저작된 남의 건물도 `BuildingHealth`를 갖지만 우리가 고칠 것이 아니다.
//   영역이 곧 *우리 거점*이다(`BuildingDormancy`가 쓰는 그 답).
// - **맞은 지 `Calm`초가 지나야** — 싸우는 도중에 폰이 벽에 매달려 망치질하면 방어가 무너진다. 조용해지면 고친다.
// - **건물마다 일감 하나** — 끝났거나 취소됐으면 다음 검사에서 다시 건다(재료가 모자라 기다리는 것도 그 일감이 든다).
//
// 매 프레임 훑지 않는다 — `TickSeconds`마다. 체력이 바뀌는 순간(`BuildingHealth.OnChanged`)에 맞은 시각만 적는다.
public class RepairDispatcher : MonoBehaviour
{
    const float TickSeconds = 3f;    // 실시간
    const float Calm        = 10f;   // 게임 초

    readonly Dictionary<BuildingHealth, RepairJob> _jobs = new();
    readonly Dictionary<BuildingHealth, float>     _hitAt = new();
    static readonly List<BuildingHealth> _gone = new();
    float _next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => SelfStanding.Create<RepairDispatcher>("RepairDispatcher");

    void OnEnable()  => BuildingHealth.OnChanged += OnChanged;
    void OnDisable() => BuildingHealth.OnChanged -= OnChanged;

    // 깎인 것만 적는다 — 고치는 중에 오르는 것은 싸움이 아니다.
    readonly Dictionary<BuildingHealth, float> _lastHp = new();
    void OnChanged(BuildingHealth hp)
    {
        if (hp == null) return;
        float now = hp.Current;
        if (!_lastHp.TryGetValue(hp, out float before) || now < before)
            _hitAt[hp] = GameTimeManager.Elapsed;
        _lastHp[hp] = now;
    }

    void Update()
    {
        if (!Application.isPlaying || JobManager.Instance == null) return;
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + TickSeconds;

        // 끝난 일감 · 사라진 건물을 거둔다.
        _gone.Clear();
        foreach (var kv in _jobs)
        {
            var j = kv.Value;
            if (kv.Key == null || j == null || j.Status == JobStatus.Done || j.Status == JobStatus.Cancelled)
                _gone.Add(kv.Key);
        }
        foreach (var k in _gone) _jobs.Remove(k);

        // **부서진·철거된 건물의 기록도 거둔다** — 맞은 시각과 마지막 체력은 `OnChanged`가 적기만 하고 지우는
        // 곳이 없어서, 습격으로 벽이 부서지고 다시 지어질 때마다 사라진 건물이 두 표에 쌓였다.
        Prune(_hitAt);
        Prune(_lastHp);

        float now = GameTimeManager.Elapsed;
        foreach (var hp in BuildingHealth.All)
        {
            if (hp == null || !hp.Damaged || _jobs.ContainsKey(hp)) continue;
            if (_hitAt.TryGetValue(hp, out float at) && now - at < Calm) continue;
            if (!InOurTerritory(hp.transform.position)) continue;

            var job = new RepairJob(hp);
            _jobs[hp] = job;
            JobManager.Instance.RegisterJob(job);
        }
    }

    static void Prune(Dictionary<BuildingHealth, float> map)
    {
        _gone.Clear();
        foreach (var k in map.Keys) if (k == null) _gone.Add(k);
        foreach (var k in _gone) map.Remove(k);
    }

    static bool InOurTerritory(Vector3 pos) => DungeonCore.AnyCovers(pos);
}
