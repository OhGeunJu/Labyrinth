using System.Collections.Generic;
using UnityEngine;

// **찍힌 칸과 채굴 일감을 맞춘다**(2026-08-29).
//
// ── 왜 필요한가 ────────────────────────────────────────────────────────────
//
// 찍을 때 일감을 한 번 내고 끝내면, **그 일감이 취소되는 순간 그 칸은 영영 안 파인다.**
// 폰이 못 가서 포기하면(`WorkNode`의 정체 판정) 일감은 사라지는데 **바위의 표시는 남고**,
// 찍은 쪽은 *"이미 냈다"*고 기억하고 있어서 다시 내지도 않는다 — 화면에는 **노랗게 찍힌
// 채로 아무도 안 오는 칸**으로만 보인다. 손으로 무르고 다시 찍는 수밖에 없다.
//
// 그런데 못 가는 것은 대개 **잠깐**이다: 아직 길이 안 뚫렸을 뿐이고, 앞을 파고 나면 갈 수
// 있다. 그러니 *"한 번 실패했으니 없던 일"*이 아니라 **나중에 다시 해 봐야** 한다.
//
// ── 찍은 것이 진짜다 ───────────────────────────────────────────────────────
//
// 그래서 축을 뒤집는다: **`RockMap`의 표시가 원본이고 일감은 거기서 파생된다.** 이 판이
// 주기적으로 둘을 맞춘다 — 표시가 있는데 일감이 없으면 낸다. 그러면 **어디서 취소되든**
// (폰이 포기하든, 다른 폰이 먼저 파든, 판이 바뀌든) 저절로 되돌아온다.
//
// **되풀이를 막는 것이 요점이다.** 정말로 못 가는 칸(사방이 안 뚫린 바위 속)을 곧바로 다시
// 내면 폰이 **가다 포기하기를 무한히 되풀이**한다. 그래서 취소된 칸은 **잠시 쉬게 둔다** —
// 그동안 다른 일을 하고, 앞이 뚫리면 그때 다시 붙는다.
public class MineJobBoard : MonoBehaviour
{
    // 취소된 칸을 다시 내기까지. **게임 시간이다** — 배속을 올리면 그만큼 빨리 다시 해 본다
    // (파는 속도도 같이 빨라지므로 축이 맞다).
    const float RetryDelay = 15f;

    // 얼마나 자주 맞추나. **매 프레임 안 한다** — 찍힌 칸이 수백이면 그만큼 훑는다.
    const float SweepInterval = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("MineJobBoard");
        _instance = go.AddComponent<MineJobBoard>();
        DontDestroyOnLoad(go);
    }

    static MineJobBoard _instance;

    // 지금 살아 있는 일감. **일감이 스스로 알려 주지 않으므로** 상태를 물어본다.
    readonly Dictionary<Vector2Int, MineJob> _live = new();

    // 이 시각 전에는 다시 안 낸다.
    readonly Dictionary<Vector2Int, float> _restUntil = new();

    float _nextSweep;

    // **찍은 직후에 바로 낸다** — 다음 훑기를 기다리면 지정하고 1초 동안 아무 일도 안 일어나
    // *"먹었나?"*가 된다. 찍는 쪽이 부른다.
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

        // **손을 뗄 때까지 기다린다**(2026-09-06). 드래그 중의 표시는 *미리보기*인데 여기서
        // 그것을 **결정으로 읽고 있었다** — 자리를 고르는 동안 폰이 벌써 캐러 갔다
        // (사용자: *"내가 채굴위치를 고민중인데 걍 바로 가서 채굴해버리니깐"*).
        //
        // **`Commit`이 있는데도 새어 나온 자리다** — 일감을 내는 문이 둘이었던 셈이다
        // (놓을 때의 `Nudge`와, 1초마다 도는 이 훑기). 훑기를 없앨 수는 없다: 찍힌 칸이
        // 나중에 파이거나 막혔다 풀리는 것을 여기가 돌본다. **그래서 미루기만 한다.**
        //
        // 이미 배정된 일감은 그대로 돈다 — 멈추는 것은 *새 일감을 내는 일*뿐이다.
        if (MineDesignator.IsDesignating) return;

        // ① 끝난 일감을 걷어낸다. **취소는 쉬게 두고, 완료는 그냥 잊는다.**
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
        // **찍힌 목록을 그대로 훑지 않는다** — 훑는 도중에 일감이 완료되며 `RockMap.Mine`이
        // 그 목록을 고치면 `InvalidOperationException`으로 죽는다.
        _pending.Clear();
        foreach (var t in RockMap.MarkedTiles) _pending.Add(t);

        foreach (var t in _pending)
        {
            if (_live.ContainsKey(t)) continue;
            if (!RockMap.IsRock(t)) { RockMap.Mark(t, false); continue; }   // 이미 파였다
            if (_restUntil.TryGetValue(t, out float until) && Time.time < until) continue;

            var job = new MineJob(t);
            jm.RegisterJob(job);
            _live[t] = job;
        }

        // ③ 표시가 풀린 칸의 기록을 지운다 — 안 지우면 사전이 판 내내 자란다.
        _done.Clear();
        foreach (var t in _restUntil.Keys)
            if (!RockMap.IsMarked(t)) _done.Add(t);
        foreach (var t in _done) _restUntil.Remove(t);
    }

    // 훑을 때마다 새로 만들지 않는다 — 1초에 한 번이라도 판 내내 쌓인다.
    readonly List<Vector2Int> _done    = new();
    readonly List<Vector2Int> _pending = new();
}
