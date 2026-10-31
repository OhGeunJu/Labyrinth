using System.Collections.Generic;
using UnityEngine;

// **근접 공격자가 표적 둘레 어디에 서나 — 답은 여기 하나다** (2026-10-12)
//
// 전에는 근접 공격자가 전부 **한 점**으로 걸었다 — 폰이면 중심, 건물이면 저마다 가장 가까운 면
// (`BTTargetUtil.ApproachPoint`). 간격을 벌리는 것은 콜라이더뿐이라, 셋이 한 표적을 물면 다가온 쪽에
// 뭉치고 뒤에 온 놈은 앞 놈 등을 밀며 헛걸었다.
//
// 표적 둘레에 자리를 놓고 공격자가 하나씩 맡는다. **가까운 자리를 먼저 보고, 차 있으면 좌우를 번갈아**
// 다음 자리를 본다.
//  · 폰 — 둘레 원 위에 여덟. 사거리 안쪽 거리다.
//  · 건물 — 발자국 둘레를 따라 한 줄로(`Spacing`마다 하나). 긴 벽 앞에서는 옆으로 퍼져 선다.
//
// **자리 잡은 놈을 건드리지 않는다**(사용자: *"중심으로 가기위해 여덟 명을 건들여? 그러면 안되는데"*) —
//  · 다 차면 밀고 들어가지 않고 **뒷줄에서 기다린다**. 뒷줄도 자리를 나눈다 — 안 나누면 같은 쪽에서 온
//    놈들이 대기 자리 한 점에서 저희끼리 밀친다.
//  · 먼 쪽 자리로 갈 때 앞줄을 가로지르지 않고 **바깥으로 돌아간다**.
//
// 예약은 **틱마다 다시 적어야 산다**(`Expire`) — 죽음·기절·표적 바꾸기·명령 등 놓는 길이 많아서, 그 길마다
// 푸는 줄을 넣으면 하나가 빠질 때 자리가 영영 잠긴다. 안 적으면 저절로 풀리고, 뒤에서 기다리던 놈이 든다.
//
// **설 자리가 하나도 없으면 옛 답으로 돌아간다**(전부 바위·건물에 걸린 표적) — 기다리게 두면 영영 안 친다.
// 원거리는 안 탄다(`Applies`). 소집 중 목줄은 `FightNode.Approach`가 이 답 위에 그대로 건다.
public static class MeleeSlots
{
    const int   Front   = 8;     // 폰 둘레의 치는 자리
    const int   Back    = 16;    // 폰 둘레의 기다리는 자리
    // 건물 둘레에서 자리 사이 간격(m) — 몸 하나에 틈 조금.
    const float Spacing = 1.0f;
    // 사거리의 이만큼 안쪽에 선다 — 사거리 끝에 세우면 표적이 반 걸음만 움직여도 다시 쫓는다.
    const float Inset   = 0.8f;
    // 뒷줄까지의 거리(m). 자리 잡은 놈의 몸 하나 뒤다.
    const float Hold    = 1.2f;
    // 이만큼(초) 다시 안 적은 예약은 빈자리다. BT 틱(0.1초) 다섯 번 — 쓰러진 놈의 자리가 곧 넘어간다.
    const float Expire  = 0.5f;
    // 건물 발자국을 다시 재는 주기(초) — 잴 때마다 콜라이더를 훑는다.
    const float Remeasure = 1f;

    class Band
    {
        public readonly int          N;
        public readonly GameObject[] Who;
        public readonly float[]      At;
        public readonly bool[]       Arrived;   // 사거리 안에 서서 치고 있다
        public readonly Vector3[]    Pts;       // 이번에 잰 자리(표적이 움직이면 같이 움직인다)

        public Band(int n)
        {
            N = n; Who = new GameObject[n]; At = new float[n]; Arrived = new bool[n]; Pts = new Vector3[n];
        }

        public int Wrap(int i) => ((i % N) + N) % N;

        public int Nearest(Vector3 from)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < N; i++)
            {
                float dx = Pts[i].x - from.x, dz = Pts[i].z - from.z, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public int Mine(GameObject self)
        {
            for (int i = 0; i < N; i++) if (Who[i] == self) return i;
            return -1;
        }

        public bool Free(int i, GameObject self)
        {
            var who = Who[i];
            return who == null || who == self || !who.activeInHierarchy || Time.time - At[i] > Expire;
        }

        public void Take(int i, GameObject self, bool arrived)
        {
            Who[i] = self; At[i] = Time.time; Arrived[i] = arrived;
        }

        public void Drop(GameObject self)
        {
            int i = Mine(self);
            if (i >= 0) Who[i] = null;
        }
    }

    class Ring
    {
        public Band   In, Out;
        public bool   Building;
        public Bounds Box;          // 건물의 발자국
        public float  MeasuredAt = -999f;
        public Vector3 Center;      // 폰의 자리(틱마다 다시 적는다)
    }

    static readonly Dictionary<GameObject, Ring>       _rings    = new();
    static readonly Dictionary<GameObject, GameObject> _targetOf = new();   // 공격자 → 표적
    static readonly List<GameObject> _dead = new();
    static int _calls;

    // 도메인 리로드를 끄면 지난 판의 예약을 들고 시작한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { _rings.Clear(); _targetOf.Clear(); _calls = 0; }

    public static bool Applies(UnitBlackboard bb, GameObject target) => !bb.RangedWeapon;

    // 사거리 밖에서 부른다 — 어디로 걸어가나.
    public static Vector3 Approach(UnitBlackboard bb, GameObject target)
    {
        var self = bb.Self;
        var from = self.transform.position;
        var ring = RingOf(self, target);

        // 큰 표적은 둘레만큼 물러선다(`BodySize.Girth`) — 거리도 둘레에서 잰다(`BTTargetUtil.DistanceTo`).
        float off = bb.AttackRange * Inset + BodySize.Girth(target);
        Lay(ring, ref ring.In, off, Front);

        var band = ring.In;
        int pref = band.Nearest(from);
        int mine = band.Mine(self);

        int chosen = Pick(ring, band, pref, self, front: true, out bool anyOpen);
        if (mine >= 0 && mine != chosen) band.Who[mine] = null;

        if (chosen < 0)
        {
            // 설 수 있는 자리가 아예 없는 표적 — 기다리면 영영 안 친다. 옛 답으로 간다.
            if (!anyOpen)
            {
                ring.Out?.Drop(self);
                return BTTargetUtil.ApproachPoint(from, target);
            }
            // **다 찼다 — 밀고 들어가지 않는다.** 뒷줄에서 제 자리를 잡고 기다린다.
            return Wait(ring, self, from, off);
        }

        ring.Out?.Drop(self);
        band.Take(chosen, self, arrived: false);

        // **먼 쪽 자리는 바깥으로 돌아간다** — 곧장 가면 길이 앞줄을 가로질러 자리 잡은 놈들을 밀친다.
        int turn = band.Wrap(chosen - pref);
        if (turn > band.N / 2) turn -= band.N;
        if (Mathf.Abs(turn) > 1)
        {
            var next  = band.Pts[band.Wrap(pref + (turn > 0 ? 1 : -1))];
            var orbit = next + Outward(ring, next) * Hold;
            return Standable(ring, orbit) ? orbit : from;
        }
        return band.Pts[chosen];
    }

    // 사거리 안에 서서 칠 때 부른다 — **선 자리가 곧 자리다.** 걸어오는 중인 남의 예약은 밀어낸다
    // (그쪽은 다음 틱에 다른 자리를 고른다). 이미 서서 치는 놈의 자리는 안 뺏는다.
    public static void Arrive(UnitBlackboard bb, GameObject target)
    {
        var self = bb.Self;
        var ring = RingOf(self, target);
        Lay(ring, ref ring.In, bb.AttackRange * Inset + BodySize.Girth(target), Front);
        ring.Out?.Drop(self);

        var band = ring.In;
        int here = band.Nearest(self.transform.position);
        int mine = band.Mine(self);

        if (here != mine)
        {
            // 남이 서서 치는 자리에 끼어 섰으면 맡아 둔 자리를 내놓는다 — 안 서 있는 자리를 잠그지 않는다.
            if (mine >= 0) band.Who[mine] = null;
            if (!band.Free(here, self) && band.Arrived[here]) return;
        }
        band.Take(here, self, arrived: true);
    }

    // 싸움이 끝났다 — 바로 비운다(안 불러도 `Expire` 뒤에 풀린다).
    public static void Release(GameObject self)
    {
        if (!_targetOf.TryGetValue(self, out var target)) return;
        _targetOf.Remove(self);
        if (target == null || !_rings.TryGetValue(target, out var ring)) return;
        ring.In?.Drop(self);
        ring.Out?.Drop(self);
    }

    // 뒷줄의 대기 자리. 뒷줄도 다 찼으면 선 자리에서 기다린다.
    static Vector3 Wait(Ring ring, GameObject self, Vector3 from, float off)
    {
        Lay(ring, ref ring.Out, off + Hold, Back);

        var band = ring.Out;
        int mine = band.Mine(self);
        int chosen = Pick(ring, band, band.Nearest(from), self, front: false, out _);
        if (mine >= 0 && mine != chosen) band.Who[mine] = null;
        if (chosen < 0) return from;

        band.Take(chosen, self, arrived: false);
        return band.Pts[chosen];
    }

    // 가까운 자리 → 좌우 번갈아. 내 자리보다 가까운 빈자리가 생기면 그리로 옮긴다.
    static int Pick(Ring ring, Band band, int pref, GameObject self, bool front, out bool anyOpen)
    {
        anyOpen = false;
        for (int k = 0; k < band.N; k++)
        {
            int idx = band.Wrap(pref + ((k & 1) == 1 ? (k + 1) / 2 : -(k / 2)));
            if (Blocked(ring, band.Pts[idx], front)) continue;
            anyOpen = true;
            if (band.Free(idx, self)) return idx;
        }
        return -1;
    }

    // 자리를 놓는다 — 표적에서 `off`만큼 떨어진 둘레에 고르게.
    static void Lay(Ring ring, ref Band band, float off, int pawnCount)
    {
        if (!ring.Building)
        {
            band ??= new Band(pawnCount);
            for (int i = 0; i < band.N; i++)
            {
                float a = i * Mathf.PI * 2f / band.N;
                band.Pts[i] = ring.Center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * off;
            }
            return;
        }

        // 발자국을 `off`만큼 부풀린 네모의 둘레를 따라 걷는다. 모서리는 건물에서 `off`보다 멀어지므로
        // (대각선) 가장 가까운 면 쪽으로 당겨 둥글린다 — 안 당기면 모서리 자리에서는 사거리에 안 든다.
        var b = ring.Box;
        float w = b.size.x + off * 2f, h = b.size.z + off * 2f;
        // **자리 수는 공격자의 사거리를 안 탄다**(앞줄이냐 뒷줄이냐만 본다) — 타면 사거리가 다른 둘이 같은
        // 건물을 칠 때 틱마다 자리 수가 바뀌어 예약이 계속 날아간다. 거리만 각자 제 사거리로 선다.
        float girth  = pawnCount == Front ? Hold : Hold * 2f;
        float around = (b.size.x + b.size.z) * 2f + Mathf.PI * 2f * girth;
        int n = Mathf.Max(4, Mathf.FloorToInt(around / Spacing));

        // 건물이 옮겨지면 자리 수가 달라진다 — 그때는 예약을 새로 받는다.
        if (band == null || band.N != n) band = new Band(n);

        float x0 = b.min.x - off, z0 = b.min.z - off, x1 = b.max.x + off, z1 = b.max.z + off;
        float lap = (w + h) * 2f;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) * lap / n;
            Vector3 p = t < w         ? new Vector3(x0 + t, 0f, z0)
                      : t < w + h     ? new Vector3(x1, 0f, z0 + (t - w))
                      : t < w * 2 + h ? new Vector3(x1 - (t - w - h), 0f, z1)
                      :                 new Vector3(x0, 0f, z1 - (t - w * 2 - h));
            p.y = ring.Center.y;
            var q = Nearest(ring, p);
            var d = p - q;
            band.Pts[i] = d.sqrMagnitude > 0.0001f ? q + d.normalized * off : p;
        }
    }

    // 표적의 몸에서 그 점에 가장 가까운 곳 — 폰은 중심, 건물은 가장 가까운 면.
    static Vector3 Nearest(Ring ring, Vector3 p)
    {
        if (!ring.Building) return ring.Center;
        var q = ring.Box.ClosestPoint(new Vector3(p.x, ring.Box.center.y, p.z));
        q.y = p.y;
        return q;
    }

    static Vector3 Outward(Ring ring, Vector3 p)
    {
        var d = p - Nearest(ring, p);
        d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.zero;
    }

    static bool Standable(Ring ring, Vector3 p)
    {
        if (RockMap.IsRock(p)) return false;
        if (!ring.Building) return true;
        // 건물 둘레는 **남의 건물이 찬 칸**도 거른다 — 이어진 벽의 옆 칸에 자리가 놓인다.
        var map = MapManager.Instance;
        return map == null || map.IsWorkable(map.WorldToTile(p));
    }

    // 그 자리에 설 수 없나. 폰의 치는 자리는 표적과의 사이를 얇은 벽이 막는지도 본다.
    static bool Blocked(Ring ring, Vector3 p, bool front)
    {
        if (!Standable(ring, p)) return true;
        if (!front || ring.Building) return false;

        var map = MapManager.Instance;
        if (map == null) return false;
        var a = map.WorldToTile(ring.Center);
        var b = map.WorldToTile(p);
        if (Mathf.Abs(b.x - a.x) > 1 || Mathf.Abs(b.y - a.y) > 1) return false;
        return !EdgeWalls.StepOpen(a.x, a.y, b.x, b.y);
    }

    static Ring RingOf(GameObject self, GameObject target)
    {
        if ((++_calls & 1023) == 0) Sweep();

        if (!_targetOf.TryGetValue(self, out var old) || old != target)
        {
            Release(self);
            _targetOf[self] = target;
        }
        if (!_rings.TryGetValue(target, out var ring)) _rings[target] = ring = new Ring();

        ring.Center = target.transform.position;
        if (Time.time - ring.MeasuredAt > Remeasure)
        {
            ring.MeasuredAt = Time.time;
            ring.Building   = target.TryGetComponent<BuildingHealth>(out var health);
            if (ring.Building) ring.Box = health.Footprint();
        }
        return ring;
    }

    // 사라진 표적의 자리를 치운다 — 열쇠가 파괴된 오브젝트라 저절로는 안 빠진다.
    static void Sweep()
    {
        _dead.Clear();
        foreach (var kv in _rings) if (kv.Key == null) _dead.Add(kv.Key);
        for (int i = 0; i < _dead.Count; i++) _rings.Remove(_dead[i]);

        _dead.Clear();
        foreach (var kv in _targetOf) if (kv.Key == null || kv.Value == null) _dead.Add(kv.Key);
        for (int i = 0; i < _dead.Count; i++) _targetOf.Remove(_dead[i]);
    }
}
