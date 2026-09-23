using System.Collections.Generic;
using UnityEngine;

// **찍힌 칸과 땅 고르기 일감을 맞춘다**(2026-09-24).
//
// `MineJobBoard`와 **같은 모양이고 같은 이유다**: 찍을 때 일감을 한 번 내고 끝내면 그 일감이
// 취소되는 순간(폰이 못 가서 포기했다) **그 칸은 영영 안 고쳐진다** — 표시는 남았는데 내는
// 쪽은 *"이미 냈다"*고 기억한다. 그래서 축을 뒤집는다: **표시가 원본이고 일감은 파생이다.**
//
// ── 왜 `MineJobBoard`와 합치지 않았나 ──────────────────────────────────────
//
// 훑는 절차는 같지만 **읽는 자료가 다르다**(`RockMap` ↔ `GroundLevel`) — 합치려면 *"표시가
// 있나 · 아직 할 일인가 · 일감을 어떻게 만드나"* 셋을 델리게이트로 받아야 하는데, 그러면
// 판이 **무엇을 맞추고 있는지 읽을 수 없게 된다.** 대신 갈리는 값(쉬는 시간·훑는 주기)은
// 저쪽과 같은 값을 쓴다 — 어긋나면 *"채굴은 다시 붙는데 고르기는 안 붙는다"*가 된다.
public class LevelJobBoard : MonoBehaviour
{
    const float RetryDelay    = 15f;   // `MineJobBoard`와 같은 값
    const float SweepInterval = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        _instance = SelfStanding.Create<LevelJobBoard>("LevelJobBoard");
    }

    static LevelJobBoard _instance;

    readonly Dictionary<Vector2Int, LevelGroundJob> _live      = new();
    readonly Dictionary<Vector2Int, float>          _restUntil = new();

    float _nextSweep;

    // **찍은 직후에 바로 낸다** — 다음 훑기를 기다리면 *"먹었나?"*가 된다.
    public static void Nudge()
    {
        if (_instance != null) _instance.Sweep();
    }

    void Update()
    {
        if (Time.time < _nextSweep) return;
        _nextSweep = Time.time + SweepInterval;
        Sweep();
    }

    void Sweep()
    {
        var jm = JobManager.Instance;
        if (jm == null) return;

        // ⓪ **표시를 무른 칸의 일감은 거둔다 — 걸어오던 폰도 그 자리에서 멈춘다.**
        //    안 거두면 폰이 끝까지 걸어와 도착해서야 *"표시가 없다"*를 안다.
        //    **드래그 중이어도 돈다** — 무르는 것은 미리보기가 아니다.
        _done.Clear();
        foreach (var kv in _live)
        {
            var job = kv.Value;
            if (job == null || job.Status == JobStatus.Done || job.Status == JobStatus.Cancelled) continue;
            if (GroundLevel.IsMarked(kv.Key)) continue;
            jm.CancelJob(job);
            _done.Add(kv.Key);
        }
        foreach (var t in _done) { _live.Remove(t); _restUntil.Remove(t); }

        // **손을 뗄 때까지 새 일감을 안 낸다** — 드래그 중의 표시는 미리보기인데 그것을
        // 결정으로 읽으면 자리를 고르는 동안 폰이 벌써 삽질하러 간다(채굴이 겪은 그것).
        if (LevelDesignator.IsDesignating) return;

        // ① 끝난 일감을 걷어낸다. **취소는 쉬게 두고, 완료는 잊는다.**
        _done.Clear();
        foreach (var kv in _live)
        {
            var job = kv.Value;
            if (job == null) { _done.Add(kv.Key); continue; }

            if (job.Status == JobStatus.Cancelled)
            {
                _done.Add(kv.Key);
                _restUntil[kv.Key] = Time.time + RetryDelay;
            }
            else if (job.Status == JobStatus.Done)
            {
                _done.Add(kv.Key);
                _restUntil.Remove(kv.Key);
            }
        }
        foreach (var t in _done) _live.Remove(t);

        // ② 찍혔는데 일감이 없는 칸에 낸다.
        //
        // **찍힌 목록을 그대로 훑지 않는다** — 훑는 도중에 일감이 끝나며 `GroundLevel.Apply`가
        // 그 목록을 고치면 `InvalidOperationException`으로 죽는다.
        _pending.Clear();
        foreach (var t in GroundLevel.MarkedTiles) _pending.Add(t);

        foreach (var t in _pending)
        {
            if (_live.ContainsKey(t)) continue;
            if (!GroundLevel.TryTarget(t, out float target)) continue;

            // **이미 맞은 칸은 표시를 푼다** — 이웃 칸을 고르며 같이 맞는 일이 흔하다
            // (칸 하나를 덮는 정점이 최대 3×3이라 주변 6m가 따라온다). 안 풀면 **아무도
            // 안 오는 표시**가 남아 *"고르기가 안 된다"*로 보인다.
            if (GroundLevel.IsSettled(t, target) || !GroundLevel.CanLevel(t))
            {
                GroundLevel.Unmark(t);
                continue;
            }

            if (_restUntil.TryGetValue(t, out float until) && Time.time < until) continue;

            var job = new LevelGroundJob(t, target);
            jm.RegisterJob(job);
            _live[t] = job;
        }

        // ③ 표시가 풀린 칸의 기록을 지운다 — 안 지우면 사전이 판 내내 자란다.
        _done.Clear();
        foreach (var t in _restUntil.Keys)
            if (!GroundLevel.IsMarked(t)) _done.Add(t);
        foreach (var t in _done) _restUntil.Remove(t);
    }

    readonly List<Vector2Int> _done    = new();
    readonly List<Vector2Int> _pending = new();
}
